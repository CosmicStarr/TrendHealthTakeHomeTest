namespace Models
{
    public class Reminder
    {
        public int Id { get; set; }
        public required string PatientName { get; set; }
        public required string Title { get; set; }
        public string Notes { get; set; } = string.Empty;

        /// <summary>When the reminder is due, stored as UTC.</summary>
        public DateTime DueDate { get; set; }
        public Status Status { get; set; }
        public Recurrence Recurrence { get; set; }

        /// <summary>When the reminder was created, stored as UTC.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When the reminder was marked Done, stored as UTC. Null while it is Open.</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// For a generated occurrence, the reminder whose completion created it. A unique index on this
        /// column means each reminder can have at most one next occurrence, even when two complete
        /// requests race each other.
        /// </summary>
        public int? SourceReminderId { get; set; }

        public List<ReminderEvent> Events { get; set; } = [];
    }
}
