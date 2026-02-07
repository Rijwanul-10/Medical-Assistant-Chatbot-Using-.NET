using Microsoft.EntityFrameworkCore;
using MedicalAssistant.Data;
using MedicalAssistant.Models;

namespace MedicalAssistant.Services;

public class ChatbotService : IChatbotService
{
    public async Task<ChatbotResponse> ProcessMessageAsync(string userMessage, string userId, ApplicationDbContext context, ConversationState? state = null)
    {
        state ??= new ConversationState();
        var lowerMessage = userMessage.ToLower();
        var response = new ChatbotResponse { State = state };
        
        // Greeting detection
        if (IsGreeting(lowerMessage))
        {
            response.Response = "Hello! 😄 I'm Doctor Koi, your AI health assistant. How can I help you today?";
            state.CurrentStep = "greeting";
            return response;
        }
        
        // Check if user is sharing symptoms
        var symptoms = await ExtractSymptomsAsync(userMessage);
        
        if (symptoms.Any())
        {
            var disease = await IdentifyDiseaseAsync(symptoms, context);
            
            if (disease != null)
            {
                var diseaseDescription = await context.Diseases
                    .Where(d => d.DiseaseName == disease.DiseaseName)
                    .Select(d => d.Description)
                    .FirstOrDefaultAsync();
                
                response.DetectedDisease = disease.DiseaseName;
                state.DetectedDisease = disease.DiseaseName;
                state.CurrentStep = "location";
                
                response.Response = $"Based on your symptoms, this could be **{disease.DiseaseName}**.\n\n";
                if (!string.IsNullOrEmpty(diseaseDescription))
                {
                    response.Response += $"{diseaseDescription}\n\n";
                }
                response.Response += "To help you better, could you please tell me your location?";
                response.RequiresLocation = true;
                
                return response;
            }
            else
            {
                response.Response = "I understand you're experiencing some symptoms. 😟\nIt's important to consult with a doctor for proper diagnosis. Could you please describe your symptoms in more detail?";
                state.CurrentStep = "problem";
                return response;
            }
        }
        
        // Default response
        response.Response = "I'm here to help! 😊\nPlease tell me about your symptoms or health concerns, and I'll do my best to assist you.";
        return response;
    }
    
    public async Task<List<string>> ExtractSymptomsAsync(string message)
    {
        var symptoms = new List<string>();
        var lowerMessage = message.ToLower();
        
        // Common symptom keywords
        var symptomKeywords = new Dictionary<string, string>
        {
            { "fever", "fever" },
            { "headache", "headache" },
            { "pain", "pain" },
            { "cough", "cough" },
            { "nausea", "nausea" },
            { "vomiting", "vomiting" },
            { "diarrhea", "diarrhea" },
            { "dizziness", "dizziness" },
            { "fatigue", "fatigue" },
            { "chest pain", "chest pain" },
            { "shortness of breath", "shortness of breath" },
            { "abdominal pain", "abdominal pain" },
            { "back pain", "back pain" },
            { "joint pain", "joint pain" },
            { "rash", "rash" },
            { "itchy", "itchy" },
            { "sore throat", "sore throat" },
            { "runny nose", "runny nose" },
            { "congestion", "congestion" },
            { "chills", "chills" }
        };
        
        foreach (var keyword in symptomKeywords.Keys)
        {
            if (lowerMessage.Contains(keyword))
            {
                symptoms.Add(symptomKeywords[keyword]);
            }
        }
        
        return symptoms;
    }
    
    public async Task<Disease?> IdentifyDiseaseAsync(List<string> symptoms, ApplicationDbContext context)
    {
        if (!symptoms.Any()) return null;
        
        // Get all diseases with their symptoms
        var diseases = await context.Diseases
            .Include(d => d.DiseaseSymptoms)
            .ThenInclude(ds => ds.Symptom)
            .ToListAsync();
        
        var diseaseScores = new Dictionary<Disease, int>();
        
        foreach (var disease in diseases)
        {
            int score = 0;
            var diseaseSymptomNames = disease.DiseaseSymptoms
                .Where(ds => ds.IsPresent)
                .Select(ds => ds.Symptom.SymptomName.ToLower())
                .ToList();
            
            foreach (var symptom in symptoms)
            {
                if (diseaseSymptomNames.Any(ds => ds.Contains(symptom) || symptom.Contains(ds)))
                {
                    score++;
                }
            }
            
            if (score > 0)
            {
                diseaseScores[disease] = score;
            }
        }
        
        // Return disease with highest score
        if (diseaseScores.Any())
        {
            return diseaseScores.OrderByDescending(ds => ds.Value).First().Key;
        }
        
        return null;
    }
    
    public string GenerateFriendlyResponse(string diseaseName, string? diseaseDescription, string userName)
    {
        var response = $"I'm sorry you're feeling unwell, {userName} 😟\n\n";
        response += $"Based on your symptoms, this could be **{diseaseName}**.\n\n";
        
        if (!string.IsNullOrEmpty(diseaseDescription))
        {
            response += $"{diseaseDescription}\n\n";
        }
        
        response += "It's important to consult a doctor soon.\n\n";
        response += "Would you like me to recommend a suitable doctor for you?";
        
        return response;
    }
    
    private bool IsGreeting(string message)
    {
        var greetings = new[] { "hi", "hello", "hey", "greetings", "whatsapp", "what's up" };
        return greetings.Any(g => message.Contains(g));
    }
}

