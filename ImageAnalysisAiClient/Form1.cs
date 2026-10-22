using Microsoft.Extensions.AI;
using Markdig;
using ModelContextProtocol.Client;
using OllamaSharp;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text;


namespace ImageAnalysisAiClient
{
    public partial class Form1 : Form
    {
        private const string AzureDeploymentName = "gpt-6-luna";
        private const string AzureEndpoint = "https://taiseiishiyama-9692-resource.services.ai.azure.com/openai/v1";
        private static readonly TimeSpan CaptureDuration = TimeSpan.FromSeconds(3);
        private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

        // ローカルで起動している Ollama サーバーに接続するチャットクライアント。
        // IChatClient インターフェイスを使用して、抽象化された方法でチャット機能を利用する。
        private IChatClient _chatClient;
        private IChatClient? _azureChatClient;

        // Ollama API クライアントのインスタンス（モデル一覧取得用）
        private readonly OllamaApiClient _ollamaClient;

        // 選択中のモデル名を保持する。
        private string? _selectedModel;

        // MCP クライアント（ツール呼び出し用）
        private McpClient? _mcpClient;
        private IList<McpClientTool>? _tools;

        // MCP サーバから取得したツールのリスト

        // 現在の撮影セッションで取得した全画像（VLM への添付用）。
        private readonly List<byte[]> _capturedImages = [];
        private readonly List<ChatMessage> _conversationHistory = [];
        private readonly StringBuilder _resultMarkdown = new();

        // 連続取得中は同じカメラを使用する。
        private VideoCapture? _capture;
        private Task<VideoCapture>? _cameraInitializationTask;
        private bool _startCaptureWhenReady;
        private bool _isCapturing;
        private bool _isClosing;
        private bool _isResultViewReady;
        private Stopwatch? _startRequestStopwatch;
        private Stopwatch? _captureStopwatch;

        public Form1()
        {
            InitializeComponent();

            // OllamaApiClient を作成し、IChatClient として使用する。
            _ollamaClient = new OllamaApiClient(new Uri("http://127.0.0.1:11434"));
            _chatClient = _ollamaClient;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                await webViewResults.EnsureCoreWebView2Async();
                webViewResults.CoreWebView2.Settings.IsScriptEnabled = false;
                _isResultViewReady = true;
                RenderResultMarkdown();
            }
            catch (Exception ex)
            {
                lblReceiveMessage.Text = "解析結果（WebView2を初期化できませんでした）";
                MessageBox.Show(
                    this,
                    $"解析結果の表示を初期化できませんでした。Microsoft Edge WebView2 Runtime がインストールされているか確認してください。\n\n{ex.Message}",
                    "WebView2 の初期化エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            _ = InitializeCameraAsync();

            await LoadListToolsAsync();

            if (cmbProvider.SelectedIndex == 0)
            {
                await LoadModelsAsync();
            }
        }

        private async void cmbProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            _selectedModel = null;
            cmbModel.Items.Clear();

            if (cmbProvider.SelectedIndex == 1)
            {
                cmbModel.Items.Add(AzureDeploymentName);
                cmbModel.SelectedIndex = 0;
            }
            else if (cmbProvider.SelectedIndex == 0)
            {
                await LoadModelsAsync();
            }
        }

        // Ollama にインストール済みのローカルモデル一覧を取得し、ドロップダウンに反映する。
        private async Task LoadModelsAsync()
        {
            try
            {
                var models = await _ollamaClient.ListLocalModelsAsync();
                var names = models
                    .Select(m => m.Name)
                    .OrderBy(name => name)
                    .ToArray();

                cmbModel.Items.Clear();
                _selectedModel = null;
                cmbModel.Items.AddRange(names);

                if (cmbModel.Items.Count > 0)
                {
                    // 先頭のモデルを既定として選択する（SelectedIndexChanged で _selectedModel が設定される）。
                    cmbModel.SelectedIndex = 0;
                }
                else
                {
                    SetResultMarkdown(
                        "利用可能なモデルが見つかりませんでした。Ollama が起動しているか、モデルが pull 済みか確認してください。");
                }
            }
            catch (Exception ex)
            {
                SetResultMarkdown($"モデル一覧の取得に失敗しました: {ex.Message}");
            }
        }

