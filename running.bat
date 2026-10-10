@echo off
chcp 65001 > nul
echo ===================================================
echo    تشغيل نظام تأجير السيارات (Car Rental System)
echo ===================================================
echo جاري تشغيل النظام...
dotnet run --project src/CarRental.App
if %ERRORLEVEL% neq 0 (
    echo.
    echo حدث خطأ أثناء تشغيل النظام.
    pause
)
