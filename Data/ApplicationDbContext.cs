using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MedicalAssistant.Models;

namespace MedicalAssistant.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Doctor> Doctors { get; set; }
    public DbSet<Disease> Diseases { get; set; }
    public DbSet<Symptom> Symptoms { get; set; }
    public DbSet<DiseaseSymptom> DiseaseSymptoms { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Configure decimal precision
        builder.Entity<Appointment>()
            .Property(a => a.Amount)
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18, 2)");
            
        builder.Entity<Doctor>()
            .Property(d => d.ConsultationFee)
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18, 2)");
        
        // Configure relationships
        builder.Entity<DiseaseSymptom>()
            .HasKey(ds => ds.Id);
            
        builder.Entity<DiseaseSymptom>()
            .HasOne(ds => ds.Disease)
            .WithMany(d => d.DiseaseSymptoms)
            .HasForeignKey(ds => ds.DiseaseId);
            
        builder.Entity<DiseaseSymptom>()
            .HasOne(ds => ds.Symptom)
            .WithMany(s => s.DiseaseSymptoms)
            .HasForeignKey(ds => ds.SymptomId);
        
        // Ignore User navigation properties - UserId is just a string (session ID), not a foreign key
        // This prevents EF Core from creating foreign key constraints
        builder.Entity<ChatMessage>()
            .Ignore(c => c.User);
        
        builder.Entity<ChatMessage>()
            .Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(450); // Match AspNetUsers.Id length but no FK constraint
        
        builder.Entity<Appointment>()
            .Ignore(a => a.User);
        
        builder.Entity<Appointment>()
            .Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450); // Match AspNetUsers.Id length but no FK constraint
        
        // Configure Appointment -> Doctor relationship
        builder.Entity<Appointment>()
            .HasOne(a => a.Doctor)
            .WithMany(d => d.Appointments)
            .HasForeignKey(a => a.DoctorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}

