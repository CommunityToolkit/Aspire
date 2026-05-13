#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Download and install NuGet packages from a PR's build artifacts for local testing.
.EXAMPLE
    ./dogfood-pr.ps1 1129
    ./dogfood-pr.ps1 1129 -WorkflowRunId 12345678
#>

param(
    [Parameter(Position = 0, Mandatory = $true, HelpMessage = "Pull request number")]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$PRNumber,

    [Parameter(HelpMessage = "Workflow run ID (skip PR resolution)")]
    [ValidateRange(1, [long]::MaxValue)]
    [long]$WorkflowRunId = 0,

    [Parameter(HelpMessage = "Install prefix directory")]
    [string]$InstallPath = "",

    [Parameter(HelpMessage = "Verbose output")]
    [switch]$VerboseOutput,

    [Parameter(HelpMessage = "Keep temp download directory")]
    [switch]$KeepArchive,

    [Parameter(HelpMessage = "Show help")]
    [switch]$Help
)

$ErrorActionPreference = "Stop"

$Script:Repo = "CommunityToolkit/Aspire"
$Script:CIWorkflow = "dotnet-ci.yml"
$Script:ArtifactName = "nuget-packages"

# --- Output Helpers ---

function Write-Link {
    param([string]$Url, [string]$Label)
    return "$([char]27)]8;;${Url}$([char]27)\${Label}$([char]27)]8;;$([char]27)\"
}

function Get-DisplayPath {
    param([string]$Path)
    if ($Path.StartsWith($HOME)) {
        return "~" + $Path.Substring($HOME.Length)
    }
    return $Path
}

function Invoke-WithSpinner {
    param([string]$Message, [string]$Command, [string[]]$Arguments)
    $psi = [System.Diagnostics.ProcessStartInfo]::new($Command)
    foreach ($arg in $Arguments) { $psi.ArgumentList.Add($arg) }
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $proc = [System.Diagnostics.Process]::Start($psi)
    $chars = [char[]]'⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'
    $i = 0
    while (-not $proc.HasExited) {
        Write-Host "`r$($chars[$i++ % $chars.Length]) $Message" -NoNewline
        Start-Sleep -Milliseconds 100
    }
    $proc.WaitForExit()
    Write-Host "`r`e[K" -NoNewline
    return $proc.ExitCode
}

