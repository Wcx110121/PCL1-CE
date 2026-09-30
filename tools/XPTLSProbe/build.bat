@echo off
setlocal
rem ============================================================
rem  XPTLSProbe build script
rem
rem  Compiles the probe with the .NET Framework 4.0 compiler that
rem  ships with Windows, so the resulting exe also runs on Windows XP.
rem  No Visual Studio required.
rem
rem  Usage: put this file next to XPTLSProbe.vb and double-click it.
rem ============================================================

set "VBC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\vbc.exe"

if not exist "%VBC%" (
    echo [ERROR] Not found: %VBC%
    echo         .NET Framework 4.0 or later is required.
    pause
    exit /b 1
)

if not exist "%~dp0BouncyCastle.Crypto.dll" (
    echo [ERROR] Not found: %~dp0BouncyCastle.Crypto.dll
    echo         It is required for the managed-TLS test mode.
    pause
    exit /b 1
)

echo Compiling XPTLSProbe.exe ...
"%VBC%" /nologo /target:exe /out:"%~dp0XPTLSProbe.exe" /optioninfer+ /optionexplicit+ /optionstrict- /r:System.dll /r:System.Core.dll /r:"%~dp0BouncyCastle.Crypto.dll" "%~dp0XPTLSProbe.vb" "%~dp0ModTls.vb"

if errorlevel 1 (
    echo.
    echo [ERROR] Compile failed. See messages above.
    pause
    exit /b 1
)

echo.
echo Done: %~dp0XPTLSProbe.exe
echo.
echo   XPTLSProbe.exe                   - diagnose TLS / certificates / registry
echo   XPTLSProbe.exe managed           - test BouncyCastle managed TLS (bypasses schannel)
echo   XPTLSProbe.exe msa ^<client_id^>  - test the full Microsoft login chain
echo   XPTLSProbe.exe download          - test the download chain
echo   XPTLSProbe.exe makereg           - generate .reg fix files
echo.
pause
