using CarRental.Core.Enums;

namespace CarRental.Core.Entities;

public class FinancialTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // رقم السند
    public string ReceiptNumber { get; set; } = string.Empty;
    
    // نوع المعاملة: قبض (Receipt) أو صرف (Payment)
    public TransactionType Type { get; set; } = TransactionType.Receipt;
    
    // التصنيف
    public TransactionCategory Category { get; set; } = TransactionCategory.RentalPayment;
    
    public decimal Amount { get; set; }
    public PaymentMode PaymentMethod { get; set; } = PaymentMode.ImmediateCash;
    
    // الموظف الذي نفذ الحركة
    public string StaffUsername { get; set; } = string.Empty;
    public DateTime TransactionDateTime { get; set; } = DateTime.Now;
    public string Description { get; set; } = string.Empty;
    
    // اسم المستفيد / مكان أو ورشة أو مغسلة الصرف
    public string? PayeeOrVendor { get; set; }
    
    // رقم الفاتورة أو السند الخارجي
    public string? InvoiceNumber { get; set; }
    
    // الارتباطات الاختيارية
    public Guid? DailyShiftId { get; set; }
    public DailyShift? DailyShift { get; set; }
    
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
}
