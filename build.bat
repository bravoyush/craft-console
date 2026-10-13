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

echo Compiling src\Launcher.cs with embedded Splash Art...
"%CSC%" /target:winexe ^
    /out:"bin\CraftConsole.exe" ^
    /win32icon:"assets\icon.ico" ^
    /resource:"assets\splash.png",SplashArt ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" ^
    /r:"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll" ^
    /r:"System.dll" ^
    /r:"System.Drawing.dll" ^
    /r:"System.Windows.Forms.dll" ^
    /r:"System.Core.dll" ^
    "src\Launcher.cs"

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Build succeeded! Binary created at:
    echo   bin\CraftConsole.exe
    echo.
) else (
    echo.
    echo [ERROR] Build failed with exit code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)

endlocal
