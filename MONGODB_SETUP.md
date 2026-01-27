# MongoDB Setup Guide

## What is MongoDB?

MongoDB is a NoSQL database that stores data in JSON-like documents. It's **separate from XAMPP** (which is for MySQL/PHP). You need to install MongoDB separately.

## Option 1: Install MongoDB Locally (Recommended for Development)

### Step 1: Download MongoDB
1. Go to: https://www.mongodb.com/try/download/community
2. Select:
   - Version: Latest (or 7.0)
   - Platform: Windows
   - Package: MSI
3. Download the installer

### Step 2: Install MongoDB
1. Run the downloaded `.msi` file
2. Choose "Complete" installation
3. **IMPORTANT**: Check "Install MongoDB as a Service"
4. Check "Run service as Network Service user"
5. Check "Install MongoDB Compass" (GUI tool to view databases)
6. Click "Install"

### Step 3: Verify MongoDB is Running
1. Open Command Prompt or PowerShell
2. Run: `mongod --version` (should show version)
3. Check Windows Services:
   - Press `Win + R`
   - Type `services.msc` and press Enter
   - Look for "MongoDB" service
   - It should be "Running"

### Step 4: Test Connection
Open Command Prompt and run:
```bash
mongosh
```
If it connects, you'll see: `Current Mongosh Log ID: ...`

## Option 2: Use MongoDB Atlas (Cloud - Easier, No Installation)

### Step 1: Create Free Account
1. Go to: https://www.mongodb.com/cloud/atlas/register
2. Sign up for free account

### Step 2: Create Cluster
1. Click "Build a Database"
2. Choose "FREE" (M0) tier
3. Select a cloud provider and region
4. Click "Create"

### Step 3: Get Connection String
1. Click "Connect" on your cluster
2. Choose "Connect your application"
3. Copy the connection string (looks like: `mongodb+srv://username:password@cluster.mongodb.net/`)
4. Replace `<password>` with your database password

### Step 4: Update appsettings.json
Replace the MongoDB connection string in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb+srv://username:password@cluster.mongodb.net/"
  },
  "MongoDB": {
    "DatabaseName": "MedicalAssistantDB"
  }
}
```

## Option 3: Use Docker (If you have Docker installed)

```bash
docker run -d -p 27017:27017 --name mongodb mongo:latest
```

## How to Check if MongoDB is Running

### Method 1: Windows Services
1. Press `Win + R`
2. Type `services.msc`
3. Look for "MongoDB" service
4. Status should be "Running"

### Method 2: Command Line
```bash
# Check if MongoDB process is running
tasklist | findstr mongod

# Or try to connect
mongosh
```

### Method 3: Check Port
```bash
netstat -an | findstr 27017
```
If you see `LISTENING`, MongoDB is running.

## Viewing Your Database and Users

### Using MongoDB Compass (GUI - Easiest)
1. Open MongoDB Compass (installed with MongoDB)
2. Connect to: `mongodb://localhost:27017`
3. You'll see your database "MedicalAssistantDB"
4. Click on it → Collections → "Users"
5. You'll see all registered users

### Using Command Line (mongosh)
```bash
# Connect to MongoDB
mongosh

# Switch to your database
use MedicalAssistantDB

# View all users
db.Users.find().pretty()

# Count users
db.Users.countDocuments()
```

## Troubleshooting

### MongoDB Won't Start
1. Check Windows Services (services.msc)
2. Right-click "MongoDB" → Start
3. If it fails, check Windows Event Viewer for errors

### Connection Refused Error
- MongoDB is not running
- Start MongoDB service: `net start MongoDB` (as Administrator)
- Or restart your computer

### Port 27017 Already in Use
- Another MongoDB instance might be running
- Stop it first, then start yours

## Quick Start Commands

```bash
# Start MongoDB (as Administrator)
net start MongoDB

# Stop MongoDB (as Administrator)
net stop MongoDB

# Connect to MongoDB shell
mongosh

# In mongosh, view users:
use MedicalAssistantDB
db.Users.find().pretty()
```

## Need Help?

If you prefer not to install MongoDB, I can modify the code to use SQL Server (which you already have) instead. Just let me know!
