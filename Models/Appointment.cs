using System.ComponentModel.DataAnnotations;

namespace MedicalAssistant.Models;

public class Appointment
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public int DoctorId { get; set; }
    
    public DateTime AppointmentDate { get; set; } = DateTime.UtcNow;
    
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled
    
    public decimal Amount { get; set; }
    
    public string? PaymentIntentId { get; set; }
    
    public bool IsPaid { get; set; } = false;
    
    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
    public virtual Doctor Doctor { get; set; } = null!;
}

