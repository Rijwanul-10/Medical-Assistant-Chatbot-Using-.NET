using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MedicalAssistant.Models;
using CsvHelper;
using System.Globalization;

namespace MedicalAssistant.Data;

public static class SeedData
{
    public static async Task InitializeAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // Create roles if they don't exist
        if (!await roleManager.RoleExistsAsync("User"))
        {
            await roleManager.CreateAsync(new IdentityRole("User"));
        }
        
        // Load doctors from CSV (with comprehensive error handling)
        try
        {
            // Check if we can access the table
            var canAccess = await context.Database.CanConnectAsync();
            if (canAccess)
            {
                // Check if Doctors table exists by trying to query it
                try
                {
                    var hasDoctors = await context.Doctors.AnyAsync();
                    if (!hasDoctors)
                    {
                        await LoadDoctorsFromCsv(context);
                    }
                }
                catch (Exception tableEx)
                {
                    // Table might not exist yet - that's okay
                    Console.WriteLine($"Doctors table not ready: {tableEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Silently skip - seeding is non-critical
            Console.WriteLine($"Could not load doctors: {ex.Message}");
        }
        
        // Load diseases and symptoms from CSV (with comprehensive error handling)
        try
        {
            // Check if we can access the table
            var canAccess = await context.Database.CanConnectAsync();
            if (canAccess)
            {
                // Check if Diseases table exists by trying to query it
                try
                {
                    var hasDiseases = await context.Diseases.AnyAsync();
                    if (!hasDiseases)
                    {
                        await LoadDiseasesFromCsv(context);
                    }
                }
                catch (Exception tableEx)
                {
                    // Table might not exist yet - that's okay
                    Console.WriteLine($"Diseases table not ready: {tableEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Silently skip - seeding is non-critical
            Console.WriteLine($"Could not load diseases: {ex.Message}");
        }
    }
    
    private static async Task LoadDoctorsFromCsv(ApplicationDbContext context)
    {
        var doctorsFilePath = Path.Combine("Datasets", "doctors_info_1.csv");
        
        if (!File.Exists(doctorsFilePath))
        {
            return;
        }
        
        using var reader = new StreamReader(doctorsFilePath);
        using var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);
        
        csv.Context.RegisterClassMap<DoctorMap>();
        
        var doctors = csv.GetRecords<Doctor>().ToList();
        
        // Also try to load from doctors_info_2.csv
        var doctors2FilePath = Path.Combine("Datasets", "doctors_info_2.csv");
        if (File.Exists(doctors2FilePath))
        {
            using var reader2 = new StreamReader(doctors2FilePath);
            using var csv2 = new CsvHelper.CsvReader(reader2, CultureInfo.InvariantCulture);
            
            // Skip header
            await csv2.ReadAsync();
            csv2.ReadHeader();
            
            while (await csv2.ReadAsync())
            {
                var doctor = new Doctor
                {
                    DoctorName = csv2.GetField("Doctor Name") ?? "",
                    Education = csv2.GetField("Education"),
                    Speciality = csv2.GetField("Speciality"),
                    Experience = ParseDouble(csv2.GetField("Experience")),
                    Chamber = csv2.GetField("Chamber"),
                    Location = csv2.GetField("Location"),
                    Concentration = csv2.GetField("Concentration"),
                    MBBS = ParseBool(csv2.GetField("MBBS")),
                    FCPS = ParseBool(csv2.GetField("FCPS")),
                    BCS = ParseBool(csv2.GetField("BCS")),
                    MD = ParseBool(csv2.GetField("MD")),
                    MS = ParseBool(csv2.GetField("MS")),
                    MCPS = ParseBool(csv2.GetField("MCPS")),
                    CCD = ParseBool(csv2.GetField("CCD")),
                    PGT = ParseBool(csv2.GetField("PGT")),
                    BDS = ParseBool(csv2.GetField("BDS")),
                    MPH = ParseBool(csv2.GetField("MPH"))
                };
                
                if (!doctors.Any(d => d.DoctorName == doctor.DoctorName && d.Location == doctor.Location))
                {
                    doctors.Add(doctor);
                }
            }
        }
        
        // Also load from doc.csv
        var docFilePath = Path.Combine("Datasets", "doc.csv");
        if (File.Exists(docFilePath))
        {
            Console.WriteLine($"Loading doctors from {docFilePath}");
            using var reader3 = new StreamReader(docFilePath);
            using var csv3 = new CsvHelper.CsvReader(reader3, CultureInfo.InvariantCulture);
            
            await csv3.ReadAsync();
            csv3.ReadHeader();
            
            int docCount = 0;
            while (await csv3.ReadAsync())
            {
                try
                {
                    var doctorName = csv3.GetField("Name")?.Trim() ?? "";
                    var specialty = csv3.GetField("Specialty")?.Trim();
                    var hospital = csv3.GetField("Hospital")?.Trim();
                    var district = csv3.GetField("District")?.Trim();
                    
                    if (!string.IsNullOrEmpty(doctorName))
                    {
                        var doctor = new Doctor
                        {
                            DoctorName = doctorName,
                            Speciality = specialty,
                            Chamber = hospital,
                            Location = district
                        };
                        
                        // Check if doctor already exists
                        if (!doctors.Any(d => d.DoctorName == doctor.DoctorName && 
                                            (string.IsNullOrEmpty(doctor.Location) || d.Location == doctor.Location)))
                        {
                            doctors.Add(doctor);
                            docCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading doctor from doc.csv: {ex.Message}");
                    continue;
                }
            }
            Console.WriteLine($"Loaded {docCount} doctors from doc.csv");
        }
        
        context.Doctors.AddRange(doctors);
        await context.SaveChangesAsync();
    }
    
    private static async Task LoadDiseasesFromCsv(ApplicationDbContext context)
    {
        var diseases = new Dictionary<string, Disease>();
        
        try
        {
            // Step 1: Load disease descriptions (simplest approach - just load from description file)
            var diseaseDescPath = Path.Combine("Datasets", "disease_description.csv");
            if (File.Exists(diseaseDescPath))
            {
                Console.WriteLine($"Loading diseases from {diseaseDescPath}");
                using var reader = new StreamReader(diseaseDescPath);
                using var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);
                
                await csv.ReadAsync();
                csv.ReadHeader();
                
                int count = 0;
                while (await csv.ReadAsync())
                {
                    try
                    {
                        var diseaseName = csv.GetField("Disease")?.Trim() ?? "";
                        var description = csv.GetField("Description");
                        
                        if (!string.IsNullOrEmpty(diseaseName) && !diseases.ContainsKey(diseaseName))
                        {
                            diseases[diseaseName] = new Disease
                            {
                                DiseaseName = diseaseName,
                                Description = description
                            };
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading disease row: {ex.Message}");
                        continue;
                    }
                }
                Console.WriteLine($"Loaded {count} diseases from description file");
            }
            else
            {
                Console.WriteLine($"Disease description file not found: {diseaseDescPath}");
            }
            
            // Step 2: Load disease-specialist mapping
            var diseaseSpecialistPath = Path.Combine("Datasets", "Disease_Specialist.csv");
            if (File.Exists(diseaseSpecialistPath))
            {
                Console.WriteLine($"Loading specialists from {diseaseSpecialistPath}");
                using var reader = new StreamReader(diseaseSpecialistPath);
                using var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);
                
                await csv.ReadAsync();
                csv.ReadHeader();
                
                int count = 0;
                while (await csv.ReadAsync())
                {
                    try
                    {
                        var diseaseName = csv.GetField("Disease")?.Trim() ?? "";
                        var specialist = csv.GetField("Specialist")?.Trim();
                        
                        if (!string.IsNullOrEmpty(diseaseName))
                        {
                            if (!diseases.ContainsKey(diseaseName))
                            {
                                diseases[diseaseName] = new Disease { DiseaseName = diseaseName };
                            }
                            diseases[diseaseName].Specialist = specialist;
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading specialist row: {ex.Message}");
                        continue;
                    }
                }
                Console.WriteLine($"Updated {count} diseases with specialist info");
            }
            
            // Step 3: Save all diseases to database
            if (diseases.Any())
            {
                // Remove duplicates by checking existing diseases
                var existingDiseases = await context.Diseases
                    .Select(d => d.DiseaseName)
                    .ToListAsync();
                
                var newDiseases = diseases.Values
                    .Where(d => !existingDiseases.Contains(d.DiseaseName))
                    .ToList();
                
                if (newDiseases.Any())
                {
                    context.Diseases.AddRange(newDiseases);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"Saved {newDiseases.Count} diseases to database");
                }
                else
                {
                    Console.WriteLine("All diseases already exist in database");
                }
            }
            else
            {
                Console.WriteLine("No diseases loaded from CSV files");
            }
            
            // Step 4: Load symptoms and relationships (simplified - skip if too complex)
            var symptomsPath = Path.Combine("Datasets", "Disease and symptoms dataset.csv");
            if (File.Exists(symptomsPath) && diseases.Any())
            {
                Console.WriteLine($"Loading symptoms from {symptomsPath} (this may take a while...)");
                try
                {
                    // Just extract unique disease names from symptoms file
                    var uniqueDiseaseNames = new HashSet<string>();
                    using var reader = new StreamReader(symptomsPath);
                    string? line = await reader.ReadLineAsync(); // Skip header
                    
                    int lineCount = 0;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        lineCount++;
                        if (lineCount % 1000 == 0)
                        {
                            Console.WriteLine($"Processed {lineCount} lines...");
                        }
                        
                        var parts = line.Split(',');
                        if (parts.Length > 0)
                        {
                            var diseaseName = parts[0].Trim();
                            if (!string.IsNullOrEmpty(diseaseName) && 
                                diseaseName.ToLower() != "diseases" &&
                                !uniqueDiseaseNames.Contains(diseaseName))
                            {
                                uniqueDiseaseNames.Add(diseaseName);
                                
                                // Add disease if not already loaded
                                if (!diseases.ContainsKey(diseaseName))
                                {
                                    diseases[diseaseName] = new Disease { DiseaseName = diseaseName };
                                }
                            }
                        }
                    }
                    
                    // Save any new diseases found
                    var savedDiseaseNames = await context.Diseases
                        .Select(d => d.DiseaseName)
                        .ToListAsync();
                    
                    var additionalDiseases = diseases.Values
                        .Where(d => !savedDiseaseNames.Contains(d.DiseaseName))
                        .ToList();
                    
                    if (additionalDiseases.Any())
                    {
                        context.Diseases.AddRange(additionalDiseases);
                        await context.SaveChangesAsync();
                        Console.WriteLine($"Added {additionalDiseases.Count} additional diseases from symptoms file");
                    }
                    
                    Console.WriteLine($"Found {uniqueDiseaseNames.Count} unique diseases in symptoms file");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading symptoms file (non-critical): {ex.Message}");
                    // Continue - symptoms are optional
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading diseases: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            // Don't throw - just log the error
        }
    }
    
    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (double.TryParse(value, out var result)) return result;
        return null;
    }
    
    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (int.TryParse(value, out var intValue)) return intValue == 1;
        if (bool.TryParse(value, out var boolValue)) return boolValue;
        return null;
    }
}

// CsvHelper mapping for Doctor
public sealed class DoctorMap : CsvHelper.Configuration.ClassMap<Doctor>
{
    public DoctorMap()
    {
        Map(m => m.DoctorName).Name("Doctor Name");
        Map(m => m.Education).Name("Education");
        Map(m => m.Speciality).Name("Speciality");
        Map(m => m.Experience).Name("Experience");
        Map(m => m.Chamber).Name("Chamber");
        Map(m => m.Location).Name("Location");
        Map(m => m.Concentration).Name("Concentration");
    }
}

