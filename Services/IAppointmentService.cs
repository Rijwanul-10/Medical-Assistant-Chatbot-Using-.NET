using MedicalAssistant.Models;
using MedicalAssistant.Data;

namespace MedicalAssistant.Services;

public interface IAppointmentService
{
    Task<Appointment> CreateAppointmentAsync(string sessionId, int doctorId, ApplicationDbContext context);
    Task<Appointment?> GetAppointmentByIdAsync(int appointmentId, ApplicationDbContext context);
    Task UpdateAppointmentPaymentAsync(int appointmentId, string paymentIntentId, ApplicationDbContext context);
}

