using Microsoft.Extensions.Options;
using MedicalAssistant.Models;
using Stripe;

namespace MedicalAssistant.Services;

/// <summary>
/// Payment Service - Handles Stripe payment processing
/// Provides methods for creating payment intents and checkout sessions
/// Converts BDT (Bangladeshi Taka) to USD for Stripe processing
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly StripeSettings _stripeSettings;
    
    /// <summary>
    /// Initializes payment service with Stripe configuration
    /// </summary>
    /// <param name="stripeSettings">Stripe API keys from configuration</param>
    public PaymentService(IOptions<StripeSettings> stripeSettings)
    {
        _stripeSettings = stripeSettings?.Value ?? throw new ArgumentNullException(nameof(stripeSettings));
        // Set global Stripe API key for all Stripe operations
        StripeConfiguration.ApiKey = _stripeSettings.SecretKey ?? string.Empty;
    }
    
    /// <summary>
    /// Creates a Stripe Payment Intent (for embedded payment forms)
    /// Note: This method is available but not currently used - we use Checkout Sessions instead
    /// </summary>
    /// <param name="amount">Payment amount in BDT (Bangladeshi Taka)</param>
    /// <param name="appointmentId">Appointment ID for metadata</param>
    /// <returns>Payment Intent client secret (used by Stripe.js)</returns>
    public async Task<string> CreatePaymentIntentAsync(decimal amount, string appointmentId)
    {
        try
        {
            // Validate input
            if (amount <= 0)
            {
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));
            }
            
            if (string.IsNullOrEmpty(appointmentId))
            {
                throw new ArgumentException("Appointment ID is required", nameof(appointmentId));
            }
            
            // Currency conversion: BDT to USD
            // Stripe processes payments in USD, so we need to convert
            // Exchange rate: 1 USD ≈ 110 BDT (approximate, adjust as needed)
            var amountInUsd = amount / 110m;
            
            // Stripe requires minimum payment of $0.50 USD
            // If converted amount is less, set to minimum
            if (amountInUsd < 0.50m)
            {
                amountInUsd = 0.50m;
            }
            
            // Create Stripe Payment Intent
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amountInUsd * 100), // Stripe amounts are in cents
                Currency = "usd", // Stripe processes in USD
                PaymentMethodTypes = new List<string> { "card" }, // Accept card payments
                Metadata = new Dictionary<string, string>
                {
                    { "appointmentId", appointmentId }, // Store appointment ID for reference
                    { "originalAmountBDT", amount.ToString("F2") } // Store original BDT amount
                },
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true // Allow Stripe to suggest payment methods
                }
            };
            
            // Create payment intent via Stripe API
            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options);
            
            // Validate response
            if (string.IsNullOrEmpty(paymentIntent.ClientSecret))
            {
                throw new Exception("Failed to create payment intent: No client secret returned");
            }
            
            // Return client secret - this is used by Stripe.js on the frontend
            return paymentIntent.ClientSecret;
        }
        catch (StripeException ex)
        {
            // Stripe-specific errors (API errors, invalid keys, etc.)
            throw new Exception($"Stripe error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            // General errors
            throw new Exception($"Error creating payment intent: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Verifies that a payment intent was successfully paid
    /// Used to confirm payment before updating appointment status
    /// </summary>
    /// <param name="paymentIntentId">Stripe payment intent ID</param>
    /// <returns>True if payment succeeded, false otherwise</returns>
    public async Task<bool> VerifyPaymentIntentAsync(string paymentIntentId)
    {
        try
        {
            if (string.IsNullOrEmpty(paymentIntentId))
            {
                return false;
            }
            
            // Retrieve payment intent from Stripe API
            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId);
            
            // Check if payment status is "succeeded"
            // Other possible statuses: "requires_payment_method", "requires_confirmation", "processing", "canceled"
            return paymentIntent.Status == "succeeded";
        }
        catch (StripeException ex)
        {
            // Stripe API error - log and return false
            System.Diagnostics.Debug.WriteLine($"Stripe error verifying payment: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            // General error - log and return false
            System.Diagnostics.Debug.WriteLine($"Error verifying payment intent: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Creates a Stripe Checkout Session - Primary payment method
    /// Redirects user to Stripe's hosted checkout page for payment
    /// </summary>
    /// <param name="amount">Payment amount in BDT (Bangladeshi Taka)</param>
    /// <param name="appointmentId">Appointment ID for metadata</param>
    /// <param name="successUrl">URL to redirect after successful payment</param>
    /// <param name="cancelUrl">URL to redirect if user cancels</param>
    /// <returns>Stripe Checkout URL</returns>
    public async Task<string> CreateCheckoutSessionAsync(decimal amount, string appointmentId, string successUrl, string cancelUrl)
    {
        try
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));
            }
            
            if (string.IsNullOrEmpty(appointmentId))
            {
                throw new ArgumentException("Appointment ID is required", nameof(appointmentId));
            }
            
            // Convert BDT to USD for Stripe (approximate rate: 1 USD = 110 BDT)
            var amountInUsd = amount / 110m;
            
            // Ensure minimum amount (Stripe requires at least $0.50 USD)
            if (amountInUsd < 0.50m)
            {
                amountInUsd = 0.50m;
            }
            
            var options = new Stripe.Checkout.SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
                {
                    new Stripe.Checkout.SessionLineItemOptions
                    {
                        PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Appointment Payment (Appointment #{appointmentId})",
                                Description = $"Medical appointment consultation fee"
                            },
                            UnitAmount = (long)(amountInUsd * 100) // Convert to cents
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "appointmentId", appointmentId },
                    { "originalAmountBDT", amount.ToString("F2") }
                }
            };
            
            var service = new Stripe.Checkout.SessionService();
            var session = await service.CreateAsync(options);
            
            if (string.IsNullOrEmpty(session.Url))
            {
                throw new Exception("Failed to create checkout session: No URL returned");
            }
            
            return session.Url;
        }
        catch (StripeException ex)
        {
            throw new Exception($"Stripe error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error creating checkout session: {ex.Message}", ex);
        }
    }
    
    public async Task<bool> VerifyCheckoutSessionAsync(string sessionId)
    {
        try
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return false;
            }
            
            var service = new Stripe.Checkout.SessionService();
            var session = await service.GetAsync(sessionId);
            
            // Verify session is paid
            return session.PaymentStatus == "paid" && session.Status == "complete";
        }
        catch (StripeException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Stripe error verifying checkout session: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error verifying checkout session: {ex.Message}");
            return false;
        }
    }
}

