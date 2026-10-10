using CarRental.Core.Enums;

namespace CarRental.Core.Entities;

public class TrafficViolation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // رقم إشعار المخالفة الرسمي
    public string ViolationNumber { get; set; } = string.Empty;
    
    // السيارة المعنية
    public Guid VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    
    // العميل المسؤول (يتم ربطه آلياً بناءً على تاريخ ووقت المخالفة بالعقد النشط حينها)
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    // العقد المرتبط إن وجد
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    
    // تفاصيل المخالفة
    public DateTime ViolationDateTime { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    
    // الحالة
    public ViolationStatus Status { get; set; } = ViolationStatus.PendingCollection;
    
    // هل سددها المكتب للمرور؟
    public bool IsPaidToAuthority { get; set; } = false;
    public DateTime? PaidToAuthorityDate { get; set; }
    
    // هل تم تحصيلها من المستأجر؟
    public bool IsCollectedFromCustomer { get; set; } = false;
    public DateTime? CollectedDate { get; set; }
    
    public string? Notes { get; set; }
    public string RecordedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
