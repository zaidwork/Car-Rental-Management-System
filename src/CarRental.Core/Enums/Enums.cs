namespace CarRental.Core.Enums;

public enum UserRole
{
    Admin,
    Staff,
    Accountant
}

public enum VehicleStatus
{
    Available,
    Rented,
    InMaintenance,
    OutOfService
}

public enum ContractStatus
{
    Active,
    Closed,
    Overdue,
    Cancelled
}

public enum PaymentMode
{
    ImmediateCash,
    DeferredOnReturn,
    BankTransfer,
    CreditCard
}

public enum FuelLevel
{
    Empty,
    Quarter,
    Half,
    ThreeQuarters,
    Full
}

public enum PromissoryNoteStatus
{
    HeldInTrust,        // محجوزة كأمانة
    ReleasedToClient,   // تم الإفراج عنها وإعادتها للعميل
    Claimed             // تم التحصيل القانوني
}

public enum MaintenanceType
{
    RoutineOilFilter,
    Tires,
    Brakes,
    Battery,
    MechanicalRepair,
    BodyWork,
    LicenseRenewal,
    InsuranceRenewal
}

public enum TransactionType
{
    Receipt,    // قبض
    Payment     // صرف
}

public enum TransactionCategory
{
    RentalPayment,
    SecurityDeposit,
    CarMaintenance,
    CarWash,
    CarInsurance,
    CarLicensing,
    SpareParts,
    Fuel,
    OfficeExpense,
    Refund,
    DebtPayment,
    ContractExtension,
    TrafficViolation,
    GeneralOfficeExpense,
    DamageCompensation,
    Other
}

public enum AuditAction
{
    Insert,
    Update,
    Delete,
    Unblock,
    Login,
    ShiftClose,
    PrintContract,
    PrintPromissoryNote,
    ContractExtend,
    ContractClose,
    TrafficViolationAdd,
    DebtSettlement,
    BackupRestore
}

public enum ViolationStatus
{
    PendingCollection,      // بانتظار التحصيل من العميل
    CollectedFromCustomer,  // تم تحصيلها من العميل
    PaidToAuthorities,      // تم سدادها للجهة المرورية
    Cancelled               // ملغاة أو معترض عليها
}

public enum InspectionType
{
    HandoverOut, // تسليم المركبة للعميل
    ReturnIn     // استلام المركبة من العميل
}
