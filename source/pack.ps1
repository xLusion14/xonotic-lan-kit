# Appends the game zip to a freshly built setup exe, followed by a 16-byte trailer
# (zip length as Int64 little-endian + "XONLANK1") that Setup.cs uses to find it.
param([Parameter(Mandatory)] [string] $Exe, [Parameter(Mandatory)] [string] $Zip)
$ErrorActionPreference = 'Stop'

$out = [IO.File]::Open((Resolve-Path $Exe), [IO.FileMode]::Append)
try {
    $in = [IO.File]::OpenRead((Resolve-Path $Zip))
    try { $in.CopyTo($out, 1MB); $length = $in.Length } finally { $in.Dispose() }
    [byte[]] $trailer = [BitConverter]::GetBytes([int64] $length) + [Text.Encoding]::ASCII.GetBytes('XONLANK1')
    $out.Write($trailer, 0, $trailer.Length)
} finally { $out.Dispose() }
