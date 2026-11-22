<#
.SYNOPSIS
    Installs or updates the IL2-SRS web server as a Windows service.

.DESCRIPTION
    Copies the server from the folder this script is in to C:\Program Files\IL2-SRS\<ServiceName> and keeps its data
    in C:\ProgramData\IL2-SRS\<ServiceName>. The service runs as SYSTEM and loads its program and plugins from these
    folders, so only Administrators and SYSTEM may change them: Windows already protects Program Files, and the script
    restricts the data folder. On the first install it writes srs-server.json with the admin password and web
    address. It adds firewall rules for the SRS port (TCP and UDP) and starts the service.

    To update, extract the new release anywhere and run its script again with the same -ServiceName. An install from
    before this layout (data in a folder such as C:\IL2-SRS\main) has its data copied to the new folder; the old
    folders are left as they were, for you to delete once the service works.

    Run from an elevated PowerShell prompt.

.EXAMPLE
    .\Install-WindowsService.ps1 -AdminPassword 'a-long-password'

.EXAMPLE
    # A second instance on another port
    .\Install-WindowsService.ps1 -ServiceName IL2-SRS-Server-2 -SrsPort 6003 -WebUrl http://localhost:8081

.EXAMPLE
    # Move an existing server's data folder into the protected one
    .\Install-WindowsService.ps1 -DataDirectory C:\IL2-SRS\main
#>
[CmdletBinding()]
param(
    [string] $ServiceName = 'IL2-SRS-Server',
    # An existing data folder to copy from, e.g. one made by an older version of this script. An existing service's
    # data folder is found by itself.
    [string] $DataDirectory,
    [string] $AdminPassword,
    [int] $SrsPort = 6002,
    [string] $WebUrl = 'http://localhost:8080',
    [switch] $NoFirewallRules
)

$ErrorActionPreference = 'Stop'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated (Administrator) PowerShell prompt.'
}

if ($ServiceName -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$') {
    throw 'The service name may contain only letters, digits, dots, hyphens and underscores.'
}

$sourceDirectory = $PSScriptRoot
if (-not (Test-Path (Join-Path $sourceDirectory 'IL2-SRS-Server.exe'))) {
    throw "IL2-SRS-Server.exe is not next to this script. Run the script from the extracted release folder."
}

$appDirectory = Join-Path $env:ProgramFiles "IL2-SRS\$ServiceName"
$dataRoot = Join-Path $env:ProgramData 'IL2-SRS'
$serviceDataDirectory = Join-Path $dataRoot $ServiceName
$executablePath = Join-Path $appDirectory 'IL2-SRS-Server.exe'

function Invoke-Icacls {
    # Windows PowerShell turns a native command's warnings into terminating errors under 'Stop' when output is
    # redirected; the exit code says whether it worked.
    $ErrorActionPreference = 'Continue'
    & icacls.exe @args | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "icacls $($args -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Robocopy {
    param([string] $From, [string] $To, [string[]] $Options)
    $ErrorActionPreference = 'Continue'
    & robocopy.exe $From $To @Options /R:2 /W:2 /NFL /NDL /NJH /NJS /NP | Out-Null
    # 0-7 mean copied or nothing to copy; 8 and above mean some files failed.
    if ($LASTEXITCODE -ge 8) {
        throw "Copying $From to $To failed (robocopy exit code $LASTEXITCODE)."
    }
}

function Test-AdministratorOwned {
    param([string] $Path)
    $owner = (Get-Acl $Path).GetOwner([Security.Principal.SecurityIdentifier]).Value
    return $owner -in @('S-1-5-32-544', 'S-1-5-18', [Security.Principal.WindowsIdentity]::GetCurrent().User.Value)
}

