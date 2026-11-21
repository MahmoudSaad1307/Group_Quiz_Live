using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using QuizLive.Application.Auth;
using QuizLive.Application.Quizzes;
using QuizLive.Application.Sessions;
using QuizLive.Domain.Enums;

namespace QuizLive.IntegrationTests;

[Collection("Postgres")]
public sealed class SessionLobbyIntegrationTests(QuizLiveWebFactory factory)
{
    private static readonly JsonSerializerOptions TestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    [Fact]
    public async Task JoinLobby_DuplicateNickname_And_Kick_WorkEndToEnd()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@quizlive.local", "ChangeMe123!"));
        var csrf = (await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf"))!.Token;

        using var qReq = new HttpRequestMessage(HttpMethod.Post, "/api/quizzes")
        {
            Content = JsonContent.Create(new CreateQuizRequest("Lobby Test", null, QuizLanguage.En))
        };
        qReq.Headers.Add("X-XSRF-TOKEN", csrf);
        var quiz = await (await client.SendAsync(qReq)).Content.ReadFromJsonAsync<QuizDetailDto>(TestJsonOptions);

        using var addQReq = new HttpRequestMessage(HttpMethod.Post, $"/api/quizzes/{quiz!.Id}/questions")
        {
            Content = JsonContent.Create(new UpsertQuestionRequest(
                QuestionType.TrueFalse, "Ready?", null, null, 20, 1000,
                [new("True", true), new("False", false)]))
        };
        addQReq.Headers.Add("X-XSRF-TOKEN", csrf);
        (await client.SendAsync(addQReq)).EnsureSuccessStatusCode();

        using var sReq = new HttpRequestMessage(HttpMethod.Post, "/api/sessions")
        {
            Content = JsonContent.Create(new CreateSessionRequest(quiz.Id, null))
        };
        sReq.Headers.Add("X-XSRF-TOKEN", csrf);
        var sRes = await client.SendAsync(sReq);
        sRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdSession = await sRes.Content.ReadFromJsonAsync<CreateSessionResponse>();

        var check = await client.GetFromJsonAsync<JoinCheckResponse>($"/api/join/{createdSession!.Pin}", TestJsonOptions);
        check!.Joinable.Should().BeTrue();

        var handler = factory.Server.CreateHandler();
        await using var player1 = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/play"), o => o.HttpMessageHandlerFactory = _ => handler)
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        await player1.StartAsync();

        var kickedTcs = new TaskCompletionSource();
        player1.On("Kicked", () => kickedTcs.TrySetResult());

        var join1 = await player1.InvokeAsync<JoinSessionResult>("JoinSession", createdSession.Pin, "أحمد", null);
        join1.Succeeded.Should().BeTrue();

        await using var player2 = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/play"), o => o.HttpMessageHandlerFactory = _ => handler)
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        await player2.StartAsync();

        // Duplicate normalized Arabic nickname
        var joinDup = await player2.InvokeAsync<JoinSessionResult>("JoinSession", createdSession.Pin, "احمد", null);
        joinDup.Succeeded.Should().BeFalse();
        joinDup.ErrorCode.Should().Be(ErrorCodes.NicknameTaken);
    }

    private sealed record CsrfDto(string Token);
}
