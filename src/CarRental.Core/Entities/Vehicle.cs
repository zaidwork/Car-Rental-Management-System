using CarRental.Core.Enums;

namespace CarRental.Core.Entities;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // رقم اللوحة
    public string PlateNumber { get; set; } = string.Empty;
    
    // الشركة والطراز وسنة الصنع
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int ModelYear { get; set; }
    public string Color { get; set; } = string.Empty;
    
    // رقم الشاسيه (VIN)
    public string VinNumber { get; set; } = string.Empty;
    
    // قراءة العداد الحالية (كم)
    public int CurrentOdometer { get; set; }
    
    // حالة المركبة (متاحة، مؤجرة، بالصيانة، خارج الخدمة)
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;
    
    // سعر استرشادي يومي
    public decimal DailyStandardRate { get; set; } = 150;
    
    // تواريخ التراخيص والتأمين
    public DateTime LicenseExpiryDate { get; set; }
    public DateTime InsuranceExpiryDate { get; set; }
    public string InsuranceCompany { get; set; } = string.Empty;
    
    // مواعيد وتنبيهات الصيانة الدورية والحرجة
    public int NextOilChangeOdometer { get; set; }
    public DateTime NextMaintenanceDate { get; set; }
    
    // الصيانة الحرجة الإجبارية (إيقاف فوري للسيارة ومنع تأجيرها عند بلوغ العداد أو الموعد)
    public bool IsMaintenanceCritical { get; set; } = false;
    public string? CriticalMaintenanceReason { get; set; }
    
    // صورة السيارة
    public string ImageUrl { get; set; } = string.Empty;
    
    // كشف حالة المركبة وسجل الخدوش والضربات والعلامات البصرية
    public string? DamageMarksJson { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // الخصائص المساعدة لكشف أضرار وخدوش المركبة
    public List<VehicleDamageMark> GetDamageMarks()
    {
        if (string.IsNullOrWhiteSpace(DamageMarksJson)) return new List<VehicleDamageMark>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<VehicleDamageMark>>(DamageMarksJson) ?? new List<VehicleDamageMark>();
        }
        catch
        {
            return new List<VehicleDamageMark>();
        }
    }

    public void SetDamageMarks(List<VehicleDamageMark> marks)
    {
        DamageMarksJson = System.Text.Json.JsonSerializer.Serialize(marks);
    }

    public int ActiveDamagesCount => GetDamageMarks().Count(m => !m.IsRepaired);


    // الخصائص الذكية للتنبيهات وحساب الحالة الملونة
    public int DaysUntilLicenseExpiry => (int)(LicenseExpiryDate.Date - DateTime.Today).TotalDays;
    public int DaysUntilInsuranceExpiry => (int)(InsuranceExpiryDate.Date - DateTime.Today).TotalDays;
    public int KmUntilOilChange => NextOilChangeOdometer - CurrentOdometer;
    public int DaysUntilMaintenance => (int)(NextMaintenanceDate.Date - DateTime.Today).TotalDays;

    // فحص تجاوز موعد الصيانة (بالتاريخ والمدة المحددة من الإدارة)
    public bool IsMaintenanceOverdue => 
        NextMaintenanceDate != default && NextMaintenanceDate.Year > 2000 && DateTime.Today >= NextMaintenanceDate.Date;

    // هل المركبة متجاوزة لموعد الصيانة الحرجة الإلزامية
    public bool IsBlockedByCriticalMaintenance => IsMaintenanceCritical && IsMaintenanceOverdue;

    // تصنيف مستوى التنبيه البصري:
    // Red: ترخيص أو تأمين منتهي، أو تجاوز موعد الصيانة المجدول، أو صيانة حرجة
    // Orange: ينتهي الترخيص/التأمين خلال أقل من 20 يوماً أو تقترب الصيانة خلال 14 يوماً
    // Green: حالة ممتازة ومتاحة
    public string AlertLevel
    {
        get
        {
            if (Status == VehicleStatus.InMaintenance || IsBlockedByCriticalMaintenance || DaysUntilLicenseExpiry <= 0 || DaysUntilInsuranceExpiry <= 0 || (NextMaintenanceDate != default && NextMaintenanceDate.Year > 2000 && DaysUntilMaintenance <= 0))
                return "danger"; // Red
            if (DaysUntilLicenseExpiry <= 20 || DaysUntilInsuranceExpiry <= 20 || (NextMaintenanceDate != default && NextMaintenanceDate.Year > 2000 && DaysUntilMaintenance <= 14))
                return "warning"; // Orange
            return "success"; // Green
        }
    }

    public string AlertSummary
    {
        get
        {
            if (Status == VehicleStatus.InMaintenance) 
                return $"⛔ متوقفة بالصيانة ومنع التأجير: {CriticalMaintenanceReason ?? "صيانة إدارية"}";
            if (IsBlockedByCriticalMaintenance) 
                return $"⛔ إيقاف إلزامي: حلول موعد الصيانة الحرجة ({CriticalMaintenanceReason ?? "صيانة إلزامية"})";
            if (DaysUntilLicenseExpiry <= 0) return "ترخيص المركبة منتهي!";
            if (DaysUntilInsuranceExpiry <= 0) return "بوليصة التأمين منتهية!";
            if (NextMaintenanceDate != default && NextMaintenanceDate.Year > 2000 && DaysUntilMaintenance <= 0) 
                return $"موعد الصيانة مستحق فوراً! (متأخر {Math.Abs(DaysUntilMaintenance)} يوم)";
            if (DaysUntilLicenseExpiry <= 20) return $"الترخيص ينتهي بعد {DaysUntilLicenseExpiry} يوم";
            if (DaysUntilInsuranceExpiry <= 20) return $"التأمين ينتهي بعد {DaysUntilInsuranceExpiry} يوم";
            if (NextMaintenanceDate != default && NextMaintenanceDate.Year > 2000 && DaysUntilMaintenance <= 14) 
                return $"موعد الصيانة بعد {DaysUntilMaintenance} يوم ({NextMaintenanceDate:yyyy/MM/dd})";
            return "الحالة الفنية ممتازة";
        }
    }

    // Navigation
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
    public ICollection<TrafficViolation> TrafficViolations { get; set; } = new List<TrafficViolation>();
}
