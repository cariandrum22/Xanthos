<# .SYNOPSIS
Checks x64 JV-Link registration and PE architecture without changing settings.
#>
param([string]$ProgId = 'JVDTLab.JVLink')
$ErrorActionPreference = 'Stop'
if ([IntPtr]::Size -ne 8) { throw 'Run this check in x64 PowerShell.' }
$root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::ClassesRoot, [Microsoft.Win32.RegistryView]::Registry64)
try {
    $key = $root.OpenSubKey("$ProgId\CLSID")
    if ($null -eq $key) { throw "No x64 registration for $ProgId." }
    try { $clsid = [string]$key.GetValue('') } finally { $key.Dispose() }
    $key = $root.OpenSubKey("CLSID\$clsid\InprocServer32")
    if ($null -eq $key) { throw 'No x64 in-process server registration.' }
    try { $dll = [string]$key.GetValue('') } finally { $key.Dispose() }
    $stream = [IO.File]::OpenRead($dll)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        $stream.Position = 0x3c
        $offset = $reader.ReadInt32()
        $stream.Position = $offset
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne 0x8664) { throw 'COM server is not an x64 PE binary.' }
    } finally { $reader.Dispose() }
    [pscustomobject]@{ ProgId = $ProgId; RegistryView = 'Registry64'; Clsid = $clsid; Server = $dll; Architecture = 'X64' }
} finally { $root.Dispose() }
