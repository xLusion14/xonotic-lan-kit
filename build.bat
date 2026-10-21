@echo off
rem Builds both setup programs with the C# compiler that ships with Windows (.NET Framework 4.x),
rem then packs the game zip into each one so it's a single self-contained installer.
rem Needs xonotic-0.8.6.zip (from xonotic.org) in this folder.
cd /d "%~dp0"

if not exist xonotic-0.8.6.zip (
  echo Put xonotic-0.8.6.zip from https://xonotic.org/download in this folder first.
  exit /b 1
)

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OPTS=/nologo /optimize+ /target:exe /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Management.dll /win32icon:source\xonotic.ico /resource:menu\zzz-lan-menu.pk3,zzz-lan-menu.pk3
set PACK=powershell -NoProfile -ExecutionPolicy Bypass -File source\pack.ps1
set WPF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF

rem The server control panel (WPF, layout in ServerPanel.xaml), embedded in the server setup
"%CSC%" /nologo /optimize+ /target:winexe "/lib:%WPF%" /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /win32icon:source\xonotic.ico /resource:source\ServerPanel.xaml,ServerPanel.xaml "/out:Xonotic Server.exe" source\ServerPanel.cs || goto fail

"%CSC%" %OPTS% /out:Xonotic-Player-Setup.exe source\Setup.cs || goto fail
"%CSC%" %OPTS% /define:SERVER "/resource:Xonotic Server.exe,Xonotic Server.exe" /out:Xonotic-Server-Setup-HOST-ONLY.exe source\Setup.cs || goto fail
echo Packing the game into the setup programs...
%PACK% Xonotic-Player-Setup.exe xonotic-0.8.6.zip || goto fail
%PACK% Xonotic-Server-Setup-HOST-ONLY.exe xonotic-0.8.6.zip || goto fail
echo Built Xonotic-Player-Setup.exe and Xonotic-Server-Setup-HOST-ONLY.exe.
exit /b 0

:fail
echo Build failed.
exit /b 1
