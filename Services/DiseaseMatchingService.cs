using MedicalAssistant.Data;
using MedicalAssistant.Models;
using Microsoft.EntityFrameworkCore;
using CsvHelper;
using System.Globalization;

namespace MedicalAssistant.Services;

public class DiseaseMatchingService
{
    private static Dictionary<string, List<List<string>>>? _diseaseSymptomsCache = null;
    private static readonly object _lockObject = new object();

    // Load Original_Dataset.csv into memory for fast matching
    public static void LoadDiseaseSymptomsCache()
    {
        if (_diseaseSymptomsCache != null) return;

        lock (_lockObject)
        {
            if (_diseaseSymptomsCache != null) return;

            _diseaseSymptomsCache = new Dictionary<string, List<List<string>>>();
            var datasetPath = Path.Combine("Datasets", "Original_Dataset.csv");

            if (!File.Exists(datasetPath))
            {
                System.Diagnostics.Debug.WriteLine($"Original_Dataset.csv not found at {datasetPath}");
                return;
            }

            try
            {
                using var reader = new StreamReader(datasetPath);
                using var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);

                csv.Read();
                csv.ReadHeader();

                int rowCount = 0;
                while (csv.Read())
                {
                    rowCount++;
                    var diseaseName = csv.GetField("Disease")?.Trim() ?? "";

                    if (string.IsNullOrEmpty(diseaseName)) continue;

                    var symptoms = new List<string>();
                    for (int i = 1; i <= 17; i++)
                    {
                        var symptom = csv.GetField($"Symptom_{i}")?.Trim();
                        if (!string.IsNullOrEmpty(symptom))
                        {
                            symptoms.Add(symptom.ToLower().Replace("_", " ").Trim());
                        }
                    }

                    if (symptoms.Any())
                    {
                        if (!_diseaseSymptomsCache.ContainsKey(diseaseName))
                        {
                            _diseaseSymptomsCache[diseaseName] = new List<List<string>>();
                        }
                        _diseaseSymptomsCache[diseaseName].Add(symptoms);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Loaded {_diseaseSymptomsCache.Count} diseases with {rowCount} symptom combinations from Original_Dataset.csv");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading Original_Dataset.csv: {ex.Message}");
                _diseaseSymptomsCache = new Dictionary<string, List<List<string>>>();
            }
        }
    }

    // Match user symptoms to diseases using Original_Dataset.csv
    public static string? MatchDiseaseBySymptoms(List<string> userSymptoms, ApplicationDbContext context)
    {
        LoadDiseaseSymptomsCache();

        if (_diseaseSymptomsCache == null || !_diseaseSymptomsCache.Any())
        {
            System.Diagnostics.Debug.WriteLine("Disease symptoms cache is empty");
            return null;
        }

        if (!userSymptoms.Any()) return null;

        // Normalize user symptoms
        var normalizedUserSymptoms = userSymptoms
            .Select(s => s.ToLower().Replace("_", " ").Trim())
            .Where(s => s.Length > 2)
            .ToList();

        if (!normalizedUserSymptoms.Any()) return null;

        var diseaseScores = new Dictionary<string, int>();

        foreach (var disease in _diseaseSymptomsCache.Keys)
        {
            int maxScore = 0;

            // Check each symptom combination for this disease
            foreach (var symptomCombination in _diseaseSymptomsCache[disease])
            {
                int score = 0;
                int matchedSymptoms = 0;

                foreach (var userSymptom in normalizedUserSymptoms)
                {
                    // Check if user symptom matches any disease symptom
                    foreach (var diseaseSymptom in symptomCombination)
                    {
                        if (diseaseSymptom.Contains(userSymptom) || 
                            userSymptom.Contains(diseaseSymptom) ||
                            diseaseSymptom.Split(' ').Any(word => userSymptom.Contains(word)) ||
                            userSymptom.Split(' ').Any(word => diseaseSymptom.Contains(word)))
                        {
                            score++;
                            matchedSymptoms++;
                            break; // Count each user symptom only once
                        }
                    }
                }

                // Bonus for matching more symptoms
                if (matchedSymptoms > 0)
                {
                    score += matchedSymptoms * 2; // Weight matches more
                }

                maxScore = Math.Max(maxScore, score);
            }

            if (maxScore > 0)
            {
                diseaseScores[disease] = maxScore;
            }
        }

        if (diseaseScores.Any())
        {
            var bestMatch = diseaseScores.OrderByDescending(ds => ds.Value).First();
            System.Diagnostics.Debug.WriteLine($"Matched disease '{bestMatch.Key}' with score {bestMatch.Value} for symptoms: {string.Join(", ", userSymptoms)}");
            return bestMatch.Key;
        }

        return null;
    }
}