# The existing service, if this updates one: its data folder and account.
$existing = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'"
$serviceAccount = if ($existing -and $existing.StartName -and $existing.StartName -ne 'LocalSystem') { $existing.StartName }
if (-not $DataDirectory -and $existing) {
    if ($existing.PathName -match '--data-dir\s+(?:"([^"]+)"|(\S+))') {
        $DataDirectory = if ($Matches[1]) { $Matches[1] } else { $Matches[2] }
    }
    else {
        # Nothing is changed yet; carrying on would start the service with new, empty data.
        throw ("$ServiceName has no --data-dir, so its data is next to its program ($($existing.PathName)). " +
            "Run this script again with -DataDirectory set to that folder, so the data is copied.")
    }
}

if ($DataDirectory -and -not (Test-Path $DataDirectory)) {
    # Carrying on would start the service with new, empty data.
    throw "The data folder $DataDirectory does not exist."
}

# Any user may create a folder in ProgramData, and plant a plugin in it, before an administrator does.
if ((Test-Path $dataRoot) -and -not (Test-AdministratorOwned $dataRoot)) {
    throw "$dataRoot was not created by an administrator. Check what is in it and delete it, then run this script again."
}

if ($existing -and $existing.State -ne 'Stopped') {
    Stop-Service -Name $ServiceName
    Write-Host "Stopped $ServiceName"
}

