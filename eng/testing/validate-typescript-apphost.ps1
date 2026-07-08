#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory = $true)]
    [string]$AppHostPath,

    [string[]]$WaitForResources = @(),

    [string[]]$RequiredCommands = @(),

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

function Invoke-ExternalCommandWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [int]$MaxAttempts = 3,

        [int]$RetryDelaySeconds = 3
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            Invoke-ExternalCommand $FilePath $Arguments
            return
        }
        catch {
            if ($attempt -ge $MaxAttempts) {
                throw
            }

            $joinedArguments = [string]::Join(" ", $Arguments)
            Write-Warning "Attempt $attempt of $MaxAttempts failed for '$FilePath $joinedArguments': $($_.Exception.Message). Retrying in $RetryDelaySeconds second(s)..."
            Start-Sleep -Seconds $RetryDelaySeconds
        }
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

$resolvedAppHostPath = (Resolve-Path $AppHostPath).Path
$appHostDirectory = Split-Path -Parent $resolvedAppHostPath
$appStarted = $false
$primaryError = $null
$cleanupFailures = [System.Collections.Generic.List[string]]::new()

if ($WaitForResources.Count -eq 1 -and -not [string]::IsNullOrWhiteSpace($WaitForResources[0])) {
    $splitOptions = [System.StringSplitOptions]::RemoveEmptyEntries -bor [System.StringSplitOptions]::TrimEntries
    $WaitForResources = $WaitForResources[0].Split(",", $splitOptions)
}

if ($RequiredCommands.Count -eq 1) {
    $splitOptions = [System.StringSplitOptions]::RemoveEmptyEntries -bor [System.StringSplitOptions]::TrimEntries
    $RequiredCommands = $RequiredCommands[0].Split(",", $splitOptions)
}

if ($RequiredCommands -contains "yarn") {
    if ($null -ne (Get-Command "corepack" -ErrorAction SilentlyContinue)) {
        Invoke-ExternalCommandWithRetry "corepack" @("enable")
        Invoke-ExternalCommandWithRetry "corepack" @("prepare", "yarn@stable", "--activate")
    }
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