function Show-Help {
    @"
Usage: dogfood-pr.ps1 [-PRNumber] <int> [OPTIONS]

Download and install NuGet packages from a PR's build artifacts for local testing.

OPTIONS:
    -WorkflowRunId ID           Workflow run ID (skip PR resolution)
    -InstallPath PATH           Install prefix (default: `$HOME/.aspire)
    -VerboseOutput              Verbose output
    -KeepArchive                Keep temp download directory
    -Help                       Show this help

EXAMPLES:
    ./dogfood-pr.ps1 1129
    ./dogfood-pr.ps1 1129 -WorkflowRunId 12345678
    ./dogfood-pr.ps1 1129 -InstallPath ./local-packages

REQUIREMENTS:
    - GitHub CLI (gh) authenticated: https://cli.github.com
"@
}

# --- Core Functions ---

function Test-Prerequisites {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Host "✗ GitHub CLI (gh) is required. Install from: https://cli.github.com" -ForegroundColor Red
        exit 1
    }

    & gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ GitHub CLI is not authenticated. Run: gh auth login" -ForegroundColor Red
        exit 1
    }
}

function Resolve-PullRequest {
    $prUrl = "https://github.com/$($Script:Repo)/pull/$PRNumber"
    try {
        $prJson = & gh api "repos/$($Script:Repo)/pulls/$PRNumber" --jq '{sha: .head.sha, title: .title, author: .user.login}' 2>$null | ConvertFrom-Json
    } catch {
        Write-Host "✗ PR #$PRNumber not found in $($Script:Repo)" -ForegroundColor Red
        exit 1
    }

    $script:HeadSha = $prJson.sha
    $prTitle = $prJson.title
    $prAuthor = $prJson.author

    $authorDisplay = Write-Link "https://github.com/$prAuthor" "@$prAuthor"

    Write-Host ""
    $cols = if ($Host.UI.RawUI.WindowSize.Width) { $Host.UI.RawUI.WindowSize.Width } else { 80 }
    $prefix = "PR #$PRNumber — "
    $suffix = " by @$prAuthor"
    $maxTitle = $cols - $prefix.Length - $suffix.Length - 2
    if ($prTitle.Length -gt $maxTitle -and $maxTitle -gt 3) {
        $prTitle = $prTitle.Substring(0, $maxTitle - 1) + "…"
    }
    Write-Host "$(Write-Link $prUrl "PR #$PRNumber") — $prTitle by $authorDisplay"
    if ($VerboseOutput) { Write-Host "  Head commit: $(Write-Link "https://github.com/$($Script:Repo)/commit/$($script:HeadSha)" $script:HeadSha.Substring(0,7))" -ForegroundColor DarkGray }
    Write-Host ""
}

function Find-WorkflowRun {
    if ($WorkflowRunId -gt 0) {
        $script:RunId = $WorkflowRunId
        return
    }

    $script:RunId = & gh api "repos/$($Script:Repo)/actions/workflows/$($Script:CIWorkflow)/runs?event=pull_request&head_sha=$($script:HeadSha)" `
        --jq '.workflow_runs | sort_by(.created_at, .updated_at) | reverse | .[0].id' 2>$null

    if (-not $script:RunId -or $script:RunId -eq "null") {
        Write-Host "✗ No workflow run found for PR #$PRNumber (SHA: $($script:HeadSha))" -ForegroundColor Red
        Write-Host "  Check: https://github.com/$($Script:Repo)/actions/workflows/$($Script:CIWorkflow)"
        exit 1
    }

    if ($VerboseOutput) { Write-Host "  Workflow run: $(Write-Link "https://github.com/$($Script:Repo)/actions/runs/$($script:RunId)" $script:RunId)" -ForegroundColor DarkGray }
}

function Save-Artifacts {
    $script:DownloadDir = Join-Path $tempDir "nuget-packages"
    $runUrl = "https://github.com/$($Script:Repo)/actions/runs/$($script:RunId)"

    $exitCode = Invoke-WithSpinner "📦 Downloading packages..." "gh" @("run", "download", $script:RunId, "-R", $Script:Repo, "--name", $Script:ArtifactName, "-D", $script:DownloadDir)
    if ($exitCode -ne 0) {
        Write-Host "✗ Failed to download artifacts — build may still be in progress or artifacts may have expired" -ForegroundColor Red
        Write-Host "   $(Write-Link $runUrl "View workflow run")" -ForegroundColor DarkGray
        exit 1
    }

    $script:Packages = Get-ChildItem -Path $script:DownloadDir -Filter "*.nupkg" -Recurse
    if ($script:Packages.Count -eq 0) {
        Write-Host "✗ No NuGet packages found in downloaded artifacts" -ForegroundColor Red
        exit 1
    }

    $totalSize = ($script:Packages | Measure-Object -Property Length -Sum).Sum
    $sizeDisplay = if ($totalSize -ge 1MB) { "{0:N1} MB" -f ($totalSize / 1MB) } elseif ($totalSize -ge 1KB) { "{0:N0} KB" -f ($totalSize / 1KB) } else { "$totalSize B" }
    Write-Host "📦 Downloaded $($script:Packages.Count) packages ($sizeDisplay)"
}

function Install-Packages {
    New-Item -ItemType Directory -Path $hiveDir -Force | Out-Null
    $script:Packages | Copy-Item -Destination $hiveDir -Force
    Write-Host "📂 Installed to $(Get-DisplayPath $hiveDir)"

    $script:Version = ""
    $firstPkg = $script:Packages | Select-Object -First 1
    if ($firstPkg) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($firstPkg.FullName)
        try {
            $nuspec = $zip.Entries | Where-Object { $_.Name -like "*.nuspec" } | Select-Object -First 1
            if ($nuspec) {
                $reader = [System.IO.StreamReader]::new($nuspec.Open())
                $xml = [xml]$reader.ReadToEnd()
                $reader.Close()
                $script:Version = $xml.package.metadata.version
            }
        } finally {
            $zip.Dispose()
        }
    }
}

function Register-NuGetSource {
    $script:NuGetConfig = $null

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host "⚠ dotnet CLI not found — configure NuGet source manually:" -ForegroundColor Yellow
        Write-Host "   dotnet nuget add source `"$hiveDir`" --name `"$sourceName`"" -ForegroundColor DarkGray
        return
    }

    $existingSources = & dotnet nuget list source 2>$null
    if ($existingSources -match [regex]::Escape($sourceName)) {
        & dotnet nuget update source $sourceName --source $hiveDir 2>&1 | Out-Null
    } else {
        & dotnet nuget add source $hiveDir --name $sourceName 2>&1 | Out-Null
    }

    $script:NuGetConfig = (& dotnet nuget config paths 2>$null | Select-Object -First 1)
    if ($script:NuGetConfig) {
        Write-Host "🔧 Configured source $sourceName in $(Get-DisplayPath $script:NuGetConfig)"
    } else {
        Write-Host "🔧 Configured source $sourceName"
    }
}

function Write-Summary {
    Write-Host ""
    if ($script:Version) {
        Write-Host "🐶 Ready — use version $($script:Version) to test these changes" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "To undo:" -ForegroundColor DarkGray
    $hivePath = Join-Path $InstallPath "hives" "community-toolkit-pr-$PRNumber"
    if ($script:NuGetConfig) {
        Write-Host "  dotnet nuget remove source `"$sourceName`" --configfile `"$($script:NuGetConfig)`" | Out-Null; Remove-Item -Recurse -Force `"$hivePath`"" -ForegroundColor DarkGray
    } else {
        Write-Host "  dotnet nuget remove source `"$sourceName`" | Out-Null; Remove-Item -Recurse -Force `"$hivePath`"" -ForegroundColor DarkGray
    }

    if ($KeepArchive) {
        Write-Host ""
        Write-Host "Archive kept at: $tempDir" -ForegroundColor DarkGray
    }

    if ($VerboseOutput) {
        Write-Host ""
        Write-Host "Packages:" -ForegroundColor DarkGray
        Get-ChildItem -Path $hiveDir -Filter "*.nupkg" | Sort-Object Name | ForEach-Object {
            Write-Host "  $($_.Name)" -ForegroundColor DarkGray
        }
    }
}

# --- Entry Point ---

if ($Help) {
    Show-Help
    exit 0
}

Test-Prerequisites

if (-not $InstallPath) {
    $InstallPath = Join-Path $HOME ".aspire"
}
$hiveDir = Join-Path $InstallPath "hives" "community-toolkit-pr-$PRNumber" "packages"
$sourceName = "CommunityToolkit-PR-$PRNumber"

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "dogfood-pr-$([System.Guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    Resolve-PullRequest
    Find-WorkflowRun
    Save-Artifacts
    Install-Packages
    Register-NuGetSource
    Write-Summary
} finally {
    if (-not $KeepArchive -and (Test-Path $tempDir)) {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