# The program. Files the server writes next to itself when run from a console without --data-dir are left behind.
New-Item -ItemType Directory -Force -Path $appDirectory | Out-Null
if ([IO.Path]::GetFullPath($sourceDirectory).TrimEnd('\') -ne [IO.Path]::GetFullPath($appDirectory).TrimEnd('\')) {
    Invoke-Robocopy -From $sourceDirectory -To $appDirectory -Options @('/MIR',
        '/XF', 'srs.db', 'srs.db-*', 'srs-server.json', '*.log', 'serverlog*.txt', '/XD', 'keys', 'plugin-data')
    Write-Host "Copied the server to $appDirectory"
}

# The data: only Administrators, SYSTEM and the service's account. Everything below inherits that.
New-Item -ItemType Directory -Force -Path $serviceDataDirectory | Out-Null
$grants = @('*S-1-5-32-544:(OI)(CI)F', '*S-1-5-18:(OI)(CI)F')
if ($serviceAccount) {
    $grants += "${serviceAccount}:(OI)(CI)M"
}

Invoke-Icacls $dataRoot /setowner '*S-1-5-32-544' /T /C /Q
Invoke-Icacls $dataRoot /inheritance:r /grant:r @grants

$copiedFrom = $null
$skippedPlugins = @()
if ($DataDirectory -and (Test-Path $DataDirectory)) {
    $oldDataDirectory = (Resolve-Path $DataDirectory).Path
    if ($oldDataDirectory.TrimEnd('\') -ne $serviceDataDirectory.TrimEnd('\')) {
        if (Test-Path (Join-Path $serviceDataDirectory 'srs.db')) {
            Write-Warning "$serviceDataDirectory already holds a server's data, so $oldDataDirectory was not copied into it."
        }
        else {
            # Plugins are code the service runs as SYSTEM, and any Windows user could add them to the old folder.
            Invoke-Robocopy -From $oldDataDirectory -To $serviceDataDirectory -Options @('/E', '/XD',
                (Join-Path $oldDataDirectory 'plugins'))
            $copiedFrom = $oldDataDirectory
            $skippedPlugins = @(Get-ChildItem -Directory (Join-Path $oldDataDirectory 'plugins') -ErrorAction SilentlyContinue)
        }
    }
}

if (Get-ChildItem -Force $dataRoot) {
    # Explicit entries, e.g. on copied files or ones a user added, give way to the folder's.
    Invoke-Icacls (Join-Path $dataRoot '*') /reset /T /C /Q
}

# Service accounts do not see your shell's environment variables, so settings go in srs-server.json.
$configFile = Join-Path $serviceDataDirectory 'srs-server.json'
if (Test-Path $configFile) {
    # An update keeps the ports the server was set up with, for its firewall rules and the address shown at the end.
    $existingConfig = Get-Content $configFile -Raw | ConvertFrom-Json
    if ($existingConfig.SRS_SERVER_PORT -and -not $PSBoundParameters.ContainsKey('SrsPort')) {
        $SrsPort = [int]$existingConfig.SRS_SERVER_PORT
    }
    if ($existingConfig.urls -and -not $PSBoundParameters.ContainsKey('WebUrl')) {
        $WebUrl = $existingConfig.urls
    }
}

if (-not (Test-Path $configFile)) {
    if (-not $AdminPassword) {
        $secure = Read-Host -AsSecureString 'Admin password for the web UI (at least 8 characters)'
        $AdminPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
    }

    if ($AdminPassword.Length -lt 8) {
        throw 'The admin password must be at least 8 characters long.'
    }

    [ordered]@{
        SRS_ADMIN_PASSWORD = $AdminPassword
        SRS_SERVER_PORT    = "$SrsPort"
        urls               = $WebUrl
    } | ConvertTo-Json | Set-Content -Path $configFile -Encoding UTF8
    Write-Host "Wrote $configFile"
}
else {
    Write-Host "Keeping existing $configFile"
}

$binaryPath = "`"$executablePath`" --data-dir `"$serviceDataDirectory`""
if ($existing) {
    # Not sc.exe: Windows PowerShell passes quoted paths with spaces to native programs broken up.
    $result = Invoke-CimMethod -InputObject $existing -MethodName Change -Arguments @{
        PathName = $binaryPath; StartMode = 'Automatic' }
    $updated = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'"
    if ($result.ReturnValue -ne 0 -or $updated.PathName -ne $binaryPath) {
        throw "Could not update service $ServiceName (error $($result.ReturnValue)); it still runs $($updated.PathName)"
    }

    Write-Host "Updated service $ServiceName"
}
else {
    New-Service -Name $ServiceName -BinaryPathName $binaryPath -DisplayName "IL2-SRS Server ($ServiceName)" `
        -Description 'IL2 SimpleRadio Standalone server with web admin UI and API.' -StartupType Automatic | Out-Null
    Write-Host "Created service $ServiceName"
}

# Restart automatically after a crash: after 10 s, 30 s, then every 60 s.
sc.exe failure $ServiceName reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null

if (-not $NoFirewallRules) {
    # Rules from an older install allow the program where it used to be.
    $oldRules = @(Get-NetFirewallRule -DisplayName "$ServiceName *" -ErrorAction SilentlyContinue)
    if ($oldRules) {
        $oldRules | Get-NetFirewallApplicationFilter | Set-NetFirewallApplicationFilter -Program $executablePath
    }

    foreach ($protocol in 'TCP', 'UDP') {
        $ruleName = "$ServiceName $protocol $SrsPort"
        if (-not ($oldRules | Where-Object { $_.DisplayName -eq $ruleName })) {
            New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol $protocol `
                -LocalPort $SrsPort -Program $executablePath | Out-Null
            Write-Host "Added firewall rule $ruleName"
        }
    }
}

Start-Service -Name $ServiceName
Write-Host "Started $ServiceName. Admin UI: $WebUrl  Logs: $(Join-Path $serviceDataDirectory 'serverlog.txt')"
Write-Host "Program: $appDirectory  Data: $serviceDataDirectory (only Administrators can open it)"
if ($copiedFrom) {
    Write-Warning ("Copied the data from $copiedFrom. That folder still holds the admin password and the keys that " +
        "encrypt webhooks: delete it, and the old program folder, once the service works.")
}

if ($skippedPlugins) {
    Write-Warning ("Plugins in the old data folder were not copied, since other Windows users could have added or " +
        "changed them: $(($skippedPlugins | ForEach-Object Name) -join ', '). Copy the ones you installed, from " +
        "their original download, into $(Join-Path $serviceDataDirectory 'plugins') and restart the service.")
}
