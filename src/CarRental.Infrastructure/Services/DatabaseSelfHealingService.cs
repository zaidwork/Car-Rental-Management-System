using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CarRental.Infrastructure.Data;
using CarRental.Infrastructure.Helpers;

namespace CarRental.Infrastructure.Services;

public static class DatabaseSelfHealingService
{
    /// <summary>
    /// فحص سلامة قاعدة البيانات والتعافي الذاتي التلقائي في حال وجود تلف ناتج عن انقطاع مفاجئ للكهرباء
    /// </summary>
    public static async Task PerformDatabaseSelfHealingAsync(string dbPath)
    {
        if (!File.Exists(dbPath)) return;

        try
        {
            // 1. التحقق من مساحة القرص الصلب (300 ميجابايت على الأقل)
            CheckDiskSpace(dbPath);

            // 2. فحص سلامة الملف عبر PRAGMA integrity_check
            bool isHealthy = await VerifyDatabaseIntegrityAsync(dbPath);
            if (!isHealthy)
            {
                await RecoverFromLatestBackupAsync(dbPath);
            }
        }
        catch (Exception ex)
        {
            LogCrash($"[Self-Healing Error] {ex.Message}");
            await RecoverFromLatestBackupAsync(dbPath);
        }
    }

    /// <summary>
    /// تطبيق إعدادات الأداء الفائق (WAL Mode, Memory Cache, Normal Sync)
    /// </summary>
    public static async Task ConfigurePerformancePragmasAsync(CarRentalDbContext context)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA mmap_size = 268435456;"); // 256MB memory mapping
            await context.Database.ExecuteSqlRawAsync("PRAGMA cache_size = -8000;");   // 8MB cache
            await context.Database.ExecuteSqlRawAsync("PRAGMA temp_store = MEMORY;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA auto_vacuum = INCREMENTAL;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout = 10000;"); // 10 seconds timeout for high concurrency
        }
        catch (Exception ex)
        {
            LogCrash($"[Pragmas Config Warning] {ex.Message}");
        }
    }

    /// <summary>
    /// تشغيل النسخ الاحتياطي اليومي التلقائي مع تدوير وحذف النسخ القديمة
    /// </summary>
    public static void RunAutomaticDailyBackup(string dbPath, string backupDir, int retentionDays = 30)
    {
        if (!File.Exists(dbPath)) return;

        try
        {
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            var todayBackupName = $"autobackup_{DateTime.Today:yyyy_MM_dd}.db";
            var targetPath = Path.Combine(backupDir, todayBackupName);

            // لا نكرر النسخ إذا كانت نسخة اليوم موجودة بالفعل وبحجم سليم
            if (!File.Exists(targetPath) || new FileInfo(targetPath).Length == 0)
            {
                using (var source = new SqliteConnection($"Data Source={dbPath}"))
                using (var destination = new SqliteConnection($"Data Source={targetPath}"))
                {
                    source.Open();
                    destination.Open();
                    source.BackupDatabase(destination);
                }
            }

            // تنظيف النسخ القديمة التي تجاوزت فترة الاستبقاء (30 يوماً) لمنع امتلاء القرص
            var oldCutoff = DateTime.Today.AddDays(-retentionDays);
            var dirInfo = new DirectoryInfo(backupDir);
            foreach (var file in dirInfo.GetFiles("autobackup_*.db"))
            {
                if (file.CreationTime < oldCutoff || file.LastWriteTime < oldCutoff)
                {
                    try { file.Delete(); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            LogCrash($"[AutoBackup Warning] {ex.Message}");
        }
    }

    private static async Task<bool> VerifyDatabaseIntegrityAsync(string dbPath)
    {
        try
        {
            await using var conn = new SqliteConnection($"Data Source={dbPath}");
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check(1);";
            var result = await cmd.ExecuteScalarAsync() as string;

            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task RecoverFromLatestBackupAsync(string dbPath)
    {
        try
        {
            // 1. عزل قاعدة البيانات التالفة
            var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var corruptPath = Path.Combine(AppPaths.DatabaseDirectory, $"corrupt_{timeStamp}.db");
            if (File.Exists(dbPath))
            {
                try { File.Move(dbPath, corruptPath, overwrite: true); } catch { }
            }

            // 2. حذف ملفات المجلات العالقة
            try { File.Delete(dbPath + "-wal"); } catch { }
            try { File.Delete(dbPath + "-shm"); } catch { }

            // 3. البحث عن أحدث نسخة سليمة
            var backupDir = new DirectoryInfo(AppPaths.AutoBackupsDirectory);
            if (backupDir.Exists)
            {
                var latestBackup = backupDir.GetFiles("*.db")
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();

                if (latestBackup != null && latestBackup.Length > 0)
                {
                    File.Copy(latestBackup.FullName, dbPath, overwrite: true);
                    LogCrash($"[Self-Healing Recovery] تم استرجاع قاعدة البيانات تلقائياً بنجاح من النسخة: {latestBackup.Name}");
                    return;
                }
            }

            LogCrash("[Self-Healing Warning] لم يتم العثور على نسخة احتياطية سابقة؛ سيتم إنشاء قاعدة بيانات نظيفة جديدة.");
        }
        catch (Exception ex)
        {
            LogCrash($"[Self-Healing Recovery Failed] {ex.Message}");
        }
    }

    private static void CheckDiskSpace(string dbPath)
    {
        try
        {
            var driveLetter = Path.GetPathRoot(Path.GetFullPath(dbPath));
            if (!string.IsNullOrEmpty(driveLetter))
            {
                var drive = new DriveInfo(driveLetter);
                if (drive.IsReady && drive.AvailableFreeSpace < 300 * 1024 * 1024) // 300MB
                {
                    LogCrash($"[Disk Space Warning] مساحة القرص المتبقية منخفضة جداً: {drive.AvailableFreeSpace / (1024 * 1024)} MB");
                }
            }
        }
        catch { }
    }

    private static void LogCrash(string message)
    {
        try
        {
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(AppPaths.CrashLogFilePath, logLine);
        }
        catch { }
    }
}
