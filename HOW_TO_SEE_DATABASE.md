# How to See Your Database in MongoDB Compass

## The Problem
MongoDB creates databases **lazily** - they only appear when you first write data to them. So if you haven't registered a user yet, the database won't exist in MongoDB Compass.

## Solution 1: Automatic Initialization (Easiest) ✅

The application now **automatically initializes the database** when you visit the home page!

1. **Make sure MongoDB is running**
   - Run `check-mongodb.bat` to verify
   - Or check Windows Services (press `Win + R`, type `services.msc`, look for "MongoDB")

2. **Start your application**
   ```bash
   dotnet run
   ```

3. **Open your browser** and go to: `http://localhost:5000` (or whatever port is shown)

4. **The database will be created automatically** when the page loads!

5. **Refresh MongoDB Compass** (press F5 or click the refresh button)

6. **You should now see:**
   - Database: `MedicalAssistantDB`
   - Collection: `Users`

## Solution 2: Register a User

If automatic initialization doesn't work, simply register a user:

1. Click the **"Register"** button on the home page
2. Fill in the registration form
3. Click **"Register"**
4. The database and collection will be created when the user is saved
5. Refresh MongoDB Compass to see the database

## Solution 3: Manual Initialization

You can also manually initialize the database by calling the API:

1. Open your browser's Developer Console (F12)
2. Go to the Console tab
3. Run this command:
   ```javascript
   fetch('/Account/InitializeDatabase', { method: 'POST' })
     .then(r => r.json())
     .then(data => console.log(data));
   ```

## Viewing Your Data

Once the database is created:

1. **Open MongoDB Compass**
2. **Connect to:** `mongodb://localhost:27017`
3. **Click on:** `MedicalAssistantDB` database
4. **Click on:** `Users` collection
5. **You'll see all registered users!**

## Troubleshooting

### Database still not showing?

1. **Check MongoDB is running:**
   ```bash
   check-mongodb.bat
   ```

2. **Check the browser console** (F12) for any errors

3. **Try registering a user** - this will definitely create the database

4. **Restart MongoDB:**
   - Open Services (`Win + R` → `services.msc`)
   - Find "MongoDB"
   - Right-click → Restart

### Still having issues?

Make sure:
- ✅ MongoDB service is running
- ✅ You're connected to `mongodb://localhost:27017` in Compass
- ✅ You've refreshed MongoDB Compass (F5)
- ✅ The application has been started (`dotnet run`)

## Quick Test

To verify everything works:

1. Start your app: `dotnet run`
2. Open browser: `http://localhost:5000`
3. Register a test user
4. Open MongoDB Compass
5. Refresh (F5)
6. You should see `MedicalAssistantDB` → `Users` → your test user!
