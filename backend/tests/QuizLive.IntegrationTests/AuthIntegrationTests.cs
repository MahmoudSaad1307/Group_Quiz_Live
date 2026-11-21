using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using QuizLive.Application.Auth;
using Testcontainers.PostgreSql;

namespace QuizLive.IntegrationTests;

[CollectionDefinition("Postgres")]
public sealed class PostgresFixtureGroup : ICollectionFixture<QuizLiveWebFactory>;

public sealed class QuizLiveWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("quizlive_test")
        .WithUsername("quizlive")
        .WithPassword("quizlive_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Issuer", "QuizLive");
        builder.UseSetting("Jwt:Audience", "QuizLive");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-at-least-32-characters-long!");
        builder.UseSetting("SeedAdmin:Email", "admin@quizlive.local");
        builder.UseSetting("SeedAdmin:Password", "ChangeMe123!");
        builder.UseSetting("SeedAdmin:DisplayName", "Test Admin");
        builder.UseSetting("Db:AutoMigrate", "true");
    }
}

[Collection("Postgres")]
public sealed class AuthIntegrationTests(QuizLiveWebFactory factory)
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var client = factory.CreateClient();
        var res = await client.GetAsync("/health/ready");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_WithoutCookie_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        var res = await client.GetAsync("/api/auth/me");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Me_And_Logout_WorkEndToEnd()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@quizlive.local", "ChangeMe123!"));
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await loginRes.Content.ReadFromJsonAsync<AdminProfileDto>();
        profile.Should().NotBeNull();
        profile!.Email.Should().Be("admin@quizlive.local");

        var meRes = await client.GetAsync("/api/auth/me");
        meRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Fetch CSRF token before mutating logout
        var csrfRes = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        csrfRes.Should().NotBeNull();

        using var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutReq.Headers.Add("X-XSRF-TOKEN", csrfRes!.Token);
        var logoutRes = await client.SendAsync(logoutReq);
        logoutRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfter = await client.GetAsync("/api/auth/me");
        meAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record CsrfResponse(string Token);
}
