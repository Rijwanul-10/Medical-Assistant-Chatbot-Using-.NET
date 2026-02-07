using Microsoft.EntityFrameworkCore;
using MedicalAssistant.Data;
using MedicalAssistant.Models;

namespace MedicalAssistant.Services;

public class DoctorService : IDoctorService
{
    public async Task<List<Doctor>> GetRecommendedDoctorsAsync(string specialty, string? userLocation, ApplicationDbContext context)
    {
        try
        {
            // Check if we have doctors in database
            var hasDoctors = await context.Doctors.AnyAsync();
            if (!hasDoctors)
            {
                System.Diagnostics.Debug.WriteLine("No doctors found in database");
                return new List<Doctor>();
            }
            
            var query = context.Doctors.AsQueryable();
            
            // Filter by specialty (case-insensitive, more flexible matching)
            if (!string.IsNullOrEmpty(specialty))
            {
                var specialtyLower = specialty.ToLower().Trim();
                query = query.Where(d => 
                    d.Speciality != null && 
                    (d.Speciality.ToLower().Contains(specialtyLower) ||
                     specialtyLower.Contains(d.Speciality.ToLower())));
            }
            
            // Filter by location if provided (more flexible matching - prioritize location)
            if (!string.IsNullOrEmpty(userLocation))
            {
                var locationLower = userLocation.ToLower().Trim();
                
                // Use simpler matching that EF Core can translate
                query = query.Where(d => 
                    d.Location != null && 
                    (d.Location.ToLower().Contains(locationLower) ||
                     locationLower.Contains(d.Location.ToLower())));
            }
            
            // Order by experience (highest first), then by name
            var doctors = await query
                .OrderByDescending(d => d.Experience)
                .ThenBy(d => d.DoctorName)
                .Take(5)
                .ToListAsync();
            
            System.Diagnostics.Debug.WriteLine($"Found {doctors.Count} doctors for specialty: {specialty}, location: {userLocation}");
            
            // If no doctors found with specialty, try without specialty filter
            if (!doctors.Any() && !string.IsNullOrEmpty(specialty))
            {
                System.Diagnostics.Debug.WriteLine("No doctors found with specialty filter, trying without specialty");
                query = context.Doctors.AsQueryable();
                
                if (!string.IsNullOrEmpty(userLocation))
                {
                    var locationLower = userLocation.ToLower();
                    query = query.Where(d => 
                        d.Location != null && 
                        (d.Location.ToLower().Contains(locationLower) ||
                         locationLower.Contains(d.Location.ToLower())));
                }
                
                doctors = await query
                    .OrderByDescending(d => d.Experience)
                    .ThenBy(d => d.DoctorName)
                    .Take(5)
                    .ToListAsync();
                
                System.Diagnostics.Debug.WriteLine($"Found {doctors.Count} doctors without specialty filter");
            }
            
            // If still no doctors, try any doctor at all
            if (!doctors.Any())
            {
                System.Diagnostics.Debug.WriteLine("No doctors found with any filter, trying any doctor");
                doctors = await context.Doctors
                    .OrderByDescending(d => d.Experience)
                    .ThenBy(d => d.DoctorName)
                    .Take(5)
                    .ToListAsync();
                
                System.Diagnostics.Debug.WriteLine($"Found {doctors.Count} doctors without any filter");
            }
            
            return doctors;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting recommended doctors: {ex.Message}");
            return new List<Doctor>();
        }
    }
    
    public async Task<Doctor?> GetDoctorByIdAsync(int doctorId, ApplicationDbContext context)
    {
        return await context.Doctors.FindAsync(doctorId);
    }
    
