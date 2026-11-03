using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TrendHealthAPI.Tests;

/// <summary>Runs the real HTTP pipeline against a throwaway SQLite file and records every log line.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"trendhealth-test-{Guid.NewGuid():N}.db");

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
        builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddProvider(Logs);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this);
    public void Dispose() { }

    private sealed class Logger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => owner.Messages.Enqueue(formatter(state, exception) + " " + exception);
    }
}

public class RemindersApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RemindersApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static object ValidBody(string notes = "notes", string recurrence = "Daily") => new
    {
        patientName = "Jane Doe",
        title = "Check BP",
        notes,
        dueDate = DateTimeOffset.UtcNow.AddDays(1),
        recurrence,
    };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Create_InvalidBody_Returns400WithErrorsPerField()
    {
        var response = await _client.PostAsJsonAsync("/reminders", new { title = "", notes = new string('x', 2001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(400, body.GetProperty("statusCode").GetInt32());
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("patientName", out _));
        Assert.True(errors.TryGetProperty("dueDate", out _));
        Assert.True(errors.TryGetProperty("notes", out _));
    }

    [Fact]
    public async Task Create_UnknownRecurrence_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/reminders", ValidBody(recurrence: "Monthly"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Json(response)).GetProperty("errors");
        var recurrence = Assert.Single(errors.EnumerateObject());
        Assert.Equal("recurrence", recurrence.Name);
        Assert.DoesNotContain("Models.", recurrence.Value.ToString());
    }

    [Fact]
    public async Task MissingReminder_Returns404OnEveryEndpoint()
    {
        foreach (var (method, url) in new[]
                 {
                     (HttpMethod.Get, "/reminders/999999"),
                     (HttpMethod.Post, "/reminders/999999/complete"),
                     (HttpMethod.Get, "/reminders/999999/history"),
                 })
        {
            var response = await _client.SendAsync(new HttpRequestMessage(method, url));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(404, (await Json(response)).GetProperty("statusCode").GetInt32());
        }
    }

    [Fact]
    public async Task CreateCompleteHistory_HappyPathAndRepeatCompleteConflict()
    {
        var created = await _client.PostAsJsonAsync("/reminders", ValidBody());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var reminder = await Json(created);
        var id = reminder.GetProperty("id").GetInt32();
        Assert.Equal($"/reminders/{id}", created.Headers.Location!.AbsolutePath);
        Assert.Equal("Open", reminder.GetProperty("status").GetString());
        Assert.True(reminder.GetProperty("isRecurring").GetBoolean());

        var completed = await _client.PostAsync($"/reminders/{id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var body = await Json(completed);
        Assert.Equal("Done", body.GetProperty("completed").GetProperty("status").GetString());
        Assert.Equal("Open", body.GetProperty("nextOccurrence").GetProperty("status").GetString());

        var again = await _client.PostAsync($"/reminders/{id}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var history = await Json(await _client.GetAsync($"/reminders/{id}/history"));
        Assert.Equal(
            ["Created", "Completed", "NextOccurrenceGenerated"],
            history.EnumerateArray().Select(e => e.GetProperty("type").GetString()));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=51")]
    [InlineData("status=Pending")]
    [InlineData("sortBy=title")]
    [InlineData("dueFrom=2026-12-01T00:00:00Z&dueTo=2026-11-01T00:00:00Z")]
    public async Task List_InvalidQuery_Returns400(string query)
    {
        var response = await _client.GetAsync($"/reminders?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEmpty((await Json(response)).GetProperty("errors").EnumerateObject());
    }

    [Fact]
    public async Task List_ReturnsPagingMetadata()
    {
        await _client.PostAsJsonAsync("/reminders", ValidBody());
        await _client.PostAsJsonAsync("/reminders", ValidBody());

        var body = await Json(await _client.GetAsync("/reminders?pageSize=1&status=Open&sortBy=createdAt&sortDir=desc"));

        Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 2);
        Assert.True(body.GetProperty("hasNextPage").GetBoolean());
    }

    [Fact]
    public async Task Logs_NeverContainNotes()
    {
        const string secret = "SECRET-NOTE-7f3a9c";
        var created = await Json(await _client.PostAsJsonAsync("/reminders", ValidBody(notes: secret)));
        var id = created.GetProperty("id").GetInt32();
        await _client.GetAsync($"/reminders/{id}");
        await _client.PostAsync($"/reminders/{id}/complete", null);
        await _client.PostAsync($"/reminders/{id}/complete", null);
        await _client.GetAsync("/reminders");

        Assert.NotEmpty(_factory.Logs.Messages);
        Assert.DoesNotContain(_factory.Logs.Messages, m => m.Contains(secret));
        Assert.Contains(_factory.Logs.Messages, m => m.Contains($"Created reminder {id}"));
    }
}
