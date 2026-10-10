using CarRental.Core.Entities;
using CarRental.Core.Enums;
using CarRental.Infrastructure.Helpers;
using CarRental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(CarRentalDbContext context)
    {
        // 1. تطبيق إعدادات الأداء الفائق وقفل الـ WAL mode المتزامن
        await DatabaseSelfHealingService.ConfigurePerformancePragmasAsync(context);

        await context.Database.EnsureCreatedAsync();

        // 2. تشغيل النسخ الاحتياطي التلقائي اليومي
        DatabaseSelfHealingService.RunAutomaticDailyBackup(AppPaths.DatabaseFilePath, AppPaths.AutoBackupsDirectory);

        // الترقية الآمنة لجدول المركبات لإضافة خصائص الصيانة الحرجة إن لم تكن موجودة
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Vehicles ADD COLUMN IsMaintenanceCritical INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Vehicles ADD COLUMN CriticalMaintenanceReason TEXT NULL;"); } catch { }

        // الترقية الآمنة لجدول العملاء لإضافة صور الوثائق
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Customers ADD COLUMN IdPhotoBase64 TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Customers ADD COLUMN LicensePhotoBase64 TEXT NULL;"); } catch { }

        // الترقية الآمنة لجدول العقود لإضافة خصائص التمديد والتسوية
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN IsExtended INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN OriginalExpectedReturnDateTime TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN ExtensionDays INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN ExtensionAdditionalAmount TEXT NOT NULL DEFAULT 0;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN LateFee TEXT NOT NULL DEFAULT 0;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN FuelShortageFee TEXT NOT NULL DEFAULT 0;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN ExtraKmFee TEXT NOT NULL DEFAULT 0;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN DamageFee TEXT NOT NULL DEFAULT 0;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN SettlementNotes TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN IsOpenEnded INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN DiscountType TEXT NOT NULL DEFAULT 'None';"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN DiscountValue TEXT NOT NULL DEFAULT '0';"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN DiscountAmount TEXT NOT NULL DEFAULT '0';"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN OriginalAgreedAmount TEXT NOT NULL DEFAULT '0';"); } catch { }
        // الترقية الآمنة لجدول المعاملات المالية
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE FinancialTransactions ADD COLUMN CustomerId TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE FinancialTransactions ADD COLUMN VehicleId TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE FinancialTransactions ADD COLUMN PayeeOrVendor TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE FinancialTransactions ADD COLUMN InvoiceNumber TEXT NULL;"); } catch { }

        // الترقية الآمنة لجدول هوية المكتب
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE CompanyProfiles ADD COLUMN PromissoryNoteTerms TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE CompanyProfiles ADD COLUMN DailyKmLimit INTEGER NOT NULL DEFAULT 250;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE CompanyProfiles ADD COLUMN ExtraKmPrice TEXT NOT NULL DEFAULT 1;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE CompanyProfiles ADD COLUMN LateHourFee TEXT NOT NULL DEFAULT 20;"); } catch { }

        // الترقية الآمنة لجدول المركبات والفحوصات لإضافة كشف الأضرار والعلامات
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Vehicles ADD COLUMN DamageMarksJson TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE VehicleInspections ADD COLUMN DamageMarksJson TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE VehicleInspections ADD COLUMN VehicleId TEXT NULL;"); } catch { }

        // الترقية الآمنة لجدول العقود لإضافة السائق الإضافي
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN AdditionalDriverName TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN AdditionalDriverIdNumber TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN AdditionalDriverLicenseNumber TEXT NULL;"); } catch { }
        try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Contracts ADD COLUMN AdditionalDriverPhone TEXT NULL;"); } catch { }


        // إنشاء الجداول الجديدة إن لم تكن موجودة
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS CompanyProfiles (
                    Id TEXT PRIMARY KEY,
                    CompanyName TEXT NOT NULL,
                    CompanyEnglishName TEXT NOT NULL,
                    CommercialRegisterNumber TEXT NOT NULL,
                    TaxNumber TEXT NULL,
                    PrimaryPhone TEXT NOT NULL,
                    SecondaryPhone TEXT NULL,
                    Address TEXT NOT NULL,
                    Email TEXT NULL,
                    LogoBase64 TEXT NULL,
                    DefaultContractTerms TEXT NOT NULL,
                    DailyKmLimit INTEGER NOT NULL DEFAULT 250,
                    ExtraKmPrice TEXT NOT NULL DEFAULT 1,
                    LateHourFee TEXT NOT NULL DEFAULT 20,
                    PromissoryNoteTerms TEXT NULL,
                    UpdatedAt TEXT NOT NULL
                );");
        } catch { }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS TrafficViolations (
                    Id TEXT PRIMARY KEY,
                    ViolationNumber TEXT NOT NULL,
                    VehicleId TEXT NOT NULL,
                    CustomerId TEXT NULL,
                    ContractId TEXT NULL,
                    ViolationDateTime TEXT NOT NULL,
                    Amount TEXT NOT NULL,
                    Reason TEXT NOT NULL,
                    Location TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    IsPaidToAuthority INTEGER NOT NULL,
                    PaidToAuthorityDate TEXT NULL,
                    IsCollectedFromCustomer INTEGER NOT NULL,
                    CollectedDate TEXT NULL,
                    Notes TEXT NULL,
                    RecordedByUsername TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (VehicleId) REFERENCES Vehicles (Id) ON DELETE RESTRICT,
                    FOREIGN KEY (CustomerId) REFERENCES Customers (Id) ON DELETE SET NULL,
                    FOREIGN KEY (ContractId) REFERENCES Contracts (Id) ON DELETE SET NULL
                );");
        } catch { }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS VehicleInspections (
                    Id TEXT PRIMARY KEY,
                    ContractId TEXT NOT NULL,
                    Type INTEGER NOT NULL,
                    InspectionDateTime TEXT NOT NULL,
                    StaffUsername TEXT NOT NULL,
                    HasSpareTire INTEGER NOT NULL,
                    HasJackAndTools INTEGER NOT NULL,
                    HasFireExtinguisher INTEGER NOT NULL,
                    HasFirstAidKit INTEGER NOT NULL,
                    IsCleanInterior INTEGER NOT NULL,
                    IsCleanExterior INTEGER NOT NULL,
                    Odometer INTEGER NOT NULL,
                    FuelLevel INTEGER NOT NULL,
                    ScratchesDescription TEXT NULL,
                    DamageNotes TEXT NULL,
                    DamageMarksJson TEXT NULL,
                    VehicleId TEXT NULL,
                    CustomerSignatureAccepted INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (ContractId) REFERENCES Contracts (Id) ON DELETE CASCADE
                );");
        } catch { }

        // إنشاء الفهارس لتسريع الاستعلامات والأداء الفائق لمئات الآلاف من العمليات
        try
        {
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Contracts_Status ON Contracts (Status);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Contracts_CustomerId ON Contracts (CustomerId);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Contracts_VehicleId ON Contracts (VehicleId);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Contracts_StartDateTime ON Contracts (StartDateTime);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Contracts_ExpectedReturnDateTime ON Contracts (ExpectedReturnDateTime);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Contracts_CreatedAt ON Contracts (CreatedAt);");

            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FinancialTransactions_DailyShiftId ON FinancialTransactions (DailyShiftId);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FinancialTransactions_CustomerId ON FinancialTransactions (CustomerId);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FinancialTransactions_VehicleId ON FinancialTransactions (VehicleId);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FinancialTransactions_ContractId ON FinancialTransactions (ContractId);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FinancialTransactions_Type ON FinancialTransactions (Type);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_FinancialTransactions_TransactionDateTime ON FinancialTransactions (TransactionDateTime);");

            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Vehicles_Status ON Vehicles (Status);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Vehicles_IsActive ON Vehicles (IsActive);");

            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_DailyShifts_StaffUsername ON DailyShifts (StaffUsername);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_DailyShifts_IsClosed ON DailyShifts (IsClosed);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_DailyShifts_OpenedAt ON DailyShifts (OpenedAt);");

            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_TrafficViolations_Status ON TrafficViolations (Status);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_TrafficViolations_ViolationDateTime ON TrafficViolations (ViolationDateTime);");

            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_AuditLogs_Timestamp ON AuditLogs (Timestamp);");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_AuditLogs_Username ON AuditLogs (Username);");
        } catch { }

        // تهيئة ملف وهوية الشركة الافتراضية إن لم تكن موجودة
        try
        {
            if (!await context.CompanyProfiles.AnyAsync())
            {
                await context.CompanyProfiles.AddAsync(new CompanyProfile());
                await context.SaveChangesAsync();
            }
        }
        catch
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS CompanyProfiles;");
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS CompanyProfiles (
                        Id TEXT PRIMARY KEY,
                        CompanyName TEXT NOT NULL,
                        CompanyEnglishName TEXT NOT NULL,
                        CommercialRegisterNumber TEXT NOT NULL,
                        TaxNumber TEXT NULL,
                        PrimaryPhone TEXT NOT NULL,
                        SecondaryPhone TEXT NULL,
                        Address TEXT NOT NULL,
                        Email TEXT NULL,
                        LogoBase64 TEXT NULL,
                        DefaultContractTerms TEXT NOT NULL,
                        DailyKmLimit INTEGER NOT NULL DEFAULT 250,
                        ExtraKmPrice TEXT NOT NULL DEFAULT 1,
                        LateHourFee TEXT NOT NULL DEFAULT 20,
                        PromissoryNoteTerms TEXT NULL,
                        UpdatedAt TEXT NOT NULL
                    );");
                await context.CompanyProfiles.AddAsync(new CompanyProfile());
                await context.SaveChangesAsync();
            }
            catch { }
        }

        // فحص وتثبيت أسطول سيارات المكتب الأساسي المعتمد (12 سيارة أصلية)
        var hasCoreFleet = await context.Vehicles.AnyAsync(v => v.PlateNumber == "6-0639-95" || v.PlateNumber == "61");
        if (!hasCoreFleet)
        {
            await ReplaceWithCoreFleetAsync(context);
        }

        // فحص وتثبيت سجلات العملاء المعتمدين الأساسيين
        if (!await context.Customers.AnyAsync())
        {
            await SeedInitialCustomersAsync(context);
        }

        if (await context.Users.AnyAsync())
        {
            // تحديث المستخدمين الحاليين وضمان وجود كافة الحسابات الأساسية بأسماء عربية واضحة ورموز دخول نشطة
            var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            if (existingAdmin != null)
            {
                existingAdmin.IsActive = true;
                if (existingAdmin.PasswordHash == "admin123" || string.IsNullOrWhiteSpace(existingAdmin.PasswordHash))
                {
                    existingAdmin.PasswordHash = "1234";
                }
                if (string.IsNullOrWhiteSpace(existingAdmin.FullName) || existingAdmin.FullName.Contains('?') || existingAdmin.FullName.Contains('\uFFFD'))
                {
                    existingAdmin.FullName = "حسين (المدير العام)";
                }
            }
            else
            {
                await context.Users.AddAsync(new User
                {
                    Id = Guid.NewGuid(),
                    Username = "admin",
                    FullName = "حسين (المدير العام)",
                    PasswordHash = "1234",
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var existingHusen = await context.Users.FirstOrDefaultAsync(u => u.Username == "husen");
            if (existingHusen != null)
            {
                existingHusen.IsActive = true;
                if (string.IsNullOrWhiteSpace(existingHusen.PasswordHash)) existingHusen.PasswordHash = "1111";
                if (string.IsNullOrWhiteSpace(existingHusen.FullName) || existingHusen.FullName.Contains('?') || existingHusen.FullName.Contains('\uFFFD'))
                {
                    existingHusen.FullName = "حسين دراغمة";
                }
            }
            else
            {
                await context.Users.AddAsync(new User
                {
                    Id = Guid.NewGuid(),
                    Username = "husen",
                    FullName = "حسين دراغمة",
                    PasswordHash = "1111",
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var existingZaid = await context.Users.FirstOrDefaultAsync(u => u.Username == "zaid");
            if (existingZaid != null)
            {
                existingZaid.IsActive = true;
                if (string.IsNullOrWhiteSpace(existingZaid.PasswordHash)) existingZaid.PasswordHash = "3838";
                if (string.IsNullOrWhiteSpace(existingZaid.FullName) || existingZaid.FullName == "zaid" || existingZaid.FullName.Contains('?') || existingZaid.FullName.Contains('\uFFFD'))
                {
                    existingZaid.FullName = "زيد دراغمة";
                }
            }
            else
            {
                await context.Users.AddAsync(new User
                {
                    Id = Guid.NewGuid(),
                    Username = "zaid",
                    FullName = "زيد دراغمة",
                    PasswordHash = "3838",
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var existingStaff = await context.Users.FirstOrDefaultAsync(u => u.Username == "staff");
            if (existingStaff != null)
            {
                existingStaff.IsActive = true;
                if (existingStaff.PasswordHash == "staff123" || string.IsNullOrWhiteSpace(existingStaff.PasswordHash))
                {
                    existingStaff.PasswordHash = "5678";
                }
                if (string.IsNullOrWhiteSpace(existingStaff.FullName) || existingStaff.FullName.Contains('?') || existingStaff.FullName.Contains('\uFFFD'))
                {
                    existingStaff.FullName = "موظف الاستقبال";
                }
            }
            else
            {
                await context.Users.AddAsync(new User
                {
                    Id = Guid.NewGuid(),
                    Username = "staff",
                    FullName = "موظف الاستقبال",
                    PasswordHash = "5678",
                    Role = UserRole.Staff,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var existingAcc = await context.Users.FirstOrDefaultAsync(u => u.Username == "accountant");
            if (existingAcc != null)
            {
                existingAcc.IsActive = true;
                if (existingAcc.PasswordHash == "acc123" || string.IsNullOrWhiteSpace(existingAcc.PasswordHash))
                {
                    existingAcc.PasswordHash = "0000";
                }
                if (string.IsNullOrWhiteSpace(existingAcc.FullName) || existingAcc.FullName.Contains('?') || existingAcc.FullName.Contains('\uFFFD'))
                {
                    existingAcc.FullName = "محاسب النظام";
                }
            }
            else
            {
                await context.Users.AddAsync(new User
                {
                    Id = Guid.NewGuid(),
                    Username = "accountant",
                    FullName = "محاسب النظام",
                    PasswordHash = "0000",
                    Role = UserRole.Accountant,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // إصلاح وتوحيد أسماء الموظفين في الحركات المالية (توجيه اسم حسين إلى اسم المستخدم الموحد husen)
            var badStaffTx = await context.FinancialTransactions
                .Where(t => t.StaffUsername == "حسين" || string.IsNullOrWhiteSpace(t.StaffUsername))
                .ToListAsync();
            foreach (var tx in badStaffTx)
            {
                tx.StaffUsername = "husen";
            }

            // إصلاح وتوحيد أسماء الورديات
            var allDailyShifts = await context.DailyShifts.ToListAsync();
            var badShiftNames = allDailyShifts
                .Where(s => string.IsNullOrWhiteSpace(s.StaffFullName) || s.StaffFullName.Contains('?') || s.StaffFullName.Contains('\uFFFD'))
                .ToList();
            foreach (var s in badShiftNames)
            {
                s.StaffFullName = s.StaffUsername switch
                {
                    "admin" => "حسين (المدير العام)",
                    "husen" => "حسين دراغمة",
                    "zaid" => "زيد دراغمة",
                    "staff" => "موظف الاستقبال",
                    "accountant" => "محاسب النظام",
                    _ => "المشرف العام"
                };
            }

            // مزامنة وترحيل العمليات المالية والسجلات السابقة لتتوافق مع أسماء المستخدمين الحالية
            var allUsers = await context.Users.ToListAsync();
            foreach (var u in allUsers)
            {
                // تحديث العقود التي تحمل اسم المستخدم بدلاً من اسمه الكامل
                var userContracts = await context.Contracts
                    .Where(c => (c.CreatedByStaffName != null && c.CreatedByStaffName.ToLower() == u.Username.ToLower())
                             || (c.ClosedByStaffName != null && c.ClosedByStaffName.ToLower() == u.Username.ToLower()))
                    .ToListAsync();
                foreach (var c in userContracts)
                {
                    if (c.CreatedByStaffName != null && c.CreatedByStaffName.Equals(u.Username, StringComparison.OrdinalIgnoreCase))
                        c.CreatedByStaffName = u.FullName;
                    if (c.ClosedByStaffName != null && c.ClosedByStaffName.Equals(u.Username, StringComparison.OrdinalIgnoreCase))
                        c.ClosedByStaffName = u.FullName;
                }
            }

            // تسوية ومطابقة العقود المغلقة للعملاء الذين تم تصفير ديونهم بالكامل
            var debtFreeCustomers = await context.Customers
                .Where(cu => cu.TotalDebts <= 0)
                .Select(cu => cu.Id)
                .ToListAsync();

            var closedContractsToReconcile = await context.Contracts
                .Where(c => debtFreeCustomers.Contains(c.CustomerId) && c.Status == ContractStatus.Closed && c.RemainingAmount > 0)
                .ToListAsync();

            foreach (var c in closedContractsToReconcile)
            {
                c.PaidAmount = c.TotalAgreedAmount;
                c.RemainingAmount = 0;
            }

            // إصلاح ذاتي استباقي لأي عقود تم فتحها بطريقة "دفع آجل عند التسليم" وسجلت بالخطأ كمسددة نقداً دون ترحيل الدين
            try
            {
                var misconfiguredContracts = await context.Contracts
                    .Include(c => c.Customer)
                    .Include(c => c.Transactions)
                    .Where(c => c.PaymentMode == PaymentMode.DeferredOnReturn
                             && c.Status == ContractStatus.Active
                             && c.RemainingAmount == 0
                             && c.TotalAgreedAmount > 0)
                    .ToListAsync();

                if (misconfiguredContracts.Any())
                {
                    var activeShift = await context.DailyShifts.FirstOrDefaultAsync(s => !s.IsClosed);

                    foreach (var c in misconfiguredContracts)
                    {
                        var previouslyPaid = c.PaidAmount;
                        c.PaidAmount = 0;
                        c.RemainingAmount = c.TotalAgreedAmount;

                        // ترحيل الذمم إلى دين العميل
                        if (c.Customer != null)
                        {
                            c.Customer.TotalDebts += c.TotalAgreedAmount;
                        }

                        // إلغاء القيود المالية الخاطئة التي أضيفت للصندوق
                        if (c.Transactions != null && c.Transactions.Any())
                        {
                            var badReceipts = c.Transactions
                                .Where(t => t.Category == TransactionCategory.RentalPayment)
                                .ToList();

                            if (badReceipts.Any())
                            {
                                var totalBadCash = badReceipts
                                    .Where(t => t.PaymentMethod == PaymentMode.ImmediateCash || t.PaymentMethod == PaymentMode.DeferredOnReturn)
                                    .Sum(t => t.Amount);

                                if (activeShift != null && totalBadCash > 0)
                                {
                                    activeShift.SystemCalculatedCash = Math.Max(0, activeShift.SystemCalculatedCash - totalBadCash);
                                }

                                context.FinancialTransactions.RemoveRange(badReceipts);
                            }
                        }
                    }
                }
            }
            catch { }
            // توحيد ومطابقة ديون كافة العملاء المسجلين في النظام بدقة تامة وبلا أي فوارق
            try
            {
                var allCustomers = await context.Customers
                    .Include(c => c.Contracts)
                    .ToListAsync();
                var allCustomerIds = allCustomers.Select(c => c.Id).ToList();
                var violList = await context.TrafficViolations
                    .Where(v => !v.IsCollectedFromCustomer && v.CustomerId.HasValue && allCustomerIds.Contains(v.CustomerId.Value))
                    .Select(v => new { CustomerId = v.CustomerId!.Value, v.Amount })
                    .ToListAsync();

                var allUnpaidViolations = violList
                    .GroupBy(v => v.CustomerId)
                    .ToDictionary(g => g.Key, g => g.Sum(v => v.Amount));

                foreach (var cust in allCustomers)
                {
                    decimal trueDebt = 0;
                    if (cust.Contracts != null)
                    {
                        foreach (var c in cust.Contracts.Where(co => co.Status != ContractStatus.Cancelled))
                        {
                            if (c.IsOpenEnded && c.Status == ContractStatus.Active)
                            {
                                trueDebt += c.OpenRentalCurrentDue;
                            }
                            else
                            {
                                trueDebt += Math.Max(0, c.RemainingAmount);
                            }
                        }
                    }
                    if (allUnpaidViolations.TryGetValue(cust.Id, out var violDebt))
                    {
                        trueDebt += violDebt;
                    }
                    cust.TotalDebts = trueDebt;
                }
            }
            catch { }

            // إصلاح وتوحيد الحركات المالية غير المنسوبة لمركبة (ربط الحركات بعقودها ومركباتها تلقائياً لضمان دقة ربحية الأسطول)
            try
            {
                var contractsList = await context.Contracts
                    .AsNoTracking()
                    .Where(c => c.VehicleId != Guid.Empty)
                    .Select(c => new { c.Id, c.CustomerId, c.VehicleId, c.CreatedAt })
                    .ToListAsync();

                var contractMap = contractsList.ToDictionary(c => c.Id, c => c.VehicleId);

                var orphanTransactions = await context.FinancialTransactions
                    .Where(t => t.VehicleId == null)
                    .ToListAsync();

                foreach (var tx in orphanTransactions)
                {
                    if (tx.ContractId.HasValue && contractMap.TryGetValue(tx.ContractId.Value, out var vId))
                    {
                        tx.VehicleId = vId;
                    }
                    else if (tx.CustomerId.HasValue)
                    {
                        var matchingContract = contractsList
                            .Where(c => c.CustomerId == tx.CustomerId.Value)
                            .OrderByDescending(c => c.CreatedAt)
                            .FirstOrDefault();

                        if (matchingContract != null)
                        {
                            if (!tx.ContractId.HasValue) tx.ContractId = matchingContract.Id;
                            tx.VehicleId = matchingContract.VehicleId;
                        }
                    }
                }
            }
            catch { }

            // إصلاح وتعديل تواريخ الإرجاع الفعلية وأيام العقود المغلقة السابقة
            try
            {
                var closedContracts = await context.Contracts
                    .Where(c => c.Status == ContractStatus.Closed)
                    .ToListAsync();

                foreach (var c in closedContracts)
                {
                    if (!c.ActualReturnDateTime.HasValue)
                    {
                        c.ActualReturnDateTime = c.ClosedAt?.ToLocalTime() ?? c.CreatedAt.AddHours(2);
                    }

                    if (c.IsOpenEnded)
                    {
                        var end = c.ActualReturnDateTime ?? c.ClosedAt?.ToLocalTime() ?? c.StartDateTime.AddDays(1);
                        var actualDays = Math.Max(1, (int)Math.Ceiling((end - c.StartDateTime).TotalDays));
                        c.ExpectedReturnDateTime = c.StartDateTime.AddDays(actualDays);
                    }
                }
            }
            catch { }

            // إصلاح وتعديل العقود المفتوحة النشطة لضمان عدم اعتبارها متأخرة إطلاقاً
            try
            {
                var activeOpenContracts = await context.Contracts
                    .Where(c => c.Status == ContractStatus.Active && c.IsOpenEnded)
                    .ToListAsync();

                foreach (var c in activeOpenContracts)
                {
                    if (c.ExpectedReturnDateTime < DateTime.Now.AddYears(1))
                    {
                        c.ExpectedReturnDateTime = DateTime.Now.AddYears(10);
                    }
                }
            }
            catch { }

            await context.SaveChangesAsync();
            return;
        }

        // مستخدمين أساسيين للنظام برموز دخول 4 أرقام
        var adminUser = new User
        {
            Username = "admin",
            FullName = "المدير العام (أدمن)",
            PasswordHash = "1234",
            Role = UserRole.Admin,
            PhoneNumber = "0501234567",
            CreatedAt = DateTime.UtcNow
        };

        var staffUser = new User
        {
            Username = "staff",
            FullName = "موظف الاستقبال",
            PasswordHash = "5678",
            Role = UserRole.Staff,
            PhoneNumber = "0509876543",
            CreatedAt = DateTime.UtcNow
        };

        var accountantUser = new User
        {
            Username = "accountant",
            FullName = "محاسب النظام",
            PasswordHash = "0000",
            Role = UserRole.Accountant,
            PhoneNumber = "0554433221",
            CreatedAt = DateTime.UtcNow
        };

        await context.Users.AddRangeAsync(adminUser, staffUser, accountantUser);
        await context.SaveChangesAsync();
    }

    public static async Task ReplaceWithCoreFleetAsync(CarRentalDbContext context)
    {
        try
        {
            var inspections = await context.VehicleInspections.ToListAsync();
            if (inspections.Any()) context.VehicleInspections.RemoveRange(inspections);

            var violations = await context.TrafficViolations.ToListAsync();
            if (violations.Any()) context.TrafficViolations.RemoveRange(violations);

            var maintenance = await context.MaintenanceRecords.ToListAsync();
            if (maintenance.Any()) context.MaintenanceRecords.RemoveRange(maintenance);

            var transactions = await context.FinancialTransactions
                .Where(t => t.ContractId != null || t.VehicleId != null)
                .ToListAsync();
            if (transactions.Any()) context.FinancialTransactions.RemoveRange(transactions);

            var notes = await context.PromissoryNotes.ToListAsync();
            if (notes.Any()) context.PromissoryNotes.RemoveRange(notes);

            var contracts = await context.Contracts.ToListAsync();
            if (contracts.Any()) context.Contracts.RemoveRange(contracts);

            var oldVehicles = await context.Vehicles.ToListAsync();
            if (oldVehicles.Any()) context.Vehicles.RemoveRange(oldVehicles);

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReplaceWithCoreFleetAsync] Clean error: {ex.Message}");
        }

        await SeedInitialVehiclesAsync(context);
    }

    public static List<Vehicle> GetDefaultOfficeFleet()
    {
        return new List<Vehicle>
        {
            // 1. اكسنت احمر (هيونداي أكسنت - لون أحمر)
            new Vehicle
            {
                PlateNumber = "6-1021-95",
                Brand = "هيونداي",
                Model = "أكسنت أحمر",
                ModelYear = 2023,
                Color = "أحمر",
                VinNumber = "KMHCT4AE6PU100001",
                CurrentOdometer = 25000,
                DailyStandardRate = 150,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "شركة المشرق للتأمين",
                NextOilChangeOdometer = 30000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = "images/cars/accent_red.webp"
            },

            // 2. اكسنت بصمة (هيونداي أكسنت - تشغيل بصمة)
            new Vehicle
            {
                PlateNumber = "6-0639-95",
                Brand = "هيونداي",
                Model = "أكسنت بصمة",
                ModelYear = 2023,
                Color = "فضي",
                VinNumber = "KMHCT4AE8PU100002",
                CurrentOdometer = 32000,
                DailyStandardRate = 150,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "ترست العالمية للتأمين",
                NextOilChangeOdometer = 40000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = "images/cars/accent_smart.jpg"
            },

            // 3. اكسنت 61 (هيونداي أكسنت - رقم تعريفي/لوحة 61)
            new Vehicle
            {
                PlateNumber = "61",
                Brand = "هيونداي",
                Model = "أكسنت 61",
                ModelYear = 2022,
                Color = "رمادي فيراني",
                VinNumber = "KMHCT4AE2PU100003",
                CurrentOdometer = 41000,
                DailyStandardRate = 140,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "التكافل الفلسطينية للتأمين",
                NextOilChangeOdometer = 50000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = "images/cars/accent_61.jpg"
            },

            // 4. اكسنت ابيض (هيونداي أكسنت - لون أبيض)
            new Vehicle
            {
                PlateNumber = "6-1022-95",
                Brand = "هيونداي",
                Model = "أكسنت أبيض",
                ModelYear = 2023,
                Color = "أبيض",
                VinNumber = "KMHCT4AE3PU100004",
                CurrentOdometer = 28000,
                DailyStandardRate = 150,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "المجموعة الأهلية للتأمين",
                NextOilChangeOdometer = 35000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = "images/cars/accent_white.jpg"
            },

            // 5. ستونيك (كيا ستونيك - Kia Stonic)
            new Vehicle
            {
                PlateNumber = "6-2031-95",
                Brand = "كيا",
                Model = "ستونيك (Kia Stonic)",
                ModelYear = 2023,
                Color = "أحمر",
                VinNumber = "KNADC81B8P100005",
                CurrentOdometer = 21000,
                DailyStandardRate = 180,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "شركة المشرق للتأمين",
                NextOilChangeOdometer = 30000,
                NextMaintenanceDate = DateTime.Today.AddMonths(7),
                ImageUrl = "images/cars/kia_stonic.jpg"
            },

            // 6. سبورتاج (كيا سبورتاج - Kia Sportage)
            new Vehicle
            {
                PlateNumber = "6-2032-95",
                Brand = "كيا",
                Model = "سبورتاج (Kia Sportage)",
                ModelYear = 2022,
                Color = "أبيض",
                VinNumber = "KNDPM3AC7N100006",
                CurrentOdometer = 35000,
                DailyStandardRate = 230,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "ترست العالمية للتأمين",
                NextOilChangeOdometer = 40000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = "images/cars/kia_sportage.jpg"
            },

            // 7. فينو رمادي (هيونداي فينيو - لون رمادي / Hyundai Venue)
            new Vehicle
            {
                PlateNumber = "6-3041-95",
                Brand = "هيونداي",
                Model = "فينيو رمادي (Hyundai Venue)",
                ModelYear = 2023,
                Color = "رمادي",
                VinNumber = "KMHCT81D9PU100007",
                CurrentOdometer = 19500,
                DailyStandardRate = 160,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "شركة المشرق للتأمين",
                NextOilChangeOdometer = 25000,
                NextMaintenanceDate = DateTime.Today.AddMonths(8),
                ImageUrl = "images/cars/venue_grey.webp"
            },

            // 8. فينو كحلي (هيونداي فينيو - لون كحلي)
            new Vehicle
            {
                PlateNumber = "6-3042-95",
                Brand = "هيونداي",
                Model = "فينيو كحلي (Hyundai Venue)",
                ModelYear = 2023,
                Color = "كحلي",
                VinNumber = "KMHCT81D1PU100008",
                CurrentOdometer = 22000,
                DailyStandardRate = 160,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "التكافل الفلسطينية للتأمين",
                NextOilChangeOdometer = 30000,
                NextMaintenanceDate = DateTime.Today.AddMonths(7),
                ImageUrl = "images/cars/venue_navy.jpg"
            },

            // 9. فينو اسود (هيونداي فينيو - لون أسود)
            new Vehicle
            {
                PlateNumber = "6-3043-95",
                Brand = "هيونداي",
                Model = "فينيو أسود (Hyundai Venue)",
                ModelYear = 2023,
                Color = "أسود",
                VinNumber = "KMHCT81D2PU100009",
                CurrentOdometer = 18000,
                DailyStandardRate = 160,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "المجموعة الأهلية للتأمين",
                NextOilChangeOdometer = 25000,
                NextMaintenanceDate = DateTime.Today.AddMonths(8),
                ImageUrl = "images/cars/venue_black.webp"
            },

            // 10. فينو خمري (هيونداي فينيو - لون خمري/ماروني)
            new Vehicle
            {
                PlateNumber = "6-3044-95",
                Brand = "هيونداي",
                Model = "فينيو خمري (Hyundai Venue)",
                ModelYear = 2023,
                Color = "خمري",
                VinNumber = "KMHCT81D4PU100010",
                CurrentOdometer = 26000,
                DailyStandardRate = 160,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "شركة المشرق للتأمين",
                NextOilChangeOdometer = 30000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = "images/cars/venue_burgundy.jpg"
            },

            // 11. فينو احمر (هيونداي فينيو - لون أحمر)
            new Vehicle
            {
                PlateNumber = "6-3045-95",
                Brand = "هيونداي",
                Model = "فينيو أحمر (Hyundai Venue)",
                ModelYear = 2023,
                Color = "أحمر",
                VinNumber = "KMHCT81D5PU100011",
                CurrentOdometer = 24000,
                DailyStandardRate = 160,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "ترست العالمية للتأمين",
                NextOilChangeOdometer = 30000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = "images/cars/venue_red.webp"
            },

            // 12. بيكانتو (كيا بيكانتو - Kia Picanto)
            new Vehicle
            {
                PlateNumber = "6-4051-95",
                Brand = "كيا",
                Model = "بيكانتو (Kia Picanto)",
                ModelYear = 2024,
                Color = "أبيض",
                VinNumber = "KNABA5128R100012",
                CurrentOdometer = 14000,
                DailyStandardRate = 120,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "شركة المشرق للتأمين",
                NextOilChangeOdometer = 20000,
                NextMaintenanceDate = DateTime.Today.AddMonths(9),
                ImageUrl = "images/cars/kia_picanto.png"
            }
        };
    }

    private static async Task SeedInitialVehiclesAsync(CarRentalDbContext context)
    {
        var existingPlates = await context.Vehicles.Select(v => v.PlateNumber.ToLower()).ToListAsync();
        var initialVehicles = GetDefaultOfficeFleet();

        var toAdd = initialVehicles
            .Where(v => !existingPlates.Contains(v.PlateNumber.ToLower()))
            .ToList();

        if (toAdd.Count > 0)
        {
            await context.Vehicles.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();
        }
    }

    public static List<Customer> GetDefaultRegisteredCustomers()
    {
        return new List<Customer>
        {
            new Customer
            {
                Id = Guid.NewGuid(),
                FullName = "زيد بشار فريح دراغمه",
                NationalIdOrPassport = "409991781",
                PrimaryPhone = "0598776026",
                EmergencyPhone = "",
                LicenseNumber = "D2855945AA",
                Address = "طوباس الاسكان",
                DateOfBirth = new DateTime(1998, 1, 1),
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                TotalDebts = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByUsername = "admin"
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                FullName = "احمد عنان حسني دراغمة",
                NationalIdOrPassport = "40998632",
                PrimaryPhone = "059976532",
                EmergencyPhone = "",
                LicenseNumber = "DL0r832743",
                Address = "طوباس حاره أبو دواس",
                Notes = "سليم وامين",
                DateOfBirth = new DateTime(1997, 5, 10),
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                TotalDebts = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByUsername = "admin"
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                FullName = "وسيم محمد ابراهيم عبد الرازق",
                NationalIdOrPassport = "4099765312",
                PrimaryPhone = "05997653110",
                EmergencyPhone = "",
                LicenseNumber = "DL-883NFUCN",
                Address = "طوباس الثعرة",
                Notes = "جيد",
                DateOfBirth = new DateTime(1996, 3, 15),
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                TotalDebts = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByUsername = "admin"
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                FullName = "حسين احمد راشد دراغمه",
                NationalIdOrPassport = "411993819",
                PrimaryPhone = "0566655550",
                EmergencyPhone = "",
                LicenseNumber = "DL0384HFD83",
                Address = "طوباس حي الحديقة",
                DateOfBirth = new DateTime(1995, 8, 20),
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                TotalDebts = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByUsername = "admin"
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                FullName = "محمد لؤي محمد ابو دواس ",
                NationalIdOrPassport = "409996816",
                PrimaryPhone = "0598733510",
                EmergencyPhone = "",
                LicenseNumber = "Sl-843rfu8433",
                Address = "طوباس منزل الدكتور لؤي",
                Notes = "زلمه محترم",
                DateOfBirth = new DateTime(1999, 11, 25),
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                TotalDebts = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByUsername = "admin"
            }
        };
    }

    private static async Task SeedInitialCustomersAsync(CarRentalDbContext context)
    {
        var existingIds = await context.Customers.Select(c => c.NationalIdOrPassport).ToListAsync();
        var defaults = GetDefaultRegisteredCustomers();
        var toAdd = defaults.Where(c => !existingIds.Contains(c.NationalIdOrPassport)).ToList();
        if (toAdd.Count > 0)
        {
            await context.Customers.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();
        }
    }
}
