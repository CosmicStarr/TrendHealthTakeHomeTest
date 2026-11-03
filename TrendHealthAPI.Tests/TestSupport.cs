using Data;
using Data.Classes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using TrendHealthAPI.Controllers;

namespace TrendHealthAPI.Tests;

/// <summary>Helpers for unwrapping controller action results in tests.</summary>
public static class ActionResults
{
    /// <summary>The body of a 2xx object result. The test fails if the result isn't one.</summary>
    public static T Body<T>(this ActionResult<T> result)
    {
        var obj = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.InRange(obj.StatusCode ?? 200, 200, 299);
        return Assert.IsAssignableFrom<T>(obj.Value);
    }

    /// <summary>The HTTP status code the action result would produce.</summary>
    public static int? StatusCode<T>(this ActionResult<T> result) => (result.Result as IStatusCodeActionResult)?.StatusCode;
}

/// <summary>A clock the test controls.</summary>
public sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>
/// An in-memory SQLite database that lives as long as this object, built from the real migrations,
/// so constraints, indexes and concurrency tokens behave as they do in production.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var context = NewContext();
        context.Database.Migrate();
    }

    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));

    public ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);

    /// <summary>A controller with its own DbContext / unit of work, just like one HTTP request.</summary>
    public RemindersController NewController() =>
        new(new UnitOfWork(NewContext()), Clock, NullLogger<RemindersController>.Instance);

    public void Dispose() => _connection.Dispose();
}
