<#
.SYNOPSIS
    Runs every gate CI runs, locally, in one command.

.DESCRIPTION
    The point is that a reviewer can prove the repository's claims without reading the workflow file.
    Each step prints its own verdict and the script exits non-zero on the first failure.
#>

[CmdletBinding()]
param(
    # Skips the container steps when Docker is not available.
    [switch]$SkipDocker
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

$steps = [System.Collections.Generic.List[object]]::new()

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)

    Write-Host ""
    Write-Host "→ $Name" -ForegroundColor Cyan

    $started = Get-Date
    try {
        & $Action
        if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
            throw "exit code $LASTEXITCODE"
        }
        $steps.Add([pscustomobject]@{ Step = $Name; Result = 'PASS'; Seconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 1) })
    }
    catch {
        $steps.Add([pscustomobject]@{ Step = $Name; Result = 'FAIL'; Seconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 1) })
        $steps | Format-Table -AutoSize
        Pop-Location
        Write-Error "$Name failed: $_"
        exit 1
    }
}

Invoke-Step 'Restore' { dotnet restore WeatherApp.slnx }
Invoke-Step 'Format' { dotnet format WeatherApp.slnx --verify-no-changes --no-restore }
Invoke-Step 'Build (warnings are errors)' { dotnet build WeatherApp.slnx --no-restore --configuration Release }
Invoke-Step 'Tests' { dotnet test WeatherApp.slnx --no-build --configuration Release }

Invoke-Step 'Vulnerable packages' {
    $audit = dotnet list WeatherApp.slnx package --vulnerable --include-transitive | Out-String
    if ($audit -match 'has the following vulnerable packages') {
        throw 'vulnerable packages found'
    }
}

Invoke-Step 'No credential in the working tree' {
    # The provider credential is a 31-character hex string; documentation is excluded because it discusses
    # the credential without containing one.
    git grep -nIE "[0-9a-f]{31}" -- ':!*.md' ':!docs/**' | Out-String -OutVariable found | Out-Null
    if ($found.Trim()) {
        throw "possible credential found:`n$found"
    }
}

if (-not $SkipDocker) {
    Invoke-Step 'Container image' { docker build -t weather-app:verify . }

    Invoke-Step 'Image runs as a non-root user' {
        $user = docker image inspect weather-app:verify --format '{{.Config.User}}'
        if (-not $user -or $user -eq 'root' -or $user -eq '0') {
            throw "image user is '$user'"
        }
    }

    Invoke-Step 'No credential baked into the image' {
        $env = docker image inspect weather-app:verify --format '{{json .Config.Env}}'
        if ($env -match '(?i)credential=.') {
            throw 'credential present in image environment'
        }
    }
}

Write-Host ""
$steps | Format-Table -AutoSize
Write-Host "All gates passed." -ForegroundColor Green
Pop-Location
