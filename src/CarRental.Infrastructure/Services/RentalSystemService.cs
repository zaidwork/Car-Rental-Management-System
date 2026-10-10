using Microsoft.EntityFrameworkCore;
using CarRental.Core.Entities;
using CarRental.Core.Enums;
using CarRental.Core.Interfaces;
using CarRental.Infrastructure.Data;
using CarRental.Infrastructure.Helpers;

namespace CarRental.Infrastructure.Services;

public class RentalSystemService : IRentalSystemService
{
    private readonly IDbContextFactory<CarRentalDbContext> _dbFactory;

    public RentalSystemService(IDbContextFactory<CarRentalDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public static event Action? GlobalContractsDataChanged;

    public event Action? OnContractsDataChanged
    {
        add => GlobalContractsDataChanged += value;
        remove => GlobalContractsDataChanged -= value;
    }

    public void NotifyContractsDataChanged()
    {
        try
        {
            GlobalContractsDataChanged?.Invoke();
        }
        catch { }
    }

    private async Task<CarRentalDbContext> GetContextAsync()
    {
        return await _dbFactory.CreateDbContextAsync();
    }

    #region Unique Number Generators

    private static readonly SemaphoreSlim _sequenceLock = new(1, 1);
    private static int _lastContractSequence = 0;
    private static int _lastContractYear = 0;
    private static int _lastNoteSequence = 0;
    private static int _lastNoteYear = 0;
    private static int _lastReceiptSequence = 0;
    private static string _lastReceiptDatePrefix = "";

    private async Task<string> GenerateUniqueContractNumberAsync(CarRentalDbContext context)
    {
        await _sequenceLock.WaitAsync();
        try
        {
            var currentYear = DateTime.Now.Year;
            var yearPrefix = $"CR-{currentYear}-";

            if (_lastContractYear != currentYear || _lastContractSequence == 0)
            {
                var latestNumber = await context.Contracts
                    .AsNoTracking()
                    .Where(c => c.ContractNumber.StartsWith(yearPrefix))
                    .OrderByDescending(c => c.ContractNumber)
                    .Select(c => c.ContractNumber)
                    .FirstOrDefaultAsync();

                int maxSeq = 0;
                if (!string.IsNullOrEmpty(latestNumber))
                {
                    var parts = latestNumber.Split('-');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
                    {
                        maxSeq = seq;
                    }
                }
                _lastContractYear = currentYear;
                _lastContractSequence = maxSeq;
            }

            _lastContractSequence++;
            var candidate = $"{yearPrefix}{_lastContractSequence:D4}";
            while (await context.Contracts.AnyAsync(c => c.ContractNumber == candidate))
            {
                _lastContractSequence++;
                candidate = $"{yearPrefix}{_lastContractSequence:D4}";
            }
            return candidate;
        }
        finally
        {
            _sequenceLock.Release();
        }
    }

    private async Task<string> GenerateUniqueNoteNumberAsync(CarRentalDbContext context)
    {
        await _sequenceLock.WaitAsync();
        try
        {
            var currentYear = DateTime.Now.Year;
            var yearPrefix = $"PN-{currentYear}-";

            if (_lastNoteYear != currentYear || _lastNoteSequence == 0)
            {
                var latestNumber = await context.PromissoryNotes
                    .AsNoTracking()
                    .Where(p => p.NoteNumber.StartsWith(yearPrefix))
                    .OrderByDescending(p => p.NoteNumber)
                    .Select(p => p.NoteNumber)
                    .FirstOrDefaultAsync();

                int maxSeq = 0;
                if (!string.IsNullOrEmpty(latestNumber))
                {
                    var parts = latestNumber.Split('-');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
                    {
                        maxSeq = seq;
                    }
                }
                _lastNoteYear = currentYear;
                _lastNoteSequence = maxSeq;
            }

            _lastNoteSequence++;
            var candidate = $"{yearPrefix}{_lastNoteSequence:D4}";
            while (await context.PromissoryNotes.AnyAsync(p => p.NoteNumber == candidate))
            {
                _lastNoteSequence++;
                candidate = $"{yearPrefix}{_lastNoteSequence:D4}";
            }
            return candidate;
        }
        finally
        {
            _sequenceLock.Release();
        }
    }

    private async Task<string> GenerateUniqueReceiptNumberAsync(CarRentalDbContext context, string prefix)
    {
        await _sequenceLock.WaitAsync();
        try
        {
            var datePrefix = $"{prefix}-{DateTime.Now:yyyyMMdd}-";

            if (_lastReceiptDatePrefix != datePrefix || _lastReceiptSequence == 0)
            {
                var latestNumber = await context.FinancialTransactions
                    .AsNoTracking()
                    .Where(t => t.ReceiptNumber.StartsWith(datePrefix))
                    .OrderByDescending(t => t.ReceiptNumber)
                    .Select(t => t.ReceiptNumber)
                    .FirstOrDefaultAsync();

                int maxSeq = 0;
                if (!string.IsNullOrEmpty(latestNumber))
                {
                    var parts = latestNumber.Split('-');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
                    {
                        maxSeq = seq;
                    }
                }
                _lastReceiptDatePrefix = datePrefix;
                _lastReceiptSequence = maxSeq;
            }

            _lastReceiptSequence++;
            var candidate = $"{datePrefix}{_lastReceiptSequence:D4}";
            while (await context.FinancialTransactions.AnyAsync(t => t.ReceiptNumber == candidate))
            {
                _lastReceiptSequence++;
                candidate = $"{datePrefix}{_lastReceiptSequence:D4}";
            }
            return candidate;
        }
        finally
        {
            _sequenceLock.Release();
        }
    }

    #endregion

    // ==================== Dashboard & Overview ====================

    public async Task<DashboardSummaryDto> GetDashboardDataAsync()
    {
        await using var context = await GetContextAsync();

        var vehicles = await context.Vehicles.AsNoTracking().ToListAsync();
        var activeContracts = await context.Contracts
            .AsNoTracking()
            .Include(c => c.Customer)
            .Include(c => c.Vehicle)
            .Include(c => c.PromissoryNote)
            .Where(c => c.Status == ContractStatus.Active)
            .ToListAsync();

        var blacklistedCount = await context.BlacklistRecords.CountAsync(b => b.IsBlocked);

        var todayStart = DateTime.Today;
        var todayReceipts = await context.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Receipt && t.TransactionDateTime >= todayStart)
            .Select(t => t.Amount)
            .ToListAsync();
        var todayRevenue = todayReceipts.Sum();

        var criticalAlerts = new List<CriticalAlertDto>();
        int alertId = 1;

        foreach (var v in vehicles)
        {
            if (v.Status == VehicleStatus.InMaintenance)
            {
                criticalAlerts.Add(new CriticalAlertDto
                {
                    Id = alertId++,
                    Title = $"مركبة متوقفة بالصيانة ({v.Brand} {v.Model})",
                    Subtitle = $"المركبة متوقفة وممنوعة من التأجير: {v.CriticalMaintenanceReason ?? "صيانة إدارية"}",
                    Severity = "danger",
                    Icon = "wrench",
                    VehicleId = v.Id
                });
            }
            else if (v.IsBlockedByCriticalMaintenance)
            {
                criticalAlerts.Add(new CriticalAlertDto
                {
                    Id = alertId++,
                    Title = $"⛔ تجاوز حد الصيانة الحرجة ({v.Brand} {v.Model})",
                    Subtitle = $"تجاوزت العداد أو الموعد الحرج! متوقفة إجبارياً عن التأجير ({v.CriticalMaintenanceReason ?? "صيانة إلزامية"})",
                    Severity = "danger",
                    Icon = "alert",
                    VehicleId = v.Id
                });
            }

            if (v.DaysUntilLicenseExpiry <= 10)
            {
                criticalAlerts.Add(new CriticalAlertDto
                {
                    Id = alertId++,
                    Title = $"تنبيه الرخصة القريبة ({v.Brand} {v.Model})",
                    Subtitle = v.DaysUntilLicenseExpiry <= 0 ? "رخصة المركبة منتهية تماماً!" : $"ينتهي الترخيص خلال {v.DaysUntilLicenseExpiry} يوم (لوحة {v.PlateNumber})",
                    Severity = v.DaysUntilLicenseExpiry <= 0 ? "danger" : "warning",
                    Icon = "alert",
                    VehicleId = v.Id
                });
            }

            if (v.NextMaintenanceDate != default && v.NextMaintenanceDate.Year > 2000 && v.DaysUntilMaintenance <= 14)
            {
                criticalAlerts.Add(new CriticalAlertDto
                {
                    Id = alertId++,
                    Title = $"موعد الصيانة المجدولة ({v.Brand} {v.Model})",
                    Subtitle = v.DaysUntilMaintenance <= 0 
                        ? $"تجاوزت موعد الصيانة المحدد منذ {Math.Abs(v.DaysUntilMaintenance)} يوم (لوحة {v.PlateNumber})" 
                        : $"متبقي {v.DaysUntilMaintenance} يوم على موعد الصيانة المجدولة ({v.NextMaintenanceDate:yyyy/MM/dd})",
                    Severity = v.DaysUntilMaintenance <= 0 ? "danger" : "warning",
                    Icon = "wrench",
                    VehicleId = v.Id
                });
            }
        }

        return new DashboardSummaryDto
        {
            TotalVehicles = vehicles.Count,
            AvailableVehicles = vehicles.Count(v => v.Status == VehicleStatus.Available),
            RentedVehicles = vehicles.Count(v => v.Status == VehicleStatus.Rented),
            InMaintenanceVehicles = vehicles.Count(v => v.Status == VehicleStatus.InMaintenance),
            ActiveContractsCount = activeContracts.Count,
            BlacklistedCount = blacklistedCount,
            TodayRevenue = todayRevenue,
            FeaturedCars = vehicles.Take(4).ToList(),
            CriticalAlerts = criticalAlerts,
            TimelineContracts = activeContracts,
            AllVehicles = vehicles
        };
    }

    // ==================== Vehicles / Fleet ====================

    public async Task<List<Vehicle>> GetVehiclesAsync(string? searchTerm = null, VehicleStatus? status = null)
    {
        await using var context = await GetContextAsync();

        // فحص وتحديث تلقائي للمركبات المتجاوزة للحد الحرج لتحويلها للصيانة فوراً ومنع تأجيرها
        var overdueVehicles = await context.Vehicles
            .Where(v => v.IsActive && v.IsMaintenanceCritical && v.Status == VehicleStatus.Available)
            .ToListAsync();

        if (overdueVehicles.Any())
        {
            bool anyChanged = false;
            foreach (var ov in overdueVehicles)
            {
                if (ov.IsMaintenanceOverdue)
                {
                    ov.Status = VehicleStatus.InMaintenance;
                    anyChanged = true;
                }
            }
            if (anyChanged)
            {
                await context.SaveChangesAsync();
            }
        }

        var query = context.Vehicles.AsNoTracking().Where(v => v.IsActive).AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(v => v.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim().ToLower();
            query = query.Where(v => v.PlateNumber.ToLower().Contains(searchTerm) ||
                                     v.Brand.ToLower().Contains(searchTerm) ||
                                     v.Model.ToLower().Contains(searchTerm) ||
                                     v.VinNumber.ToLower().Contains(searchTerm));
        }

        return await query.OrderBy(v => v.Brand).ToListAsync();
    }

    public async Task<Vehicle?> GetVehicleByIdAsync(Guid id)
    {
        await using var context = await GetContextAsync();
        return await context.Vehicles
            .AsNoTracking()
            .Include(v => v.Contracts)
            .Include(v => v.MaintenanceRecords)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<Vehicle> SaveVehicleAsync(Vehicle vehicle, string username)
    {
        await using var context = await GetContextAsync();
        var cleanPlate = vehicle.PlateNumber.Trim();
        var isNew = vehicle.Id == Guid.Empty || !await context.Vehicles.AnyAsync(v => v.Id == vehicle.Id);

        string? oldPlateNumber = null;

        if (isNew)
        {
            if (await context.Vehicles.AnyAsync(v => v.PlateNumber == cleanPlate))
            {
                throw new InvalidOperationException($"رقم اللوحة ({cleanPlate}) مسجل مسبقاً لمركبة أخرى في الأسطول!");
            }

            if (vehicle.Id == Guid.Empty) vehicle.Id = Guid.NewGuid();
            vehicle.PlateNumber = cleanPlate;
            await context.Vehicles.AddAsync(vehicle);
            await LogAuditAsync(username, UserRole.Admin, AuditAction.Insert, "Vehicles", vehicle.Id.ToString(), $"إضافة مركبة جديدة {vehicle.Brand} {vehicle.Model} لوحة {vehicle.PlateNumber}");
        }
        else
        {
            var existing = await context.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicle.Id);
            if (existing == null) throw new InvalidOperationException("المركبة غير موجودة في قاعدة البيانات!");

            if (await context.Vehicles.AnyAsync(v => v.PlateNumber == cleanPlate && v.Id != vehicle.Id))
            {
                throw new InvalidOperationException($"رقم اللوحة ({cleanPlate}) مسجل مسبقاً لمركبة أخرى!");
            }

            oldPlateNumber = existing.PlateNumber;

            existing.Brand = vehicle.Brand;
            existing.Model = vehicle.Model;
            existing.ModelYear = vehicle.ModelYear;
            existing.Color = vehicle.Color;
            existing.PlateNumber = cleanPlate;
            existing.VinNumber = vehicle.VinNumber;
            existing.DailyStandardRate = vehicle.DailyStandardRate;
            existing.CurrentOdometer = vehicle.CurrentOdometer;
            existing.LicenseExpiryDate = vehicle.LicenseExpiryDate;
            existing.InsuranceExpiryDate = vehicle.InsuranceExpiryDate;
            existing.InsuranceCompany = vehicle.InsuranceCompany;
            existing.NextOilChangeOdometer = vehicle.NextOilChangeOdometer;
            existing.NextMaintenanceDate = vehicle.NextMaintenanceDate;
            existing.IsMaintenanceCritical = vehicle.IsMaintenanceCritical;
            existing.CriticalMaintenanceReason = vehicle.CriticalMaintenanceReason;
            existing.ImageUrl = vehicle.ImageUrl;
            existing.Status = vehicle.Status;
            existing.IsActive = vehicle.IsActive;

            await LogAuditAsync(username, UserRole.Admin, AuditAction.Update, "Vehicles", vehicle.Id.ToString(), $"تحديث بيانات المركبة {vehicle.Brand} {vehicle.Model} لوحة {vehicle.PlateNumber}");
        }

        try
        {
            await context.SaveChangesAsync();
            CloudSyncService.TriggerSyncVehicle(vehicle, oldPlateNumber);
            NotifyContractsDataChanged();
        }
        catch (DbUpdateException dbEx)
        {
            var detail = dbEx.InnerException?.Message ?? dbEx.Message;
            throw new InvalidOperationException($"فشل حفظ بيانات المركبة: {detail}", dbEx);
        }
        return vehicle;
    }

