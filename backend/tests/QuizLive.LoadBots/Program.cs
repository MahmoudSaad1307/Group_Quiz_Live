using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using QuizLive.Application.Game;
using QuizLive.Application.Sessions;

var url = GetArg(args, "--url") ?? "http://localhost:5000";
var pin = GetArg(args, "--pin") ?? throw new ArgumentException("--pin <6-digit PIN> is required.");
var botCount = int.Parse(GetArg(args, "--bots") ?? "300", CultureInfo.InvariantCulture);
var thinkMin = int.Parse(GetArg(args, "--think-min") ?? "500", CultureInfo.InvariantCulture);
var thinkMax = int.Parse(GetArg(args, "--think-max") ?? "8000", CultureInfo.InvariantCulture);
var expectedQuestions = int.Parse(GetArg(args, "--questions") ?? "5", CultureInfo.InvariantCulture);

var ackLatenciesMs = new ConcurrentBag<double>();
var fanoutLatenciesMs = new ConcurrentBag<double>();
var questionsReceived = new ConcurrentDictionary<int, int>();
var finishedTcs = new TaskCompletionSource();
var finishedCount = 0;

Console.WriteLine($"Connecting {botCount} bots to {url}/hubs/play (PIN {pin})...");

var bots = new List<HubConnection>(botCount);
for (var i = 1; i <= botCount; i++)
{
    var botIndex = i;
    var conn = new HubConnectionBuilder()
        .WithUrl($"{url.TrimEnd('/')}/hubs/play")
        .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
        .WithAutomaticReconnect()
        .Build();

    conn.On<QuestionOpenedPayload>("QuestionOpened", async q =>
    {
        var receiveTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fanout = Math.Max(0, receiveTimeMs - q.ServerNowMs);
        fanoutLatenciesMs.Add(fanout);
        questionsReceived.AddOrUpdate(botIndex, 1, (_, c) => c + 1);

        var maxWait = Math.Max(thinkMin, Math.Min(thinkMax, q.DurationMs - 400));
        var delay = Random.Shared.Next(thinkMin, Math.Max(thinkMin + 1, maxWait));
        await Task.Delay(delay);

        var opt = q.Options[Random.Shared.Next(q.Options.Count)];
        var sw = Stopwatch.StartNew();
        await conn.InvokeAsync<SubmitAnswerResult>("SubmitAnswer", q.QuestionId, opt.Id);
        sw.Stop();
        ackLatenciesMs.Add(sw.Elapsed.TotalMilliseconds);
    });

    conn.On<FinalResultsPayload>("FinalResults", _ =>
    {
        if (Interlocked.Increment(ref finishedCount) >= botCount)
        {
            finishedTcs.TrySetResult();
        }
    });

    await conn.StartAsync();
    var join = await conn.InvokeAsync<JoinSessionResult>("JoinSession", pin, $"Bot_{i:D4}", null);
    if (!join.Succeeded)
    {
        Console.Error.WriteLine($"Bot_{i:D4} failed to join: {join.ErrorCode} - {join.ErrorMessage}");
    }

    bots.Add(conn);
    if (i % 25 == 0)
    {
        Console.WriteLine($"Joined {i}/{botCount}");
        await Task.Delay(50);
    }
}

Console.WriteLine("All bots joined. Waiting for game completion...");
await finishedTcs.Task;

PrintPercentiles("Answer Ack Latency (ms)", [.. ackLatenciesMs]);
PrintPercentiles("QuestionOpened Fan-out (ms)", [.. fanoutLatenciesMs]);

var dropped = Enumerable.Range(1, botCount).Count(i => questionsReceived.GetValueOrDefault(i, 0) < expectedQuestions);
Console.WriteLine($"Bots with dropped questions (<{expectedQuestions}): {dropped}/{botCount}");

foreach (var b in bots)
{
    await b.DisposeAsync();
}

static string? GetArg(string[] args, string name)
{
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

static void PrintPercentiles(string label, double[] values)
{
    if (values.Length == 0)
    {
        Console.WriteLine($"{label}: no samples");
        return;
    }

    Array.Sort(values);
    var p50 = values[(int)(values.Length * 0.50)];
    var p95 = values[(int)(values.Length * 0.95)];
    var p99 = values[Math.Min(values.Length - 1, (int)(values.Length * 0.99))];
    Console.WriteLine($"{label} (n={values.Length}): p50={p50:F1}ms  p95={p95:F1}ms  p99={p99:F1}ms");
}
