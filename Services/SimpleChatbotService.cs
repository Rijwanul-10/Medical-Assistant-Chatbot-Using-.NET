using Microsoft.EntityFrameworkCore;
using MedicalAssistant.Data;
using MedicalAssistant.Models;
using System.Text;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace MedicalAssistant.Services;

public class SimpleChatbotService : IChatbotService
{
    private readonly HttpClient _httpClient;
    private readonly IDoctorService _doctorService;
    private readonly string _apiKey;
    private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";
    
    public SimpleChatbotService(
        IConfiguration configuration,
        IDoctorService doctorService,
        IHttpClientFactory httpClientFactory)
    {
        _apiKey = configuration["Groq:ApiKey"] ?? "";
        _httpClient = httpClientFactory.CreateClient();
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _doctorService = doctorService;
    }
    
    public async Task<ChatbotResponse> ProcessMessageAsync(string userMessage, string userId, ApplicationDbContext context, ConversationState? state = null)
    {
        try
        {
            state ??= new ConversationState();
            var response = new ChatbotResponse { State = state };
            var lowerMessage = userMessage.ToLower();
            
            // Step 1: Greeting
            if (IsGreeting(lowerMessage) || string.IsNullOrEmpty(state.CurrentStep))
            {
                response.Response = "Hello! 😄 I'm Doctor Koi, your AI health assistant. How can I help you today?";
                state.CurrentStep = "greeting";
                return response;
            }
            
            // Step 2: Check for health problem
            if (state.CurrentStep == "greeting" || state.CurrentStep == "problem")
            {
                var symptoms = await ExtractSymptomsAsync(userMessage);
                bool hasHealthProblem = symptoms.Any() || IsHealthProblem(userMessage);
                
                System.Diagnostics.Debug.WriteLine($"Extracted symptoms: {string.Join(", ", symptoms)}");
                System.Diagnostics.Debug.WriteLine($"Has health problem: {hasHealthProblem}");
                
                // Declare disease at higher scope
                Disease? disease = null;
                
                if (hasHealthProblem)
                {
                    // Strategy 1: Try direct disease name matching from user message
                    try
                    {
                        disease = await MatchDiseaseByNameAsync(userMessage, context);
                        if (disease != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Found disease by name match: {disease.DiseaseName}");
                        }
                    }
                    catch (Exception nameEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Disease name matching failed: {nameEx.Message}");
                    }
                    
                    // Strategy 2: Try symptom-based matching using Original_Dataset.csv
                    if (disease == null && symptoms.Any())
                    {
                        try
                        {
                            var matchedDiseaseName = DiseaseMatchingService.MatchDiseaseBySymptoms(symptoms, context);
                            if (!string.IsNullOrEmpty(matchedDiseaseName))
                            {
                                disease = await context.Diseases
                                    .FirstOrDefaultAsync(d => d.DiseaseName.Equals(matchedDiseaseName, StringComparison.OrdinalIgnoreCase));
                                System.Diagnostics.Debug.WriteLine($"Matched disease from Original_Dataset.csv: {(disease != null ? disease.DiseaseName : "null")}");
                            }
                        }
                        catch (Exception matchEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Disease matching from Original_Dataset.csv failed: {matchEx.Message}");
                        }
                    }
                    
                    // Strategy 2b: Try database symptom-based matching as fallback
                    if (disease == null)
                    {
                        try
                        {
                            disease = await IdentifyDiseaseAsync(symptoms, context);
                            System.Diagnostics.Debug.WriteLine($"Database symptom lookup result: {(disease != null ? disease.DiseaseName : "null")}");
                        }
                        catch (Exception dbEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Database disease identification failed: {dbEx.Message}");
                            System.Diagnostics.Debug.WriteLine($"Stack trace: {dbEx.StackTrace}");
                        }
                    }
                    
                    // Strategy 3: ALWAYS use AI if database failed or returned null
                    if (disease == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Database returned null, trying AI identification...");
                        try
                        {
                            var aiResponse = await GetDiseaseFromAIAsync(userMessage, symptoms, context);
                            System.Diagnostics.Debug.WriteLine($"AI response: {aiResponse}");
                            
                            if (!string.IsNullOrEmpty(aiResponse))
                            {
                                // Extract disease name from AI response
                                var diseaseName = ExtractDiseaseNameFromAI(aiResponse);
                                System.Diagnostics.Debug.WriteLine($"Extracted disease name: {diseaseName}");
                                
                                if (!string.IsNullOrEmpty(diseaseName))
                                {
                                    // Try to find in database for description
                                    string? diseaseDescription = null;
                                    try
                                    {
                                        disease = await context.Diseases
                                            .Where(d => d.DiseaseName.ToLower().Contains(diseaseName.ToLower()) || 
                                                       diseaseName.ToLower().Contains(d.DiseaseName.ToLower()))
                                            .FirstOrDefaultAsync();
                                        
                                        if (disease != null)
                                        {
                                            diseaseDescription = disease.Description;
                                            System.Diagnostics.Debug.WriteLine($"Found matching disease in database: {disease.DiseaseName}");
                                        }
                                    }
                                    catch (Exception descEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error getting disease description: {descEx.Message}");
                                        // Continue without description
                                    }
                                    
                                    // Always use AI suggestion (either from database match or pure AI)
                                    response.DetectedDisease = diseaseName;
                                    state.DetectedDisease = diseaseName;
                                    state.CurrentStep = "location";
                                    
                                    response.Response = $"Based on your symptoms, this could be **{diseaseName}**.\n\n";
                                    
                                    if (!string.IsNullOrEmpty(diseaseDescription))
                                    {
                                        response.Response += $"{diseaseDescription}\n\n";
                                    }
                                    else
                                    {
                                        // Use AI response as description
                                        response.Response += $"{aiResponse}\n\n";
                                    }
                                    
                                    response.Response += "To help you better, could you please tell me your location?";
                                    response.RequiresLocation = true;
                                    return response;
                                }
                            }
                        }
                        catch (Exception aiEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"AI disease identification failed: {aiEx.Message}");
                            System.Diagnostics.Debug.WriteLine($"Stack trace: {aiEx.StackTrace}");
                            // Continue to fallback
                        }
                    }
                    
                    // If we found a disease from database
                    if (disease != null)
                    {
                        string? diseaseDescription = null;
                        try
                        {
                            diseaseDescription = disease.Description ?? 
                                await context.Diseases
                                    .Where(d => d.DiseaseName == disease.DiseaseName)
                                    .Select(d => d.Description)
                                    .FirstOrDefaultAsync();
                        }
                        catch (Exception descEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error getting disease description: {descEx.Message}");
                            // Ignore if description not found
                        }
                        
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
                    
                    // If no disease found but user has symptoms, ask for location anyway
                    if (symptoms.Count >= 2 || userMessage.Length > 30)
                    {
                        state.CurrentStep = "location";
                        response.Response = "I understand you're experiencing some symptoms. To recommend the best doctor for you, could you please tell me your location?";
                        response.RequiresLocation = true;
                        return response;
                    }
                }
                
                // If we still don't have a disease but user mentioned health problem, ALWAYS try AI
                if (IsHealthProblem(userMessage) && disease == null)
                {
                    System.Diagnostics.Debug.WriteLine("No disease found, trying AI as final attempt...");
                    try
                    {
                        var aiResponse = await GetDiseaseFromAIAsync(userMessage, symptoms, context);
                        if (!string.IsNullOrEmpty(aiResponse))
                        {
                            var diseaseName = ExtractDiseaseNameFromAI(aiResponse);
                            if (!string.IsNullOrEmpty(diseaseName))
                            {
                                // Try to find in database
                                try
                                {
                                    disease = await context.Diseases
                                        .Where(d => d.DiseaseName.ToLower().Contains(diseaseName.ToLower()) || 
                                                   diseaseName.ToLower().Contains(d.DiseaseName.ToLower()))
                                        .FirstOrDefaultAsync();
                                }
                                catch { }
                                
                                response.DetectedDisease = diseaseName;
                                state.DetectedDisease = diseaseName;
                                state.CurrentStep = "location";
                                
                                response.Response = $"Based on your symptoms, this could be **{diseaseName}**.\n\n";
                                if (disease != null && !string.IsNullOrEmpty(disease.Description))
                                {
                                    response.Response += $"{disease.Description}\n\n";
                                }
                                else
                                {
                                    response.Response += $"{aiResponse}\n\n";
                                }
                                response.Response += "To help you better, could you please tell me your location?";
                                response.RequiresLocation = true;
                                return response;
                            }
                        }
                    }
                    catch (Exception finalAiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Final AI attempt failed: {finalAiEx.Message}");
                    }
                    
                    // Only ask for more details if AI also failed
                    if (symptoms.Count < 1)
                    {
                        response.Response = "I understand you're not feeling well. Could you please describe your symptoms in more detail? For example: fever, headache, pain, cough, etc.";
                        state.CurrentStep = "problem";
                        return response;
                    }
                }
            }
            
            // Step 3: Location - Accept ANY input when in location step OR if location is detected
            // Also handle location if user provides it after disease identification
            if (state.CurrentStep == "location" || 
                (!string.IsNullOrEmpty(state.DetectedDisease) && IsLocation(userMessage) && state.CurrentStep != "recommendation"))
            {
                // Extract location from message (remove common words like "my location is", "i am from", etc.)
                var location = ExtractLocation(userMessage);
                state.UserLocation = location;
                state.CurrentStep = "recommendation";
                
                System.Diagnostics.Debug.WriteLine($"User provided location: '{location}'");
                System.Diagnostics.Debug.WriteLine($"Detected disease: '{state.DetectedDisease}'");
                
                if (!string.IsNullOrEmpty(state.DetectedDisease))
                {
                    try
                    {
                        var specialty = _doctorService.MapDiseaseToSpecialty(state.DetectedDisease, context);
                        System.Diagnostics.Debug.WriteLine($"Mapped disease '{state.DetectedDisease}' to specialty: {specialty}");
                        
                        // PRIORITY 1: Try specialty + location (MOST IMPORTANT - location-based matching)
                        var doctors = await _doctorService.GetRecommendedDoctorsAsync(specialty, state.UserLocation, context);
                        System.Diagnostics.Debug.WriteLine($"Found {doctors.Count} doctors with specialty '{specialty}' in location '{state.UserLocation}'");
                        
                        // PRIORITY 2: If no doctors in user's location, try nearby locations or same city
                        if (!doctors.Any() && !string.IsNullOrEmpty(state.UserLocation))
                        {
                            System.Diagnostics.Debug.WriteLine($"No doctors in exact location, trying location-only search");
                            doctors = await _doctorService.GetRecommendedDoctorsAsync(string.Empty, state.UserLocation, context);
                            System.Diagnostics.Debug.WriteLine($"Found {doctors.Count} doctors in location '{state.UserLocation}' (any specialty)");
                        }
                        
                        // PRIORITY 3: If still no doctors, try specialty only (but warn user)
                        if (!doctors.Any())
                        {
                            System.Diagnostics.Debug.WriteLine($"No doctors in location '{state.UserLocation}', trying specialty '{specialty}' in other locations");
                            doctors = await _doctorService.GetRecommendedDoctorsAsync(specialty, null, context);
                            System.Diagnostics.Debug.WriteLine($"Found {doctors.Count} doctors with specialty '{specialty}' (any location)");
                        }
                        
                        // PRIORITY 4: Last resort - General Medicine in user's location
                        if (!doctors.Any() && !string.IsNullOrEmpty(state.UserLocation))
                        {
                            System.Diagnostics.Debug.WriteLine($"Trying General Medicine in location '{state.UserLocation}'");
                            doctors = await _doctorService.GetRecommendedDoctorsAsync("General Medicine", state.UserLocation, context);
                            System.Diagnostics.Debug.WriteLine($"Found {doctors.Count} General Medicine doctors in location '{state.UserLocation}'");
                        }
                        
                        if (doctors.Any())
                        {
                            // Show only ONE perfect doctor (the best match)
                            var bestDoctor = doctors.First();
                            
                            state.RecommendedDoctorId = bestDoctor.Id;
                            response.RecommendedDoctorId = bestDoctor.Id;
                            
                            response.Response = $"Based on your condition (**{state.DetectedDisease}**), I recommend consulting a **{specialty}** specialist.\n\n";
                            response.Response += $"**Recommended Doctor:**\n";
                            response.Response += $"**Dr. {bestDoctor.DoctorName}**\n";
                            if (!string.IsNullOrEmpty(bestDoctor.Speciality))
                                response.Response += $"Specialty: {bestDoctor.Speciality}\n";
                            if (!string.IsNullOrEmpty(bestDoctor.Chamber))
                                response.Response += $"Chamber: {bestDoctor.Chamber}\n";
                            if (!string.IsNullOrEmpty(bestDoctor.Location))
                                response.Response += $"Location: {bestDoctor.Location}\n";
                            if (bestDoctor.Experience.HasValue)
                                response.Response += $"Experience: {bestDoctor.Experience} years\n";
                            if (bestDoctor.ConsultationFee > 0)
                                response.Response += $"Consultation Fee: {bestDoctor.ConsultationFee} BDT\n";
                            
                            response.Response += "\nWould you like to book an appointment?";
                            
                            state.CurrentStep = "recommendation";
                            return response;
                        }
                        else
                        {
                            response.Response = $"I couldn't find a **{specialty}** specialist in **{state.UserLocation}** at the moment.\n\n";
                            response.Response += "Would you like me to search for doctors in nearby areas or different locations?";
                            state.CurrentStep = "recommendation";
                            return response;
                        }
                    }
                    catch (Exception docEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Doctor recommendation error: {docEx.Message}");
                        System.Diagnostics.Debug.WriteLine($"Stack trace: {docEx.StackTrace}");
                        response.Response = "I encountered an error while searching for doctors. Please try again.";
                        return response;
                    }
                }
                else
                {
                    response.Response = "I couldn't identify your condition. Please describe your symptoms again, and I'll help you find the right doctor.";
                    state.CurrentStep = "problem";
                    return response;
                }
            }
            
            // Step 4: Booking confirmation
            if (state.CurrentStep == "recommendation")
            {
                if (lowerMessage.Contains("yes") || lowerMessage.Contains("book"))
                {
                    if (state.RecommendedDoctorId.HasValue)
                    {
                        try
                        {
                            // Verify doctor exists first
                            var doctor = await context.Doctors.FindAsync(state.RecommendedDoctorId.Value);
                            if (doctor == null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Doctor with ID {state.RecommendedDoctorId.Value} not found");
                                response.Response = "The recommended doctor is no longer available. Please try again.";
                                return response;
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"Creating appointment for doctor ID: {state.RecommendedDoctorId.Value}, UserId: {userId}");
                            
                            // Create appointment directly (more control)
                            var appointment = new Appointment
                            {
                                UserId = userId,
                                DoctorId = state.RecommendedDoctorId.Value,
                                AppointmentDate = DateTime.UtcNow.AddDays(1),
                                Status = "Pending",
                                Amount = doctor.ConsultationFee > 0 ? doctor.ConsultationFee : 500, // Default to 500 if fee is 0
                                IsPaid = false
                            };
                            
                            context.Appointments.Add(appointment);
                            await context.SaveChangesAsync();
                            
                            // Reload appointment to ensure we have the generated ID
                            await context.Entry(appointment).ReloadAsync();
                            
                            var appointmentId = appointment.Id;
                            System.Diagnostics.Debug.WriteLine($"Appointment created successfully with ID: {appointmentId}");
                            
                            if (appointmentId <= 0)
                            {
                                System.Diagnostics.Debug.WriteLine("ERROR: Appointment ID is invalid!");
                                response.Response = "I encountered an error creating your appointment. Please try again.";
                                return response;
                            }
                            
                            state.CurrentStep = "booking";
                            response.RecommendedDoctorId = state.RecommendedDoctorId.Value;
                            response.AppointmentId = appointmentId;
                            response.Response = "Great! I'm opening the payment window to confirm your appointment.";
                            
                            System.Diagnostics.Debug.WriteLine($"Response AppointmentId set to: {response.AppointmentId}");
                            
                            // Store doctor info for frontend (using dictionary for proper serialization)
                            response.DoctorInfo = new Dictionary<string, object>
                            {
                                { "id", doctor.Id },
                                { "name", doctor.DoctorName ?? "" },
                                { "specialty", doctor.Speciality ?? "" },
                                { "location", doctor.Location ?? "" },
                                { "chamber", doctor.Chamber ?? "" },
                                { "fee", appointment.Amount }
                            };
                            
                            return response;
                        }
                        catch (Exception apptEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error creating appointment: {apptEx.Message}");
                            System.Diagnostics.Debug.WriteLine($"Stack trace: {apptEx.StackTrace}");
                            if (apptEx.InnerException != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Inner exception: {apptEx.InnerException.Message}");
                            }
                            // Show user-friendly error, but log detailed error
                            var errorMsg = apptEx.Message.ToLower();
                            if (errorMsg.Contains("foreign key") || errorMsg.Contains("constraint"))
                            {
                                response.Response = "There was an issue with the doctor information. Please try selecting a doctor again.";
                            }
                            else if (errorMsg.Contains("required") || errorMsg.Contains("null"))
                            {
                                response.Response = "Some required information is missing. Please try again.";
                            }
                            else
                            {
                                response.Response = "I encountered an error creating your appointment. Please try again.";
                            }
                            return response;
                        }
                    }
                }
                else if (lowerMessage.Contains("no"))
                {
                    response.Response = "No worries! If you need any help later, feel free to chat with me anytime. Take care! 😊";
                    state.CurrentStep = "greeting";
                    return response;
                }
            }
            
            // Before default, check if user might be providing location after disease was identified
            if (!string.IsNullOrEmpty(state.DetectedDisease) && 
                state.CurrentStep != "location" && 
                state.CurrentStep != "recommendation" &&
                IsLocation(userMessage))
            {
                // User provided location, process it
                var location = ExtractLocation(userMessage);
                state.UserLocation = location;
                state.CurrentStep = "recommendation";
                
                System.Diagnostics.Debug.WriteLine($"Detected location '{location}' after disease identification");
                
                // Re-process with location (reuse the location handling logic)
                var specialty = _doctorService.MapDiseaseToSpecialty(state.DetectedDisease, context);
                var doctors = await _doctorService.GetRecommendedDoctorsAsync(specialty, state.UserLocation, context);
                
                if (!doctors.Any())
                {
                    doctors = await _doctorService.GetRecommendedDoctorsAsync(string.Empty, state.UserLocation, context);
                }
                
                if (doctors.Any())
                {
                    var primaryDoctor = doctors.First();
                    state.RecommendedDoctorId = primaryDoctor.Id;
                    response.RecommendedDoctorId = primaryDoctor.Id;
                    
                    response.Response = $"Based on your condition (**{state.DetectedDisease}**), I recommend:\n\n";
                    response.Response += $"**Dr. {primaryDoctor.DoctorName}**\n";
                    if (!string.IsNullOrEmpty(primaryDoctor.Speciality))
                        response.Response += $"Specialty: {primaryDoctor.Speciality}\n";
                    if (!string.IsNullOrEmpty(primaryDoctor.Location))
                        response.Response += $"Location: {primaryDoctor.Location}\n";
                    if (primaryDoctor.Experience.HasValue)
                        response.Response += $"Experience: {primaryDoctor.Experience} years\n\n";
                    response.Response += "Would you like to book an appointment?";
                    
                    state.CurrentStep = "recommendation";
                    return response;
                }
            }
            
            // Default: Use Groq API or fallback
            var groqResponse = await GetGroqResponseAsync(userMessage, userId, context, state);
            response.Response = groqResponse;
            return response;
        }
        catch (Exception ex)
        {
            // Return a safe fallback response
            System.Diagnostics.Debug.WriteLine($"SimpleChatbotService Error: {ex.Message}");
            return new ChatbotResponse
            {
                Response = "I'm here to help! 😊 Please tell me about your health concerns or symptoms.",
                State = state ?? new ConversationState()
            };
        }
    }
    
