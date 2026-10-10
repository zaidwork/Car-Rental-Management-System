using Microsoft.EntityFrameworkCore;
using CarRental.Core.Entities;

namespace CarRental.Infrastructure.Data;

public class CarRentalDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<BlacklistRecord> BlacklistRecords => Set<BlacklistRecord>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<PromissoryNote> PromissoryNotes => Set<PromissoryNote>();
    public DbSet<DailyShift> DailyShifts => Set<DailyShift>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<TrafficViolation> TrafficViolations => Set<TrafficViolation>();
    public DbSet<VehicleInspection> VehicleInspections => Set<VehicleInspection>();

    public CarRentalDbContext(DbContextOptions<CarRentalDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User constraints
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Customer constraints
        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.NationalIdOrPassport)
            .IsUnique();

        // Blacklist constraints
        modelBuilder.Entity<BlacklistRecord>()
            .HasIndex(b => b.NationalId);

        modelBuilder.Entity<Customer>()
            .HasOne(c => c.BlacklistRecord)
            .WithOne(b => b.Customer)
            .HasForeignKey<BlacklistRecord>(b => b.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Vehicle constraints
        modelBuilder.Entity<Vehicle>()
            .HasIndex(v => v.PlateNumber)
            .IsUnique();

        // Contract constraints
        modelBuilder.Entity<Contract>()
            .HasIndex(c => c.ContractNumber)
            .IsUnique();

        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Customer)
            .WithMany(c => c.Contracts)
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Vehicle)
            .WithMany(v => v.Contracts)
            .HasForeignKey(c => c.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // PromissoryNote constraints
        modelBuilder.Entity<PromissoryNote>()
            .HasIndex(p => p.NoteNumber)
            .IsUnique();

        modelBuilder.Entity<Contract>()
            .HasOne(c => c.PromissoryNote)
            .WithOne(p => p.Contract)
            .HasForeignKey<PromissoryNote>(p => p.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        // FinancialTransaction
        modelBuilder.Entity<FinancialTransaction>()
            .HasIndex(f => f.ReceiptNumber)
            .IsUnique();

        // TrafficViolation
        modelBuilder.Entity<TrafficViolation>()
            .HasIndex(t => t.ViolationNumber);

        modelBuilder.Entity<TrafficViolation>()
            .HasOne(t => t.Vehicle)
            .WithMany(v => v.TrafficViolations)
            .HasForeignKey(t => t.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrafficViolation>()
            .HasOne(t => t.Customer)
            .WithMany(c => c.TrafficViolations)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TrafficViolation>()
            .HasOne(t => t.Contract)
            .WithMany(c => c.TrafficViolations)
            .HasForeignKey(t => t.ContractId)
            .OnDelete(DeleteBehavior.SetNull);

        // VehicleInspection
        modelBuilder.Entity<VehicleInspection>()
            .HasOne(i => i.Contract)
            .WithMany(c => c.Inspections)
            .HasForeignKey(i => i.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VehicleInspection>()
            .HasOne(i => i.Vehicle)
            .WithMany()
            .HasForeignKey(i => i.VehicleId)
            .OnDelete(DeleteBehavior.SetNull);

        // ================= Performance Indexes for High Scale (100,000+ Operations) =================
        // Contract indexes
        modelBuilder.Entity<Contract>().HasIndex(c => c.Status);
        modelBuilder.Entity<Contract>().HasIndex(c => c.StartDateTime);
        modelBuilder.Entity<Contract>().HasIndex(c => c.ExpectedReturnDateTime);
        modelBuilder.Entity<Contract>().HasIndex(c => c.CreatedAt);

        // FinancialTransaction indexes
        modelBuilder.Entity<FinancialTransaction>().HasIndex(f => f.DailyShiftId);
        modelBuilder.Entity<FinancialTransaction>().HasIndex(f => f.CustomerId);
        modelBuilder.Entity<FinancialTransaction>().HasIndex(f => f.VehicleId);
        modelBuilder.Entity<FinancialTransaction>().HasIndex(f => f.ContractId);
        modelBuilder.Entity<FinancialTransaction>().HasIndex(f => f.Type);
        modelBuilder.Entity<FinancialTransaction>().HasIndex(f => f.TransactionDateTime);

        // Vehicle indexes
        modelBuilder.Entity<Vehicle>().HasIndex(v => v.Status);
        modelBuilder.Entity<Vehicle>().HasIndex(v => v.IsActive);

        // DailyShift indexes
        modelBuilder.Entity<DailyShift>().HasIndex(s => s.StaffUsername);
        modelBuilder.Entity<DailyShift>().HasIndex(s => s.IsClosed);
        modelBuilder.Entity<DailyShift>().HasIndex(s => s.OpenedAt);

        // TrafficViolation indexes
        modelBuilder.Entity<TrafficViolation>().HasIndex(t => t.Status);
        modelBuilder.Entity<TrafficViolation>().HasIndex(t => t.ViolationDateTime);

        // AuditLog indexes
        modelBuilder.Entity<AuditLog>().HasIndex(a => a.Timestamp);
        modelBuilder.Entity<AuditLog>().HasIndex(a => a.Username);
    }
}
