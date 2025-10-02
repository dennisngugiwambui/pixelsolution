# PixelSolution - Ngrok Quick Start Script
# This script helps you start ngrok tunnel for your application

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  PixelSolution - Ngrok Tunnel Setup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if ngrok is installed
$ngrokPath = "C:\ngrok\ngrok.exe"

if (-Not (Test-Path $ngrokPath)) {
    Write-Host "❌ Ngrok not found at: $ngrokPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install ngrok:" -ForegroundColor Yellow
    Write-Host "1. Download from: https://ngrok.com/download" -ForegroundColor Yellow
    Write-Host "2. Extract to: C:\ngrok\" -ForegroundColor Yellow
    Write-Host "3. Run this script again" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit
}

Write-Host "✅ Ngrok found!" -ForegroundColor Green
Write-Host ""

# Check if application is running
Write-Host "🔍 Checking if PixelSolution is running on https://localhost:5001..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://localhost:5001" -SkipCertificateCheck -TimeoutSec 5 -ErrorAction Stop
    Write-Host "✅ Application is running!" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Application not detected on https://localhost:5001" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please start your PixelSolution application first:" -ForegroundColor Yellow
    Write-Host "1. Open Visual Studio" -ForegroundColor Yellow
    Write-Host "2. Press F5 or Ctrl+F5 to run the project" -ForegroundColor Yellow
    Write-Host "3. Wait for the app to start" -ForegroundColor Yellow
    Write-Host "4. Run this script again" -ForegroundColor Yellow
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne "y") {
        exit
    }
}

Write-Host ""
Write-Host "🚀 Starting ngrok tunnel..." -ForegroundColor Cyan
Write-Host ""
Write-Host "📝 IMPORTANT INSTRUCTIONS:" -ForegroundColor Yellow
Write-Host "1. Copy the HTTPS forwarding URL (e.g., https://abc123.ngrok-free.app)" -ForegroundColor White
Write-Host "2. Update appsettings.Development.json with the callback URL:" -ForegroundColor White
Write-Host "   'CallbackUrl': 'https://YOUR-URL.ngrok-free.app/api/mpesa/callback'" -ForegroundColor White
Write-Host "3. Restart your application after updating the callback URL" -ForegroundColor White
Write-Host "4. Access your app at: https://YOUR-URL.ngrok-free.app/Admin/Sales" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop ngrok when done" -ForegroundColor Yellow
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Start ngrok
& $ngrokPath http https://localhost:5001
