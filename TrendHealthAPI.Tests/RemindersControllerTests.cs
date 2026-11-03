using Microsoft.EntityFrameworkCore;
using Models;
using Models.Dtos;

namespace TrendHealthAPI.Tests;

/// <summary>
/// Controller logic (recurrence roll-over, audit trail, races) against a real SQLite database.
/// Each <c>NewController()</c> is one "request" with its own unit of work.
/// </summary>
public class RemindersControllerTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private async Task<ReminderResponse> Create(Recurrence recurrence = Recurrence.None, DateTimeOffset? due = null, string title = "Check BP") =>
        (await _db.NewController().Create(new CreateReminderRequest
        {
            PatientName = "  Jane Doe ",
            Title = title,
            Notes = "private note",
            DueDate = due ?? _db.Clock.Now.AddHours(1),
            Recurrence = recurrence,
        })).Body();

    private async Task<IReadOnlyList<ReminderEventResponse>> History(int id) =>
        (await _db.NewController().History(id)).Body();

    private async Task<CompleteReminderResponse> Complete(int id) =>
        (await _db.NewController().Complete(id)).Body();

    [Fact]
    public async Task Create_StoresOpenReminderAndAppendsCreatedEvent()
    {
        var reminder = await Create();

        Assert.Equal(Status.Open, reminder.Status);
        Assert.Equal("Jane Doe", reminder.PatientName);
        Assert.Equal(_db.Clock.Now.UtcDateTime, reminder.CreatedAt);

        var created = Assert.Single(await History(reminder.Id));
        Assert.Equal(ReminderEventType.Created, created.Type);
        Assert.Null(created.RelatedReminderId);
    }

    [Fact]
    public async Task CompleteOneOff_MarksDoneWithoutNextOccurrence()
    {
        var reminder = await Create();
        _db.Clock.Advance(TimeSpan.FromMinutes(30));

        var result = await Complete(reminder.Id);

        Assert.Null(result.NextOccurrence);
        Assert.Equal(Status.Done, result.Completed.Status);
        Assert.Equal(_db.Clock.Now.UtcDateTime, result.Completed.CompletedAt);
        Assert.Equal([ReminderEventType.Created, ReminderEventType.Completed], (await History(reminder.Id)).Select(e => e.Type));

        using var ctx = _db.NewContext();
        Assert.Equal(1, await ctx.Reminders.CountAsync());
    }

    [Theory]
    [InlineData(Recurrence.Daily, 1)]
    [InlineData(Recurrence.Weekly, 7)]
    public async Task CompleteRecurring_CreatesNextOpenOccurrenceCarryingFieldsOver(Recurrence recurrence, int days)
    {
        var due = _db.Clock.Now.AddHours(2);
        var original = await Create(recurrence, due);
        _db.Clock.Advance(TimeSpan.FromHours(1));

        var next = (await Complete(original.Id)).NextOccurrence!;

        Assert.NotEqual(original.Id, next.Id);
        Assert.Equal(Status.Open, next.Status);
        Assert.Equal(due.UtcDateTime.AddDays(days), next.DueDate);
        Assert.Equal(original.PatientName, next.PatientName);
        Assert.Equal(original.Title, next.Title);
        Assert.Equal(original.Notes, next.Notes);
        Assert.Equal(recurrence, next.Recurrence);
        Assert.Equal(original.Id, next.SourceReminderId);
        Assert.Equal(_db.Clock.Now.UtcDateTime, next.CreatedAt);
        Assert.Null(next.CompletedAt);

        // The saved row matches what was returned.
        var reloaded = (await _db.NewController().Get(next.Id)).Body();
        Assert.Equal(next.DueDate, reloaded.DueDate);
        Assert.Equal(DateTimeKind.Utc, reloaded.DueDate.Kind);
    }

    [Fact]
    public async Task CompleteRecurring_RecordsAuditTrailOnBothReminders()
    {
        var original = await Create(Recurrence.Daily);
        _db.Clock.Advance(TimeSpan.FromMinutes(10));
        var nextId = (await Complete(original.Id)).NextOccurrence!.Id;

        var history = await History(original.Id);
        Assert.Equal(
            [ReminderEventType.Created, ReminderEventType.Completed, ReminderEventType.NextOccurrenceGenerated],
            history.Select(e => e.Type));
        Assert.Equal(nextId, history[2].RelatedReminderId);
        Assert.True(history[0].OccurredAt < history[1].OccurredAt);

        var created = Assert.Single(await History(nextId));
        Assert.Equal(ReminderEventType.Created, created.Type);
        Assert.Equal(original.Id, created.RelatedReminderId);
    }

    [Fact]
    public async Task CompletingTheNextOccurrence_ContinuesTheChain()
    {
        var first = await Create(Recurrence.Weekly);
        var second = (await Complete(first.Id)).NextOccurrence!;
        var third = (await Complete(second.Id)).NextOccurrence!;

        Assert.Equal(second.Id, third.SourceReminderId);
        Assert.Equal(first.DueDate.AddDays(14), third.DueDate);
    }

    [Fact]
    public async Task CompleteTwice_Returns409AndDoesNotDuplicateOccurrencesOrEvents()
    {
        var reminder = await Create(Recurrence.Daily);
        await Complete(reminder.Id);

        var second = await _db.NewController().Complete(reminder.Id);

        Assert.Equal(409, second.StatusCode());
        using var ctx = _db.NewContext();
        Assert.Equal(2, await ctx.Reminders.CountAsync());
        Assert.Equal(3, await ctx.ReminderEvents.CountAsync(e => e.ReminderId == reminder.Id));
    }

    [Fact]
    public async Task ConcurrentCompletes_OnlyOneWins()
    {
        var reminder = await Create(Recurrence.Daily);

        // Two "requests" both load (and track) the reminder while it's still Open.
        var a = _db.NewController();
        var b = _db.NewController();
        Assert.Equal(Status.Open, (await a.Get(reminder.Id)).Body().Status);
        Assert.Equal(Status.Open, (await b.Get(reminder.Id)).Body().Status);

        var resultA = await a.Complete(reminder.Id);
        var resultB = await b.Complete(reminder.Id);

        Assert.Equal(200, resultA.StatusCode());
        Assert.Equal(409, resultB.StatusCode());
        using var ctx = _db.NewContext();
        Assert.Equal(1, await ctx.Reminders.CountAsync(r => r.SourceReminderId == reminder.Id));
        Assert.Equal(1, await ctx.ReminderEvents.CountAsync(e => e.ReminderId == reminder.Id && e.Type == ReminderEventType.Completed));
    }

    [Fact]
    public async Task MissingReminder_Returns404()
    {
        var controller = _db.NewController();
        Assert.Equal(404, (await controller.Complete(404)).StatusCode());
        Assert.Equal(404, (await controller.History(404)).StatusCode());
        Assert.Equal(404, (await controller.Get(404)).StatusCode());
    }

    [Fact]
    public async Task Events_CannotBeModifiedOrDeleted()
    {
        var reminder = await Create();

        using (var ctx = _db.NewContext())
        {
            var ev = await ctx.ReminderEvents.SingleAsync(e => e.ReminderId == reminder.Id);
            ctx.Entry(ev).Property(e => e.OccurredAt).CurrentValue = DateTime.UtcNow;
            await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
        }

        using (var ctx = _db.NewContext())
        {
            var ev = await ctx.ReminderEvents.SingleAsync(e => e.ReminderId == reminder.Id);
            ctx.ReminderEvents.Remove(ev);
            Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
        }
    }

    [Fact]
    public async Task List_FiltersSortsAndPages()
    {
        var start = _db.Clock.Now;
        for (var i = 1; i <= 5; i++)
        {
            await Create(due: start.AddDays(i), title: $"R{i}");
            _db.Clock.Advance(TimeSpan.FromMinutes(1));
        }
        var done = await Create(due: start.AddDays(3), title: "Done one");
        await Complete(done.Id);

        async Task<PagedResponse<ReminderResponse>> List(ReminderListQuery q) => (await _db.NewController().List(q)).Body();

        var page1 = await List(new ReminderListQuery
        {
            Status = Status.Open,
            DueFrom = start.AddDays(2),
            DueTo = start.AddDays(5),
            SortBy = ReminderSortField.DueDate,
            SortDir = SortDirection.Desc,
            PageSize = 3,
        });

        Assert.Equal(["R5", "R4", "R3"], page1.Items.Select(r => r.Title));
        Assert.Equal(4, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.True(page1.HasNextPage);

        var page2 = await List(new ReminderListQuery
        {
            Status = Status.Open, DueFrom = start.AddDays(2), DueTo = start.AddDays(5),
            SortDir = SortDirection.Desc, PageSize = 3, Page = 2,
        });
        Assert.Equal(["R2"], page2.Items.Select(r => r.Title));
        Assert.False(page2.HasNextPage);

        var newest = await List(new ReminderListQuery
        {
            SortBy = ReminderSortField.CreatedAt, SortDir = SortDirection.Desc, PageSize = 1,
        });
        Assert.Equal("Done one", Assert.Single(newest.Items).Title);
    }
}