        private async Task LoadListToolsAsync()
        {
            try
            {
                var transport = new HttpClientTransport(new()
                {
                    Endpoint = new Uri("http://127.0.0.1:5050/mcp")
                });

                _mcpClient = await McpClient.CreateAsync(transport);

                _tools = await _mcpClient.ListToolsAsync();

                _chatClient = new ChatClientBuilder(_ollamaClient).UseFunctionInvocation().Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MCP ツールの読み込みに失敗しました: {ex.Message}");
            }
        }

        // ドロップダウンで選択されたモデルを _selectedModel フィールドに保持する。
        private void cmbModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbModel.SelectedItem is string model)
            {
                _selectedModel = model;
                Text = $"ImageAnalysisAiClient - {model}";
            }
        }

        // 開始直後に1枚取得し、その後は500msごとに取得する。
        private void btnSelectImage_Click(object sender, EventArgs e)
        {
            if (_isCapturing || !btnSelectImage.Enabled)
            {
                return;
            }

            _conversationHistory.Clear();
            SetResultMarkdown(string.Empty);
            btnSend.Enabled = false;
            _startRequestStopwatch = Stopwatch.StartNew();
            if (_capture is null)
            {
                _startCaptureWhenReady = true;
                btnSelectImage.Enabled = false;
                btnSend.Enabled = false;
                UpdateCaptureStatus("カメラ準備中。準備完了後に撮影を開始します");

                if (_cameraInitializationTask is null)
                {
                    _ = InitializeCameraAsync();
                }

                return;
            }

            StartImageCapture();
        }

        private async Task InitializeCameraAsync()
        {
            if (_cameraInitializationTask is not null || _capture is not null || _isClosing)
            {
                return;
            }

            UpdateCaptureStatus("カメラを準備中...");
            var initializationStopwatch = Stopwatch.StartNew();
            _cameraInitializationTask = Task.Run(() =>
            {
                var capture = new VideoCapture(0);
                if (!capture.IsOpened())
                {
                    capture.Dispose();
                    throw new InvalidOperationException("カメラを開けませんでした。カメラが接続され、他のアプリで使用中でないことを確認してください。");
                }

                return capture;
            });

            try
            {
                var initializedCapture = await _cameraInitializationTask;
                if (_isClosing || IsDisposed)
                {
                    initializedCapture.Dispose();
                    return;
                }

                _capture = initializedCapture;
                UpdateCaptureStatus($"カメラ準備完了（{initializationStopwatch.ElapsedMilliseconds}ms）");

                if (_startCaptureWhenReady)
                {
                    StartImageCapture();
                }
            }
            catch (Exception ex)
            {
                if (!_isClosing && !IsDisposed)
                {
                    _startCaptureWhenReady = false;
                    _startRequestStopwatch = null;
                    btnSelectImage.Enabled = true;
                    btnSend.Enabled = _isResultViewReady && _capturedImages.Count > 0;
                    UpdateCaptureStatus("カメラを準備できませんでした。開始ボタンで再試行できます");
                    SetResultMarkdown($"カメラの準備に失敗しました: {ex.Message}");
                }
            }
            finally
            {
                _cameraInitializationTask = null;
            }
        }

        private void StartImageCapture()
        {
            if (_capture is null || _isCapturing || _isClosing)
            {
                return;
            }

            try
            {
                _startCaptureWhenReady = false;
                _capturedImages.Clear();
                btnSend.Enabled = false;
                picImage.Image?.Dispose();
                picImage.Image = null;
                _isCapturing = true;
                btnSelectImage.Enabled = false;
                UpdateCaptureStatus("取得中（500ms間隔、3秒間）");

                CaptureImage();
                if (_isCapturing)
                {
                    _captureStopwatch = Stopwatch.StartNew();
                    captureTimer.Start();
                    captureDurationTimer.Start();
                }
            }
            catch (Exception ex)
            {
                HandleCaptureError(ex);
            }
        }

        private void captureTimer_Tick(object? sender, EventArgs e)
        {
            if (_captureStopwatch?.Elapsed >= CaptureDuration)
            {
                CompleteImageCapture();
                return;
            }

            try
            {
                CaptureImage();
            }
            catch (Exception ex)
            {
                HandleCaptureError(ex);
            }
        }

        private void CaptureImage()
        {
            if (_capture is null)
            {
                return;
            }

            using var frame = new Mat();
            if (!_capture.Read(frame) || frame.Empty())
            {
                throw new InvalidOperationException("カメラから画像を取得できませんでした。");
            }

            using var bitmap = BitmapConverter.ToBitmap(frame);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);

            _capturedImages.Add(stream.ToArray());

            // 既存のプレビュー画像を破棄する。
            picImage.Image?.Dispose();
            picImage.Image = new Bitmap(bitmap);
            if (_startRequestStopwatch is not null)
            {
                var elapsed = _startRequestStopwatch.ElapsedMilliseconds;
                _startRequestStopwatch = null;
                UpdateCaptureStatus($"取得中（500ms間隔、開始から初回画像まで{elapsed}ms）");
            }
            else
            {
                UpdateCaptureStatus("取得中（500ms間隔）");
            }
        }

        private void captureDurationTimer_Tick(object? sender, EventArgs e)
        {
            CompleteImageCapture();
        }

        private void CompleteImageCapture()
        {
            StopImageCapture();
            UpdateCaptureStatus("撮影完了");
        }

        private void StopImageCapture()
        {
            captureTimer.Stop();
            captureDurationTimer.Stop();
            _isCapturing = false;
            _startCaptureWhenReady = false;
            _startRequestStopwatch = null;
            _captureStopwatch = null;
            btnSelectImage.Enabled = true;
            btnSend.Enabled = _isResultViewReady && _capturedImages.Count > 0;
        }

        private void ReleaseCamera()
        {
            StopImageCapture();
            _capture?.Dispose();
            _capture = null;
        }

        private void HandleCaptureError(Exception ex)
        {
            ReleaseCamera();
            UpdateCaptureStatus("撮影停止（取得済み画像を保持）");
            SetResultMarkdown($"カメラ撮影に失敗しました: {ex.Message}");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!e.Cancel)
            {
                _isClosing = true;
                ReleaseCamera();
            }
        }

        private void UpdateCaptureStatus(string status)
        {
            lblImagePath.Text = $"取得枚数: {_capturedImages.Count}枚 - {status}";
        }

        private static string LoadAzureApiKey()
        {
            var apiKey = Environment.GetEnvironmentVariable("AZURE_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "環境変数 AZURE_API_KEY が設定されていません。Azure APIキーを設定してからアプリを起動してください。");
            }

            return apiKey.Trim();
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            if (_isCapturing || _startCaptureWhenReady)
            {
                SetResultMarkdown("撮影が完了するまでお待ちください。");
                return;
            }

            // 画像は最初の送信時だけ添付し、以降は補足情報を同じ会話に追加する。
            var images = _capturedImages.ToArray();
            var additionalInfo = txbAdditionalInfo.Text.Trim();

            if (images.Length == 0)
            {
                SetResultMarkdown("開始ボタンを押して画像を取得してください。");
                return;
            }

            var isInitialMessage = _conversationHistory.Count == 0;
            if (!isInitialMessage && string.IsNullOrWhiteSpace(additionalInfo))
            {
                SetResultMarkdown("再判定するための補足情報を入力してください。");
                return;
            }

            // モデルが未選択のまま送信されないようにする。
            if (string.IsNullOrEmpty(_selectedModel))
            {
                SetResultMarkdown("モデルを選択してください。");
                return;
            }

            var promptPath = Path.Combine(AppContext.BaseDirectory, "waste_sorting_vision_prompt.md");
            if (!File.Exists(promptPath))
            {
                SetResultMarkdown("waste_sorting_vision_prompt.md が見つかりません。アプリの出力フォルダーを確認してください。");
                return;
            }

            string? systemPrompt = null;
            if (isInitialMessage)
            {
                systemPrompt = await File.ReadAllTextAsync(promptPath);
            }

            // 送信中は二重送信を防ぐためボタンを無効化する。
            btnSend.Enabled = false;
            btnSelectImage.Enabled = false;
            var displayPrompt = isInitialMessage
                ? "撮影した画像の判定を依頼しました。"
                : additionalInfo;
            if (isInitialMessage && !string.IsNullOrWhiteSpace(additionalInfo))
            {
                displayPrompt += $"\n補足情報: {additionalInfo}";
            }

            AppendResultMarkdown(
                $"{Environment.NewLine}### あなた{Environment.NewLine}{Environment.NewLine}{displayPrompt}{Environment.NewLine}{Environment.NewLine}### 判定{Environment.NewLine}{Environment.NewLine}");

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

            if (isInitialMessage)
            {
                _conversationHistory.Add(new ChatMessage(ChatRole.System, systemPrompt!));
            }

            _conversationHistory.Add(userMessage);
            var assistantResponse = new StringBuilder();
            var requestSucceeded = false;
            try
            {
                txbAdditionalInfo.Enabled = false;
                if (cmbProvider.SelectedIndex == 1 && _azureChatClient is null)
                {
                    var azureApiKey = LoadAzureApiKey();

                    var openAiChatClient = new OpenAI.Chat.ChatClient(
                        AzureDeploymentName,
                        new System.ClientModel.ApiKeyCredential(azureApiKey),
                        new OpenAI.OpenAIClientOptions { Endpoint = new Uri(AzureEndpoint) })
                        .AsIChatClient();

                    _azureChatClient = new ChatClientBuilder(openAiChatClient)
                        .UseFunctionInvocation()
                        .Build();
                }

                // ChatOptions を使用してモデル ID を指定する。
                var options = new ChatOptions
                {
                    Tools = _tools is null ? null : [.. _tools],
                    ModelId = _selectedModel
                };

                // IChatClient の GetStreamingResponseAsync を使用して、メッセージを送信し応答をストリーミングで受信する。
                var activeChatClient = cmbProvider.SelectedIndex == 1 ? _azureChatClient! : _chatClient;
                var renderThrottle = Stopwatch.StartNew();
                await foreach (var update in activeChatClient.GetStreamingResponseAsync(_conversationHistory, options))
                {
                    assistantResponse.Append(update.Text);
                    if (renderThrottle.ElapsedMilliseconds >= 500)
                    {
                        RenderResultMarkdown(assistantResponse.ToString());
                        renderThrottle.Restart();
                    }
                }

                requestSucceeded = true;
            }
            catch (Exception ex)
            {
                AppendResultMarkdown($"エラー: {ex.Message}{Environment.NewLine}");
            }
            finally
            {
                txbAdditionalInfo.Enabled = true;
                if (requestSucceeded)
                {
                    _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, assistantResponse.ToString()));
                    foreach (var imageContent in userMessage.Contents.OfType<DataContent>().ToArray())
                    {
                        userMessage.Contents.Remove(imageContent);
                    }

                    txbAdditionalInfo.Clear();
                    AppendResultMarkdown($"{assistantResponse}{Environment.NewLine}{Environment.NewLine}");
                }
                else
                {
                    _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
                    if (_conversationHistory.Count == 1 && isInitialMessage)
                    {
                        _conversationHistory.Clear();
                    }
                }

                btnSelectImage.Enabled = !_isCapturing && !_startCaptureWhenReady;
                btnSend.Enabled = _isResultViewReady && btnSelectImage.Enabled && _capturedImages.Count > 0;
            }
        }

        private void SetResultMarkdown(string markdown)
        {
            _resultMarkdown.Clear();
            _resultMarkdown.Append(markdown);
            RenderResultMarkdown();
        }

        private void AppendResultMarkdown(string markdown)
        {
            _resultMarkdown.Append(markdown);
            RenderResultMarkdown();
        }

        private void RenderResultMarkdown(string? pendingAssistantResponse = null)
        {
            if (!_isResultViewReady || webViewResults.IsDisposed)
            {
                return;
            }

            var markdown = pendingAssistantResponse is null
                ? _resultMarkdown.ToString()
                : string.Concat(_resultMarkdown, pendingAssistantResponse);
            var renderedMarkdown = Markdown.ToHtml(markdown, MarkdownPipeline);
            var html = $$"""
                <!DOCTYPE html>
                <html lang="ja">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <style>
                        body { font-family: "Segoe UI", sans-serif; color: #24292f; padding: 12px; line-height: 1.6; }
                        table { border-collapse: collapse; width: 100%; margin: 12px 0; }
                        th, td { border: 1px solid #d0d7de; padding: 8px 12px; text-align: left; vertical-align: top; }
                        th { background: #f6f8fa; }
                        tr:nth-child(even) { background: #f6f8fa; }
                        pre { overflow-x: auto; padding: 12px; background: #f6f8fa; }
                    </style>
                </head>
                <body>{{renderedMarkdown}}</body>
                </html>
                """;

            webViewResults.NavigateToString(html);
        }
    }
}
