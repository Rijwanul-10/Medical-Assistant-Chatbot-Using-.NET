@echo off
echo === MongoDB Status Check ===
echo.

echo 1. Checking Windows Service...
sc query MongoDB >nul 2>&1
if %errorlevel% == 0 (
    sc query MongoDB | findstr "RUNNING" >nul
    if %errorlevel% == 0 (
        echo    [OK] MongoDB service is RUNNING
    ) else (
        echo    [X] MongoDB service is NOT RUNNING
        echo    Try running: net start MongoDB (as Administrator)
    )
) else (
    echo    [X] MongoDB service not found. MongoDB may not be installed.
)

echo.
echo 2. Checking port 27017...
netstat -an | findstr ":27017" | findstr "LISTENING" >nul
if %errorlevel% == 0 (
    echo    [OK] Port 27017 is LISTENING
) else (
    echo    [X] Port 27017 is NOT LISTENING
    echo    MongoDB is not running or not listening on port 27017
)

echo.
echo === Summary ===
echo.
echo To view your database:
echo   1. Open MongoDB Compass
echo   2. Connect to: mongodb://localhost:27017
echo   3. Look for database: MedicalAssistantDB
echo   4. Click on Collections -^> Users to see registered users
echo.
echo To install MongoDB:
echo   1. Download from: https://www.mongodb.com/try/download/community
echo   2. Or use MongoDB Atlas (cloud): https://www.mongodb.com/cloud/atlas/register
echo   3. See MONGODB_SETUP.md for detailed instructions
echo.
pause
