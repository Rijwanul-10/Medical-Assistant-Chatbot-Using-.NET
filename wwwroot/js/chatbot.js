// Make sure this script is loading - NO TRY-CATCH so errors are visible
console.log('=== CHATBOT.JS SCRIPT LOADING ===');

// Immediately expose a test function to verify script loaded - MUST BE FIRST
window.sendMessageTest = function() {
    console.log('sendMessageTest called - script is loaded!');
    if (typeof sendMessage === 'function') {
        sendMessage();
    } else {
        alert('sendMessage function not found! But script is loaded.');
    }
};

console.log('=== sendMessageTest function defined ===');

let stripe;
let paymentElement;
let currentAppointmentId = null;

// Store pending message if user tries to send before login
let pendingMessage = null;

// Initialize event listeners - wait for both DOM and script to be ready
function initializeChatbot() {
    console.log('Initializing chatbot...');
    console.log('sendMessage function available:', typeof sendMessage);
    
    // Input is enabled by default - chatbot is visible first
    const messageInput = document.getElementById('messageInput');
    const sendButton = document.getElementById('sendButton');
    
    console.log('messageInput found:', !!messageInput);
    console.log('sendButton found:', !!sendButton);
    
    if (!messageInput) {
        console.error('messageInput element not found!');
        return;
    }
    
    if (!sendButton) {
        console.error('sendButton element not found!');
        return;
    }
    
    messageInput.disabled = false;
    sendButton.disabled = false;
    
    // Attach event listener for send button
    sendButton.addEventListener('click', function(e) {
        e.preventDefault();
        e.stopPropagation();
        console.log('Send button clicked!');
        console.log('sendMessage type:', typeof sendMessage);
        if (typeof sendMessage === 'function') {
            sendMessage();
        } else {
            console.error('sendMessage function is not defined!');
            alert('Error: sendMessage function not found. Please refresh the page.');
        }
    });
    
    // Also attach Enter key handler
    messageInput.addEventListener('keypress', function(e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            console.log('Enter key pressed!');
            if (typeof sendMessage === 'function') {
                sendMessage();
            } else {
                console.error('sendMessage function is not defined!');
            }
        }
    });
    
    // Load chat history if authenticated (silently)
    loadChatHistory().catch(() => {
        // If not authenticated, that's fine - user can still see chatbot
    });
    
    console.log('Chatbot initialized successfully');
}

// Check authentication status on page load - but don't block UI
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOMContentLoaded fired');
    // Wait a tiny bit to ensure all scripts are loaded
    setTimeout(initializeChatbot, 100);
});

async function loadChatHistory() {
    try {
        const response = await fetch('/api/Chat/history');
        if (response.ok) {
            const messages = await response.json();
            const messagesContainer = document.getElementById('chatMessages');
            // Clear welcome message and add history
            messagesContainer.innerHTML = '';
            messages.forEach(msg => {
                addMessageToChat(msg.Message, msg.IsUser);
            });
        }
    } catch (error) {
        // Not authenticated or error - that's okay
    }
}

function showLogin(e) {
    if (e) e.preventDefault();
    const loginTab = document.getElementById('loginTab');
    const registerTab = document.getElementById('registerTab');
    const loginTabButton = document.getElementById('login-tab-btn');
    const registerTabButton = document.getElementById('register-tab-btn');
    
    if (loginTab) loginTab.style.display = 'block';
    if (registerTab) registerTab.style.display = 'none';
    
    // Update button styles
    if (loginTabButton) {
        loginTabButton.classList.remove('btn-outline-primary');
        loginTabButton.classList.add('btn-primary', 'active');
    }
    if (registerTabButton) {
        registerTabButton.classList.remove('btn-success', 'active');
        registerTabButton.classList.add('btn-outline-success');
    }
    return false;
}

function showRegister(e) {
    if (e) e.preventDefault();
    const loginTab = document.getElementById('loginTab');
    const registerTab = document.getElementById('registerTab');
    const loginTabButton = document.getElementById('login-tab-btn');
    const registerTabButton = document.getElementById('register-tab-btn');
    
    if (loginTab) loginTab.style.display = 'none';
    if (registerTab) registerTab.style.display = 'block';
    
    // Update button styles - Register is active
    if (loginTabButton) {
        loginTabButton.classList.remove('btn-primary', 'active');
        loginTabButton.classList.add('btn-outline-primary');
    }
    if (registerTabButton) {
        registerTabButton.classList.remove('btn-outline-success');
        registerTabButton.classList.add('btn-success', 'active');
        registerTabButton.style.backgroundColor = '#28a745';
        registerTabButton.style.borderColor = '#28a745';
        registerTabButton.style.color = 'white';
    }
    return false;
}

