# MongoDB Status Checker
Write-Host "=== MongoDB Status Check ===" -ForegroundColor Cyan
Write-Host ""

# Check if MongoDB service is running
Write-Host "1. Checking Windows Service..." -ForegroundColor Yellow
$service = Get-Service -Name "MongoDB" -ErrorAction SilentlyContinue

if ($service) {
    if ($service.Status -eq "Running") {
        Write-Host "   ✓ MongoDB service is RUNNING" -ForegroundColor Green
    } else {
        Write-Host "   ✗ MongoDB service is NOT RUNNING (Status: $($service.Status))" -ForegroundColor Red
        Write-Host "   Try running: net start MongoDB (as Administrator)" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ✗ MongoDB service not found. MongoDB may not be installed." -ForegroundColor Red
}

Write-Host ""

# Check if port 27017 is listening
Write-Host "2. Checking port 27017..." -ForegroundColor Yellow
$port = Get-NetTCPConnection -LocalPort 27017 -ErrorAction SilentlyContinue

if ($port) {
    Write-Host "   ✓ Port 27017 is LISTENING" -ForegroundColor Green
} else {
    Write-Host "   ✗ Port 27017 is NOT LISTENING" -ForegroundColor Red
    Write-Host "   MongoDB is not running or not listening on port 27017" -ForegroundColor Yellow
}

Write-Host ""

# Try to connect using mongosh (if available)
Write-Host "3. Testing connection..." -ForegroundColor Yellow
try {
    $mongosh = Get-Command mongosh -ErrorAction SilentlyContinue
    if ($mongosh) {
        Write-Host "   ℹ mongosh is installed. Try running 'mongosh' to connect." -ForegroundColor Cyan
    } else {
        Write-Host "   ℹ mongosh not found in PATH. MongoDB may not be installed." -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ℹ Cannot check mongosh" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
if ($service -and $service.Status -eq "Running" -and $port) {
    Write-Host "✓ MongoDB appears to be running correctly!" -ForegroundColor Green
    Write-Host ""
    Write-Host "To view your database:" -ForegroundColor Cyan
    Write-Host "  1. Open MongoDB Compass" -ForegroundColor White
    Write-Host "  2. Connect to: mongodb://localhost:27017" -ForegroundColor White
    Write-Host "  3. Look for database: MedicalAssistantDB" -ForegroundColor White
    Write-Host "  4. Click on Collections → Users to see registered users" -ForegroundColor White
} else {
    Write-Host "✗ MongoDB is NOT running" -ForegroundColor Red
    Write-Host ""
    Write-Host "To fix this:" -ForegroundColor Cyan
    Write-Host "  1. Install MongoDB from: https://www.mongodb.com/try/download/community" -ForegroundColor White
    Write-Host "  2. Or use MongoDB Atlas (cloud): https://www.mongodb.com/cloud/atlas/register" -ForegroundColor White
    Write-Host "  3. See MONGODB_SETUP.md for detailed instructions" -ForegroundColor White
}

Write-Host ""
