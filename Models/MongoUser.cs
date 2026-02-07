using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MedicalAssistant.Models;

public class MongoUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;
    
    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;
    
    [BsonElement("name")]
    public string? Name { get; set; }
    
    [BsonElement("age")]
    public int? Age { get; set; }
    
    [BsonElement("address")]
    public string? Address { get; set; }
    
    [BsonElement("phoneNumber")]
    public string? PhoneNumber { get; set; }
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [BsonElement("lastLogin")]
    public DateTime? LastLogin { get; set; }
}
