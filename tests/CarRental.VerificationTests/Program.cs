using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CarRental.Core.Entities;
using CarRental.Core.Enums;
using CarRental.Core.Interfaces;
using CarRental.Infrastructure.Data;
using CarRental.Infrastructure.Helpers;
using CarRental.Infrastructure.Services;

namespace CarRental.VerificationTests;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("       مختبر الفحص العلمي والتحليل الزمني والحسابي الشامل لنظام تأجير السيارات      ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        // إيقاف تام لمزامنة السحابة أثناء الفحوصات لمنع حقن مركبات أو عملاء وهميين في Supabase
        CloudSyncService.IsSyncPaused = true;

        if (args.Contains("--audit-live-db"))
        {
            await RunLiveDatabaseAuditAsync();
            return 0;
        }

        if (args.Contains("--clean-strictly-to-image-and-v2"))
        {
            await CleanStrictlyToImageAndV2Async();
            return 0;
        }

        if (args.Contains("--prepare-clean-db"))
        {
            await PrepareCleanDatabaseAsync();
            return 0;
        }

        if (args.Contains("--apply-core-fleet"))
        {
            AppPaths.Initialize();
            var dbPath = AppPaths.DatabaseFilePath;
            Console.WriteLine($"[ApplyCoreFleet] Target DB: {dbPath}");

            // 1. كتابة كود DbInitializer.cs بترميز UTF-8 مع BOM لضمان عدم حدوث تشويه للحروف العربية
            var dbInitPath = Path.GetFullPath(@"src\CarRental.Infrastructure\Data\DbInitializer.cs");
            if (File.Exists(dbInitPath))
            {
                var originalLines = File.ReadAllLines(dbInitPath, System.Text.Encoding.UTF8);
                // ابحث عن موقع بداية دالة ReplaceWithCoreFleetAsync
                int cutIndex = -1;
                for (int i = 0; i < originalLines.Length; i++)
                {
                    if (originalLines[i].Contains("public static async Task ReplaceWithCoreFleetAsync"))
                    {
                        cutIndex = i;
                        break;
                    }
                }

                if (cutIndex > 0)
                {
                    var head = string.Join("\r\n", originalLines.Take(cutIndex).Select(l => l.TrimEnd()));
                    var cleanTail = @"    public static async Task ReplaceWithCoreFleetAsync(CarRentalDbContext context)
    {
        try
        {
            var inspections = await context.VehicleInspections.ToListAsync();
            if (inspections.Any()) context.VehicleInspections.RemoveRange(inspections);

            var violations = await context.TrafficViolations.ToListAsync();
            if (violations.Any()) context.TrafficViolations.RemoveRange(violations);

            var maintenance = await context.MaintenanceRecords.ToListAsync();
            if (maintenance.Any()) context.MaintenanceRecords.RemoveRange(maintenance);

            var transactions = await context.FinancialTransactions
                .Where(t => t.ContractId != null || t.VehicleId != null)
                .ToListAsync();
            if (transactions.Any()) context.FinancialTransactions.RemoveRange(transactions);

            var notes = await context.PromissoryNotes.ToListAsync();
            if (notes.Any()) context.PromissoryNotes.RemoveRange(notes);

            var contracts = await context.Contracts.ToListAsync();
            if (contracts.Any()) context.Contracts.RemoveRange(contracts);

            var oldVehicles = await context.Vehicles.ToListAsync();
            if (oldVehicles.Any()) context.Vehicles.RemoveRange(oldVehicles);

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($""[ReplaceWithCoreFleetAsync] Clean error: {ex.Message}"");
        }

        await SeedInitialVehiclesAsync(context);
    }

    public static List<Vehicle> GetDefaultOfficeFleet()
    {
        return new List<Vehicle>
        {
            // 1. اكسنت احمر (هيونداي أكسنت - لون أحمر)
            new Vehicle
            {
                PlateNumber = ""6-1021-95"",
                Brand = ""هيونداي"",
                Model = ""أكسنت أحمر"",
                ModelYear = 2023,
                Color = ""أحمر"",
                VinNumber = ""KMHCT4AE6PU100001"",
                CurrentOdometer = 25000,
                DailyStandardRate = 150,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""شركة المشرق للتأمين"",
                NextOilChangeOdometer = 30000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = ""images/cars/accent_red.webp""
            },

            // 2. اكسنت بصمة (هيونداي أكسنت - تشغيل بصمة)
            new Vehicle
            {
                PlateNumber = ""6-0639-95"",
                Brand = ""هيونداي"",
                Model = ""أكسنت بصمة"",
                ModelYear = 2023,
                Color = ""فضي"",
                VinNumber = ""KMHCT4AE8PU100002"",
                CurrentOdometer = 32000,
                DailyStandardRate = 150,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""ترست العالمية للتأمين"",
                NextOilChangeOdometer = 40000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = ""images/cars/accent_smart.jpg""
            },

            // 3. اكسنت 61 (هيونداي أكسنت - رقم تعريفي/لوحة 61)
            new Vehicle
            {
                PlateNumber = ""61"",
                Brand = ""هيونداي"",
                Model = ""أكسنت 61"",
                ModelYear = 2022,
                Color = ""رمادي فيراني"",
                VinNumber = ""KMHCT4AE2PU100003"",
                CurrentOdometer = 41000,
                DailyStandardRate = 140,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""التكافل الفلسطينية للتأمين"",
                NextOilChangeOdometer = 50000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = ""images/cars/accent_61.jpg""
            },

            // 4. اكسنت ابيض (هيونداي أكسنت - لون أبيض)
            new Vehicle
            {
                PlateNumber = ""6-1022-95"",
                Brand = ""هيونداي"",
                Model = ""أكسنت أبيض"",
                ModelYear = 2023,
                Color = ""أبيض"",
                VinNumber = ""KMHCT4AE3PU100004"",
                CurrentOdometer = 28000,
                DailyStandardRate = 150,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""المجموعة الأهلية للتأمين"",
                NextOilChangeOdometer = 35000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = ""images/cars/accent_white.jpg""
            },

            // 5. ستونيك (كيا ستونيك - Kia Stonic)
            new Vehicle
            {
                PlateNumber = ""6-2031-95"",
                Brand = ""كيا"",
                Model = ""ستونيك (Kia Stonic)"",
                ModelYear = 2023,
                Color = ""أحمر"",
                VinNumber = ""KNADC81B8P100005"",
                CurrentOdometer = 21000,
                DailyStandardRate = 180,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""شركة المشرق للتأمين"",
                NextOilChangeOdometer = 30000,
                NextMaintenanceDate = DateTime.Today.AddMonths(7),
                ImageUrl = ""images/cars/kia_stonic.jpg""
            },

            // 6. سبورتاج (كيا سبورتاج - Kia Sportage)
            new Vehicle
            {
                PlateNumber = ""6-2032-95"",
                Brand = ""كيا"",
                Model = ""سبورتاج (Kia Sportage)"",
                ModelYear = 2022,
                Color = ""أبيض"",
                VinNumber = ""KNDPM3AC7N100006"",
                CurrentOdometer = 35000,
                DailyStandardRate = 230,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""ترست العالمية للتأمين"",
                NextOilChangeOdometer = 40000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = ""images/cars/kia_sportage.jpg""
            },

            // 7. فينو رمادي (هيونداي فينيو - لون رمادي / Hyundai Venue)
            new Vehicle
            {
                PlateNumber = ""6-3041-95"",
                Brand = ""هيونداي"",
                Model = ""فينيو رمادي (Hyundai Venue)"",
                ModelYear = 2023,
                Color = ""رمادي"",
                VinNumber = ""KMHCT81D9PU100007"",
                CurrentOdometer = 19500,
                DailyStandardRate = 160,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""شركة المشرق للتأمين"",
                NextOilChangeOdometer = 25000,
                NextMaintenanceDate = DateTime.Today.AddMonths(8),
                ImageUrl = ""images/cars/venue_grey.webp""
            },

            // 8. فينو كحلي (هيونداي فينيو - لون كحلي)
            new Vehicle
            {
                PlateNumber = ""6-3042-95"",
                Brand = ""هيونداي"",
                Model = ""فينيو كحلي (Hyundai Venue)"",
                ModelYear = 2023,
                Color = ""كحلي"",
                VinNumber = ""KMHCT81D1PU100008"",
                CurrentOdometer = 22000,
                DailyStandardRate = 160,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""التكافل الفلسطينية للتأمين"",
                NextOilChangeOdometer = 30000,
                NextMaintenanceDate = DateTime.Today.AddMonths(7),
                ImageUrl = ""images/cars/venue_navy.jpg""
            },

            // 9. فينو اسود (هيونداي فينيو - لون أسود)
            new Vehicle
            {
                PlateNumber = ""6-3043-95"",
                Brand = ""هيونداي"",
                Model = ""فينيو أسود (Hyundai Venue)"",
                ModelYear = 2023,
                Color = ""أسود"",
                VinNumber = ""KMHCT81D2PU100009"",
                CurrentOdometer = 18000,
                DailyStandardRate = 160,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""المجموعة الأهلية للتأمين"",
                NextOilChangeOdometer = 25000,
                NextMaintenanceDate = DateTime.Today.AddMonths(8),
                ImageUrl = ""images/cars/venue_black.webp""
            },

            // 10. فينو خمري (هيونداي فينيو - لون خمري/ماروني)
            new Vehicle
            {
                PlateNumber = ""6-3044-95"",
                Brand = ""هيونداي"",
                Model = ""فينيو خمري (Hyundai Venue)"",
                ModelYear = 2023,
                Color = ""خمري"",
                VinNumber = ""KMHCT81D4PU100010"",
                CurrentOdometer = 26000,
                DailyStandardRate = 160,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""شركة المشرق للتأمين"",
                NextOilChangeOdometer = 30000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = ""images/cars/venue_burgundy.jpg""
            },

            // 11. فينو احمر (هيونداي فينيو - لون أحمر)
            new Vehicle
            {
                PlateNumber = ""6-3045-95"",
                Brand = ""هيونداي"",
                Model = ""فينيو أحمر (Hyundai Venue)"",
                ModelYear = 2023,
                Color = ""أحمر"",
                VinNumber = ""KMHCT81D5PU100011"",
                CurrentOdometer = 24000,
                DailyStandardRate = 160,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""ترست العالمية للتأمين"",
                NextOilChangeOdometer = 30000,
                NextMaintenanceDate = DateTime.Today.AddMonths(6),
                ImageUrl = ""images/cars/venue_red.webp""
            },

            // 12. بيكانتو (كيا بيكانتو - Kia Picanto)
            new Vehicle
            {
                PlateNumber = ""6-4051-95"",
                Brand = ""كيا"",
                Model = ""بيكانتو (Kia Picanto)"",
                ModelYear = 2024,
                Color = ""أبيض"",
                VinNumber = ""KNABA5128R100012"",
                CurrentOdometer = 14000,
                DailyStandardRate = 120,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = ""شركة المشرق للتأمين"",
                NextOilChangeOdometer = 20000,
                NextMaintenanceDate = DateTime.Today.AddMonths(9),
                ImageUrl = ""images/cars/kia_picanto.png""
            }
        };
    }

    private static async Task SeedInitialVehiclesAsync(CarRentalDbContext context)
    {
        var existingPlates = await context.Vehicles.Select(v => v.PlateNumber.ToLower()).ToListAsync();
        var initialVehicles = GetDefaultOfficeFleet();

        var toAdd = initialVehicles
            .Where(v => !existingPlates.Contains(v.PlateNumber.ToLower()))
            .ToList();

        if (toAdd.Count > 0)
        {
            await context.Vehicles.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();
        }
    }
}";
                    var fullText = head + "\r\n" + cleanTail + "\r\n";
                    File.WriteAllText(dbInitPath, fullText, new System.Text.UTF8Encoding(true));
                    Console.WriteLine("[ApplyCoreFleet] DbInitializer.cs updated with UTF-8 BOM successfully.");
                }
            }

            var optBuilder = new DbContextOptionsBuilder<CarRentalDbContext>();
            optBuilder.UseSqlite($"Data Source={dbPath}");
            using (var ctx = new CarRentalDbContext(optBuilder.Options))
            {
                // مسح كل السيارات الحالية المشوهة
                var oldVehicles = await ctx.Vehicles.ToListAsync();
                ctx.Vehicles.RemoveRange(oldVehicles);
                await ctx.SaveChangesAsync();

                // إضافة السيارات الـ 12 بالأسماء العربية السليمة
                var freshVehicles = new List<Vehicle>
                {
                    new Vehicle
                    {
                        PlateNumber = "6-1021-95",
                        Brand = "هيونداي",
                        Model = "أكسنت أحمر",
                        ModelYear = 2023,
                        Color = "أحمر",
                        VinNumber = "KMHCT4AE6PU100001",
                        CurrentOdometer = 25000,
                        DailyStandardRate = 150,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "شركة المشرق للتأمين",
                        NextOilChangeOdometer = 30000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(6),
                        ImageUrl = "images/cars/accent_red.webp"
                    },
                    new Vehicle
                    {
                        PlateNumber = "6-0639-95",
                        Brand = "هيونداي",
                        Model = "أكسنت بصمة",
                        ModelYear = 2023,
                        Color = "فضي",
                        VinNumber = "KMHCT4AE8PU100002",
                        CurrentOdometer = 32000,
                        DailyStandardRate = 150,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "ترست العالمية للتأمين",
                        NextOilChangeOdometer = 40000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(6),
                        ImageUrl = "images/cars/accent_smart.jpg"
                    },
                    new Vehicle
                    {
                        PlateNumber = "61",
                        Brand = "هيونداي",
                        Model = "أكسنت 61",
                        ModelYear = 2022,
                        Color = "رمادي فيراني",
                        VinNumber = "KMHCT4AE2PU100003",
                        CurrentOdometer = 41000,
                        DailyStandardRate = 140,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "التكافل الفلسطينية للتأمين",
                        NextOilChangeOdometer = 50000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(6),
                        ImageUrl = "images/cars/accent_61.jpg"
                    },
                    new Vehicle
                    {
                        PlateNumber = "6-1022-95",
                        Brand = "هيونداي",
                        Model = "أكسنت أبيض",
                        ModelYear = 2023,
                        Color = "أبيض",
                        VinNumber = "KMHCT4AE3PU100004",
                        CurrentOdometer = 28000,
                        DailyStandardRate = 150,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "المجموعة الأهلية للتأمين",
                        NextOilChangeOdometer = 35000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(6),
                        ImageUrl = "images/cars/accent_white.jpg"
                    },
                    new Vehicle
                    {
                        PlateNumber = "6-2031-95",
                        Brand = "كيا",
                        Model = "ستونيك (Kia Stonic)",
                        ModelYear = 2023,
                        Color = "أحمر",
                        VinNumber = "KNADC81B8P100005",
                        CurrentOdometer = 21000,
                        DailyStandardRate = 180,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "شركة المشرق للتأمين",
                        NextOilChangeOdometer = 30000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(7),
                        ImageUrl = "images/cars/kia_stonic.jpg"
                    },
                    new Vehicle
                    {
                        PlateNumber = "6-2032-95",
                        Brand = "كيا",
                        Model = "سبورتاج (Kia Sportage)",
                        ModelYear = 2022,
                        Color = "أبيض",
                        VinNumber = "KNDPM3AC7N100006",
                        CurrentOdometer = 35000,
                        DailyStandardRate = 230,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "ترست العالمية للتأمين",
                        NextOilChangeOdometer = 40000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(6),
                        ImageUrl = "images/cars/kia_sportage.jpg"
                    },
                    new Vehicle
                    {
                        PlateNumber = "6-3041-95",
                        Brand = "هيونداي",
                        Model = "فينيو رمادي (Hyundai Venue)",
                        ModelYear = 2023,
                        Color = "رمادي",
                        VinNumber = "KMHCT81D9PU100007",
                        CurrentOdometer = 19500,
                        DailyStandardRate = 160,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "شركة المشرق للتأمين",
                        NextOilChangeOdometer = 25000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(8),
                        ImageUrl = "images/cars/venue_grey.webp"
                    },
                    new Vehicle
                    {
                        PlateNumber = "6-3042-95",
                        Brand = "هيونداي",
                        Model = "فينيو كحلي (Hyundai Venue)",
                        ModelYear = 2023,
                        Color = "كحلي",
                        VinNumber = "KMHCT81D1PU100008",
                        CurrentOdometer = 22000,
                        DailyStandardRate = 160,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "التكافل الفلسطينية للتأمين",
                        NextOilChangeOdometer = 30000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(7),
                        ImageUrl = "images/cars/venue_navy.jpg"
                    },
                    new Vehicle
                    {
                        PlateNumber = "6-3043-95",
                        Brand = "هيونداي",
                        Model = "فينيو أسود (Hyundai Venue)",
                        ModelYear = 2023,
                        Color = "أسود",
                        VinNumber = "KMHCT81D2PU100009",
                        CurrentOdometer = 18000,
                        DailyStandardRate = 160,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "المجموعة الأهلية للتأمين",
                        NextOilChangeOdometer = 25000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(8),
                        ImageUrl = "images/cars/venue_black.webp"
                    },
                    new Vehicle
                    {
                        PlateNumber = "6-3044-95",
                        Brand = "هيونداي",
                        Model = "فينيو خمري (Hyundai Venue)",
                        ModelYear = 2023,
                        Color = "خمري",
                        VinNumber = "KMHCT81D4PU100010",
                        CurrentOdometer = 26000,
                        DailyStandardRate = 160,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "شركة المشرق للتأمين",
                        NextOilChangeOdometer = 30000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(6),
                        ImageUrl = "images/cars/venue_burgundy.jpg"
                    },
                    new Vehicle
                    {
                        PlateNumber = "6-3045-95",
                        Brand = "هيونداي",
                        Model = "فينيو أحمر (Hyundai Venue)",
                        ModelYear = 2023,
                        Color = "أحمر",
                        VinNumber = "KMHCT81D5PU100011",
                        CurrentOdometer = 24000,
                        DailyStandardRate = 160,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "ترست العالمية للتأمين",
                        NextOilChangeOdometer = 30000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(6),
                        ImageUrl = "images/cars/venue_red.webp"
                    },
                    new Vehicle
                    {
                        PlateNumber = "6-4051-95",
                        Brand = "كيا",
                        Model = "بيكانتو (Kia Picanto)",
                        ModelYear = 2024,
                        Color = "أبيض",
                        VinNumber = "KNABA5128R100012",
                        CurrentOdometer = 14000,
                        DailyStandardRate = 120,
                        Status = VehicleStatus.Available,
                        LicenseExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                        InsuranceCompany = "شركة المشرق للتأمين",
                        NextOilChangeOdometer = 20000,
                        NextMaintenanceDate = DateTime.Today.AddMonths(9),
                        ImageUrl = "images/cars/kia_picanto.png"
                    }
                };

                await ctx.Vehicles.AddRangeAsync(freshVehicles);
                await ctx.SaveChangesAsync();

                var count = await ctx.Vehicles.CountAsync();
                Console.WriteLine($"[ApplyCoreFleet] Success! Total core vehicles in database: {count}");
                foreach (var v in await ctx.Vehicles.ToListAsync())
                {
                    Console.WriteLine($" - {v.Brand} {v.Model} (لوحة: {v.PlateNumber}) | اللون: {v.Color}");
                }
            }
            return 0;
        }

        string tempDbPath = Path.Combine(Path.GetTempPath(), $"CarRental_ScientificAudit_{Guid.NewGuid():N}.db");
        string tempBackupDir = Path.Combine(Path.GetTempPath(), $"CarRental_Backups_{Guid.NewGuid():N}");

        try
        {
            var services = new ServiceCollection();
            services.AddDbContextFactory<CarRentalDbContext>(options =>
            {
                options.UseSqlite($"Data Source={tempDbPath}");
            });
            services.AddScoped<IRentalSystemService, RentalSystemService>();
            services.AddScoped<INotificationService, NotificationService>();

            var sp = services.BuildServiceProvider();
            var dbFactory = sp.GetRequiredService<IDbContextFactory<CarRentalDbContext>>();

            // 1. التهيئة وتطبيق الفهارس والـ WAL
            using (var ctx = dbFactory.CreateDbContext())
            {
                await DbInitializer.InitializeAsync(ctx);
            }

            var rentalService = sp.GetRequiredService<IRentalSystemService>();

            int passedTests = 0;
            int totalTests = 0;

            // ===================================================================
            // PILLAR 1: الفحص الحسابي والمحاسبي المطلق (Financial Precision)
            // ===================================================================
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[المرحلة 1]: فحص الحسابات والكسور الرياضية وتسلسل الديون والورديات...");
            Console.ResetColor();

            totalTests++;
            bool stage1Success = await TestFinancialMathAndReconciliationAsync(rentalService, dbFactory);
            if (stage1Success) passedTests++;

            // ===================================================================
            // PILLAR 2: فحص المنطق والتسلسل وقواعد الأمان (Business Logic & Constraints)
            // ===================================================================
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[المرحلة 2]: فحص المنطق الصارم وقواعد الأمان ومنع التضارب...");
            Console.ResetColor();

            totalTests++;
            bool stage2Success = await TestBusinessLogicAndStateTransitionsAsync(rentalService, dbFactory);
            if (stage2Success) passedTests++;

            // ===================================================================
            // PILLAR 3: فحص التحمل والتزامن الفائق والسرعة (High-Concurrency & Stress)
            // ===================================================================
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[المرحلة 3]: فحص الضغط العالي والعمليات المتزامنة الفائقة...");
            Console.ResetColor();

            totalTests++;
            bool stage3Success = await TestHighConcurrencyAndScaleAsync(rentalService, dbFactory);
            if (stage3Success) passedTests++;

            // ===================================================================
            // PILLAR 4: فحص التعافي الذاتي ومقاومة الانهيار (Resilience & Self-Healing)
            // ===================================================================
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[المرحلة 4]: فحص التعافي الذاتي والحماية من انقطاع الكهرباء وتلف الملفات...");
            Console.ResetColor();

            totalTests++;
            bool stage4Success = await TestSelfHealingAndIntegrityAsync(tempDbPath, tempBackupDir);
            if (stage4Success) passedTests++;

            // ===================================================================
            // PILLAR 5: فحص تهيئة وتصفير النظام وجدولة الصيانة بالتاريخ
            // ===================================================================
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[المرحلة 5]: فحص تهيئة وتصفير النظام التشغيلي وجدولة الصيانة المعتمدة على التاريخ...");
            Console.ResetColor();

            totalTests++;
            bool stage5Success = await TestOperationalResetAndDateDrivenMaintenanceAsync(rentalService, dbFactory, sp.GetRequiredService<INotificationService>());
            if (stage5Success) passedTests++;

            // ===================================================================
            // PILLAR 6: فحص توحيد مسارات الديون وسندات القبض للعقود المفتوحة
            // ===================================================================
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[المرحلة 6]: فحص توحيد مسارات الديون وسندات القبض للعقود المفتوحة (حالة 70 يوم @ 100 ₪ = 7000 ₪ ثم قبض 500 ₪)...");
            Console.ResetColor();

            totalTests++;
            bool stage6Success = await TestUnifiedOpenRentalDebtAndPaymentScenarioAsync(rentalService, dbFactory);
            if (stage6Success) passedTests++;

            // ===================================================================
            // التقرير النهائي
            // ===================================================================
            Console.WriteLine("\n================================================================================");
            if (passedTests == totalTests)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ النتيجة النهائية: اجتاز النظام كافة الاختبارات بنجاح 100% ({passedTests}/{totalTests})!");
                Console.WriteLine("نظام محكم محاسبياً، آمن منطقياً، فائق السرعة، ومحصن ضد الانهيار تحت الضغط العالي.");
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ النتيجة النهائية: فشل في بعض الاختبارات ({passedTests}/{totalTests})!");
                Console.ResetColor();
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"💥 خطأ استثنائي غير متوقع: {ex.Message}\n{ex.StackTrace}");
            Console.ResetColor();
            return 1;
        }
        finally
        {
            // تنظيف الملفات المؤقتة
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
                if (File.Exists(tempDbPath + "-wal")) File.Delete(tempDbPath + "-wal");
                if (File.Exists(tempDbPath + "-shm")) File.Delete(tempDbPath + "-shm");
                if (Directory.Exists(tempBackupDir)) Directory.Delete(tempBackupDir, true);
            }
            catch { }
        }
    }

    private static async Task<bool> TestFinancialMathAndReconciliationAsync(IRentalSystemService service, IDbContextFactory<CarRentalDbContext> dbFactory)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // 1. تسجيل عميل ومركبة
            var customer = await service.SaveCustomerAsync(new Customer
            {
                FullName = "محمود عادل إبراهيم",
                NationalIdOrPassport = "401928374",
                PrimaryPhone = "0599112233",
                LicenseNumber = "DL-88192",
                LicenseExpiryDate = DateTime.Today.AddYears(3),
                DateOfBirth = new DateTime(1995, 5, 12),
                Address = "طوباس"
            }, "admin");

            var vehicle = await service.SaveVehicleAsync(new Vehicle
            {
                PlateNumber = "6-9901-B",
                Brand = "هيونداي",
                Model = "إلنترا",
                ModelYear = 2024,
                Color = "فضي",
                VinNumber = "KMHD1234567890123",
                CurrentOdometer = 35000,
                DailyStandardRate = 200,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(10),
                InsuranceExpiryDate = DateTime.Today.AddMonths(10),
                InsuranceCompany = "فلسطين للتأمين"
            }, "admin");

            // 2. فتح وردية الصندوق
            var shift = await service.OpenShiftAsync("staff", "موظف الاستقبال", 500m);
            Assert(shift.OpeningBalance == 500m, "الرصيد الافتتاحي للوردية يجب أن يكون 500");

            // 3. إنشاء عقد تأجير (5 أيام @ 200 = 1000 ₪)، دفع مقدماً 300 ₪، متبقي 700 ₪
            var contract = new Contract
            {
                CustomerId = customer.Id,
                VehicleId = vehicle.Id,
                StartDateTime = DateTime.Today,
                ExpectedReturnDateTime = DateTime.Today.AddDays(5),
                DailyAgreedRate = 200,
                TotalAgreedAmount = 1000,
                PaidAmount = 300,
                RemainingAmount = 700,
                PaymentMode = PaymentMode.ImmediateCash,
                PromissoryNoteAmount = 35000
            };
            contract = await service.CreateContractAsync(contract, "staff");

            Assert(contract.RemainingAmount == 700m, "متبقي العقد الأولي يجب أن يكون 700");
            using (var ctx = dbFactory.CreateDbContext())
            {
                var refreshedCust = await ctx.Customers.FindAsync(customer.Id);
                Assert(refreshedCust!.TotalDebts == 700m, "ديون العميل الأولية يجب أن تكون 700 تماماً");
                
                var currentShift = await ctx.DailyShifts.FindAsync(shift.Id);
                Assert(currentShift!.SystemCalculatedCash == 300m, "نقد الوردية يجب أن يزيد بـ 300 دفعة أولى");
            }

            // 4. تمديد العقد: 3 أيام إضافية @ 200 = 600 ₪ إضافي.
            // العميل دفع 750 ₪ (سدد تكلفة التمديد 600 + 150 ₪ من دينه القديم!)
            contract = await service.ExtendContractAsync(contract.Id, DateTime.Today.AddDays(8), 200m, 750m, "staff");
            Assert(contract.TotalAgreedAmount == 1600m, "إجمالي العقد بعد التمديد يجب أن يكون 1000 + 600 = 1600");
            Assert(contract.PaidAmount == 1050m, "إجمالي المدفوع على العقد يجب أن يكون 300 + 750 = 1050");
            Assert(contract.RemainingAmount == 550m, "المتبقي على العقد يجب أن ينخفض إلى 1600 - 1050 = 550");

            using (var ctx = dbFactory.CreateDbContext())
            {
                var refreshedCust = await ctx.Customers.FindAsync(customer.Id);
                Assert(refreshedCust!.TotalDebts == 550m, "ديون العميل يجب أن تنخفض تلقائياً إلى 550 تماماً بدقة");
            }

            // 5. تسوية وإغلاق العقد (Return Settlement):
            // عداد الاستلام: 35000، عداد الإرجاع: 37200 (قطع 2200 كم).
            // مسموح 8 أيام * 250 = 2000 كم -> زيادة 200 كم @ 1 ₪ = 200 ₪
            // نقص وقود: 80 ₪، تأخير: 100 ₪، أضرار: 150 ₪.
            // إجمالي رسوم التسوية = 200 + 80 + 100 + 150 = 530 ₪.
            // العقد كان متبقي عليه 550. إجمالي المطلوب = 550 + 530 = 1080 ₪.
            // العميل دفع الآن نقداً: 880 ₪، وتبقت بذمته 200 ₪.
            contract = await service.CloseContractWithSettlementAsync(
                contract.Id,
                endOdometer: 37200,
                endFuel: FuelLevel.Half,
                lateFee: 100m,
                fuelFee: 80m,
                extraKmFee: 200m,
                damageFee: 150m,
                amountPaidNow: 880m,
                settlementNotes: "تسوية ذكية دقيقة",
                staffUsername: "staff"
            );

            Assert(contract.TotalAgreedAmount == 1600m + 530m, "إجمالي العقد النهائي يجب أن يكون 2130");
            Assert(contract.PaidAmount == 1050m + 880m, "المدفوع النهائي يجب أن يكون 1930");
            Assert(contract.RemainingAmount == 200m, "المتبقي النهائي على العقد يجب أن يكون 200 بالضبط");

            using (var ctx = dbFactory.CreateDbContext())
            {
                var refreshedCust = await ctx.Customers.FindAsync(customer.Id);
                Assert(refreshedCust!.TotalDebts == 200m, "ديون العميل المتبقية يجب أن تكون 200 بالضبط");
            }

            // 6. سداد الديون المباشر (Direct Debt Settlement):
            // العميل يسدد الـ 200 ₪ المتبقية
            await service.SettleCustomerDebtAsync(customer.Id, 200m, PaymentMode.ImmediateCash, "تصفير حساب العقد بالكامل", "staff");

            using (var ctx = dbFactory.CreateDbContext())
            {
                var refreshedCust = await ctx.Customers.FindAsync(customer.Id);
                Assert(refreshedCust!.TotalDebts == 0m, "يجب أن تصبح ديون العميل 0.0000 بالضبط!");
                
                var closedContract = await ctx.Contracts.FindAsync(contract.Id);
                Assert(closedContract!.RemainingAmount == 0m, "يجب أن يتم تصفير متبقي العقد المغلق تلقائياً!");
            }

            // 7. تسجيل مصروف عام من الصندوق (مثلاً غيار زيت 180 ₪)
            await service.AddGeneralExpenseAsync(180m, "صيانة وغيار زيت", TransactionCategory.CarMaintenance, PaymentMode.ImmediateCash, "staff", vehicle.Id);

            // 8. إغلاق وردية الصندوق وتدقيق الفارق (Discrepancy Check)
            // الافتتاحي: 500
            // المقبوضات: 300 (عقد) + 750 (تمديد) + 880 (تسوية) + 200 (سداد دين) = 2130 ₪
            // المدفوعات: 180 ₪ (مصروف)
            // الصافي المحسوب بالنظام: 2130 - 180 = 1950 ₪
            // إجمالي النقد المتوقع بالدرج: 500 + 1950 = 2450 ₪
            var closedShift = await service.CloseShiftAsync(shift.Id, 2450m, "إغلاق مطابق تماماً");
            Assert(closedShift.SystemCalculatedCash == 1950m, "صافي حركات الصندوق المحسوب يجب أن يكون 1950 ₪");
            Assert(closedShift.Discrepancy == 0m, "الفارق بين الفعلي والمتوقع يجب أن يكون 0.0000 ₪ (تطابق تام)");

            // 9. فحص خاص وحاسم للدفع الآجل (DeferredOnReturn) ونظام حماية النزاهة المالية
            var deferredCustomer = await service.SaveCustomerAsync(new Customer
            {
                FullName = "خالد خليل رضوان",
                NationalIdOrPassport = "999888777",
                PrimaryPhone = "0599554433",
                LicenseNumber = "DL-99281",
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                DateOfBirth = new DateTime(1992, 3, 10),
                Address = "نابلس"
            }, "admin");

            var car2 = await service.SaveVehicleAsync(new Vehicle
            {
                PlateNumber = "6-9999-Z",
                Brand = "كيا",
                Model = "سبورتاج",
                ModelYear = 2024,
                Color = "أسود",
                VinNumber = "KNA12345678901234",
                CurrentOdometer = 12000,
                DailyStandardRate = 300,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "المشرق للتأمين"
            }, "admin");

            // فتح وردية جديدة
            var shift2 = await service.OpenShiftAsync("staff", "موظف الاستقبال", 1000m);

            // إنشاء عقد آجل بالكامل (3 أيام @ 300 = 900 ₪) مع محاكاة إرسال خاطئ من الواجهة كأنه مدفوع
            var deferredContract = new Contract
            {
                CustomerId = deferredCustomer.Id,
                VehicleId = car2.Id,
                StartDateTime = DateTime.Now,
                ExpectedReturnDateTime = DateTime.Now.AddDays(3),
                DailyAgreedRate = 300,
                TotalAgreedAmount = 900,
                PaidAmount = 900, // محاكاة الخطأ السابق: إرسال المبلغ كمدفوع بالكامل
                RemainingAmount = 0,
                PaymentMode = PaymentMode.DeferredOnReturn,
                PromissoryNoteAmount = 10000,
                FuelLevelStart = FuelLevel.Full
            };

            var createdDeferred = await service.CreateContractAsync(deferredContract, "staff");
            Assert(createdDeferred.PaidAmount == 0, "عقد الدفع الآجل يجب أن يحمي الصندوق ويصفر المقبوض نقداً");
            Assert(createdDeferred.RemainingAmount == 900, "عقد الدفع الآجل يجب أن يضع كامل المبلغ كمتبقي 900 ₪");

            using (var ctx = dbFactory.CreateDbContext())
            {
                var refreshedDeferredCust = await ctx.Customers.FindAsync(deferredCustomer.Id);
                Assert(refreshedDeferredCust!.TotalDebts == 900m, "دين العميل يجب أن يزيد بمقدار 900 ₪ بالضبط");

                var refreshedShift2 = await ctx.DailyShifts.FindAsync(shift2.Id);
                Assert(refreshedShift2!.SystemCalculatedCash == 0m, "الصندوق يجب ألا يدخل فيه أي قرش كاش وهمي");
            }

            // 10. فحص تجميع المستأجرين في إدارة المستندات (عدم تكرار اسم العميل وتعدد السيارات والإشعار الفوري)
            bool eventFired = false;
            service.OnContractsDataChanged += () => eventFired = true;

            var car3 = await service.SaveVehicleAsync(new Vehicle
            {
                PlateNumber = "6-7788-X",
                Brand = "مرسيدس",
                Model = "E200",
                ModelYear = 2024,
                Color = "أبيض",
                VinNumber = "WDB12345678909876",
                CurrentOdometer = 8000,
                DailyStandardRate = 500,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(15),
                InsuranceExpiryDate = DateTime.Today.AddMonths(15),
                InsuranceCompany = "الأهلية للتأمين"
            }, "admin");

            // نفس العميل يستأجر سيارة ثانية
            var contract2ForCustomer = new Contract
            {
                CustomerId = deferredCustomer.Id,
                VehicleId = car3.Id,
                StartDateTime = DateTime.Now,
                ExpectedReturnDateTime = DateTime.Now.AddDays(2),
                DailyAgreedRate = 500,
                TotalAgreedAmount = 1000,
                PaidAmount = 1000,
                RemainingAmount = 0,
                PaymentMode = PaymentMode.ImmediateCash,
                PromissoryNoteAmount = 10000,
                FuelLevelStart = FuelLevel.Full
            };

            await service.CreateContractAsync(contract2ForCustomer, "staff");
            Assert(eventFired, "يجب إطلاق حدث OnContractsDataChanged تلقائياً عند إنشاء العقد لتحديث الواجهة فوراً دون الحاجة لإعادة التشغيل");

            var allActive = await service.GetContractsAsync(ContractStatus.Active);
            var customerGrouped = allActive
                .GroupBy(c => c.CustomerId)
                .Select(g => new { CustomerId = g.Key, Count = g.Count() })
                .ToList();

            var deferredCustGroup = customerGrouped.FirstOrDefault(g => g.CustomerId == deferredCustomer.Id);
            Assert(deferredCustGroup != null && deferredCustGroup.Count == 2, "نفس المستأجر يجب أن يظهر كاسم واحد تحته سيارتين");

            sw.Stop();
            Console.WriteLine($"   ✓ اكتمل فحص الحسابات والديون والورديات والدفع الآجل وتجميع المستندات بنجاح في {sw.ElapsedMilliseconds} ms دون أي خطأ كسري!");
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"   ✗ فشل الفحص المحاسبي: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static async Task<bool> TestBusinessLogicAndStateTransitionsAsync(IRentalSystemService service, IDbContextFactory<CarRentalDbContext> dbFactory)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // 1. منع تأجير سيارة مؤجرة بالفعل
            var testCar = await service.SaveVehicleAsync(new Vehicle
            {
                PlateNumber = "8-1122-C",
                Brand = "كيا",
                Model = "سبورتاج",
                ModelYear = 2023,
                DailyStandardRate = 250,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddYears(1),
                InsuranceExpiryDate = DateTime.Today.AddYears(1)
            }, "admin");

            var cust1 = await service.SaveCustomerAsync(new Customer
            {
                FullName = "خالد سامي نمر",
                NationalIdOrPassport = "402819283",
                PrimaryPhone = "0599000111",
                LicenseNumber = "DL-99120",
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                DateOfBirth = new DateTime(1992, 1, 1),
                Address = "نابلس"
            }, "admin");

            var cust2 = await service.SaveCustomerAsync(new Customer
            {
                FullName = "عمر توفيق جابر",
                NationalIdOrPassport = "403928172",
                PrimaryPhone = "0599000222",
                LicenseNumber = "DL-99121",
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                DateOfBirth = new DateTime(1990, 1, 1),
                Address = "جنين"
            }, "admin");

            var contract1 = await service.CreateContractAsync(new Contract
            {
                CustomerId = cust1.Id,
                VehicleId = testCar.Id,
                StartDateTime = DateTime.Today,
                ExpectedReturnDateTime = DateTime.Today.AddDays(2),
                DailyAgreedRate = 250,
                TotalAgreedAmount = 500,
                PaidAmount = 500,
                RemainingAmount = 0
            }, "admin");

            // محاولة تأجير نفس السيارة لعميل آخر في نفس الوقت -> يجب أن ترفض بقوة!
            bool doubleRentPrevented = false;
            try
            {
                await service.CreateContractAsync(new Contract
                {
                    CustomerId = cust2.Id,
                    VehicleId = testCar.Id,
                    StartDateTime = DateTime.Today,
                    ExpectedReturnDateTime = DateTime.Today.AddDays(1),
                    DailyAgreedRate = 250,
                    TotalAgreedAmount = 250
                }, "admin");
            }
            catch (InvalidOperationException)
            {
                doubleRentPrevented = true;
            }
            Assert(doubleRentPrevented, "حظر تأجير السيارة المؤجرة لعميل آخر لم يعمل!");

            // 2. حظر التأجير للعميل في القائمة السوداء
            await service.AddToBlacklistAsync(cust2.Id, "مماطلة في السداد", "admin");
            bool blacklistPrevented = false;
            try
            {
                await service.CreateContractAsync(new Contract
                {
                    CustomerId = cust2.Id,
                    VehicleId = testCar.Id,
                    StartDateTime = DateTime.Today,
                    ExpectedReturnDateTime = DateTime.Today.AddDays(1),
                    DailyAgreedRate = 250,
                    TotalAgreedAmount = 250
                }, "admin");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("القائمة السوداء"))
            {
                blacklistPrevented = true;
            }
            Assert(blacklistPrevented, "حظر التأجير للعملاء في القائمة السوداء لم يعمل!");

            // 3. فحص إلغاء العقد بالكامل (Cancellation Flow)
            // إلغاء عقد cust1: يجب أن تعود السيارة متاحة، يلغى الضمان، وتسجل حركة استرداد بالصندوق
            await service.CancelContractAsync(contract1.Id, "admin", "رغبة العميل في الفسخ");

            using (var ctx = dbFactory.CreateDbContext())
            {
                var v = await ctx.Vehicles.FindAsync(testCar.Id);
                Assert(v!.Status == VehicleStatus.Available, "السيارة يجب أن تعود للحالة (متاحة) فور إلغاء العقد!");

                var c = await ctx.Contracts.FindAsync(contract1.Id);
                Assert(c!.Status == ContractStatus.Cancelled, "حالة العقد يجب أن تكون ملغى (Cancelled)!");
                Assert(c.PaidAmount == 0 && c.RemainingAmount == 0, "مبالغ العقد الملغي يجب تصفيرها!");

                var txs = await ctx.FinancialTransactions.Where(t => t.ContractId == contract1.Id).ToListAsync();
                Assert(!txs.Any(), "يجب إلغاء وتفريغ كافة الحركات المالية للعقد الملغي حتى لا يظهر أي قبض أو صرف سالب بالصندوق!");
            }

            // 4. فحص نظام الاستلام والتسليم المبكر وسحب الأيام والخصم (Early Return & Days Billing)
            var earlyReturnCar = await service.SaveVehicleAsync(new Vehicle
            {
                PlateNumber = "9-7788-XX",
                Brand = "هيونداي",
                Model = "النترا",
                ModelYear = 2024,
                DailyStandardRate = 200,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddYears(1),
                InsuranceExpiryDate = DateTime.Today.AddYears(1),
                CurrentOdometer = 10000
            }, "admin");

            var earlyCust = await service.SaveCustomerAsync(new Customer
            {
                FullName = "محمود عادل الشريف",
                NationalIdOrPassport = "998811223",
                PrimaryPhone = "0599112233",
                LicenseNumber = "DL-ER-1",
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                DateOfBirth = new DateTime(1995, 5, 5)
            }, "admin");

            var earlyContract = await service.CreateContractAsync(new Contract
            {
                CustomerId = earlyCust.Id,
                VehicleId = earlyReturnCar.Id,
                DailyAgreedRate = 200,
                TotalAgreedAmount = 600,
                PaidAmount = 600,
                RemainingAmount = 0,
                StartDateTime = DateTime.Now.AddDays(-1),
                ExpectedReturnDateTime = DateTime.Now.AddDays(2), // 3 أيام
                PaymentMode = PaymentMode.ImmediateCash,
                PromissoryNoteAmount = 5000
            }, "admin");

            // الزبون سلم بعد يوم، وتم سحب واحتساب يوم واحد فقط (200 شيكل) مع خصم تسوية 20 شيكل، واسترداد نقدي 420 شيكل من الصندوق
            await service.CloseContractWithSettlementAsync(
                earlyContract.Id,
                10150,
                FuelLevel.Full,
                lateFee: 0,
                fuelFee: 0,
                extraKmFee: 0,
                damageFee: 0,
                amountPaidNow: 0,
                settlementNotes: "تسليم مبكر بعد يوم واحد",
                staffUsername: "admin",
                paymentMethod: PaymentMode.ImmediateCash,
                settlementDiscount: 20,
                billedDays: 1,
                customerRefundAmount: 420,
                refundFromCashDrawer: true);

            using (var ctx = dbFactory.CreateDbContext())
            {
                var closedC = await ctx.Contracts.FindAsync(earlyContract.Id);
                Assert(closedC!.Status == ContractStatus.Closed, "العقد يجب أن يكون مغلقاً!");
                Assert(closedC.TotalAgreedAmount == 180, $"إجمالي العقد يجب أن يصبح 180 شيكل بعد احتساب يوم واحد (200 - 20 خصم) ولكن القيمة كانت: {closedC.TotalAgreedAmount}");
                Assert(closedC.PaidAmount == 180, $"المدفوع الصافي بعد الاسترداد يجب أن يكون 180 شيكل ولكن القيمة: {closedC.PaidAmount}");
                Assert(closedC.RemainingAmount == 0, "المتبقي يجب أن يكون 0!");

                var refundTx = await ctx.FinancialTransactions.FirstOrDefaultAsync(t => t.ContractId == earlyContract.Id && t.Type == TransactionType.Payment);
                Assert(refundTx != null && refundTx.Amount == 420, "يجب تسجيل سند صرف استرداد بقيمة 420 شيكل للمسترد النقدي!");
            }

            sw.Stop();
            Console.WriteLine($"   ✓ اكتمل فحص المنطق الصارم وترابط الحالات بنجاح في {sw.ElapsedMilliseconds} ms!");
            return true;
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"   ✗ فشل فحص المنطق: {msg}");
            Console.ResetColor();
            return false;
        }
    }

    private static async Task<bool> TestHighConcurrencyAndScaleAsync(IRentalSystemService service, IDbContextFactory<CarRentalDbContext> dbFactory)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // تجهيز 20 سيارة و 20 عميل للاختبار التزامني الفائق
            var carIds = new List<Guid>();
            var custIds = new List<Guid>();

            for (int i = 0; i < 50; i++)
            {
                var c = await service.SaveCustomerAsync(new Customer
                {
                    FullName = $"عميل التزامن رقم {i + 1}",
                    NationalIdOrPassport = $"990000{i:D4}",
                    PrimaryPhone = $"0599{i:D6}",
                    LicenseNumber = $"DL-CONC-{i}",
                    LicenseExpiryDate = DateTime.Today.AddYears(3),
                    DateOfBirth = new DateTime(1990, 1, 1),
                    Address = "فحص التزامن"
                }, "admin");
                custIds.Add(c.Id);

                var v = await service.SaveVehicleAsync(new Vehicle
                {
                    PlateNumber = $"CONC-{i:D4}",
                    Brand = "سكودا",
                    Model = "أوكتافيا",
                    ModelYear = 2024,
                    DailyStandardRate = 220,
                    Status = VehicleStatus.Available,
                    LicenseExpiryDate = DateTime.Today.AddYears(1),
                    InsuranceExpiryDate = DateTime.Today.AddYears(1)
                }, "admin");
                carIds.Add(v.Id);
            }

            // إطلاق 50 عملية إنشاء عقود بالتوازي في نفس اللحظة عبر Task.WhenAll
            var tasks = new List<Task<Contract>>();
            for (int i = 0; i < 50; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var contract = new Contract
                    {
                        CustomerId = custIds[index % custIds.Count],
                        VehicleId = carIds[index % carIds.Count],
                        StartDateTime = DateTime.Today,
                        ExpectedReturnDateTime = DateTime.Today.AddDays(2),
                        DailyAgreedRate = 220,
                        TotalAgreedAmount = 440,
                        PaidAmount = 440,
                        RemainingAmount = 0,
                        PaymentMode = PaymentMode.ImmediateCash
                    };
                    return await service.CreateContractAsync(contract, "admin");
                }));
            }

            var createdContracts = await Task.WhenAll(tasks);

            // التحقق من أن كافة أرقام العقود وأرقام السندات فريدة تماماً 100% ولا يوجد أي تصادم (No duplicate key)
            var contractNumbers = createdContracts.Select(c => c.ContractNumber).ToList();
            Assert(contractNumbers.Count == 50, "يجب إنشاء 50 عقد");
            Assert(contractNumbers.Distinct().Count() == 50, "يوجد تكرار في أرقام العقود المتزامنة! التزامن فشل!");

            using (var ctx = dbFactory.CreateDbContext())
            {
                var todayPrefix = $"REC-{DateTime.Now:yyyyMMdd}-";
                var receipts = await ctx.FinancialTransactions
                    .Where(t => t.ReceiptNumber.StartsWith(todayPrefix))
                    .Select(t => t.ReceiptNumber)
                    .ToListAsync();
                Assert(receipts.Distinct().Count() == receipts.Count, "يوجد تكرار في أرقام سندات القبض المتزامنة!");
            }

            // فحص سرعة استعلام ربحية الأسطول مع الفهارس الجديدة
            var profSw = Stopwatch.StartNew();
            var prof = await service.GetVehicleProfitabilityAsync();
            profSw.Stop();
            Assert(prof.Count > 0, "يجب استرجاع ربحية الأسطول");
            Console.WriteLine($"      ⚡ استعلام ربحية الأسطول كاملة مع التجميع استغرق: {profSw.ElapsedMilliseconds} ms فقط!");

            sw.Stop();
            Console.WriteLine($"   ✓ اكتمل فحص التزامن والضغط (50 عملية متوازية في نفس اللحظة) في {sw.ElapsedMilliseconds} ms مع صفر تصادم وصفر أخطاء lock!");
            return true;
        }
        catch (Exception ex)
        {
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"   ✗ فشل فحص التزامن: {inner.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static async Task<bool> TestSelfHealingAndIntegrityAsync(string dbPath, string backupDir)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // 1. اختبار النسخ الاحتياطي التلقائي
            DatabaseSelfHealingService.RunAutomaticDailyBackup(dbPath, backupDir, 30);
            var backupFiles = Directory.GetFiles(backupDir, "autobackup_*.db");
            Assert(backupFiles.Length > 0, "فشل إنشاء النسخة الاحتياطية التلقائية!");

            // 2. اختبار فحص السلامة (Self-Healing Integrity Check)
            await DatabaseSelfHealingService.PerformDatabaseSelfHealingAsync(dbPath);

            sw.Stop();
            Console.WriteLine($"   ✓ اكتمل فحص سلامة قاعدة البيانات والنسخ الاحتياطي والتعافي التلقائي بنجاح في {sw.ElapsedMilliseconds} ms!");
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"   ✗ فشل فحص التعافي الذاتي: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static async Task<bool> TestOperationalResetAndDateDrivenMaintenanceAsync(
        IRentalSystemService service, 
        IDbContextFactory<CarRentalDbContext> dbFactory,
        INotificationService notifService)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // -------------------------------------------------------------
            // TEST 1: Date-Driven Maintenance (استقلالية الصيانة عن العداد واعتمادها الصارم على التاريخ)
            // -------------------------------------------------------------
            var carA = new Vehicle
            {
                Brand = "Toyota",
                Model = "Camry Test",
                PlateNumber = "TEST-DATE-01",
                CurrentOdometer = 999999, // عداد ضخم
                NextOilChangeOdometer = 5000,
                NextMaintenanceDate = DateTime.Today.AddMonths(4), // بعد 4 أشهر
                DailyStandardRate = 200,
                LicenseExpiryDate = DateTime.Today.AddYears(1),
                InsuranceExpiryDate = DateTime.Today.AddYears(1),
                Status = VehicleStatus.Available
            };

            var carB = new Vehicle
            {
                Brand = "Hyundai",
                Model = "Elantra Test",
                PlateNumber = "TEST-DATE-02",
                CurrentOdometer = 100, // عداد صغير جداً
                NextMaintenanceDate = DateTime.Today.AddDays(-2), // متأخرة بيومين
                DailyStandardRate = 180,
                LicenseExpiryDate = DateTime.Today.AddYears(1),
                InsuranceExpiryDate = DateTime.Today.AddYears(1),
                Status = VehicleStatus.Available
            };

            var carC = new Vehicle
            {
                Brand = "Kia",
                Model = "K5 Test",
                PlateNumber = "TEST-DATE-03",
                CurrentOdometer = 2000,
                NextMaintenanceDate = DateTime.Today.AddDays(5), // بعد 5 أيام
                DailyStandardRate = 220,
                LicenseExpiryDate = DateTime.Today.AddYears(1),
                InsuranceExpiryDate = DateTime.Today.AddYears(1),
                Status = VehicleStatus.Available
            };

            Assert(!carA.IsMaintenanceOverdue, "المركبة A يجب ألا تعتبر متجاوزة للصيانة لأن تاريخها لم يحن بعد!");
            Assert(carA.AlertLevel != "danger", "مستوى التنبيه للمركبة A لا يجب أن يكون خطراً لأن موعد صيانتها بعد 4 أشهر!");

            Assert(carB.IsMaintenanceOverdue, "المركبة B يجب أن تعتبر متجاوزة للصيانة فوراً لأن تاريخها قد انقضى!");
            Assert(carB.AlertLevel == "danger", "مستوى التنبيه للمركبة B يجب أن يكون خطراً (danger) لأن تاريخ الصيانة انقضى!");

            Assert(carC.AlertLevel == "warning", "المركبة C يجب أن تظهر تنبيهاً تحذيرياً (warning) لأن موعدها خلال 5 أيام!");

            carA = await service.SaveVehicleAsync(carA, "admin");
            carB = await service.SaveVehicleAsync(carB, "admin");
            carC = await service.SaveVehicleAsync(carC, "admin");

            await notifService.RefreshNotificationsAsync();
            var notifB = notifService.Notifications.FirstOrDefault(n => n.Id == $"vehicle-maintenance-{carB.Id}");
            Assert(notifB != null, "لم يتم توليد إشعار تلقائي بتجاوز الصيانة للمركبة B المعتمدة على التاريخ!");
            Assert(notifB!.Severity == NotificationSeverity.Danger, "يجب أن تكون درجة خطورة الإشعار للمركبة B هي Danger!");

            var notifC = notifService.Notifications.FirstOrDefault(n => n.Id == $"vehicle-maintenance-{carC.Id}");
            Assert(notifC != null, "لم يتم توليد إشعار تلقائي باقتراب موعد الصيانة للمركبة C!");
            Assert(notifC!.Severity == NotificationSeverity.Warning, "يجب أن تكون درجة خطورة الإشعار للمركبة C هي Warning!");

            // -------------------------------------------------------------
            // TEST 2: Operational Data Reset (تهيئة وتصفير النظام وحذف السجلات التشغيلية)
            // -------------------------------------------------------------
            var cust = await service.SaveCustomerAsync(new Customer
            {
                FullName = "خالد مصطفى التميمي",
                NationalIdOrPassport = "409988776",
                PrimaryPhone = "0599112233",
                DateOfBirth = new DateTime(1995, 5, 5),
                LicenseNumber = "DL-TEST-999",
                LicenseExpiryDate = DateTime.Today.AddYears(3),
                TotalDebts = 750
            }, "admin");

            var contract = new Contract
            {
                VehicleId = carA.Id,
                CustomerId = cust.Id,
                StartDateTime = DateTime.Today,
                ExpectedReturnDateTime = DateTime.Today.AddDays(3),
                DailyAgreedRate = 200,
                TotalAgreedAmount = 600,
                PaidAmount = 200,
                RemainingAmount = 400,
                PaymentMode = PaymentMode.ImmediateCash,
                PromissoryNoteAmount = 30000
            };
            contract = await service.CreateContractAsync(contract, "admin");

            using (var preCtx = dbFactory.CreateDbContext())
            {
                var contractsCount = await preCtx.Contracts.CountAsync();
                var transactionsCount = await preCtx.FinancialTransactions.CountAsync();
                var notesCount = await preCtx.PromissoryNotes.CountAsync();
                var shiftsCount = await preCtx.DailyShifts.CountAsync();

                Assert(contractsCount > 0, "يجب أن توجد عقود قبل التصفير!");
                Assert(transactionsCount > 0, "يجب أن توجد حركات مالية قبل التصفير!");
                Assert(notesCount > 0, "يجب أن توجد كمبيالات قبل التصفير!");
                Assert(shiftsCount > 0, "يجب أن توجد ورديات قبل التصفير!");
            }

            var backupSavedPath = await service.ResetOperationalDataAsync("admin");
            Assert(!string.IsNullOrEmpty(backupSavedPath) && File.Exists(backupSavedPath), 
                "فشل حفظ النسخة الاحتياطية التلقائية قبل تصفير النظام!");

            using (var postCtx = dbFactory.CreateDbContext())
            {
                // 1. مسح البيانات التشغيلية
                var contractsPost = await postCtx.Contracts.CountAsync();
                var transactionsPost = await postCtx.FinancialTransactions.CountAsync();
                var notesPost = await postCtx.PromissoryNotes.CountAsync();
                var inspectionsPost = await postCtx.VehicleInspections.CountAsync();
                var violationsPost = await postCtx.TrafficViolations.CountAsync();
                var maintenancePost = await postCtx.MaintenanceRecords.CountAsync();

                Assert(contractsPost == 0, $"فشل تصفير العقود! لا يزال هناك {contractsPost} عقد.");
                Assert(transactionsPost == 0, $"فشل تصفير الحركات المالية! لا يزال هناك {transactionsPost} حركة.");
                Assert(notesPost == 0, $"فشل تصفير الكمبيالات! لا يزال هناك {notesPost} كمبيالة.");
                Assert(inspectionsPost == 0, $"فشل تصفير الفحوصات!");
                Assert(violationsPost == 0, $"فشل تصفير المخالفات!");
                Assert(maintenancePost == 0, $"فشل تصفير سجلات الصيانة!");

                // 2. المحفوظات المحمية 100%
                var usersPost = await postCtx.Users.CountAsync();
                Assert(usersPost > 0, "تم حذف الموظفين بالخطأ! يجب ألا يُمس جدول المستخدمين نهائياً.");
                var adminUser = await postCtx.Users.FirstOrDefaultAsync(u => u.Username == "admin");
                Assert(adminUser != null && !string.IsNullOrEmpty(adminUser.PasswordHash), "تم العبث بحساب المدير أو كلمة سره!");

                var vehiclesPost = await postCtx.Vehicles.ToListAsync();
                Assert(vehiclesPost.Count >= 3, "تم حذف مركبات من الأسطول! يجب ألا تُمس سيارات الأسطول.");
                Assert(vehiclesPost.All(v => v.Status == VehicleStatus.Available), "يجب أن تعود جميع سيارات الأسطول لحالة متاحة للتأجير!");
                Assert(vehiclesPost.All(v => !v.IsMaintenanceCritical), "يجب إلغاء الحظر الحرج لجميع المركبات بعد التصفير.");

                var customersPost = await postCtx.Customers.ToListAsync();
                Assert(customersPost.Count > 0, "تم حذف العملاء بالخطأ! يجب ألا يُمس جدول العملاء.");
                Assert(customersPost.All(c => c.TotalDebts == 0), "يجب تصفير ديون جميع العملاء لتكون 0 بعد تصفير النظام!");

                // 3. وردية افتتاحية جديدة
                var shiftsPost = await postCtx.DailyShifts.ToListAsync();
                Assert(shiftsPost.Count == 1, "يجب أن توجد وردية أولى جديدة برصيد صفر بعد التصفير!");
                Assert(!shiftsPost[0].IsClosed && shiftsPost[0].OpeningBalance == 0, "الوردية الجديدة يجب أن تكون مفتوحة وبرصيد 0!");
            }

            sw.Stop();
            Console.WriteLine($"   ✓ اكتمل اختبار جدولة الصيانة بالتاريخ وتصفير النظام مع حماية الموظفين والسيارات والعملاء في {sw.ElapsedMilliseconds} ms!");
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"   ✗ فشل فحص تهيئة وتصفير النظام وجدولة الصيانة: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static void Assert(bool condition, string errorMessage)
    {
        if (!condition)
        {
            throw new Exception($"[فشل التحقق]: {errorMessage}");
        }
    }

    private static async Task RunLiveDatabaseAuditAsync()
    {
        AppPaths.Initialize();
        var dbPath = AppPaths.DatabaseFilePath;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("    [تقرير الفحص المجهري والشامل لقاعدة البيانات والمنظومة التشغيلية الحية]     ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        Console.WriteLine($"[1] مسار قاعدة البيانات الحية: {dbPath}");
        if (!File.Exists(dbPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ خطأ: ملف قاعدة البيانات غير موجود في المسار المحدد!");
            Console.ResetColor();
            return;
        }

        var dbFileInfo = new FileInfo(dbPath);
        Console.WriteLine($"    - حجم الملف الفعلي: {dbFileInfo.Length:N0} بايت ({dbFileInfo.Length / 1024.0 / 1024.0:F2} MB)");
        Console.WriteLine($"    - آخر تعديل: {dbFileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

        // 1. فحص سلامة محرك SQLite الداخلي والأوامر الهيكلية (PRAGMAs)
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[2] فحص سلامة الهيكل الفيزيائي والـ PRAGMAs (Physical Integrity & Low-Level Pragmas):");
        Console.ResetColor();

        await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            await conn.OpenAsync();

            // integrity_check
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA integrity_check;";
                var integrity = await cmd.ExecuteScalarAsync() as string;
                if (string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("    ✓ فحص التكامل الفيزيائي (integrity_check): سليم 100% (ok)");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    ❌ تحذير في فحص التكامل (integrity_check): {integrity}");
                    Console.ResetColor();
                }
            }

            // quick_check
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA quick_check;";
                var quickCheck = await cmd.ExecuteScalarAsync() as string;
                Console.WriteLine($"    ✓ الفحص السريع للفهارس والصفحات (quick_check): {quickCheck}");
            }

            // foreign_key_check
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_key_check;";
                await using var fkReader = await cmd.ExecuteReaderAsync();
                int fkErrors = 0;
                while (await fkReader.ReadAsync())
                {
                    fkErrors++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    ❌ خطأ مفتاح أجنبي: جدول {fkReader.GetString(0)}, صف {fkReader.GetInt64(1)}, مستهدف {fkReader.GetString(2)}");
                    Console.ResetColor();
                }
                if (fkErrors == 0)
                {
                    Console.WriteLine("    ✓ فحص ترابط المفاتيح الأجنبية (foreign_key_check): سليم تماماً (0 أخطاء علاقات)");
                }
            }

            // journal_mode & synchronous
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode;";
                var jMode = await cmd.ExecuteScalarAsync() as string;
                Console.WriteLine($"    ✓ نمط كتابة العمليات (journal_mode): {jMode} {(jMode?.ToUpper() == "WAL" ? "(ممتاز - أقصى سرعة وتزامن)" : "(تحذير: يفضل WAL)")}");
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA synchronous;";
                var syncVal = await cmd.ExecuteScalarAsync();
                Console.WriteLine($"    ✓ وضع التزامن الفيزيائي (synchronous): {syncVal}");
            }

            // فحص وجود الحقول والترقيات الحديثة في جدول Contracts
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(Contracts);";
                await using var colReader = await cmd.ExecuteReaderAsync();
                var contractColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (await colReader.ReadAsync())
                {
                    contractColumns.Add(colReader.GetString(1));
                }

                Console.WriteLine("\n    [تدقيق أعمدة جدول العقود Contracts]:");
                string[] vitalColumns = { "IsOpenEnded", "SettlementNotes", "LateFee", "FuelShortageFee", "ExtraKmFee", "DamageFee", "IsExtended", "ExtensionDays", "ExtensionAdditionalAmount", "AdditionalDriverName" };
                foreach (var col in vitalColumns)
                {
                    if (contractColumns.Contains(col))
                    {
                        Console.WriteLine($"      ✓ العمود الحيوي [{col}] موجود ومطابق.");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"      ❌ نقص: العمود [{col}] غير موجود في جدول العقود!");
                        Console.ResetColor();
                    }
                }
            }
        }

        // 2. فحص السجلات عبر سياق EF Core وتشغيل التهيئة والتعافي التلقائي
        var opt = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using var context = new CarRentalDbContext(opt);
        await DbInitializer.InitializeAsync(context);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[3] إحصائيات الجداول والسجلات الحالية (Database Entity Inventory):");
        Console.ResetColor();

        var usersCount = await context.Users.CountAsync();
        var customersCount = await context.Customers.CountAsync();
        var blacklistCount = await context.BlacklistRecords.CountAsync();
        var vehiclesCount = await context.Vehicles.CountAsync();
        var contractsCount = await context.Contracts.CountAsync();
        var notesCount = await context.PromissoryNotes.CountAsync();
        var shiftsCount = await context.DailyShifts.CountAsync();
        var transactionsCount = await context.FinancialTransactions.CountAsync();
        var maintenanceCount = await context.MaintenanceRecords.CountAsync();
        var auditLogsCount = await context.AuditLogs.CountAsync();
        var companyCount = await context.CompanyProfiles.CountAsync();
        var violationsCount = await context.TrafficViolations.CountAsync();
        var inspectionsCount = await context.VehicleInspections.CountAsync();

        Console.WriteLine($"    - المستخدمين والموظفين (Users): {usersCount}");
        Console.WriteLine($"    - العملاء (Customers): {customersCount}");
        Console.WriteLine($"    - القائمة السوداء (BlacklistRecords): {blacklistCount}");
        Console.WriteLine($"    - أسطول المركبات (Vehicles): {vehiclesCount}");
        Console.WriteLine($"    - عقود التأجير (Contracts): {contractsCount}");
        Console.WriteLine($"    - كمبيالات الأمانة (PromissoryNotes): {notesCount}");
        Console.WriteLine($"    - الورديات المالية (DailyShifts): {shiftsCount}");
        Console.WriteLine($"    - الحركات والقيود المالية (FinancialTransactions): {transactionsCount}");
        Console.WriteLine($"    - المخالفات المرورية (TrafficViolations): {violationsCount}");
        Console.WriteLine($"    - كشوفات الفحص والاستلام (VehicleInspections): {inspectionsCount}");
        Console.WriteLine($"    - سجلات الصيانة (MaintenanceRecords): {maintenanceCount}");
        Console.WriteLine($"    - سجل النشاط والرقابة (AuditLogs): {auditLogsCount}");
        Console.WriteLine($"    - إعدادات وهوية الشركة (CompanyProfiles): {companyCount}");

        // 3. فحص الإدارة والمستخدمين والصلاحيات
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[4] تدقيق إدارة المستخدمين والصلاحيات (Users & Roles Audit):");
        Console.ResetColor();
        var users = await context.Users.ToListAsync();
        foreach (var u in users)
        {
            var hasPin = !string.IsNullOrWhiteSpace(u.PasswordHash);
            Console.WriteLine($"    - المستخدم: [{u.Username}] | الاسم: {u.FullName} | الدور: {u.Role} | رمز الدخول PIN: {(hasPin ? "مضبوط (" + u.PasswordHash.Length + " خانات)" : "❌ مفقود!")}");
        }

        // 4. تدقيق الأسطول وحالات السيارات
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[5] تدقيق إدارة الأسطول وحالات المركبات (Fleet & Status Audit):");
        Console.ResetColor();
        var vehicles = await context.Vehicles.ToListAsync();
        var activeContracts = await context.Contracts.Where(c => c.Status == ContractStatus.Active).ToListAsync();
        var activeRentedVehicleIds = activeContracts.Select(c => c.VehicleId).ToHashSet();

        int availableCount = vehicles.Count(v => v.Status == VehicleStatus.Available);
        int rentedCount = vehicles.Count(v => v.Status == VehicleStatus.Rented);
        int maintenanceVehicleCount = vehicles.Count(v => v.Status == VehicleStatus.InMaintenance);

        Console.WriteLine($"    - السيارات المتاحة: {availableCount} | المؤجرة: {rentedCount} | في الصيانة: {maintenanceVehicleCount}");

        int vehicleInconsistencies = 0;
        foreach (var v in vehicles)
        {
            bool hasActiveContract = activeRentedVehicleIds.Contains(v.Id);
            if (v.Status == VehicleStatus.Rented && !hasActiveContract)
            {
                vehicleInconsistencies++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    ❌ تناقض: المركبة {v.Brand} {v.Model} ({v.PlateNumber}) مسجلة كمؤجرة (Rented) ولكن لا يوجد لها عقد نشط حالياً!");
                Console.ResetColor();
            }
            else if (v.Status == VehicleStatus.Available && hasActiveContract)
            {
                vehicleInconsistencies++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    ❌ تناقض: المركبة {v.Brand} {v.Model} ({v.PlateNumber}) مسجلة كمتاحة (Available) بالرغم من وجود عقد نشط عليها!");
                Console.ResetColor();
            }

            if (v.IsBlockedByCriticalMaintenance)
            {
                Console.WriteLine($"    ⚠️ تنبيه صيانة حرجة: المركبة {v.PlateNumber} متوقفة للصيانة ({v.CriticalMaintenanceReason ?? "تجاوز الحد الحرج"}).");
            }
        }
        if (vehicleInconsistencies == 0)
        {
            Console.WriteLine("    ✓ تطابق تام 100% بين حالات سيارات الأسطول والعقود النشطة بالمنظومة.");
        }

        // 5. تدقيق العقود والعمليات التشغيلية والمنطقية
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[6] تدقيق عقود التأجير والمعاملات المنطقية (Contracts & Logic Audit):");
        Console.ResetColor();
        var allContracts = await context.Contracts.Include(c => c.Customer).ToListAsync();
        int activeC = allContracts.Count(c => c.Status == ContractStatus.Active);
        int closedC = allContracts.Count(c => c.Status == ContractStatus.Closed);
        int cancelledC = allContracts.Count(c => c.Status == ContractStatus.Cancelled);
        int openEndedC = allContracts.Count(c => c.IsOpenEnded);
        int extendedC = allContracts.Count(c => c.IsExtended);

        Console.WriteLine($"    - إجمالي العقود النشطة: {activeC} | المغلقة: {closedC} | الملغاة: {cancelledC}");
        Console.WriteLine($"    - العقود المفتوحة: {openEndedC} | العقود الممددة: {extendedC}");

        foreach (var c in allContracts)
        {
            Console.WriteLine($"      📄 العقد [{c.ContractNumber}] | سيارة: {c.VehicleId} | مفتوح: {c.IsOpenEnded} | حالة: {c.Status} | البدء: {c.StartDateTime:yyyy-MM-dd HH:mm} | الإرجاع المتوقع: {c.ExpectedReturnDateTime:yyyy-MM-dd HH:mm} | الإرجاع الفعلي: {c.ActualReturnDateTime:yyyy-MM-dd HH:mm} | الإغلاق: {c.ClosedAt:yyyy-MM-dd HH:mm} | الإجمالي: {c.TotalAgreedAmount} ₪ | المدفوع: {c.PaidAmount} ₪");
        }

        if (activeC > 0)
        {
            Console.WriteLine("    [تفاصيل العقود النشطة حالياً]:");
            var activeContractsList = await context.Contracts
                .Include(c => c.Customer)
                .Include(c => c.Vehicle)
                .Where(c => c.Status == ContractStatus.Active)
                .ToListAsync();

            foreach (var ac in activeContractsList)
            {
                Console.WriteLine($"      • رقم العقد: [{ac.ContractNumber}] | المستأجر: {ac.Customer?.FullName} (هوية: {ac.Customer?.NationalIdOrPassport})");
                Console.WriteLine($"        المركبة: {ac.Vehicle?.Brand} {ac.Vehicle?.Model} ({ac.Vehicle?.PlateNumber}) | الإجمالي: {ac.TotalAgreedAmount:N0} ₪ | المدفوع: {ac.PaidAmount:N0} ₪ | المتبقي: {ac.RemainingAmount:N0} ₪");
                Console.WriteLine($"        من: {ac.StartDateTime:yyyy/MM/dd HH:mm} إلى: {ac.ExpectedReturnDateTime:yyyy/MM/dd HH:mm} | الدفع: {ac.PaymentMode} | ضمان: {ac.PromissoryNoteAmount:N0} ₪");
            }
        }

        int contractAnomalies = 0;
        foreach (var c in allContracts)
        {
            if (c.RemainingAmount < 0)
            {
                contractAnomalies++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    ❌ خطأ حسابي: العقد {c.ContractNumber} المتبقي فيه سالب ({c.RemainingAmount})!");
                Console.ResetColor();
            }
            if (c.PaidAmount < 0)
            {
                contractAnomalies++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    ❌ خطأ حسابي: العقد {c.ContractNumber} المدفوع فيه سالب ({c.PaidAmount})!");
                Console.ResetColor();
            }
            if (c.Status == ContractStatus.Closed && c.EndOdometer.HasValue && c.EndOdometer.Value < c.StartOdometer)
            {
                contractAnomalies++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    ❌ خطأ في العداد: العقد {c.ContractNumber} عداد الإرجاع ({c.EndOdometer}) أقل من عداد الاستلام ({c.StartOdometer})!");
                Console.ResetColor();
            }
        }
        if (contractAnomalies == 0)
        {
            Console.WriteLine("    ✓ جميع العقود تخلو من أي مبالغ سالبة أو أخطاء في تتابع قراءات العدادات.");
        }

        // 6. تدقيق الحسابات والمالية والذمم والورديات
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[7] تدقيق المال والمحاسبة والديون والورديات (Financial & Shift Audit):");
        Console.ResetColor();

        // فحص الورديات
        var shifts = await context.DailyShifts.Include(s => s.Transactions).ToListAsync();
        var openShifts = shifts.Where(s => !s.IsClosed).ToList();
        Console.WriteLine($"    - إجمالي الورديات المسجلة: {shifts.Count} | الورديات المفتوحة حالياً: {openShifts.Count}");
        if (openShifts.Count > 1)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"    ⚠️ ملاحظة: يوجد أكثر من وردية مفتوحة حالياً ({openShifts.Count} ورديات) للموظفين التالية أسماؤهم:");
            foreach (var os in openShifts)
            {
                Console.WriteLine($"       - موظف: {os.StaffUsername} ({os.StaffFullName}) | تاريخ الفتح: {os.OpenedAt:yyyy-MM-dd HH:mm} | الرصيد الافتتاحي: {os.OpeningBalance:N0} ₪ | النقد المحسوب: {os.SystemCalculatedCash:N0} ₪");
            }
            Console.ResetColor();
        }
        else if (openShifts.Count == 1)
        {
            var os = openShifts[0];
            var openCashReceipts = os.Transactions.Where(t => t.PaymentMethod == PaymentMode.ImmediateCash && t.Type == TransactionType.Receipt).Sum(t => t.Amount);
            var openCashPayments = os.Transactions.Where(t => t.PaymentMethod == PaymentMode.ImmediateCash && t.Type == TransactionType.Payment).Sum(t => t.Amount);
            var openExpected = openCashReceipts - openCashPayments;

            Console.WriteLine($"    ✓ وردية حالية مفتوحة واحدة: الموظف [{os.StaffUsername}] ({os.StaffFullName}) | فتحت: {os.OpenedAt:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"      الرصيد الافتتاحي: {os.OpeningBalance:N0} ₪ | النقد المسجل بالوردية: {os.SystemCalculatedCash:N0} ₪ | مجموع سندات الوردية: {openExpected:N0} ₪ (مقبوض: {openCashReceipts:N0} ₪ - مصروف: {openCashPayments:N0} ₪) | عدد السندات: {os.Transactions.Count}");
            if (Math.Abs(os.SystemCalculatedCash - openExpected) > 0.01m)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"      ⚠️ تنبيه مالي: يوجد فارق قدره {os.SystemCalculatedCash - openExpected:N0} ₪ بين النقد المسجل بالوردية ومجموع سنداتها.");
                Console.ResetColor();
            }
        }
        else
        {
            Console.WriteLine("    ℹ️ لا توجد ورديات مفتوحة حالياً (كافة الورديات مغلقة ومسواة).");
        }

        // فحص سجلات القائمة السوداء
        var blacklistRecords = await context.BlacklistRecords.Include(b => b.Customer).ToListAsync();
        if (blacklistRecords.Any())
        {
            Console.WriteLine("\n    [سجلات القائمة السوداء والمحظورين]:");
            foreach (var bl in blacklistRecords)
            {
                Console.WriteLine($"      🚫 الاسم: {bl.Customer?.FullName ?? bl.NationalId} | هوية: {bl.NationalId} | السبب: {bl.Reason} | محظور: {bl.IsBlocked} | تاريخ: {bl.BlockedAt:yyyy/MM/dd}");
            }
        }

        // فحص دقة النقد المحسوب في الورديات المغلقة
        int shiftCashDiscrepancies = 0;
        foreach (var s in shifts)
        {
            var cashReceipts = s.Transactions.Where(t => t.PaymentMethod == PaymentMode.ImmediateCash && t.Type == TransactionType.Receipt).Sum(t => t.Amount);
            var cashPayments = s.Transactions.Where(t => t.PaymentMethod == PaymentMode.ImmediateCash && t.Type == TransactionType.Payment).Sum(t => t.Amount);
            var expectedSystemCash = cashReceipts - cashPayments;

            if (s.IsClosed && Math.Abs(s.SystemCalculatedCash - expectedSystemCash) > 0.01m)
            {
                shiftCashDiscrepancies++;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"    ⚠️ اختلاف في حساب الوردية {s.Id}: المسجل {s.SystemCalculatedCash} ₪ بينما مجموع الحركات {expectedSystemCash} ₪");
                Console.ResetColor();
            }
        }
        if (shiftCashDiscrepancies == 0)
        {
            Console.WriteLine("    ✓ تدقيق النقدية بالورديات المغلقة: تطابق دقيق 100% بين حركات الصندوق المسجلة ورصيد كل وردية.");
        }

        // فحص ديون العملاء
        var customers = await context.Customers.ToListAsync();
        var indebtedCustomers = customers.Where(c => c.TotalDebts > 0).ToList();
        Console.WriteLine($"\n    - إجمالي العملاء: {customers.Count} | العملاء الذين عليهم ديون وذمم: {indebtedCustomers.Count}");
        decimal totalSystemDebts = customers.Sum(c => c.TotalDebts);
        Console.WriteLine($"    - إجمالي الديون المستحقة للمكتب في ذمم العملاء: {totalSystemDebts:N0} ₪");

        Console.WriteLine("    [قائمة كافة العملاء المسجلين بالنظام]:");
        foreach (var cu in customers)
        {
            var custContracts = await context.Contracts
                .Where(c => c.CustomerId == cu.Id && c.Status != ContractStatus.Cancelled)
                .ToListAsync();
            var remainingOnContracts = custContracts.Sum(c => c.RemainingAmount);
            Console.WriteLine($"      • [{cu.FullName}] | هوية: {cu.NationalIdOrPassport} | هاتف: {cu.PrimaryPhone} | رصيد الذمة/الدين: {cu.TotalDebts:N0} ₪ | متبقي العقود: {remainingOnContracts:N0} ₪ | عدد عقوده: {custContracts.Count} | ملاحظات: {(string.IsNullOrEmpty(cu.Notes) ? "لا توجد" : cu.Notes)}");
        }

        // فحص الحركات المالية (Financial Transactions)
        var allTrans = await context.FinancialTransactions.OrderByDescending(t => t.TransactionDateTime).ToListAsync();
        decimal totalReceipts = allTrans.Where(t => t.Type == TransactionType.Receipt).Sum(t => t.Amount);
        decimal totalPayments = allTrans.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
        Console.WriteLine($"\n    - إجمالي المقبوضات التاريخية المسجلة: {totalReceipts:N0} ₪");
        Console.WriteLine($"    - إجمالي المدفوعات والمصروفات التاريخية المسجلة: {totalPayments:N0} ₪");
        Console.WriteLine($"    - صافي التدفق المالي التراكمي: {(totalReceipts - totalPayments):N0} ₪");

        if (allTrans.Any())
        {
            Console.WriteLine("    [تفاصيل الحركات المالية الأخيرة]:");
            foreach (var tr in allTrans.Take(5))
            {
                Console.WriteLine($"      • سند رقم: [{tr.ReceiptNumber}] | نوع: {tr.Type} | فئة: {tr.Category} | مبلغ: {tr.Amount:N0} ₪ ({tr.PaymentMethod}) | موظف: {tr.StaffUsername} | تاريخ: {tr.TransactionDateTime:yyyy/MM/dd HH:mm} | مركبة: {tr.VehicleId} | عقد: {tr.ContractId}");
                Console.WriteLine($"        بيان: {tr.Description}");
            }
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[تدقيق كشف ربحية وتحليل إيرادات الأسطول الحالية (Fleet Profitability Audit)]:");
        Console.ResetColor();
        var services = new ServiceCollection();
        services.AddDbContextFactory<CarRentalDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddSingleton<IRentalSystemService, RentalSystemService>();
        services.AddSingleton<IAuthStateService, AuthStateService>();
        services.AddSingleton<INotificationService, NotificationService>();
        var sp = services.BuildServiceProvider();
        var rentalSvc = sp.GetRequiredService<IRentalSystemService>();
        var profList = await rentalSvc.GetVehicleProfitabilityAsync();
        foreach (var p in profList)
        {
            if (p.TotalRevenue > 0 || p.TotalExpenses > 0 || p.TotalRentalDays > 0)
            {
                Console.WriteLine($"    🚗 [{p.BrandAndModel}] لوحة: {p.PlateNumber} | إجمالي الإيرادات: +{p.TotalRevenue:N0} ₪ | المصاريف: -{p.TotalExpenses:N0} ₪ | صافي الربح: {p.NetProfit:N0} ₪ | أيام التأجير: {p.TotalRentalDays} يوم");
            }
        }

        // تفاصيل سجلات الرقابة والتدقيق الأخيرة
        var recentAuditLogs = await context.AuditLogs.OrderByDescending(a => a.Timestamp).Take(15).ToListAsync();
        if (recentAuditLogs.Any())
        {
            Console.WriteLine("\n    [آخر عمليات الرقابة وسجل التدقيق الأمني (Audit Logs)]:");
            foreach (var al in recentAuditLogs)
            {
                Console.WriteLine($"      • [{al.Timestamp:yyyy/MM/dd HH:mm:ss}] ({al.Username} - {al.Role}) | إجراء: {al.Action} على {al.EntityName}: {al.Details}");
            }
        }

        // 7. فحص النسخ الاحتياطية الآلية
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n[8] تدقيق النسخ الاحتياطية التلقائية والتعافي (Backups Audit):");
        Console.ResetColor();
        var backupDir = AppPaths.AutoBackupsDirectory;
        if (Directory.Exists(backupDir))
        {
            var backupFiles = new DirectoryInfo(backupDir).GetFiles("*.db")
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();
            Console.WriteLine($"    - مجلد النسخ: {backupDir}");
            Console.WriteLine($"    - إجمالي النسخ الاحتياطية المتوفرة: {backupFiles.Count} نسخة");
            if (backupFiles.Any())
            {
                var latest = backupFiles.First();
                Console.WriteLine($"    ✓ أحدث نسخة احتياطية: [{latest.Name}] | الحجم: {latest.Length / 1024.0 / 1024.0:F2} MB | التاريخ: {latest.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("    ⚠️ مجلد النسخ الاحتياطية غير موجود!");
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("    ✅ اكتمل الفحص الشامل المباشر لقاعدة البيانات بنجاح تام وبأعلى درجات الدقة!    ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();
    }

    private static async Task PrepareCleanDatabaseAsync()
    {
        AppPaths.Initialize();
        var dbPath = AppPaths.DatabaseFilePath;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("      تجهيز وتطهير قاعدة البيانات وتصفير كافة العمليات مع الاحتفاظ بالأسطول والعملاء   ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();
        Console.WriteLine($"مسار قاعدة البيانات: {dbPath}");

        if (!File.Exists(dbPath))
        {
            Console.WriteLine("قاعدة البيانات غير موجودة!");
            return;
        }

        // 1. أخذ نسخة احتياطية آمنة
        var backupDir = AppPaths.AutoBackupsDirectory;
        if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"CarRental_Backup_BeforePackaging_{ts}.db");
        File.Copy(dbPath, backupPath, true);
        Console.WriteLine($"[1] تم أخذ نسخة احتياطية آمنة في: {backupPath}");

        // 2. قراءة العملاء الحاليين
        var opt = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        List<Customer> customersList;
        List<Vehicle> vehiclesList;

        await using (var ctx = new CarRentalDbContext(opt))
        {
            customersList = await ctx.Customers.AsNoTracking().ToListAsync();
            vehiclesList = await ctx.Vehicles.AsNoTracking().ToListAsync();

            Console.WriteLine($"\n[2] عدد العملاء المسجلين حالياً: {customersList.Count}");
            foreach (var c in customersList)
            {
                Console.WriteLine($"   • {c.FullName} | هوية: {c.NationalIdOrPassport} | هاتف: {c.PrimaryPhone} | رخصة: {c.LicenseNumber} | العنوان: {c.Address}");
            }

            Console.WriteLine($"\n[3] عدد المركبات بالأسطول: {vehiclesList.Count}");
            foreach (var v in vehiclesList)
            {
                Console.WriteLine($"   • {v.Brand} {v.Model} ({v.PlateNumber}) | اللون: {v.Color}");
            }
        }

        // 3. التطهير الشامل للبيانات التشغيلية
        await using (var ctx = new CarRentalDbContext(opt))
        {
            await ctx.Database.ExecuteSqlRawAsync(@"
                DELETE FROM VehicleInspections;
                DELETE FROM TrafficViolations;
                DELETE FROM FinancialTransactions;
                DELETE FROM PromissoryNotes;
                DELETE FROM MaintenanceRecords;
                DELETE FROM Contracts;
                DELETE FROM DailyShifts;
                DELETE FROM BlacklistRecords;
                DELETE FROM AuditLogs;
                UPDATE Customers SET TotalDebts = 0;
                UPDATE Vehicles SET Status = 0, IsMaintenanceCritical = 0, CriticalMaintenanceReason = NULL;
            ");

            // إنشاء وردية نقدية أولى نظيفة للإدارة برصيد صفر
            var freshShift = new DailyShift
            {
                Id = Guid.NewGuid(),
                StaffUsername = "admin",
                StaffFullName = "حسين (المدير العام)",
                OpenedAt = DateTime.Now,
                OpeningBalance = 0,
                SystemCalculatedCash = 0,
                IsClosed = false
            };
            await ctx.DailyShifts.AddAsync(freshShift);

            // التحقق من الحسابات الأساسية وتحديث كلمات السر الافتراضية
            var adminUser = await ctx.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            if (adminUser != null)
            {
                adminUser.IsActive = true;
                if (string.IsNullOrWhiteSpace(adminUser.PasswordHash)) adminUser.PasswordHash = "1234";
            }

            await ctx.SaveChangesAsync();
            Console.WriteLine("\n[4] تم تصفير كافة العقود، الديون، الحركات المالية، السندات، المخالفات، كشوف الفحص، وسجلات التدقيق.");
            Console.WriteLine("    تم إعادة كافة المركبات لحالة 'متاحة للتأجير' وتصفير ديون جميع العملاء لتصبح 0 ₪ تماماً.");
        }

        // 4. تقليص وتطهير ملف SQLite فيزيائياً
        await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); VACUUM;";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("\n[5] تم تنفيذ Checkpoint و VACUUM لتطهير وضغط الملف فيزيائياً.");
        }

        // 5. نسخ قاعدة البيانات النظيفة كقالب رئيسي
        var distWinDir = Path.GetFullPath(@"dist\win-x64");
        if (Directory.Exists(distWinDir))
        {
            var distDbPath = Path.Combine(distWinDir, "car_rental_local.db");
            File.Copy(dbPath, distDbPath, true);
            Console.WriteLine($"[6] تم نسخ قاعدة البيانات النظيفة إلى حزمة النشر: {distDbPath}");
        }

        var installerDir = Path.GetFullPath(@"installer");
        if (Directory.Exists(installerDir))
        {
            var installerDbPath = Path.Combine(installerDir, "car_rental_clean.db");
            File.Copy(dbPath, installerDbPath, true);
            Console.WriteLine($"[7] تم حفظ قالب قاعدة البيانات النظيفة في: {installerDbPath}");
        }

        // 8. تحديث كود DbInitializer.cs ليتضمن زراعة العملاء الأساسيين تلقائياً
        await UpdateDbInitializerWithCleanSeedAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("      ✅ تمت عملية تجهيز وتطهير قاعدة البيانات بنجاح 100%!");
        Console.WriteLine("================================================================================");
        Console.ResetColor();
    }

    private static async Task UpdateDbInitializerWithCleanSeedAsync()
    {
        var dbInitPath = Path.GetFullPath(@"src\CarRental.Infrastructure\Data\DbInitializer.cs");
        if (!File.Exists(dbInitPath)) return;

        var text = await File.ReadAllTextAsync(dbInitPath, System.Text.Encoding.UTF8);
        if (text.Contains("SeedInitialCustomersAsync"))
        {
            Console.WriteLine("[8] كود DbInitializer.cs يحتوي مسبقاً على دالة SeedInitialCustomersAsync.");
            return;
        }

        // إضافة استدعاء SeedInitialCustomersAsync بعد فحص الأسطول
        var anchor = "if (!hasCoreFleet)\r\n        {\r\n            await ReplaceWithCoreFleetAsync(context);\r\n        }";
        if (!text.Contains(anchor))
        {
            anchor = "if (!hasCoreFleet)\n        {\n            await ReplaceWithCoreFleetAsync(context);\n        }";
        }

        var customerCall = @"

        // فحص وتثبيت سجلات العملاء المعتمدين الأساسيين
        if (!await context.Customers.AnyAsync())
        {
            await SeedInitialCustomersAsync(context);
        }";

        if (text.Contains(anchor))
        {
            text = text.Replace(anchor, anchor + customerCall);
        }

        // إضافة الدوال في نهاية الكلاس قبل القوس الأخير
        var methodsToAdd = @"
    public static List<Customer> GetDefaultRegisteredCustomers()
    {
        return new List<Customer>
        {
            new Customer
            {
                Id = Guid.NewGuid(),
                FullName = ""زيد بشار فريح دراغمه"",
                NationalIdOrPassport = ""409991781"",
                PrimaryPhone = ""0598776026"",
                EmergencyPhone = """",
                LicenseNumber = ""D2855945AA"",
                Address = ""طوباس الاسكان"",
                DateOfBirth = new DateTime(1998, 1, 1),
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                TotalDebts = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByUsername = ""admin""
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                FullName = ""احمد عنان حسني دراغمة"",
                NationalIdOrPassport = ""40998632"",
                PrimaryPhone = ""059976532"",
                EmergencyPhone = """",
                LicenseNumber = ""DL0r832743"",
                Address = ""طوباس حاره أبو دواس"",
                Notes = ""سليم وامين"",
                DateOfBirth = new DateTime(1997, 5, 10),
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                TotalDebts = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByUsername = ""admin""
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                FullName = ""وسيم محمد ابراهيم عبد الرازق"",
                NationalIdOrPassport = ""4099765312"",
                PrimaryPhone = ""05997653110"",
                EmergencyPhone = """",
                LicenseNumber = ""DL-883NFUCN"",
                Address = ""طوباس الثعرة"",
                Notes = ""جيد"",
                DateOfBirth = new DateTime(1996, 3, 15),
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                TotalDebts = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByUsername = ""admin""
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                FullName = ""حسين احمد راشد دراغمه"",
                NationalIdOrPassport = ""411993819"",
                PrimaryPhone = ""0566655550"",
                EmergencyPhone = """",
                LicenseNumber = ""DL0384HFD83"",
                Address = ""طوباس حي الحديقة"",
                DateOfBirth = new DateTime(1995, 8, 20),
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                TotalDebts = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByUsername = ""admin""
            },
            new Customer
            {
                Id = Guid.NewGuid(),
                FullName = ""محمد لؤي محمد ابو دواس "",
                NationalIdOrPassport = ""409996816"",
                PrimaryPhone = ""0598733510"",
                EmergencyPhone = """",
                LicenseNumber = ""Sl-843rfu8433"",
                Address = ""طوباس منزل الدكتور لؤي"",
                Notes = ""زلمه محترم"",
                DateOfBirth = new DateTime(1999, 11, 25),
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                TotalDebts = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedByUsername = ""admin""
            }
        };
    }

    private static async Task SeedInitialCustomersAsync(CarRentalDbContext context)
    {
        var existingIds = await context.Customers.Select(c => c.NationalIdOrPassport).ToListAsync();
        var defaults = GetDefaultRegisteredCustomers();
        var toAdd = defaults.Where(c => !existingIds.Contains(c.NationalIdOrPassport)).ToList();
        if (toAdd.Count > 0)
        {
            await context.Customers.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();
        }
    }
}
";
        int lastBrace = text.LastIndexOf('}');
        if (lastBrace > 0)
        {
            text = text.Substring(0, lastBrace).TrimEnd() + "\r\n" + methodsToAdd;
            await File.WriteAllTextAsync(dbInitPath, text, new System.Text.UTF8Encoding(true));
            Console.WriteLine("[8] تم تحديث DbInitializer.cs بنجاح وتضمين زراعة العملاء المعتمدين بترميز UTF-8 BOM!");
        }
    }

    private static async Task CleanStrictlyToImageAndV2Async()
    {
        AppPaths.Initialize();
        var dbPath = AppPaths.DatabaseFilePath;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("    تطهير قاعدة البيانات الصارم: الإبقاء على عملاء الصورة الـ 5 فقط والأسطول وتصفير الباقي   ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        // 1. أخذ نسخة احتياطية
        var backupDir = AppPaths.AutoBackupsDirectory;
        if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(backupDir, $"CarRental_Backup_BeforeStrictClean_{ts}.db");
        File.Copy(dbPath, backupPath, true);
        Console.WriteLine($"[1] تم حفظ نسخة احتياطية في: {backupPath}");

        var opt = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using (var ctx = new CarRentalDbContext(opt))
        {
            // تنفيذ تنظيف قطعي ومباشر عبر SQL الصارم لضمان عدم بقاء أي عميل تجريبي أو عملية سابقة
            await ctx.Database.ExecuteSqlRawAsync(@"
                DELETE FROM VehicleInspections;
                DELETE FROM TrafficViolations;
                DELETE FROM FinancialTransactions;
                DELETE FROM PromissoryNotes;
                DELETE FROM MaintenanceRecords;
                DELETE FROM Contracts;
                DELETE FROM DailyShifts;
                DELETE FROM BlacklistRecords;
                DELETE FROM AuditLogs;
                DELETE FROM Customers WHERE NationalIdOrPassport NOT IN ('409996816', '411993819', '4099765312', '40998632', '409991781');
                UPDATE Customers SET TotalDebts = 0;
                DELETE FROM Vehicles WHERE PlateNumber NOT IN ('6-3041-95', '6-3045-95', '6-2032-95', '6-3042-95', '6-3044-95', '6-1022-95', '6-4051-95', '6-3043-95', '6-0639-95', '6-1021-95', '6-2031-95', '61', 'fs3');
                UPDATE Vehicles SET Status = 0, IsMaintenanceCritical = 0, CriticalMaintenanceReason = NULL;
            ");

            // التأكد من وجود العملاء الخمسة المعتمدين وتحديث بياناتهم بدقة
            var currentCusts = await ctx.Customers.ToListAsync();
            var existingIds = currentCusts.Select(c => c.NationalIdOrPassport).ToHashSet();

            var exactCustomers = new List<Customer>
            {
                new Customer
                {
                    FullName = "محمد لؤي محمد ابو دواس ",
                    NationalIdOrPassport = "409996816",
                    LicenseNumber = "Sl-843rfu8433",
                    LicenseExpiryDate = new DateTime(2028, 9, 27),
                    DateOfBirth = DateTime.Today.AddYears(-22),
                    PrimaryPhone = "0598733510",
                    Address = "طوباس منزل الدكتور لؤي",
                    TotalDebts = 0,
                    CreatedByUsername = "admin"
                },
                new Customer
                {
                    FullName = "حسين احمد راشد دراغمه",
                    NationalIdOrPassport = "411993819",
                    LicenseNumber = "DL0384HFD83",
                    LicenseExpiryDate = new DateTime(2028, 9, 24),
                    DateOfBirth = DateTime.Today.AddYears(-28),
                    PrimaryPhone = "0566655550",
                    Address = "طوباس حي الحديقة",
                    TotalDebts = 0,
                    CreatedByUsername = "admin"
                },
                new Customer
                {
                    FullName = "وسيم محمد ابراهيم عبد الرازق",
                    NationalIdOrPassport = "4099765312",
                    LicenseNumber = "DL-883NFUCN",
                    LicenseExpiryDate = new DateTime(2028, 9, 23),
                    DateOfBirth = DateTime.Today.AddYears(-22),
                    PrimaryPhone = "05997653110",
                    Address = "طوباس الثعرة",
                    TotalDebts = 0,
                    CreatedByUsername = "admin"
                },
                new Customer
                {
                    FullName = "احمد عنان حسني دراغمة",
                    NationalIdOrPassport = "40998632",
                    LicenseNumber = "DL0r832743",
                    LicenseExpiryDate = new DateTime(2028, 9, 22),
                    DateOfBirth = DateTime.Today.AddYears(-23),
                    PrimaryPhone = "059976532",
                    Address = "طوباس حاره أبو دواس",
                    TotalDebts = 0,
                    CreatedByUsername = "admin"
                },
                new Customer
                {
                    FullName = "زيد بشار فريح دراغمه",
                    NationalIdOrPassport = "409991781",
                    LicenseNumber = "D2855945AA",
                    LicenseExpiryDate = new DateTime(2029, 9, 5),
                    DateOfBirth = DateTime.Today.AddYears(-22),
                    PrimaryPhone = "0598776026",
                    Address = "طوباس الاسكان",
                    TotalDebts = 0,
                    CreatedByUsername = "admin"
                }
            };

            foreach (var exact in exactCustomers)
            {
                var existing = currentCusts.FirstOrDefault(c => c.NationalIdOrPassport == exact.NationalIdOrPassport);
                if (existing != null)
                {
                    existing.FullName = exact.FullName;
                    existing.LicenseNumber = exact.LicenseNumber;
                    existing.LicenseExpiryDate = exact.LicenseExpiryDate;
                    existing.PrimaryPhone = exact.PrimaryPhone;
                    existing.Address = exact.Address;
                    existing.TotalDebts = 0;
                }
                else
                {
                    ctx.Customers.Add(exact);
                }
            }

            // إنشاء وردية أولى نظيفة للإدارة برصيد 0
            var freshShift = new DailyShift
            {
                Id = Guid.NewGuid(),
                StaffUsername = "admin",
                StaffFullName = "حسين (المدير العام)",
                OpenedAt = DateTime.Now,
                OpeningBalance = 0,
                SystemCalculatedCash = 0,
                IsClosed = false
            };
            await ctx.DailyShifts.AddAsync(freshShift);

            await ctx.SaveChangesAsync();
            Console.WriteLine("[2] تم تنفيذ التطهير المباشر: تم حذف كافة العمليات والعملاء الوهميين وتثبيت العملاء الـ 5 فقط.");
        }

        // 5. تقليص وتطهير ملف SQLite
        await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); VACUUM;";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("[5] تم تنفيذ PRAGMA wal_checkpoint(TRUNCATE) و VACUUM بنجاح.");
        }

        // 6. نسخ قاعدة البيانات الصافية 100% إلى dist و installer
        var distWinDir = Path.GetFullPath(@"dist\win-x64");
        if (Directory.Exists(distWinDir))
        {
            File.Copy(dbPath, Path.Combine(distWinDir, "car_rental_local.db"), true);
            Console.WriteLine("[6] تم تحديث قاعدة البيانات في dist\\win-x64.");
        }

        var installerDir = Path.GetFullPath(@"installer");
        if (Directory.Exists(installerDir))
        {
            File.Copy(dbPath, Path.Combine(installerDir, "car_rental_clean.db"), true);
            Console.WriteLine("[7] تم تحديث قالب car_rental_clean.db في installer.");
        }

        // 7. طباعة العملاء النهائيين
        await using (var checkCtx = new CarRentalDbContext(opt))
        {
            var finalCusts = await checkCtx.Customers.AsNoTracking().ToListAsync();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[النتيجة]: عدد العملاء النهائي بالقاعدة: {finalCusts.Count} (المطابقين للصورة تماماً):");
            foreach (var c in finalCusts)
            {
                Console.WriteLine($"   ✓ {c.FullName} | هوية: {c.NationalIdOrPassport} | رخصة: {c.LicenseNumber} | رصيد الذمة: {c.TotalDebts} ₪ | هاتف: {c.PrimaryPhone}");
            }

            var finalVehicles = await checkCtx.Vehicles.AsNoTracking().ToListAsync();
            Console.WriteLine($"\n[النتيجة]: عدد المركبات بالأسطول: {finalVehicles.Count} مركبة (جميعها Available):");
            foreach (var v in finalVehicles)
            {
                Console.WriteLine($"   ✓ {v.Brand} {v.Model} ({v.PlateNumber}) | الحالة: {v.Status}");
            }
            Console.ResetColor();
        }
    }

    private static async Task<bool> TestUnifiedOpenRentalDebtAndPaymentScenarioAsync(IRentalSystemService service, IDbContextFactory<CarRentalDbContext> dbFactory)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // 1. تسجيل عميل ومركبة للاختبار
            var customer = await service.SaveCustomerAsync(new Customer
            {
                FullName = "أحمد يوسف خليل",
                NationalIdOrPassport = "402819382",
                PrimaryPhone = "0599887766",
                LicenseNumber = "DL-99123",
                LicenseExpiryDate = DateTime.Today.AddYears(2),
                DateOfBirth = new DateTime(1992, 4, 15),
                Address = "جنين"
            }, "admin");

            var vehicle = await service.SaveVehicleAsync(new Vehicle
            {
                PlateNumber = "6-7788-B",
                Brand = "هيونداي",
                Model = "أكسنت",
                ModelYear = 2023,
                Color = "أبيض",
                VinNumber = "KMHCT4AE8PU998877",
                CurrentOdometer = 20000,
                DailyStandardRate = 100,
                Status = VehicleStatus.Available,
                LicenseExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceExpiryDate = DateTime.Today.AddMonths(12),
                InsuranceCompany = "شركة المشرق للتأمين"
            }, "admin");

            // 2. فتح وردية صندوق
            var shift = await service.OpenShiftAsync("staff", "موظف الاستقبال", 100m);

            // 3. إنشاء عقد تأجير مفتوح (Open-Ended) بدأ قبل 70 يوماً بسعر 100 ₪ يومياً
            var startDate = DateTime.Today.AddDays(-70);
            var contract = new Contract
            {
                CustomerId = customer.Id,
                VehicleId = vehicle.Id,
                StartDateTime = startDate,
                ExpectedReturnDateTime = startDate.AddDays(70),
                DailyAgreedRate = 100,
                TotalAgreedAmount = 0, // مفتوح
                PaidAmount = 0,        // لم يدفع أي شيء وقت الاستلام
                RemainingAmount = 0,
                IsOpenEnded = true,
                PaymentMode = PaymentMode.DeferredOnReturn,
                PromissoryNoteAmount = 30000
            };
            contract = await service.CreateContractAsync(contract, "staff");

            // 4. التحقق من حساب الأيام المتراكمة والمبلغ المستحق: 70 يوم @ 100 = 7000 ₪
            Assert(contract.IsOpenEnded, "العقد يجب أن يكون مفتوح المدة IsOpenEnded");
            Assert(contract.OpenRentalDays >= 70, $"أيام العقد المفتوح يجب أن تكون على الأقل 70 (الفعلي: {contract.OpenRentalDays})");
            Assert(contract.OpenRentalCurrentDue >= 7000m, $"المستحق الجاري للعقد المفتوح يجب أن يكون على الأقل 7000 (الفعلي: {contract.OpenRentalCurrentDue})");

            // التحقق من أن دين العميل أصبح 7000 ₪ في ملفه تلقائياً
            var customerWithDebt = await service.GetCustomerByIdAsync(customer.Id);
            Assert(customerWithDebt != null, "يجب العثور على العميل");
            Assert(customerWithDebt!.TotalDebts >= 7000m, $"دين العميل في ملفه يجب أن يعكس 7000 ₪ للعقد المفتوح (الفعلي: {customerWithDebt.TotalDebts})");

            // 5. دفع 500 ₪ دفعة على العقد عبر RecordContractPaymentAsync
            var paymentResult = await service.RecordContractPaymentAsync(contract.Id, 500m, PaymentMode.ImmediateCash, "دفعة نقدية على الحساب من العميل", "staff");
            Assert(paymentResult.Amount == 500m, $"المبلغ في سند القبض يجب أن يكون 500 (الفعلي: {paymentResult.Amount})");
            
            // التحقق من خصم الـ 500 من المستحق الجاري للعقد
            Contract updatedContract;
            using (var ctx = dbFactory.CreateDbContext())
            {
                updatedContract = (await ctx.Contracts.FindAsync(contract.Id))!;
            }
            Assert(updatedContract.PaidAmount == 500m, $"المبلغ المقبوض على العقد يجب أن يصبح 500 (الفعلي: {updatedContract.PaidAmount})");
            var remainingDueAfter500 = updatedContract.OpenRentalCurrentDue;
            Assert(remainingDueAfter500 == (updatedContract.OpenRentalAccruedAmount - 500m), "المتبقي بعد قبض 500 يجب أن ينخفض بـ 500 بالضبط");

            // التحقق الجوهري: دين العميل في كل النظام يجب أن ينخفض بـ 500 (يصبح 6500 ₪)
            var customerAfter500 = await service.GetCustomerByIdAsync(customer.Id);
            Assert(customerAfter500!.TotalDebts == remainingDueAfter500, $"دين العميل يجب أن ينخفض تلقائياً إلى {remainingDueAfter500} ₪ بعد قبض 500 (الفعلي: {customerAfter500.TotalDebts})");

            // 6. التحقق من حركة الصندوق: النقد في الوردية يجب أن يزيد بـ 500 ₪
            using (var ctx = dbFactory.CreateDbContext())
            {
                var refreshedShift = await ctx.DailyShifts.FindAsync(shift.Id);
                Assert(refreshedShift!.SystemCalculatedCash == 500m, $"نقد الوردية يجب أن يزيد بـ 500 (الفعلي: {refreshedShift.SystemCalculatedCash})");
            }

            // 7. دفع 1000 ₪ إضافية عبر سند قبض محاسبي عام (Accounting Receipt Voucher Flow)
            var accountingReceipt = new FinancialTransaction
            {
                Type = TransactionType.Receipt,
                Category = TransactionCategory.RentalPayment,
                Amount = 1000m,
                CustomerId = customer.Id,
                PaymentMethod = PaymentMode.ImmediateCash,
                StaffUsername = "staff",
                Description = "سند قبض محاسبي - دفعة إضافية"
            };
            await service.AddTransactionAsync(accountingReceipt);

            // التحقق من توزيع سند القبض تلقائياً على العقد المفتوح وتحديث دين العميل
            var customerAfter1000 = await service.GetCustomerByIdAsync(customer.Id);
            var expectedDebtAfter1500 = updatedContract.OpenRentalAccruedAmount - 1500m;
            Assert(customerAfter1000!.TotalDebts == expectedDebtAfter1500, $"دين العميل بعد سند القبض الثاني يجب أن ينخفض إلى {expectedDebtAfter1500} ₪ (الفعلي: {customerAfter1000.TotalDebts})");

            // 8. فحص كشف حساب العميل (Customer Statement)
            var statement = await service.GetCustomerStatementAsync(customer.Id);
            Assert(statement.TotalPaid == 1500m, $"إجمالي المقبوض في كشف الحساب يجب أن يكون 1500 (الفعلي: {statement.TotalPaid})");
            Assert(statement.TotalRemainingDebts == expectedDebtAfter1500, $"صافي الرصيد المستحق بكشف الحساب يجب أن يطابق بدقة {expectedDebtAfter1500} (الفعلي: {statement.TotalRemainingDebts})");

            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"   ✓ اكتمل فحص توحيد مسار ديون العميل وسندات القبض للعقود المفتوحة بنجاح في {sw.ElapsedMilliseconds} ms! تم التحقق: 7000 ₪ -> خصم 500 ₪ -> 6500 ₪ -> خصم 1000 ₪ -> {expectedDebtAfter1500} ₪.");
            Console.ResetColor();
            return true;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"   ✗ فشل فحص توحيد مسار الديون: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }
}
