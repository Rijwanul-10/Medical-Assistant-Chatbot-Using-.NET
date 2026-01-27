# API Documentation

Complete API reference for Doctor Koi? Medical Assistant Chatbot.

## Base URL

- **Development**: `https://localhost:5001` or `http://localhost:5000`
- **Production**: Your production domain

## Authentication

Most endpoints use session-based authentication. User must be logged in for protected endpoints.

### Session Management
- Sessions are managed server-side
- Session ID is stored in cookies
- User ID is stored in session after login

## Endpoints

### Chat Endpoints

#### Send Message
Send a message to the chatbot.

**Endpoint**: `POST /api/Chat/send`

**Authentication**: Not required

**Request Body**:
```json
{
  "message": "I have a headache and fever"
}
```

**Response** (200 OK):
```json
{
  "message": "Based on your symptoms, this could be Dengue Fever. Would you like me to recommend a doctor?",
  "appointmentId": null,
  "doctorInfo": null
}
```

**Response with Appointment** (200 OK):
```json
{
  "message": "Great! I'm opening the payment window to confirm your appointment.",
  "appointmentId": 123,
  "doctorInfo": {
    "id": 1,
    "name": "Dr. John Doe",
    "specialty": "General Medicine",
    "location": "Dhaka",
    "chamber": "City Hospital",
    "fee": 500
  }
}
```

**Error Response** (500):
```json
{
  "error": "An error occurred: ...",
  "message": "I'm sorry, but I encountered an error. Please try again."
}
```

---

#### Get Chat History
Retrieve chat history for logged-in user.

**Endpoint**: `GET /api/Chat/history`

**Authentication**: Required (must be logged in)

**Request Headers**:
- Cookie: Session cookie

**Response** (200 OK):
```json
[
  {
    "message": "Hello",
    "isFromUser": true,
    "timestamp": "2024-01-01T10:00:00Z"
  },
  {
    "message": "How can I help you today?",
    "isFromUser": false,
    "timestamp": "2024-01-01T10:00:01Z"
  }
]
```

**Error Response** (401 Unauthorized):
```json
{
  "error": "Please login to view chat history"
}
```

---

### Account Endpoints

#### Login
Authenticate user and create session.

**Endpoint**: `POST /Account/Login`

**Authentication**: Not required

**Request Headers**:
```
Content-Type: application/json
Accept: application/json
```

**Request Body**:
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Response** (200 OK):
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

**Error Response** (200 OK with errors):
```json
{
  "success": false,
  "errors": ["Invalid email or password."]
}
```

**Error Response** (500):
```json
{
  "success": false,
  "errors": ["Database error: Cannot connect to MongoDB. Please ensure MongoDB is running."]
}
```

---

#### Register
Create new user account.

**Endpoint**: `POST /Account/Register`

**Authentication**: Not required

**Request Headers**:
```
Content-Type: application/json
Accept: application/json
```

**Request Body**:
```json
{
  "name": "John Doe",
  "email": "user@example.com",
  "password": "password123",
  "confirmPassword": "password123",
  "age": 30,
  "phoneNumber": "+1234567890",
  "address": "123 Main St, City"
}
```

**Required Fields**:
- `name` (string)
- `email` (string, valid email format)
- `password` (string, minimum 6 characters)
- `confirmPassword` (string, must match password)

**Optional Fields**:
- `age` (integer)
- `phoneNumber` (string)
- `address` (string)

**Response** (200 OK):
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

**Error Response** (200 OK with errors):
```json
{
  "success": false,
  "errors": ["Email already registered"]
}
```

---

#### Get Current User
Get information about currently logged-in user.

**Endpoint**: `GET /Account/GetCurrentUser`

**Authentication**: Not required (returns logged out status if not authenticated)

**Response** (200 OK - Logged In):
```json
{
  "isLoggedIn": true,
  "name": "John Doe",
  "email": "user@example.com"
}
```

**Response** (200 OK - Not Logged In):
```json
{
  "isLoggedIn": false,
  "name": null,
  "email": null
}
```

---

#### Logout
End user session.

**Endpoint**: `POST /Account/Logout`

