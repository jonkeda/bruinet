@echo off
setlocal

rem Generates .gen.cs files from every .tpl.cs file under the native Android Controls folder
rem (Bruinet.NativeAndroid) using the Bruinet ControlObject generator.

set "SCRIPT_DIR=%~dp0"
set "CLI_PROJECT=%SCRIPT_DIR%..\Bruinet.Cli\Bruinet.Cli.csproj"
set "CLI_EXE=%SCRIPT_DIR%..\Bruinet.Cli\bin\Release\net10.0\bruinet.exe"
set INPUT="%SCRIPT_DIR%..\..\src\Bruinet.NativeAndroid\Controls"

echo Building bruinet...
dotnet build "%CLI_PROJECT%" -c Release -v:minimal /nr:false
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo Running generator...
"%CLI_EXE%" controls generate %INPUT%
if errorlevel 1 (
    echo Generation failed.
    exit /b 1
)

echo Done. Generated output in: %INPUT%
endlocal
