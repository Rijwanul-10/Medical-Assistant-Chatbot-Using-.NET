using Microsoft.AspNetCore.Identity;

namespace MedicalAssistant.Models;

public class ApplicationUser : IdentityUser
{
    public string? Name { get; set; }
    public int? Age { get; set; }
    public string? Address { get; set; }
    public new string? PhoneNumber { get; set; }
    
    // Navigation properties
    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

