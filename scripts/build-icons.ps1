param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../src/DeskStickyNotes/Assets'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$assetDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$vectorPath = Join-Path $PSScriptRoot '../src/DeskStickyNotes/Assets/Icons.xaml'
$reader = [Xml.XmlReader]::Create([IO.Path]::GetFullPath($vectorPath))
try { $drawings = [Windows.Markup.XamlReader]::Load($reader) } finally { $reader.Dispose() }

function ConvertTo-IconPng($Drawing, [int]$Size) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.DrawImage($Drawing, [Windows.Rect]::new(0, 0, $Size, $Size))
    $context.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($Size, $Size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    try { $encoder.Save($stream); return ,$stream.ToArray() } finally { $stream.Dispose() }
}

function Write-MultiSizeIcon([string]$Path, [int[]]$Sizes, [bool]$Tray) {
    $frames = @($Sizes | ForEach-Object {
        $source = if ($Tray -or $_ -le 24) { $drawings['TrayMark'] } else { $drawings['AppMark'] }
        ConvertTo-IconPng $source $_
    })
    $stream = [IO.File]::Create($Path)
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$Sizes.Count)
        $offset = 6 + 16 * $Sizes.Count
        for ($i = 0; $i -lt $Sizes.Count; $i++) {
            $dimension = if ($Sizes[$i] -eq 256) { 0 } else { $Sizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose() }
}

[IO.Directory]::CreateDirectory($assetDirectory) | Out-Null
[IO.File]::WriteAllBytes((Join-Path $assetDirectory 'AppIcon.png'), (ConvertTo-IconPng $drawings['AppMark'] 512))
Write-MultiSizeIcon (Join-Path $assetDirectory 'AppIcon.ico') @(16,20,24,32,40,48,64,128,256) $false
Write-MultiSizeIcon (Join-Path $assetDirectory 'TrayIcon.ico') @(16,20,24,32,40,48,64) $true
Write-Output "Exported application and tray icons to $assetDirectory"
