using System.ClientModel;
using System.Text;
using ImageAnalysisAiClient.Web;
using Markdig;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(_ => new OllamaApiClient(new Uri(builder.Configuration["Ollama:Endpoint"] ?? "http://127.0.0.1:11434")));
builder.Services.AddSingleton<AzureChatClientProvider>();

var app = builder.Build();

var markdownPipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .DisableHtml()
    .Build();
var azureDeploymentName = app.Configuration["Azure:DeploymentName"] ?? "gpt-6-luna";

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/api", () => Results.Ok(new
{
    name = "ImageAnalysisAiClient API",
    version = "1.0"
}));

app.MapGet("/api/models", async (string provider, OllamaApiClient ollama, CancellationToken cancellationToken) =>
{
    if (provider == "azure")
    {
        return Results.Ok(new[] { azureDeploymentName });
    }

    try
    {
        var models = await ollama.ListLocalModelsAsync(cancellationToken);
        return Results.Ok(models.Select(m => m.Name).OrderBy(name => name).ToArray());
    }
    catch (Exception ex)
    {
        return Results.Problem($"モデル一覧の取得に失敗しました: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/markdown", (MarkdownRequest request) =>
    Results.Content(Markdown.ToHtml(request.Markdown ?? string.Empty, markdownPipeline), "text/html; charset=utf-8"));

app.MapPost("/api/analyze", async (
    AnalyzeRequest request,
    HttpContext context,
    OllamaApiClient ollama,
    AzureChatClientProvider azureProvider,
    ILogger<Program> logger) =>
{
    var cancellationToken = context.RequestAborted;
    if (string.IsNullOrWhiteSpace(request.Model))
    {
        return Results.BadRequest("モデルを選択してください。");
    }

    var history = request.History ?? [];
    var isInitialMessage = history.Count == 0;
    var additionalInfo = request.AdditionalInfo?.Trim() ?? string.Empty;
    byte[][] images;
    try
    {
        images = (request.Images ?? []).Select(Convert.FromBase64String).ToArray();
    }
    catch (FormatException)
    {
        return Results.BadRequest("画像データが不正です。");
    }

    if (isInitialMessage && images.Length == 0)
    {
        return Results.BadRequest("開始ボタンを押して画像を取得してください。");
    }

    if (!isInitialMessage && string.IsNullOrWhiteSpace(additionalInfo))
    {
        return Results.BadRequest("再判定するための補足情報を入力してください。");
    }

    var promptPath = Path.Combine(AppContext.BaseDirectory, "waste_sorting_vision_prompt.md");
    if (!File.Exists(promptPath))
    {
        return Results.Problem("waste_sorting_vision_prompt.md が見つかりません。");
    }

    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, await File.ReadAllTextAsync(promptPath, cancellationToken))
    };
    foreach (var item in history)
    {
        var role = item.Role == "assistant" ? ChatRole.Assistant : ChatRole.User;
        messages.Add(new ChatMessage(role, item.Text ?? string.Empty));
    }

    var userPrompt = isInitialMessage
        ? $"以下は500ms間隔で取得した連続画像です（全{images.Length}枚）。すべての画像を確認し、同じ物体が複数画像に写っている場合は重複して数えずに分別してください。"
        : additionalInfo;
    if (isInitialMessage && !string.IsNullOrWhiteSpace(additionalInfo))
    {
        userPrompt += $"\n\nユーザーからのゴミに関する補足情報:\n{additionalInfo}";
    }

    var userMessage = new ChatMessage(ChatRole.User, userPrompt);
    if (isInitialMessage)
    {
        foreach (var image in images)
        {
            userMessage.Contents.Add(new DataContent(image, "image/png"));
        }
    }

    messages.Add(userMessage);

    IChatClient chatClient;
    try
    {
        chatClient = request.Provider == "azure" ? azureProvider.GetClient() : ollama;
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }

    context.Response.ContentType = "text/plain; charset=utf-8";
    var assistantResponse = new StringBuilder();
    try
    {
        var options = new ChatOptions { ModelId = request.Model };
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            if (string.IsNullOrEmpty(update.Text))
            {
                continue;
            }

            assistantResponse.Append(update.Text);
            await context.Response.WriteAsync(update.Text, cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.Empty;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "AI への問い合わせに失敗しました。");
        await context.Response.WriteAsync($"\n\n[[ERROR]]エラー: {ex.Message}", CancellationToken.None);
        return Results.Empty;
    }

    if (isInitialMessage && UndeterminedImageStore.IsUndeterminedResult(assistantResponse.ToString()))
    {
        try
        {
            UndeterminedImageStore.SaveSharpest(images);
        }
        catch (Exception ex)
        {
            await context.Response.WriteAsync($"\n\n判定不能画像の保存に失敗しました: {ex.Message}", CancellationToken.None);
        }
    }

    return Results.Empty;
});

app.Run();

internal sealed record MarkdownRequest(string? Markdown);

internal sealed record HistoryItem(string Role, string? Text);

internal sealed record AnalyzeRequest(
    string? Provider,
    string? Model,
    string? AdditionalInfo,
    List<string>? Images,
    List<HistoryItem>? History);

internal sealed class AzureChatClientProvider(IConfiguration configuration)
{
    private readonly Lock _lock = new();
    private IChatClient? _client;

    public IChatClient GetClient()
    {
        lock (_lock)
        {
            if (_client is not null)
            {
                return _client;
            }

            var apiKey = Environment.GetEnvironmentVariable("AZURE_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "環境変数 AZURE_API_KEY が設定されていません。Azure APIキーを設定してからサーバーを起動してください。");
            }

            var deploymentName = configuration["Azure:DeploymentName"] ?? "gpt-6-luna";
            var endpoint = configuration["Azure:Endpoint"]
                ?? throw new InvalidOperationException("Azure:Endpoint が設定されていません。");

            var openAiChatClient = new OpenAI.Chat.ChatClient(
                    deploymentName,
                    new ApiKeyCredential(apiKey.Trim()),
                    new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
                .AsIChatClient();

            _client = new ChatClientBuilder(openAiChatClient)
                .UseFunctionInvocation()
                .Build();
            return _client;
        }
    }
}
