using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MedicalAssistant.Data;
using MedicalAssistant.Models;
using MedicalAssistant.Services;
using Stripe;
using MongoDB.Driver;

// ============================================================================
// Application Startup Configuration
// ============================================================================
// This file configures the ASP.NET Core application, including:
// - Service registration (dependency injection)
// - Database configuration (SQL Server and MongoDB)
// - Middleware pipeline setup
// - Database initialization and data seeding
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// Add MVC services to enable controllers and views
builder.Services.AddControllersWithViews();
// Configure distributed memory cache for session storage
builder.Services.AddDistributedMemoryCache();

// Configure session management
// Sessions are used to maintain conversation state and user authentication
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session expires after 30 minutes of inactivity
    options.Cookie.HttpOnly = true; // Prevent JavaScript access to cookie (security)
    options.Cookie.IsEssential = true; // Required for GDPR compliance
});

// ============================================================================
// SQL Server Database Configuration
// ============================================================================
// SQL Server is used for:
// - Doctor information
// - Disease and symptom data
// - Appointment records
// - Chat messages (when user is logged in)
// ============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=MedicalAssistantDb;Trusted_Connection=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ============================================================================
// ASP.NET Identity Configuration
// ============================================================================
// Note: Identity is configured but not actively used for authentication.
// User authentication is handled by MongoDB (MongoUserService).
// Identity is kept for potential future use or compatibility.
// ============================================================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Simplified password requirements for easier testing/demo
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6; // Minimum 6 characters
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ============================================================================
// MongoDB Configuration
// ============================================================================
// MongoDB is used for user authentication and profile storage.
// This provides a NoSQL alternative to SQL Server for user data.
// ============================================================================
try
{
    // Get MongoDB connection string from configuration or use default
    var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
    var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"] ?? "MedicalAssistantDB";
    
    // Create MongoDB client and database instance
    var mongoClient = new MongoClient(mongoConnectionString);
    var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);
    
    // Test MongoDB connection to verify it's accessible
    try
    {
        mongoClient.ListDatabaseNames(); // This will throw if MongoDB is not running
        System.Diagnostics.Debug.WriteLine("MongoDB connection successful");
    }
    catch (Exception ex)
    {
        // Connection test failed, but continue anyway
        // MongoDB might start later, or user might be using MongoDB Atlas
        System.Diagnostics.Debug.WriteLine($"MongoDB connection warning: {ex.Message}");
    }
    
    // Register MongoDB database as singleton (shared across all requests)
    builder.Services.AddSingleton<IMongoDatabase>(mongoDatabase);
    // Register MongoUserService as scoped (one per request)
    builder.Services.AddScoped<MongoUserService>();
}
catch (Exception ex)
{
    // If MongoDB initialization fails completely, create a dummy instance
    // This prevents application crash and allows app to run without MongoDB
    // User will see error messages when trying to login/register
    System.Diagnostics.Debug.WriteLine($"MongoDB initialization error: {ex.Message}");
    var mongoClient = new MongoClient("mongodb://localhost:27017");
    var mongoDatabase = mongoClient.GetDatabase("MedicalAssistantDB");
    builder.Services.AddSingleton<IMongoDatabase>(mongoDatabase);
    builder.Services.AddScoped<MongoUserService>();
}

// ============================================================================
// Application Services Registration
// ============================================================================
// Register all business logic services with dependency injection
// ============================================================================

// HTTP client for making external API calls (e.g., Groq API)
builder.Services.AddHttpClient();

// Chatbot service - handles conversation logic and AI integration
builder.Services.AddScoped<IChatbotService, SimpleChatbotService>();

// Doctor service - handles doctor search and recommendations
builder.Services.AddScoped<IDoctorService, DoctorService>();

// Appointment service - handles appointment CRUD operations
builder.Services.AddScoped<IAppointmentService, AppointmentService>();

// Payment service - handles Stripe payment processing
builder.Services.AddScoped<IPaymentService, PaymentService>();

// ============================================================================
// Stripe Payment Configuration
// ============================================================================
// Configure Stripe settings from appsettings.json
// Set global Stripe API key for all Stripe operations
// ============================================================================
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"] ?? string.Empty;

var app = builder.Build();

// ============================================================================
// HTTP Request Pipeline Configuration
// ============================================================================
// Middleware is executed in order - order matters!
// ============================================================================

