using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MedicalAssistant.Data;
using MedicalAssistant.Models;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace MedicalAssistant.Services;

public class GroqChatbotService : IChatbotService
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;
    private readonly IDoctorService _doctorService;
    private readonly string _apiKey;
    private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";
    
    public GroqChatbotService(
        IConfiguration configuration,
        ApplicationDbContext context,
        IDoctorService doctorService,
        IHttpClientFactory httpClientFactory)
    {
        _apiKey = configuration["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq API key not configured");
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _context = context;
        _doctorService = doctorService;
    }
    
    public async Task<ChatbotResponse> ProcessMessageAsync(string userMessage, string userId, ApplicationDbContext context, ConversationState? state = null)
    {
        state ??= new ConversationState();
        var lowerMessage = userMessage.ToLower();
        var response = new ChatbotResponse { State = state };
        
        // STEP 1: Problem Understanding - Check if user shared health problem
        if (state.CurrentStep == "greeting" || state.CurrentStep == "problem")
        {
            var symptoms = await ExtractSymptomsAsync(userMessage);
            if (symptoms.Any() || IsHealthProblem(userMessage))
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
            }
            
            // If no disease detected but user seems to have a problem, ask for more details
            if (IsHealthProblem(userMessage))
            {
                response.Response = "I understand you're not feeling well. Could you please describe your symptoms in more detail? For example, do you have fever, headache, pain, or any other specific symptoms?";
                state.CurrentStep = "problem";
                return response;
            }
        }
        
        // STEP 2: Location - User provided location
        if (state.CurrentStep == "location" && IsLocation(userMessage))
        {
            state.UserLocation = ExtractLocation(userMessage);
            state.CurrentStep = "recommendation";
            
            // Get doctor recommendation
            if (!string.IsNullOrEmpty(state.DetectedDisease))
            {
                var specialty = _doctorService.MapDiseaseToSpecialty(state.DetectedDisease, context);
                var doctors = await _doctorService.GetRecommendedDoctorsAsync(specialty, state.UserLocation, context);
                
                if (!doctors.Any())
                {
                    doctors = await _doctorService.GetRecommendedDoctorsAsync(specialty, null, context);
                }
                
                if (doctors.Any())
                {
                    var doctor = doctors.First();
                    state.RecommendedDoctorId = doctor.Id;
                    response.RecommendedDoctorId = doctor.Id;
                    
                    response.Response = $"I recommend consulting a **{specialty}** specialist.\n\n";
                    response.Response += $"**Dr. {doctor.DoctorName}**\n";
                    if (!string.IsNullOrEmpty(doctor.Chamber))
                        response.Response += $"Chamber: {doctor.Chamber}\n";
                    if (!string.IsNullOrEmpty(doctor.Location))
                        response.Response += $"Location: {doctor.Location}\n";
                    if (doctor.Experience.HasValue)
                        response.Response += $"Experience: {doctor.Experience} years\n\n";
                    response.Response += "Would you like to book an appointment?";
                    
                    state.CurrentStep = "recommendation";
                    return response;
                }
            }
            
            response.Response = "I couldn't find a suitable doctor at the moment. Please try again later.";
            return response;
        }
        
        // STEP 3: Doctor Recommendation - Already done, waiting for user response
        if (state.CurrentStep == "recommendation")
        {
            if (lowerMessage.Contains("yes") || lowerMessage.Contains("book") || lowerMessage.Contains("appointment"))
            {
                if (state.RecommendedDoctorId.HasValue)
                {
                    state.CurrentStep = "booking";
                    response.RecommendedDoctorId = state.RecommendedDoctorId.Value;
                    response.Response = "Great! I'm opening the payment window to confirm your appointment.";
                    return response;
                }
            }
            else if (lowerMessage.Contains("no") || lowerMessage.Contains("not") || lowerMessage.Contains("decline"))
            {
                response.Response = "No worries at all 😊 If you need any help later, feel free to chat with me anytime. Take care and stay healthy 🌿";
                state.CurrentStep = "greeting";
                return response;
            }
        }
        
        // STEP 4 & 5: Booking and Payment - Handled by controller
        
        // Default: Use Groq for friendly conversation
        var groqResponse = await GetGroqResponseAsync(userMessage, userId, context, state);
        response.Response = groqResponse;
        
        return response;
    }
    
    private async Task<string> GetGroqResponseAsync(string userMessage, string userId, ApplicationDbContext context, ConversationState state)
    {
        // Get conversation history
        var recentMessages = await context.ChatMessages
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Timestamp)
            .Take(10)
            .OrderBy(m => m.Timestamp)
            .Select(m => new { m.Message, m.IsFromUser })
            .ToListAsync();
        
        // Build system prompt
        var systemPrompt = BuildSystemPrompt(state);
        
        // Build conversation context
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };
        
        // Add recent conversation history
        foreach (var msg in recentMessages)
        {
            messages.Add(new
            {
                role = msg.IsFromUser ? "user" : "assistant",
                content = msg.Message
            });
        }
        
        // Add current user message
        messages.Add(new
        {
            role = "user",
            content = userMessage
        });
        
        try
        {
            var requestBody = new
            {
                model = "llama-3.1-70b-versatile",
                messages = messages,
                temperature = 0.7,
                max_tokens = 300
            };
            
            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(GroqApiUrl, content);
            response.EnsureSuccessStatusCode();
            
            var responseString = await response.Content.ReadAsStringAsync();
            var groqResponse = JsonConvert.DeserializeObject<GroqApiResponse>(responseString);
            
            if (groqResponse?.Choices != null && groqResponse.Choices.Count > 0)
            {
                var message = groqResponse.Choices[0].Message;
                if (message?.Content != null)
                {
                    return message.Content;
                }
            }
        }
        catch (Exception)
        {
            // Fallback
        }
        
        return await FallbackResponseAsync(userMessage, state);
    }
    
    private string BuildSystemPrompt(ConversationState state)
    {
        var prompt = @"You are ""Doctor Koi?"", a friendly, empathetic, and informal AI health assistant.

PERSONALITY:
- Friendly, informal, empathetic (like ChatGPT)
- Short, clear, human-like responses (2-4 sentences max)
- Use emojis naturally (😊, 😟, 💙, etc.)
- Be supportive and understanding

IMPORTANT RULES:
- ONLY respond when user shares a health-related problem
- DO NOT provide final medical diagnosis
- Always encourage professional doctor consultation
- Keep responses conversational and warm

Current conversation step: " + state.CurrentStep + @"
" + (state.DetectedDisease != null ? $"Detected disease: {state.DetectedDisease}" : "") + @"
" + (state.UserLocation != null ? $"User location: {state.UserLocation}" : "");

        return prompt;
    }
    
    private bool IsHealthProblem(string message)
    {
        var lower = message.ToLower();
        var healthKeywords = new[] { "fever", "headache", "pain", "sick", "ill", "symptom", "unwell", "feeling", "hurt", "ache", "cough", "nausea", "dizzy", "tired", "fatigue" };
        return healthKeywords.Any(keyword => lower.Contains(keyword));
    }
    
    private bool IsLocation(string message)
    {
        var lower = message.ToLower();
        // Common location indicators
        var locationKeywords = new[] { "dhanmondi", "gulshan", "banani", "uttara", "mirpur", "dhaka", "chittagong", "sylhet", "location", "area", "city", "live", "from" };
        return locationKeywords.Any(keyword => lower.Contains(keyword)) || message.Length < 50; // Short messages are likely locations
    }
    
    private string ExtractLocation(string message)
    {
        // Simple extraction - can be improved
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Take(3)); // Take first few words as location
    }
    
    private async Task<string> FallbackResponseAsync(string userMessage, ConversationState state)
    {
        var lowerMessage = userMessage.ToLower();
        
        if (IsGreeting(lowerMessage))
        {
            return "Hello! 😄 I'm Doctor Koi, your AI health assistant. How can I help you today?";
        }
        
        if (IsHealthProblem(lowerMessage))
        {
            return "I'm sorry you're feeling unwell 😟 Please describe your symptoms, and I'll help you identify the possible condition and recommend a suitable doctor.";
        }
        
        return "I'm here to help! 😊 Please tell me about your health concerns or symptoms, and I'll assist you.";
    }
    
    public async Task<List<string>> ExtractSymptomsAsync(string message)
    {
        var symptoms = new List<string>();
        var lowerMessage = message.ToLower();
        
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
            { "chills", "chills" },
            { "severe", "severe" }
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

// Groq API Response Models
public class GroqApiResponse
{
    [JsonProperty("choices")]
    public List<GroqChoice>? Choices { get; set; }
}

public class GroqChoice
{
    [JsonProperty("message")]
    public GroqMessage? Message { get; set; }
}

public class GroqMessage
{
    [JsonProperty("content")]
    public string? Content { get; set; }
}
