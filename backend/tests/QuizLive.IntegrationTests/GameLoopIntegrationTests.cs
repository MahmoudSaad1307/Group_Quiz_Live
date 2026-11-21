using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using QuizLive.Application.Auth;
using QuizLive.Application.Game;
using QuizLive.Application.Quizzes;
using QuizLive.Application.Sessions;
using QuizLive.Domain.Enums;

namespace QuizLive.IntegrationTests;

[Collection("Postgres")]
public sealed class GameLoopIntegrationTests(QuizLiveWebFactory factory)
{
    private static readonly JsonSerializerOptions TestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    [Fact]
    public async Task FullGameLoop_WithThreePlayers_ScoresAndCompletes()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@quizlive.local", "ChangeMe123!"));
        var csrf = (await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf"))!.Token;

        using var qReq = new HttpRequestMessage(HttpMethod.Post, "/api/quizzes")
        {
            Content = JsonContent.Create(new CreateQuizRequest("5Q Live", null, QuizLanguage.En))
        };
        qReq.Headers.Add("X-XSRF-TOKEN", csrf);
        var quiz = await (await client.SendAsync(qReq)).Content.ReadFromJsonAsync<QuizDetailDto>(TestJsonOptions);

        var qIds = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            using var addQ = new HttpRequestMessage(HttpMethod.Post, $"/api/quizzes/{quiz!.Id}/questions")
            {
                Content = JsonContent.Create(new UpsertQuestionRequest(
                    QuestionType.SingleChoice, $"Q{i + 1}", null, "Because", 20, 1000,
                    [new("Right", true), new("Wrong", false)]))
            };
            addQ.Headers.Add("X-XSRF-TOKEN", csrf);
            var qDto = await (await client.SendAsync(addQ)).Content.ReadFromJsonAsync<QuestionDto>(TestJsonOptions);
            qIds.Add(qDto!.Id);
        }

        using var sReq = new HttpRequestMessage(HttpMethod.Post, "/api/sessions")
        {
            Content = JsonContent.Create(new CreateSessionRequest(quiz!.Id, null))
        };
        sReq.Headers.Add("X-XSRF-TOKEN", csrf);
        var session = await (await client.SendAsync(sReq)).Content.ReadFromJsonAsync<CreateSessionResponse>();

        var handler = factory.Server.CreateHandler();
        var authCookie = client.DefaultRequestHeaders.TryGetValues("Cookie", out var c) ? string.Join("; ", c) : "";

        await using var hostConn = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/host"), o => o.HttpMessageHandlerFactory = _ => handler)
            .Build();
        // Note: TestServer handler shares cookies when configured via CookieContainer; for HostHub we use HttpClient cookies
        // Connect 3 bot players
        var players = new List<HubConnection>();
        for (var i = 1; i <= 3; i++)
        {
            var conn = new HubConnectionBuilder()
                .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/play"), o => o.HttpMessageHandlerFactory = _ => handler)
                .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
                .Build();
            await conn.StartAsync();
            var jr = await conn.InvokeAsync<JoinSessionResult>("JoinSession", session!.Pin, $"Bot{i}", null);
            jr.Succeeded.Should().BeTrue();
            players.Add(conn);
        }

        var qOpenedTcs = new TaskCompletionSource<QuestionOpenedPayload>();
        var qClosedTcs = new TaskCompletionSource<QuestionClosedPayload>();
        var yourResultTcs = new TaskCompletionSource<YourResultPayload>();
        players[0].On<QuestionOpenedPayload>("QuestionOpened", p => qOpenedTcs.TrySetResult(p));
        players[0].On<QuestionClosedPayload>("QuestionClosed", p => qClosedTcs.TrySetResult(p));
        players[0].On<YourResultPayload>("YourResult", p => yourResultTcs.TrySetResult(p));

        foreach (var p in players) await p.DisposeAsync();
    }

    private sealed record CsrfDto(string Token);
}
