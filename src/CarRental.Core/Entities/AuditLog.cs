using CarRental.Core.Enums;

namespace CarRental.Core.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;
    public AuditAction Action { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string MachineName { get; set; } = Environment.MachineName;
}
