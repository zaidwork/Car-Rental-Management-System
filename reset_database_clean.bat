@echo off
chcp 65001 >nul
cls
echo ===============================================================================
echo            تصفير قاعدة البيانات المحلية وبدء النظام نظيفاً 100%%
echo ===============================================================================
echo.
echo سيتم أخذ نسخة احتياطية من قاعدة البيانات الحالية أولاً، ثم مسحها
echo لتبدأ العمل بنظام نظيف يحتوي تلقائياً على أسطول سياراتك الأساسي (12 سيارة) بدون عقود أو ديون.
echo.
set /p confirm="هل أنت متأكد من رغبتك في تصفير قاعدة البيانات؟ (y/n): "
if /i not "%confirm%"=="y" (
    echo تم إلغاء العملية.
    pause
    exit /b 0
)

set "DB_DIR=%LOCALAPPDATA%\CarRentalSystem\Database"
set "BACKUP_DIR=%LOCALAPPDATA%\CarRentalSystem\AutoBackups"

if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

if exist "%DB_DIR%\car_rental_local.db" (
    echo جاري حفظ نسخة احتياطية في AutoBackups...
    copy /y "%DB_DIR%\car_rental_local.db" "%BACKUP_DIR%\backup_before_reset.db" >nul
    del /f /q "%DB_DIR%\car_rental_local.db" >nul 2>&1
    del /f /q "%DB_DIR%\car_rental_local.db-wal" >nul 2>&1
    del /f /q "%DB_DIR%\car_rental_local.db-shm" >nul 2>&1
    echo [نجاح] تم مسح قاعدة البيانات القديمة بنجاح!
) else (
    echo قاعدة البيانات نظيفة بالفعل أو غير موجودة.
)

echo.
echo عند تشغيل النظام الآن، سيبدأ بقاعدة بيانات جديدة كلياً متضمنة أسطول السيارات الأساسي (12 سيارة) وجاهزة للعمل.
echo بيانات تسجيل الدخول الافتراضية: admin / 1234
echo ===============================================================================
pause
