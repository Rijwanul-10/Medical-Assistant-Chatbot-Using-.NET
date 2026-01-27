# Setup Guide - Doctor Koi? Medical Assistant

## Quick Start

### 1. Install Dependencies
```bash
dotnet restore
```

### 2. Configure API Keys

Edit `appsettings.json`:

```json
{
  "Groq": {
    "ApiKey": "gsk_your_groq_api_key_here"
  },
  "Stripe": {
    "PublishableKey": "pk_test_51...",
    "SecretKey": "sk_test_51..."
  }
}
```

**Get Groq API Key:**
1. Visit https://console.groq.com/
2. Sign up/Login
3. Go to API Keys section
4. Create a new API key
5. Copy and paste into `appsettings.json`

**Get Stripe Test Keys:**
1. Visit https://dashboard.stripe.com/test/apikeys
2. Copy Publishable key and Secret key
3. Paste into `appsettings.json`

### 3. Run the Application

```bash
dotnet run
```

The application will:
- Create the database automatically
- Load CSV datasets into the database
- Start the web server

### 4. Access the Application

Open your browser and navigate to:
- `https://localhost:5001` (or the port shown in console)

### 5. Register/Login

1. Click "Register" to create an account
2. Fill in:
   - Name
   - Age (optional)
   - Address/Location (important for doctor recommendations)
   - Phone Number
   - Email
   - Password

3. After registration, you'll be automatically logged in

### 6. Start Chatting

1. The chatbot interface will appear
2. Try greeting: "hey whatsapp"
3. Share symptoms: "I have fever and severe headache"
4. Follow the conversation flow to book appointments

## Testing the Flow

### Example Conversation:

**You:** "hey whatsapp"  
**Bot:** "Hey! 😄 I'm your health assistant. How can I help you today?"

**You:** "I have fever and severe headache for the last two days"  
**Bot:** "I'm sorry you're feeling unwell 😟 Based on your symptoms, this could be **Dengue Fever**... Would you like me to recommend a suitable doctor for you?"

**You:** "yes"  
**Bot:** "I recommend consulting a **General Medicine Specialist**. 📍 **Dr. [Name]**... Would you like me to book an appointment for you?"

**You:** "yes"  
**Bot:** "Great! 👍 I'm booking an appointment... To confirm your appointment, please complete the payment."

**Payment:** Stripe payment modal will open. Use test card:
- Card: `4242 4242 4242 4242`
- Expiry: Any future date
- CVC: Any 3 digits
- ZIP: Any 5 digits

## Troubleshooting

### Database Issues
- Ensure SQL Server LocalDB is installed
- Check connection string in `appsettings.json`
- Try deleting the database and restarting (it will recreate)

### Groq API Errors
- Verify API key is correct
- Check internet connection
- Ensure you have API credits/quota

### Stripe Payment Issues
- Use test keys (not live keys)
- Verify keys are correctly formatted
- Check browser console for errors

### CSV Data Not Loading
- Ensure CSV files are in `Datasets/` folder
- Check file names match exactly
- Verify CSV format is correct

## File Structure

```
MedicalAssistant/
├── Datasets/              # CSV files (doctors, diseases, symptoms)
├── Controllers/           # API endpoints
├── Models/                # Data models
├── Services/              # Business logic (Groq, Stripe, etc.)
├── Views/                 # Razor views
├── wwwroot/              # Static files (CSS, JS)
└── appsettings.json      # Configuration
```

## Important Notes

- **Test Mode**: This application uses Stripe test mode. Use test card numbers only.
- **Groq API**: Free tier has rate limits. For production, consider upgrading.
- **Database**: Uses LocalDB by default. For production, use full SQL Server.
- **Security**: Never commit API keys to version control. Use environment variables or Azure Key Vault in production.

## Next Steps

1. Test the complete flow: Chat → Disease Detection → Doctor Recommendation → Appointment → Payment
2. Customize the chatbot personality in `Services/GroqChatbotService.cs`
3. Add more doctors to the CSV files
4. Enhance the UI/UX
5. Add email notifications for appointments
6. Implement appointment scheduling calendar

## Support

For issues or questions:
1. Check the README.md for detailed documentation
2. Review error messages in the console
3. Check browser developer tools for JavaScript errors
4. Verify all API keys are correctly configured

