using System;
using System.IO;

namespace CarRental.Infrastructure.Helpers;

public static class AppPaths
{
    private static readonly Lazy<string> _baseDataDir = new(() =>
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "CarRentalSystem");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    });

    public static string BaseDataDirectory => _baseDataDir.Value;

    public static string DatabaseDirectory => Path.Combine(BaseDataDirectory, "Database");
    public static string DatabaseFilePath => Path.Combine(DatabaseDirectory, "car_rental_local.db");

    public static string AutoBackupsDirectory => Path.Combine(BaseDataDirectory, "AutoBackups");
    public static string LogsDirectory => Path.Combine(BaseDataDirectory, "Logs");
    public static string WebView2DataDirectory => Path.Combine(BaseDataDirectory, "WebView2Data");
    public static string SystemLogFilePath => Path.Combine(LogsDirectory, "system.log");
    public static string CrashLogFilePath => Path.Combine(LogsDirectory, "crash.log");

    public static void Initialize()
    {
        Directory.CreateDirectory(DatabaseDirectory);
        Directory.CreateDirectory(AutoBackupsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(WebView2DataDirectory);

        // ضبط مسار WebView2 الآمن وبيئة التشغيل الفائقة لمنع التعليق والشاشة البيضاء
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", WebView2DataDirectory);
        Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", 
            "--no-first-run --disable-background-networking --disable-sync --disable-features=Translate,AutofillServerCommunication,OptimizationHints,msWebOOUI,msPdfOOUI --disable-component-update --disable-background-timer-throttling --disable-backgrounding-occluded-windows --disable-renderer-backgrounding --disable-extensions --disk-cache-size=0 --disable-application-cache");

        MigrateLegacyDatabaseIfNeeded();
    }

    /// <summary>
    /// نقل قاعدة البيانات القديمة من مجلد التطبيق (إذا وجدت) إلى المجلد الآمن في LocalAppData لتجنب مشاكل الصلاحيات
    /// </summary>
    private static void MigrateLegacyDatabaseIfNeeded()
    {
        try
        {
            var oldDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "car_rental_local.db");
            if (File.Exists(oldDbPath) && !File.Exists(DatabaseFilePath))
            {
                File.Copy(oldDbPath, DatabaseFilePath, overwrite: true);

                // نقل ملفات الـ WAL والـ SHM إن وجدت
                var oldWal = oldDbPath + "-wal";
                var newWal = DatabaseFilePath + "-wal";
                if (File.Exists(oldWal) && !File.Exists(newWal))
                {
                    File.Copy(oldWal, newWal, overwrite: true);
                }

                var oldShm = oldDbPath + "-shm";
                var newShm = DatabaseFilePath + "-shm";
                if (File.Exists(oldShm) && !File.Exists(newShm))
                {
                    File.Copy(oldShm, newShm, overwrite: true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppPaths] Migration warning: {ex.Message}");
        }
    }
}
