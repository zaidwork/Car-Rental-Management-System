namespace CarRental.Core.Entities;

public class VehicleDamageMark
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    // زاوية العرض: all (المخطط الشامل), front (أمامي), side (جانبي), rear (خلفي), persp_front (مجسم أمامي), persp_rear (مجسم خلفي)
    public string ViewType { get; set; } = "all";
    
    // إحداثيات نسبية مئوية على الصورة (0% إلى 100%) تضمن تناسق وتطابق العرض على كافة الشاشات
    public double XPercent { get; set; }
    public double YPercent { get; set; }
    
    // نوع الضرر: خدش، ضربة / صدمة، كسر / تغيير، حكة دهان، أخرى
    public string DamageType { get; set; } = "خدش";
    
    // وصف وتفاصيل الضرر (مثلاً: خدش بطول 10 سم على الباب الأمامي يمين)
    public string Description { get; set; } = string.Empty;
    
    // شكل العلامة البصرية: circle (دائرة)، pin (دبوس)، cross (إشارة X)
    public string Shape { get; set; } = "circle";
    
    // لون التمييز البصري
    public string Color { get; set; } = "#f59e0b";
    
    // قطر أو حجم التحديد المئوي إذا كان دائرة
    public double RadiusPercent { get; set; } = 3.5;
    
    // بيانات التسجيل
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string CreatedBy { get; set; } = string.Empty;
    
    // هل تم إصلاحه / إزالته
    public bool IsRepaired { get; set; } = false;
    public DateTime? RepairedAt { get; set; }
    public string? RepairedBy { get; set; }
    public string? RepairNotes { get; set; }
    
    // مرحلة الرصد (Initial: مبدئي في الأسطول، HandoverOut: عند خروج وتسليم السيارة للعميل، ReturnIn: عند استلام السيارة من العميل)
    public string InspectionPhase { get; set; } = "Initial";
    
    // رقم العقد إن كان الضرر مسجلاً أثناء عقد معين
    public Guid? ContractId { get; set; }
    public string? ContractNumber { get; set; }
}
