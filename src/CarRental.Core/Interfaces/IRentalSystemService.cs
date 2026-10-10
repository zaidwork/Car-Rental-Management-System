using CarRental.Core.Entities;
using CarRental.Core.Enums;

namespace CarRental.Core.Interfaces;

public interface IRentalSystemService
{
    // Dashboard & Overview
    Task<DashboardSummaryDto> GetDashboardDataAsync();

    // Vehicles / Fleet
    Task<List<Vehicle>> GetVehiclesAsync(string? searchTerm = null, VehicleStatus? status = null);
    Task<Vehicle?> GetVehicleByIdAsync(Guid id);
    Task<Vehicle> SaveVehicleAsync(Vehicle vehicle, string username);
    Task DeleteVehicleAsync(Guid id, string username);

    // Customers & Blacklist
    Task<List<Customer>> GetCustomersAsync(string? searchTerm = null);
    Task<Customer?> GetCustomerByIdAsync(Guid id);
    Task<CustomerCheckResultDto> CheckCustomerEligibilityAsync(string nationalId);
    Task<Customer> SaveCustomerAsync(Customer customer, string username);
    Task DeleteCustomerAsync(Guid customerId, string username);
    Task<List<BlacklistRecord>> GetBlacklistRecordsAsync();
    Task AddToBlacklistAsync(Guid customerId, string reason, string username);
    Task UnblockCustomerAsync(Guid customerId, string unblockReason, string adminUsername);

    // Contracts & Promissory Notes
    Task<List<Contract>> GetContractsAsync(ContractStatus? status = null);
    Task<Contract?> GetContractByIdAsync(Guid id);
    Task<Contract> CreateContractAsync(Contract contract, string staffUsername);
    Task<Contract> CloseContractAsync(Guid contractId, int endOdometer, FuelLevel endFuel, decimal extraPayment, string staffUsername);
    Task<Contract> ExtendContractAsync(Guid contractId, DateTime newExpectedReturn, decimal additionalRate, decimal additionalPaid, string staffUsername);
    Task<Contract> CloseContractWithSettlementAsync(Guid contractId, int endOdometer, FuelLevel endFuel, decimal lateFee, decimal fuelFee, decimal extraKmFee, decimal damageFee, decimal amountPaidNow, string? settlementNotes, string staffUsername, PaymentMode paymentMethod = PaymentMode.ImmediateCash, decimal settlementDiscount = 0, int? billedDays = null, decimal customerRefundAmount = 0, bool refundFromCashDrawer = false);
    Task<Contract> CancelContractAsync(Guid contractId, string staffUsername, string? cancellationReason = null);
    Task<FinancialTransaction> RecordContractPaymentAsync(Guid contractId, decimal amount, PaymentMode mode, string notes, string staffUsername);
    Task<string> GetNextContractNumberCandidateAsync();
    Task RollbackAndDiscardNewContractAsync(Guid contractId, string staffUsername);
    Task<List<OverdueContractDto>> GetOverdueContractsAsync();
    event Action? OnContractsDataChanged;
    void NotifyContractsDataChanged();

    // Vehicle Inspection & Handover
    Task<VehicleInspection> SaveVehicleInspectionAsync(VehicleInspection inspection, string username);
    Task<List<VehicleInspection>> GetVehicleInspectionsAsync(Guid contractId);
    Task<List<VehicleInspection>> GetVehicleInspectionsByVehicleIdAsync(Guid vehicleId);
    Task<Vehicle> UpdateVehicleDamagesAsync(Guid vehicleId, List<VehicleDamageMark> marks, string username, string? auditNote = null);


