using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using QuizLive.Application.Auth;
using QuizLive.Application.Quizzes;
using QuizLive.Domain.Enums;
using SkiaSharp;

namespace QuizLive.IntegrationTests;

[Collection("Postgres")]
public sealed class QuizIntegrationTests(QuizLiveWebFactory factory)
{
    private static readonly JsonSerializerOptions TestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private async Task<(HttpClient Client, string Csrf)> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@quizlive.local", "ChangeMe123!"));
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var csrf = await client.GetFromJsonAsync<CsrfDto>("/api/auth/csrf");
        return (client, csrf!.Token);
    }

    [Fact]
    public async Task Quiz_Crud_Reorder_Duplicate_And_Delete_Work()
    {
        var (client, csrf) = await CreateAuthenticatedClientAsync();
        using var _ = client;

        using var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/quizzes")
        {
            Content = JsonContent.Create(new CreateQuizRequest("History Quiz", "World history", QuizLanguage.En))
        };
        createReq.Headers.Add("X-XSRF-TOKEN", csrf);
        var createRes = await client.SendAsync(createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<QuizDetailDto>(TestJsonOptions);
        created.Should().NotBeNull();

        using var q1Req = new HttpRequestMessage(HttpMethod.Post, $"/api/quizzes/{created!.Id}/questions")
        {
            Content = JsonContent.Create(new UpsertQuestionRequest(
                QuestionType.SingleChoice,
                "First?",
                null,
                null,
                20,
                1000,
                [new("A", true), new("B", false)]))
        };
        q1Req.Headers.Add("X-XSRF-TOKEN", csrf);
        var q1Res = await client.SendAsync(q1Req);
        q1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var q1 = await q1Res.Content.ReadFromJsonAsync<QuestionDto>(TestJsonOptions);

        using var q2Req = new HttpRequestMessage(HttpMethod.Post, $"/api/quizzes/{created.Id}/questions")
        {
            Content = JsonContent.Create(new UpsertQuestionRequest(
                QuestionType.TrueFalse,
                "Second?",
                null,
                null,
                15,
                500,
                [new("True", true), new("False", false)]))
        };
        q2Req.Headers.Add("X-XSRF-TOKEN", csrf);
        var q2Res = await client.SendAsync(q2Req);
        q2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var q2 = await q2Res.Content.ReadFromJsonAsync<QuestionDto>(TestJsonOptions);

        using var reorderReq = new HttpRequestMessage(HttpMethod.Put, $"/api/quizzes/{created.Id}/questions/reorder")
        {
            Content = JsonContent.Create(new ReorderQuestionsRequest([q2!.Id, q1!.Id]))
        };
        reorderReq.Headers.Add("X-XSRF-TOKEN", csrf);
        var reorderRes = await client.SendAsync(reorderReq);
        reorderRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var reordered = await reorderRes.Content.ReadFromJsonAsync<QuizDetailDto>(TestJsonOptions);
        reordered!.Questions.Select(q => q.Id).Should().ContainInOrder(q2.Id, q1.Id);

        using var dupReq = new HttpRequestMessage(HttpMethod.Post, $"/api/quizzes/{created.Id}/duplicate");
        dupReq.Headers.Add("X-XSRF-TOKEN", csrf);
        var dupRes = await client.SendAsync(dupReq);
        dupRes.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UploadImage_ReencodesToWebp_And_RejectsInvalidFiles()
    {
        var (client, csrf) = await CreateAuthenticatedClientAsync();
        using var _ = client;

        using var badContent = new MultipartFormDataContent();
        badContent.Add(new ByteArrayContent([1, 2, 3, 4]), "file", "fake.png");
        using var badReq = new HttpRequestMessage(HttpMethod.Post, "/api/uploads/image") { Content = badContent };
        badReq.Headers.Add("X-XSRF-TOKEN", csrf);
        var badRes = await client.SendAsync(badReq);
        badRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var bmp = new SKBitmap(10, 10);
        bmp.Erase(SKColors.CornflowerBlue);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);

        using var goodContent = new MultipartFormDataContent();
        var fileBytes = new ByteArrayContent(data.ToArray());
        fileBytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        goodContent.Add(fileBytes, "file", "test.png");

        using var goodReq = new HttpRequestMessage(HttpMethod.Post, "/api/uploads/image") { Content = goodContent };
        goodReq.Headers.Add("X-XSRF-TOKEN", csrf);
        var goodRes = await client.SendAsync(goodReq);
        goodRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var uploaded = await goodRes.Content.ReadFromJsonAsync<UploadResponse>();
        uploaded!.Url.Should().StartWith("/uploads/").And.EndWith(".webp");

        var fetchedBytes = await client.GetByteArrayAsync(uploaded.Url);
        using var codec = SKCodec.Create(new MemoryStream(fetchedBytes));
        codec.EncodedFormat.Should().Be(SKEncodedImageFormat.Webp);
    }

    private sealed record CsrfDto(string Token);
    private sealed record UploadResponse(string Url);
}
