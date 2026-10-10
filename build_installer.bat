@echo off
chcp 65001 >nul
cls
echo ===============================================================================
echo        بناء وتجميع حزمة التثبيت الذاتية لنظام تأجير السيارات (Inno Setup)
echo ===============================================================================
echo.

:: 1. التحقق من وجود .NET SDK
where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [خطأ] محرك .NET غير موجود في مسار النظام (PATH).
    pause
    exit /b 1
)

:: 2. التحقق من وجود Inno Setup Compiler (ISCC.exe)
set "ISCC_PATH="
if exist "%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe" (
    set "ISCC_PATH=%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe"
) else if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
) else if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%ProgramFiles%\Inno Setup 6\ISCC.exe"
) else (
    where iscc >nul 2>&1
    if %ERRORLEVEL% equ 0 (
        set "ISCC_PATH=iscc"
    )
)

if "%ISCC_PATH%"=="" (
    echo [خطأ] لم يتم العثور على Inno Setup Compiler (ISCC.exe).
    echo يرجى التأكد من تثبيت برنامج Inno Setup.
    pause
    exit /b 1
)

echo [1/3] جاري فحص ونشر ملفات النظام ذاتياً (Self-Contained Release win-x64)...
dotnet publish src\CarRental.App\CarRental.App.csproj -c Release -r win-x64 --self-contained true -o dist\win-x64
if %ERRORLEVEL% neq 0 (
    echo [خطأ] فشلت عملية النشر (dotnet publish).
    pause
    exit /b 1
)

echo.
echo [1.5/3] جاري تضمين قالب قاعدة البيانات النظيفة 100%% داخل الحزمة...
if exist "installer\car_rental_clean.db" (
    copy /y "installer\car_rental_clean.db" "dist\win-x64\car_rental_local.db" >nul
)

echo.
echo [2/3] جاري ضغط وتجميع ملف التثبيت بواسطة Inno Setup Compiler...
"%ISCC_PATH%" installer\installer.iss
if %ERRORLEVEL% neq 0 (
    echo [خطأ] فشل تجميع ملف التثبيت بواسطة Inno Setup.
    pause
    exit /b 1
)

if exist "installer_output\CarRentalSystem_Setup_Pro_X_4.exe" (
    copy /y "installer_output\CarRentalSystem_Setup_Pro_X_4.exe" "installer_output\pro_x_4.exe" >nul
    copy /y "installer_output\CarRentalSystem_Setup_Pro_X_4.exe" "installer_output\Pro_X_4_Setup.exe" >nul
)

echo.
echo ===============================================================================
echo [3/3] تم إنشاء ملف التثبيت بنجاح تام بعنوان Pro X 4!
echo مسار الملف: installer_output\CarRentalSystem_Setup_Pro_X_4.exe
echo نسخة سريعة: installer_output\pro_x_4.exe
echo ===============================================================================
echo.
explorer installer_output
pause
