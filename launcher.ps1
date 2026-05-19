Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem

$ErrorActionPreference = "Stop"

$ConfigPath = Join-Path $PSScriptRoot "launcher.config.json"
$StateDir = Join-Path $env:LOCALAPPDATA "StarCraft2PatchLauncher"
$StatePath = Join-Path $StateDir "state.json"

function Read-LauncherConfig {
    if (!(Test-Path $ConfigPath)) {
        throw "Missing config file: $ConfigPath"
    }

    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    foreach ($key in @("app_name", "version", "packages")) {
        if (!$config.$key) {
            throw "Missing config value: $key"
        }
    }

    if ($null -eq $config.starcraft2_paths) {
        $config | Add-Member -NotePropertyName "starcraft2_paths" -NotePropertyValue @()
    }
    if ($null -eq $config.launch_after_install) {
        $config | Add-Member -NotePropertyName "launch_after_install" -NotePropertyValue $true
    }

    return $config
}

function Convert-GoogleDriveUrl($url) {
    if ($url -match "drive\.google\.com/file/d/([^/]+)") {
        return "https://drive.google.com/uc?export=download&id=$($Matches[1])"
    }

    return $url
}

function Get-StarCraft2Path($config) {
    $candidates = New-Object System.Collections.Generic.List[string]

    foreach ($path in @($config.starcraft2_paths)) {
        if ($path) {
            $candidates.Add([Environment]::ExpandEnvironmentVariables($path))
        }
    }

    $registryPaths = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\StarCraft II",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\StarCraft II",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\StarCraft II"
    )

    foreach ($registryPath in $registryPaths) {
        if (Test-Path $registryPath) {
            $item = Get-ItemProperty $registryPath
            foreach ($value in @($item.InstallLocation, $item.DisplayIcon)) {
                if ($value) {
                    $clean = $value -replace '"', ""
                    if ([IO.Path]::GetExtension($clean)) {
                        $clean = Split-Path $clean -Parent
                    }
                    $candidates.Add($clean)
                }
            }
        }
    }

    foreach ($path in @(
        "C:\Program Files (x86)\StarCraft II",
        "C:\Program Files\StarCraft II",
        "D:\Program Files (x86)\StarCraft II",
        "D:\Program Files\StarCraft II",
        "$env:USERPROFILE\Documents\StarCraft II"
    )) {
        $candidates.Add($path)
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (!$candidate) {
            continue
        }

        $exePath = Join-Path $candidate "StarCraft II.exe"
        $supportPath = Join-Path $candidate "Support64\SC2Switcher_x64.exe"
        if ((Test-Path $candidate) -and ((Test-Path $exePath) -or (Test-Path $supportPath))) {
            return (Resolve-Path $candidate).Path
        }
    }

    $dialog = New-Object Windows.Forms.FolderBrowserDialog
    $dialog.Description = "Select the StarCraft II installation folder."
    $dialog.ShowNewFolderButton = $false
    if ($dialog.ShowDialog() -eq [Windows.Forms.DialogResult]::OK) {
        return $dialog.SelectedPath
    }

    throw "StarCraft II installation folder was not selected."
}

function Read-State {
    if (!(Test-Path $StatePath)) {
        return @{}
    }

    try {
        return Get-Content $StatePath -Raw | ConvertFrom-Json
    }
    catch {
        return @{}
    }
}

function Test-Installed($config) {
    $state = Read-State
    return $state.version -eq $config.version
}

function Save-State($config, $installPath) {
    New-Item -ItemType Directory -Force -Path $StateDir | Out-Null
    @{
        version = $config.version
        install_path = $installPath
        installed_at = (Get-Date).ToString("s")
    } | ConvertTo-Json | Set-Content -Encoding UTF8 $StatePath
}

function Get-DownloadTarget($package) {
    if ($package.file_name) {
        $fileName = $package.file_name
    }
    else {
        $uri = [Uri](Convert-GoogleDriveUrl $package.url)
        $fileName = [IO.Path]::GetFileName($uri.AbsolutePath)
        if (!$fileName -or $fileName -eq "uc") {
            $fileName = "$($package.name).zip"
        }
    }

    return Join-Path ([IO.Path]::GetTempPath()) $fileName
}

