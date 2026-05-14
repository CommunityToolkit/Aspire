#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory = $true)]
    [string]$AppHostPath,

    [Parameter(Mandatory = $true)]
    [string]$PackageProjectPath,

    [Parameter(Mandatory = $true)]
    [string]$PackageName,

    [string[]]$WaitForResources = @(),

    [string[]]$RequiredCommands = @(),

    [string]$PackageVersion = "",

    [ValidateSet("healthy", "up", "down")]
    [string]$WaitStatus = "healthy",

    [int]$WaitTimeoutSeconds = 180,

    [string[]]$Secrets = @()
)

$ErrorActionPreference = "Stop"

function Resolve-ExternalCommandPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    if ([System.IO.Path]::IsPathRooted($FilePath) -or
        $FilePath.Contains([System.IO.Path]::DirectorySeparatorChar) -or
        $FilePath.Contains([System.IO.Path]::AltDirectorySeparatorChar)) {
        return $FilePath
    }

    $commandCandidates = @(Get-Command $FilePath -All -ErrorAction Stop)
    $preferredCandidate = $commandCandidates |
        Where-Object { $_.CommandType -eq [System.Management.Automation.CommandTypes]::Application } |
        Select-Object -First 1

    if ($null -ne $preferredCandidate) {
        return $preferredCandidate.Source
    }

    return $commandCandidates[0].Source
}

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $resolvedFilePath = Resolve-ExternalCommandPath $FilePath

    & $resolvedFilePath @Arguments
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

        [System.Collections.Generic.List[string]]$Failures = $null
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

        throw $message
    }
}

function Remove-PathWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [switch]$Recurse
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $maxAttempts = 6
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            if ($Recurse) {
                Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            }
            else {
                Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
            }

            return
        }
        catch {
            if ($attempt -eq $maxAttempts) {
                throw
            }

            Start-Sleep -Milliseconds (250 * $attempt)
        }
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
$primaryError = $null
$cleanupFailures = [System.Collections.Generic.List[string]]::new()

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $versionPrefix = (& dotnet msbuild $resolvedPackageProjectPath -nologo -v:q -getProperty:VersionPrefix).Trim()
    if ([string]::IsNullOrWhiteSpace($versionPrefix)) {
        throw "Could not determine the evaluated VersionPrefix for $resolvedPackageProjectPath."
    }

    $PackageVersion = "$versionPrefix-polyglot.local"
}

# Discover local CommunityToolkit project references that also need packing
$localDependencies = @()
$projRefJson = (& dotnet msbuild $resolvedPackageProjectPath -nologo -v:q -getItem:ProjectReference) | Out-String
$projRefData = $projRefJson | ConvertFrom-Json
$projRefs = @($projRefData.Items.ProjectReference)
foreach ($ref in $projRefs) {
    if ($ref.Filename -like "CommunityToolkit.*") {
        $localDependencies += @{
            Name = $ref.Filename
            FullPath = $ref.FullPath
        }
    }
}

if ($localDependencies.Count -gt 0) {
    $depNames = ($localDependencies | ForEach-Object { $_.Name }) -join ", "
    Write-Host "Discovered local dependencies to pack: $depNames"
}

