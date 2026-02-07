using MedicalAssistant.Data;
using MedicalAssistant.Models;

namespace MedicalAssistant.Services;

public interface IDoctorService
{
    Task<List<Doctor>> GetRecommendedDoctorsAsync(string specialty, string? userLocation, ApplicationDbContext context);
    Task<Doctor?> GetDoctorByIdAsync(int doctorId, ApplicationDbContext context);
    string MapDiseaseToSpecialty(string diseaseName, ApplicationDbContext context);
}