    private async Task<string> GetGroqResponseAsync(string userMessage, string userId, ApplicationDbContext context, ConversationState state)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            return FallbackResponse(userMessage, state);
        }
        
        try
        {
            var recentMessages = await context.ChatMessages
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.Timestamp)
                .Take(5)
                .OrderBy(m => m.Timestamp)
                .Select(m => new { m.Message, m.IsFromUser })
                .ToListAsync();
            
            var messages = new List<object>
            {
                new { role = "system", content = "You are Doctor Koi, a friendly AI health assistant. Be helpful, empathetic, and keep responses short (2-3 sentences)." }
            };
            
            foreach (var msg in recentMessages)
            {
                messages.Add(new
                {
                    role = msg.IsFromUser ? "user" : "assistant",
                    content = msg.Message
                });
            }
            
            messages.Add(new { role = "user", content = userMessage });
            
            var requestBody = new
            {
                model = "llama-3.1-70b-versatile",
                messages = messages,
                temperature = 0.7,
                max_tokens = 200
            };
            
            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(GroqApiUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
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
        }
        catch (Exception)
        {
            // Fall through to fallback
        }
        
        return FallbackResponse(userMessage, state);
    }
    
    private string FallbackResponse(string userMessage, ConversationState state)
    {
        var lower = userMessage.ToLower();
        
        if (IsGreeting(lower))
        {
            return "Hello! 😄 I'm Doctor Koi. How can I help you today?";
        }
        
        if (IsHealthProblem(lower))
        {
            return "I'm sorry you're feeling unwell 😟 Please describe your symptoms, and I'll help you identify the possible condition.";
        }
        
        return "I'm here to help! 😊 Please tell me about your health concerns or symptoms.";
    }
    
    public async Task<List<string>> ExtractSymptomsAsync(string message)
    {
        var symptoms = new List<string>();
        var lowerMessage = message.ToLower();
        
        // Extended symptom keywords (prioritize common ones first)
        var symptomKeywords = new[] { 
            "fever", "headache", "pain", "cough", "nausea", "vomiting", "diarrhea", 
            "dizziness", "fatigue", "chest pain", "abdominal pain", "back pain", 
            "rash", "itchy", "sore throat", "chills", "sweating", "weakness",
            "numbness", "tingling", "shortness of breath", "difficulty breathing",
            "stomach ache", "stomach pain", "joint pain", "muscle pain", "sneezing",
            "runny nose", "congestion", "sore", "ache", "burning", "swelling",
            "have fever", "got fever", "feeling fever", "high temperature"
        };
        
        foreach (var keyword in symptomKeywords)
        {
            if (lowerMessage.Contains(keyword))
            {
                // Extract the base symptom name (remove "have", "got", etc.)
                var baseKeyword = keyword
                    .Replace("have ", "")
                    .Replace("got ", "")
                    .Replace("feeling ", "")
                    .Trim();
                
                if (!symptoms.Contains(baseKeyword))
                {
                    symptoms.Add(baseKeyword);
                }
            }
        }
        
        // If no symptoms found but message seems health-related, ALWAYS use AI to extract
        if (symptoms.Count == 0 && IsHealthProblem(message))
        {
            if (!string.IsNullOrEmpty(_apiKey))
            {
                try
                {
                    var aiSymptoms = await ExtractSymptomsFromAIAsync(message);
                    symptoms.AddRange(aiSymptoms);
                    System.Diagnostics.Debug.WriteLine($"AI extracted symptoms: {string.Join(", ", aiSymptoms)}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AI symptom extraction failed: {ex.Message}");
                }
            }
            
            // Fallback: Extract any word that might be a symptom
            if (symptoms.Count == 0)
            {
                var words = lowerMessage.Split(new[] { ' ', ',', '.', '!', '?', '-' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    var cleanWord = word.Trim();
                    // Skip common words
                    var skipWords = new[] { "have", "got", "feeling", "i", "am", "my", "me", "the", "a", "an", "is", "are", "was", "were", "this", "that", "with", "and", "or", "but" };
                    if (cleanWord.Length > 2 && !skipWords.Contains(cleanWord))
                    {
                        symptoms.Add(cleanWord);
                        if (symptoms.Count >= 3) break; // Add up to 3 words
                    }
                }
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"Extracted symptoms from '{message}': {string.Join(", ", symptoms)}");
        return symptoms;
    }
    
    private async Task<List<string>> ExtractSymptomsFromAIAsync(string message)
    {
        var symptoms = new List<string>();
        
        try
        {
            var prompt = $"Extract all medical symptoms mentioned in this text: \"{message}\". " +
                        $"Return only a comma-separated list of symptoms. Be concise. " +
                        $"Example: fever, headache, cough";
            
            var messages = new List<object>
            {
                new { role = "system", content = "You are a medical assistant. Extract symptoms from patient descriptions. Return only a comma-separated list." },
                new { role = "user", content = prompt }
            };
            
            var requestBody = new
            {
                model = "llama-3.1-70b-versatile",
                messages = messages,
                temperature = 0.3,
                max_tokens = 50
            };
            
            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(GroqApiUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var groqResponse = JsonConvert.DeserializeObject<GroqApiResponse>(responseString);
                
                if (groqResponse?.Choices != null && groqResponse.Choices.Count > 0)
                {
                    var aiResponse = groqResponse.Choices[0].Message?.Content ?? "";
                    var extracted = aiResponse.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim().ToLower())
                        .Where(s => s.Length > 2)
                        .ToList();
                    symptoms.AddRange(extracted);
                }
            }
        }
        catch
        {
            // Return empty if AI fails
        }
        
        return symptoms;
    }
    
    // New method: Match disease by name directly from user message
    private async Task<Disease?> MatchDiseaseByNameAsync(string userMessage, ApplicationDbContext context)
    {
        try
        {
            var lowerMessage = userMessage.ToLower();
            
            // Get all diseases from database
            var allDiseases = await context.Diseases.ToListAsync();
            
            if (!allDiseases.Any())
            {
                return null;
            }
            
            // Try exact or partial name matching
            var matchingDisease = allDiseases.FirstOrDefault(d => 
            {
                var diseaseNameLower = d.DiseaseName.ToLower();
                // Check if disease name is in message or message contains disease name
                return lowerMessage.Contains(diseaseNameLower) || 
                       diseaseNameLower.Contains(lowerMessage) ||
                       // Check word-by-word matching
                       diseaseNameLower.Split(' ', '-', '_').Any(word => 
                           word.Length > 3 && lowerMessage.Contains(word));
            });
            
            if (matchingDisease != null)
            {
                System.Diagnostics.Debug.WriteLine($"Matched disease by name: {matchingDisease.DiseaseName}");
                return matchingDisease;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MatchDiseaseByNameAsync: {ex.Message}");
        }
        
        return null;
    }
    
    public async Task<Disease?> IdentifyDiseaseAsync(List<string> symptoms, ApplicationDbContext context)
    {
        if (!symptoms.Any()) return null;
        
        try
        {
            // Check if database has diseases
            var hasDiseases = await context.Diseases.AnyAsync();
            if (!hasDiseases)
            {
                System.Diagnostics.Debug.WriteLine("No diseases found in database");
                return null;
            }
            
            // Try with Include first, but fallback to simpler query if it fails
            List<Disease> diseases;
            try
            {
                diseases = await context.Diseases
                    .Include(d => d.DiseaseSymptoms)
                    .ThenInclude(ds => ds.Symptom)
                    .ToListAsync();
            }
            catch (Exception includeEx)
            {
                System.Diagnostics.Debug.WriteLine($"Include query failed, trying simple query: {includeEx.Message}");
                // Fallback: Load diseases without relationships
                diseases = await context.Diseases.ToListAsync();
                
                // If we have diseases but no relationships, try to match by disease name containing symptom keywords
                if (diseases.Any())
                {
                    var symptomText = string.Join(" ", symptoms).ToLower();
                    var matchingDisease = diseases.FirstOrDefault(d => 
                        symptomText.Contains(d.DiseaseName.ToLower()) || 
                        d.DiseaseName.ToLower().Contains(symptomText));
                    
                    if (matchingDisease != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found disease by name match: {matchingDisease.DiseaseName}");
                        return matchingDisease;
                    }
                }
                
                // If simple query also fails or no match, return null
                return null;
            }
            
            if (!diseases.Any())
            {
                System.Diagnostics.Debug.WriteLine("Diseases list is empty");
                return null;
            }
            
            // Try to load disease-symptom relationships separately if Include didn't work
            var diseaseScores = new Dictionary<Disease, int>();
            
            foreach (var disease in diseases)
            {
                int score = 0;
                List<string> diseaseSymptomNames = new List<string>();
                
                try
                {
                    // Check if DiseaseSymptoms are loaded
                    if (disease.DiseaseSymptoms != null && disease.DiseaseSymptoms.Any())
                    {
                        diseaseSymptomNames = disease.DiseaseSymptoms
                            .Where(ds => ds.IsPresent && ds.Symptom != null)
                            .Select(ds => ds.Symptom.SymptomName.ToLower())
                            .ToList();
                    }
                    else
                    {
                        // Try to load relationships manually
                        var relationships = await context.DiseaseSymptoms
                            .Where(ds => ds.DiseaseId == disease.Id && ds.IsPresent)
                            .Include(ds => ds.Symptom)
                            .Select(ds => ds.Symptom.SymptomName.ToLower())
                            .ToListAsync();
                        
                        diseaseSymptomNames = relationships;
                    }
                }
                catch (Exception relEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading disease symptoms for {disease.DiseaseName}: {relEx.Message}");
                    // Continue without symptom matching for this disease
                }
                
                if (!diseaseSymptomNames.Any())
                {
                    // Fallback: Match by disease name containing symptom keywords
                    var symptomText = string.Join(" ", symptoms).ToLower();
                    if (symptomText.Contains(disease.DiseaseName.ToLower()) || 
                        disease.DiseaseName.ToLower().Contains(symptomText))
                    {
                        score = 1; // Give it a score so it can be matched
                    }
                }
                else
                {
                    // Normal symptom matching
                    foreach (var symptom in symptoms)
                    {
                        var symptomLower = symptom.ToLower();
                        // More flexible matching - check each disease symptom
                        foreach (var ds in diseaseSymptomNames)
                        {
                            if (ds.Contains(symptomLower) || symptomLower.Contains(ds))
                            {
                                score++;
                                break; // Count each symptom only once per disease
                            }
                        }
                    }
                }
                
                if (score > 0)
                {
                    diseaseScores[disease] = score;
                }
            }
            
            if (diseaseScores.Any())
            {
                var bestMatch = diseaseScores.OrderByDescending(ds => ds.Value).First();
                System.Diagnostics.Debug.WriteLine($"Found disease: {bestMatch.Key.DiseaseName} with score: {bestMatch.Value}");
                return bestMatch.Key;
            }
            
            System.Diagnostics.Debug.WriteLine($"No disease match found for symptoms: {string.Join(", ", symptoms)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error identifying disease: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            // Don't throw - return null so AI can be used
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
        
        response += "Would you like me to recommend a suitable doctor for you?";
        return response;
    }
    
    private bool IsGreeting(string message)
    {
        var greetings = new[] { "hi", "hello", "hey", "greetings", "whatsapp", "what's up" };
        return greetings.Any(g => message.Contains(g));
    }
    
    private bool IsHealthProblem(string message)
    {
        var lower = message.ToLower();
        var healthKeywords = new[] { "fever", "headache", "pain", "sick", "ill", "symptom", "unwell", "feeling", "hurt", "ache", "cough" };
        return healthKeywords.Any(keyword => lower.Contains(keyword));
    }
    
    private bool IsLocation(string message)
    {
        var lower = message.ToLower();
        // Common location keywords in Bangladesh
        var locationKeywords = new[] { 
            "dhanmondi", "gulshan", "banani", "uttara", "mirpur", "dhaka", "chittagong", "sylhet", 
            "rajshahi", "khulna", "barisal", "rangpur", "comilla", "narayanganj", "gazipur",
            "location", "area", "city", "from", "live", "located", "address", "my location is",
            "i am from", "i live in", "i'm from", "i'm in"
        };
        return locationKeywords.Any(keyword => lower.Contains(keyword)) || message.Length < 50;
    }
    
    private string ExtractLocation(string message)
    {
        var lower = message.ToLower();
        
        // Remove common phrases
        var phrasesToRemove = new[] { 
            "my location is", "i am from", "i live in", "i'm from", "i'm in", 
            "location:", "from", "in", "at", "near", "around"
        };
        
        var cleaned = message;
        foreach (var phrase in phrasesToRemove)
        {
            if (lower.Contains(phrase))
            {
                cleaned = cleaned.Substring(lower.IndexOf(phrase) + phrase.Length).Trim();
                lower = cleaned.ToLower();
            }
        }
        
        // Extract location words (capitalize first letter of each word)
        var words = cleaned.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2) // Filter out short words
            .Take(3) // Take up to 3 words
            .Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower())
            .ToArray();
        
        var location = string.Join(" ", words);
        
        // If still empty, just use the cleaned message
        if (string.IsNullOrWhiteSpace(location))
        {
            location = message.Trim();
        }
        
        System.Diagnostics.Debug.WriteLine($"Extracted location from '{message}': '{location}'");
        return location;
    }
    
    private async Task<string> GetDiseaseFromAIAsync(string userMessage, List<string> symptoms, ApplicationDbContext context)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            System.Diagnostics.Debug.WriteLine("Groq API key is missing");
            return string.Empty;
        }
        
        try
        {
            // Get list of diseases from database to help AI
            string? diseaseList = null;
            try
            {
                var diseases = await context.Diseases
                    .Select(d => d.DiseaseName)
                    .Take(50) // Limit to avoid too long prompt
                    .ToListAsync();
                
                if (diseases.Any())
                {
                    diseaseList = string.Join(", ", diseases);
                }
            }
            catch
            {
                // Ignore if can't load diseases
            }
            
            var symptomList = symptoms.Any() ? string.Join(", ", symptoms) : "various symptoms";
            var prompt = $"User description: \"{userMessage}\". " +
                        $"Symptoms mentioned: {symptomList}. " +
                        $"Identify the most likely disease or medical condition from the user's description. " +
                        $"Provide a brief explanation (2-3 sentences) about the condition. " +
                        $"IMPORTANT: Start your response with the disease name in bold format: **Disease Name**. " +
                        $"Then provide a brief explanation. " +
                        $"Be specific and accurate. If you cannot identify a specific disease, suggest the most likely condition based on the symptoms.";
            
            if (!string.IsNullOrEmpty(diseaseList))
            {
                prompt += $" Common diseases in our database include: {diseaseList}. Try to match to one of these if possible.";
            }
            
            var messages = new List<object>
            {
                new { role = "system", content = "You are a medical assistant. Identify diseases from patient descriptions. Always provide a specific disease name in **bold** format at the start of your response, followed by a brief explanation. Be accurate and helpful." },
                new { role = "user", content = prompt }
            };
            
            var requestBody = new
            {
                model = "llama-3.1-70b-versatile",
                messages = messages,
                temperature = 0.7,
                max_tokens = 250
            };
            
            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            System.Diagnostics.Debug.WriteLine($"Calling Groq API for disease identification");
            var response = await _httpClient.PostAsync(GroqApiUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var groqResponse = JsonConvert.DeserializeObject<GroqApiResponse>(responseString);
                
                if (groqResponse?.Choices != null && groqResponse.Choices.Count > 0)
                {
                    var message = groqResponse.Choices[0].Message;
                    if (message?.Content != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"AI response: {message.Content}");
                        return message.Content;
                    }
                }
            }
            else
            {
                var errorText = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Groq API error: {response.StatusCode} - {errorText}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error calling Groq API: {ex.Message}");
        }
        
        return string.Empty;
    }
    
    private string ExtractDiseaseNameFromAI(string aiResponse)
    {
        // Try to extract disease name from AI response
        // Look for common patterns like "could be X", "likely X", "X disease", etc.
        var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0)
        {
            var firstLine = lines[0].Trim();
            
            // Remove common prefixes
            var prefixes = new[] { "Based on", "This could be", "This is likely", "The condition is", "You may have" };
            foreach (var prefix in prefixes)
            {
                if (firstLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    firstLine = firstLine.Substring(prefix.Length).Trim();
                }
            }
            
            // Remove markdown formatting
            firstLine = firstLine.Replace("**", "").Replace("*", "").Trim();
            
            // Take first sentence or first 50 characters
            var diseaseName = firstLine.Split('.', ':', '!', '?')[0].Trim();
            if (diseaseName.Length > 50)
            {
                diseaseName = diseaseName.Substring(0, 50).Trim();
            }
            
            return diseaseName;
        }
        
        return string.Empty;
    }
}

