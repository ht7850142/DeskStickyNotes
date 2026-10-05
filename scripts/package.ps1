[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$IncludeRuntime,
    [switch]$AllInstallers,
    [switch]$NoRestore,
    [string]$RuntimeInstaller = "",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $root "src\DeskStickyNotes\DeskStickyNotes.csproj"
$installerDir = Join-Path $root "artifacts\installer"
$installerScript = Join-Path $root "installer\DeskStickyNotes.iss"
$publishRoot = [IO.Path]::GetFullPath((Join-Path $root "artifacts\publish"))
[xml]$projectMetadata = Get-Content -LiteralPath $project -Raw
$appVersion = @($projectMetadata.Project.PropertyGroup.Version | Where-Object { $_ })[0]
if ($appVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "Invalid release version in $project"
}
$runtimeVersion = "8.0.31"

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
    $installerPath = if ($RequestedPath) { [IO.Path]::GetFullPath($RequestedPath) } else {
        Join-Path $root "windowsdesktop-runtime-$runtimeVersion-$Runtime.exe"
    }

    $metadata = Invoke-RestMethod -Uri "https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json"
    $release = $metadata.releases | Where-Object { $_.windowsdesktop.version -eq $runtimeVersion } | Select-Object -First 1
    $runtimeFile = $release.windowsdesktop.files | Where-Object { $_.rid -eq $Runtime -and $_.name -like "*.exe" } | Select-Object -First 1
    if (-not $runtimeFile -or $runtimeFile.url -notlike 'https://builds.dotnet.microsoft.com/*') {
        throw "The official .NET $runtimeVersion installer could not be resolved."
    }

    if (-not (Test-Path -LiteralPath $installerPath)) {
        if ($RequestedPath) { throw "Runtime installer was not found: $installerPath" }
        Write-Host "Downloading Microsoft .NET Desktop Runtime $runtimeVersion..."
        $downloadPath = "$installerPath.download"
        Invoke-WebRequest -Uri $runtimeFile.url -OutFile $downloadPath -UseBasicParsing
        if ((Get-FileHash -LiteralPath $downloadPath -Algorithm SHA512).Hash -ne $runtimeFile.hash) {
            Remove-Item -LiteralPath $downloadPath -Force
            throw "The downloaded runtime installer failed SHA-512 verification."
        }
        Move-Item -LiteralPath $downloadPath -Destination $installerPath -Force
    }

    if ((Get-FileHash -LiteralPath $installerPath -Algorithm SHA512).Hash -ne $runtimeFile.hash) {
        throw "Runtime installer does not match Microsoft's .NET $runtimeVersion release hash: $installerPath"
    }
    Write-Host "Verified Microsoft runtime installer: $installerPath"
    return $installerPath
}

function Assert-PublishDirectory([string]$PublishDir) {
    $resolvedPath = [IO.Path]::GetFullPath($PublishDir)
    if (-not $resolvedPath.StartsWith($publishRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Publish directory must be inside $publishRoot`: $resolvedPath"
    }
    if ((Test-Path -LiteralPath $resolvedPath) -and
        ((Get-Item -LiteralPath $resolvedPath).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to replace a linked publish directory: $resolvedPath"
    }
}

function Publish-App(
    [string]$PublishDir,
    [bool]$PublishSelfContained
) {
    Assert-PublishDirectory $PublishDir
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
        $publishArgs += "-p:RuntimeFrameworkVersion=$runtimeVersion"
    }

    if ($NoRestore) {
        $publishArgs += "--no-restore"
    }

    & $dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    foreach ($symbol in Get-ChildItem -LiteralPath $PublishDir -Filter "*.pdb" -Recurse -File -ErrorAction SilentlyContinue) {
        Remove-Item -LiteralPath $symbol.FullName -Force
    }

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
        throw "Inno Setup compiler was not found. Install it with: winget install JRSoftware.InnoSetup"
    }

    $isccArgs = @(
        "/DSourceDir=$PublishDir",
        "/DOutputDir=$installerDir",
        "/DPackageSuffix=$PackageSuffix"
        "/DMyAppVersion=$appVersion"
        "/DRuntimeVersion=$runtimeVersion"
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

if (-not $SkipInstaller -and -not (Resolve-InnoSetup)) {
    throw "Inno Setup compiler was not found. Install it with: winget install JRSoftware.InnoSetup"
}

if ($AllInstallers) {
    $runtimeInstallerPath = Resolve-RuntimeInstaller $RuntimeInstaller

    $smallPublishDir = Join-Path $root "artifacts\publish\$Runtime-fd"
    Publish-App -PublishDir $smallPublishDir -PublishSelfContained $false
    New-PortableZip -PublishDir $smallPublishDir -Name "DeskStickyNotes-portable-$appVersion-$Runtime-fd.zip"
    Invoke-Inno -PublishDir $smallPublishDir -PackageSuffix "-fd" -BundleRuntime $false -RuntimeInstallerPath ""
    Invoke-Inno -PublishDir $smallPublishDir -PackageSuffix "-runtime" -BundleRuntime $true -RuntimeInstallerPath $runtimeInstallerPath

    New-SelfContainedPortableZip -Name "DeskStickyNotes-portable-$appVersion-$Runtime-runtime.zip"

    if (-not $SkipInstaller) {
        $releaseNames = @(
            "DeskStickyNotes-portable-$appVersion-$Runtime-fd.zip",
            "DeskStickyNotes-portable-$appVersion-$Runtime-runtime.zip",
            "DeskStickyNotesSetup-$appVersion-fd.exe",
            "DeskStickyNotesSetup-$appVersion-runtime.exe"
        )
        $checksums = foreach ($name in $releaseNames) {
            $releasePath = Join-Path $installerDir $name
            if (-not (Test-Path -LiteralPath $releasePath -PathType Leaf)) { throw "Missing release artifact: $releasePath" }
            $hash = (Get-FileHash -LiteralPath $releasePath -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $name"
        }
        $checksums | Set-Content -LiteralPath (Join-Path $installerDir "DeskStickyNotes-$appVersion-SHA256SUMS.txt") -Encoding ASCII
    }
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