// On page load, ensure register tab is shown by default in modal
document.addEventListener('DOMContentLoaded', function() {
    const registerTab = document.getElementById('registerTab');
    const loginTab = document.getElementById('loginTab');
    if (registerTab && loginTab) {
        registerTab.style.display = 'block';
        loginTab.style.display = 'none';
    }
});

// Handle Enter key press
function handleKeyPress(event) {
    if (event.key === 'Enter') {
        sendMessage();
    }
}

async function handleLogin() {
    const email = document.getElementById('loginEmail').value;
    const password = document.getElementById('loginPassword').value;
    const rememberMe = document.getElementById('rememberMe').checked;
    
    try {
        const response = await fetch('/Account/Login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ email, password, rememberMe })
        });
        
        const result = await response.json();
        if (result.success) {
            // Close modal
            const authModal = bootstrap.Modal.getInstance(document.getElementById('authModal'));
            if (authModal) {
                authModal.hide();
            }
            
            // If there was a pending message, send it now
            if (pendingMessage) {
                const messageToSend = pendingMessage;
                pendingMessage = null;
                // Small delay to ensure auth is set
                setTimeout(() => {
                    addMessageToChat(messageToSend, true);
                    sendAuthenticatedMessage(messageToSend);
                }, 500);
            } else {
                // Reload to get authenticated state
                location.reload();
            }
        } else {
            alert('Login failed: ' + (result.errors || 'Invalid credentials'));
        }
    } catch (error) {
        alert('Error: ' + error.message);
    }
}

async function handleRegister() {
    const password = document.getElementById('regPassword').value;
    const confirmPassword = document.getElementById('regConfirmPassword').value;
    
    if (password !== confirmPassword) {
        alert('Passwords do not match');
        return;
    }
    
    const data = {
        name: document.getElementById('regName').value,
        age: parseInt(document.getElementById('regAge').value) || null,
        phoneNumber: document.getElementById('regPhone').value,
        address: document.getElementById('regAddress').value,
        email: document.getElementById('regEmail').value,
        password: password,
        confirmPassword: confirmPassword
    };
    
    try {
        const response = await fetch('/Account/Register', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(data)
        });
        
        const result = await response.json();
        if (result.success) {
            // Close modal
            const authModal = bootstrap.Modal.getInstance(document.getElementById('authModal'));
            if (authModal) {
                authModal.hide();
            }
            
            // If there was a pending message, send it now
            if (pendingMessage) {
                const messageToSend = pendingMessage;
                pendingMessage = null;
                // Small delay to ensure auth is set
                setTimeout(() => {
                    addMessageToChat(messageToSend, true);
                    sendAuthenticatedMessage(messageToSend);
                }, 500);
            } else {
                // Reload to get authenticated state
                location.reload();
            }
        } else {
            alert('Registration failed: ' + (result.errors || 'Please check your input'));
        }
    } catch (error) {
        alert('Error: ' + error.message);
    }
}

async function sendMessage() {
    console.log('sendMessage function called!');
    const input = document.getElementById('messageInput');
    if (!input) {
        console.error('messageInput element not found!');
        return;
    }
    
    const message = input.value.trim();
    console.log('Message to send:', message);
    
    if (!message) {
        console.log('Message is empty, returning');
        return;
    }
    
    // Store message in case we need to show auth modal
    pendingMessage = message;
    
    // Disable input while processing
    input.disabled = true;
    document.getElementById('sendButton').disabled = true;
    
    try {
        const response = await fetch('/api/Chat/send', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ message })
        });
        
        // Check for 401 Unauthorized FIRST
        if (response.status === 401) {
            // User not authenticated - show auth modal
            console.log('User not authenticated, showing auth modal');
            showAuthModal();
            input.disabled = false;
            document.getElementById('sendButton').disabled = false;
            return;
        }
        
        // Check for other errors
        if (!response.ok) {
            const errorText = await response.text();
            console.error('API Error:', response.status, errorText);
            addMessageToChat('Sorry, I encountered an error. Please try again.', false);
            input.disabled = false;
            document.getElementById('sendButton').disabled = false;
            return;
        }
        
        // Clear pending message since we're authenticated
        pendingMessage = null;
        
        // Add user message to chat
        addMessageToChat(message, true);
        input.value = '';
        input.disabled = false;
        document.getElementById('sendButton').disabled = false;
        
        const result = await response.json();
        
        // Add bot response
        addMessageToChat(result.message, false);
        
        // Check if response contains appointment booking confirmation with payment
        if (result.appointmentId && result.doctor) {
            showPaymentModal(result.doctor, result.appointmentId);
        }
        
        // Check if response contains doctor recommendation
        if (result.message && result.message.includes('Would you like me to book an appointment') && result.doctor) {
            // Store doctor info for later booking
            window.currentRecommendedDoctor = result.doctor;
        }
        
        // Check if user wants to book appointment (says yes after recommendation)
        const lowerMessage = message.toLowerCase();
        if ((lowerMessage.includes('yes') || lowerMessage.includes('book')) && window.currentRecommendedDoctor) {
            handleAppointmentBooking(window.currentRecommendedDoctor);
        }
        
    } catch (error) {
        console.error('Network error:', error);
        // Network error - could be 401 or other issue
        addMessageToChat('Please login or register to continue. Showing login modal...', false);
        showAuthModal();
        input.disabled = false;
        document.getElementById('sendButton').disabled = false;
    }
}

