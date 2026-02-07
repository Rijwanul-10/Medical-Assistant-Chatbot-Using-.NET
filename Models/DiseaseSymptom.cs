namespace MedicalAssistant.Models;

public class DiseaseSymptom
{
    public int Id { get; set; }
    public int DiseaseId { get; set; }
    public int SymptomId { get; set; }
    public bool IsPresent { get; set; }
    
    // Navigation properties
    public virtual Disease Disease { get; set; } = null!;
    public virtual Symptom Symptom { get; set; } = null!;
}

