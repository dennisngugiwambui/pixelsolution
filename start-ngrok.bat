@echo off
echo ============================================
echo   PixelSolution - Ngrok Tunnel Setup
echo ============================================
echo.

REM Check if ngrok exists
if not exist "C:\ngrok\ngrok.exe" (
    echo ERROR: Ngrok not found at C:\ngrok\ngrok.exe
    echo.
    echo Please install ngrok:
    echo 1. Download from: https://ngrok.com/download
    echo 2. Extract to: C:\ngrok\
    echo 3. Run this script again
    echo.
    pause
    exit
)

echo Ngrok found!
echo.
echo Starting ngrok tunnel for https://localhost:5001...
echo.
echo IMPORTANT INSTRUCTIONS:
echo 1. Copy the HTTPS forwarding URL (e.g., https://abc123.ngrok-free.app)
echo 2. Update appsettings.Development.json with callback URL
echo 3. Restart your application
echo 4. Access: https://YOUR-URL.ngrok-free.app/Admin/Sales
echo.
echo Press Ctrl+C to stop ngrok when done
echo.
echo ============================================
echo.

cd C:\ngrok
ngrok http https://localhost:5001
