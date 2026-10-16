@echo off
setlocal
echo ========================================================
echo   Building Craft Console (EffectCraft / AE Companion)
echo ========================================================

set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo [ERROR] Could not find .NET Framework 4.x C# compiler at:
    echo %CSC%
    pause
    exit /b 1
)

if not exist "bin" mkdir "bin"
if not exist "dist" mkdir "dist"

echo [1/5] Compiling src\Launcher.cs with embedded Splash Art...
"%CSC%" /nologo /target:winexe ^
    /out:"bin\CraftConsole.exe" ^
    /win32icon:"assets\icon.ico" ^
    /resource:assets\splash.png,SplashArt ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" ^
    /r:"System.dll" ^
    /r:"System.Drawing.dll" ^
    /r:"System.Windows.Forms.dll" ^
    /r:"System.Core.dll" ^
    "src\Launcher.cs"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Failed compiling bin\CraftConsole.exe
    exit /b %ERRORLEVEL%
)

copy /y "bin\CraftConsole.exe" "dist\CraftConsole.exe" >nul

echo [2/5] Compiling Dedicated Uninstaller (bin\Uninstall.exe)...
"%CSC%" /nologo /target:winexe ^
    /out:"bin\Uninstall.exe" ^
    /win32icon:"assets\icon.ico" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" ^
    /r:"System.dll" ^
    /r:"System.Drawing.dll" ^
    /r:"System.Windows.Forms.dll" ^
    /r:"System.Core.dll" ^
    "src\Uninstall.cs"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Failed compiling bin\Uninstall.exe
    exit /b %ERRORLEVEL%
)

echo [3/5] Compiling Modern Dark Studio Installer (dist\CraftConsole-Setup-Modern.exe)...
"%CSC%" /nologo /target:winexe ^
    /out:"dist\CraftConsole-Setup-Modern.exe" ^
    /win32icon:"assets\icon.ico" ^
    /resource:bin\CraftConsole.exe,AppPayload ^
    /resource:bin\Uninstall.exe,UninstallPayload ^
    /resource:assets\splash.png,SplashArt ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" ^
    /r:"System.dll" ^
    /r:"System.Drawing.dll" ^
    /r:"System.Windows.Forms.dll" ^
    /r:"System.Core.dll" ^
    "src\SetupModern.cs"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Failed compiling dist\CraftConsole-Setup-Modern.exe
    exit /b %ERRORLEVEL%
)

echo [4/5] Compiling Classic Windows Wizard Installer (dist\CraftConsole-Setup-Classic.exe)...
"%CSC%" /nologo /target:winexe ^
    /out:"dist\CraftConsole-Setup-Classic.exe" ^
    /win32icon:"assets\icon.ico" ^
    /resource:bin\CraftConsole.exe,AppPayload ^
    /resource:bin\Uninstall.exe,UninstallPayload ^
    /resource:assets\splash.png,SplashArt ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" ^
    /r:"System.dll" ^
    /r:"System.Drawing.dll" ^
    /r:"System.Windows.Forms.dll" ^
    /r:"System.Core.dll" ^
    "src\SetupClassic.cs"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Failed compiling dist\CraftConsole-Setup-Classic.exe
    exit /b %ERRORLEVEL%
)

echo [5/5] Packaging Portable Distribution Zip (dist\CraftConsole-v1.0.2-portable.zip)...
powershell -NoProfile -Command "Compress-Archive -Path 'dist\CraftConsole.exe', 'scriptui', 'dist\splash', 'LICENSE', 'ATTRIBUTION.md', 'dist\README.txt' -DestinationPath 'dist\CraftConsole-v1.0.2-portable.zip' -Force"

if %ERRORLEVEL% neq 0 (
    echo [WARNING] Failed creating portable zip
)

echo.
echo ========================================================
echo   [SUCCESS] All targets built and updated successfully!
echo   * Standalone Binary:       bin\CraftConsole.exe
echo   * Dedicated Uninstaller:   bin\Uninstall.exe
echo   * Distribution Executable: dist\CraftConsole.exe
echo   * Modern Dark Installer:   dist\CraftConsole-Setup-Modern.exe
echo   * Classic Wizard Setup:    dist\CraftConsole-Setup-Classic.exe
echo   * Portable Release Zip:    dist\CraftConsole-v1.0.2-portable.zip
echo ========================================================

endlocal