    // Traffic Violations
    Task<List<TrafficViolation>> GetTrafficViolationsAsync(Guid? vehicleId = null, Guid? customerId = null, ViolationStatus? status = null);
    Task<Customer?> FindResponsibleCustomerForViolationAsync(Guid vehicleId, DateTime violationDateTime);
    Task<List<Contract>> GetRecentVehicleContractsAsync(Guid vehicleId, int days = 14);
    Task<TrafficViolation> AddTrafficViolationAsync(TrafficViolation violation, bool chargeToCustomer, string username);
    Task<TrafficViolation> SettleViolationWithCustomerAsync(Guid violationId, decimal amountPaid, PaymentMode mode, string username);
    Task<TrafficViolation> PayViolationToAuthorityAsync(Guid violationId, string username);

    // Maintenance & Vehicle Profile
    Task<Vehicle?> GetVehicleMaintenanceProfileAsync(Guid vehicleId);
    Task<List<MaintenanceRecord>> GetMaintenanceRecordsAsync(Guid? vehicleId = null);
    Task<MaintenanceRecord> AddMaintenanceRecordAsync(MaintenanceRecord record, string username);
    Task SetCriticalMaintenanceScheduleAsync(Guid vehicleId, int? nextDueOdometer, DateTime? nextDueDate, bool isCritical, string? criticalReason, string username);
    Task ToggleVehicleMaintenanceHaltAsync(Guid vehicleId, bool halt, string? reason, string username);
    Task<MaintenanceRecord> ConfirmMaintenanceCompletedAsync(Guid vehicleId, MaintenanceType type, int odometerAtService, decimal cost, string serviceProvider, string? invoiceNumber, string? notes, int? newNextDueOdometer, DateTime? newNextDueDate, bool newIsCritical, string username);

    // Accounting, Shifts & Debt Settlement
    Task<DailyShift?> GetCurrentShiftAsync(string staffUsername);
    Task<DailyShift> OpenShiftAsync(string staffUsername, string staffFullName, decimal openingBalance);
    Task<DailyShift> CloseShiftAsync(Guid shiftId, decimal actualCash, string? notes);
    Task<List<DailyShift>> GetAllShiftsAsync();
    Task<List<FinancialTransaction>> GetTransactionsAsync(DateTime? from = null, DateTime? to = null);
    Task<FinancialTransaction?> GetTransactionByIdAsync(Guid id);
    Task<FinancialTransaction> AddTransactionAsync(FinancialTransaction transaction);
    Task<FinancialTransaction> SettleCustomerDebtAsync(Guid customerId, decimal amount, PaymentMode mode, string notes, string staffUsername);
    Task<decimal> RecalculateCustomerDebtsAsync(Guid customerId);
    Task<FinancialTransaction> AddGeneralExpenseAsync(decimal amount, string description, TransactionCategory category, PaymentMode mode, string staffUsername, Guid? vehicleId = null, string? payeeOrVendor = null, string? invoiceNumber = null);
    Task<CustomerStatementDto> GetCustomerStatementAsync(Guid customerId);
    Task<List<VehicleProfitabilityDto>> GetVehicleProfitabilityAsync();
    Task<List<FinancialTransaction>> GetVehicleTransactionsAsync(Guid vehicleId);

    // Company Profile & Settings
    Task<CompanyProfile> GetCompanyProfileAsync();
    Task<CompanyProfile> SaveCompanyProfileAsync(CompanyProfile profile, string username);

    // Audit Logs
    Task<List<AuditLog>> GetAuditLogsAsync();
    Task LogAuditAsync(string username, UserRole role, AuditAction action, string entity, string recordId, string details);

    // Backup & Local System
    Task<string> CreateLocalBackupAsync(string targetDirectory);
    Task<bool> RestoreBackupAsync(string sourceBackupFilePath, string username);
    Task<string> ResetOperationalDataAsync(string adminUsername);

    // Users Management & Authentication
    Task<List<User>> GetUsersAsync();
    Task<User?> GetUserByIdAsync(Guid id);
    Task<User?> AuthenticateUserWithPinAsync(string pin, string? username = null);
    Task<User> SaveUserAsync(User user, string adminUsername);
    Task DeleteUserAsync(Guid id, string adminUsername);
}