    public async Task DeleteVehicleAsync(Guid id, string username)
    {
        await using var context = await GetContextAsync();
        var vehicle = await context.Vehicles
            .Include(v => v.Contracts)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vehicle != null)
        {
            var hasActiveContracts = vehicle.Contracts.Any(c => c.Status == ContractStatus.Active);
            if (hasActiveContracts)
            {
                throw new InvalidOperationException("لا يمكن حذف المركبة لأنها مؤجرة حالياً بعقد نشط! يجب استلام السيارة وإنهاء العقد أولاً.");
            }

            // إذا لم يكن للمركبة أي عقود على الإطلاق، يتم حذفها نهائياً مع السجلات الملحقة بها
            if (!vehicle.Contracts.Any())
            {
                var inspections = await context.VehicleInspections.Where(i => i.VehicleId == id).ToListAsync();
                if (inspections.Any()) context.VehicleInspections.RemoveRange(inspections);

                var maintenances = await context.MaintenanceRecords.Where(m => m.VehicleId == id).ToListAsync();
                if (maintenances.Any()) context.MaintenanceRecords.RemoveRange(maintenances);

                var violations = await context.TrafficViolations.Where(t => t.VehicleId == id).ToListAsync();
                if (violations.Any()) context.TrafficViolations.RemoveRange(violations);

                context.Vehicles.Remove(vehicle);
                await LogAuditAsync(username, UserRole.Admin, AuditAction.Delete, "Vehicles", id.ToString(), $"حذف نهائي للمركبة {vehicle.Brand} {vehicle.Model} (لوحة: {vehicle.PlateNumber})");
                CloudSyncService.TriggerSyncDeleteVehicle(vehicle.PlateNumber);
            }
            else
            {
                // إذا كان للمركبة عقود تاريخية منتهية، يتم إلغاء تفعيلها للحفاظ على السجلات المالية والتاريخية
                vehicle.IsActive = false;
                vehicle.Status = VehicleStatus.OutOfService;
                await LogAuditAsync(username, UserRole.Admin, AuditAction.Delete, "Vehicles", id.ToString(), $"أرشفة وإلغاء تفعيل المركبة {vehicle.Brand} {vehicle.Model} (لوحة: {vehicle.PlateNumber})");
                CloudSyncService.TriggerSyncVehicle(vehicle);
            }

            await context.SaveChangesAsync();
        }
    }

    // ==================== Customers & Blacklist ====================

    public async Task<List<Customer>> GetCustomersAsync(string? searchTerm = null)
    {
        await using var context = await GetContextAsync();
        var query = context.Customers
            .AsNoTracking()
            .Include(c => c.BlacklistRecord)
            .Include(c => c.Contracts)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.Trim().ToLower();
            query = query.Where(c => c.FullName.ToLower().Contains(searchTerm) ||
                                     c.NationalIdOrPassport.Contains(searchTerm) ||
                                     c.LicenseNumber.Contains(searchTerm) ||
                                     c.PrimaryPhone.Contains(searchTerm));
        }

        var customers = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        var customerIds = customers.Select(c => c.Id).ToList();
        var violList = await context.TrafficViolations
            .AsNoTracking()
            .Where(v => !v.IsCollectedFromCustomer && v.CustomerId.HasValue && customerIds.Contains(v.CustomerId.Value))
            .Select(v => new { CustomerId = v.CustomerId!.Value, v.Amount })
            .ToListAsync();

        var pendingViolations = violList
            .GroupBy(v => v.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(v => v.Amount));

        foreach (var cust in customers)
        {
            decimal liveDebts = 0;
            if (cust.Contracts != null)
            {
                foreach (var c in cust.Contracts.Where(co => co.Status != ContractStatus.Cancelled))
                {
                    if (c.IsOpenEnded && c.Status == ContractStatus.Active)
                    {
                        liveDebts += c.OpenRentalCurrentDue;
                    }
                    else
                    {
                        liveDebts += Math.Max(0, c.RemainingAmount);
                    }
                }
            }
            if (pendingViolations.TryGetValue(cust.Id, out var violDebt))
            {
                liveDebts += violDebt;
            }
            cust.TotalDebts = liveDebts;
        }
        return customers;
    }

    public async Task<Customer?> GetCustomerByIdAsync(Guid id)
    {
        await using var context = await GetContextAsync();
        var customer = await context.Customers
            .AsNoTracking()
            .Include(c => c.BlacklistRecord)
            .Include(c => c.Contracts)
                .ThenInclude(co => co.Vehicle)
            .Include(c => c.PromissoryNotes)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer != null)
        {
            decimal liveDebts = 0;
            if (customer.Contracts != null)
            {
                foreach (var c in customer.Contracts.Where(co => co.Status != ContractStatus.Cancelled))
                {
                    if (c.IsOpenEnded && c.Status == ContractStatus.Active)
                    {
                        liveDebts += c.OpenRentalCurrentDue;
                    }
                    else
                    {
                        liveDebts += Math.Max(0, c.RemainingAmount);
                    }
                }
            }
            var violList = await context.TrafficViolations
                .AsNoTracking()
                .Where(v => !v.IsCollectedFromCustomer && v.CustomerId == customer.Id)
                .Select(v => v.Amount)
                .ToListAsync();

            decimal pendingViolations = violList.Sum();

            customer.TotalDebts = liveDebts + pendingViolations;
        }
        return customer;
    }

    public async Task<CustomerCheckResultDto> CheckCustomerEligibilityAsync(string nationalId)
    {
        await using var context = await GetContextAsync();
        nationalId = nationalId.Trim();

        var customer = await context.Customers
            .AsNoTracking()
            .Include(c => c.BlacklistRecord)
            .FirstOrDefaultAsync(c => c.NationalIdOrPassport == nationalId);

        if (customer == null)
        {
            return new CustomerCheckResultDto
            {
                Exists = false,
                Message = "العميل غير مسجل في النظام! شرط إلزامي: يجب تسجيل العميل أولاً قبل فتح أي عقد."
            };
        }

        // فحص القائمة السوداء
        var blacklist = await context.BlacklistRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.NationalId == nationalId && b.IsBlocked);

        if (blacklist != null)
        {
            return new CustomerCheckResultDto
            {
                Exists = true,
                Customer = customer,
                IsBlacklisted = true,
                BlacklistReason = blacklist.Reason,
                Message = $"🚫 تنبيه أمني قاطع: العميل مُدرج في القائمة السوداء! السبب: ({blacklist.Reason}). يمنع فتح العقد منعاً باتاً."
            };
        }

        // فحص رخصة القيادة
        if (customer.IsLicenseExpired)
        {
            return new CustomerCheckResultDto
            {
                Exists = true,
                Customer = customer,
                IsLicenseExpired = true,
                Message = $"⚠️ تنبيه: رخصة القيادة منتهية الصلاحية منذ {Math.Abs(customer.DaysUntilLicenseExpiry)} يوم! يمنع تسليم المركبة."
            };
        }

        // فحص السن القانوني
        if (customer.Age < 21)
        {
            return new CustomerCheckResultDto
            {
                Exists = true,
                Customer = customer,
                IsUnderage = true,
                Message = $"⚠️ تنبيه: عمر العميل ({customer.Age} سنة) أقل من السن القانوني المسموح للتأجير (21 سنة)."
            };
        }

        return new CustomerCheckResultDto
        {
            Exists = true,
            Customer = customer,
            IsBlacklisted = false,
            OutstandingDebts = customer.TotalDebts,
            Message = "✅ العميل مسجل ومؤهل للتأجير."
        };
    }

    public async Task<Customer> SaveCustomerAsync(Customer customer, string username)
    {
        await using var context = await GetContextAsync();
        var cleanId = customer.NationalIdOrPassport.Trim();
        var isNew = customer.Id == Guid.Empty || !await context.Customers.AnyAsync(c => c.Id == customer.Id);

        if (isNew)
        {
            if (await context.Customers.AnyAsync(c => c.NationalIdOrPassport == cleanId))
            {
                throw new InvalidOperationException($"رقم الهوية أو جواز السفر ({cleanId}) مسجل مسبقاً لعميل آخر!");
            }

            if (customer.Id == Guid.Empty) customer.Id = Guid.NewGuid();
            customer.NationalIdOrPassport = cleanId;
            customer.CreatedByUsername = username;
            customer.CreatedAt = DateTime.UtcNow;
            await context.Customers.AddAsync(customer);
            await LogAuditAsync(username, UserRole.Staff, AuditAction.Insert, "Customers", customer.Id.ToString(), $"تسجيل عميل جديد: {customer.FullName} هوية {customer.NationalIdOrPassport}");
        }
        else
        {
            var existing = await context.Customers.FirstOrDefaultAsync(c => c.Id == customer.Id);
            if (existing == null) throw new InvalidOperationException("العميل غير موجود في قاعدة البيانات!");

            if (await context.Customers.AnyAsync(c => c.NationalIdOrPassport == cleanId && c.Id != customer.Id))
            {
                throw new InvalidOperationException($"رقم الهوية أو جواز السفر ({cleanId}) مسجل مسبقاً لعميل آخر!");
            }

            existing.FullName = customer.FullName;
            existing.NationalIdOrPassport = cleanId;
            existing.PrimaryPhone = customer.PrimaryPhone.Trim();
            existing.EmergencyPhone = customer.EmergencyPhone?.Trim() ?? string.Empty;
            existing.LicenseNumber = customer.LicenseNumber.Trim();
            existing.LicenseExpiryDate = customer.LicenseExpiryDate;
            existing.DateOfBirth = customer.DateOfBirth;
            existing.Address = customer.Address;
            // الحفاظ على رصيد الذمم والديون الفعلي المحسوب من العمليات المالية والعقود
            // existing.TotalDebts لا يتم تعديله يدوياً من نموذج تعديل بيانات العميل لمنع تشويه الحسابات
            existing.Notes = customer.Notes;

            await LogAuditAsync(username, UserRole.Staff, AuditAction.Update, "Customers", customer.Id.ToString(), $"تعديل بيانات العميل: {customer.FullName}");
        }

        try
        {
            await context.SaveChangesAsync();
            CloudSyncService.TriggerSyncCustomer(customer);
        }
        catch (DbUpdateException dbEx)
        {
            var detail = dbEx.InnerException?.Message ?? dbEx.Message;
            throw new InvalidOperationException($"فشل حفظ بيانات العميل: {detail}", dbEx);
        }
        return customer;
    }

    public async Task DeleteCustomerAsync(Guid customerId, string username)
    {
        await using var context = await GetContextAsync();
        var customer = await context.Customers
            .Include(c => c.Contracts)
            .Include(c => c.BlacklistRecord)
            .Include(c => c.PromissoryNotes)
            .Include(c => c.TrafficViolations)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null) return;

        // 1. التحقق من عدم وجود عقود نشطة جارية حالياً
        if (customer.Contracts.Any(c => c.Status == ContractStatus.Active))
        {
            throw new InvalidOperationException($"لا يمكن حذف العميل '{customer.FullName}' لأن لديه عقد تأجير نشط حالياً (السيارة قيد الاستخدام)! يرجى تسليم وإغلاق العقد أولاً.");
        }

        var nationalId = customer.NationalIdOrPassport;
        var customerName = customer.FullName;

        // 2. إزالة سجلات الحظر والقائمة السوداء للعميل
        if (customer.BlacklistRecord != null)
        {
            context.BlacklistRecords.Remove(customer.BlacklistRecord);
        }
        var extraBlacklists = await context.BlacklistRecords
            .Where(b => b.CustomerId == customerId || b.NationalId == nationalId)
            .ToListAsync();
        if (extraBlacklists.Any())
        {
            context.BlacklistRecords.RemoveRange(extraBlacklists);
        }

        // 3. إزالة المعاملات المالية المرتبطة بحساب العميل
        var transactions = await context.FinancialTransactions
            .Where(t => t.CustomerId == customerId)
            .ToListAsync();
        if (transactions.Any())
        {
            context.FinancialTransactions.RemoveRange(transactions);
        }

        // 4. إزالة المخالفات المرورية المسجلة
        if (customer.TrafficViolations.Any())
        {
            context.TrafficViolations.RemoveRange(customer.TrafficViolations);
        }

        // 5. إزالة سندات الأمر والكمبيالات
        if (customer.PromissoryNotes.Any())
        {
            context.PromissoryNotes.RemoveRange(customer.PromissoryNotes);
        }

        // 6. إزالة العقود وفحوصاتها التابعة
        if (customer.Contracts.Any())
        {
            var contractIds = customer.Contracts.Select(c => c.Id).ToList();
            var inspections = await context.VehicleInspections
                .Where(i => contractIds.Contains(i.ContractId))
                .ToListAsync();
            if (inspections.Any())
            {
                context.VehicleInspections.RemoveRange(inspections);
            }

            context.Contracts.RemoveRange(customer.Contracts);
        }

        // 7. حذف سجل العميل الأساسي
        context.Customers.Remove(customer);

        await LogAuditAsync(username, UserRole.Admin, AuditAction.Delete, "Customers", customerId.ToString(), $"حذف العميل نهائياً: {customerName} (هوية: {nationalId})");
        await context.SaveChangesAsync();

        // 8. مزامنة الحذف مع السحابة وتطبيق الموبايل
        CloudSyncService.TriggerSyncDeleteCustomer(nationalId);

        // 9. إشعار الواجهات الحية فورياً
        NotifyContractsDataChanged();
    }

    public async Task<List<BlacklistRecord>> GetBlacklistRecordsAsync()
    {
        await using var context = await GetContextAsync();
        return await context.BlacklistRecords
            .AsNoTracking()
            .Include(b => b.Customer)
            .OrderByDescending(b => b.BlockedAt)
            .ToListAsync();
    }

    public async Task AddToBlacklistAsync(Guid customerId, string reason, string username)
    {
        await using var context = await GetContextAsync();
        var customer = await context.Customers.FindAsync(customerId);
        if (customer == null) return;

        var existing = await context.BlacklistRecords.FirstOrDefaultAsync(b => b.CustomerId == customerId);
        if (existing != null)
        {
            existing.IsBlocked = true;
            existing.Reason = reason;
            existing.BlockedAt = DateTime.UtcNow;
            existing.BlockedByUsername = username;
            existing.UnblockedAt = null;
            existing.UnblockedByUsername = null;
            existing.UnblockReason = null;
        }
        else
        {
            var record = new BlacklistRecord
            {
                CustomerId = customerId,
                NationalId = customer.NationalIdOrPassport,
                Reason = reason,
                IsBlocked = true,
                BlockedAt = DateTime.UtcNow,
                BlockedByUsername = username
            };
            await context.BlacklistRecords.AddAsync(record);
        }

        await LogAuditAsync(username, UserRole.Admin, AuditAction.Insert, "Blacklist", customerId.ToString(), $"إدراج العميل {customer.FullName} في القائمة السوداء: {reason}");
        await context.SaveChangesAsync();
    }

    public async Task UnblockCustomerAsync(Guid customerId, string unblockReason, string adminUsername)
    {
        await using var context = await GetContextAsync();
        var record = await context.BlacklistRecords
            .Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.CustomerId == customerId);

        if (record != null)
        {
            record.IsBlocked = false;
            record.UnblockedAt = DateTime.UtcNow;
            record.UnblockedByUsername = adminUsername;
            record.UnblockReason = unblockReason;

            await LogAuditAsync(adminUsername, UserRole.Admin, AuditAction.Unblock, "Blacklist", customerId.ToString(), $"فك الحظر عن العميل {record.Customer?.FullName}. السبب: {unblockReason}");
            await context.SaveChangesAsync();
        }
    }

    // ==================== Contracts & Promissory Notes ====================

    public async Task<List<Contract>> GetContractsAsync(ContractStatus? status = null)
    {
        await using var context = await GetContextAsync();
        var query = context.Contracts
            .AsNoTracking()
            .Include(c => c.Customer)
            .Include(c => c.Vehicle)
            .Include(c => c.PromissoryNote)
            .Include(c => c.Inspections)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        var contracts = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        foreach (var c in contracts)
        {
            if (c.IsOpenEnded && c.Status == ContractStatus.Active)
            {
                c.TotalAgreedAmount = c.OpenRentalAccruedAmount;
                c.RemainingAmount = c.OpenRentalCurrentDue;
            }
        }
        return contracts;
    }

    public async Task<Contract?> GetContractByIdAsync(Guid id)
    {
        await using var context = await GetContextAsync();
        var contract = await context.Contracts
            .AsNoTracking()
            .Include(c => c.Customer)
            .Include(c => c.Vehicle)
            .Include(c => c.PromissoryNote)
            .Include(c => c.Transactions)
            .Include(c => c.Inspections)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract != null && contract.IsOpenEnded && contract.Status == ContractStatus.Active)
        {
            contract.TotalAgreedAmount = contract.OpenRentalAccruedAmount;
            contract.RemainingAmount = contract.OpenRentalCurrentDue;
        }
        return contract;
    }

    public async Task<Contract> CreateContractAsync(Contract contract, string staffUsername)
    {
        await using var context = await GetContextAsync();

        // 0. حماية كاملة: تفريغ أي كائنات ملاحة منفصلة ممررة لمنع تصادم تتبع الكيانات في EF Core
        contract.Customer = null;
        contract.Vehicle = null;
        contract.PromissoryNote = null;
        contract.Transactions = new List<FinancialTransaction>();

        // 1. التحقق الإلزامي من وجود وأهلية العميل داخل السياق الحالي
        var customer = await context.Customers
            .Include(c => c.BlacklistRecord)
            .FirstOrDefaultAsync(c => c.Id == contract.CustomerId);

        if (customer == null)
        {
            throw new InvalidOperationException("العميل المحدد غير مسجل في النظام! شرط إلزامي: يجب تسجيل العميل أولاً.");
        }

        // فحص القائمة السوداء
        var blacklist = await context.BlacklistRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.NationalId == customer.NationalIdOrPassport && b.IsBlocked);

        if (blacklist != null)
        {
            throw new InvalidOperationException($"🚫 تنبيه أمني قاطع: العميل مُدرج في القائمة السوداء! السبب: ({blacklist.Reason}). يمنع فتح العقد منعاً باتاً.");
        }

        // فحص صلاحية الرخصة
        if (customer.IsLicenseExpired)
        {
            throw new InvalidOperationException($"⚠️ تنبيه: رخصة القيادة للعميل منتهية الصلاحية منذ {Math.Abs(customer.DaysUntilLicenseExpiry)} يوم! يمنع فتح العقد.");
        }

        // فحص السن القانوني
        if (customer.Age < 21)
        {
            throw new InvalidOperationException($"⚠️ تنبيه: عمر العميل ({customer.Age} سنة) أقل من السن القانوني المسموح للتأجير (21 سنة).");
        }

        // 2. التحقق من السيارة وحالتها داخل السياق الحالي
        var vehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.Id == contract.VehicleId);
        if (vehicle == null)
        {
            throw new InvalidOperationException("المركبة المختارة غير موجودة في أسطول النظام!");
        }

        if (vehicle.Status != VehicleStatus.Available || vehicle.IsBlockedByCriticalMaintenance)
        {
            var reason = vehicle.IsBlockedByCriticalMaintenance 
                ? $"متوقفة لتجاوز حد الصيانة الحرجة الإلزامية ({vehicle.CriticalMaintenanceReason ?? "صيانة إلزامية"})" 
                : $"حالتها الحالية: {vehicle.Status}";
            throw new InvalidOperationException($"المركبة ({vehicle.Brand} {vehicle.Model} لوحة {vehicle.PlateNumber}) غير صالحة للتأجير نهائياً ({reason})! يرجى تأكيد إنجاز الصيانة أولاً.");
        }

        // 3. إنشاء رقم العقد التسلسلي الفريد المضمون
        contract.ContractNumber = await GenerateUniqueContractNumberAsync(context);
        var noteNumber = await GenerateUniqueNoteNumberAsync(context);

        if (contract.Id == Guid.Empty) contract.Id = Guid.NewGuid();
        contract.StartOdometer = vehicle.CurrentOdometer;
        contract.Status = ContractStatus.Active;
        contract.CreatedByStaffName = staffUsername;
        contract.CreatedAt = DateTime.UtcNow;

        // ضمان النزاهة المالية للدفع الآجل والدفع الفوري (Financial Integrity Guard)
        if (contract.PaymentMode == PaymentMode.DeferredOnReturn)
        {
            // في الدفع الآجل (ذمم عند التسليم)، إذا وصل المبلغ المقبوض مساوياً لإجمالي العقد والمتبقي صفر
            // فهذا خطأ واجهة مستخدم؛ يتم تصفير المقبوض فوراً وترحيل كامل القيمة لذمة ودين العميل
            if (contract.RemainingAmount == 0 && contract.TotalAgreedAmount > 0 && contract.PaidAmount >= contract.TotalAgreedAmount)
            {
                contract.PaidAmount = 0;
            }
            contract.RemainingAmount = contract.IsOpenEnded ? 0 : Math.Max(0, contract.TotalAgreedAmount - contract.PaidAmount);
        }
        else
        {
            if (!contract.IsOpenEnded && contract.RemainingAmount == 0 && contract.PaidAmount < contract.TotalAgreedAmount)
            {
                contract.RemainingAmount = Math.Max(0, contract.TotalAgreedAmount - contract.PaidAmount);
            }
        }

        if (contract.OriginalAgreedAmount == 0)
        {
            contract.OriginalAgreedAmount = contract.TotalAgreedAmount + contract.DiscountAmount;
        }

        // إضافة العقد للسياق أولاً لضمان ترابط المفاتيح الأجنبية مع الكمبيالة والحركات المالية
        await context.Contracts.AddAsync(contract);

        // 4. إنشاء كمبيالة الضمان الرسمية (Promissory Note)
        var note = new PromissoryNote
        {
            Id = Guid.NewGuid(),
            NoteNumber = noteNumber,
            ContractId = contract.Id,
            CustomerId = customer.Id,
            Amount = contract.PromissoryNoteAmount,
            IssueDate = DateTime.Today,
            DueDate = contract.ExpectedReturnDateTime.Date,
            Status = PromissoryNoteStatus.HeldInTrust,
            LegalText = $"أتعهد أنا الموقع أدناه ({customer.FullName}) الحامل للهوية ({customer.NationalIdOrPassport}) بأن أدفع بموجب هذه الكمبيالة لأمر شركة تأجير السيارات مبلغ وقدره ({contract.PromissoryNoteAmount:N0} شيكل) كضمان للمركبة ({vehicle.Brand} {vehicle.Model} لوحة {vehicle.PlateNumber})."
        };
        await context.PromissoryNotes.AddAsync(note);
        contract.PromissoryNoteNumber = note.NoteNumber;

        // 5. تحديث حالة المركبة إلى مؤجرة
        vehicle.Status = VehicleStatus.Rented;

        // 6. ترحيل الذمم المتبقية إلى رصيد ديون العميل
        if (contract.RemainingAmount > 0)
        {
            customer.TotalDebts += contract.RemainingAmount;
        }

        // 7. تسجيل الدفعة النقدية في الصندوق والوردية المفتوحة
        if (contract.PaidAmount > 0)
        {
            var activeShift = await EnsureActiveShiftAsync(context, staffUsername);

            var receiptNumber = await GenerateUniqueReceiptNumberAsync(context, "REC");
            var transaction = new FinancialTransaction
            {
                Id = Guid.NewGuid(),
                ReceiptNumber = receiptNumber,
                DailyShiftId = activeShift.Id,
                ContractId = contract.Id,
                CustomerId = customer.Id,
                VehicleId = vehicle.Id,
                Type = TransactionType.Receipt,
                Category = TransactionCategory.RentalPayment,
                Amount = contract.PaidAmount,
                PaymentMethod = contract.PaymentMode,
                StaffUsername = staffUsername,
                TransactionDateTime = DateTime.Now,
                Description = $"قبض دفعة عقد إيجار {contract.ContractNumber} لسيارة {vehicle.Brand} {vehicle.Model}" + (contract.DiscountAmount > 0 ? $" (خصم: {contract.DiscountAmount:N0} ₪)" : "")
            };
            await context.FinancialTransactions.AddAsync(transaction);

            if (contract.PaymentMode == PaymentMode.ImmediateCash)
            {
                activeShift.SystemCalculatedCash += contract.PaidAmount;
            }
        }

        // 8. تسجيل التدقيق وحفظ العقد والعمليات
        await RecalculateCustomerDebtsInternalAsync(context, customer.Id);
        await LogAuditAsync(staffUsername, UserRole.Staff, AuditAction.Insert, "Contracts", contract.Id.ToString(), 
            $"إصدار عقد تأجير جديد {contract.ContractNumber} بمبلغ {contract.TotalAgreedAmount} شيكل وضمان {contract.PromissoryNoteAmount} شيكل للعميل {customer.FullName}");

        try
        {
            await context.SaveChangesAsync();
            CloudSyncService.TriggerSyncRentalCreated(contract, vehicle, customer);
            CloudSyncService.TriggerSyncCustomerDebt(customer.NationalIdOrPassport, customer.TotalDebts);
        }
        catch (DbUpdateException dbEx)
        {
            var detail = dbEx.InnerException?.Message ?? dbEx.Message;
            throw new InvalidOperationException($"فشل حفظ العقد في قاعدة البيانات: {detail}", dbEx);
        }

        NotifyContractsDataChanged();
        return contract;
    }

    public async Task<Contract> CloseContractAsync(Guid contractId, int endOdometer, FuelLevel endFuel, decimal extraPayment, string staffUsername)
    {
        return await CloseContractWithSettlementAsync(contractId, endOdometer, endFuel, 0, 0, 0, 0, extraPayment, null, staffUsername);
    }

    public async Task<Contract> CloseContractWithSettlementAsync(Guid contractId, int endOdometer, FuelLevel endFuel, decimal lateFee, decimal fuelFee, decimal extraKmFee, decimal damageFee, decimal amountPaidNow, string? settlementNotes, string staffUsername, PaymentMode paymentMethod = PaymentMode.ImmediateCash, decimal settlementDiscount = 0, int? billedDays = null, decimal customerRefundAmount = 0, bool refundFromCashDrawer = false)
    {
        await using var context = await GetContextAsync();
        var contract = await context.Contracts
            .Include(c => c.Vehicle)
            .Include(c => c.PromissoryNote)
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) throw new InvalidOperationException("العقد غير موجود!");

        if (contract.Status == ContractStatus.Closed)
        {
            throw new InvalidOperationException("هذا العقد مغلق ومستلم بالفعل مسبقاً!");
        }

        if (endOdometer < contract.StartOdometer)
        {
            throw new InvalidOperationException($"قراءة العداد عند الإرجاع ({endOdometer:N0} كم) لا يمكن أن تكون أقل من قراءة الاستلام ({contract.StartOdometer:N0} كم)!");
        }

        var originalAgreed = contract.TotalAgreedAmount;
        var originalRemaining = contract.RemainingAmount;

        // احتساب مبالغ التسوية الإضافية
        var totalSettlementFees = lateFee + fuelFee + extraKmFee + damageFee;
        contract.LateFee = lateFee;
        contract.FuelShortageFee = fuelFee;
        contract.ExtraKmFee = extraKmFee;
        contract.DamageFee = damageFee;
        contract.SettlementNotes = settlementNotes;
        contract.ActualReturnDateTime = DateTime.Now;

        if (settlementDiscount > 0)
        {
            contract.DiscountAmount += settlementDiscount;
        }

        if (contract.IsOpenEnded)
        {
            // احتساب أيام التأجير الفعلية المنقضية للعقد المفتوح
            var actualDays = billedDays ?? Math.Max(1, (int)Math.Ceiling((DateTime.Now - contract.StartDateTime).TotalDays));
            var totalRentalRent = actualDays * contract.DailyAgreedRate;
            contract.TotalAgreedAmount = Math.Max(0, (totalRentalRent + totalSettlementFees) - settlementDiscount);
            contract.PaidAmount += amountPaidNow;
            contract.RemainingAmount = Math.Max(0, contract.TotalAgreedAmount - contract.PaidAmount);

            // ترحيل المبلغ المتبقي (ذمم) على ديون العميل
            if (contract.Customer != null)
            {
                contract.Customer.TotalDebts = Math.Max(0, contract.Customer.TotalDebts + contract.RemainingAmount);
            }
        }
        else
        {
            // عقد عادي (محدد المدة) - فحص إذا تم تعديل أو سحب عدد أيام العقد (تسليم مبكر)
            if (billedDays.HasValue && billedDays.Value > 0)
            {
                var rentalRent = billedDays.Value * contract.DailyAgreedRate;
                contract.TotalAgreedAmount = Math.Max(0, (rentalRent + totalSettlementFees) - settlementDiscount);
            }
            else
            {
                var netAdditional = Math.Max(0, totalSettlementFees - settlementDiscount);
                contract.TotalAgreedAmount += netAdditional;
            }

            // معالجة المبالغ المدفوعة والمستردة
            if (customerRefundAmount > 0 && refundFromCashDrawer)
            {
                // تم استرداد مبلغ نقدي للعميل من الصندوق (كاش)
                contract.PaidAmount = Math.Max(0, contract.PaidAmount - customerRefundAmount);

                var activeShift = await EnsureActiveShiftAsync(context, staffUsername);
                var refundReceiptNumber = await GenerateUniqueReceiptNumberAsync(context, "RFD");
                var refundTransaction = new FinancialTransaction
                {
                    Id = Guid.NewGuid(),
                    ReceiptNumber = refundReceiptNumber,
                    DailyShiftId = activeShift.Id,
                    ContractId = contract.Id,
                    CustomerId = contract.CustomerId,
                    VehicleId = contract.VehicleId,
                    Type = TransactionType.Payment,
                    Category = TransactionCategory.Refund,
                    Amount = customerRefundAmount,
                    PaymentMethod = PaymentMode.ImmediateCash,
                    StaffUsername = staffUsername,
                    TransactionDateTime = DateTime.Now,
                    Description = $"استرداد نقدي لتسليم مبكر للعقد {contract.ContractNumber} للعميل {contract.Customer?.FullName} (سحب {billedDays ?? contract.RentalDays} يوم من أصل {contract.RentalDays} يوم)"
                };
                await context.FinancialTransactions.AddAsync(refundTransaction);
                activeShift.SystemCalculatedCash = Math.Max(0, activeShift.SystemCalculatedCash - customerRefundAmount);
            }
            else if (amountPaidNow > 0)
            {
                contract.PaidAmount += amountPaidNow;
            }

            contract.RemainingAmount = Math.Max(0, contract.TotalAgreedAmount - contract.PaidAmount);

            // تعديل ديون العميل بدقة بناءً على المتبقي الحقيقي وخصم أي رصيد دائن
            if (contract.Customer != null)
            {
                var nonCashCredit = (customerRefundAmount > 0 && !refundFromCashDrawer) ? customerRefundAmount : 0;
                contract.Customer.TotalDebts = Math.Max(0, contract.Customer.TotalDebts - originalRemaining + contract.RemainingAmount - nonCashCredit);
            }
        }

        contract.Status = ContractStatus.Closed;
        contract.EndOdometer = endOdometer;
        contract.FuelLevelEnd = endFuel;
        contract.ClosedByStaffName = staffUsername;
        contract.ClosedAt = DateTime.UtcNow;
        contract.ActualReturnDateTime = DateTime.Now;

        if (contract.IsOpenEnded)
        {
            var actualDays = billedDays ?? Math.Max(1, (int)Math.Ceiling((DateTime.Now - contract.StartDateTime).TotalDays));
            contract.ExpectedReturnDateTime = contract.StartDateTime.AddDays(actualDays);
        }
        else if (billedDays.HasValue && billedDays.Value > 0)
        {
            contract.ExpectedReturnDateTime = contract.StartDateTime.AddDays(billedDays.Value);
        }

        // تحرير المركبة وتحديث عدادها وفحص الصيانة الحرجة
        if (contract.Vehicle != null)
        {
            contract.Vehicle.CurrentOdometer = Math.Max(contract.Vehicle.CurrentOdometer, endOdometer);
            if (contract.Vehicle.IsBlockedByCriticalMaintenance)
            {
                contract.Vehicle.Status = VehicleStatus.InMaintenance;
            }
            else
            {
                contract.Vehicle.Status = VehicleStatus.Available;
            }
        }

        // الإفراج عن كمبيالة الضمان
        if (contract.PromissoryNote != null)
        {
            contract.PromissoryNote.Status = PromissoryNoteStatus.ReleasedToClient;
            contract.PromissoryNote.ReleasedAt = DateTime.UtcNow;
            contract.PromissoryNote.ReleasedByStaffName = staffUsername;
        }

        // تسجيل الدفعة المستلمة بطريقة الدفع المختارة إن وُجدت
        if (amountPaidNow > 0)
        {
            var activeShift = await EnsureActiveShiftAsync(context, staffUsername);

            var receiptNumber = await GenerateUniqueReceiptNumberAsync(context, "REC");
            var discountDesc = settlementDiscount > 0 ? $" - خصم تسوية: {settlementDiscount:N0} ₪" : "";
            var transaction = new FinancialTransaction
            {
                Id = Guid.NewGuid(),
                ReceiptNumber = receiptNumber,
                DailyShiftId = activeShift.Id,
                ContractId = contract.Id,
                CustomerId = contract.CustomerId,
                VehicleId = contract.VehicleId,
                Type = TransactionType.Receipt,
                Category = TransactionCategory.RentalPayment,
                Amount = amountPaidNow,
                PaymentMethod = paymentMethod,
                StaffUsername = staffUsername,
                TransactionDateTime = DateTime.Now,
                Description = $"تسوية نهائية وإغلاق العقد {(contract.IsOpenEnded ? "(مفتوح)" : "")} {contract.ContractNumber} (مدفوع: {amountPaidNow} ₪ - رسوم إضافية: {totalSettlementFees} ₪{discountDesc})"
            };
            await context.FinancialTransactions.AddAsync(transaction);

            if (paymentMethod == PaymentMode.ImmediateCash)
            {
                activeShift.SystemCalculatedCash += amountPaidNow;
            }
        }

        if (contract.Customer != null)
        {
            await RecalculateCustomerDebtsInternalAsync(context, contract.CustomerId);
            CloudSyncService.TriggerSyncCustomerDebt(contract.Customer.NationalIdOrPassport, contract.Customer.TotalDebts);
        }

        await LogAuditAsync(staffUsername, UserRole.Staff, AuditAction.ContractClose, "Contracts", contractId.ToString(), 
            $"إغلاق العقد {(contract.IsOpenEnded ? "(مفتوح)" : "")} {contract.ContractNumber} واستلام المركبة بقراءة عداد {endOdometer} كم ورسوم إضافية {totalSettlementFees} ₪ وسداد {amountPaidNow} ₪ والمتبقي {contract.RemainingAmount} ₪");

        await context.SaveChangesAsync();
        CloudSyncService.TriggerSyncRentalClosed(contract.Vehicle?.PlateNumber, contract.ContractNumber);
        NotifyContractsDataChanged();
        return contract;
    }

    public async Task<Contract> CancelContractAsync(Guid contractId, string staffUsername, string? cancellationReason = null)
    {
        await using var context = await GetContextAsync();
        var contract = await context.Contracts
            .Include(c => c.Vehicle)
            .Include(c => c.Customer)
            .Include(c => c.PromissoryNote)
            .Include(c => c.Transactions)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) throw new InvalidOperationException("عقد الإيجار المطلوب غير موجود في النظام!");
        if (contract.Status == ContractStatus.Cancelled) throw new InvalidOperationException("هذا العقد ملغي مسبقاً!");

        // 1. إعادة المركبة إلى الحالة المناسبة فوراً (متاحة أو بالصيانة إذا كانت متجاوزة للحد الحرج)
        if (contract.Vehicle != null)
        {
            contract.Vehicle.Status = contract.Vehicle.IsBlockedByCriticalMaintenance 
                ? VehicleStatus.InMaintenance 
                : VehicleStatus.Available;
        }

        // 2. إرجاع وإلغاء كمبيالة الضمان
        if (contract.PromissoryNote != null)
        {
            contract.PromissoryNote.Status = PromissoryNoteStatus.ReleasedToClient;
            contract.PromissoryNote.ReleasedAt = DateTime.UtcNow;
            contract.PromissoryNote.ReleasedByStaffName = staffUsername;
            contract.PromissoryNote.Notes = $"ألغي العقد بتاريخ {DateTime.Now:yyyy/MM/dd HH:mm} بواسطة {staffUsername}";
        }

        // 3. إلغاء أي ذمم متبقية تم تحميلها على حساب العميل لهذا العقد
        if (contract.Customer != null && contract.RemainingAmount > 0)
        {
            contract.Customer.TotalDebts = Math.Max(0, contract.Customer.TotalDebts - contract.RemainingAmount);
        }

        // 4. إلغاء القيود المالية بالكامل دون ترصيد أي حركة سلبية أو سند صرف في الصندوق (ولا كأنه شيء دخل ولا كأنه شيء طلع)
        var contractTransactions = await context.FinancialTransactions
            .Where(t => t.ContractId == contract.Id)
            .ToListAsync();

        foreach (var tx in contractTransactions)
        {
            // إعادة ضبط نقد الصندوق للوردية إذا كانت الحركة قبضا نقديا
            if (tx.Type == TransactionType.Receipt && tx.PaymentMethod == PaymentMode.ImmediateCash && tx.DailyShiftId.HasValue)
            {
                var shift = await context.DailyShifts.FirstOrDefaultAsync(s => s.Id == tx.DailyShiftId.Value);
                if (shift != null)
                {
                    shift.SystemCalculatedCash = Math.Max(0, shift.SystemCalculatedCash - tx.Amount);
                }
            }
        }

        // إذا كان هناك دفعة نقدية مسجلة في العقد ولم نجد سندها المالي في الجدول، نخصمها من الوردية المفتوحة لضمان سلامة النقد
        if (contract.PaidAmount > 0 && contract.PaymentMode == PaymentMode.ImmediateCash && !contractTransactions.Any(t => t.Type == TransactionType.Receipt))
        {
            var activeShift = await context.DailyShifts.FirstOrDefaultAsync(s => !s.IsClosed);
            if (activeShift != null)
            {
                activeShift.SystemCalculatedCash = Math.Max(0, activeShift.SystemCalculatedCash - contract.PaidAmount);
            }
        }

        // حذف كافة حركات العقد المالية تماماً من قاعدة البيانات لكي لا تظهر كقبض ولا كصرف ولا أي أرقام سالبة
        if (contractTransactions.Any())
        {
            context.FinancialTransactions.RemoveRange(contractTransactions);
        }

        // 5. تصفير مبالغ العقد وتحويل حالته إلى ملغي
        var originalAgreed = contract.TotalAgreedAmount;
        var originalPaid = contract.PaidAmount;
        contract.PaidAmount = 0;
        contract.RemainingAmount = 0;
        contract.TotalAgreedAmount = 0;
        contract.Status = ContractStatus.Cancelled;
        contract.ClosedAt = DateTime.UtcNow;
        contract.ClosedByStaffName = staffUsername;
        contract.Notes = (string.IsNullOrWhiteSpace(contract.Notes) ? "" : contract.Notes + " | ") + 
            $"[تم إلغاء العقد بالكامل واسترجاع المركبة وإلغاء كافة القيود المالية من الصندوق دون ترصيد أي حركة سلبية بتاريخ {DateTime.Now:yyyy/MM/dd HH:mm} بواسطة {staffUsername}. السبب: {cancellationReason ?? "بناءً على طلب العميل وفسخ العملية"}]";

        if (contract.Customer != null)
        {
            await RecalculateCustomerDebtsInternalAsync(context, contract.CustomerId);
            CloudSyncService.TriggerSyncCustomerDebt(contract.Customer.NationalIdOrPassport, contract.Customer.TotalDebts);
        }

        await context.SaveChangesAsync();
        CloudSyncService.TriggerSyncRentalClosed(contract.Vehicle?.PlateNumber, contract.ContractNumber);
        NotifyContractsDataChanged();
        return contract;
    }

    public async Task<FinancialTransaction> RecordContractPaymentAsync(Guid contractId, decimal amount, PaymentMode mode, string notes, string staffUsername)
    {
        await using var context = await GetContextAsync();
        var contract = await context.Contracts
            .Include(c => c.Customer)
            .Include(c => c.Vehicle)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) throw new InvalidOperationException("عقد الإيجار المطلوب غير موجود في النظام!");
        if (amount <= 0) throw new InvalidOperationException("المبلغ المقبوض يجب أن يكون أكبر من الصفر!");

        var activeShift = await EnsureActiveShiftAsync(context, staffUsername);
        var receiptNumber = await GenerateUniqueReceiptNumberAsync(context, "REC");

        // تطبيق الدفعة مباشرة على العقد
        contract.PaidAmount += amount;
        if (contract.IsOpenEnded && contract.Status == ContractStatus.Active)
        {
            contract.RemainingAmount = contract.OpenRentalCurrentDue;
        }
        else
        {
            contract.RemainingAmount = Math.Max(0, contract.TotalAgreedAmount - contract.PaidAmount);
        }

        var carInfo = contract.Vehicle != null ? $"{contract.Vehicle.Brand} {contract.Vehicle.Model} ({contract.Vehicle.PlateNumber})" : "";
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = receiptNumber,
            DailyShiftId = activeShift.Id,
            ContractId = contract.Id,
            CustomerId = contract.CustomerId,
            VehicleId = contract.VehicleId,
            Type = TransactionType.Receipt,
            Category = TransactionCategory.RentalPayment,
            Amount = amount,
            PaymentMethod = mode,
            StaffUsername = staffUsername,
            TransactionDateTime = DateTime.Now,
            Description = string.IsNullOrWhiteSpace(notes)
                ? $"قبض دفعة من حساب عقد التأجير {contract.ContractNumber} ({carInfo}) - العميل {contract.Customer?.FullName}"
                : $"قبض دفعة لعقد {contract.ContractNumber} - {notes}"
        };
        await context.FinancialTransactions.AddAsync(transaction);

        if (mode == PaymentMode.ImmediateCash)
        {
            activeShift.SystemCalculatedCash += amount;
        }

        // تحديث إجمالي ديون العميل ومزامنتها في النظام كاملاً
        if (contract.Customer != null)
        {
            await RecalculateCustomerDebtsInternalAsync(context, contract.CustomerId);
        }

        await LogAuditAsync(staffUsername, UserRole.Staff, AuditAction.DebtSettlement, "Contracts", contract.Id.ToString(),
            $"قبض دفعة مالية بقيمة {amount} ₪ للعقد {contract.ContractNumber} للعميل {contract.Customer?.FullName} - إجمالي المدفوع: {contract.PaidAmount} ₪");

        await context.SaveChangesAsync();

        if (contract.Customer != null)
        {
            CloudSyncService.TriggerSyncCustomerDebt(contract.Customer.NationalIdOrPassport, contract.Customer.TotalDebts);
        }

        NotifyContractsDataChanged();
        return transaction;
    }

    public async Task<string> GetNextContractNumberCandidateAsync()
    {
        await using var context = await GetContextAsync();
        var currentYear = DateTime.Now.Year;
        var yearPrefix = $"CNT-{currentYear}-";

        var latestContract = await context.Contracts
            .AsNoTracking()
            .Where(c => c.ContractNumber.StartsWith(yearPrefix))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        int maxSeq = 0;
        if (latestContract != null && !string.IsNullOrEmpty(latestContract.ContractNumber))
        {
            var parts = latestContract.ContractNumber.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
            {
                maxSeq = seq;
            }
        }

        return $"{yearPrefix}{(maxSeq + 1):D4}";
    }

    public async Task RollbackAndDiscardNewContractAsync(Guid contractId, string staffUsername)
    {
        await using var context = await GetContextAsync();
        var contract = await context.Contracts
            .Include(c => c.Vehicle)
            .Include(c => c.Customer)
            .Include(c => c.PromissoryNote)
            .Include(c => c.Transactions)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) return;

        // 1. إعادة حالة المركبة لتصبح متاحة فوراً
        if (contract.Vehicle != null)
        {
            contract.Vehicle.Status = contract.Vehicle.IsBlockedByCriticalMaintenance 
                ? VehicleStatus.InMaintenance 
                : VehicleStatus.Available;
        }

        // 2. إلغاء أي ذمة مالية أضيفت للعميل
        if (contract.Customer != null && contract.RemainingAmount > 0)
        {
            contract.Customer.TotalDebts = Math.Max(0, contract.Customer.TotalDebts - contract.RemainingAmount);
        }

        // 3. إعادة خصم الكاش من الوردية إذا كان قد سدد نقداً
        if (contract.PaidAmount > 0 && contract.PaymentMode == PaymentMode.ImmediateCash)
        {
            var activeShift = await context.DailyShifts.FirstOrDefaultAsync(s => !s.IsClosed);
            if (activeShift != null)
            {
                activeShift.SystemCalculatedCash = Math.Max(0, activeShift.SystemCalculatedCash - contract.PaidAmount);
            }
        }

        // 4. حذف السندات المالية وكمبيالة الضمان وكشوفات الفحص المرتبطة
        if (contract.Transactions != null && contract.Transactions.Any())
        {
            context.FinancialTransactions.RemoveRange(contract.Transactions);
        }

        if (contract.PromissoryNote != null)
        {
            context.PromissoryNotes.Remove(contract.PromissoryNote);
        }

        var inspections = await context.VehicleInspections.Where(i => i.ContractId == contract.Id).ToListAsync();
        if (inspections.Any())
        {
            context.VehicleInspections.RemoveRange(inspections);
        }

        context.Contracts.Remove(contract);
        await LogAuditAsync(staffUsername, UserRole.Staff, AuditAction.Delete, "Contracts", contract.Id.ToString(),
            $"تراجع وحذف كامل للعقد الجديد {contract.ContractNumber} للعودة لتعديل البيانات دون أي أثر بالنظام بواسطة {staffUsername}");

        await context.SaveChangesAsync();
        CloudSyncService.TriggerSyncRentalClosed(contract.Vehicle?.PlateNumber, contract.ContractNumber);
        NotifyContractsDataChanged();
    }

    public async Task<Contract> ExtendContractAsync(Guid contractId, DateTime newExpectedReturn, decimal additionalRate, decimal additionalPaid, string staffUsername)
    {
        await using var context = await GetContextAsync();
        var contract = await context.Contracts
            .Include(c => c.Vehicle)
            .Include(c => c.Customer)
            .Include(c => c.PromissoryNote)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) throw new InvalidOperationException("العقد غير موجود!");
        if (contract.Status != ContractStatus.Active) throw new InvalidOperationException("لا يمكن تمديد عقد مغلق أو ملغى!");
        if (newExpectedReturn <= contract.ExpectedReturnDateTime)
        {
            throw new InvalidOperationException("تاريخ التمديد الجديد يجب أن يكون بعد تاريخ الإرجاع السابق!");
        }

        var extraDays = Math.Max(1, (int)Math.Ceiling((newExpectedReturn - contract.ExpectedReturnDateTime).TotalDays));
        var rate = additionalRate > 0 ? additionalRate : contract.DailyAgreedRate;
        var additionalTotal = extraDays * rate;

        if (contract.OriginalExpectedReturnDateTime == null)
        {
            contract.OriginalExpectedReturnDateTime = contract.ExpectedReturnDateTime;
        }

        contract.ExpectedReturnDateTime = newExpectedReturn;
        contract.IsExtended = true;
        contract.ExtensionDays += extraDays;
        contract.ExtensionAdditionalAmount += additionalTotal;
        contract.TotalAgreedAmount += additionalTotal;
        contract.PaidAmount += additionalPaid;
        contract.RemainingAmount = Math.Max(0, contract.TotalAgreedAmount - contract.PaidAmount);

        // تحديث تاريخ استحقاق الكمبيالة الضامنة
        if (contract.PromissoryNote != null)
        {
            contract.PromissoryNote.DueDate = newExpectedReturn.Date;
        }

        // تحديث ديون العميل بالفرق (زيادة إذا لم يسدد تكلفة التمديد كاملة، ونقصان إذا سدد مبالغ إضافية لتغطية ذمم سابقة)
        var unpaidExtension = additionalTotal - additionalPaid;
        if (contract.Customer != null)
        {
            contract.Customer.TotalDebts = Math.Max(0, contract.Customer.TotalDebts + unpaidExtension);
        }

        // تسجيل دفعة نقدية للتمديد إن وجدت
        if (additionalPaid > 0)
        {
            var activeShift = await EnsureActiveShiftAsync(context, staffUsername);

            var receiptNumber = await GenerateUniqueReceiptNumberAsync(context, "REC");
            var transaction = new FinancialTransaction
            {
                Id = Guid.NewGuid(),
                ReceiptNumber = receiptNumber,
                DailyShiftId = activeShift.Id,
                ContractId = contract.Id,
                CustomerId = contract.CustomerId,
                VehicleId = contract.VehicleId,
                Type = TransactionType.Receipt,
                Category = TransactionCategory.ContractExtension,
                Amount = additionalPaid,
                PaymentMethod = PaymentMode.ImmediateCash,
                StaffUsername = staffUsername,
                TransactionDateTime = DateTime.Now,
                Description = $"قبض دفعة تمديد عقد {contract.ContractNumber} لمدة {extraDays} يوم حتى {newExpectedReturn:yyyy/MM/dd}"
            };
            await context.FinancialTransactions.AddAsync(transaction);

            activeShift.SystemCalculatedCash += additionalPaid;
        }

        if (contract.Customer != null)
        {
            await RecalculateCustomerDebtsInternalAsync(context, contract.CustomerId);
            CloudSyncService.TriggerSyncCustomerDebt(contract.Customer.NationalIdOrPassport, contract.Customer.TotalDebts);
        }

        await LogAuditAsync(staffUsername, UserRole.Staff, AuditAction.ContractExtend, "Contracts", contractId.ToString(), 
            $"تمديد العقد {contract.ContractNumber} لـ {extraDays} يوم إضافي بقيمة {additionalTotal} ₪ وسداد {additionalPaid} ₪");

        await context.SaveChangesAsync();
        CloudSyncService.TriggerSyncContractUpdated(contract);
        NotifyContractsDataChanged();
        return contract;
    }

    public async Task<List<OverdueContractDto>> GetOverdueContractsAsync()
    {
        await using var context = await GetContextAsync();
        var now = DateTime.Now;
        var overdue = await context.Contracts
            .AsNoTracking()
            .Include(c => c.Customer)
            .Include(c => c.Vehicle)
            .Where(c => c.Status == ContractStatus.Active && c.ExpectedReturnDateTime < now)
            .ToListAsync();

        return overdue.Select(c => new OverdueContractDto
        {
            Contract = c,
            OverdueHours = (int)Math.Ceiling((now - c.ExpectedReturnDateTime).TotalHours),
            OverdueDays = (int)Math.Ceiling((now - c.ExpectedReturnDateTime).TotalDays),
            EstimatedLateFee = Math.Max(1, (int)Math.Ceiling((now - c.ExpectedReturnDateTime).TotalDays)) * c.DailyAgreedRate
        }).OrderByDescending(o => o.OverdueHours).ToList();
    }

    public async Task<VehicleInspection> SaveVehicleInspectionAsync(VehicleInspection inspection, string username)
    {
        await using var context = await GetContextAsync();
        if (inspection.Id == Guid.Empty) inspection.Id = Guid.NewGuid();
        inspection.StaffUsername = username;
        inspection.CreatedAt = DateTime.UtcNow;

        if (inspection.VehicleId == null && inspection.ContractId != Guid.Empty)
        {
            var contract = await context.Contracts.FirstOrDefaultAsync(c => c.Id == inspection.ContractId);
            if (contract != null)
            {
                inspection.VehicleId = contract.VehicleId;
            }
        }

        if (inspection.VehicleId.HasValue && !string.IsNullOrWhiteSpace(inspection.DamageMarksJson))
        {
            var vehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.Id == inspection.VehicleId.Value);
            if (vehicle != null)
            {
                vehicle.DamageMarksJson = inspection.DamageMarksJson;
            }
        }

        await context.VehicleInspections.AddAsync(inspection);
        await LogAuditAsync(username, UserRole.Staff, AuditAction.Insert, "VehicleInspection", inspection.Id.ToString(), 
            $"تسجيل كشف فحص مركبة ({inspection.Type}) للعقد {inspection.ContractId}");

        await context.SaveChangesAsync();
        return inspection;
    }

    public async Task<List<VehicleInspection>> GetVehicleInspectionsAsync(Guid contractId)
    {
        await using var context = await GetContextAsync();
        return await context.VehicleInspections
            .AsNoTracking()
            .Where(i => i.ContractId == contractId)
            .OrderByDescending(i => i.InspectionDateTime)
            .ToListAsync();
    }

    public async Task<List<VehicleInspection>> GetVehicleInspectionsByVehicleIdAsync(Guid vehicleId)
    {
        await using var context = await GetContextAsync();
        return await context.VehicleInspections
            .AsNoTracking()
            .Include(i => i.Contract)
                .ThenInclude(c => c!.Customer)
            .Where(i => i.VehicleId == vehicleId || (i.Contract != null && i.Contract.VehicleId == vehicleId))
            .OrderByDescending(i => i.InspectionDateTime)
            .ToListAsync();
    }

    public async Task<Vehicle> UpdateVehicleDamagesAsync(Guid vehicleId, List<VehicleDamageMark> marks, string username, string? auditNote = null)
    {
        await using var context = await GetContextAsync();
        var vehicle = await context.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId);
        if (vehicle == null) throw new InvalidOperationException("المركبة غير موجودة في النظام");

        vehicle.SetDamageMarks(marks);
        await LogAuditAsync(username, UserRole.Staff, AuditAction.Update, "Vehicle", vehicle.Id.ToString(),
            auditNote ?? $"تحديث كشف أضرار وخدوش المركبة ({vehicle.PlateNumber}) - إجمالي العلامات: {marks.Count} (القائمة: {marks.Count(m => !m.IsRepaired)})");

        await context.SaveChangesAsync();
        return vehicle;
    }


    // ==================== Maintenance & Vehicle Profile ====================

    public async Task<Vehicle?> GetVehicleMaintenanceProfileAsync(Guid vehicleId)
    {
        await using var context = await GetContextAsync();
        var vehicle = await context.Vehicles
            .Include(v => v.MaintenanceRecords)
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (vehicle != null && vehicle.IsActive && vehicle.IsMaintenanceCritical && vehicle.IsMaintenanceOverdue && vehicle.Status == VehicleStatus.Available)
        {
            vehicle.Status = VehicleStatus.InMaintenance;
            await context.SaveChangesAsync();
        }

        return vehicle;
    }

    public async Task<List<MaintenanceRecord>> GetMaintenanceRecordsAsync(Guid? vehicleId = null)
    {
        await using var context = await GetContextAsync();
        var query = context.MaintenanceRecords
            .AsNoTracking()
            .Include(m => m.Vehicle)
            .AsQueryable();

        if (vehicleId.HasValue)
        {
            query = query.Where(m => m.VehicleId == vehicleId.Value);
        }

        return await query.OrderByDescending(m => m.ServiceDate).ToListAsync();
    }

    public async Task SetCriticalMaintenanceScheduleAsync(Guid vehicleId, int? nextDueOdometer, DateTime? nextDueDate, bool isCritical, string? criticalReason, string username)
    {
        await using var context = await GetContextAsync();
        var vehicle = await context.Vehicles.FindAsync(vehicleId);
        if (vehicle == null) throw new InvalidOperationException("المركبة غير موجودة في أسطول النظام!");

        if (nextDueOdometer.HasValue) vehicle.NextOilChangeOdometer = nextDueOdometer.Value;
        if (nextDueDate.HasValue) vehicle.NextMaintenanceDate = nextDueDate.Value;
        vehicle.IsMaintenanceCritical = isCritical;
        vehicle.CriticalMaintenanceReason = criticalReason;

        if (isCritical && vehicle.IsMaintenanceOverdue && vehicle.Status == VehicleStatus.Available)
        {
            vehicle.Status = VehicleStatus.InMaintenance;
        }

        await LogAuditAsync(username, UserRole.Admin, AuditAction.Update, "Vehicles", vehicleId.ToString(),
            $"تحديث جدول ومواصفات الصيانة للمركبة {vehicle.Brand} {vehicle.Model} (لوحة {vehicle.PlateNumber}): صيانة حرجة={isCritical}، عداد={nextDueOdometer}، موعد={nextDueDate:yyyy-MM-dd}");
        await context.SaveChangesAsync();
    }

    public async Task ToggleVehicleMaintenanceHaltAsync(Guid vehicleId, bool halt, string? reason, string username)
    {
        await using var context = await GetContextAsync();
        var vehicle = await context.Vehicles.FindAsync(vehicleId);
        if (vehicle == null) throw new InvalidOperationException("المركبة غير موجودة في أسطول النظام!");

        if (halt)
        {
            if (vehicle.Status == VehicleStatus.Rented)
            {
                throw new InvalidOperationException("المركبة مؤجرة حالياً بموجب عقد إيجار نشط! يجب استلامها وإغلاق العقد أولاً قبل إيقافها للصيانة.");
            }
            vehicle.Status = VehicleStatus.InMaintenance;
            vehicle.CriticalMaintenanceReason = string.IsNullOrWhiteSpace(reason) ? "إيقاف إداري للصيانة ومنع التأجير" : reason;
            await LogAuditAsync(username, UserRole.Staff, AuditAction.Update, "Vehicles", vehicleId.ToString(),
                $"إيقاف المركبة {vehicle.Brand} {vehicle.Model} فوراً عن التأجير وتحويلها للصيانة. السبب: {vehicle.CriticalMaintenanceReason}");
        }
        else
        {
            if (vehicle.IsBlockedByCriticalMaintenance)
            {
                throw new InvalidOperationException($"لا يمكن فك إيقاف المركبة لأنها تجاوزت حد الصيانة الحرجة ({vehicle.CriticalMaintenanceReason ?? "صيانة إلزامية"})! يجب تأكيد إنجاز الصيانة بنجاح أولاً لتصفير العداد وتفعيلها.");
            }
            vehicle.Status = VehicleStatus.Available;
            vehicle.CriticalMaintenanceReason = null;
            await LogAuditAsync(username, UserRole.Staff, AuditAction.Update, "Vehicles", vehicleId.ToString(),
                $"فك إيقاف المركبة {vehicle.Brand} {vehicle.Model} وإعادتها متاحة للتأجير الفوري.");
        }

        await context.SaveChangesAsync();
    }

    public async Task<MaintenanceRecord> ConfirmMaintenanceCompletedAsync(Guid vehicleId, MaintenanceType type, int odometerAtService, decimal cost, string serviceProvider, string? invoiceNumber, string? notes, int? newNextDueOdometer, DateTime? newNextDueDate, bool newIsCritical, string username)
    {
        await using var context = await GetContextAsync();
        var vehicle = await context.Vehicles.FindAsync(vehicleId);
        if (vehicle == null) throw new InvalidOperationException("المركبة غير موجودة في أسطول النظام!");

        var record = new MaintenanceRecord
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            Type = type,
            OdometerAtService = odometerAtService,
            Cost = cost,
            ServiceDate = DateTime.Now,
            ServiceProvider = serviceProvider,
            InvoiceNumber = invoiceNumber,
            Notes = notes,
            NextDueOdometer = newNextDueOdometer,
            NextDueDate = newNextDueDate
        };
        await context.MaintenanceRecords.AddAsync(record);

        // تحديث السيارة وإعادتها للخدمة
        vehicle.CurrentOdometer = Math.Max(vehicle.CurrentOdometer, odometerAtService);
        if (newNextDueOdometer.HasValue) vehicle.NextOilChangeOdometer = newNextDueOdometer.Value;
        if (newNextDueDate.HasValue) vehicle.NextMaintenanceDate = newNextDueDate.Value;
        vehicle.IsMaintenanceCritical = newIsCritical;
        vehicle.CriticalMaintenanceReason = null;
        vehicle.Status = VehicleStatus.Available; // Return to Available!

        // حركة صرف مالية للصيانة
        if (cost > 0)
        {
            var activeShift = await EnsureActiveShiftAsync(context, username);

            var payReceipt = await GenerateUniqueReceiptNumberAsync(context, "PAY");
            var trans = new FinancialTransaction
            {
                Id = Guid.NewGuid(),
                ReceiptNumber = payReceipt,
                DailyShiftId = activeShift.Id,
                VehicleId = vehicleId,
                Type = TransactionType.Payment,
                Category = TransactionCategory.CarMaintenance,
                Amount = cost,
                PaymentMethod = PaymentMode.ImmediateCash,
                StaffUsername = username,
                TransactionDateTime = DateTime.Now,
                Description = $"مصاريف إنجاز صيانة لمركبة {vehicle.Brand} {vehicle.Model}: {type} - فاتورة {invoiceNumber}",
                PayeeOrVendor = serviceProvider,
                InvoiceNumber = invoiceNumber
            };
            await context.FinancialTransactions.AddAsync(trans);
            activeShift.SystemCalculatedCash -= cost;
        }

        await LogAuditAsync(username, UserRole.Staff, AuditAction.Insert, "Maintenance", vehicleId.ToString(),
            $"تأكيد إنجاز الصيانة بنجاح لمركبة {vehicle.Brand} {vehicle.Model} (لوحة {vehicle.PlateNumber}) وإعادتها متاحة للتأجير الفوري.");

        await context.SaveChangesAsync();
        return record;
    }

    public async Task<MaintenanceRecord> AddMaintenanceRecordAsync(MaintenanceRecord record, string username)
    {
        await using var context = await GetContextAsync();
        if (record.Id == Guid.Empty) record.Id = Guid.NewGuid();
        await context.MaintenanceRecords.AddAsync(record);

        // تحديث السيارة
        var vehicle = await context.Vehicles.FindAsync(record.VehicleId);
        if (vehicle != null)
        {
            if (record.NextDueOdometer.HasValue) vehicle.NextOilChangeOdometer = record.NextDueOdometer.Value;
            if (record.NextDueDate.HasValue) vehicle.NextMaintenanceDate = record.NextDueDate.Value;
            vehicle.CurrentOdometer = Math.Max(vehicle.CurrentOdometer, record.OdometerAtService);
        }

        // تسجيل حركة صرف مالية للصيانة
        if (record.Cost > 0)
        {
            var activeShift = await EnsureActiveShiftAsync(context, username);

            var payReceipt = await GenerateUniqueReceiptNumberAsync(context, "PAY");
            var trans = new FinancialTransaction
            {
                Id = Guid.NewGuid(),
                ReceiptNumber = payReceipt,
                DailyShiftId = activeShift.Id,
                VehicleId = record.VehicleId,
                Type = TransactionType.Payment,
                Category = TransactionCategory.CarMaintenance,
                Amount = record.Cost,
                PaymentMethod = PaymentMode.ImmediateCash,
                StaffUsername = username,
                TransactionDateTime = DateTime.Now,
                Description = $"مصاريف صيانة لمركبة {vehicle?.Brand} {vehicle?.Model}: {record.Type} - فاتورة {record.InvoiceNumber}",
                PayeeOrVendor = record.ServiceProvider,
                InvoiceNumber = record.InvoiceNumber
            };
            await context.FinancialTransactions.AddAsync(trans);
            activeShift.SystemCalculatedCash -= record.Cost;
        }

        await LogAuditAsync(username, UserRole.Admin, AuditAction.Insert, "Maintenance", record.VehicleId.ToString(), $"تسجيل صيانة لمركبة {vehicle?.PlateNumber} بتكلفة {record.Cost} شيكل");
        await context.SaveChangesAsync();
        return record;
    }

    // ==================== Accounting & Daily Shifts ====================

    private static readonly SemaphoreSlim _shiftCreationLock = new(1, 1);

    public async Task<DailyShift> EnsureActiveShiftAsync(CarRentalDbContext context, string staffUsername, string? staffFullName = null)
    {
        var openShift = await context.DailyShifts
            .FirstOrDefaultAsync(s => !s.IsClosed && (s.StaffUsername == staffUsername || s.StaffFullName == staffUsername))
            ?? await context.DailyShifts.FirstOrDefaultAsync(s => !s.IsClosed);

        if (openShift != null)
            return openShift;

        await _shiftCreationLock.WaitAsync();
        try
        {
            // فحص إضافي بعد الدخول في القفل للتأكد من عدم إنشائها بواسطة thread آخر متزامن
            openShift = await context.DailyShifts
                .FirstOrDefaultAsync(s => !s.IsClosed && (s.StaffUsername == staffUsername || s.StaffFullName == staffUsername))
                ?? await context.DailyShifts.FirstOrDefaultAsync(s => !s.IsClosed);

            if (openShift != null)
                return openShift;

            // استخدام سياق مستقل تماماً لإنشاء الوردية لمنع حفظ أي كائنات أخرى عالقة في الـ ChangeTracker الخاص بالسياق الرئيسي
            await using var shiftContext = await GetContextAsync();
            var user = await shiftContext.Users.FirstOrDefaultAsync(u => u.Username == staffUsername);
            var fullName = !string.IsNullOrWhiteSpace(staffFullName)
                ? staffFullName
                : (user?.FullName ?? (string.IsNullOrWhiteSpace(staffUsername) ? "المشرف العام" : staffUsername));

            var lastClosed = await shiftContext.DailyShifts
                .Where(s => s.IsClosed)
                .OrderByDescending(s => s.ClosedAt ?? s.OpenedAt)
                .FirstOrDefaultAsync();

            decimal openingBal = lastClosed?.ClosingBalanceActual ?? 0m;

            var autoShift = new DailyShift
            {
                Id = Guid.NewGuid(),
                StaffUsername = string.IsNullOrWhiteSpace(staffUsername) ? "admin" : staffUsername,
                StaffFullName = fullName,
                OpenedAt = DateTime.Now,
                OpeningBalance = openingBal,
                SystemCalculatedCash = 0,
                IsClosed = false,
                Notes = "افتتاح تلقائي للنظام لضمان دقة الحسابات وعدم ضياع الحركات"
            };

            await shiftContext.DailyShifts.AddAsync(autoShift);
            await shiftContext.SaveChangesAsync();

            // إرجاع الكيان متتبعاً في السياق الحالي للمستدعي
            return await context.DailyShifts.FirstAsync(s => s.Id == autoShift.Id);
        }
        finally
        {
            _shiftCreationLock.Release();
        }
    }

    public async Task<DailyShift?> GetCurrentShiftAsync(string staffUsername)
    {
        await using var context = await GetContextAsync();
        var shift = await context.DailyShifts
            .AsNoTracking()
            .Include(s => s.Transactions)
            .FirstOrDefaultAsync(s => (s.StaffUsername == staffUsername || s.StaffFullName == staffUsername) && !s.IsClosed)
            ?? await context.DailyShifts
                .AsNoTracking()
                .Include(s => s.Transactions)
                .FirstOrDefaultAsync(s => !s.IsClosed);

        if (shift != null)
        {
            var cashReceipts = shift.Transactions
                .Where(t => t.PaymentMethod == PaymentMode.ImmediateCash && t.Type == TransactionType.Receipt)
                .Sum(t => t.Amount);

            var cashPayments = shift.Transactions
                .Where(t => t.PaymentMethod == PaymentMode.ImmediateCash && t.Type == TransactionType.Payment)
                .Sum(t => t.Amount);

            shift.SystemCalculatedCash = cashReceipts - cashPayments;
        }

        return shift;
    }

    public async Task<DailyShift> OpenShiftAsync(string staffUsername, string staffFullName, decimal openingBalance)
    {
        await using var context = await GetContextAsync();
        
        var existingOpen = await context.DailyShifts.FirstOrDefaultAsync(s => !s.IsClosed);

        if (existingOpen != null)
        {
            return existingOpen; // إعادة الوردية المفتوحة حالياً بدلاً من التكرار أو إطلاق خطأ
        }

        var shift = new DailyShift
        {
            StaffUsername = staffUsername,
            StaffFullName = staffFullName,
            OpenedAt = DateTime.Now,
            OpeningBalance = openingBalance,
            SystemCalculatedCash = 0,
            IsClosed = false
        };

        await context.DailyShifts.AddAsync(shift);
        await LogAuditAsync(staffUsername, UserRole.Staff, AuditAction.Insert, "DailyShifts", shift.Id.ToString(), $"افتتاح وردية جديدة بعهدة نقدية {openingBalance} شيكل");
        await context.SaveChangesAsync();
        return shift;
    }

    public async Task<DailyShift> CloseShiftAsync(Guid shiftId, decimal actualCash, string? notes)
    {
        await using var context = await GetContextAsync();
        var shift = await context.DailyShifts
            .Include(s => s.Transactions)
            .FirstOrDefaultAsync(s => s.Id == shiftId);

        if (shift == null) throw new InvalidOperationException("الوردية غير موجودة!");

        // إعادة حساب صافي النقد الفعلي من الحركات النقدية المرتبطة بالوردية بدقة متناهية
        var cashReceipts = shift.Transactions
            .Where(t => t.PaymentMethod == PaymentMode.ImmediateCash && t.Type == TransactionType.Receipt)
            .Sum(t => t.Amount);

        var cashPayments = shift.Transactions
            .Where(t => t.PaymentMethod == PaymentMode.ImmediateCash && t.Type == TransactionType.Payment)
            .Sum(t => t.Amount);

        shift.SystemCalculatedCash = cashReceipts - cashPayments;
        shift.IsClosed = true;
        shift.ClosedAt = DateTime.Now;
        shift.ClosingBalanceActual = actualCash;
        shift.Notes = notes;

        await LogAuditAsync(shift.StaffUsername, UserRole.Staff, AuditAction.ShiftClose, "DailyShifts", shiftId.ToString(), $"إغلاق الوردية بمبلغ نقدي فعلي {actualCash} شيكل وفارق عجز/فائض {shift.Discrepancy} شيكل");
        await context.SaveChangesAsync();
        return shift;
    }

    public async Task<List<DailyShift>> GetAllShiftsAsync()
    {
        await using var context = await GetContextAsync();
        var shifts = await context.DailyShifts
            .AsNoTracking()
            .Include(s => s.Transactions)
            .OrderByDescending(s => s.OpenedAt)
            .ToListAsync();

        foreach (var s in shifts)
        {
            var cashReceipts = s.Transactions
                .Where(t => t.PaymentMethod == PaymentMode.ImmediateCash && t.Type == TransactionType.Receipt)
                .Sum(t => t.Amount);

            var cashPayments = s.Transactions
                .Where(t => t.PaymentMethod == PaymentMode.ImmediateCash && t.Type == TransactionType.Payment)
                .Sum(t => t.Amount);

            s.SystemCalculatedCash = cashReceipts - cashPayments;
        }

        return shifts;
    }

    public async Task<List<FinancialTransaction>> GetTransactionsAsync(DateTime? from = null, DateTime? to = null)
    {
        await using var context = await GetContextAsync();
        var query = context.FinancialTransactions
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Vehicle)
            .Include(t => t.Contract)
            .AsQueryable();

        if (from.HasValue) query = query.Where(t => t.TransactionDateTime >= from.Value);
        if (to.HasValue) query = query.Where(t => t.TransactionDateTime <= to.Value);

        return await query.OrderByDescending(t => t.TransactionDateTime).ToListAsync();
    }

    public async Task<FinancialTransaction> AddTransactionAsync(FinancialTransaction transaction)
    {
        await using var context = await GetContextAsync();
        if (string.IsNullOrEmpty(transaction.ReceiptNumber))
        {
            var prefix = transaction.Type == TransactionType.Receipt ? "REC" : "PAY";
            transaction.ReceiptNumber = await GenerateUniqueReceiptNumberAsync(context, prefix);
        }

        if (transaction.Id == Guid.Empty) transaction.Id = Guid.NewGuid();
        if (transaction.TransactionDateTime == default) transaction.TransactionDateTime = DateTime.Now;

        // ربط المعاملة تلقائياً بالوردية الحالية
        var activeShift = await EnsureActiveShiftAsync(context, transaction.StaffUsername);
        if (transaction.DailyShiftId == null)
        {
            transaction.DailyShiftId = activeShift.Id;
        }

        if (transaction.PaymentMethod == PaymentMode.ImmediateCash)
        {
            if (transaction.Type == TransactionType.Receipt)
                activeShift.SystemCalculatedCash += transaction.Amount;
            else
                activeShift.SystemCalculatedCash -= transaction.Amount;
        }

        // توحيد مسار ديون العميل: إذا كانت الحركة سند قبض لعميل، نخصم المبلغ من ديونه وعقوده تلقائياً
        if (transaction.Type == TransactionType.Receipt && transaction.CustomerId.HasValue && transaction.Amount > 0)
        {
            var custId = transaction.CustomerId.Value;
            var customer = await context.Customers.FirstOrDefaultAsync(c => c.Id == custId);
            if (customer != null)
            {
                Contract? targetContract = null;
                if (transaction.ContractId.HasValue)
                {
                    targetContract = await context.Contracts.FirstOrDefaultAsync(c => c.Id == transaction.ContractId.Value);
                }
                else if (transaction.VehicleId.HasValue)
                {
                    targetContract = await context.Contracts
                        .FirstOrDefaultAsync(c => c.CustomerId == custId && c.VehicleId == transaction.VehicleId.Value && c.Status == ContractStatus.Active);
                    if (targetContract != null)
                    {
                        transaction.ContractId = targetContract.Id;
                    }
                }

                if (targetContract != null)
                {
                    targetContract.PaidAmount += transaction.Amount;
                    if (transaction.VehicleId == null && targetContract.VehicleId != Guid.Empty)
                    {
                        transaction.VehicleId = targetContract.VehicleId;
                    }
                    if (targetContract.IsOpenEnded && targetContract.Status == ContractStatus.Active)
                    {
                        targetContract.RemainingAmount = targetContract.OpenRentalCurrentDue;
                    }
                    else
                    {
                        targetContract.RemainingAmount = Math.Max(0, targetContract.TotalAgreedAmount - targetContract.PaidAmount);
                    }
                }
                else
                {
                    // توزيع سداد القبض على عقود العميل غير المسددة (من الأقدم للأحدث بما فيها العقود المفتوحة والنشطة)
                    var unpaid = await context.Contracts
                        .Where(c => c.CustomerId == custId && c.Status != ContractStatus.Cancelled)
                        .OrderBy(c => c.CreatedAt)
                        .ToListAsync();

                    var rem = transaction.Amount;
                    foreach (var c in unpaid)
                    {
                        if (rem <= 0) break;
                        decimal due = c.IsOpenEnded && c.Status == ContractStatus.Active
                            ? c.OpenRentalCurrentDue
                            : c.RemainingAmount;

                        if (due > 0)
                        {
                            var apply = Math.Min(rem, due);
                            c.PaidAmount += apply;
                            if (c.IsOpenEnded && c.Status == ContractStatus.Active)
                                c.RemainingAmount = c.OpenRentalCurrentDue;
                            else
                                c.RemainingAmount = Math.Max(0, c.TotalAgreedAmount - c.PaidAmount);
                            rem -= apply;

                            if (!transaction.ContractId.HasValue)
                            {
                                transaction.ContractId = c.Id;
                            }
                            if (!transaction.VehicleId.HasValue && c.VehicleId != Guid.Empty)
                            {
                                transaction.VehicleId = c.VehicleId;
                            }
                        }
                    }

                    if (!transaction.VehicleId.HasValue)
                    {
                        var activeContract = await context.Contracts
                            .Where(c => c.CustomerId == custId && c.Status == ContractStatus.Active)
                            .OrderByDescending(c => c.CreatedAt)
                            .FirstOrDefaultAsync();
                        if (activeContract != null)
                        {
                            if (!transaction.ContractId.HasValue) transaction.ContractId = activeContract.Id;
                            if (activeContract.VehicleId != Guid.Empty) transaction.VehicleId = activeContract.VehicleId;
                        }
                    }
                }

                await RecalculateCustomerDebtsInternalAsync(context, custId);
                CloudSyncService.TriggerSyncCustomerDebt(customer.NationalIdOrPassport, customer.TotalDebts);
            }
        }

        await context.FinancialTransactions.AddAsync(transaction);
        await LogAuditAsync(transaction.StaffUsername, UserRole.Accountant, AuditAction.Insert, "Transactions", transaction.Id.ToString(), $"تسجيل قيد {transaction.Type} بمبلغ {transaction.Amount} شيكل");
        await context.SaveChangesAsync();
        NotifyContractsDataChanged();
        return transaction;
    }

    public async Task<List<VehicleProfitabilityDto>> GetVehicleProfitabilityAsync()
    {
        await using var context = await GetContextAsync();
        var vehicles = await context.Vehicles.AsNoTracking().ToListAsync();

        var contractRows = await context.Contracts
            .AsNoTracking()
            .Where(c => c.Status != ContractStatus.Cancelled)
            .Select(c => new 
            { 
                c.Id, 
                c.VehicleId, 
                c.PaidAmount, 
                c.StartDateTime, 
                c.ExpectedReturnDateTime,
                c.ActualReturnDateTime,
                c.ClosedAt,
                c.IsOpenEnded,
                c.Status 
            })
            .ToListAsync();

        var contractVehicleMap = contractRows.ToDictionary(c => c.Id, c => c.VehicleId);

        var contractRevByVehicle = contractRows
            .GroupBy(c => c.VehicleId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.PaidAmount));

        var rawReceipts = await context.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Receipt)
            .Select(t => new { t.VehicleId, t.ContractId, t.Amount })
            .ToListAsync();

        var txRevByVehicle = new Dictionary<Guid, decimal>();
        foreach (var r in rawReceipts)
        {
            Guid? targetVid = r.VehicleId;
            if (!targetVid.HasValue && r.ContractId.HasValue && contractVehicleMap.TryGetValue(r.ContractId.Value, out var cvid))
            {
                targetVid = cvid;
            }

            if (targetVid.HasValue)
            {
                if (!txRevByVehicle.ContainsKey(targetVid.Value))
                    txRevByVehicle[targetVid.Value] = 0;
                txRevByVehicle[targetVid.Value] += r.Amount;
            }
        }

        var rawPayments = await context.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Payment)
            .Select(t => new { t.VehicleId, t.ContractId, t.Amount })
            .ToListAsync();

        var expByVehicle = new Dictionary<Guid, decimal>();
        foreach (var p in rawPayments)
        {
            Guid? targetVid = p.VehicleId;
            if (!targetVid.HasValue && p.ContractId.HasValue && contractVehicleMap.TryGetValue(p.ContractId.Value, out var cvid))
            {
                targetVid = cvid;
            }

            if (targetVid.HasValue)
            {
                if (!expByVehicle.ContainsKey(targetVid.Value))
                    expByVehicle[targetVid.Value] = 0;
                expByVehicle[targetVid.Value] += p.Amount;
            }
        }

        var daysDict = contractRows
            .GroupBy(c => c.VehicleId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(c => 
                {
                    if (c.IsOpenEnded)
                    {
                        var end = (c.Status == ContractStatus.Closed) 
                            ? (c.ActualReturnDateTime ?? c.ClosedAt?.ToLocalTime() ?? DateTime.Now) 
                            : DateTime.Now;
                        return Math.Max(1, (int)Math.Ceiling((end - c.StartDateTime).TotalDays));
                    }
                    else
                    {
                        var end = (c.Status == ContractStatus.Closed && c.ActualReturnDateTime.HasValue)
                            ? c.ActualReturnDateTime.Value
                            : (c.Status == ContractStatus.Closed && c.ClosedAt.HasValue && c.ClosedAt.Value.ToLocalTime() < c.ExpectedReturnDateTime)
                                ? c.ClosedAt.Value.ToLocalTime()
                                : c.ExpectedReturnDateTime;
                        return Math.Max(1, (int)Math.Ceiling((end - c.StartDateTime).TotalDays));
                    }
                })
            );

        var list = new List<VehicleProfitabilityDto>(vehicles.Count);

        foreach (var v in vehicles)
        {
            contractRevByVehicle.TryGetValue(v.Id, out var contractRev);
            txRevByVehicle.TryGetValue(v.Id, out var txRev);
            var rev = Math.Max(contractRev, txRev);

            expByVehicle.TryGetValue(v.Id, out var exp);
            daysDict.TryGetValue(v.Id, out var days);

            list.Add(new VehicleProfitabilityDto
            {
                VehicleId = v.Id,
                PlateNumber = v.PlateNumber,
                BrandAndModel = $"{v.Brand} {v.Model} ({v.ModelYear})",
                TotalRevenue = rev,
                TotalExpenses = exp,
                TotalRentalDays = days
            });
        }

        return list.OrderByDescending(p => p.NetProfit).ToList();
    }

    public async Task<List<FinancialTransaction>> GetVehicleTransactionsAsync(Guid vehicleId)
    {
        await using var context = await GetContextAsync();
        return await context.FinancialTransactions
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Contract)
            .Include(t => t.Vehicle)
            .Where(t => t.VehicleId == vehicleId || (t.Contract != null && t.Contract.VehicleId == vehicleId))
            .OrderByDescending(t => t.TransactionDateTime)
            .ToListAsync();
    }

    // ==================== Audit Logs ====================

    public async Task<List<AuditLog>> GetAuditLogsAsync()
    {
        await using var context = await GetContextAsync();
        return await context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .Take(200)
            .ToListAsync();
    }

    public async Task LogAuditAsync(string username, UserRole role, AuditAction action, string entity, string recordId, string details)
    {
        try
        {
            await using var context = await GetContextAsync();
            var log = new AuditLog
            {
                Username = username,
                Role = role,
                Action = action,
                EntityName = entity,
                RecordId = recordId,
                Details = details,
                Timestamp = DateTime.Now,
                MachineName = Environment.MachineName
            };
            await context.AuditLogs.AddAsync(log);
            await context.SaveChangesAsync();
        }
        catch
        {
            // Fail-safe: do not crash if audit write has transient error
        }
    }

    // ==================== Backup ====================

    public async Task<string> CreateLocalBackupAsync(string targetPathOrDirectory)
    {
        var sourceDb = File.Exists(AppPaths.DatabaseFilePath)
            ? AppPaths.DatabaseFilePath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "car_rental_local.db");

        if (!File.Exists(sourceDb))
        {
            throw new FileNotFoundException("ملف قاعدة البيانات الأساسية غير موجود!", sourceDb);
        }

        string destPath;

        if (string.IsNullOrWhiteSpace(targetPathOrDirectory))
        {
            var defaultDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
            if (!Directory.Exists(defaultDir)) Directory.CreateDirectory(defaultDir);
            destPath = Path.Combine(defaultDir, $"CarRental_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        }
        else if (targetPathOrDirectory.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            destPath = targetPathOrDirectory;
            var parentDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }
        }
        else
        {
            if (!Directory.Exists(targetPathOrDirectory))
            {
                Directory.CreateDirectory(targetPathOrDirectory);
            }
            var backupFileName = $"CarRental_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            destPath = Path.Combine(targetPathOrDirectory, backupFileName);
        }

        File.Copy(sourceDb, destPath, true);
        return destPath;
    }

    // ==================== Users & Authentication ====================

    public async Task<List<User>> GetUsersAsync()
    {
        await using var context = await GetContextAsync();
        return await context.Users
            .AsNoTracking()
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        await using var context = await GetContextAsync();
        return await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> AuthenticateUserWithPinAsync(string pin, string? username = null)
    {
        if (string.IsNullOrWhiteSpace(pin)) return null;

        await using var context = await GetContextAsync();
        var query = context.Users.Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(username))
        {
            query = query.Where(u => u.Username.ToLower() == username.Trim().ToLower());
        }

        var users = await query.ToListAsync();
        var matchedUser = users.FirstOrDefault(u => u.PasswordHash == pin.Trim());
        if (matchedUser != null)
        {
            matchedUser.LastLoginAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            await LogAuditAsync(matchedUser.Username, matchedUser.Role, AuditAction.Login, "User", matchedUser.Id.ToString(), $"تسجيل دخول ناجح للمستخدم: {matchedUser.FullName}");
        }

        return matchedUser;
    }

    public async Task<User> SaveUserAsync(User user, string adminUsername)
    {
        await using var context = await GetContextAsync();
        var cleanUsername = user.Username.Trim();
        var cleanUsernameLower = cleanUsername.ToLower();

        // 1. تحقق من فرادة رمز المرور السري (PIN) عبر جميع الحسابات النشطة لمنع تضارب الدخول
        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            var cleanPin = user.PasswordHash.Trim();
            var pinConflict = await context.Users.AnyAsync(u => u.IsActive && u.PasswordHash == cleanPin && u.Id != user.Id);
            if (pinConflict)
            {
                throw new InvalidOperationException($"رمز الدخول السري (PIN: {cleanPin}) مستخدم بالفعل لحساب آخر! يرجى اختيار رمز سري مختلف ومميز.");
            }
        }

        var existing = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        if (existing == null)
        {
            if (await context.Users.AnyAsync(u => u.Username.ToLower() == cleanUsernameLower))
            {
                throw new InvalidOperationException($"اسم المستخدم ({cleanUsername}) مستخدم بالفعل، يرجى اختيار اسم آخر!");
            }

            if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
            user.Username = cleanUsername;
            user.FullName = user.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(user.PasswordHash)) user.PasswordHash = user.PasswordHash.Trim();
            user.CreatedAt = DateTime.UtcNow;
            await context.Users.AddAsync(user);
            await LogAuditAsync(adminUsername, UserRole.Admin, AuditAction.Insert, "User", user.Id.ToString(), $"إضافة مستخدم جديد: {user.FullName} (اسم المستخدم: {user.Username}، الدور: {user.Role})");
        }
        else
        {
            if (await context.Users.AnyAsync(u => u.Username.ToLower() == cleanUsernameLower && u.Id != user.Id))
            {
                throw new InvalidOperationException($"اسم المستخدم ({cleanUsername}) مستخدم بالفعل، يرجى اختيار اسم آخر!");
            }

            var oldUsername = existing.Username;
            var oldFullName = existing.FullName;
            var newUsername = cleanUsername;
            var newFullName = user.FullName.Trim();

            bool usernameChanged = !string.Equals(oldUsername, newUsername, StringComparison.OrdinalIgnoreCase);
            bool fullNameChanged = !string.Equals(oldFullName, newFullName, StringComparison.Ordinal);

            if (usernameChanged || fullNameChanged)
            {
                // ترحيل وتحديث العمليات والسجلات السابقة تلقائياً لترتبط بالاسم الجديد
                
                // 1. الورديات والصناديق اليومية (DailyShifts)
                var shifts = await context.DailyShifts
                    .Where(s => s.StaffUsername.ToLower() == oldUsername.ToLower())
                    .ToListAsync();
                foreach (var shift in shifts)
                {
                    if (usernameChanged) shift.StaffUsername = newUsername;
                    if (fullNameChanged) shift.StaffFullName = newFullName;
                }

                // 2. القيود والسندات المالية (FinancialTransactions)
                if (usernameChanged)
                {
                    var txs = await context.FinancialTransactions
                        .Where(t => t.StaffUsername.ToLower() == oldUsername.ToLower())
                        .ToListAsync();
                    foreach (var tx in txs)
                    {
                        tx.StaffUsername = newUsername;
                    }
                }

                // 3. عقود الإيجار (Contracts)
                var contracts = await context.Contracts
                    .Where(c => (c.CreatedByStaffName != null && (c.CreatedByStaffName.ToLower() == oldUsername.ToLower() || c.CreatedByStaffName == oldFullName))
                             || (c.ClosedByStaffName != null && (c.ClosedByStaffName.ToLower() == oldUsername.ToLower() || c.ClosedByStaffName == oldFullName)))
                    .ToListAsync();
                foreach (var contract in contracts)
                {
                    if (contract.CreatedByStaffName != null)
                    {
                        if (contract.CreatedByStaffName.Equals(oldUsername, StringComparison.OrdinalIgnoreCase) || contract.CreatedByStaffName == oldFullName)
                        {
                            contract.CreatedByStaffName = newFullName;
                        }
                    }
                    if (contract.ClosedByStaffName != null)
                    {
                        if (contract.ClosedByStaffName.Equals(oldUsername, StringComparison.OrdinalIgnoreCase) || contract.ClosedByStaffName == oldFullName)
                        {
                            contract.ClosedByStaffName = newFullName;
                        }
                    }
                }

                // 4. فحوصات استلام وتسليم المركبات (VehicleInspections)
                if (usernameChanged)
                {
                    var inspections = await context.VehicleInspections
                        .Where(i => i.StaffUsername.ToLower() == oldUsername.ToLower())
                        .ToListAsync();
                    foreach (var insp in inspections)
                    {
                        insp.StaffUsername = newUsername;
                    }
                }

                // 5. المخالفات المرورية (TrafficViolations)
                if (usernameChanged)
                {
                    var violations = await context.TrafficViolations
                        .Where(v => v.RecordedByUsername != null && v.RecordedByUsername.ToLower() == oldUsername.ToLower())
                        .ToListAsync();
                    foreach (var v in violations)
                    {
                        v.RecordedByUsername = newUsername;
                    }
                }

                // 6. ملفات العملاء (Customers)
                if (usernameChanged)
                {
                    var customers = await context.Customers
                        .Where(c => c.CreatedByUsername != null && c.CreatedByUsername.ToLower() == oldUsername.ToLower())
                        .ToListAsync();
                    foreach (var c in customers)
                    {
                        c.CreatedByUsername = newUsername;
                    }
                }

                // 7. سجلات القائمة السوداء (BlacklistRecords)
                if (usernameChanged)
                {
                    var blacklists = await context.BlacklistRecords
                        .Where(b => (b.BlockedByUsername != null && b.BlockedByUsername.ToLower() == oldUsername.ToLower())
                                 || (b.UnblockedByUsername != null && b.UnblockedByUsername.ToLower() == oldUsername.ToLower()))
                        .ToListAsync();
                    foreach (var b in blacklists)
                    {
                        if (b.BlockedByUsername != null && b.BlockedByUsername.Equals(oldUsername, StringComparison.OrdinalIgnoreCase))
                            b.BlockedByUsername = newUsername;
                        if (b.UnblockedByUsername != null && b.UnblockedByUsername.Equals(oldUsername, StringComparison.OrdinalIgnoreCase))
                            b.UnblockedByUsername = newUsername;
                    }
                }

                // 8. سجل الرقابة وتتبع العمليات (AuditLogs)
                if (usernameChanged)
                {
                    var audits = await context.AuditLogs
                        .Where(a => a.Username.ToLower() == oldUsername.ToLower())
                        .ToListAsync();
                    foreach (var a in audits)
                    {
                        a.Username = newUsername;
                    }
                }

                // 9. الكمبيالات والسندات لأمر (PromissoryNotes)
                var notes = await context.PromissoryNotes
                    .Where(n => n.ReleasedByStaffName != null && (n.ReleasedByStaffName.ToLower() == oldUsername.ToLower() || n.ReleasedByStaffName == oldFullName))
                    .ToListAsync();
                foreach (var n in notes)
                {
                    n.ReleasedByStaffName = newFullName;
                }
            }

            // حماية فائقة: التحقق من عدم تعطيل أو تغيير رتبة الآدمن الوحيد في النظام
            if (existing.Role == UserRole.Admin)
            {
                var otherActiveAdminsCount = await context.Users
                    .CountAsync(u => u.Role == UserRole.Admin && u.IsActive && u.Id != user.Id);

                if (otherActiveAdminsCount == 0)
                {
                    if (!user.IsActive)
                    {
                        throw new InvalidOperationException("لا يمكن تعطيل أو قفل حساب المدير العام (الآدمن) الوحيد النشط في النظام! يجب توفير وتفعيل حساب مدير عام آخر على الأقل أولاً لتفادي قفل لوحة التحكم.");
                    }
                    if (user.Role != UserRole.Admin)
                    {
                        throw new InvalidOperationException("لا يمكن تغيير صلاحية المدير العام الوحيد النشط في النظام! يجب أن يبقى على الأقل مدير عام واحد نشط.");
                    }
                }
            }

            existing.FullName = newFullName;
            existing.Username = newUsername;
            existing.Role = user.Role;
            existing.PhoneNumber = user.PhoneNumber?.Trim();
            existing.IsActive = user.IsActive;
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                existing.PasswordHash = user.PasswordHash.Trim();
            }
            await LogAuditAsync(adminUsername, UserRole.Admin, AuditAction.Update, "User", user.Id.ToString(), $"تعديل بيانات المستخدم: {user.FullName} (الدور: {user.Role})");
        }

        await context.SaveChangesAsync();
        CloudSyncService.TriggerSyncUser(existing ?? user);
        return user;
    }

    public async Task DeleteUserAsync(Guid id, string adminUsername)
    {
        await using var context = await GetContextAsync();
        var user = await context.Users.FindAsync(id);
        if (user != null)
        {
            if (user.Role == UserRole.Admin)
            {
                var otherActiveAdminsCount = await context.Users
                    .CountAsync(u => u.Role == UserRole.Admin && u.IsActive && u.Id != id);
                if (otherActiveAdminsCount == 0)
                {
                    throw new InvalidOperationException("لا يمكن حذف حساب المدير العام الوحيد النشط في النظام! يجب توفير حساب مدير عام نشط آخر أولاً.");
                }
            }

            var usernameToDelete = user.Username;
            context.Users.Remove(user);
            await LogAuditAsync(adminUsername, UserRole.Admin, AuditAction.Delete, "User", id.ToString(), $"حذف المستخدم {user.FullName}");
            await context.SaveChangesAsync();
            CloudSyncService.TriggerSyncDeleteUser(usernameToDelete);
        }
    }

    // ==================== Traffic Violations ====================

    public async Task<List<TrafficViolation>> GetTrafficViolationsAsync(Guid? vehicleId = null, Guid? customerId = null, ViolationStatus? status = null)
    {
        await using var context = await GetContextAsync();
        var query = context.TrafficViolations
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Customer)
            .Include(t => t.Contract)
            .AsQueryable();

        if (vehicleId.HasValue) query = query.Where(t => t.VehicleId == vehicleId.Value);
        if (customerId.HasValue) query = query.Where(t => t.CustomerId == customerId.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);

        return await query.OrderByDescending(t => t.ViolationDateTime).ToListAsync();
    }

    public async Task<Customer?> FindResponsibleCustomerForViolationAsync(Guid vehicleId, DateTime violationDateTime)
    {
        await using var context = await GetContextAsync();
        var contract = await context.Contracts
            .AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.VehicleId == vehicleId && 
                        c.StartDateTime <= violationDateTime &&
                        (c.ActualReturnDateTime != null ? c.ActualReturnDateTime >= violationDateTime : c.ExpectedReturnDateTime.AddDays(7) >= violationDateTime))
            .OrderByDescending(c => c.StartDateTime)
            .FirstOrDefaultAsync();

        return contract?.Customer;
    }

    public async Task<List<Contract>> GetRecentVehicleContractsAsync(Guid vehicleId, int days = 14)
    {
        await using var context = await GetContextAsync();
        var threshold = DateTime.Now.Date.AddDays(-days);

        return await context.Contracts
            .AsNoTracking()
            .Include(c => c.Customer)
            .Include(c => c.Vehicle)
            .Where(c => c.VehicleId == vehicleId && 
                        ((c.ActualReturnDateTime != null && c.ActualReturnDateTime >= threshold) ||
                         (c.ExpectedReturnDateTime >= threshold) ||
                         (c.StartDateTime >= threshold)))
            .OrderByDescending(c => c.StartDateTime)
            .ToListAsync();
    }

    public async Task<TrafficViolation> AddTrafficViolationAsync(TrafficViolation violation, bool chargeToCustomer, string username)
    {
        await using var context = await GetContextAsync();
        if (violation.Id == Guid.Empty) violation.Id = Guid.NewGuid();
        violation.RecordedByUsername = username;
        violation.CreatedAt = DateTime.UtcNow;

        if (violation.CustomerId == null)
        {
            var responsibleCustomer = await FindResponsibleCustomerForViolationAsync(violation.VehicleId, violation.ViolationDateTime);
            if (responsibleCustomer != null)
            {
                violation.CustomerId = responsibleCustomer.Id;
                var activeContract = await context.Contracts
                    .Where(c => c.VehicleId == violation.VehicleId && c.CustomerId == responsibleCustomer.Id && c.StartDateTime <= violation.ViolationDateTime)
                    .OrderByDescending(c => c.StartDateTime)
                    .FirstOrDefaultAsync();
                violation.ContractId = activeContract?.Id;
            }
        }

        if (chargeToCustomer && violation.CustomerId.HasValue)
        {
            var customer = await context.Customers.FirstOrDefaultAsync(c => c.Id == violation.CustomerId.Value);
            if (customer != null)
            {
                customer.TotalDebts += violation.Amount;
            }
            violation.Status = ViolationStatus.PendingCollection;
        }

        await context.TrafficViolations.AddAsync(violation);
        await LogAuditAsync(username, UserRole.Staff, AuditAction.TrafficViolationAdd, "TrafficViolation", violation.Id.ToString(), 
            $"تسجيل مخالفة مرورية {violation.ViolationNumber} بمبلغ {violation.Amount} ₪");

        await context.SaveChangesAsync();
        return violation;
    }

    public async Task<TrafficViolation> SettleViolationWithCustomerAsync(Guid violationId, decimal amountPaid, PaymentMode mode, string username)
    {
        await using var context = await GetContextAsync();
        var violation = await context.TrafficViolations
            .Include(v => v.Customer)
            .Include(v => v.Vehicle)
            .FirstOrDefaultAsync(v => v.Id == violationId);

        if (violation == null) throw new InvalidOperationException("المخالفة غير موجودة!");

        violation.IsCollectedFromCustomer = true;
        violation.CollectedDate = DateTime.Now;
        violation.Status = violation.IsPaidToAuthority ? ViolationStatus.PaidToAuthorities : ViolationStatus.CollectedFromCustomer;

        if (violation.Customer != null)
        {
            violation.Customer.TotalDebts = Math.Max(0, violation.Customer.TotalDebts - amountPaid);
        }

        var activeShift = await EnsureActiveShiftAsync(context, username);

        var receiptNumber = await GenerateUniqueReceiptNumberAsync(context, "REC");
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = receiptNumber,
            DailyShiftId = activeShift.Id,
            CustomerId = violation.CustomerId,
            VehicleId = violation.VehicleId,
            ContractId = violation.ContractId,
            Type = TransactionType.Receipt,
            Category = TransactionCategory.TrafficViolation,
            Amount = amountPaid,
            PaymentMethod = mode,
            StaffUsername = username,
            TransactionDateTime = DateTime.Now,
            Description = $"تحصيل قيمة مخالفة مرورية {violation.ViolationNumber} ({violation.Reason}) من العميل {violation.Customer?.FullName}"
        };
        await context.FinancialTransactions.AddAsync(transaction);

        if (mode == PaymentMode.ImmediateCash)
        {
            activeShift.SystemCalculatedCash += amountPaid;
        }

        await LogAuditAsync(username, UserRole.Staff, AuditAction.Update, "TrafficViolation", violation.Id.ToString(), 
            $"تحصيل مخالفة {violation.ViolationNumber} بمبلغ {amountPaid} ₪");

        await context.SaveChangesAsync();
        return violation;
    }

    public async Task<TrafficViolation> PayViolationToAuthorityAsync(Guid violationId, string username)
    {
        await using var context = await GetContextAsync();
        var violation = await context.TrafficViolations
            .Include(v => v.Vehicle)
            .FirstOrDefaultAsync(v => v.Id == violationId);

        if (violation == null) throw new InvalidOperationException("المخالفة غير موجودة!");

        violation.IsPaidToAuthority = true;
        violation.PaidToAuthorityDate = DateTime.Now;
        violation.Status = ViolationStatus.PaidToAuthorities;

        var activeShift = await EnsureActiveShiftAsync(context, username);

        var payReceipt = await GenerateUniqueReceiptNumberAsync(context, "PAY");
        var trans = new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = payReceipt,
            DailyShiftId = activeShift.Id,
            VehicleId = violation.VehicleId,
            CustomerId = violation.CustomerId,
            Type = TransactionType.Payment,
            Category = TransactionCategory.TrafficViolation,
            Amount = violation.Amount,
            PaymentMethod = PaymentMode.ImmediateCash,
            StaffUsername = username,
            TransactionDateTime = DateTime.Now,
            Description = $"سداد مخالفة مرورية {violation.ViolationNumber} لإدارة المرور لمركبة {violation.Vehicle?.PlateNumber}",
            PayeeOrVendor = "إدارة المرور والشرطة",
            InvoiceNumber = violation.ViolationNumber
        };
        await context.FinancialTransactions.AddAsync(trans);

        activeShift.SystemCalculatedCash -= violation.Amount;

        await LogAuditAsync(username, UserRole.Staff, AuditAction.Update, "TrafficViolation", violation.Id.ToString(), 
            $"سداد مخالفة {violation.ViolationNumber} لإدارة المرور بمبلغ {violation.Amount} ₪");

        await context.SaveChangesAsync();
        return violation;
    }

    // ==================== Direct Debt Settlement & General Expenses ====================

    public async Task<FinancialTransaction?> GetTransactionByIdAsync(Guid id)
    {
        await using var context = await GetContextAsync();
        return await context.FinancialTransactions
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Vehicle)
            .Include(t => t.Contract)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    private async Task<decimal> RecalculateCustomerDebtsInternalAsync(CarRentalDbContext context, Guid customerId)
    {
        var customer = await context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer == null) return 0;

        var dbContracts = await context.Contracts
            .Where(c => c.CustomerId == customerId && c.Status != ContractStatus.Cancelled)
            .ToListAsync();

        var localContracts = context.Contracts.Local
            .Where(c => c.CustomerId == customerId && 
                        c.Status != ContractStatus.Cancelled && 
                        context.Entry(c).State != EntityState.Deleted)
            .ToList();

        var allContracts = dbContracts
            .Where(c => context.Entry(c).State != EntityState.Deleted)
            .UnionBy(localContracts, c => c.Id)
            .ToList();

        var dbViolations = await context.TrafficViolations
            .Where(v => v.CustomerId == customerId && v.Status == ViolationStatus.PendingCollection)
            .ToListAsync();

        var localViolations = context.TrafficViolations.Local
            .Where(v => v.CustomerId == customerId && 
                        v.Status == ViolationStatus.PendingCollection && 
                        context.Entry(v).State != EntityState.Deleted)
            .ToList();

        var allViolations = dbViolations
            .Where(v => context.Entry(v).State != EntityState.Deleted)
            .UnionBy(localViolations, v => v.Id)
            .ToList();

        decimal contractsDebt = 0;
        foreach (var c in allContracts)
        {
            if (c.IsOpenEnded && c.Status == ContractStatus.Active)
            {
                c.RemainingAmount = c.OpenRentalCurrentDue;
                contractsDebt += c.OpenRentalCurrentDue;
            }
            else
            {
                contractsDebt += Math.Max(0, c.RemainingAmount);
            }
        }

        decimal violationsDebt = allViolations.Sum(v => v.Amount);
        customer.TotalDebts = contractsDebt + violationsDebt;
        return customer.TotalDebts;
    }

    public async Task<decimal> RecalculateCustomerDebtsAsync(Guid customerId)
    {
        await using var context = await GetContextAsync();
        var total = await RecalculateCustomerDebtsInternalAsync(context, customerId);
        await context.SaveChangesAsync();
        
        var customer = await context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer != null)
        {
            CloudSyncService.TriggerSyncCustomerDebt(customer.NationalIdOrPassport, customer.TotalDebts);
        }
        return total;
    }

    public async Task<FinancialTransaction> SettleCustomerDebtAsync(Guid customerId, decimal amount, PaymentMode mode, string notes, string staffUsername)
    {
        await using var context = await GetContextAsync();
        var customer = await context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer == null) throw new InvalidOperationException("العميل غير موجود!");
        if (amount <= 0) throw new InvalidOperationException("المبلغ المسدد يجب أن يكون أكبر من الصفر!");

        // ترحيل وتوزيع سداد الدين تلقائياً على عقود العميل ذات المبالغ المتبقية (من الأقدم للأحدث بما فيها العقود المفتوحة والنشطة)
        var unpaidContracts = await context.Contracts
            .Where(c => c.CustomerId == customerId && c.Status != ContractStatus.Cancelled)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        Guid? linkedContractId = null;
        Guid? linkedVehicleId = null;

        var remainingToApply = amount;
        foreach (var c in unpaidContracts)
        {
            if (remainingToApply <= 0) break;
            decimal due = c.IsOpenEnded && c.Status == ContractStatus.Active
                ? c.OpenRentalCurrentDue
                : c.RemainingAmount;

            if (due > 0)
            {
                var applyAmount = Math.Min(remainingToApply, due);
                c.PaidAmount += applyAmount;
                if (c.IsOpenEnded && c.Status == ContractStatus.Active)
                {
                    c.RemainingAmount = c.OpenRentalCurrentDue;
                }
                else
                {
                    c.RemainingAmount = Math.Max(0, c.TotalAgreedAmount - c.PaidAmount);
                }
                remainingToApply -= applyAmount;

                if (!linkedContractId.HasValue)
                {
                    linkedContractId = c.Id;
                    linkedVehicleId = c.VehicleId;
                }
            }
        }

        if (!linkedVehicleId.HasValue)
        {
            var lastContract = unpaidContracts.LastOrDefault();
            if (lastContract != null)
            {
                linkedContractId = lastContract.Id;
                linkedVehicleId = lastContract.VehicleId;
            }
        }

        // تحديث إجمالي ديون العميل بدقة بناءً على المتبقي الحقيقي
        await RecalculateCustomerDebtsInternalAsync(context, customerId);

        var activeShift = await EnsureActiveShiftAsync(context, staffUsername);
        var receiptNumber = await GenerateUniqueReceiptNumberAsync(context, "REC");
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = receiptNumber,
            DailyShiftId = activeShift.Id,
            CustomerId = customer.Id,
            ContractId = linkedContractId,
            VehicleId = linkedVehicleId,
            Type = TransactionType.Receipt,
            Category = TransactionCategory.DebtPayment,
            Amount = amount,
            PaymentMethod = mode,
            StaffUsername = staffUsername,
            TransactionDateTime = DateTime.Now,
            Description = string.IsNullOrWhiteSpace(notes) 
                ? $"سداد دفعة من الحساب/الديون السابقة للعميل {customer.FullName}" 
                : $"سداد دفعة من الديون للعميل {customer.FullName} - {notes}"
        };
        await context.FinancialTransactions.AddAsync(transaction);

        if (mode == PaymentMode.ImmediateCash)
        {
            activeShift.SystemCalculatedCash += amount;
        }

        await LogAuditAsync(staffUsername, UserRole.Staff, AuditAction.DebtSettlement, "Customers", customerId.ToString(), 
            $"قبض دفعة من ديون العميل {customer.FullName} بمبلغ {amount} ₪ - المتبقي: {customer.TotalDebts} ₪");

        await context.SaveChangesAsync();
        CloudSyncService.TriggerSyncCustomerDebt(customer.NationalIdOrPassport, customer.TotalDebts);
        NotifyContractsDataChanged();
        return transaction;
    }

    public async Task<FinancialTransaction> AddGeneralExpenseAsync(
        decimal amount, 
        string description, 
        TransactionCategory category, 
        PaymentMode mode, 
        string staffUsername, 
        Guid? vehicleId = null, 
        string? payeeOrVendor = null, 
        string? invoiceNumber = null)
    {
        await using var context = await GetContextAsync();
        if (amount <= 0) throw new InvalidOperationException("مبلغ المصروف يجب أن يكون أكبر من الصفر!");

        var activeShift = await EnsureActiveShiftAsync(context, staffUsername);

        var payReceipt = await GenerateUniqueReceiptNumberAsync(context, "PAY");
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = payReceipt,
            DailyShiftId = activeShift.Id,
            VehicleId = vehicleId,
            Type = TransactionType.Payment,
            Category = category,
            Amount = amount,
            PaymentMethod = mode,
            StaffUsername = staffUsername,
            TransactionDateTime = DateTime.Now,
            Description = description,
            PayeeOrVendor = payeeOrVendor,
            InvoiceNumber = invoiceNumber
        };
        await context.FinancialTransactions.AddAsync(transaction);

        if (mode == PaymentMode.ImmediateCash)
        {
            activeShift.SystemCalculatedCash -= amount;
        }

        string extraDetails = "";
        if (!string.IsNullOrWhiteSpace(payeeOrVendor)) extraDetails += $" | المصروف له: {payeeOrVendor}";
        if (!string.IsNullOrWhiteSpace(invoiceNumber)) extraDetails += $" | فاتورة: {invoiceNumber}";

        await LogAuditAsync(staffUsername, UserRole.Accountant, AuditAction.Insert, "Transactions", transaction.Id.ToString(), 
            $"تسجيل مصروف {description} بقيمة {amount} ₪ ({category}){extraDetails}");

        await context.SaveChangesAsync();
        return transaction;
    }

    public async Task<CustomerStatementDto> GetCustomerStatementAsync(Guid customerId)
    {
        await using var context = await GetContextAsync();
        var customer = await context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null) throw new InvalidOperationException("العميل غير موجود!");

        var contracts = await context.Contracts
            .AsNoTracking()
            .Include(c => c.Vehicle)
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.StartDateTime)
            .ToListAsync();

        var transactions = await context.FinancialTransactions
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.TransactionDateTime)
            .ToListAsync();

        var violations = await context.TrafficViolations
            .AsNoTracking()
            .Include(v => v.Vehicle)
            .Where(v => v.CustomerId == customerId)
            .OrderByDescending(v => v.ViolationDateTime)
            .ToListAsync();

        var totalAgreed = contracts
            .Where(c => c.Status != ContractStatus.Cancelled)
            .Sum(c => c.IsOpenEnded && c.Status == ContractStatus.Active ? c.OpenRentalAccruedAmount : c.TotalAgreedAmount);

        var totalPaid = transactions
            .Where(t => t.Type == TransactionType.Receipt)
            .Sum(t => t.Amount);

        var totalRemaining = Math.Max(0, totalAgreed - totalPaid);

        return new CustomerStatementDto
        {
            Customer = customer,
            Contracts = contracts,
            Transactions = transactions,
            TrafficViolations = violations,
            TotalAgreed = totalAgreed,
            TotalPaid = totalPaid,
            TotalRemainingDebts = Math.Max(customer.TotalDebts, totalRemaining),
            GeneratedAt = DateTime.Now
        };
    }

    // ==================== Company Profile & Settings ====================

    public async Task<CompanyProfile> GetCompanyProfileAsync()
    {
        await using var context = await GetContextAsync();
        var profile = await context.CompanyProfiles.FirstOrDefaultAsync();
        if (profile == null)
        {
            profile = new CompanyProfile();
            await context.CompanyProfiles.AddAsync(profile);
            await context.SaveChangesAsync();
        }
        return profile;
    }

    public async Task<CompanyProfile> SaveCompanyProfileAsync(CompanyProfile profile, string username)
    {
        await using var context = await GetContextAsync();
        var existing = await context.CompanyProfiles.FirstOrDefaultAsync();
        if (existing == null)
        {
            if (profile.Id == Guid.Empty) profile.Id = Guid.NewGuid();
            profile.UpdatedAt = DateTime.UtcNow;
            await context.CompanyProfiles.AddAsync(profile);
        }
        else
        {
            existing.CompanyName = profile.CompanyName;
            existing.CompanyEnglishName = profile.CompanyEnglishName;
            existing.CommercialRegisterNumber = profile.CommercialRegisterNumber;
            existing.TaxNumber = profile.TaxNumber;
            existing.PrimaryPhone = profile.PrimaryPhone;
            existing.SecondaryPhone = profile.SecondaryPhone;
            existing.Address = profile.Address;
            existing.Email = profile.Email;
            existing.LogoBase64 = profile.LogoBase64;
            existing.DefaultContractTerms = profile.DefaultContractTerms;
            existing.DailyKmLimit = profile.DailyKmLimit;
            existing.ExtraKmPrice = profile.ExtraKmPrice;
            existing.LateHourFee = profile.LateHourFee;
            existing.PromissoryNoteTerms = profile.PromissoryNoteTerms;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await LogAuditAsync(username, UserRole.Admin, AuditAction.Update, "CompanyProfile", profile.Id.ToString(), 
            "تحديث بيانات وهوية وشروط المكتب");

        await context.SaveChangesAsync();
        return existing ?? profile;
    }

    // ==================== Backup Restore ====================

    public async Task<bool> RestoreBackupAsync(string sourceBackupFilePath, string username)
    {
        if (!File.Exists(sourceBackupFilePath))
        {
            throw new FileNotFoundException("ملف النسخة الاحتياطية المحدد غير موجود على هذا المسار!", sourceBackupFilePath);
        }

        try
        {
            var dbPath = AppPaths.DatabaseFilePath;
            
            // نسخة أمان قبل الاسترجاع
            var preRestoreBackup = Path.Combine(AppPaths.AutoBackupsDirectory, $"prerestore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            if (File.Exists(dbPath))
            {
                File.Copy(dbPath, preRestoreBackup, overwrite: true);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Copy(sourceBackupFilePath, dbPath, overwrite: true);

            try { File.Delete(dbPath + "-wal"); } catch { }
            try { File.Delete(dbPath + "-shm"); } catch { }

            await LogAuditAsync(username, UserRole.Admin, AuditAction.BackupRestore, "Database", "Restore", 
                $"تم استرجاع قاعدة البيانات بنجاح من النسخة: {Path.GetFileName(sourceBackupFilePath)}");

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"فشل استرجاع قاعدة البيانات: {ex.Message}", ex);
        }
    }

    // ==================== Operational Data Reset (تهيئة وتصفير النظام التشغيلي) ====================

    public async Task<string> ResetOperationalDataAsync(string adminUsername)
    {
        // 1. أخذ نسخة احتياطية تلقائية آمنة قبل التصفير
        var autoBackupDir = AppPaths.AutoBackupsDirectory;
        if (!Directory.Exists(autoBackupDir)) Directory.CreateDirectory(autoBackupDir);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(autoBackupDir, $"CarRental_Backup_BeforeReset_{timestamp}.db");

        var currentDbPath = File.Exists(AppPaths.DatabaseFilePath)
            ? AppPaths.DatabaseFilePath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "car_rental_local.db");

        // 1. أخذ نسخة احتياطية آمنة وفورية عبر محرك SQLite دون أي تعليق للملفات
        if (File.Exists(currentDbPath))
        {
            try
            {
                using var source = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={currentDbPath}");
                using var destination = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={backupPath}");
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }
            catch
            {
                try
                {
                    File.Copy(currentDbPath, backupPath, overwrite: true);
                }
                catch { }
            }
        }

        // 2. تصفير ومسح السجلات التشغيلية داخل سياق ومعاملة برمجية موحدة وسريعة
        await using var context = await GetContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // حذف سجلات الفحوصات والمخالفات والحركات المالية والكمبيالات وسجلات الصيانة والعقود والورديات مباشرة وبشكل قطعي لمنع استثناءات التزامن
            await context.Database.ExecuteSqlRawAsync(@"
                DELETE FROM VehicleInspections;
                DELETE FROM TrafficViolations;
                DELETE FROM FinancialTransactions;
                DELETE FROM PromissoryNotes;
                DELETE FROM MaintenanceRecords;
                DELETE FROM Contracts;
                DELETE FROM DailyShifts;
                UPDATE Customers SET TotalDebts = 0;
                UPDATE Vehicles SET Status = 0, IsMaintenanceCritical = 0, CriticalMaintenanceReason = NULL;
            ");

            // إنشاء وردية نقدية أولى نظيفة للإدارة برصيد افتتاحي صفر
            var freshShift = new DailyShift
            {
                Id = Guid.NewGuid(),
                StaffUsername = adminUsername,
                StaffFullName = "مدير النظام",
                OpenedAt = DateTime.Now,
                OpeningBalance = 0,
                SystemCalculatedCash = 0,
                IsClosed = false
            };
            await context.DailyShifts.AddAsync(freshShift);

            // توثيق العملية الحساسة في سجل التدقيق الأمني ضمن نفس السياق والمعاملة لمنع أي تضارب أو deadlock
            context.AuditLogs.Add(new AuditLog
            {
                Username = adminUsername,
                Role = UserRole.Admin,
                Action = AuditAction.Delete,
                EntityName = "System",
                RecordId = "ResetOperationalData",
                Details = $"تم تنفيذ تهيئة وتصفير النظام وحذف كافة العقود والكمبيالات والحركات المالية والورديات وسجلات الصيانة، مع الإبقاء الكامل على الموظفين وكلمات سرهم والسيارات والعملاء. تم حفظ نسخة احتياطية تلقائية في: {Path.GetFileName(backupPath)}",
                Timestamp = DateTime.Now,
                MachineName = Environment.MachineName
            });

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            // إشعار كافة الشاشات والمكونات المفتوحة للتحديث اللحظي الفوري
            NotifyContractsDataChanged();
            CloudSyncService.TriggerSyncReset();

            return backupPath;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException($"تعذر إتمام تهيئة وتصفير النظام: {ex.Message}", ex);
        }
    }
}
