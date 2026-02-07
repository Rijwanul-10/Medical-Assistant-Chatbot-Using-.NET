using System.ComponentModel.DataAnnotations;

namespace MedicalAssistant.Models;

public class ChatMessage
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    public bool IsFromUser { get; set; } = true;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public string? DetectedDisease { get; set; }
    public int? RecommendedDoctorId { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
    public virtual Doctor? RecommendedDoctor { get; set; }
}

