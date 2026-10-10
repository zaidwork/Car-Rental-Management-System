using CarRental.Core.Enums;

namespace CarRental.Core.Entities;

public class MaintenanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    
    public MaintenanceType Type { get; set; } = MaintenanceType.RoutineOilFilter;
    public int OdometerAtService { get; set; }
    public decimal Cost { get; set; }
    public DateTime ServiceDate { get; set; } = DateTime.Today;
    
    public int? NextDueOdometer { get; set; }
    public DateTime? NextDueDate { get; set; }
    
    public string ServiceProvider { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public string? Notes { get; set; }
}
