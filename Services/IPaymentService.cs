namespace MedicalAssistant.Services;

public interface IPaymentService
{
    Task<string> CreatePaymentIntentAsync(decimal amount, string appointmentId);
    Task<bool> VerifyPaymentIntentAsync(string paymentIntentId);
    Task<string> CreateCheckoutSessionAsync(decimal amount, string appointmentId, string successUrl, string cancelUrl);
    Task<bool> VerifyCheckoutSessionAsync(string sessionId);
}

