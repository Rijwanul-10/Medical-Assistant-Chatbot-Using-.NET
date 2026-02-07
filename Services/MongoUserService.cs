using MongoDB.Driver;
using MedicalAssistant.Models;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace MedicalAssistant.Services;

/// <summary>
/// MongoDB User Service - Handles user authentication and profile management
/// Uses MongoDB for user storage (NoSQL database)
/// Provides methods for user CRUD operations, password hashing, and authentication
/// </summary>
public class MongoUserService
{
    private readonly IMongoCollection<MongoUser> _users;
    
    /// <summary>
    /// Initializes MongoDB user service
    /// Creates unique index on email field to prevent duplicate registrations
    /// </summary>
    /// <param name="database">MongoDB database instance</param>
    public MongoUserService(IMongoDatabase database)
    {
        try
        {
            // Get or create "Users" collection
            _users = database.GetCollection<MongoUser>("Users");
            
            // Create unique index on email field
            // This ensures no two users can have the same email address
            // Also ensures the collection exists (MongoDB creates collections lazily)
            try
            {
                var indexOptions = new CreateIndexOptions { Unique = true };
                var indexDefinition = Builders<MongoUser>.IndexKeys.Ascending(u => u.Email);
                var indexModel = new CreateIndexModel<MongoUser>(indexDefinition, indexOptions);
                _users.Indexes.CreateOne(indexModel);
            }
            catch
            {
                // Index might already exist - this is fine, ignore the error
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MongoDB initialization error: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Initializes the database and collection by ensuring they exist.
    /// This makes the database visible in MongoDB Compass.
    /// </summary>
    public async Task<bool> InitializeDatabaseAsync()
    {
        try
        {
            // Ensure collection exists by creating index (if not already exists)
            // This will create the collection and database if they don't exist
            var indexOptions = new CreateIndexOptions { Unique = true };
            var indexDefinition = Builders<MongoUser>.IndexKeys.Ascending(u => u.Email);
            var indexModel = new CreateIndexModel<MongoUser>(indexDefinition, indexOptions);
            
            // This will create the collection if it doesn't exist
            await _users.Indexes.CreateOneAsync(indexModel);
            
            // Verify the database exists by listing collections
            var collections = await _users.Database.ListCollectionNamesAsync();
            await collections.ToListAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing database: {ex.Message}");
            return false;
        }
    }
    
    public async Task<MongoUser?> GetUserByEmailAsync(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }
            
            var filter = Builders<MongoUser>.Filter.Eq(u => u.Email, email.ToLower().Trim());
            return await _users.Find(filter).FirstOrDefaultAsync();
        }
        catch (MongoConnectionException ex)
        {
            System.Diagnostics.Debug.WriteLine($"MongoDB connection error: {ex.Message}");
            throw new Exception("Cannot connect to database. Please ensure MongoDB is running.");
        }
        catch (MongoException ex)
        {
            System.Diagnostics.Debug.WriteLine($"MongoDB error getting user: {ex.Message}");
            throw new Exception($"Database error: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting user by email: {ex.Message}");
            throw new Exception($"Error accessing database: {ex.Message}");
        }
    }
    
    public async Task<MongoUser?> GetUserByIdAsync(string id)
    {
        return await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
    }
    
    public async Task<(bool Success, string? UserId, string? Error)> CreateUserAsync(MongoUser user)
    {
        try
        {
            // Check if user already exists
            var existingUser = await GetUserByEmailAsync(user.Email);
            if (existingUser != null)
            {
                return (false, null, "Email already registered");
            }
            
            user.Email = user.Email.ToLower();
            user.CreatedAt = DateTime.UtcNow;
            await _users.InsertOneAsync(user);
            
            // Get the inserted user ID
            var insertedUser = await GetUserByEmailAsync(user.Email);
            if (insertedUser != null)
            {
                return (true, insertedUser.Id, null);
            }
            
            return (false, null, "User created but ID not found");
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            // Duplicate key error
            return (false, null, "Email already registered");
        }
        catch (MongoConnectionException ex)
        {
            System.Diagnostics.Debug.WriteLine($"MongoDB connection error: {ex.Message}");
            throw new Exception("Cannot connect to database. Please ensure MongoDB is running.");
        }
        catch (MongoException ex)
        {
            System.Diagnostics.Debug.WriteLine($"MongoDB error creating user: {ex.Message}");
            throw new Exception($"Database error: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating user: {ex.Message}");
            throw;
        }
    }
    
    public async Task<bool> UpdateUserAsync(MongoUser user)
    {
        var result = await _users.ReplaceOneAsync(u => u.Id == user.Id, user);
        return result.ModifiedCount > 0;
    }
    
    public async Task UpdateLastLoginAsync(string email)
    {
        var filter = Builders<MongoUser>.Filter.Eq(u => u.Email, email.ToLower());
        var update = Builders<MongoUser>.Update.Set(u => u.LastLogin, DateTime.UtcNow);
        await _users.UpdateOneAsync(filter, update);
    }
    
    public string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
    
    public bool VerifyPassword(string password, string passwordHash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput == passwordHash;
    }
}
