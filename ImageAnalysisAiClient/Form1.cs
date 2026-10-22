using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;


namespace ImageAnalysisAiClient
{
    public partial class Form1 : Form
    {
        private const string AzureDeploymentName = "gpt-6-luna";
        private const string AzureEndpoint = "https://taiseiishiyama-9692-resource.services.ai.azure.com/openai/v1";
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

        // 連続取得中は同じカメラを使用する。
        private VideoCapture? _capture;
        private Task<VideoCapture>? _cameraInitializationTask;
        private bool _startCaptureWhenReady;
        private bool _isCapturing;
        private bool _isClosing;
        private Stopwatch? _startRequestStopwatch;

        public Form1()
        {
            InitializeComponent();

            // OllamaApiClient を作成し、IChatClient として使用する。
            _ollamaClient = new OllamaApiClient(new Uri("http://127.0.0.1:11434"));
            _chatClient = _ollamaClient;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
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
                    txbReceiveMessage.Text =
                        "利用可能なモデルが見つかりませんでした。Ollama が起動しているか、モデルが pull 済みか確認してください。";
                }
            }
            catch (Exception ex)
            {
                txbReceiveMessage.Text = $"モデル一覧の取得に失敗しました: {ex.Message}";
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
            if (_isCapturing)
            {
                return;
            }

            _startRequestStopwatch = Stopwatch.StartNew();
            if (_capture is null)
            {
                _startCaptureWhenReady = true;
                btnSelectImage.Enabled = false;
                btnStop.Enabled = true;
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
                    btnStop.Enabled = false;
                    btnSend.Enabled = _capturedImages.Count > 0;
                    UpdateCaptureStatus("カメラを準備できませんでした。開始ボタンで再試行できます");
                    txbReceiveMessage.Text = $"カメラの準備に失敗しました: {ex.Message}";
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
                btnStop.Enabled = true;
                UpdateCaptureStatus("取得中（500ms間隔）");

                CaptureImage();
                if (_isCapturing)
                {
                    captureTimer.Start();
                }
            }
            catch (Exception ex)
            {
                HandleCaptureError(ex);
            }
        }

        private void captureTimer_Tick(object? sender, EventArgs e)
        {
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

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopImageCapture();
            UpdateCaptureStatus("停止中");
        }

        private void StopImageCapture()
        {
            captureTimer.Stop();
            _isCapturing = false;
            _startCaptureWhenReady = false;
            _startRequestStopwatch = null;
            btnSelectImage.Enabled = true;
            btnStop.Enabled = false;
            btnSend.Enabled = _capturedImages.Count > 0;
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
            txbReceiveMessage.Text = $"カメラ撮影に失敗しました: {ex.Message}";
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
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(settingsPath))
            {
                throw new FileNotFoundException(
                    "appsettings.json が見つかりません。アプリの出力フォルダーに配置してください。", settingsPath);
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (!document.RootElement.TryGetProperty("Azure", out var azure) ||
                    !azure.TryGetProperty("ApiKey", out var apiKeyElement))
                {
                    throw new InvalidOperationException(
                        "appsettings.json に Azure.ApiKey が設定されていません。");
                }

                var apiKey = apiKeyElement.GetString();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException(
                        "appsettings.json の Azure.ApiKey が空です。");
                }

                return apiKey;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "appsettings.json のJSON形式が正しくありません。", ex);
            }
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            if (_isCapturing || _startCaptureWhenReady)
            {
                txbReceiveMessage.Text = "すべての画像を判別するには、先に停止ボタンを押してください。";
                return;
            }

            // 解析中に撮影が続いても、対象はボタンを押した時点までに取得した全画像に固定する。
            var images = _capturedImages.ToArray();
            var additionalInfo = txbAdditionalInfo.Text.Trim();

            // 画像が未選択の場合は解析できない。
            if (images.Length == 0)
            {
                txbReceiveMessage.Text = "開始ボタンを押して画像を取得してください。";
                return;
            }

            // モデルが未選択のまま送信されないようにする。
            if (string.IsNullOrEmpty(_selectedModel))
            {
                txbReceiveMessage.Text = "モデルを選択してください。";
                return;
            }

            var promptPath = Path.Combine(AppContext.BaseDirectory, "waste_sorting_vision_prompt.md");
            if (!File.Exists(promptPath))
            {
                txbReceiveMessage.Text = "waste_sorting_vision_prompt.md が見つかりません。アプリの出力フォルダーを確認してください。";
                return;
            }

            var systemPrompt = await File.ReadAllTextAsync(promptPath);

            // 送信中は二重送信を防ぐためボタンを無効化する。
            btnSend.Enabled = false;
            txbReceiveMessage.Clear();

            try
            {
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

                // ユーザーメッセージに撮影セッションで取得した全画像を添付する。
                var systemMessage = new ChatMessage(ChatRole.System, systemPrompt);
                var userPrompt = $"以下は500ms間隔で取得した連続画像です（全{images.Length}枚）。すべての画像を確認し、同じ物体が複数画像に写っている場合は重複して数えずに分別してください。";
                if (!string.IsNullOrWhiteSpace(additionalInfo))
                {
                    userPrompt += $"\n\nユーザーからのゴミに関する補足情報:\n{additionalInfo}";
                }

                var chatMessage = new ChatMessage(ChatRole.User, userPrompt);
                foreach (var image in images)
                {
                    chatMessage.Contents.Add(new DataContent(image, "image/png"));
                }

                // IChatClient の GetStreamingResponseAsync を使用して、メッセージを送信し応答をストリーミングで受信する。
                var activeChatClient = cmbProvider.SelectedIndex == 1 ? _azureChatClient! : _chatClient;
                await foreach (var update in activeChatClient.GetStreamingResponseAsync([systemMessage, chatMessage], options))
                {
                    // 届いた断片を都度 txbReceiveMessage に追記して表示する。
                    txbReceiveMessage.AppendText(update.Text);
                }
            }
            catch (Exception ex)
            {
                txbReceiveMessage.Text = $"エラー: {ex.Message}";
            }
            finally
            {
                btnSend.Enabled = _capture is null && _capturedImages.Count > 0;
            }
        }
    }
}
