@echo off
setlocal
echo ========================================================
echo Building Wireless Mirror Control Center Launcher...
echo ========================================================

set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC_PATH%" (
    set CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC_PATH%" (
    echo [ERROR] C# compiler csc.exe not found in Microsoft.NET Framework.
    pause
    exit /b 1
)

"%CSC_PATH%" /target:winexe /optimize+ /platform:anycpu /out:"%~dp0..\..\bin\WirelessMirrorLauncher.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0WirelessMirrorLauncher.cs"

if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Built successfully: bin\WirelessMirrorLauncher.exe
) else (
    echo [ERROR] Build failed with exit code %ERRORLEVEL%.
)

endlocal
