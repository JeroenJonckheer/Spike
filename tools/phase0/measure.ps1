# Phase 0: runs INSIDE Windows Sandbox (started by start-phase0.ps1).
# Downloads the remote-access programs that scammers use most, then records what Spike
# can see of them: signature, version information, processes, services, connections,
# folders and window titles. Finally it leaves AnyDesk running and starts an input logger,
# so a real session from a phone can show whether remote mouse input is marked "injected".
# Results go to C:\phase0\out, which is a folder on the host.

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'
$out = 'C:\phase0\out'
$dl = 'C:\Users\WDAGUtilityAccount\Downloads\phase0'   # inside the Sandbox only, never on the host
New-Item -ItemType Directory -Force $out, $dl | Out-Null
Start-Transcript "$out\transcript.txt" | Out-Null

$tools = @(
    @{ Name = 'AnyDesk';     Url = 'https://download.anydesk.com/AnyDesk.exe';                                             File = 'AnyDesk.exe';               Args = '' },
    @{ Name = 'TeamViewer';  Url = 'https://download.teamviewer.com/download/TeamViewerQS_x64.exe';                          File = 'TeamViewerQS_x64.exe';      Args = '' },
    @{ Name = 'RustDesk';    Url = 'https://github.com/rustdesk/rustdesk/releases/download/1.5.3/rustdesk-1.5.3-x86_64.exe'; File = 'rustdesk-1.5.3-x86_64.exe'; Args = '' },
    @{ Name = 'UltraViewer'; Url = 'https://www.ultraviewer.net/en/UltraViewer_setup_6.6_en.exe';                            File = 'UltraViewer_setup.exe';     Args = '/VERYSILENT /NORESTART' }
)

function Describe-File([string]$path) {
    if (-not $path -or -not (Test-Path $path)) { return $null }
    $sig = Get-AuthenticodeSignature $path
    $ver = (Get-Item $path).VersionInfo
    [ordered]@{
        Path             = $path
        Size             = (Get-Item $path).Length
        Sha256           = (Get-FileHash $path -Algorithm SHA256).Hash
        SignatureStatus  = "$($sig.Status)"
        Signer           = $sig.SignerCertificate.Subject
        Issuer           = $sig.SignerCertificate.Issuer
        Thumbprint       = $sig.SignerCertificate.Thumbprint
        OriginalFilename = $ver.OriginalFilename
        ProductName      = $ver.ProductName
        CompanyName      = $ver.CompanyName
        FileDescription  = $ver.FileDescription
        InternalName     = $ver.InternalName
        FileVersion      = $ver.FileVersion
    }
}

function Snapshot {
    [ordered]@{
        Processes   = @(Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId, Name, ExecutablePath, CommandLine)
        Services    = @(Get-CimInstance Win32_Service | Select-Object Name, DisplayName, PathName, StartMode, State, StartName)
        Tcp         = @(Get-NetTCPConnection -ErrorAction SilentlyContinue | Select-Object LocalAddress, LocalPort, RemoteAddress, RemotePort, State, OwningProcess)
        Udp         = @(Get-NetUDPEndpoint -ErrorAction SilentlyContinue | Select-Object LocalAddress, LocalPort, OwningProcess)
        Folders     = @(foreach ($root in @($env:ProgramData, $env:APPDATA, $env:LOCALAPPDATA, ${env:ProgramFiles}, ${env:ProgramFiles(x86)})) {
                          if ($root -and (Test-Path $root)) { Get-ChildItem $root -Directory -ErrorAction SilentlyContinue | ForEach-Object FullName } })
        Windows     = @(Get-Process | Where-Object MainWindowTitle | Select-Object Id, ProcessName, MainWindowTitle)
    }
}

# 1. Download
foreach ($t in $tools) {
    $target = Join-Path $dl $t.File
    try {
        Invoke-WebRequest -Uri $t.Url -OutFile $target -UserAgent 'Mozilla/5.0' -UseBasicParsing
        Write-Host "downloaded $($t.Name): $((Get-Item $target).Length) bytes"
    } catch { Write-Host "download FAILED $($t.Name): $_" }
}

# 2. Per program: start it, wait, and record what changed.
$results = [ordered]@{}
foreach ($t in $tools) {
    $file = Join-Path $dl $t.File
    if (-not (Test-Path $file)) { continue }
    $before = Snapshot
    $p = if ($t.Args) { Start-Process $file -ArgumentList $t.Args -PassThru } else { Start-Process $file -PassThru }
    Start-Sleep -Seconds 45
    $after = Snapshot

    $oldPids = $before.Processes.ProcessId
    $newProcs = @($after.Processes | Where-Object { $_.ProcessId -notin $oldPids })
    $oldSvcs = $before.Services.Name
    $newSvcs = @($after.Services | Where-Object { $_.Name -notin $oldSvcs })
    $newPids = @($newProcs.ProcessId)
    $results[$t.Name] = [ordered]@{
        Download     = Describe-File $file
        NewProcesses = @($newProcs | ForEach-Object { [ordered]@{ Process = $_; File = (Describe-File $_.ExecutablePath) } })
        NewServices  = @($newSvcs | ForEach-Object { [ordered]@{ Service = $_; File = (Describe-File (($_.PathName -replace '^"([^"]+)".*$', '$1') -replace '^(\S+\.exe).*$', '$1')) } })
        Tcp          = @($after.Tcp | Where-Object { $_.OwningProcess -in $newPids })
        Udp          = @($after.Udp | Where-Object { $_.OwningProcess -in $newPids })
        NewFolders   = @($after.Folders | Where-Object { $_ -notin $before.Folders })
        Windows      = @($after.Windows | Where-Object { $_.Id -in $newPids })
    }
    $results | ConvertTo-Json -Depth 8 | Set-Content "$out\results.json" -Encoding UTF8

    # Close it again (services too) so the next program starts from a clean state.
    foreach ($s in $newSvcs) { Stop-Service $s.Name -Force -ErrorAction SilentlyContinue }
    foreach ($q in $newProcs) { Stop-Process -Id $q.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 5
}

# 3. Input logger + AnyDesk for the session test from a phone.
Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'C:\phase0\inputlog.ps1' -WindowStyle Minimized
$any = Join-Path $dl 'AnyDesk.exe'
if (Test-Path $any) { Start-Process $any }

'done' | Set-Content "$out\measure-done.txt"
Stop-Transcript | Out-Null
