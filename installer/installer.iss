; ==============================================================================
; سكريبت Inno Setup Compiler الاحترافي لتجميع وتوليد ملف تثبيت نظام تأجير السيارات
; ==============================================================================

#define MyAppName "نظام إدارة تأجير السيارات - Pro X 4"
#define MyAppEnglishName "Car Rental Management System Pro X 4"
#define MyAppVersion "Pro X 4"
#define MyAppPublisher "زيد بشار دراغمة (Zaid Bashar Daraghmeh)"
#define MyAppPublisherEmail "zaidbashardaraghmeh@gmail.com"
#define MyAppPublisherPhone "+972598776026"
#define MyAppCopyright "Copyright © 2026 Zaid Bashar Daraghmeh. All Rights Reserved."
#define MyAppExeName "CarRental.App.exe"
#define MyAppId "{{7D2E68B4-9F81-4C5C-A77E-8F1A20D75630}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppPublisherEmail}
AppSupportPhone={#MyAppPublisherPhone}
AppContact={#MyAppPublisherEmail}
AppCopyright={#MyAppCopyright}
DefaultDirName={autopf}\CarRentalSystem
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\installer_output
OutputBaseFilename=CarRentalSystem_Setup_Pro_X_4
SetupIconFile=..\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

; الصفحات الثلاث القانونية والتعريفية المطلوبة
LicenseFile=license.txt
InfoBeforeFile=rights.txt
InfoAfterFile=info.txt

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "cleandb"; Description: "تصفير وتثبيت قاعدة بيانات نظيفة وجديدة (السيارات والزبائن المسجلين فقط وبدون أي عمليات سابقة)"; GroupDescription: "خيارات تهيئة النظام"; Flags: unchecked

[InstallDelete]
; تنظيف كافة الملفات التنفيذية والمكتبات القديمة قبل نسخ الإصدار الجديد لمنع أي تعارض
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.exe"
Type: files; Name: "{app}\*.pdb"

[Files]
; تضمين كافة ملفات وحزم التطبيق المنشورة ذاتياً (Self-Contained x64)
Source: "..\dist\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\AppIcon.ico"; DestDir: "{app}"; Flags: ignoreversion
; تثبيت وتجهيز قاعدة البيانات النظيفة تلقائياً إذا كان التثبيت جديداً أو عند تحديد خيار التصفير
Source: "car_rental_clean.db"; DestDir: "{localappdata}\CarRentalSystem\Database"; DestName: "car_rental_local.db"; Flags: onlyifdoesntexist
Source: "car_rental_clean.db"; DestDir: "{localappdata}\CarRentalSystem\Database"; DestName: "car_rental_local.db"; Tasks: cleandb; Flags: ignoreversion

[Icons]
; اختصارات قائمة ابدأ وسطح المكتب
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\AppIcon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\AppIcon.ico"; Tasks: desktopicon

[Run]
; تشغيل النظام تلقائياً بعد انتهاء التثبيت إذا رغب المستخدم
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; تنظيف الملفات المؤقتة واللوجات عند إلغاء التثبيت
Type: filesandordirs; Name: "{app}\Logs"

[Code]
// وظيفة استخراج مسار إلغاء تثبيت النسخة السابقة من سجل ويندوز
function GetPriorUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}_is1');
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

// وظيفة الفحص المسبق: إنهاء أي نسخة قيد التشغيل وإلغاء تثبيت النسخة السابقة صامتاً لتحديث نظيف
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
  Result := '';
  NeedsRestart := False;

  // 1. إغلاق أي نافذة أو عملية نشطة للبرنامج لفك قفل الملفات قبل التثبيت
  Exec('taskkill.exe', '/f /im {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, iResultCode);
  Sleep(400);

  // 2. إذا كانت هناك نسخة سابقة مثبتة، يتم تشغيل برنامج إلغاء التثبيت صامتاً لحذف الملفات القديمة
  sUnInstallString := GetPriorUninstallString();
  if sUnInstallString <> '' then
  begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    Exec(sUnInstallString, '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, iResultCode);
    Sleep(800);
  end;
end;
