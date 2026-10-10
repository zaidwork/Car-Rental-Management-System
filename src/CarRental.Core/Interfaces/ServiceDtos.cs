using CarRental.Core.Entities;
using CarRental.Core.Enums;

namespace CarRental.Core.Interfaces;

public class DashboardSummaryDto
{
    public int TotalVehicles { get; set; }
    public int AvailableVehicles { get; set; }
    public int RentedVehicles { get; set; }
    public int InMaintenanceVehicles { get; set; }
    public int ActiveContractsCount { get; set; }
    public int BlacklistedCount { get; set; }
    public decimal TodayRevenue { get; set; }
    public List<Vehicle> FeaturedCars { get; set; } = new();
    public List<CriticalAlertDto> CriticalAlerts { get; set; } = new();
    public List<Contract> TimelineContracts { get; set; } = new();
    public List<Vehicle> AllVehicles { get; set; } = new();
}

public class CriticalAlertDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Severity { get; set; } = "danger"; // danger (red), warning (orange/yellow)
    public string Icon { get; set; } = "alert";
    public Guid? VehicleId { get; set; }
}

public class CustomerCheckResultDto
{
    public bool Exists { get; set; }
    public Customer? Customer { get; set; }
    public bool IsBlacklisted { get; set; }
    public string? BlacklistReason { get; set; }
    public bool IsLicenseExpired { get; set; }
    public bool IsUnderage { get; set; }
    public decimal OutstandingDebts { get; set; }
    public bool CanRent => Exists && !IsBlacklisted && !IsLicenseExpired && !IsUnderage;
    public string Message { get; set; } = string.Empty;
}

public class VehicleProfitabilityDto
{
    public Guid VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string BrandAndModel { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit => TotalRevenue - TotalExpenses;
    public int TotalRentalDays { get; set; }
}

public class CustomerStatementDto
{
    public Customer Customer { get; set; } = null!;
    public List<Contract> Contracts { get; set; } = new();
    public List<FinancialTransaction> Transactions { get; set; } = new();
    public List<TrafficViolation> TrafficViolations { get; set; } = new();
    public decimal TotalAgreed { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRemainingDebts { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}

public class OverdueContractDto
{
    public Contract Contract { get; set; } = null!;
    public int OverdueHours { get; set; }
    public int OverdueDays { get; set; }
    public decimal EstimatedLateFee { get; set; }

    public string CustomerName => Contract?.Customer?.FullName ?? string.Empty;
    public string CustomerPhone => Contract?.Customer?.PrimaryPhone ?? string.Empty;
    public string VehicleInfo => $"{Contract?.Vehicle?.Brand} {Contract?.Vehicle?.Model}".Trim();
    public string PlateNumber => Contract?.Vehicle?.PlateNumber ?? string.Empty;
    public string ContractNumber => Contract?.ContractNumber ?? string.Empty;
    public DateTime ExpectedReturnDateTime => Contract?.ExpectedReturnDateTime ?? DateTime.MinValue;
}

