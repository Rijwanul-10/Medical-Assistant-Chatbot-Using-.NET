using MedicalAssistant.Models;
using MedicalAssistant.Data;

namespace MedicalAssistant.Services;

public interface IChatbotService
{
    Task<ChatbotResponse> ProcessMessageAsync(string userMessage, string userId, ApplicationDbContext context, ConversationState? state = null);
    Task<List<string>> ExtractSymptomsAsync(string message);
    Task<Disease?> IdentifyDiseaseAsync(List<string> symptoms, ApplicationDbContext context);
    string GenerateFriendlyResponse(string diseaseName, string? diseaseDescription, string userName);
}

public class ConversationState
{
    public string? DetectedDisease { get; set; }
    public string? UserLocation { get; set; }
    public int? RecommendedDoctorId { get; set; }
    public string CurrentStep { get; set; } = "greeting"; // greeting, problem, location, recommendation, booking, payment
}

public class ChatbotResponse
{
    public string Response { get; set; } = string.Empty;
    public ConversationState State { get; set; } = new();
    public string? DetectedDisease { get; set; }
    public int? RecommendedDoctorId { get; set; }
    public bool RequiresLocation { get; set; }
    public int? AppointmentId { get; set; }
    public object? DoctorInfo { get; set; }
}

