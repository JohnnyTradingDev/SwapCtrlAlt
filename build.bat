@echo off
setlocal
set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    echo [ERROR] Built-in .NET Framework compiler not found at %CSC%.
    exit /b 1
)

if not exist dist mkdir dist
if not exist assets mkdir assets

echo [1/2] Generating multi-resolution icon and preview banner...
%CSC% /nologo /target:exe /main:SwapCtrlAlt.IconGenerator /out:dist\MakeIcon.exe src\SwapCtrlAlt.cs src\MakeIcon.cs
if errorlevel 1 exit /b 1
dist\MakeIcon.exe assets\SwapCtrlAlt.ico assets\preview.png
del /q dist\MakeIcon.exe

echo [2/2] Compiling SwapCtrlAlt.exe...
%CSC% /nologo /target:winexe /optimize+ /utf8output /win32icon:assets\SwapCtrlAlt.ico /main:SwapCtrlAlt.Program /out:dist\SwapCtrlAlt.exe src\SwapCtrlAlt.cs
if errorlevel 1 exit /b 1

echo.
echo [SUCCESS] Built dist\SwapCtrlAlt.exe (~42 KB) with zero external dependencies!
