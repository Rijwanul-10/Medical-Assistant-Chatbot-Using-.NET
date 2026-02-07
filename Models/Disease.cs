namespace MedicalAssistant.Models;

public class Disease
{
    public int Id { get; set; }
    public string DiseaseName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Specialist { get; set; }
    
    // Navigation properties
    public virtual ICollection<DiseaseSymptom> DiseaseSymptoms { get; set; } = new List<DiseaseSymptom>();
}

