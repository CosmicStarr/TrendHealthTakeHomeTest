namespace Models
{
    /// <summary>
    /// An immutable entry in a reminder's audit trail. Events are only ever inserted:
    /// <c>ApplicationDbContext</c> refuses to save an update or delete of one.
    /// </summary>
    public class ReminderEvent
    {
        public int Id { get; init; }
        public int ReminderId { get; init; }
        public ReminderEventType Type { get; init; }

        /// <summary>When the event happened, stored as UTC.</summary>
        public DateTime OccurredAt { get; init; }

        /// <summary>
        /// The other reminder involved, if any. For <see cref="ReminderEventType.NextOccurrenceGenerated"/>
        /// it is the new occurrence; for a generated occurrence's <see cref="ReminderEventType.Created"/>
        /// event it is the reminder it was generated from.
        /// </summary>
        public int? RelatedReminderId { get; init; }

        public Reminder? Reminder { get; init; }
        public Reminder? RelatedReminder { get; init; }
    }
}
