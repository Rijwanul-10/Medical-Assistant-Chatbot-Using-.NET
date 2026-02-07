using Microsoft.AspNetCore.Mvc;
using MedicalAssistant.Models;
using MedicalAssistant.Services;
using MedicalAssistant.Data;

namespace MedicalAssistant.Controllers;

/// <summary>
/// Payment Controller - Handles Stripe payment processing
/// Manages appointment payments and payment verification
/// </summary>
public class PaymentController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly IAppointmentService _appointmentService;
    private readonly ApplicationDbContext _context;
    
    public PaymentController(
        IPaymentService paymentService,
        IAppointmentService appointmentService,
        ApplicationDbContext context)
    {
        _paymentService = paymentService;
        _appointmentService = appointmentService;
        _context = context;
    }
    
    /// <summary>
    /// Gets or creates session ID for identifying the current user/session
    /// </summary>
    private string GetSessionId()
    {
        var sessionId = HttpContext.Session.GetString("SessionId");
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
            HttpContext.Session.SetString("SessionId", sessionId);
        }
        return sessionId;
    }
    
    [HttpGet]
    public async Task<IActionResult> Index(int? appointmentId)
    {
        // Get appointmentId from query string if not provided as parameter
        if (!appointmentId.HasValue)
        {
            var appointmentIdStr = Request.Query["appointmentId"].FirstOrDefault();
            if (!string.IsNullOrEmpty(appointmentIdStr) && int.TryParse(appointmentIdStr, out int parsedId))
            {
                appointmentId = parsedId;
            }
        }
        
        if (!appointmentId.HasValue || appointmentId.Value <= 0)
        {
            return BadRequest("Appointment ID is required. Please make sure you're booking an appointment first.");
        }
        
        var appointment = await _appointmentService.GetAppointmentByIdAsync(appointmentId.Value, _context);
        
        if (appointment == null)
        {
            return NotFound($"Appointment with ID {appointmentId.Value} not found");
        }
        
        var sessionId = GetSessionId();
        if (appointment.UserId != sessionId)
        {
            return BadRequest("You don't have permission to access this appointment.");
        }
        
        // Check if already paid
        if (appointment.IsPaid)
        {
            return BadRequest("This appointment has already been paid.");
        }
        
        ViewBag.Appointment = appointment;
        var publishableKey = HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["Stripe:PublishableKey"];
        ViewBag.PublishableKey = publishableKey ?? string.Empty;
        
        return View();
    }
    
    /// <summary>
    /// POST /Payment/create-checkout-session
    /// Creates a Stripe Checkout session for appointment payment
    /// Returns a URL that redirects user to Stripe's payment page
    /// </summary>
    /// <param name="request">Payment request with amount and appointment ID</param>
    /// <returns>Stripe Checkout URL</returns>
    [HttpPost]
    [Route("Payment/create-checkout-session")]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutSessionRequest request)
    {
        try
        {
            // Validate payment amount
            if (request.Amount <= 0)
            {
                return BadRequest(new { error = "Invalid amount" });
            }
            
            // Validate appointment ID
            if (request.AppointmentId <= 0)
            {
                return BadRequest(new { error = "Invalid appointment ID" });
            }
            
            // Security check: Verify appointment exists and belongs to current session
            // Prevents users from paying for other users' appointments
            var sessionId = GetSessionId();
            var appointment = await _appointmentService.GetAppointmentByIdAsync(request.AppointmentId, _context);
            
            if (appointment == null)
            {
                return NotFound(new { error = "Appointment not found" });
            }
            
            // Verify appointment ownership
            if (appointment.UserId != sessionId)
            {
                return BadRequest(new { error = "You don't have permission to pay for this appointment." });
            }
            
            // Prevent duplicate payments
            if (appointment.IsPaid)
            {
                return BadRequest(new { error = "This appointment has already been paid." });
            }
            
            // Build redirect URLs for Stripe Checkout
            // {CHECKOUT_SESSION_ID} is a Stripe placeholder that gets replaced with actual session ID
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var successUrl = $"{baseUrl}/Payment/Success?session_id={{CHECKOUT_SESSION_ID}}&appointmentId={request.AppointmentId}";
            var cancelUrl = $"{baseUrl}/Payment?appointmentId={request.AppointmentId}";
            
            // Create Stripe Checkout session and get payment URL
            var checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(
                request.Amount, 
                request.AppointmentId.ToString(),
                successUrl,
                cancelUrl);
            
            return Ok(new { url = checkoutUrl });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating checkout session: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
    
    [HttpPost]
    [Route("Payment/create-payment-intent")]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] PaymentIntentRequest request)
    {
        try
        {
            // Validate request
            if (request.Amount <= 0)
            {
                return BadRequest(new { error = "Invalid amount" });
            }
            
            if (request.AppointmentId <= 0)
            {
                return BadRequest(new { error = "Invalid appointment ID" });
            }
            
            // Verify appointment exists and belongs to current session
            var sessionId = GetSessionId();
            var appointment = await _appointmentService.GetAppointmentByIdAsync(request.AppointmentId, _context);
            
            if (appointment == null)
            {
                return NotFound(new { error = "Appointment not found" });
            }
            
            if (appointment.UserId != sessionId)
            {
                return BadRequest(new { error = "You don't have permission to pay for this appointment." });
            }
            
            if (appointment.IsPaid)
            {
                return BadRequest(new { error = "This appointment has already been paid." });
            }
            
            // Create payment intent
            var clientSecret = await _paymentService.CreatePaymentIntentAsync(
                request.Amount, 
                request.AppointmentId.ToString());
            
            return Ok(new { clientSecret });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating payment intent: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
    
    [HttpPost]
    [Route("Payment/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.PaymentIntentId))
            {
                return BadRequest(new { error = "Payment intent ID is required" });
            }
            
            // Verify payment with Stripe first
            var isPaymentVerified = await _paymentService.VerifyPaymentIntentAsync(request.PaymentIntentId);
            if (!isPaymentVerified)
            {
                return BadRequest(new { error = "Payment verification failed. Please try again." });
            }
            
            var sessionId = GetSessionId();
            var appointment = await _appointmentService.GetAppointmentByIdAsync(request.AppointmentId, _context);
            
            if (appointment == null)
            {
                return NotFound(new { error = "Appointment not found" });
            }
            
            if (appointment.UserId != sessionId)
            {
                return BadRequest(new { error = "You don't have permission to confirm this payment." });
            }
            
            if (appointment.IsPaid)
            {
                return BadRequest(new { error = "This appointment has already been paid." });
            }
            
            // Set appointment time to next day at 4 PM or 5 PM (randomly choose)
            var random = new Random();
            var appointmentHour = random.Next(16, 18); // 4 PM or 5 PM (16 or 17)
            var appointmentDate = DateTime.UtcNow.AddDays(1).Date.AddHours(appointmentHour);
            
            // Update appointment with payment and time
            appointment.IsPaid = true;
            appointment.Status = "Confirmed";
            appointment.AppointmentDate = appointmentDate;
            appointment.PaymentIntentId = request.PaymentIntentId;
            
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();
            
            // Format appointment time for display
            var appointmentTimeStr = appointmentDate.ToString("dddd, MMMM dd, yyyy 'at' h:mm tt");
            
            // Send confirmation message with appointment time
            var response = "✅ Payment successful!\n\n";
            response += $"Your appointment with **{appointment.Doctor?.DoctorName}** has been confirmed.\n\n";
            response += $"📅 **Appointment Time:** {appointmentTimeStr}\n";
            response += $"📍 **Location:** {appointment.Doctor?.Chamber}\n";
            response += $"🏥 **Address:** {appointment.Doctor?.Location}\n\n";
            response += "I wish you a speedy recovery 💙\n";
            response += "Let me know if you need anything else.";
            
            var botMessage = new ChatMessage
            {
                UserId = sessionId,
                Message = response,
                IsFromUser = false,
                Timestamp = DateTime.UtcNow
            };
            
            _context.ChatMessages.Add(botMessage);
            await _context.SaveChangesAsync();
            
            return Ok(new { success = true, message = response });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    
    /// <summary>
    /// GET /Payment/Success
    /// Handles successful payment callback from Stripe
    /// This page is shown after user completes payment on Stripe's checkout page
    /// </summary>
    /// <param name="session_id">Stripe checkout session ID</param>
    /// <param name="appointmentId">Appointment ID</param>
    /// <returns>Success page view</returns>
    [HttpGet]
    [Route("Payment/Success")]
    public async Task<IActionResult> Success(string session_id, int appointmentId)
    {
        try
        {
            // Validate Stripe session ID
            if (string.IsNullOrEmpty(session_id))
            {
                return BadRequest("Invalid payment session");
            }
            
            // Security check: Verify appointment belongs to current session
            var sessionId = GetSessionId();
            var appointment = await _appointmentService.GetAppointmentByIdAsync(appointmentId, _context);
            
            if (appointment == null)
            {
                return NotFound("Appointment not found");
            }
            
            if (appointment.UserId != sessionId)
            {
                return BadRequest("You don't have permission to access this appointment.");
            }
            
            // Verify payment with Stripe to ensure it was actually paid
            // This prevents users from manually navigating to success page without paying
            var isPaymentVerified = await _paymentService.VerifyCheckoutSessionAsync(session_id);
            if (!isPaymentVerified)
            {
                return BadRequest("Payment verification failed. Please contact support.");
            }
            
            // Update appointment only if not already paid (prevents duplicate processing)
            if (!appointment.IsPaid)
            {
                // Generate random appointment time: next day at 4 PM or 5 PM
                var random = new Random();
                var appointmentHour = random.Next(16, 18); // 16 = 4 PM, 17 = 5 PM
                var appointmentDate = DateTime.UtcNow.AddDays(1).Date.AddHours(appointmentHour);
                
                // Mark appointment as paid and confirmed
                appointment.IsPaid = true;
                appointment.Status = "Confirmed";
                appointment.AppointmentDate = appointmentDate;
                appointment.PaymentIntentId = session_id; // Store Stripe session ID for reference
                
                _context.Appointments.Update(appointment);
                await _context.SaveChangesAsync();
                
                // Send confirmation message to chat
                // This message will appear in the user's chat history
                var appointmentTimeStr = appointmentDate.ToString("dddd, MMMM dd, yyyy 'at' h:mm tt");
                var response = "✅ Payment successful!\n\n";
                response += $"Your appointment with **{appointment.Doctor?.DoctorName}** has been confirmed.\n\n";
                response += $"📅 **Appointment Time:** {appointmentTimeStr}\n";
                response += $"📍 **Location:** {appointment.Doctor?.Chamber}\n";
                response += $"🏥 **Address:** {appointment.Doctor?.Location}\n\n";
                response += "I wish you a speedy recovery 💙\n";
                response += "Let me know if you need anything else.";
                
                var botMessage = new ChatMessage
                {
                    UserId = sessionId,
                    Message = response,
                    IsFromUser = false,
                    Timestamp = DateTime.UtcNow
                };
                
                _context.ChatMessages.Add(botMessage);
                await _context.SaveChangesAsync();
            }
            
            // Pass appointment data to view
            ViewBag.Appointment = appointment;
            ViewBag.Success = true;
            return View();
        }
        catch (Exception ex)
        {
            return BadRequest($"Error: {ex.Message}");
        }
    }
    
    [HttpGet("check-status")]
    public async Task<IActionResult> CheckPaymentStatus(int appointmentId)
    {
        try
        {
            var appointment = await _appointmentService.GetAppointmentByIdAsync(appointmentId, _context);
            if (appointment == null)
            {
                return NotFound(new { success = false });
            }
            
            if (appointment.IsPaid)
            {
                var response = "✅ Payment successful!\n\n";
                response += $"Your appointment with **{appointment.Doctor?.DoctorName}** has been confirmed.\n";
                response += "I wish you a speedy recovery 💙\n";
                response += "Let me know if you need anything else.";
                
                return Ok(new { success = true, message = response });
            }
            
            return Ok(new { success = false });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class CheckoutSessionRequest
{
    public decimal Amount { get; set; }
    public int AppointmentId { get; set; }
}

public class PaymentIntentRequest
{
    public decimal Amount { get; set; }
    public int AppointmentId { get; set; }
}

public class ConfirmPaymentRequest
{
    public int AppointmentId { get; set; }
    public string PaymentIntentId { get; set; } = string.Empty;
}

