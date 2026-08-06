using System.ComponentModel;
using ChatAppAI.McpServer.Services;
using ModelContextProtocol.Server;

namespace ChatAppAI.McpServer.Tools;

[McpServerToolType]
public sealed class RemoteEngineTools(RemoteEngineService remoteEngineService)
{
    private readonly RemoteEngineService _remoteEngineService = remoteEngineService;

    [McpServerTool(Name = "remoteengine_list_providers"), Description("RemoteEngine に登録されているプロバイダーの静的情報を取得します。未起動のプロバイダーも検索できます。")]
    public async Task<string> ListProvidersAsync(
        [Description("RemoteEngine の gRPC endpoint。例: http://127.0.0.1:7103")] string remoteEngineUri,
        [Description("プロバイダー名、ID、タイトル、説明、タグで絞り込む検索語。全件取得する場合は省略します。")]
        string? providerName = null,
        CancellationToken cancellationToken = default)
    {
        { }
        var providers = await _remoteEngineService.ListProvidersAsync(remoteEngineUri, providerName, cancellationToken).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Serialize(new { status = "ok", providers });
    }

    [McpServerTool(Name = "remoteengine_terminate_provider"), Description("RemoteEngine のプロバイダーを停止します。providerName または pid のどちらかを指定します。")]
    public async Task<string> TerminateProviderAsync(
        [Description("RemoteEngine の gRPC endpoint。例: http://127.0.0.1:7103")] string remoteEngineUri,
        [Description("停止するプロバイダー名。pid を指定する場合は省略できます。")]
        string? providerName = null,
        [Description("停止するプロバイダーの PID。providerName を指定する場合は省略できます。")]
        int? pid = null,
        CancellationToken cancellationToken = default)
    {
        await _remoteEngineService.TerminateProviderAsync(remoteEngineUri, providerName, pid, cancellationToken).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Serialize(new { status = "ok", providerName, pid });
    }

    [McpServerTool(Name = "remoteengine_get_provider_manual"), Description("RemoteEngine の provider ID とバージョンに一致するマニュアル本文とリンク一覧を取得します。")]
    public async Task<string> GetProviderManualAsync(
        [Description("RemoteEngine の gRPC endpoint。例: http://127.0.0.1:7103")] string remoteEngineUri,
        [Description("取得対象 provider の ID")] string providerId,
        [Description("取得対象 provider のバージョン。完全一致で検索します。")] string version,
        CancellationToken cancellationToken)
    {
        var manual = await _remoteEngineService.GetProviderManualAsync(remoteEngineUri, providerId, version, cancellationToken).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Serialize(new { status = "ok", manual });
    }

    [McpServerTool(Name = "provider_get_variable_value"), Description("指定 URL の ProviderCore に接続し、Controller 名と配下の Variable 名で一意に特定した値を取得します。読み取り専用です。")]
    public async Task<string> GetProviderVariableValueAsync(
        [Description("ProviderCore の gRPC endpoint。例: http://127.0.0.1:50000")] string providerUri,
        [Description("取得対象 Controller の名前。完全一致で検索します。")] string controllerName,
        [Description("Controller 配下の取得対象 Variable の名前。完全一致で検索します。")] string variableName,
        CancellationToken cancellationToken = default)
    {
        var variable = await _remoteEngineService.GetProviderVariableValueAsync(providerUri, controllerName, variableName, cancellationToken).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Serialize(new { status = "ok", variable });
    }
}