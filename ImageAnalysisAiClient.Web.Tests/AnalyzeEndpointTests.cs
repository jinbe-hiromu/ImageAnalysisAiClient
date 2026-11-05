using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OllamaSharp;
using Xunit;

namespace ImageAnalysisAiClient.Web.Tests;

public class AnalyzeEndpointTests
{
    [Fact]
    public async Task Analyze_ReturnsErrorWhenExcelPathIsNotConfigured()
    {
        await using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WasteCatalog:ExcelPath"] = ""
                })));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/analyze", new
        {
            Provider = "local",
            Model = "test",
            Images = new[] { "AA==" }
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("WasteCatalog:ExcelPath", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Analyze_SendsExcelMappingsAndImagesToModel()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("D1", "注意点"),
            ("B2", "紙"), ("C2", "紙類"), ("D2", "送信しない注意点"));
        workbook.Save();
        var handler = new FakeOllamaHandler();
        using var ollamaHttp = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        await using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?> { ["WasteCatalog:ExcelPath"] = workbook.Path }));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<OllamaApiClient>();
                    services.AddSingleton(new OllamaApiClient(ollamaHttp));
                });
            });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/analyze", new
        {
            Provider = "local",
            Model = "test",
            Images = new[] { "AA==" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("判定結果", await response.Content.ReadAsStringAsync());
        using var request = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var messages = request.RootElement.GetProperty("messages");
        var system = messages[0].GetProperty("content").GetString();
        Assert.Contains("紙 → 紙類", system);
        Assert.DoesNotContain("送信しない注意点", system);
        Assert.Single(messages[1].GetProperty("images").EnumerateArray());
    }

    [Theory]
    [InlineData("invalid-path")]
    [InlineData("missing")]
    [InlineData("invalid-format")]
    [InlineData("missing-index")]
    public async Task Analyze_ReturnsErrorBeforeCallingModelWhenExcelCannotBeRead(string failure)
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "見出しなし"));
        workbook.Save();
        if (failure == "invalid-format")
        {
            await File.WriteAllTextAsync(workbook.Path, "not an Excel file");
        }

        var path = failure switch
        {
            "invalid-path" => "\0",
            "missing" => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx"),
            _ => workbook.Path
        };
        var handler = new FakeOllamaHandler();
        using var ollamaHttp = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        await using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?> { ["WasteCatalog:ExcelPath"] = path }));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<OllamaApiClient>();
                    services.AddSingleton(new OllamaApiClient(ollamaHttp));
                });
            });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/analyze", new
        {
            Provider = "local",
            Model = "test",
            Images = new[] { "AA==" }
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("Excel", problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Analyze_RereadsExcelForFollowUpAndRejectsBrokenReplacement()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.Save();
        using var replacement = new ExcelTestWorkbook();
        replacement.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "缶"), ("C2", "金属"));
        replacement.Save();
        var handler = new FakeOllamaHandler();
        using var ollamaHttp = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        await using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?> { ["WasteCatalog:ExcelPath"] = workbook.Path }));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<OllamaApiClient>();
                    services.AddSingleton(new OllamaApiClient(ollamaHttp));
                });
            });
        using var client = factory.CreateClient();
        var initial = await client.PostAsJsonAsync("/api/analyze", new
        {
            Provider = "local",
            Model = "test",
            Images = new[] { "AA==" }
        });
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);

        File.Copy(replacement.Path, workbook.Path, overwrite: true);
        var followUpRequest = new
        {
            Provider = "local",
            Model = "test",
            AdditionalInfo = "補足",
            History = new[] { new { Role = "assistant", Text = "前回の判定" } }
        };
        var followUp = await client.PostAsJsonAsync("/api/analyze", followUpRequest);

        Assert.Equal(HttpStatusCode.OK, followUp.StatusCode);
        using var request = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var messages = request.RootElement.GetProperty("messages");
        Assert.Contains("缶 → 金属", messages[0].GetProperty("content").GetString());
        Assert.Equal("前回の判定", messages[1].GetProperty("content").GetString());
        Assert.Equal("補足", messages[2].GetProperty("content").GetString());
        Assert.False(messages[2].TryGetProperty("images", out _));

        await File.WriteAllTextAsync(workbook.Path, "broken");
        var broken = await client.PostAsJsonAsync("/api/analyze", followUpRequest);
        Assert.Equal(HttpStatusCode.InternalServerError, broken.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Theory]
    [InlineData("missing-model")]
    [InlineData("missing-image")]
    [InlineData("invalid-image")]
    [InlineData("missing-follow-up")]
    public async Task Analyze_RejectsInvalidRequestBeforeReadingExcel(string failure)
    {
        await using var factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["WasteCatalog:ExcelPath"] = "" })));
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/analyze", new
        {
            Provider = "local",
            Model = failure == "missing-model" ? "" : "test",
            Images = failure switch
            {
                "missing-image" => Array.Empty<string>(),
                "invalid-image" => new[] { "invalid base64" },
                _ => new[] { "AA==" }
            },
            History = failure == "missing-follow-up" ? new[] { new { Role = "user", Text = "判定" } } : null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class FakeOllamaHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"model":"test","message":{"role":"assistant","content":"判定結果"},"done":true}""" + "\n",
                    Encoding.UTF8, "application/x-ndjson")
            };
        }
    }
}
