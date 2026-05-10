@echo off
echo ========================================
echo   XNB Exporter Pro v2.0 - Build Script
echo ========================================
echo.

:: Try to find C# compiler
set CSC=
set FOUND=0

:: Try .NET Framework csc.exe (most common on Windows)
for %%v in (v4.0.30319 v3.5 v2.0.50727) do (
    if exist "%WINDIR%\Microsoft.NET\Framework\%%v\csc.exe" (
        set CSC=%WINDIR%\Microsoft.NET\Framework\%%v\csc.exe
        set FOUND=1
        echo Found C# compiler: %%v
        goto :BUILD
    )
)

:: Try 64-bit Framework
for %%v in (v4.0.30319 v3.5) do (
    if exist "%WINDIR%\Microsoft.NET\Framework64\%%v\csc.exe" (
        set CSC=%WINDIR%\Microsoft.NET\Framework64\%%v\csc.exe
        set FOUND=1
        echo Found C# compiler (x64): %%v
        goto :BUILD
    )
)

:: Try MSBuild / Roslyn csc
for /f "tokens=*" %%i in ('where csc.exe 2^>nul') do (
    set CSC=%%i
    set FOUND=1
    echo Found C# compiler: %%i
    goto :BUILD
)

:: Try dotnet build as fallback
where dotnet >nul 2>&1
if %ERRORLEVEL%==0 (
    echo Using dotnet build...
    dotnet build -c Release
    if %ERRORLEVEL%==0 (
        echo.
        echo Build successful!
        echo Output: bin\Release\
    ) else (
        echo Build failed!
    )
    goto :EOF
)

echo ERROR: No C# compiler found!
echo Please install .NET Framework SDK or Visual Studio.
echo Or run from Visual Studio Developer Command Prompt.
goto :EOF

:BUILD
echo.
echo Compiling...

if not exist "bin" mkdir bin

"%CSC%" /nologo /optimize+ /target:winexe /out:bin\XNBExporterPro.exe ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.IO.Compression.dll ^
    /reference:System.Windows.Forms.dll ^
    Program.cs MainForm.cs XnbReader.cs DxtDecoder.cs LzxDecompressor.cs ImageWriter.cs

if %ERRORLEVEL%==0 (
    echo.
    echo ========================================
    echo   Build successful!
    echo   Output: bin\XNBExporterPro.exe
    echo ========================================
) else (
    echo.
    echo Build FAILED! Check errors above.
)

:EOF
pause
