namespace ImageAnalysisAiClient.Web;

internal sealed record WasteCatalogEntry(string Name, string Destination);

internal sealed class WastePromptBuilder
{
    public string Build(IReadOnlyList<WasteCatalogEntry> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Count == 0)
        {
            throw new ArgumentException("分別索引が空です。", nameof(catalog));
        }

        var mappings = string.Join("\n", catalog.Select(entry => $"{entry.Name} → {entry.Destination}"));
        return """
            あなたは廃棄物分別アシスタントです。
            画像に写っている物体を列挙し、分別先が異なる素材・部位に分解してください。
            各素材・部位の廃棄先は、以下の索引の対応に基づいて決めてください。
            索引にない物体や素材が特定できない場合は、廃棄先を「判定不可」とし、確認すべき点を示してください。
            索引に記載のない禁止事項、注意事項、廃棄方法を推測しないでください。

            | # | 物体 | 分解した素材・部位 | 廃棄先 | 根拠・注意 | 精度 |
            |---|------|------------------|--------|-----------|------|

            # 廃棄物索引
            """ + "\n" + mappings;
    }
}
