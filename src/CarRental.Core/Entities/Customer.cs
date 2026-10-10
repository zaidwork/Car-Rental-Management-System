namespace CarRental.Core.Entities;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // الاسم الرباعي الإلزامي
    public string FullName { get; set; } = string.Empty;
    
    // رقم الهوية الوطنية أو جواز السفر
    public string NationalIdOrPassport { get; set; } = string.Empty;
    
    // رقم رخصة القيادة
    public string LicenseNumber { get; set; } = string.Empty;
    
    // تاريخ انتهاء رخصة القيادة
    public DateTime LicenseExpiryDate { get; set; }
    
    // تاريخ الميلاد (للتحقق من السن القانوني >= 21)
    public DateTime DateOfBirth { get; set; }
    
    // أرقام التواصل
    public string PrimaryPhone { get; set; } = string.Empty;
    public string EmergencyPhone { get; set; } = string.Empty;
    
    public string? Address { get; set; }
    
    // إجمالي الديون السابقة
    public decimal TotalDebts { get; set; } = 0;
    
    // صور وثائق العميل (أرشفة إلكترونية للرجوع الفوري)
    public string? IdPhotoBase64 { get; set; }
    public string? LicensePhotoBase64 { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUsername { get; set; } = string.Empty;

    // Helper properties
    public int Age => (int)((DateTime.Today - DateOfBirth).TotalDays / 365.2425);
    public bool IsLicenseExpired => LicenseExpiryDate.Date < DateTime.Today;
    public int DaysUntilLicenseExpiry => (int)(LicenseExpiryDate.Date - DateTime.Today).TotalDays;

    // Navigation
    public BlacklistRecord? BlacklistRecord { get; set; }
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<PromissoryNote> PromissoryNotes { get; set; } = new List<PromissoryNote>();
    public ICollection<TrafficViolation> TrafficViolations { get; set; } = new List<TrafficViolation>();
}
