using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;


namespace ImageAnalysisAiClient
{
    public partial class Form1 : Form
    {
        // ローカルで起動している Ollama サーバーに接続するチャットクライアント。
        // IChatClient インターフェイスを使用して、抽象化された方法でチャット機能を利用する。
        private IChatClient _chatClient;

        // Ollama API クライアントのインスタンス（モデル一覧取得用）
        private readonly OllamaApiClient _ollamaClient;

        // 選択中のモデル名を保持する。
        private string? _selectedModel;

        // MCP クライアント（ツール呼び出し用）
        private McpClient? _mcpClient;
        private IList<McpClientTool> _tools;

        // MCP サーバから取得したツールのリスト

        public Form1()
        {
            InitializeComponent();

            // OllamaApiClient を作成し、IChatClient として使用する。
            _ollamaClient = new OllamaApiClient(new Uri("http://127.0.0.1:11434"));
            _chatClient = _ollamaClient;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            await LoadModelsAsync();
            await LoadListToolsAsync();
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
                Text = $"ChatAppAI - {model}";
            }
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            var message = txbSendMessage.Text;
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            // モデルが未選択のまま送信されないようにする。
            if (string.IsNullOrEmpty(_selectedModel))
            {
                txbReceiveMessage.Text = "モデルを選択してください。";
                return;
            }

            // 送信中は二重送信を防ぐためボタンを無効化する。
            btnSend.Enabled = false;
            txbReceiveMessage.Clear();

            try
            {
                // ChatOptions を使用してモデル ID を指定する。
                var options = new ChatOptions
                {
                    Tools = [.. _tools],
                    ModelId = _selectedModel
                };

                // IChatClient の GetStreamingResponseAsync を使用して、メッセージを送信し応答をストリーミングで受信する。
                await foreach (var update in _chatClient.GetStreamingResponseAsync(message, options))
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
