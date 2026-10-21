# Builds menu\zzz-lan-menu.pk3: the LAN main screen (games on the network, gear -> Settings pop-up).
# Takes the official Xonotic 0.8.6 source that ships with the game (C:\Games\Xonotic\source), overlays the
# changed files from menu\qcsrc and menu\gmqcc, builds the gmqcc QuakeC compiler with MSVC, compiles menu.dat
# (mirroring qcsrc/tools/qcc.sh) and packs it with the gear icon.
# Needs Visual Studio Build Tools with the C++ workload. Only needed when the menu changes; the built pk3 is committed.
param(
    [string] $XonoticSource = 'C:\Games\Xonotic\source',
    [string] $Work = 'C:\Games\xon-menu-work'
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio Build Tools with C++ not found.' }
$vcvars = "$vs\VC\Auxiliary\Build\vcvars64.bat"
$env:PATH = "$(Split-Path $vswhere);$env:PATH"

Write-Host 'Copying the official source and applying the LAN changes...'
if (Test-Path $Work) { Remove-Item -LiteralPath $Work -Recurse -Force }
New-Item -ItemType Directory -Force "$Work\out" | Out-Null
Copy-Item "$XonoticSource\qcsrc" "$Work\qcsrc" -Recurse
Copy-Item "$XonoticSource\gmqcc" "$Work\gmqcc" -Recurse
Copy-Item "$here\qcsrc\*" "$Work\qcsrc" -Recurse -Force
Copy-Item "$here\gmqcc\*" "$Work\gmqcc" -Recurse -Force   # fold.cpp: MSVC fixes (clz, NaN/inf constants)

Write-Host 'Building gmqcc...'
$src = 'ast.cpp code.cpp conout.cpp fold.cpp ftepp.cpp intrin.cpp ir.cpp lexer.cpp opts.cpp parser.cpp stat.cpp utf8.cpp util.cpp main.cpp'
cmd /c "`"$vcvars`" >nul && cd /d `"$Work\gmqcc`" && cl /nologo /O2 /EHsc /std:c++17 /W0 /D_CRT_SECURE_NO_WARNINGS /Fe:gmqcc.exe $src >`"$Work\out\gmqcc-build.txt`" 2>&1"
if ($LASTEXITCODE -ne 0) { Get-Content "$Work\out\gmqcc-build.txt" | Select-String error | Select-Object -First 20; throw 'gmqcc build failed' }

Write-Host 'Preprocessing the menu...'
$defs = '/DGMQCC /DXONOTIC=1 /DWATERMARK="\"0.8.6-lan\"" /DNDEBUG=1 /DENABLE_EFFECTINFO=0 /DENABLE_DEBUGDRAW=0 /DENABLE_DEBUGTRACE=0 /DMENUQC'
cmd /c "`"$vcvars`" >nul && cd /d `"$Work\qcsrc`" && cl /nologo /E /Zc:preprocessor /Zc:__STDC__ /TC /I. $defs menu\progs.inc > `"$Work\out\menu.txt`" 2> `"$Work\out\cpp-err.txt`""
if ($LASTEXITCODE -ne 0) { Get-Content "$Work\out\cpp-err.txt" | Select-Object -First 20; throw 'preprocessor failed' }

# gmqcc wants "#pragma file(...)/#pragma line(...)" on fresh lines instead of #line, and no #pragma once.
$txt = [IO.File]::ReadAllText("$Work\out\menu.txt").Replace("`r`n", "`n")
$qc = [regex]::Replace($txt, '(?m)^[ \t]*#line (\d+)(?: "(.*)")?[ \t]*$', {
    param($m)
    if ($m.Groups[2].Success) { "`n#pragma file($($m.Groups[2].Value.Replace('\\', '/')))`n#pragma line($($m.Groups[1].Value))" }
    else { "`n#pragma line($($m.Groups[1].Value))" }
})
$qc = [regex]::Replace($qc, '(?m)^[ \t]*#pragma once[ \t]*$', '')
[IO.File]::WriteAllText("$Work\out\menu.qc", $qc)

Write-Host 'Compiling menu.dat...'
$flags = '-std=gmqcc', '-Ooverlap-locals', '-O3', '-Werror', '-Wall', '-Wno-field-redeclared', '-flno', '-futf8',
         '-fno-bail-on-werror', '-frelaxed-switch', '-freturn-assignments'
Push-Location "$Work\qcsrc\tools"
try { & "$Work\gmqcc\gmqcc.exe" @flags -o "$Work\out\menu.dat" "$Work\out\menu.qc" > "$Work\out\gmqcc.txt" 2>&1 }
finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { Get-Content "$Work\out\gmqcc.txt" | Select-Object -Last 20; throw 'gmqcc failed' }

$pk3 = "$here\zzz-lan-menu.pk3"
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
if (Test-Path $pk3) { Remove-Item -LiteralPath $pk3 }
$zip = [IO.Compression.ZipFile]::Open($pk3, 'Create')
try {
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, "$Work\out\menu.dat", 'menu.dat')
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, "$here\gfx\lanmenu\gear.png", 'gfx/lanmenu/gear.png')
} finally { $zip.Dispose() }
Write-Host "Built $pk3 ($([math]::Round((Get-Item $pk3).Length / 1KB)) KB)"