if ($WaitForResources.Count -eq 1 -and -not [string]::IsNullOrWhiteSpace($WaitForResources[0])) {
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

if ($Secrets.Count -eq 1 -and -not [string]::IsNullOrWhiteSpace($Secrets[0])) {
    $splitOptions = [System.StringSplitOptions]::RemoveEmptyEntries -bor [System.StringSplitOptions]::TrimEntries
    $Secrets = $Secrets[0].Split(",", $splitOptions)
}

$parsedSecrets = [System.Collections.Generic.List[string[]]]::new()
foreach ($secret in $Secrets) {
    if ([string]::IsNullOrWhiteSpace($secret)) {
        continue
    }

    $eqIndex = $secret.IndexOf('=')
    if ($eqIndex -le 0) {
        throw "Invalid secret format '$secret'. Expected 'key=value'."
    }

    $key = $secret.Substring(0, $eqIndex)
    $value = $secret.Substring($eqIndex + 1)
    $parsedSecrets.Add(@($key, $value))
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

    foreach ($dep in $localDependencies) {
        Invoke-ExternalCommand "dotnet" @(
            "pack",
            $dep.FullPath,
            "-c", "Debug",
            "-p:PackageVersion=$PackageVersion",
            "-o", $localSource
        )
    }

    $config = $originalConfig | ConvertFrom-Json -AsHashtable
    if ($null -eq $config["packages"]) {
        $config["packages"] = [ordered]@{}
    }

    $config["packages"][$PackageName] = $PackageVersion
    foreach ($dep in $localDependencies) {
        $config["packages"][$dep.Name] = $PackageVersion
    }
    $config | ConvertTo-Json -Depth 10 | Set-Content -Path $configPath -NoNewline

    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local-polyglot" value="$localSource" />
  </packageSources>

  <packageSourceMapping>
    <packageSource key="local-polyglot">
      <package pattern="CommunityToolkit.Aspire.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -Path $nugetConfigPath -NoNewline

    Push-Location $appHostDirectory
    try {
        Invoke-ExternalCommand "npm" @("ci")
        Invoke-ExternalCommand "aspire" @(
            "restore",
            "--apphost", $resolvedAppHostPath,
            "--non-interactive",
            "--log-level", "debug"
        )
        Invoke-ExternalCommand "npx" @("tsc", "--noEmit")
    }
    finally {
        Pop-Location
    }

    foreach ($secretPair in $parsedSecrets) {
        Invoke-ExternalCommand "aspire" @(
            "secret", "set",
            $secretPair[0], $secretPair[1],
            "--apphost", $resolvedAppHostPath,
            "--non-interactive",
            "--log-level", "debug"
        )
    }

    Push-Location $appHostDirectory
    try {
        Invoke-ExternalCommand "aspire" @(
            "start",
            "--apphost", $resolvedAppHostPath,
            "--isolated",
            "--format", "Json",
            "--non-interactive",
            "--log-level", "debug"
        )
        $appStarted = $true

        foreach ($resource in $WaitForResources) {
            Invoke-ExternalCommand "aspire" @(
                "wait",
                $resource,
                "--status", $WaitStatus,
                "--apphost", $resolvedAppHostPath,
                "--timeout", $WaitTimeoutSeconds,
                "--log-level", "debug"
            )
        }

        Invoke-ExternalCommand "aspire" @(
            "describe",
            "--apphost", $resolvedAppHostPath,
            "--format", "Json",
            "--log-level", "debug"
        )
    }
    finally {
        Pop-Location
    }
}
catch {
    $primaryError = $_
}
finally {
    Invoke-CleanupStep -Description "stop Aspire app" -Action {
        if ($appStarted) {
            Push-Location $appHostDirectory
            try {
                Invoke-ExternalCommand "aspire" @(
                    "stop",
                    "--apphost", $resolvedAppHostPath
                )
            }
            finally {
                Pop-Location
            }

            Start-Sleep -Milliseconds 500
        }
    } -Failures $cleanupFailures

    Invoke-CleanupStep -Description "remove secrets" -Action {
        foreach ($secretPair in $parsedSecrets) {
            Invoke-ExternalCommand "aspire" @(
                "secret", "delete",
                $secretPair[0],
                "--apphost", $resolvedAppHostPath,
                "--non-interactive"
            )
        }
    } -Failures $cleanupFailures

    Invoke-CleanupStep -Description "restore Aspire config" -Action {
        if ($null -ne $originalConfig) {
            Set-Content -Path $configPath -Value $originalConfig -NoNewline
        }
    } -Failures $cleanupFailures

    Invoke-CleanupStep -Description "remove generated nuget.config" -Action {
        Remove-PathWithRetry -Path $nugetConfigPath
    } -Failures $cleanupFailures

    Invoke-CleanupStep -Description "remove local package source" -Action {
        Remove-PathWithRetry -Path $localSource -Recurse
    } -Failures $cleanupFailures
}

if ($cleanupFailures.Count -gt 0) {
    $cleanupFailureMessage = "Cleanup failures:${Environment.NewLine}$($cleanupFailures -join [Environment]::NewLine)"
    if ($null -ne $primaryError) {
        Write-Error -Message $cleanupFailureMessage -ErrorAction Continue
    }
    else {
        throw $cleanupFailureMessage
    }
}

if ($null -ne $primaryError) {
    throw $primaryError
}
