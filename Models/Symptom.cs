namespace MedicalAssistant.Models;

public class Symptom
{
    public int Id { get; set; }
    public string SymptomName { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual ICollection<DiseaseSymptom> DiseaseSymptoms { get; set; } = new List<DiseaseSymptom>();
}

