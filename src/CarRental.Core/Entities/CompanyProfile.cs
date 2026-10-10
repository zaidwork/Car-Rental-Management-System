namespace CarRental.Core.Entities;

public class CompanyProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CompanyName { get; set; } = "شركة التاج لتأجير السيارات";
    public string CompanyEnglishName { get; set; } = "TAJ CAR RENTAL";
    public string CommercialRegisterNumber { get; set; } = "562514273";
    public string? TaxNumber { get; set; } = "562514273";
    public string PrimaryPhone { get; set; } = "0566655550";
    public string? SecondaryPhone { get; set; } = "0566009003";
    public string Address { get; set; } = "طوباس - قرب الصحة الجديدة";
    public string? Email { get; set; }
    public string? LogoBase64 { get; set; }
    
    // شروط وبنود العقد الافتراضية
    public string DefaultContractTerms { get; set; } = 
        "1. يتعهد المستأجر بإعادة المركبة بنفس حالتها عند الاستلام.\n" +
        "2. يتحمل المستأجر كافة المخالفات المرورية والأضرار الناتجة عن الحوادث خلال فترة استئجاره.\n" +
        "3. يلتزم المستأجر بإعادة المركبة في الوقت المحدد، وأي تأخير يحتسب بتكلفة ساعة/يوم إضافي.\n" +
        "4. كمبيالة الضمان تعتبر التزاماً قانونياً نافذاً فور إخلال المستأجر بأي بند.";
        
    // سياسة الكيلومترات والتأخير الافتراضية
    public int DailyKmLimit { get; set; } = 250;
    public decimal ExtraKmPrice { get; set; } = 1;
    public decimal LateHourFee { get; set; } = 20;
    public string? PromissoryNoteTerms { get; set; } = "أتعهد بسداد قيمة السند دون قيد أو شرط عند أول مطالبة من المستفيد.";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
