@echo off
rem Builds both setup programs with the C# compiler that ships with Windows (.NET Framework 4.x).
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OPTS=/nologo /optimize+ /target:exe /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /win32icon:source\xonotic.ico

"%CSC%" %OPTS% /out:Xonotic-Player-Setup.exe source\Setup.cs || goto fail
"%CSC%" %OPTS% /define:SERVER /out:Xonotic-Server-Setup-HOST-ONLY.exe source\Setup.cs || goto fail
echo Built Xonotic-Player-Setup.exe and Xonotic-Server-Setup-HOST-ONLY.exe.
exit /b 0

:fail
echo Build failed.
exit /b 1
