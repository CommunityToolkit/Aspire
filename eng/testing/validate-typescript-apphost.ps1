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
        [string]$Step,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action,

        [Parameter(Mandatory = $false)]
        [System.Collections.Generic.List[object]]$Failures = $null
    )

    try {
        & $Action
    }
    catch {
        if ($null -ne $Failures) {
            $Failures.Add([pscustomobject]@{
                Step = $Step
                ErrorRecord = $_
            })
        }
    }
}

function Get-CleanupFailureMessage {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[object]]$Failures
    )

    $lines = @("Cleanup encountered the following failure(s):")
    foreach ($failure in $Failures) {
        $lines += " - $($failure.Step): $($failure.ErrorRecord.Exception.Message)"
    }

    return [string]::Join([Environment]::NewLine, $lines)
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
$cleanupFailures = [System.Collections.Generic.List[object]]::new()

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
            Invoke-CleanupStep -Step "Restore working directory" -Action {
                Pop-Location
            } -Failures $cleanupFailures
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
    finally {
        Invoke-CleanupStep -Step "Restore Aspire config" -Action {
            if ($null -ne $originalConfig) {
                Set-Content -Path $configPath -Value $originalConfig -NoNewline
            }
        } -Failures $cleanupFailures

        Invoke-CleanupStep -Step "Remove generated nuget.config" -Action {
            if (Test-Path $nugetConfigPath) {
                Remove-Item $nugetConfigPath -Force
            }
        } -Failures $cleanupFailures

        Invoke-CleanupStep -Step "Remove temporary package source" -Action {
            if (Test-Path $localSource) {
                Remove-Item $localSource -Recurse -Force
            }
        } -Failures $cleanupFailures

        Invoke-CleanupStep -Step "Stop Aspire app host" -Action {
            if ($appStarted) {
                Invoke-ExternalCommand "aspire" @(
                    "stop",
                    "--apphost", $resolvedAppHostPath
                )
            }
        } -Failures $cleanupFailures
    }
}
catch {
    if ($cleanupFailures.Count -gt 0) {
        $Host.UI.WriteErrorLine((Get-CleanupFailureMessage -Failures $cleanupFailures))
    }

    throw
}

if ($cleanupFailures.Count -gt 0) {
    throw (Get-CleanupFailureMessage -Failures $cleanupFailures)
}
