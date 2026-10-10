namespace CarRental.Core.Entities;

public class DailyShift
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string StaffUsername { get; set; } = string.Empty;
    public string StaffFullName { get; set; } = string.Empty;
    
    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public DateTime? ClosedAt { get; set; }
    
    public decimal OpeningBalance { get; set; } = 0;
    public decimal? ClosingBalanceActual { get; set; }  // المبلغ الفعلي المسلم من الموظف
    public decimal SystemCalculatedCash { get; set; } = 0; // المبلغ المحسوب آلياً بالنظام
    public decimal Discrepancy => (ClosingBalanceActual ?? 0) - (OpeningBalance + SystemCalculatedCash);
    
    public bool IsClosed { get; set; } = false;
    public string? Notes { get; set; }
    
    public ICollection<FinancialTransaction> Transactions { get; set; } = new List<FinancialTransaction>();
}
