using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using CarRental.Core.Entities;
using CarRental.Core.Enums;
using CarRental.Core.Interfaces;
using CarRental.Infrastructure.Data;

namespace CarRental.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IDbContextFactory<CarRentalDbContext> _contextFactory;
    private readonly HashSet<string> _readIds = new();
    private readonly HashSet<string> _dismissedIds = new();
    private List<SystemNotification> _notifications = new();

    public event Action? OnChanged;

    public NotificationService(IDbContextFactory<CarRentalDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<SystemNotification> Notifications => _notifications
        .Where(n => !_dismissedIds.Contains(n.Id))
        .OrderBy(n => n.IsRead)
        .ThenByDescending(n => n.Severity == NotificationSeverity.Danger)
        .ThenByDescending(n => n.CreatedAt)
        .ToList();

    public int TotalCount => Notifications.Count;

    public int UnreadCount => Notifications.Count(n => !n.IsRead);

    public int DangerCount => Notifications.Count(n => !n.IsRead && n.Severity == NotificationSeverity.Danger);

    public void MarkAsRead(string id)
    {
        _readIds.Add(id);
        var item = _notifications.FirstOrDefault(n => n.Id == id);
        if (item != null) item.IsRead = true;
        OnChanged?.Invoke();
    }

    public void MarkAllAsRead()
    {
        foreach (var item in _notifications)
        {
            _readIds.Add(item.Id);
            item.IsRead = true;
        }
        OnChanged?.Invoke();
    }

    public void Dismiss(string id)
    {
        _dismissedIds.Add(id);
        _readIds.Add(id);
        OnChanged?.Invoke();
    }

    public async Task RefreshNotificationsAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var list = new List<SystemNotification>();

            // 1. العقود المتأخرة عن موعد الإرجاع الرسمي
            var now = DateTime.Now;
            var overdueContracts = await context.Contracts
                .AsNoTracking()
                .Include(c => c.Customer)
                .Include(c => c.Vehicle)
                .Where(c => c.Status == ContractStatus.Active && c.ExpectedReturnDateTime < now)
                .OrderBy(c => c.ExpectedReturnDateTime)
                .ToListAsync();

            foreach (var c in overdueContracts)
            {
                var delay = now - c.ExpectedReturnDateTime;
                var delayText = delay.TotalDays >= 1
                    ? $"{Math.Floor(delay.TotalDays)} يوم و {delay.Hours} ساعة"
                    : $"{Math.Max(1, (int)delay.TotalHours)} ساعة";

                var notifId = $"contract-overdue-{c.Id}";
                list.Add(new SystemNotification
                {
                    Id = notifId,
                    Title = $"🚨 عقد إيجار متأخر: {c.ContractNumber}",
                    Description = $"العميل: {c.Customer?.FullName ?? "غير محدد"} ({c.Customer?.PrimaryPhone ?? "بدون هاتف"})\nالمركبة: {c.Vehicle?.Brand} {c.Vehicle?.Model} [لوحة {c.Vehicle?.PlateNumber}].\nموعد الإرجاع كان: {c.ExpectedReturnDateTime:yyyy/MM/dd HH:mm} (تأخير {delayText}). يرجى التواصل فوراً لإرجاع السيارة أو تمديد العقد وتحصيل الرسوم.",
                    Severity = NotificationSeverity.Danger,
                    Category = NotificationCategory.OverdueContract,
                    TargetUrl = "contracts",
                    SoundType = "danger",
                    CreatedAt = c.ExpectedReturnDateTime,
                    IsRead = _readIds.Contains(notifId),
                    ActionLabel = "إدارة العقد والإرجاع",
                    BadgeLabel = $"تأخير {delayText}"
                });
            }

            // 2. المركبات وحالات الصيانة والتراخيص
            var vehicles = await context.Vehicles.AsNoTracking().ToListAsync();

            foreach (var v in vehicles)
            {
                // مركبات محجوبة بالصيانة أو متوقفة إجبارياً
                if (v.Status == VehicleStatus.InMaintenance || v.IsBlockedByCriticalMaintenance)
                {
                    var notifId = $"vehicle-maint-{v.Id}";
                    list.Add(new SystemNotification
                    {
                        Id = notifId,
                        Title = $"⛔ مركبة محجوبة بالصيانة: {v.Brand} {v.Model}",
                        Description = $"المركبة [لوحة {v.PlateNumber}] متوقفة عن العمل وممنوعة من التأجير!\nالسبب: {v.CriticalMaintenanceReason ?? "صيانة إلزامية عاجلة"}.\nالعداد الحالي: {v.CurrentOdometer:N0} كم.",
                        Severity = NotificationSeverity.Danger,
                        Category = NotificationCategory.VehicleMaintenance,
                        TargetUrl = "fleet",
                        SoundType = "maintenance",
                        CreatedAt = DateTime.Now,
                        IsRead = _readIds.Contains(notifId),
                        ActionLabel = "فحص حالة الصيانة",
                        BadgeLabel = "حظر صيانة"
                    });
                }

                // تراخيص المركبات المنتهية أو القريبة (خلال 14 يوم)
                if (v.DaysUntilLicenseExpiry <= 14)
                {
                    var isExpired = v.DaysUntilLicenseExpiry <= 0;
                    var notifId = $"vehicle-lic-{v.Id}";
                    list.Add(new SystemNotification
                    {
                        Id = notifId,
                        Title = isExpired ? $"🚨 ترخيص منتهي: {v.Brand} {v.Model}" : $"⚠️ اقتراب انتهاء الترخيص: {v.Brand} {v.Model}",
                        Description = isExpired
                            ? $"رخصة سير المركبة [لوحة {v.PlateNumber}] منتهية الصلاحية تماماً منذ {Math.Abs(v.DaysUntilLicenseExpiry)} يوم! يحظر قيادتها لتجنب الحجز والمخالفات المرورية."
                            : $"رخصة سير المركبة [لوحة {v.PlateNumber}] تنتهي خلال {v.DaysUntilLicenseExpiry} يوم (تاريخ الانتهاء: {v.LicenseExpiryDate:yyyy/MM/dd}). يرجى تجديد الترخيص والفحص الدوري.",
                        Severity = isExpired ? NotificationSeverity.Danger : NotificationSeverity.Warning,
                        Category = NotificationCategory.VehicleLicense,
                        TargetUrl = "fleet",
                        SoundType = isExpired ? "danger" : "warning",
                        CreatedAt = DateTime.Now,
                        IsRead = _readIds.Contains(notifId),
                        ActionLabel = "تجديد الرخصة بالأسطول",
                        BadgeLabel = isExpired ? "رخصة منتهية" : $"باقي {v.DaysUntilLicenseExpiry} يوم"
                    });
                }

                // موعد الصيانة المجدولة (خلال 14 يوماً من التاريخ المحدد أو متجاوز)
                if (v.NextMaintenanceDate != default && v.NextMaintenanceDate.Year > 2000 && v.DaysUntilMaintenance <= 14)
                {
                    var isOverdue = v.DaysUntilMaintenance <= 0;
                    var notifId = $"vehicle-maintenance-{v.Id}";
                    list.Add(new SystemNotification
                    {
                        Id = notifId,
                        Title = isOverdue ? $"🚨 تجاوز موعد الصيانة المجدولة: {v.Brand} {v.Model}" : $"📅 اقتراب موعد الصيانة: {v.Brand} {v.Model}",
                        Description = isOverdue
                            ? $"المركبة [لوحة {v.PlateNumber}] تجاوزت تاريخ الصيانة المحدد لها بتاريخ ({v.NextMaintenanceDate:yyyy/MM/dd}) منذ {Math.Abs(v.DaysUntilMaintenance)} يوم! يرجى إدخالها للصيانة فوراً."
                            : $"المركبة [لوحة {v.PlateNumber}] يحل موعد صيانتها المجدولة خلال {v.DaysUntilMaintenance} يوم (تاريخ الاستحقاق: {v.NextMaintenanceDate:yyyy/MM/dd}).",
                        Severity = isOverdue ? NotificationSeverity.Danger : NotificationSeverity.Warning,
                        Category = NotificationCategory.OilChange,
                        TargetUrl = "fleet",
                        SoundType = "maintenance",
                        CreatedAt = DateTime.Now,
                        IsRead = _readIds.Contains(notifId),
                        ActionLabel = "إدارة صيانة المركبة",
                        BadgeLabel = isOverdue ? "صيانة متأخرة" : $"باقي {v.DaysUntilMaintenance} يوم"
                    });
                }
            }


            // 4. رخص قيادة العملاء النشطين المنتهية أو القريبة
            var activeContractsWithCustomer = await context.Contracts
                .AsNoTracking()
                .Include(c => c.Customer)
                .Include(c => c.Vehicle)
                .Where(c => c.Status == ContractStatus.Active && c.Customer != null)
                .ToListAsync();

            foreach (var ac in activeContractsWithCustomer)
            {
                if (ac.Customer != null && ac.Customer.DaysUntilLicenseExpiry <= 7)
                {
                    var isExpired = ac.Customer.IsLicenseExpired;
                    var notifId = $"customer-lic-{ac.CustomerId}";
                    list.Add(new SystemNotification
                    {
                        Id = notifId,
                        Title = isExpired ? $"🪪 رخصة قيادة المستأجر منتهية: {ac.Customer.FullName}" : $"⚠️ اقتراب انتهاء رخصة المستأجر: {ac.Customer.FullName}",
                        Description = $"العميل المستأجر للمركبة [{ac.Vehicle?.PlateNumber}] رخصة قيادته {(isExpired ? "منتهية الصلاحية" : "تنتهي قريباً")} بتاريخ {ac.Customer.LicenseExpiryDate:yyyy/MM/dd}. يرجى طلب تحديث صورة الرخصة الرسمية.",
                        Severity = isExpired ? NotificationSeverity.Danger : NotificationSeverity.Warning,
                        Category = NotificationCategory.CustomerLicense,
                        TargetUrl = "customers",
                        SoundType = isExpired ? "danger" : "warning",
                        CreatedAt = DateTime.Now,
                        IsRead = _readIds.Contains(notifId),
                        ActionLabel = "ملف العميل",
                        BadgeLabel = isExpired ? "رخصة منتهية" : "تجديد رخصة"
                    });
                }
            }

            _notifications = list;
            OnChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotificationService] Error refreshing notifications: {ex.Message}");
        }
    }
}
