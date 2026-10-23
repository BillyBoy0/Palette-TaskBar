@echo off
rem Compile Palette-TaskBar.exe avec le compilateur C# fourni par Windows.
setlocal
cd /d "%~dp0"

rem L'exe en cours d'utilisation ne peut pas etre remplace : on le ferme d'abord.
taskkill /im Palette-TaskBar.exe /f >nul 2>&1

"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /optimize+ /target:winexe /win32icon:palette.ico /resource:palette.ico /out:Palette-TaskBar.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll Palette-TaskBar.cs
if errorlevel 1 (
    echo.
    echo Echec de la compilation.
    pause
    exit /b 1
)
echo Palette-TaskBar.exe compile.
