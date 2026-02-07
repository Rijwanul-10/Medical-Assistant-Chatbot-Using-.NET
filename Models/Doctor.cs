namespace MedicalAssistant.Models;

public class Doctor
{
    public int Id { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string? Education { get; set; }
    public string? Speciality { get; set; }
    public double? Experience { get; set; }
    public string? Chamber { get; set; }
    public string? Location { get; set; }
    public string? Concentration { get; set; }
    public decimal ConsultationFee { get; set; } = 500; // Default fee
    
    // Additional fields from doctors_info_2
    public bool? MBBS { get; set; }
    public bool? FCPS { get; set; }
    public bool? BCS { get; set; }
    public bool? MD { get; set; }
    public bool? MS { get; set; }
    public bool? MCPS { get; set; }
    public bool? CCD { get; set; }
    public bool? PGT { get; set; }
    public bool? BDS { get; set; }
    public bool? MPH { get; set; }
    
    // Navigation properties
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

