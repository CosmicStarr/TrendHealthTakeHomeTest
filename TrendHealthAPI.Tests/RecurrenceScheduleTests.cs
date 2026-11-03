using Models;

namespace TrendHealthAPI.Tests;

public class RecurrenceScheduleTests
{
    private static readonly DateTime Due = new(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void OneOff_HasNoNextOccurrence()
    {
        Assert.Null(RecurrenceSchedule.NextDueDate(Due, Recurrence.None, Due));
    }

    [Theory]
    [InlineData(Recurrence.Daily, 1)]
    [InlineData(Recurrence.Weekly, 7)]
    public void CompletedOnTime_AdvancesByOneInterval(Recurrence recurrence, int days)
    {
        var next = RecurrenceSchedule.NextDueDate(Due, recurrence, Due.AddMinutes(5));
        Assert.Equal(Due.AddDays(days), next);
    }

    [Theory]
    [InlineData(Recurrence.Daily, 1)]
    [InlineData(Recurrence.Weekly, 7)]
    public void CompletedEarly_StillAnchorsToDueDate(Recurrence recurrence, int days)
    {
        var next = RecurrenceSchedule.NextDueDate(Due, recurrence, Due.AddHours(-20));
        Assert.Equal(Due.AddDays(days), next);
    }

    [Fact]
    public void Daily_CompletedLate_SkipsMissedOccurrencesAndKeepsTimeOfDay()
    {
        // Due Mon 09:30, completed Thu 21:00. Tue, Wed and Thu 09:30 have all passed, so the next is Fri 09:30.
        var completedAt = Due.AddDays(3).AddHours(11.5);
        var next = RecurrenceSchedule.NextDueDate(Due, Recurrence.Daily, completedAt);
        Assert.Equal(Due.AddDays(4), next);
    }

    [Fact]
    public void Weekly_CompletedLate_KeepsWeekday()
    {
        var completedAt = Due.AddDays(10);
        var next = RecurrenceSchedule.NextDueDate(Due, Recurrence.Weekly, completedAt);
        Assert.Equal(Due.AddDays(14), next);
        Assert.Equal(Due.DayOfWeek, next!.Value.DayOfWeek);
    }

    [Fact]
    public void CompletedExactlyAtNextSlot_MovesPastIt()
    {
        // The new occurrence must be due strictly after completion, so it's never overdue the moment it's created.
        var next = RecurrenceSchedule.NextDueDate(Due, Recurrence.Daily, Due.AddDays(1));
        Assert.Equal(Due.AddDays(2), next);
    }
}
