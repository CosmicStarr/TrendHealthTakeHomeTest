using System.Linq.Expressions;
using CosmicStoreAPI.Error;
using Data.Interfaces;
using Data.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models;
using Models.Dtos;

namespace TrendHealthAPI.Controllers;

/// <summary>
/// Reminder endpoints, implemented directly on <see cref="IUnitOfWork"/> / <see cref="IRepository{T}"/>.
/// Every state change stages its audit events in the same <see cref="IUnitOfWork.Complete"/> call,
/// so a change and its events are saved together or not at all.
/// Logging rule: only identifiers are logged, never notes or whole records.
/// </summary>
// Replaces BaseController's "api/[controller]" route: the spec requires the endpoints at /reminders.
[Route("reminders")]
public class RemindersController(IUnitOfWork unitOfWork, TimeProvider clock, ILogger<RemindersController> logger)
    : BaseController
{
    private readonly IUnitOfWork _uow = unitOfWork;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<RemindersController> _logger = logger;

    private IRepository<Reminder> Reminders => _uow.Repository<Reminder>();
    private IRepository<ReminderEvent> Events => _uow.Repository<ReminderEvent>();

    [HttpGet]
    [ProducesResponseType<PagedResponse<ReminderResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiValidationResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ReminderResponse>>> List([FromQuery] ReminderListQuery query)
    {
        var status = query.Status;
        var from = query.DueFrom?.UtcDateTime;
        var to = query.DueTo?.UtcDateTime;
        Expression<Func<Reminder, bool>> filter = r =>
            (status == null || r.Status == status)
            && (from == null || r.DueDate >= from)
            && (to == null || r.DueDate <= to);

        Func<IQueryable<Reminder>, IOrderedQueryable<Reminder>> orderBy = (query.SortBy, query.SortDir) switch
        {
            // Id breaks ties, so paging stays stable when several rows share a timestamp.
            (ReminderSortField.CreatedAt, SortDirection.Asc) => q => q.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id),
            (ReminderSortField.CreatedAt, SortDirection.Desc) => q => q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id),
            (_, SortDirection.Desc) => q => q.OrderByDescending(r => r.DueDate).ThenByDescending(r => r.Id),
            _ => q => q.OrderBy(r => r.DueDate).ThenBy(r => r.Id),
        };

        var pageParams = new PageParams { PageNumber = query.Page, PageSize = query.PageSize };
        var page = await Reminders.GetAllParams(pageParams, filter, orderBy);

        return Ok(new PagedResponse<ReminderResponse>(
            page.Select(ReminderResponse.From).ToList(),
            page.CurrentPage,
            page.PageSize,
            page.TotalCount,
            page.TotalPages,
            page.HasNextPage,
            page.HasPreviousPage));
    }

    [HttpPost]
    [ProducesResponseType<ReminderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiValidationResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReminderResponse>> Create([FromBody] CreateReminderRequest request)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var reminder = new Reminder
        {
            PatientName = request.PatientName!.Trim(),
            Title = request.Title!.Trim(),
            Notes = request.Notes?.Trim() ?? string.Empty,
            DueDate = request.DueDate!.Value.UtcDateTime,
            Recurrence = request.Recurrence,
            Status = Status.Open,
            CreatedAt = now,
        };
        reminder.Events.Add(new ReminderEvent { Type = ReminderEventType.Created, OccurredAt = now });

        Reminders.Add(reminder);
        await _uow.Complete();

        _logger.LogInformation("Created reminder {ReminderId}", reminder.Id);
        return CreatedAtAction(nameof(Get), new { id = reminder.Id }, ReminderResponse.From(reminder));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ReminderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderResponse>> Get(int id)
    {
        Reminder? reminder = await Reminders.Get(id);
        return reminder is null ? ReminderNotFound(id) : Ok(ReminderResponse.From(reminder));
    }

    /// <summary>
    /// Marks a reminder Done. If it recurs, also creates the next Open occurrence. The steps:
    /// <list type="bullet">
    /// <item>Completing a reminder that is already Done returns 409 and changes nothing, so a repeated
    /// request can't create a second next occurrence.</item>
    /// <item>Two requests racing to complete the same Open reminder: <c>Status</c> is a concurrency token,
    /// and <c>SourceReminderId</c> is unique, so the second save fails and is reported as 409.</item>
    /// <item>The next due date is worked out by <see cref="RecurrenceSchedule.NextDueDate"/>.</item>
    /// </list>
    /// </summary>
    [HttpPost("{id:int}/complete")]
    [ProducesResponseType<CompleteReminderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompleteReminderResponse>> Complete(int id)
    {
        // Get uses FindAsync, so the entity is tracked and its changes are saved by Complete().
        Reminder? reminder = await Reminders.Get(id);
        if (reminder is null)
        {
            return ReminderNotFound(id);
        }
        if (reminder.Status == Status.Done)
        {
            _logger.LogInformation("Reminder {ReminderId} is already completed", id);
            return AlreadyCompleted(id);
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        reminder.Status = Status.Done;
        reminder.CompletedAt = now;
        Events.Add(new ReminderEvent { ReminderId = reminder.Id, Type = ReminderEventType.Completed, OccurredAt = now });

        Reminder? next = null;
        if (RecurrenceSchedule.NextDueDate(reminder.DueDate, reminder.Recurrence, now) is { } nextDue)
        {
            next = new Reminder
            {
                PatientName = reminder.PatientName,
                Title = reminder.Title,
                Notes = reminder.Notes,
                DueDate = nextDue,
                Recurrence = reminder.Recurrence,
                Status = Status.Open,
                CreatedAt = now,
                SourceReminderId = reminder.Id,
            };
            next.Events.Add(new ReminderEvent
            {
                Type = ReminderEventType.Created,
                OccurredAt = now,
                RelatedReminderId = reminder.Id,
            });
            Reminders.Add(next);

            // RelatedReminder is set through the navigation property because the new id doesn't exist
            // until the save; EF fills in the foreign key during that save.
            Events.Add(new ReminderEvent
            {
                ReminderId = reminder.Id,
                Type = ReminderEventType.NextOccurrenceGenerated,
                OccurredAt = now,
                RelatedReminder = next,
            });
        }

        try
        {
            await _uow.Complete();
        }
        catch (DbUpdateException ex)
        {
            // A concurrent request completed this reminder first. Only the exception type is logged.
            // The message is skipped to keep row data out of the logs.
            _logger.LogWarning("{ExceptionType} while completing reminder {ReminderId}; treating it as already completed",
                ex.GetType().Name, id);
            return AlreadyCompleted(id);
        }

        if (next is null)
        {
            _logger.LogInformation("Completed reminder {ReminderId}", reminder.Id);
        }
        else
        {
            _logger.LogInformation("Completed reminder {ReminderId} and generated next occurrence {NextReminderId}", reminder.Id, next.Id);
        }

        return Ok(new CompleteReminderResponse(
            ReminderResponse.From(reminder),
            next is null ? null : ReminderResponse.From(next)));
    }

    [HttpGet("{id:int}/history")]
    [ProducesResponseType<IReadOnlyList<ReminderEventResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReminderEventResponse>>> History(int id)
    {
        Reminder? reminder = await Reminders.GetFirstOrDefault(r => r.Id == id);
        if (reminder is null)
        {
            return ReminderNotFound(id);
        }

        // Events written in one operation share a timestamp. The event type's enum order
        // (Created < Completed < NextOccurrenceGenerated) is also the order they happen in, so it breaks the tie.
        var history = await Events.GetAll(
            filter: e => e.ReminderId == id,
            orderby: q => q.OrderBy(e => e.OccurredAt).ThenBy(e => e.Type).ThenBy(e => e.Id));

        return Ok(history.Select(ReminderEventResponse.From).ToList());
    }

    private NotFoundObjectResult ReminderNotFound(int id) => NotFoundError($"Reminder {id} was not found.");

    private ConflictObjectResult AlreadyCompleted(int id) => ConflictError($"Reminder {id} is already completed.");
}
