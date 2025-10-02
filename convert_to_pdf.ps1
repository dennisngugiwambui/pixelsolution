# PowerShell script to convert HTML to PDF using Chrome/Edge in headless mode
param(
    [string]$InputHtml = "PixelSolution_Business_Description.html",
    [string]$OutputPdf = "PixelSolution_Business_Description.pdf"
)

$htmlPath = Resolve-Path $InputHtml
$pdfPath = Join-Path (Get-Location) $OutputPdf

# Try Chrome first
$chromePath = @(
    "${env:ProgramFiles}\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
    "${env:LOCALAPPDATA}\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($chromePath) {
    Write-Host "Using Chrome to generate PDF..."
    & $chromePath --headless --disable-gpu --print-to-pdf="$pdfPath" --no-margins --print-to-pdf-no-header "file:///$htmlPath"
    if (Test-Path $pdfPath) {
        Write-Host "PDF generated successfully: $pdfPath"
        return
    }
}

# Try Edge as fallback
$edgePath = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
if (Test-Path $edgePath) {
    Write-Host "Using Edge to generate PDF..."
    & $edgePath --headless --disable-gpu --print-to-pdf="$pdfPath" --no-margins --print-to-pdf-no-header "file:///$htmlPath"
    if (Test-Path $pdfPath) {
        Write-Host "PDF generated successfully: $pdfPath"
        return
    }
}

Write-Host "Neither Chrome nor Edge found. Please install one of them or use an online HTML to PDF converter."
Write-Host "You can open the HTML file in your browser and use Print > Save as PDF"
