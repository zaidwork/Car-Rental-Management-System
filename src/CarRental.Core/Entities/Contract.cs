using CarRental.Core.Enums;

namespace CarRental.Core.Entities;

public class Contract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // رقم العقد الرسمي المسلسل
    public string ContractNumber { get; set; } = string.Empty;
    
    // معرف العميل والسيارة
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    
    public Guid VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    
    // الفترة الزمنية للتأجير
    public DateTime StartDateTime { get; set; } = DateTime.Now;
    public DateTime ExpectedReturnDateTime { get; set; } = DateTime.Now.AddDays(1);
    public DateTime? ActualReturnDateTime { get; set; }
    
    // العداد والوقود عند الاستلام
    public int StartOdometer { get; set; }
    public int? EndOdometer { get; set; }
    public FuelLevel FuelLevelStart { get; set; } = FuelLevel.Full;
    public FuelLevel? FuelLevelEnd { get; set; }
    
    // التسعير المفتوح والاتفاقي
    public decimal DailyAgreedRate { get; set; }       // السعر اليومي المتفق عليه يدوياً
    public decimal TotalAgreedAmount { get; set; }      // إجمالي المبلغ المتفق عليه
    public decimal PaidAmount { get; set; }             // المبلغ المدفوع مقدماً
    public decimal RemainingAmount { get; set; }        // المبلغ المتبقي (ذمم أو مؤجل)
    
    // نظام الخصم (Discount Management)
    public string DiscountType { get; set; } = "None";   // "None", "Percentage", "FixedAmount"
    public decimal DiscountValue { get; set; } = 0;       // e.g. 10 for 10%, or 50 for 50 NIS
    public decimal DiscountAmount { get; set; } = 0;      // قيمة الخصم المالي الفعلي بالشيكل
    public decimal OriginalAgreedAmount { get; set; } = 0; // المبلغ الإجمالي الأصلي قبل الخصم
    
    // طريقة الدفع
    public PaymentMode PaymentMode { get; set; } = PaymentMode.ImmediateCash;
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    
    // كمبيالة الضمان
    public decimal PromissoryNoteAmount { get; set; }   // مبلغ الضمان للكمبيالة
    public string? PromissoryNoteNumber { get; set; }
    
    // المسؤولية وسجل الموظفين
    public string CreatedByStaffName { get; set; } = string.Empty;
    public string? ClosedByStaffName { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    
    public string? TermsAndConditionsText { get; set; }
    public string? Notes { get; set; }

    // السائق الإضافي المعتمد (اختياري)
    public string? AdditionalDriverName { get; set; }
    public string? AdditionalDriverIdNumber { get; set; }
    public string? AdditionalDriverLicenseNumber { get; set; }
    public string? AdditionalDriverPhone { get; set; }

    // خصائص التمديد (Contract Extension)
    public bool IsExtended { get; set; } = false;
    public DateTime? OriginalExpectedReturnDateTime { get; set; }
    public int ExtensionDays { get; set; } = 0;
    public decimal ExtensionAdditionalAmount { get; set; } = 0;

    // خصائص التسوية الذكية عند الإغلاق (Smart Return Settlement)
    public decimal LateFee { get; set; } = 0;
    public decimal FuelShortageFee { get; set; } = 0;
    public decimal ExtraKmFee { get; set; } = 0;
    public decimal DamageFee { get; set; } = 0;
    public string? SettlementNotes { get; set; }

    // خصائص التأجير المفتوح (Open-ended Rental)
    public bool IsOpenEnded { get; set; } = false;
    public int OpenRentalDays => Math.Max(1, (int)Math.Ceiling(((ActualReturnDateTime ?? (Status == ContractStatus.Closed ? ClosedAt : null) ?? DateTime.Now) - StartDateTime).TotalDays));
    public decimal OpenRentalAccruedAmount => IsOpenEnded ? (OpenRentalDays * DailyAgreedRate) : TotalAgreedAmount;
    public decimal OpenRentalCurrentDue => IsOpenEnded ? Math.Max(0, OpenRentalAccruedAmount - PaidAmount) : RemainingAmount;

    // Helper: Total days calculated
    public int RentalDays => IsOpenEnded 
        ? OpenRentalDays
        : Math.Max(1, (int)Math.Ceiling(((Status == ContractStatus.Closed ? (ActualReturnDateTime ?? ClosedAt ?? ExpectedReturnDateTime) : ExpectedReturnDateTime) - StartDateTime).TotalDays));

    // هل العقد متأخر عن موعد الإرجاع؟ (العقود المفتوحة لا تعتبر متأخرة لأنها بدون تاريخ إرجاع محدد)
    public bool IsOverdue => !IsOpenEnded && Status == ContractStatus.Active && DateTime.Now > ExpectedReturnDateTime;
    public int OverdueHours => IsOverdue ? (int)Math.Ceiling((DateTime.Now - ExpectedReturnDateTime).TotalHours) : 0;
    public int OverdueDays => IsOverdue ? (int)Math.Ceiling((DateTime.Now - ExpectedReturnDateTime).TotalDays) : 0;

    // Navigation
    public PromissoryNote? PromissoryNote { get; set; }
    public ICollection<FinancialTransaction> Transactions { get; set; } = new List<FinancialTransaction>();
    public ICollection<TrafficViolation> TrafficViolations { get; set; } = new List<TrafficViolation>();
    public ICollection<VehicleInspection> Inspections { get; set; } = new List<VehicleInspection>();
}
