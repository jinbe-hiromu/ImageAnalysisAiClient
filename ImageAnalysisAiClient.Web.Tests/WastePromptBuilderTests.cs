using ImageAnalysisAiClient.Web;
using Xunit;

namespace ImageAnalysisAiClient.Web.Tests;

public class WastePromptBuilderTests
{
    [Fact]
    public void Build_RejectsEmptyCatalog()
    {
        Assert.Throws<ArgumentException>(() => new WastePromptBuilder().Build([]));
    }

    [Fact]
    public void Build_IncludesCatalogMapping()
    {
        var catalog = new[] { new WasteCatalogEntry("ペン", "焼却廃プラ") };

        var prompt = new WastePromptBuilder().Build(catalog);

        Assert.Contains("ペン → 焼却廃プラ", prompt);
    }

    [Fact]
    public void Build_RequiresUnknownWhenCatalogHasNoMatch()
    {
        var prompt = new WastePromptBuilder().Build([new("ペン", "焼却廃プラ")]);

        Assert.Contains("判定不可", prompt);
        Assert.Contains("索引にない", prompt);
        Assert.Contains("| 物体 |", prompt);
    }
}