    public string MapDiseaseToSpecialty(string diseaseName, ApplicationDbContext context)
    {
        try
        {
            var disease = context.Diseases
                .FirstOrDefault(d => d.DiseaseName.Equals(diseaseName, StringComparison.OrdinalIgnoreCase));
            
            if (disease != null && !string.IsNullOrEmpty(disease.Specialist))
            {
                var specialist = disease.Specialist.Trim();
                System.Diagnostics.Debug.WriteLine($"Found specialist '{specialist}' for disease '{diseaseName}'");
                
                // Map specialist names from Disease_Specialist.csv to doctor specialties
                // Use exact match first, then flexible matching
                var specialistLower = specialist.ToLower();
                
                // Exact matches from Disease_Specialist.csv
                if (specialistLower.Contains("allergist")) return "Allergist";
                if (specialistLower.Contains("cardiologist")) return "Cardiologist";
                if (specialistLower.Contains("dermatologist")) return "Dermatologist";
                if (specialistLower.Contains("endocrinologist")) return "Endocrinologist";
                if (specialistLower.Contains("gastroenterologist")) return "Gastroenterologist";
                if (specialistLower.Contains("gynecologist") || specialistLower.Contains("gynae")) return "Gynecologist";
                if (specialistLower.Contains("hepatologist")) return "Hepatologist";
                if (specialistLower.Contains("neurologist")) return "Neurologist";
                if (specialistLower.Contains("pediatrician") || specialistLower.Contains("pediatric")) return "Pediatrician";
                if (specialistLower.Contains("pulmonologist")) return "Pulmonologist";
                if (specialistLower.Contains("rheumatologist") || specialistLower.Contains("rheumatologists")) return "Rheumatologist";
                if (specialistLower.Contains("otolaryngologist") || specialistLower.Contains("ent")) return "ENT (Ear Nose Throat)";
                if (specialistLower.Contains("internal medicine") || specialistLower.Contains("internal medcine")) return "Internal Medicine";
                if (specialistLower.Contains("phlebologist")) return "Phlebologist";
                if (specialistLower.Contains("osteopathic")) return "Osteopathic";
                
                // Return the specialist name as-is (might match doctor specialties directly)
                return specialist;
            }
            
            // Fallback: try to infer from disease name
            var diseaseLower = diseaseName.ToLower();
            if (diseaseLower.Contains("heart") || diseaseLower.Contains("cardiac") || diseaseLower.Contains("hypertension")) return "Cardiologist";
            if (diseaseLower.Contains("skin") || diseaseLower.Contains("rash") || diseaseLower.Contains("acne") || diseaseLower.Contains("psoriasis")) return "Dermatologist";
            if (diseaseLower.Contains("stomach") || diseaseLower.Contains("digestive") || diseaseLower.Contains("gerd") || diseaseLower.Contains("ulcer")) return "Gastroenterologist";
            if (diseaseLower.Contains("hepatitis") || diseaseLower.Contains("liver") || diseaseLower.Contains("jaundice")) return "Hepatologist";
            if (diseaseLower.Contains("diabetes") || diseaseLower.Contains("thyroid") || diseaseLower.Contains("hypoglycemia")) return "Endocrinologist";
            if (diseaseLower.Contains("asthma") || diseaseLower.Contains("pneumonia") || diseaseLower.Contains("tuberculosis")) return "Pulmonologist";
            if (diseaseLower.Contains("migraine") || diseaseLower.Contains("vertigo") || diseaseLower.Contains("paralysis")) return "Neurologist";
            if (diseaseLower.Contains("arthritis") || diseaseLower.Contains("osteoarthristis")) return "Rheumatologist";
            if (diseaseLower.Contains("fever") || diseaseLower.Contains("flu") || diseaseLower.Contains("cold") || diseaseLower.Contains("malaria") || diseaseLower.Contains("dengue") || diseaseLower.Contains("typhoid")) return "Internal Medicine";
            
            System.Diagnostics.Debug.WriteLine($"No specialist found for disease: {diseaseName}, using General Medicine");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error mapping disease to specialty: {ex.Message}");
        }
        
        return "General Medicine";
    }
}

