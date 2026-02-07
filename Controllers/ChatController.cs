using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalAssistant.Models;
using MedicalAssistant.Data;
using MedicalAssistant.Services;

namespace MedicalAssistant.Controllers;

/// <summary>
/// Chat Controller - Handles chatbot API endpoints
/// Provides endpoints for sending messages and retrieving chat history
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatbotService _chatbotService;
    private readonly ApplicationDbContext _context;
    
    public ChatController(
        IChatbotService chatbotService,
        ApplicationDbContext context)
    {
        _chatbotService = chatbotService;
        _context = context;
    }
    
    /// <summary>
    /// Gets or creates a unique session ID for the current user
    /// Session ID is used to maintain conversation state and identify users
    /// </summary>
    /// <returns>Session ID string (GUID)</returns>
    private string GetSessionId()
    {
        var sessionId = HttpContext.Session.GetString("SessionId");
        if (string.IsNullOrEmpty(sessionId))
        {
            // Generate new session ID if one doesn't exist
            sessionId = Guid.NewGuid().ToString();
            HttpContext.Session.SetString("SessionId", sessionId);
        }
        return sessionId;
    }
    
    /// <summary>
    /// GET /api/Chat/history
    /// Retrieves chat history for the currently logged-in user
    /// Requires authentication - returns 401 if user is not logged in
    /// </summary>
    /// <returns>List of chat messages with timestamps</returns>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        try
        {
            // Check if user is logged in (userId is set during login)
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "Please login to view chat history" });
            }
            
            // Retrieve all messages for this user, ordered by timestamp (oldest first)
            var messages = await _context.ChatMessages
                .Where(m => m.UserId == userId) // Filter by user ID
                .OrderBy(m => m.Timestamp) // Order chronologically
                .Select(m => new { 
                    message = m.Message, 
                    isFromUser = m.IsFromUser, // true for user messages, false for bot
                    timestamp = m.Timestamp
                })
                .ToListAsync();
            
            return Ok(messages);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    
    /// <summary>
    /// POST /api/Chat/send
    /// Processes a user message and returns bot response
    /// This is the main chatbot endpoint - handles conversation flow
    /// </summary>
    /// <param name="request">Chat request containing user message</param>
    /// <returns>Bot response with optional appointment and doctor information</returns>
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrEmpty(request.Message))
            {
                return BadRequest(new { error = "Message cannot be empty" });
            }
            
            // Determine user identifier:
            // - If logged in: use userId (MongoDB user ID) - messages saved to user account
            // - If not logged in: use sessionId (temporary session) - messages saved to session
            var userId = HttpContext.Session.GetString("UserId");
            var sessionId = GetSessionId();
            var identifier = !string.IsNullOrEmpty(userId) ? userId : sessionId;
            
            // Save user message to database (optional - won't fail if DB is unavailable)
            // This allows chat history to be preserved
            try
            {
                var userMessage = new ChatMessage
                {
                    UserId = identifier, // Use userId if logged in, sessionId if not
                    Message = request.Message,
                    IsFromUser = true, // Mark as user message
                    Timestamp = DateTime.UtcNow
                };
                
                _context.ChatMessages.Add(userMessage);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Database save failed - continue anyway
                // Chat functionality should work even if database is unavailable
                System.Diagnostics.Debug.WriteLine("Could not save user message to database - continuing anyway");
            }
            
            // Process message through chatbot service
            // Uses sessionId (not userId) for conversation state management
            // This ensures conversation state persists even if user logs in/out
            var conversationState = GetConversationState();
            var result = await _chatbotService.ProcessMessageAsync(
                request.Message, 
                sessionId, // Use sessionId for conversation state
                _context, 
                conversationState);
            
            // Save updated conversation state to session
            // This maintains context across multiple messages
            UpdateConversationState(result.State);
            
            // Save bot response to database (optional - won't fail if DB is unavailable)
            try
            {
                var botMessage = new ChatMessage
                {
                    UserId = identifier, // Use same identifier as user message
                    Message = result.Response,
                    IsFromUser = false, // Mark as bot message
                    Timestamp = DateTime.UtcNow,
                    DetectedDisease = result.DetectedDisease, // Store detected disease if any
                    RecommendedDoctorId = result.RecommendedDoctorId // Store recommended doctor if any
                };
                
                _context.ChatMessages.Add(botMessage);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Database save failed - continue anyway
                System.Diagnostics.Debug.WriteLine("Could not save bot message to database - continuing anyway");
            }
            
            // Prepare response data
            // Explicitly handle nullable types to ensure proper JSON serialization
            int? appointmentIdValue = result.AppointmentId;
            object? doctorInfoValue = result.DoctorInfo;
            
            // Debug logging for troubleshooting
            System.Diagnostics.Debug.WriteLine($"ChatController response - AppointmentId: {appointmentIdValue}, Type: {appointmentIdValue?.GetType()}, HasDoctorInfo: {doctorInfoValue != null}");
            
            var responseData = new { 
                message = result.Response, // Bot's text response
                appointmentId = appointmentIdValue, // Appointment ID if appointment was created
                doctorInfo = doctorInfoValue // Doctor information if doctor was recommended
            };
            
            System.Diagnostics.Debug.WriteLine($"Response data - appointmentId: {responseData.appointmentId}, doctorInfo: {responseData.doctorInfo != null}");
            
            return Ok(responseData);
        }
        catch (Exception ex)
        {
            // Log detailed error information for debugging
            System.Diagnostics.Debug.WriteLine($"Chat Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            
            // Return user-friendly error message
            return StatusCode(500, new { 
                error = $"An error occurred: {ex.Message}",
                message = "I'm sorry, but I encountered an error. Please try again."
            });
        }
    }
    
    /// <summary>
    /// Retrieves conversation state from session
    /// Conversation state tracks: current step, detected disease, user location, etc.
    /// </summary>
    /// <returns>ConversationState object or new instance if none exists</returns>
    private ConversationState GetConversationState()
    {
        var stateJson = HttpContext.Session.GetString("ConversationState");
        if (string.IsNullOrEmpty(stateJson))
        {
            // No existing state - return new empty state
            return new ConversationState();
        }
        // Deserialize JSON to ConversationState object
        return System.Text.Json.JsonSerializer.Deserialize<ConversationState>(stateJson) ?? new ConversationState();
    }
    
    /// <summary>
    /// Saves conversation state to session
    /// This allows the bot to remember context across multiple messages
    /// </summary>
    /// <param name="state">ConversationState to save</param>
    private void UpdateConversationState(ConversationState state)
    {
        // Serialize state to JSON and store in session
        var stateJson = System.Text.Json.JsonSerializer.Serialize(state);
        HttpContext.Session.SetString("ConversationState", stateJson);
    }
}

/// <summary>
/// Request model for chat endpoint
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// User's message text
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
