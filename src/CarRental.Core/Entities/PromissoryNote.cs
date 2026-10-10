using CarRental.Core.Enums;

namespace CarRental.Core.Entities;

public class PromissoryNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // رقم الكمبيالة
    public string NoteNumber { get; set; } = string.Empty;
    
    // الارتباط بالعقد والعميل
    public Guid ContractId { get; set; }
    public Contract? Contract { get; set; }
    
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    // قيمة مبلغ الضمان
    public decimal Amount { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.Today;
    public DateTime? DueDate { get; set; }
    
    // حالة الكمبيالة (محجوزة كضمان، تم الإفراج عنها وإعادتها، تم التحصيل)
    public PromissoryNoteStatus Status { get; set; } = PromissoryNoteStatus.HeldInTrust;
    
    public DateTime? ReleasedAt { get; set; }
    public string? ReleasedByStaffName { get; set; }
    
    public string? Notes { get; set; }
    public string LegalText { get; set; } = string.Empty;
}
