namespace Models
{
    public static class RecurrenceSchedule
    {
        /// <summary>Length of one repeat interval, or null for a one-off reminder.</summary>
        public static TimeSpan? Interval(Recurrence recurrence) => recurrence switch
        {
            Recurrence.Daily => TimeSpan.FromDays(1),
            Recurrence.Weekly => TimeSpan.FromDays(7),
            _ => null,
        };

        /// <summary>
        /// Works out the due date of the next occurrence after a reminder is completed at
        /// <paramref name="completedAt"/>. Returns null for a one-off reminder.
        /// <para>
        /// The next date is anchored to the previous <em>due date</em>, not to when the reminder was
        /// completed, so time of day and weekday stay the same. If the reminder was completed late, it
        /// keeps moving forward one interval at a time until the date is after
        /// <paramref name="completedAt"/>. Missed occurrences are skipped rather than generated as
        /// reminders that are already overdue.
        /// </para>
        /// </summary>
        public static DateTime? NextDueDate(DateTime dueDate, Recurrence recurrence, DateTime completedAt)
        {
            if (Interval(recurrence) is not { } interval)
            {
                return null;
            }

            var next = dueDate + interval;
            if (next <= completedAt)
            {
                // Jump straight to the first slot after completedAt instead of looping, so a reminder
                // that is years overdue costs nothing extra.
                var missed = (completedAt - next).Ticks / interval.Ticks + 1;
                next += TimeSpan.FromTicks(interval.Ticks * missed);
            }

            return next;
        }
    }
}