function Save-ResponseBody($response, $target, $progressCallback, $basePercent, $rangePercent) {
    $total = $response.ContentLength
    $inputStream = $response.GetResponseStream()
    $outputStream = [IO.File]::Create($target)

    try {
        $buffer = New-Object byte[] (256KB)
        $downloaded = 0
        while (($read = $inputStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $outputStream.Write($buffer, 0, $read)
            $downloaded += $read
            if ($total -gt 0) {
                $percent = $basePercent + [int](($downloaded / $total) * $rangePercent)
                & $progressCallback $percent
            }
        }
    }
    finally {
        $outputStream.Dispose()
        $inputStream.Dispose()
    }
}

function Invoke-Download($url, $target, $progressCallback, $basePercent, $rangePercent) {
    $request = [System.Net.HttpWebRequest]::Create($url)
    $request.UserAgent = "Mozilla/5.0 StarCraft2PatchLauncher/1.0"
    $request.AllowAutoRedirect = $true

    $response = $request.GetResponse()
    try {
        Save-ResponseBody $response $target $progressCallback $basePercent $rangePercent
    }
    finally {
        $response.Dispose()
    }
}

function Download-Package($package, $progressCallback, $basePercent, $rangePercent) {
    $target = Get-DownloadTarget $package
    $url = Convert-GoogleDriveUrl $package.url

    Invoke-Download $url $target $progressCallback $basePercent $rangePercent

    $previewBuffer = New-Object byte[] 8192
    $stream = [IO.File]::OpenRead($target)
    try {
        $previewLength = $stream.Read($previewBuffer, 0, $previewBuffer.Length)
    }
    finally {
        $stream.Dispose()
    }
    $preview = [Text.Encoding]::UTF8.GetString($previewBuffer, 0, $previewLength)

    $isHtml = $preview -match "<html|<!doctype html"
    $hasConfirmToken = $preview -match 'confirm=([0-9A-Za-z_]+)'
    $confirmToken = $Matches[1]
    $hasFileId = $url -match 'id=([^&]+)'
    $fileId = $Matches[1]

    if ($isHtml -and $hasConfirmToken -and $hasFileId) {
        if ($preview -match 'uuid=([^&"''<>]+)') {
            $uuid = $Matches[1]
            $confirmedUrl = "https://drive.google.com/uc?export=download&id=$fileId&confirm=$confirmToken&uuid=$uuid"
        }
        else {
            $confirmedUrl = "https://drive.google.com/uc?export=download&id=$fileId&confirm=$confirmToken"
        }
        Invoke-Download $confirmedUrl $target $progressCallback $basePercent $rangePercent
    }

    if ($package.sha256) {
        $actual = (Get-FileHash -Algorithm SHA256 $target).Hash.ToLowerInvariant()
        if ($actual -ne $package.sha256.ToLowerInvariant()) {
            throw "$($package.name) failed SHA-256 verification."
        }
    }

    return $target
}

function Expand-Package($package, $archivePath, $installPath) {
    $extractRoot = $installPath
    if ($package.target_subdir) {
        $extractRoot = Join-Path $installPath $package.target_subdir
    }

    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null

    $extension = [IO.Path]::GetExtension($archivePath).ToLowerInvariant()
    if ($extension -eq ".zip") {
        $tempExtract = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
        New-Item -ItemType Directory -Force -Path $tempExtract | Out-Null
        try {
            [System.IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $tempExtract)
            Copy-Item -Recurse -Force (Join-Path $tempExtract "*") $extractRoot
        }
        finally {
            Remove-Item -Recurse -Force $tempExtract -ErrorAction SilentlyContinue
        }
        return
    }

    if ($extension -eq ".7z" -or $extension -eq ".rar") {
        $sevenZip = Get-ArchiveTool "7z.exe"

        if ($sevenZip) {
            $process = Start-Process -FilePath $sevenZip -ArgumentList @("x", "-y", "-o$extractRoot", $archivePath) -Wait -PassThru
            if ($process.ExitCode -ne 0) {
                throw "7-Zip extraction failed for $($package.name)."
            }
            return
        }

        $tar = Get-ArchiveTool "tar.exe"
        if ($tar) {
            $process = Start-Process -FilePath $tar -ArgumentList @("-xf", $archivePath, "-C", $extractRoot) -Wait -PassThru
            if ($process.ExitCode -eq 0) {
                return
            }
        }

        throw "$($package.name) is $extension. Install 7-Zip and run the launcher again."
    }

    throw "Unsupported archive type for $($package.name): $extension"
}

function Get-ArchiveTool($name) {
    foreach ($path in @(
        (Join-Path $env:ProgramFiles "7-Zip\$name"),
        (Join-Path ${env:ProgramFiles(x86)} "7-Zip\$name"),
        (Join-Path $env:WINDIR "System32\$name")
    )) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    $command = Get-Command $name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    return $null
}

function Install-Packages($config, $statusCallback, $progressCallback) {
    $installPath = Get-StarCraft2Path $config
    $packages = @($config.packages)
    $count = $packages.Count
    if ($count -eq 0) {
        throw "No packages configured."
    }

    for ($i = 0; $i -lt $count; $i++) {
        $package = $packages[$i]
        $base = [int](($i / $count) * 100)
        $downloadRange = [int](70 / $count)

        & $statusCallback "Downloading $($package.name)..."
        $archive = Download-Package $package $progressCallback $base $downloadRange

        & $statusCallback "Extracting $($package.name)..."
        Expand-Package $package $archive $installPath
        & $progressCallback ([int]((($i + 1) / $count) * 100))
    }

    Save-State $config $installPath
    return $installPath
}

function Start-StarCraft2($installPath) {
    $candidates = @(
        (Join-Path $installPath "StarCraft II.exe"),
        (Join-Path $installPath "Support64\SC2Switcher_x64.exe"),
        (Join-Path $installPath "Support\SC2Switcher.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            Start-Process -FilePath $candidate -WorkingDirectory (Split-Path $candidate -Parent)
            return
        }
    }
}

$Config = Read-LauncherConfig

$Form = New-Object Windows.Forms.Form
$Form.Text = "$($Config.app_name) Launcher"
$Form.ClientSize = New-Object Drawing.Size(460, 230)
$Form.FormBorderStyle = "FixedDialog"
$Form.MaximizeBox = $false
$Form.StartPosition = "CenterScreen"

$Title = New-Object Windows.Forms.Label
$Title.Text = $Config.app_name
$Title.Font = New-Object Drawing.Font("Segoe UI", 17, [Drawing.FontStyle]::Bold)
$Title.TextAlign = "MiddleCenter"
$Title.SetBounds(20, 18, 420, 34)

$Version = New-Object Windows.Forms.Label
$Version.Text = "Version $($Config.version)"
$Version.Font = New-Object Drawing.Font("Segoe UI", 10)
$Version.TextAlign = "MiddleCenter"
$Version.SetBounds(20, 54, 420, 24)

$Progress = New-Object Windows.Forms.ProgressBar
$Progress.SetBounds(30, 104, 400, 20)

$Status = New-Object Windows.Forms.Label
$Status.Font = New-Object Drawing.Font("Segoe UI", 10)
$Status.TextAlign = "MiddleCenter"
$Status.SetBounds(20, 132, 420, 30)

$Button = New-Object Windows.Forms.Button
$Button.SetBounds(150, 174, 160, 32)

function Refresh-LauncherUi {
    if (Test-Installed $Config) {
        $Button.Text = "Reinstall / Update"
        $Status.Text = "Installed. Click to reinstall or update."
    }
    else {
        $Button.Text = "Install"
        $Status.Text = "Ready"
    }
}

$Button.Add_Click({
    try {
        $Button.Enabled = $false
        $Progress.Value = 0

        $installPath = Install-Packages $Config {
            param($text)
            $Status.Text = $text
            [Windows.Forms.Application]::DoEvents()
        } {
            param($value)
            $Progress.Value = [Math]::Max(0, [Math]::Min(100, $value))
            [Windows.Forms.Application]::DoEvents()
        }

        $Status.Text = "Done: $installPath"
        $Progress.Value = 100
        if ($Config.launch_after_install) {
            Start-StarCraft2 $installPath
        }
        Refresh-LauncherUi
    }
    catch {
        $Status.Text = "Failed"
        [Windows.Forms.MessageBox]::Show($_.Exception.Message, "Launcher error", "OK", "Error") | Out-Null
    }
    finally {
        $Button.Enabled = $true
    }
})

$Form.Controls.AddRange(@($Title, $Version, $Progress, $Status, $Button))
Refresh-LauncherUi
[Windows.Forms.Application]::Run($Form)