function showAuthModal() {
    console.log('showAuthModal called');
    const modalElement = document.getElementById('authModal');
    
    if (!modalElement) {
        console.error('Auth modal element not found!');
        alert('Please login or register to continue. Redirecting to login page...');
        window.location.href = '/Account/Login';
        return;
    }
    
    // Check if Bootstrap is available
    if (typeof bootstrap === 'undefined') {
        console.error('Bootstrap is not loaded!');
        // Fallback: show modal manually
        modalElement.style.display = 'block';
        modalElement.classList.add('show');
        document.body.classList.add('modal-open');
        const backdrop = document.createElement('div');
        backdrop.className = 'modal-backdrop fade show';
        backdrop.id = 'authModalBackdrop';
        document.body.appendChild(backdrop);
    } else {
        // Show Bootstrap modal
        const authModal = new bootstrap.Modal(modalElement, {
            backdrop: 'static',
            keyboard: false
        });
        authModal.show();
    }
    
    // Ensure register tab is shown by default
    const registerTab = document.getElementById('registerTab');
    const loginTab = document.getElementById('loginTab');
    if (registerTab && loginTab) {
        registerTab.style.display = 'block';
        loginTab.style.display = 'none';
    }
    
    // Update button styles
    const loginTabButton = document.getElementById('login-tab-btn');
    const registerTabButton = document.getElementById('register-tab-btn');
    if (loginTabButton && registerTabButton) {
        loginTabButton.classList.remove('btn-primary', 'active');
        loginTabButton.classList.add('btn-outline-primary');
        registerTabButton.classList.remove('btn-outline-success');
        registerTabButton.classList.add('btn-success', 'active');
    }
}

async function sendAuthenticatedMessage(message) {
    try {
        const response = await fetch('/api/Chat/send', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ message })
        });
        
        if (response.ok) {
            const result = await response.json();
            addMessageToChat(result.message, false);
            
            // Check if response contains appointment booking confirmation with payment
            if (result.appointmentId && result.doctor) {
                showPaymentModal(result.doctor, result.appointmentId);
            }
            
            // Check if response contains doctor recommendation
            if (result.message.includes('Would you like me to book an appointment') && result.doctor) {
                window.currentRecommendedDoctor = result.doctor;
            }
        }
    } catch (error) {
        addMessageToChat('Sorry, I encountered an error. Please try again.', false);
        console.error('Error:', error);
    }
}

function handleDoctorRecommendation(result) {
    // This will be called when doctor is recommended
    // The user can say "yes" to book appointment
}

async function handleAppointmentBooking(doctor) {
    if (doctor && doctor.id) {
        try {
            const response = await fetch('/api/Chat/book-appointment', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ doctorId: doctor.id })
            });
            
            const appointmentResult = await response.json();
            
            if (appointmentResult.appointmentId) {
                currentAppointmentId = appointmentResult.appointmentId;
                // Add booking confirmation message
                addMessageToChat(appointmentResult.message, false);
                // Show payment modal
                showPaymentModal(appointmentResult.doctor, appointmentResult.appointmentId);
            }
        } catch (error) {
            console.error('Error booking appointment:', error);
            addMessageToChat('Sorry, I encountered an error booking your appointment. Please try again.', false);
        }
    }
}

function addMessageToChat(message, isUser) {
    const messagesContainer = document.getElementById('chatMessages');
    const messageDiv = document.createElement('div');
    messageDiv.className = `message ${isUser ? 'user-message' : 'bot-message'}`;
    
    // Convert markdown-style formatting to HTML
    let formattedMessage = message
        .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
        .replace(/\n/g, '<br>');
    
    messageDiv.innerHTML = formattedMessage;
    messagesContainer.appendChild(messageDiv);
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

// Removed duplicate loadChatHistory - already defined above
// Removed duplicate DOMContentLoaded - already handled above

async function showPaymentModal(doctor, appointmentId) {
    currentAppointmentId = appointmentId;
    
    // For now, redirect to payment page
    window.location.href = `/Payment?appointmentId=${appointmentId}`;
}

async function confirmPayment() {
    // This will be handled in the payment page
}

