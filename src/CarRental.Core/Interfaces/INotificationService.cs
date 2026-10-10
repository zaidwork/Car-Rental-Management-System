namespace CarRental.Core.Interfaces;

public enum NotificationSeverity
{
    Danger,
    Warning,
    Info,
    Success
}

public enum NotificationCategory
{
    OverdueContract,
    VehicleMaintenance,
    VehicleLicense,
    OilChange,
    CustomerLicense,
    TrafficViolation,
    FinancialDebt,
    System
}

public class SystemNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public NotificationCategory Category { get; set; } = NotificationCategory.System;
    public string TargetUrl { get; set; } = string.Empty;
    public string SoundType { get; set; } = "info"; // "danger", "warning", "maintenance", "contract", "info"
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; } = false;
    public string ActionLabel { get; set; } = "الانتقال للمعاينة";
    public string? BadgeLabel { get; set; }
}

public interface INotificationService
{
    IReadOnlyList<SystemNotification> Notifications { get; }
    int TotalCount { get; }
    int UnreadCount { get; }
    int DangerCount { get; }
    event Action? OnChanged;
    Task RefreshNotificationsAsync();
    void MarkAsRead(string id);
    void MarkAllAsRead();
    void Dismiss(string id);
}
