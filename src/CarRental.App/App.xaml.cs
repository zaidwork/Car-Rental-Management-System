using System;
using System.IO;
using System.Windows;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Markup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CarRental.Infrastructure.Data;
using CarRental.Infrastructure.Helpers;
using CarRental.Infrastructure.Services;
using CarRental.Core.Interfaces;

namespace CarRental.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private static Mutex? _appMutex;

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr proc, UIntPtr min, UIntPtr max);

    protected override void OnStartup(StartupEventArgs e)
    {
        // 1. حماية النظام ومنع تكرار فتح البرنامج في نفس الوقت لحماية ملفات البيانات من الـ Locking
        const string mutexName = @"Global\CarRentalSystemSingleInstanceMutex";
        _appMutex = new Mutex(true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "نظام إدارة تأجير السيارات مفتوح ويعمل بالفعل على هذا الجهاز!\nيرجى استخدام النافذة المفتوحة مسبقاً لتفادي تضارب البيانات.",
                "نظام تأجير السيارات",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // 2. تسجيل حواضن ومصائد الاستثناءات غير المتوقعة (Multi-Tier Crash Protection)
        SetupGlobalExceptionHandling();

        // 3. فرض التقويم الميلادي الصارم على كافة خيوط وعمليات النظام وتطبيقات العرض
        var gregorianCulture = (CultureInfo)CultureInfo.GetCultureInfo("ar-EG").Clone();
        gregorianCulture.DateTimeFormat.Calendar = new GregorianCalendar();
        gregorianCulture.DateTimeFormat.DateSeparator = "/";
        gregorianCulture.DateTimeFormat.ShortDatePattern = "yyyy/MM/dd";
        gregorianCulture.DateTimeFormat.LongDatePattern = "dd MMMM yyyy";
        gregorianCulture.NumberFormat.DigitSubstitution = DigitShapes.None;

        CultureInfo.DefaultThreadCurrentCulture = gregorianCulture;
        CultureInfo.DefaultThreadCurrentUICulture = gregorianCulture;
        Thread.CurrentThread.CurrentCulture = gregorianCulture;
        Thread.CurrentThread.CurrentUICulture = gregorianCulture;

        try
        {
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("ar-EG")));
        }
        catch { }

        // 4. تهيئة مسارات التخزين الآمنة في %LocalAppData% والتحقق من سلامة البيانات (Self-Healing)
        AppPaths.Initialize();
        DatabaseSelfHealingService.PerformDatabaseSelfHealingAsync(AppPaths.DatabaseFilePath).GetAwaiter().GetResult();

        // 5. تهيئة حقن التبعيات
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddWpfBlazorWebView();
        serviceCollection.AddBlazorWebViewDeveloperTools();

        serviceCollection.AddDbContextFactory<CarRentalDbContext>(options =>
        {
            options.UseSqlite($"Data Source={AppPaths.DatabaseFilePath}");
        });

        serviceCollection.AddSingleton<IRentalSystemService, RentalSystemService>();
        serviceCollection.AddSingleton<IAuthStateService, AuthStateService>();
        serviceCollection.AddSingleton<INotificationService, NotificationService>();

        Services = serviceCollection.BuildServiceProvider();

        // 6. تسجيل الحاوية في موارد التطبيق قبل فتح النافذة
        Resources.Add("services", Services);

        // 7. تهيئة قاعدة البيانات المحلية وتطبيق الـ WAL mode والنسخ الاحتياطي التلقائي
        var dbFactory = Services.GetRequiredService<IDbContextFactory<CarRentalDbContext>>();
        using (var context = dbFactory.CreateDbContext())
        {
            DbInitializer.InitializeAsync(context).GetAwaiter().GetResult();
        }

        // 7.1. تشغيل محرك المزامنة السحابية ثنائي الاتجاه بالخلفية
        try
        {
            var rentalService = Services.GetRequiredService<IRentalSystemService>();
            CloudSyncService.StartTwoWaySyncBackground(dbFactory, rentalService);
        }
        catch { }

        // 8. تقليص مساحة الذاكرة الفيزيائية المستهلكة
        MinimizeMemoryFootprint();

        // 9. فتح نافذة التطبيق الرئيسية بعد اكتمال التهيئة بنجاح
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _appMutex?.ReleaseMutex();
            _appMutex?.Dispose();
        }
        catch { }

        base.OnExit(e);

        try
        {
            Environment.Exit(0);
        }
        catch { }
    }

    private void SetupGlobalExceptionHandling()
    {
        // 1. واجهة المستخدم الرسومية وخيوط الـ UI
        DispatcherUnhandledException += (s, args) =>
        {
            LogException("UI Dispatcher", args.Exception);
            args.Handled = true; // منع الإغلاق والانهيار المفاجئ للبرنامج
            
            var detailedMessage = args.Exception.InnerException != null 
                ? $"{args.Exception.Message}\n({args.Exception.InnerException.Message})" 
                : args.Exception.Message;

            MessageBox.Show(
                $"تنبيه أمان: حدث استثناء في واجهة النظام وتمت معالجته بأمان دون فقد البيانات.\nالتفاصيل: {detailedMessage}",
                "تنبيه استقرار النظام",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        };

        // 2. بيئة الـ CLR العامة
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogException("AppDomain", ex);
            }
        };

        // 3. المهام غير المتزامنة (Async/Await Tasks)
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogException("TaskScheduler", args.Exception);
            args.SetObserved();
        };
    }

    private static void LogException(string source, Exception ex)
    {
        try
        {
            var logPath = AppPaths.CrashLogFilePath;
            // تدوير ملف اللوغ إذا زاد حجمه عن 5 ميجابايت
            if (File.Exists(logPath) && new FileInfo(logPath).Length > 5 * 1024 * 1024)
            {
                var oldLog = Path.Combine(AppPaths.LogsDirectory, $"crash_prev_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.Move(logPath, oldLog, overwrite: true);
            }

            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex.GetType().Name}: {ex.ToString()}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(logPath, logEntry);
        }
        catch { }
    }

    /// <summary>
    /// تقليص استهلاك الرام وإعادة الصفحات غير المستخدمة لنظام التشغيل
    /// </summary>
    public static void MinimizeMemoryFootprint()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, (UIntPtr)UInt32.MaxValue, (UIntPtr)UInt32.MaxValue);
            }
        }
        catch { }
    }
}
