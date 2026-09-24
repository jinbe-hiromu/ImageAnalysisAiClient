using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;


namespace ImageAnalysisAiClient
{
    public partial class Form1 : Form
    {
        private const string AzureDeploymentName = "gpt-6-luna";
        private const string AzureEndpoint = "https://taiseiishiyama-9692-resource.services.ai.azure.com/openai/v1";
        // TODO: Azure AI Foundry の実際の API キーを手動で入力してください。
        private static readonly string AzureApiKey = "YOUR_AZURE_AI_FOUNDRY_API_KEY";

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

        // 選択中の画像データ（VLM への添付用）。未選択時は null。
        private byte[]? _selectedImageBytes;

        // 選択中の画像の MIME タイプ（例: image/png）。
        private string? _selectedImageMediaType;

        public Form1()
        {
            InitializeComponent();

            // OllamaApiClient を作成し、IChatClient として使用する。
            _ollamaClient = new OllamaApiClient(new Uri("http://127.0.0.1:11434"));
            _chatClient = _ollamaClient;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
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

        // 「画像を選択」ボタン。エクスプローラー（OpenFileDialog）を開き、
        // 選択された画像をプレビュー表示しつつ、VLM 送信用にバイト配列として保持する。
        private void btnSelectImage_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "画像ファイルを選択してください",
                Filter = "画像ファイル (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|すべてのファイル (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                var path = dialog.FileName;

                // VLM への送信用に画像バイトを読み込む。
                _selectedImageBytes = File.ReadAllBytes(path);
                _selectedImageMediaType = GetImageMediaType(path);

                // 既存のプレビュー画像を破棄する。
                picImage.Image?.Dispose();

                // ファイルロックを避けるため、バイト配列から生成したうえでコピーを保持する。
                using var ms = new MemoryStream(_selectedImageBytes);
                using var loaded = Image.FromStream(ms);
                picImage.Image = new Bitmap(loaded);

                lblImagePath.Text = Path.GetFileName(path);
            }
            catch (Exception ex)
            {
                _selectedImageBytes = null;
                _selectedImageMediaType = null;
                picImage.Image?.Dispose();
                picImage.Image = null;
                lblImagePath.Text = "画像が選択されていません";
                txbReceiveMessage.Text = $"画像の読み込みに失敗しました: {ex.Message}";
            }
        }

        // 拡張子から画像の MIME タイプを判定する。
        private static string GetImageMediaType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            // 画像が選択されているかどうか。
            var hasImage = _selectedImageBytes is not null && _selectedImageMediaType is not null;

            // 画像が未選択の場合は解析できない。
            if (!hasImage)
            {
                txbReceiveMessage.Text = "画像を選択してください。";
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
                    if (AzureApiKey == "YOUR_AZURE_AI_FOUNDRY_API_KEY")
                    {
                        txbReceiveMessage.Text = "Form1.cs の AzureApiKey に Azure AI Foundry の API キーを入力してください。";
                        return;
                    }

                    var openAiChatClient = new OpenAI.Chat.ChatClient(
                        AzureDeploymentName,
                        new System.ClientModel.ApiKeyCredential(AzureApiKey),
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

                // ユーザーメッセージを構築し、選択された画像を添付する。
                var systemMessage = new ChatMessage(ChatRole.System, systemPrompt);
                var chatMessage = new ChatMessage(ChatRole.User, "添付画像を指示に従って分別してください。");
                chatMessage.Contents.Add(new DataContent(_selectedImageBytes!, _selectedImageMediaType!));

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
                btnSend.Enabled = true;
            }
        }
    }
}
