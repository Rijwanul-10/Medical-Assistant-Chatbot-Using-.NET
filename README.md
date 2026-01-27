# Doctor Koi? - Medical Assistant Chatbot

An AI-powered medical chatbot integrated into an ASP.NET Core MVC web application that provides health guidance, doctor recommendations, appointment booking, and payment processing.

## 📋 Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation & Setup](#installation--setup)
- [Configuration](#configuration)
- [Database Setup](#database-setup)
- [Project Structure](#project-structure)
- [Architecture](#architecture)
- [API Endpoints](#api-endpoints)
- [Usage Guide](#usage-guide)
- [Technologies Used](#technologies-used)
- [Troubleshooting](#troubleshooting)
- [License](#license)

## ✨ Features

### Core Features
- 🤖 **AI-Powered Chatbot** - Natural language conversation using Groq API (LLM)
- 👤 **User Authentication** - MongoDB-based login/registration system
- 🏥 **Doctor Recommendations** - Location and specialty-based doctor suggestions
- 📅 **Appointment Booking** - Automated appointment creation system
- 💳 **Stripe Payment Integration** - Secure payment processing for appointments
- 📊 **Dataset-Driven Analysis** - Disease and symptom analysis from CSV datasets
- 📜 **Chat History** - View previous conversations (login required)
- 🎨 **Modern UI** - Responsive design with gradient animations

### Key Capabilities
- **Symptom Analysis**: Detects diseases based on user symptoms
- **Location-Based Search**: Finds doctors near user's location
- **Payment Processing**: Stripe Checkout integration for appointment fees
- **Session Management**: Maintains conversation context across messages
- **History Tracking**: Saves all chat messages to database

## 🔧 Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 10.0 SDK** or later ([Download](https://dotnet.microsoft.com/download))
- **SQL Server** (LocalDB, SQL Server Express, or SQL Server) ([Download](https://www.microsoft.com/sql-server/sql-server-downloads))
- **MongoDB** (Community Server or MongoDB Atlas) ([Download](https://www.mongodb.com/try/download/community))
- **Groq API Key** ([Get one here](https://console.groq.com/))
- **Stripe API Keys** ([Get test keys here](https://dashboard.stripe.com/test/apikeys))

## 🚀 Installation & Setup

### Step 1: Clone the Repository

```bash
git clone <repository-url>
cd "Medical Assistant-cursor"
```

### Step 2: Install Dependencies

```bash
dotnet restore
```

### Step 3: Configure API Keys

Edit `appsettings.json` and add your API keys:

```json
{
  "Groq": {
    "ApiKey": "your-groq-api-key-here"
  },
  "Stripe": {
    "PublishableKey": "pk_test_your_publishable_key",
    "SecretKey": "sk_test_your_secret_key"
  }
}
```

### Step 4: Setup Databases

#### SQL Server Setup
The SQL Server database will be created automatically on first run. Ensure SQL Server is running.

#### MongoDB Setup
1. **Local MongoDB**:
   - Install MongoDB Community Server
   - Start MongoDB service
   - Default connection: `mongodb://localhost:27017`

2. **MongoDB Atlas** (Cloud):
   - Create account at [MongoDB Atlas](https://www.mongodb.com/cloud/atlas)
   - Create a cluster
   - Get connection string
   - Update `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "MongoDB": "mongodb+srv://username:password@cluster.mongodb.net/"
     }
   }
   ```

See [MONGODB_SETUP.md](./MONGODB_SETUP.md) for detailed MongoDB setup instructions.

### Step 5: Run the Application

```bash
dotnet run
```

Navigate to `https://localhost:5001` (or the port shown in the console).

## ⚙️ Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MedicalAssistantDb;Trusted_Connection=True;MultipleActiveResultSets=true",
    "MongoDB": "mongodb://localhost:27017"
  },
  "MongoDB": {
    "DatabaseName": "MedicalAssistantDB"
  },
  "Stripe": {
    "PublishableKey": "pk_test_...",
    "SecretKey": "sk_test_..."
  },
  "Groq": {
    "ApiKey": "gsk_..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Environment Variables (Optional)

You can also use environment variables:

- `ASPNETCORE_HTTPS_PORT` - HTTPS port for development
- `ConnectionStrings__DefaultConnection` - SQL Server connection string
- `ConnectionStrings__MongoDB` - MongoDB connection string
- `Groq__ApiKey` - Groq API key
- `Stripe__PublishableKey` - Stripe publishable key
- `Stripe__SecretKey` - Stripe secret key

## 🗄️ Database Setup

### SQL Server Database

The application uses Entity Framework Core with SQL Server for:
- **Doctors** - Doctor information and specialties
- **Diseases** - Disease data and descriptions
- **Symptoms** - Symptom information
- **Appointments** - Appointment records
- **ChatMessages** - Chat history (when logged in)

**Automatic Setup**:
- Database is created automatically on first run
- CSV datasets are loaded automatically from `Datasets/` folder
- Tables are created using Entity Framework migrations

**Manual Setup** (if needed):
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### MongoDB Database

MongoDB is used for:
- **Users** - User authentication and profile data

**Collections**:
- `Users` - Stores user registration information

**Setup**:
1. Ensure MongoDB is running
2. Database and collection are created automatically on first user registration
3. See [HOW_TO_SEE_DATABASE.md](./HOW_TO_SEE_DATABASE.md) for viewing data in MongoDB Compass

### CSV Datasets

The following CSV files in `Datasets/` are automatically loaded:

- `doctors_info_1.csv` - Doctor information (part 1)
- `doctors_info_2.csv` - Doctor information (part 2)
- `Disease and symptoms dataset.csv` - Disease-symptom mappings
- `disease_description.csv` - Disease descriptions
- `Disease_Specialist.csv` - Disease to specialist mappings
- `Original_Dataset.csv` - Original dataset for symptom matching
- `Symptom_Weights.csv` - Symptom weights for matching

## 📁 Project Structure

```
MedicalAssistant/
├── Controllers/
│   ├── HomeController.cs          # Main page controller
│   ├── AccountController.cs       # Login/Registration endpoints
│   ├── ChatController.cs          # Chatbot API endpoints
│   └── PaymentController.cs       # Stripe payment handling
│
├── Models/
│   ├── ApplicationUser.cs         # User model (SQL Server)
│   ├── MongoUser.cs               # User model (MongoDB)
│   ├── Doctor.cs                  # Doctor information
│   ├── Disease.cs                 # Disease data
│   ├── Symptom.cs                 # Symptom information
│   ├── Appointment.cs             # Appointment records
│   ├── ChatMessage.cs             # Chat history
│   └── StripeSettings.cs          # Stripe configuration
│
├── Services/
│   ├── IChatbotService.cs         # Chatbot service interface
│   ├── SimpleChatbotService.cs    # Main chatbot service
│   ├── GroqChatbotService.cs      # Groq API integration
│   ├── IDoctorService.cs          # Doctor service interface
│   ├── DoctorService.cs           # Doctor recommendation logic
│   ├── IAppointmentService.cs     # Appointment service interface
│   ├── AppointmentService.cs      # Appointment management
│   ├── IPaymentService.cs         # Payment service interface
│   ├── PaymentService.cs          # Stripe payment processing
│   ├── DiseaseMatchingService.cs  # Disease-symptom matching
│   └── MongoUserService.cs        # MongoDB user operations
│
├── Data/
│   ├── ApplicationDbContext.cs    # Entity Framework context
│   └── SeedData.cs               # CSV data loading
│
├── Views/
│   ├── Home/
│   │   └── Index.cshtml          # Main chatbot interface
│   ├── Payment/
│   │   ├── Index.cshtml          # Payment page
│   │   └── Success.cshtml        # Payment success page
│   └── Shared/
│       ├── _Layout.cshtml        # Main layout
│       └── _LoginPartial.cshtml  # Login partial view
│
├── wwwroot/
│   ├── css/                      # Stylesheets
│   └── js/
│       └── chatbot.js           # Client-side JavaScript
│
├── Datasets/                     # CSV data files
│   ├── doctors_info_1.csv
│   ├── doctors_info_2.csv
│   ├── Disease and symptoms dataset.csv
│   ├── disease_description.csv
│   ├── Disease_Specialist.csv
│   ├── Original_Dataset.csv
│   └── Symptom_Weights.csv
│
├── Program.cs                    # Application startup
├── appsettings.json              # Configuration
├── MedicalAssistant.csproj       # Project file
├── README.md                     # This file
├── MONGODB_SETUP.md             # MongoDB setup guide
└── HOW_TO_SEE_DATABASE.md        # Database viewing guide
```

## 🏗️ Architecture

### System Architecture

```
┌─────────────────┐
│   Web Browser   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  ASP.NET Core   │
│      MVC        │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
┌────────┐ ┌──────────┐
│  SQL   │ │ MongoDB  │
│ Server │ │          │
└────────┘ └──────────┘
    │         │
    │         │
    ▼         ▼
┌─────────────────┐
│  External APIs  │
│  - Groq API     │
│  - Stripe API   │
└─────────────────┘
```

### Data Flow

1. **User Input** → Chatbot Interface
2. **Message Processing** → SimpleChatbotService
3. **Disease Detection** → DiseaseMatchingService
4. **Doctor Recommendation** → DoctorService
5. **Appointment Creation** → AppointmentService
6. **Payment Processing** → PaymentService (Stripe)
7. **Data Storage** → SQL Server (appointments, doctors) + MongoDB (users)

### Service Layer

- **SimpleChatbotService**: Main chatbot logic, conversation state management
- **GroqChatbotService**: Groq API integration for natural language processing
- **DoctorService**: Doctor search and recommendation algorithms
- **AppointmentService**: Appointment CRUD operations
- **PaymentService**: Stripe payment intent and checkout session creation
- **DiseaseMatchingService**: Symptom-to-disease matching using CSV data
- **MongoUserService**: User authentication and profile management

## 🔌 API Endpoints

### Chat Endpoints

#### `POST /api/Chat/send`
Send a message to the chatbot.

**Request Body**:
```json
{
  "message": "I have a headache and fever"
}
```

**Response**:
```json
{
  "message": "Based on your symptoms...",
  "appointmentId": 123,
  "doctorInfo": {
    "id": 1,
    "name": "Dr. John Doe",
    "specialty": "General Medicine",
    "fee": 500
  }
}
```

#### `GET /api/Chat/history`
Get chat history (requires authentication).

**Response**:
```json
[
  {
    "message": "Hello",
    "isFromUser": true,
    "timestamp": "2024-01-01T10:00:00Z"
  },
  {
    "message": "How can I help?",
    "isFromUser": false,
    "timestamp": "2024-01-01T10:00:01Z"
  }
]
```

### Account Endpoints

#### `POST /Account/Login`
User login.

**Request Body**:
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Login successful! Welcome back, John!",
  "user": {
    "name": "John Doe",
    "email": "user@example.com"
  }
}
```

#### `POST /Account/Register`
User registration.

**Request Body**:
```json
{
  "name": "John Doe",
  "email": "user@example.com",
  "password": "password123",
  "confirmPassword": "password123",
  "age": 30,
  "phoneNumber": "+1234567890",
  "address": "123 Main St"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Registration successful! You are now logged in.",
  "user": {
    "name": "John Doe",
    "email": "user@example.com"
  }
}
```

#### `GET /Account/GetCurrentUser`
Get current user information.

**Response**:
```json
{
  "isLoggedIn": true,
  "name": "John Doe",
  "email": "user@example.com"
}
```

#### `POST /Account/Logout`
User logout.

**Response**:
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```

### Payment Endpoints

#### `POST /Payment/create-checkout-session`
Create Stripe checkout session.

**Request Body**:
```json
{
  "amount": 500,
  "appointmentId": 123
}
```

**Response**:
```json
{
  "url": "https://checkout.stripe.com/pay/cs_..."
}
```

#### `GET /Payment/Success`
Payment success page (redirected from Stripe).

**Query Parameters**:
- `session_id` - Stripe checkout session ID
- `appointmentId` - Appointment ID

## 📖 Usage Guide

### For Users

#### 1. Starting a Conversation
- Open the application in your browser
- Type your message in the chat input
- The bot will respond with health guidance

#### 2. Getting Doctor Recommendations
- Describe your symptoms (e.g., "I have fever and headache")
- The bot will analyze symptoms and suggest possible diseases
- Provide your location when asked
- The bot will recommend suitable doctors

#### 3. Booking an Appointment
- When a doctor is recommended, say "yes" to book
- You'll be redirected to Stripe payment page
- Complete payment to confirm appointment
- View appointment details on success page

#### 4. Viewing Chat History
- Login or register first
- Click the "📜 History" button (top right)
- Side panel will show all previous conversations
- Click outside or X to close

#### 5. User Registration/Login
- Click "Login" or "Register" button (top right)
- Fill in the required information
- Registration automatically logs you in
- Login is optional for chatting, required for viewing history

### For Developers

#### Running in Development
```bash
dotnet run
```

#### Building for Production
```bash
dotnet build -c Release
```

#### Database Migrations
```bash
# Create migration
dotnet ef migrations add MigrationName

# Apply migration
dotnet ef database update
```

#### Checking MongoDB Status
```bash
# Windows
check-mongodb.bat

# PowerShell
check-mongodb.ps1
```

## 🛠️ Technologies Used

### Backend
- **ASP.NET Core MVC 10.0** - Web framework
- **Entity Framework Core 10.0** - ORM for SQL Server
- **ASP.NET Identity** - Authentication framework
- **MongoDB.Driver 2.28.0** - MongoDB client
- **Stripe.net 50.2.0** - Stripe payment SDK
- **CsvHelper 30.0.1** - CSV file parsing
- **Newtonsoft.Json 13.0.3** - JSON serialization

### Frontend
- **HTML5/CSS3** - Markup and styling
- **JavaScript (ES6+)** - Client-side logic
- **Bootstrap 5** - UI framework (if used)
- **Font Awesome** - Icons

### External Services
- **Groq API** - Large Language Model for chatbot
- **Stripe API** - Payment processing
- **SQL Server** - Relational database
- **MongoDB** - NoSQL database for users

## 🐛 Troubleshooting

### Common Issues

#### 1. Database Connection Errors

**SQL Server**:
- Ensure SQL Server is running
- Check connection string in `appsettings.json`
- Verify database name and server name

**MongoDB**:
- Ensure MongoDB service is running
- Check connection string
- Run `check-mongodb.bat` to verify status
- See [MONGODB_SETUP.md](./MONGODB_SETUP.md) for setup help

#### 2. API Key Errors

**Groq API**:
- Verify API key in `appsettings.json`
- Check API key is active in Groq console
- Ensure you have API credits

**Stripe API**:
- Use test keys for development
- Verify keys are correct in `appsettings.json`
- Check Stripe dashboard for errors

#### 3. CSV Data Not Loading

- Ensure CSV files are in `Datasets/` folder
- Check file names match exactly
- Verify CSV format is correct
- Check application logs for errors

#### 4. Payment Not Working

- Verify Stripe keys are correct
- Check browser console for errors
- Ensure appointment ID is valid
- Verify Stripe test mode is enabled

#### 5. Chat History Not Showing

- Ensure user is logged in
- Check MongoDB is running
- Verify user ID in session
- Check browser console for errors

#### 6. HTTPS Redirect Errors

- Set `ASPNETCORE_HTTPS_PORT` environment variable
- Or configure HTTPS in `launchSettings.json`
- Or disable HTTPS redirection in development

### Debugging Tips

1. **Check Application Logs**:
   - Logs are written to console
   - Check for error messages

2. **Browser Developer Tools**:
   - Open F12 console
   - Check for JavaScript errors
   - Monitor network requests

3. **Database Verification**:
   - SQL Server: Use SQL Server Management Studio
   - MongoDB: Use MongoDB Compass
   - See [HOW_TO_SEE_DATABASE.md](./HOW_TO_SEE_DATABASE.md)

4. **API Testing**:
   - Use Postman or curl to test endpoints
   - Verify request/response formats
   - Check authentication headers

## 📝 Additional Documentation

- [MONGODB_SETUP.md](./MONGODB_SETUP.md) - Detailed MongoDB setup guide
- [HOW_TO_SEE_DATABASE.md](./HOW_TO_SEE_DATABASE.md) - How to view data in MongoDB Compass

## 🔒 Security Notes

- **API Keys**: Never commit API keys to version control
- **Passwords**: User passwords are hashed using SHA256
- **Sessions**: Session data is stored server-side
- **HTTPS**: Use HTTPS in production
- **Stripe**: Use test keys for development, live keys for production

## 📄 License

This project is for educational/demonstration purposes.

## 👥 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📧 Support

For issues and questions:
1. Check the [Troubleshooting](#troubleshooting) section
2. Review the documentation files
3. Check application logs
4. Open an issue on the repository

---

**Built with ❤️ using ASP.NET Core and AI**
