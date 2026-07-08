[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$IncludeRuntime,
    [switch]$AllInstallers,
    [string]$RuntimeInstaller = "",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\DeskStickyNotes\DeskStickyNotes.csproj"
$installerDir = Join-Path $root "artifacts\installer"
$installerScript = Join-Path $root "installer\DeskStickyNotes.iss"
$appVersion = "0.2.5"

function Resolve-DotNet {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        return $dotnet.Source
    }

    $defaultPath = "C:\Program Files\dotnet\dotnet.exe"
    if (Test-Path -LiteralPath $defaultPath) {
        return $defaultPath
    }

    throw "dotnet was not found. Install the .NET Desktop SDK or add C:\Program Files\dotnet\ to PATH."
}

function Resolve-InnoSetup {
    $iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($iscc) {
        return $iscc.Source
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    return $null
}

function Resolve-RuntimeInstaller([string]$RequestedPath) {
    if ($RequestedPath) {
        if (-not (Test-Path -LiteralPath $RequestedPath)) {
            throw "Runtime installer was not found: $RequestedPath"
        }

        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    $defaultPath = Join-Path $root "windowsdesktop-runtime-8.0.28-win-x64.exe"
    if (-not (Test-Path -LiteralPath $defaultPath)) {
        throw "Runtime installer was not found: $defaultPath"
    }

    return (Resolve-Path -LiteralPath $defaultPath).Path
}

function Publish-App(
    [string]$PublishDir,
    [bool]$PublishSelfContained
) {
    if (Test-Path -LiteralPath $PublishDir) {
        Remove-Item -LiteralPath $PublishDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $PublishDir, $installerDir -Force | Out-Null

    $dotnet = Resolve-DotNet
    $selfContainedValue = if ($PublishSelfContained) { "true" } else { "false" }
    $publishArgs = @(
        "publish", $project,
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained:$selfContainedValue",
        "-p:PublishSingleFile=true",
        "-p:PublishTrimmed=false",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-o", $PublishDir
    )

    if ($PublishSelfContained) {
        $publishArgs += "-p:EnableCompressionInSingleFile=true"
    }

    & $dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    Get-ChildItem -LiteralPath $PublishDir -Filter "*.pdb" -Recurse -File -ErrorAction SilentlyContinue |
        Remove-Item -Force

    Write-Host "Published to $PublishDir"
}

function New-PortableZip(
    [string]$PublishDir,
    [string]$Name
) {
    $portableZip = Join-Path $installerDir $Name
    if (Test-Path -LiteralPath $portableZip) {
        Remove-Item -LiteralPath $portableZip -Force
    }

    Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $portableZip -Force
    Write-Host "Portable package: $portableZip"
}

function Invoke-Inno(
    [string]$PublishDir,
    [string]$PackageSuffix,
    [bool]$BundleRuntime,
    [string]$RuntimeInstallerPath
) {
    if ($SkipInstaller) {
        return
    }

    $iscc = Resolve-InnoSetup
    if (-not $iscc) {
        Write-Warning "Inno Setup compiler was not found. Install it with: winget install JRSoftware.InnoSetup"
        return
    }

    $isccArgs = @(
        "/DSourceDir=$PublishDir",
        "/DOutputDir=$installerDir",
        "/DPackageSuffix=$PackageSuffix"
    )

    if ($BundleRuntime) {
        $isccArgs += "/DIncludeRuntime=1"
        $isccArgs += "/DRuntimeInstaller=$RuntimeInstallerPath"
    }

    & $iscc $installerScript @isccArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compiler failed with exit code $LASTEXITCODE"
    }
}

function New-SelfContainedPortableZip(
    [string]$Name
) {
    $portableRuntimePublishDir = Join-Path $root "artifacts\publish\$Runtime-portable-runtime"
    Publish-App -PublishDir $portableRuntimePublishDir -PublishSelfContained $true
    New-PortableZip -PublishDir $portableRuntimePublishDir -Name $Name
}

if ($AllInstallers) {
    $runtimeInstallerPath = Resolve-RuntimeInstaller $RuntimeInstaller

    $smallPublishDir = Join-Path $root "artifacts\publish\$Runtime-fd"
    Publish-App -PublishDir $smallPublishDir -PublishSelfContained $false
    New-PortableZip -PublishDir $smallPublishDir -Name "DeskStickyNotes-portable-$appVersion-$Runtime-fd.zip"
    Invoke-Inno -PublishDir $smallPublishDir -PackageSuffix "-fd" -BundleRuntime $false -RuntimeInstallerPath ""
    Invoke-Inno -PublishDir $smallPublishDir -PackageSuffix "-runtime" -BundleRuntime $true -RuntimeInstallerPath $runtimeInstallerPath

    New-SelfContainedPortableZip -Name "DeskStickyNotes-portable-$appVersion-$Runtime-runtime.zip"
}
else {
    $bundleRuntime = [bool]$IncludeRuntime
    $runtimeInstallerPath = if ($bundleRuntime) { Resolve-RuntimeInstaller $RuntimeInstaller } else { "" }
    $publishDir = Join-Path $root "artifacts\publish\$Runtime"

    if ($bundleRuntime) {
        $SelfContained = $false
    }

    Publish-App -PublishDir $publishDir -PublishSelfContained ([bool]$SelfContained)

    if ($bundleRuntime) {
        New-SelfContainedPortableZip -Name "DeskStickyNotes-portable-$appVersion-$Runtime-runtime.zip"
    }
    else {
        New-PortableZip -PublishDir $publishDir -Name "DeskStickyNotes-portable-$appVersion-$Runtime.zip"
    }

    $suffix = if ($bundleRuntime) { "-runtime" } elseif ($SelfContained) { "-self-contained" } else { "-fd" }
    Invoke-Inno -PublishDir $publishDir -PackageSuffix $suffix -BundleRuntime $bundleRuntime -RuntimeInstallerPath $runtimeInstallerPath
}

Write-Host "Installer output:"
Get-ChildItem -LiteralPath $installerDir -Filter "*.exe" | Sort-Object LastWriteTime -Descending | Select-Object FullName, Length, LastWriteTime
