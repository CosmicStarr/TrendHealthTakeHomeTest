using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Models;

namespace Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Reminder> Reminders { get; set; }
    public DbSet<ReminderEvent> ReminderEvents { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no timezone-aware date type, so values come back with DateTimeKind.Unspecified.
        // Everything is stored as UTC, so mark it as UTC on the way out. That makes the JSON carry a 'Z'.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reminder>(e =>
        {
            e.Property(r => r.PatientName).HasMaxLength(200);
            e.Property(r => r.Title).HasMaxLength(200);
            e.Property(r => r.Notes).HasMaxLength(2000);
            // Concurrency token: the UPDATE that completes a reminder includes "WHERE Status = <the value
            // that was read>", so when two complete requests race, only the first one succeeds.
            e.Property(r => r.Status).IsConcurrencyToken();
            e.HasIndex(r => r.DueDate);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.SourceReminderId).IsUnique();
            e.HasOne<Reminder>()
                .WithMany()
                .HasForeignKey(r => r.SourceReminderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReminderEvent>(e =>
        {
            e.HasOne(ev => ev.Reminder)
                .WithMany(r => r.Events)
                .HasForeignKey(ev => ev.ReminderId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ev => ev.RelatedReminder)
                .WithMany()
                .HasForeignKey(ev => ev.RelatedReminderId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(ev => new { ev.ReminderId, ev.OccurredAt });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureEventsAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureEventsAreAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>The audit trail is append-only: inserting an event is allowed, changing or deleting one is not.</summary>
    private void EnsureEventsAreAppendOnly()
    {
        var tampered = ChangeTracker.Entries<ReminderEvent>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (tampered)
        {
            throw new InvalidOperationException("Reminder events are append-only and cannot be modified or deleted.");
        }
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
