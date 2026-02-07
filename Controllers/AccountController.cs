using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MedicalAssistant.Models;
using MedicalAssistant.Services;

namespace MedicalAssistant.Controllers;

/// <summary>
/// Account Controller - Handles user authentication and registration
/// Uses MongoDB for user storage (MongoUserService)
/// All endpoints return JSON responses for AJAX calls
/// </summary>
public class AccountController : Controller
{
    private readonly MongoUserService _userService;
    private readonly ILogger<AccountController>? _logger;
    
    public AccountController(MongoUserService userService, ILogger<AccountController>? logger = null)
    {
        _userService = userService;
        _logger = logger;
    }
    
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }
    
    /// <summary>
    /// POST /Account/Login
    /// Authenticates user and creates session
    /// Returns JSON response for AJAX calls
    /// </summary>
    /// <param name="model">Login credentials (email and password)</param>
    /// <returns>JSON response with success status and user info</returns>
    [HttpPost]
    [Route("Account/Login")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> Login([FromBody] LoginViewModel model)
    {
        // Validate model state (data annotations)
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            Response.ContentType = "application/json";
            return Json(new { success = false, errors = errors });
        }
        
        try
        {
            // Additional input validation
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                Response.ContentType = "application/json";
                return Json(new { success = false, errors = new[] { "Please enter your email address." } });
            }
            
            if (string.IsNullOrWhiteSpace(model.Password))
            {
                Response.ContentType = "application/json";
                return Json(new { success = false, errors = new[] { "Please enter your password." } });
            }
            
            // Retrieve user from MongoDB database
            MongoUser? user;
            try
            {
                user = await _userService.GetUserByEmailAsync(model.Email);
            }
            catch (Exception dbEx)
            {
                // MongoDB connection error - provide helpful error message
                System.Diagnostics.Debug.WriteLine($"Database error in login: {dbEx.Message}");
                Response.ContentType = "application/json";
                Response.StatusCode = 500;
                return Json(new { 
                    success = false, 
                    errors = new[] { $"MongoDB is not running. Please install and start MongoDB. Error: {dbEx.Message.Split('.').FirstOrDefault()}" } 
                });
            }
            
            // Check if user exists
            if (user == null)
            {
                Response.ContentType = "application/json";
                return Json(new { success = false, errors = new[] { "User not found in database. Please register first." } });
            }
            
            // Verify password using SHA256 hash comparison
            if (!_userService.VerifyPassword(model.Password, user.PasswordHash))
            {
                Response.ContentType = "application/json";
                return Json(new { success = false, errors = new[] { "Invalid email or password." } });
            }
            
            // Update last login timestamp
            await _userService.UpdateLastLoginAsync(user.Email);
            
            // Store user information in session for authentication
            // These values are used throughout the application to identify the logged-in user
            HttpContext.Session.SetString("UserId", user.Id ?? ""); // MongoDB user ID
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", user.Name ?? user.Email);
            HttpContext.Session.SetString("IsLoggedIn", "true");
            
            // Return success response with user information
            Response.ContentType = "application/json";
            return Json(new { 
                success = true, 
                message = $"Login successful! Welcome back, {user.Name ?? user.Email}!", 
                user = new { name = user.Name, email = user.Email } 
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Login error");
            System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Ensure we return JSON even on errors
            Response.ContentType = "application/json";
            Response.StatusCode = 500;
            return Json(new { 
                success = false, 
                errors = new[] { "An error occurred during login. Please try again." } 
            });
        }
    }
    
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }
    
    [HttpPost]
    [Route("Account/Register")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            Response.ContentType = "application/json";
            return Json(new { success = false, errors = errors });
        }
        
        try
        {
            // Validate email format
            if (string.IsNullOrWhiteSpace(model.Email) || !model.Email.Contains("@"))
            {
                Response.ContentType = "application/json";
                return Json(new { success = false, errors = new[] { "Please enter a valid email address." } });
            }
            
            // Check if user already exists in MongoDB
            MongoUser? existingUser;
            try
            {
                existingUser = await _userService.GetUserByEmailAsync(model.Email);
            }
            catch (Exception dbEx)
            {
                System.Diagnostics.Debug.WriteLine($"Database error checking existing user: {dbEx.Message}");
                Response.ContentType = "application/json";
                Response.StatusCode = 500;
                return Json(new { 
                    success = false, 
                    errors = new[] { $"MongoDB is not running. Please install and start MongoDB. Error: {dbEx.Message.Split('.').FirstOrDefault()}" } 
                });
            }
            
            if (existingUser != null)
            {
                Response.ContentType = "application/json";
                return Json(new { success = false, errors = new[] { "Email already registered. Please login instead." } });
            }
            
            // Create new user
            var user = new MongoUser
            {
                Email = model.Email.Trim().ToLower(),
                PasswordHash = _userService.HashPassword(model.Password),
                Name = model.Name?.Trim(),
                Age = model.Age,
                Address = model.Address?.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            
            try
            {
                var (success, userId, error) = await _userService.CreateUserAsync(user);
                
                if (success && !string.IsNullOrEmpty(userId))
                {
                    // Get the created user from database to confirm
                    MongoUser? createdUser;
                    try
                    {
                        createdUser = await _userService.GetUserByEmailAsync(user.Email);
                    }
                    catch (Exception dbEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Database error getting created user: {dbEx.Message}");
                        Response.ContentType = "application/json";
                        Response.StatusCode = 500;
                        return Json(new { 
                            success = false, 
                            errors = new[] { $"MongoDB is not running. Please install and start MongoDB. Error: {dbEx.Message.Split('.').FirstOrDefault()}" } 
                        });
                    }
                    
                    if (createdUser == null)
                    {
                        Response.ContentType = "application/json";
                        return Json(new { success = false, errors = new[] { "Registration failed: User not found in database after creation." } });
                    }
                    
                    // Auto-login after registration
                    HttpContext.Session.SetString("UserId", createdUser.Id ?? "");
                    HttpContext.Session.SetString("UserEmail", createdUser.Email);
                    HttpContext.Session.SetString("UserName", createdUser.Name ?? createdUser.Email);
                    HttpContext.Session.SetString("IsLoggedIn", "true");
                    
                    Response.ContentType = "application/json";
                    return Json(new { 
                        success = true, 
                        message = "Registration successful! You have been logged in.", 
                        user = new { name = createdUser.Name, email = createdUser.Email } 
                    });
                }
                else
                {
                    Response.ContentType = "application/json";
                    return Json(new { success = false, errors = new[] { error ?? "Failed to create account. Please try again." } });
                }
            }
            catch (Exception dbEx)
            {
                // Handle database connection errors
                System.Diagnostics.Debug.WriteLine($"Database error in registration: {dbEx.Message}");
                Response.ContentType = "application/json";
                Response.StatusCode = 500;
                return Json(new { 
                    success = false, 
                    errors = new[] { $"MongoDB is not running. Please install and start MongoDB. Error: {dbEx.Message.Split('.').FirstOrDefault()}" } 
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Registration error");
            System.Diagnostics.Debug.WriteLine($"Registration error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Ensure we return JSON even on errors
            Response.ContentType = "application/json";
            Response.StatusCode = 500;
            return Json(new { 
                success = false, 
                errors = new[] { "An error occurred during registration. Please try again." } 
            });
        }
    }
    
    [HttpPost]
    [Route("Account/Logout")]
    [Produces("application/json")]
    public IActionResult Logout()
    {
        try
        {
            HttpContext.Session.Clear();
            Response.ContentType = "application/json";
            return Json(new { success = true, message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Logout error");
            Response.ContentType = "application/json";
            Response.StatusCode = 500;
            return Json(new { success = false, errors = new[] { "Logout error" } });
        }
    }
    
    [HttpGet]
    [Route("Account/GetCurrentUser")]
    [Produces("application/json")]
    public IActionResult GetCurrentUser()
    {
        var isLoggedIn = HttpContext.Session.GetString("IsLoggedIn") == "true";
        if (!isLoggedIn)
        {
            Response.ContentType = "application/json";
            return Json(new { isLoggedIn = false });
        }
        
        Response.ContentType = "application/json";
        return Json(new
        {
            isLoggedIn = true,
            userId = HttpContext.Session.GetString("UserId"),
            email = HttpContext.Session.GetString("UserEmail"),
            name = HttpContext.Session.GetString("UserName")
        });
    }
    
    [HttpPost]
    [Route("Account/InitializeDatabase")]
    [Produces("application/json")]
    public async Task<IActionResult> InitializeDatabase()
    {
        try
        {
            // Initialize the database and collection
            var success = await _userService.InitializeDatabaseAsync();
            
            if (success)
            {
                Response.ContentType = "application/json";
                return Json(new 
                { 
                    success = true, 
                    message = "✅ Database 'MedicalAssistantDB' and collection 'Users' have been initialized! Refresh MongoDB Compass to see them." 
                });
            }
            else
            {
                Response.ContentType = "application/json";
                Response.StatusCode = 500;
                return Json(new 
                { 
                    success = false, 
                    errors = new[] { "Failed to initialize database. Please ensure MongoDB is running." } 
                });
            }
        }
        catch (Exception ex)
        {
            Response.ContentType = "application/json";
            Response.StatusCode = 500;
            return Json(new 
            { 
                success = false, 
                errors = new[] { $"Failed to initialize database: {ex.Message}" } 
            });
        }
    }
}

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    
    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public int? Age { get; set; }
    
    public string? Address { get; set; }
    
    [Phone]
    public string? PhoneNumber { get; set; }
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
