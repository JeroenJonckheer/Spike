# Starts Windows Sandbox for the end-to-end test: dist\Spike.exe is mapped read-only to
# C:\spike, this folder to C:\phase0 for the results. Publish first:
#   dotnet publish src\Spike -c Release

$here = $PSScriptRoot
$dist = Join-Path (Split-Path (Split-Path $here)) 'dist'
if (-not (Test-Path (Join-Path $dist 'Spike.exe'))) { throw "Publish Spike first: dist\Spike.exe is missing." }
$out = Join-Path $here 'out\e2e'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

$wsb = @"
<Configuration>
  <Networking>Enable</Networking>
  <MappedFolders>
    <MappedFolder><HostFolder>$dist</HostFolder><SandboxFolder>C:\spike</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$here</HostFolder><SandboxFolder>C:\phase0</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell -NoProfile -ExecutionPolicy Bypass -File C:\phase0\e2e.ps1</Command>
  </LogonCommand>
</Configuration>
"@
$path = Join-Path $env:TEMP 'spike-e2e.wsb'
Set-Content $path $wsb -Encoding UTF8
Start-Process $path
