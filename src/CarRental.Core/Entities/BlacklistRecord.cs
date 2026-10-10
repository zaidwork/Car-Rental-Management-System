namespace CarRental.Core.Entities;

public class BlacklistRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string NationalId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsBlocked { get; set; } = true;
    public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    public string BlockedByUsername { get; set; } = string.Empty;
    
    // فك الحظر محصور حصراً بالمدير العام (Admin)
    public DateTime? UnblockedAt { get; set; }
    public string? UnblockedByUsername { get; set; }
    public string? UnblockReason { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
}
