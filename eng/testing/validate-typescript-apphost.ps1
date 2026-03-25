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

    [string]$PackageVersion = "",

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

$resolvedAppHostPath = (Resolve-Path $AppHostPath).Path
$resolvedPackageProjectPath = (Resolve-Path $PackageProjectPath).Path
$appHostDirectory = Split-Path -Parent $resolvedAppHostPath
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\\..")).Path
$settingsPath = Join-Path $appHostDirectory ".aspire\\settings.json"
$nugetConfigPath = Join-Path $appHostDirectory "nuget.config"
$localSource = Join-Path ([System.IO.Path]::GetTempPath()) ("ct-polyglot-" + [Guid]::NewGuid().ToString("N"))
$originalSettings = $null
$appStarted = $false

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

try {
    $originalSettings = Get-Content -Path $settingsPath -Raw
    New-Item -ItemType Directory -Path $localSource -Force | Out-Null

    Invoke-ExternalCommand "dotnet" @(
        "pack",
        $resolvedPackageProjectPath,
        "-c", "Debug",
        "-p:PackageVersion=$PackageVersion",
        "-o", $localSource
    )

    $settings = $originalSettings | ConvertFrom-Json
    $settings.packages.$PackageName = $PackageVersion
    $settings | ConvertTo-Json -Depth 10 | Set-Content -Path $settingsPath -NoNewline

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
    if ($null -ne $originalSettings) {
        Set-Content -Path $settingsPath -Value $originalSettings -NoNewline
    }

    if (Test-Path $nugetConfigPath) {
        Remove-Item $nugetConfigPath -Force
    }

    if (Test-Path $localSource) {
        Remove-Item $localSource -Recurse -Force
    }

    if ($appStarted) {
        Invoke-ExternalCommand "aspire" @(
            "stop",
            "--apphost", $resolvedAppHostPath
        )
    }
}
