#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory = $true)]
    [string]$AppHostPath,

    [Parameter(Mandatory = $true)]
    [string]$PackageProjectPath,

    [Parameter(Mandatory = $true)]
    [string]$PackageName,

    [Parameter(Mandatory = $true)]
    [string[]]$WaitForResources,

    [string[]]$RequiredCommands = @(),

    [string]$PackageVersion = "",

    [ValidateSet("healthy", "up", "down")]
    [string]$WaitStatus = "healthy",

    [int]$WaitTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        $joinedArguments = [string]::Join(" ", $Arguments)
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $joinedArguments"
    }
}

function Invoke-CleanupStep {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action,

        [System.Collections.Generic.List[string]]$Failures
    )

    try {
        & $Action
    }
    catch {
        $message = "Cleanup step '$Description' failed: $($_.Exception.Message)"

        if ($null -ne $Failures) {
            $Failures.Add($message)
            return
        }

        throw
    }
}

$resolvedAppHostPath = (Resolve-Path $AppHostPath).Path
$resolvedPackageProjectPath = (Resolve-Path $PackageProjectPath).Path
$appHostDirectory = Split-Path -Parent $resolvedAppHostPath
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
$configPath = Join-Path $appHostDirectory "aspire.config.json"
$nugetConfigPath = Join-Path $appHostDirectory "nuget.config"
$localSource = Join-Path ([System.IO.Path]::GetTempPath()) ("ct-polyglot-" + [Guid]::NewGuid().ToString("N"))
$originalConfig = $null
$appStarted = $false
$primaryFailure = $null
$cleanupFailures = [System.Collections.Generic.List[string]]::new()

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $versionPrefix = (& dotnet msbuild $resolvedPackageProjectPath -nologo -v:q -getProperty:VersionPrefix).Trim()
    if ([string]::IsNullOrWhiteSpace($versionPrefix)) {
        throw "Could not determine the evaluated VersionPrefix for $resolvedPackageProjectPath."
    }

    $PackageVersion = "$versionPrefix-polyglot.local"
}

if ($WaitForResources.Count -eq 1) {
    $splitOptions = [System.StringSplitOptions]::RemoveEmptyEntries -bor [System.StringSplitOptions]::TrimEntries
    $WaitForResources = $WaitForResources[0].Split(",", $splitOptions)
}

if ($RequiredCommands.Count -eq 1) {
    $splitOptions = [System.StringSplitOptions]::RemoveEmptyEntries -bor [System.StringSplitOptions]::TrimEntries
    $RequiredCommands = $RequiredCommands[0].Split(",", $splitOptions)
}

foreach ($commandName in $RequiredCommands) {
    if ($null -eq (Get-Command $commandName -ErrorAction SilentlyContinue)) {
        throw "Required command '$commandName' was not found on PATH."
    }
}

try {
    $originalConfig = Get-Content -Path $configPath -Raw
    New-Item -ItemType Directory -Path $localSource -Force | Out-Null

    Invoke-ExternalCommand "dotnet" @(
        "pack",
        $resolvedPackageProjectPath,
        "-c", "Debug",
        "-p:PackageVersion=$PackageVersion",
        "-o", $localSource
    )

    $config = $originalConfig | ConvertFrom-Json
    $config.packages.$PackageName = $PackageVersion
    $config | ConvertTo-Json -Depth 10 | Set-Content -Path $configPath -NoNewline

    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local-polyglot" value="$localSource" />
  </packageSources>
</configuration>
"@ | Set-Content -Path $nugetConfigPath -NoNewline

    Push-Location $appHostDirectory
    try {
        Invoke-ExternalCommand "npm" @("ci")
        Invoke-ExternalCommand "aspire" @(
            "restore",
            "--apphost", $resolvedAppHostPath,
            "--non-interactive"
        )
        Invoke-ExternalCommand "npx" @("tsc", "--noEmit")
    }
    finally {
        Pop-Location
    }

    Invoke-ExternalCommand "aspire" @(
        "start",
        "--apphost", $resolvedAppHostPath,
        "--isolated",
        "--format", "Json",
        "--non-interactive"
    )
    $appStarted = $true

    foreach ($resource in $WaitForResources) {
        Invoke-ExternalCommand "aspire" @(
            "wait",
            $resource,
            "--status", $WaitStatus,
            "--apphost", $resolvedAppHostPath,
            "--timeout", $WaitTimeoutSeconds
        )
    }

    Invoke-ExternalCommand "aspire" @(
        "describe",
        "--apphost", $resolvedAppHostPath,
        "--format", "Json"
    )
}
catch {
    $primaryFailure = $_
}
finally {
    Invoke-CleanupStep -Description "restore config" -Action {
        if ($null -ne $originalConfig) {
            Set-Content -Path $configPath -Value $originalConfig -NoNewline
        }
    } -Failures $cleanupFailures

    Invoke-CleanupStep -Description "remove generated nuget.config" -Action {
        if (Test-Path $nugetConfigPath) {
            Remove-Item $nugetConfigPath -Force
        }
    } -Failures $cleanupFailures

    Invoke-CleanupStep -Description "remove local package source" -Action {
        if (Test-Path $localSource) {
            Remove-Item $localSource -Recurse -Force
        }
    } -Failures $cleanupFailures

    Invoke-CleanupStep -Description "stop apphost" -Action {
        if ($appStarted) {
            Invoke-ExternalCommand "aspire" @(
                "stop",
                "--apphost", $resolvedAppHostPath
            )
        }
    } -Failures $cleanupFailures
}

if ($cleanupFailures.Count -gt 0) {
    $cleanupFailureMessage = "Cleanup failed:" + [Environment]::NewLine + [string]::Join([Environment]::NewLine, $cleanupFailures)

    if ($null -ne $primaryFailure) {
        [Console]::Error.WriteLine($cleanupFailureMessage)
    }
    else {
        throw $cleanupFailureMessage
    }
}

if ($null -ne $primaryFailure) {
    throw $primaryFailure
}
