using CarRental.Core.Enums;

namespace CarRental.Core.Entities;

public class VehicleInspection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid ContractId { get; set; }
    public Contract? Contract { get; set; }
    
    public InspectionType Type { get; set; } = InspectionType.HandoverOut;
    public DateTime InspectionDateTime { get; set; } = DateTime.Now;
    public string StaffUsername { get; set; } = string.Empty;
    
    // قائمة فحص الملحقات
    public bool HasSpareTire { get; set; } = true;
    public bool HasJackAndTools { get; set; } = true;
    public bool HasFireExtinguisher { get; set; } = true;
    public bool HasFirstAidKit { get; set; } = true;
    public bool IsCleanInterior { get; set; } = true;
    public bool IsCleanExterior { get; set; } = true;
    
    // الحالة الفنية عند الفحص
    public int Odometer { get; set; }
    public FuelLevel FuelLevel { get; set; } = FuelLevel.Full;
    
    // كشف الخدوش والضربات (نقاط الهيكل: مصد أمامي، باب يمين، سقف، إلخ)
    public string? ScratchesDescription { get; set; }
    public string? DamageNotes { get; set; }
    
    // سجل علامات وخدوش النموذج البصري التفاعلي
    public string? DamageMarksJson { get; set; }
    
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    
    public bool CustomerSignatureAccepted { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

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

}
