# Git Push Script for PixelSolution
# This script handles the complete git workflow safely

param(
    [string]$CommitMessage = "Update PixelSolution changes"
)

Write-Host "Starting Git workflow..." -ForegroundColor Green

# Check if there are any changes
$status = git status --porcelain
if (-not $status) {
    Write-Host "No changes to commit." -ForegroundColor Yellow
    exit 0
}

# Pull latest changes first
Write-Host "Pulling latest changes..." -ForegroundColor Cyan
git pull origin master --no-rebase

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to pull changes. Please resolve conflicts manually." -ForegroundColor Red
    exit 1
}

# Add all changes
Write-Host "Adding changes..." -ForegroundColor Cyan
git add .

# Commit changes
Write-Host "Committing changes..." -ForegroundColor Cyan
git commit -m $CommitMessage

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to commit changes." -ForegroundColor Red
    exit 1
}

# Push changes
Write-Host "Pushing changes..." -ForegroundColor Cyan
git push origin master

if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully pushed changes to GitHub!" -ForegroundColor Green
} else {
    Write-Host "Failed to push changes. Trying with force-with-lease..." -ForegroundColor Yellow
    git push --force-with-lease origin master
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully pushed changes with force-with-lease!" -ForegroundColor Green
    } else {
        Write-Host "Failed to push changes. Please check your internet connection and try again." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Git workflow completed successfully!" -ForegroundColor Green
