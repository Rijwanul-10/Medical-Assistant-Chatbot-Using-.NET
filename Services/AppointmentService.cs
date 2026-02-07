using Microsoft.EntityFrameworkCore;
using MedicalAssistant.Data;
using MedicalAssistant.Models;

namespace MedicalAssistant.Services;

public class AppointmentService : IAppointmentService
{
    public async Task<Appointment> CreateAppointmentAsync(string sessionId, int doctorId, ApplicationDbContext context)
    {
        var doctor = await context.Doctors.FindAsync(doctorId);
        if (doctor == null)
        {
            throw new Exception("Doctor not found");
        }
        
        var appointment = new Appointment
        {
            UserId = sessionId,
            DoctorId = doctorId,
            AppointmentDate = DateTime.UtcNow.AddDays(1), // Default to tomorrow
            Status = "Pending",
            Amount = doctor.ConsultationFee,
            IsPaid = false
        };
        
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();
        
        return appointment;
    }
    
    public async Task<Appointment?> GetAppointmentByIdAsync(int appointmentId, ApplicationDbContext context)
    {
        return await context.Appointments
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);
    }
    
    public async Task UpdateAppointmentPaymentAsync(int appointmentId, string paymentIntentId, ApplicationDbContext context)
    {
        var appointment = await context.Appointments.FindAsync(appointmentId);
        if (appointment != null)
        {
            appointment.PaymentIntentId = paymentIntentId;
            appointment.IsPaid = true;
            appointment.Status = "Confirmed";
            await context.SaveChangesAsync();
        }
    }
}