// Configure error handling based on environment
if (!app.Environment.IsDevelopment())
{
    // In production, use custom error page
    app.UseExceptionHandler("/Home/Error");
    // Enable HTTP Strict Transport Security (HSTS)
    app.UseHsts();
}

// HTTPS redirection configuration
// In development, only redirect if HTTPS port is explicitly configured
// This prevents errors when HTTPS is not set up locally
if (app.Environment.IsDevelopment())
{
    var httpsPort = builder.Configuration["ASPNETCORE_HTTPS_PORT"] 
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT");
    
    if (!string.IsNullOrEmpty(httpsPort))
    {
        app.UseHttpsRedirection(); // Only redirect if HTTPS is available
    }
}
else
{
    // In production, always enforce HTTPS for security
    app.UseHttpsRedirection();
}

// Serve static files (CSS, JavaScript, images) from wwwroot folder
app.UseStaticFiles();

// Enable routing to match URLs to controllers and actions
app.UseRouting();

// Enable session management (must be before UseAuthentication)
app.UseSession();

// Enable authentication and authorization
app.UseAuthentication(); // Who are you?
app.UseAuthorization();   // Are you allowed?

// Configure default route: /Home/Index
// Pattern: {controller}/{action}/{id?}
// Example: /Home/Index, /Account/Login, /Payment/Success?appointmentId=123
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ============================================================================
// Database Initialization and Data Seeding
// ============================================================================
// This runs once when the application starts to:
// 1. Create database if it doesn't exist
// 2. Create all tables based on Entity Framework models
// 3. Load CSV data (doctors, diseases, symptoms) into database
// 4. Initialize disease matching cache
// ============================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        // Get required services
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        
        // Ensure database exists and schema is correct
        try
        {
            // Check if database exists and is accessible
            bool canConnect = await context.Database.CanConnectAsync();
            
            if (canConnect)
            {
                // Verify schema by attempting to query key tables
                // If query fails, schema might be outdated
                try
                {
                    await context.ChatMessages.FirstOrDefaultAsync();
                    await context.Appointments.FirstOrDefaultAsync();
                    logger.LogInformation("Database schema verified");
                }
                catch
                {
                    // Schema mismatch detected - recreate database
                    logger.LogWarning("Database schema mismatch detected. Deleting and recreating database...");
                    await context.Database.EnsureDeletedAsync();
                    canConnect = false;
                }
            }
            
            if (!canConnect)
            {
                // Create database and all tables based on Entity Framework models
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Database created successfully");
            }
            
            // Wait 2 seconds to ensure all tables are fully created
            // This prevents race conditions during data seeding
            await Task.Delay(2000);
            
            // Load CSV data into database
            try
            {
                logger.LogInformation("Starting data seeding...");
                // SeedData.InitializeAsync loads:
                // - Doctors from doctors_info_1.csv and doctors_info_2.csv
                // - Diseases from disease_description.csv
                // - Disease-specialist mappings from Disease_Specialist.csv
                // - Symptoms from Disease and symptoms dataset.csv
                await SeedData.InitializeAsync(context, userManager, roleManager);
                
                // Verify data was loaded successfully
                var doctorCount = await context.Doctors.CountAsync();
                var diseaseCount = await context.Diseases.CountAsync();
                logger.LogInformation($"Data seeding completed. Loaded {doctorCount} doctors and {diseaseCount} diseases.");
                
                // Load disease-symptom matching cache into memory
                // This improves performance when matching user symptoms to diseases
                MedicalAssistant.Services.DiseaseMatchingService.LoadDiseaseSymptomsCache();
                logger.LogInformation("Disease symptoms cache loaded from Original_Dataset.csv");
            }
            catch (Exception seedEx)
            {
                // Data seeding failed, but continue application startup
                // Some features may not work, but app won't crash
                logger.LogError(seedEx, "Data seeding failed. Application will continue but some features may not work.");
            }
        }
        catch (Exception dbEx)
        {
            // Database initialization failed - try to recover
            logger.LogError(dbEx, "Database initialization error - attempting to recreate database...");
            try
            {
                // Delete and recreate database as last resort
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Database recreated successfully");
            }
            catch (Exception recreateEx)
            {
                // Even recreation failed - log error but continue
                // Application will run but database features won't work
                logger.LogError(recreateEx, "Could not recreate database - application will continue without database");
            }
        }
    }
    catch (Exception ex)
    {
        // Catch-all for any unexpected errors during initialization
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Start the web server and begin accepting HTTP requests
app.Run();

app.Run();

