using System.ComponentModel.DataAnnotations;

namespace Models.Dtos;

public class CreateReminderRequest
{
    [Required, StringLength(200)]
    public string? PatientName { get; set; }

    [Required, StringLength(200)]
    public string? Title { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>An ISO 8601 date-time with an offset, e.g. <c>2026-11-05T09:00:00Z</c>. Stored as UTC.</summary>
    [Required]
    public DateTimeOffset? DueDate { get; set; }

    [EnumDataType(typeof(Recurrence))]
    public Recurrence Recurrence { get; set; } = Recurrence.None;
}

public enum ReminderSortField
{
    DueDate,
    CreatedAt,
}

public enum SortDirection
{
    Asc,
    Desc,
}

/// <summary>Query string for <c>GET /reminders</c>.</summary>
public class ReminderListQuery : IValidatableObject
{
    public const int MaxPageSize = 50;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 10;

    [EnumDataType(typeof(Status))]
    public Status? Status { get; set; }

    /// <summary>Inclusive lower bound on dueDate.</summary>
    public DateTimeOffset? DueFrom { get; set; }

    /// <summary>Inclusive upper bound on dueDate.</summary>
    public DateTimeOffset? DueTo { get; set; }

    [EnumDataType(typeof(ReminderSortField))]
    public ReminderSortField SortBy { get; set; } = ReminderSortField.DueDate;

    [EnumDataType(typeof(SortDirection))]
    public SortDirection SortDir { get; set; } = SortDirection.Asc;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DueFrom is { } from && DueTo is { } to && from > to)
        {
            yield return new ValidationResult("dueFrom must be on or before dueTo.", [nameof(DueFrom)]);
        }
    }
}

public record ReminderResponse(
    int Id,
    string PatientName,
    string Title,
    string Notes,
    DateTime DueDate,
    Status Status,
    Recurrence Recurrence,
    bool IsRecurring,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int? SourceReminderId)
{
    public static ReminderResponse From(Reminder r) => new(
        r.Id, r.PatientName, r.Title, r.Notes, r.DueDate, r.Status, r.Recurrence,
        r.Recurrence != Recurrence.None, r.CreatedAt, r.CompletedAt, r.SourceReminderId);
}

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public record CompleteReminderResponse(ReminderResponse Completed, ReminderResponse? NextOccurrence);

public record ReminderEventResponse(int Id, ReminderEventType Type, DateTime OccurredAt, int? RelatedReminderId)
{
    public static ReminderEventResponse From(ReminderEvent e) => new(e.Id, e.Type, e.OccurredAt, e.RelatedReminderId);
}