**Authentication**: Not required (safe to call even if not logged in)

**Request Headers**:
```
Content-Type: application/json
```

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```

---

### Payment Endpoints

#### Create Checkout Session
Create Stripe checkout session for appointment payment.

**Endpoint**: `POST /Payment/create-checkout-session`

**Authentication**: Not required (but appointment must belong to session user)

**Request Headers**:
```
Content-Type: application/json
Accept: application/json
```

**Request Body**:
```json
{
  "amount": 500,
  "appointmentId": 123
}
```

**Response** (200 OK):
```json
{
  "url": "https://checkout.stripe.com/pay/cs_test_..."
}
```

**Error Response** (400 Bad Request):
```json
{
  "error": "Invalid amount"
}
```

**Error Response** (404 Not Found):
```json
{
  "error": "Appointment not found"
}
```

**Error Response** (400 Bad Request):
```json
{
  "error": "This appointment has already been paid."
}
```

**Error Response** (500):
```json
{
  "error": "Error creating checkout session: ..."
}
```

---

#### Payment Success
Handle payment success callback from Stripe.

**Endpoint**: `GET /Payment/Success`

**Authentication**: Not required

**Query Parameters**:
- `session_id` (string, required) - Stripe checkout session ID
- `appointmentId` (integer, required) - Appointment ID

**Response**: HTML page showing payment success

**Process**:
1. Verifies Stripe checkout session
2. Updates appointment payment status
3. Displays success page
4. Redirects to home after delay

---

## Error Codes

| Status Code | Description |
|------------|-------------|
| 200 | Success |
| 400 | Bad Request - Invalid input or business logic error |
| 401 | Unauthorized - Authentication required |
| 404 | Not Found - Resource not found |
| 500 | Internal Server Error - Server error |

## Request/Response Formats

### Content Types
- **Request**: `application/json`
- **Response**: `application/json` (API endpoints) or `text/html` (views)

### Date Formats
- All timestamps use ISO 8601 format: `YYYY-MM-DDTHH:mm:ssZ`
- Example: `2024-01-01T10:00:00Z`

### Currency
- All amounts are in **BDT (Bangladeshi Taka)**
- Stripe converts to USD automatically (1 BDT ≈ 0.009 USD)

## Rate Limiting

Currently, there are no rate limits implemented. Consider implementing rate limiting for production use.

## CORS

CORS is configured to allow all origins (`*`). For production, restrict to specific domains.

## Session Configuration

- **Idle Timeout**: 30 minutes
- **Cookie HttpOnly**: true
- **Cookie IsEssential**: true

## Testing

### Using cURL

**Send Message**:
```bash
curl -X POST https://localhost:5001/api/Chat/send \
  -H "Content-Type: application/json" \
  -d '{"message":"Hello"}'
```

**Login**:
```bash
curl -X POST https://localhost:5001/Account/Login \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d '{"email":"user@example.com","password":"password123"}' \
  -c cookies.txt
```

**Get History** (with session cookie):
```bash
curl -X GET https://localhost:5001/api/Chat/history \
  -b cookies.txt
```

### Using Postman

1. Create a new request
2. Set method (GET/POST)
3. Set URL
4. Add headers:
   - `Content-Type: application/json`
   - `Accept: application/json`
5. Add body (for POST requests) in JSON format
6. For authenticated requests, include session cookie from login response

## Webhooks

Currently, no webhooks are implemented. Stripe webhooks can be added for:
- Payment confirmation
- Payment failure notifications
- Refund processing

## Security Considerations

1. **API Keys**: Never expose API keys in client-side code
2. **Passwords**: Passwords are hashed server-side using SHA256
3. **Sessions**: Session data is stored server-side, not in cookies
4. **HTTPS**: Always use HTTPS in production
5. **Input Validation**: All inputs are validated server-side
6. **SQL Injection**: Protected by Entity Framework parameterized queries
7. **XSS**: Output is escaped in views

## Versioning

Currently, no API versioning is implemented. Consider adding versioning for future updates:
- `/api/v1/Chat/send`
- `/api/v2/Chat/send`

---

**Last Updated**: 2024-01-18
