using ImageAnalysisAiClient.Web;
using DocumentFormat.OpenXml.Spreadsheet;
using System.IO.Compression;
using Xunit;

namespace ImageAnalysisAiClient.Web.Tests;

public class ExcelCatalogReaderTests
{
    [Fact]
    public async Task ReadAsync_RejectsMissingPath()
    {
        using var reader = new WasteCatalogReader();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(null, AppContext.BaseDirectory));

        Assert.Contains("WasteCatalog:ExcelPath", error.Message);
    }

    [Fact]
    public async Task ReadAsync_RejectsMissingFile()
    {
        using var reader = new WasteCatalogReader();

        await Assert.ThrowsAsync<FileNotFoundException>(() => reader.ReadAsync($"{Guid.NewGuid():N}.xlsx", Path.GetTempPath()));
    }

    [Fact]
    public async Task ReadAsync_RejectsInvalidExcel()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "not an Excel file");
            using var reader = new WasteCatalogReader();

            var error = await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(path, Path.GetTempPath()));

            Assert.Contains("Excel", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_ReadsNameAndDestination()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B4", "廃棄物名"), ("C4", "廃棄先"),
            ("B5", "紙"), ("C5", "紙類"));
        workbook.Save();
        using var reader = new WasteCatalogReader();

        var entries = await reader.ReadAsync(workbook.Path, Path.GetTempPath());

        Assert.Equal([new WasteCatalogEntry("紙", "紙類")], entries);
    }

    [Fact]
    public async Task ReadAsync_ReadsMultipleSheetsAndColumnGroupsWithoutNotes()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧1", ("B3", "廃棄物名"), ("C3", "廃棄先"), ("D3", "注意点"),
            ("E3", "廃棄物名"), ("F3", "廃棄先"), ("G3", "注意点"),
            ("B4", "紙"), ("C4", "紙類"), ("D4", "注意1"),
            ("E4", "缶"), ("F4", "金属"), ("G4", "注意2"));
        workbook.AddSheet("一覧2", ("F7", "廃棄物名"), ("G7", "廃棄先"),
            ("F8", "木"), ("G8", "一般ごみ"));
        workbook.Save();

        var entries = await ReadAsync(workbook.Path, Path.GetTempPath());

        Assert.Equal([new("紙", "紙類"), new("缶", "金属"), new("木", "一般ごみ")], entries);
    }

    [Fact]
    public async Task ReadAsync_UsesDisplayTextWithoutPhoneticRuns()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"),
            ("B2", "仮"), ("C2", "仮"));
        workbook.SetSharedString("一覧", "B2", new SharedStringItem(
            new Run(new Text("空")), new Run(new Text("き缶")), new PhoneticRun(new Text("アキカン"))));
        workbook.SetInlineString("一覧", "C2", new InlineString(
            new Run(new Text("金")), new Run(new Text("属")), new PhoneticRun(new Text("キンゾク"))));
        workbook.Save();

        var entries = await ReadAsync(workbook.Path, Path.GetTempPath());

        Assert.Equal([new WasteCatalogEntry("空き缶", "金属")], entries);
    }

    [Fact]
    public async Task ReadAsync_RejectsMissingIndex()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "別の見出し"));
        workbook.Save();

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadAsync(workbook.Path, Path.GetTempPath()));

        Assert.Contains("索引", error.Message);
    }

    [Theory]
    [InlineData("紙", "")]
    [InlineData("", "紙類")]
    public async Task ReadAsync_RejectsIncompletePair(string name, string destination)
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"),
            ("B2", "缶"), ("C2", "金属"), ("B3", name), ("C3", destination));
        workbook.Save();

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadAsync(workbook.Path, Path.GetTempPath()));

        Assert.Contains("対応", error.Message);
    }

    [Fact]
    public async Task ReadAsync_IgnoresFormulaOnlySummary()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.AddSheet("集計", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "仮"), ("C2", "仮"));
        workbook.SetFormula("集計", "B2", "一覧!B2", "かみ");
        workbook.SetFormula("集計", "C2", "一覧!C2", "紙類");
        workbook.Save();

        var entries = await ReadAsync(workbook.Path, Path.GetTempPath());

        Assert.Equal([new WasteCatalogEntry("紙", "紙類")], entries);
    }

    [Fact]
    public async Task ReadAsync_RejectsFormulasInSourceIndex()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"),
            ("B2", "紙"), ("C2", "紙類"), ("B3", "缶"), ("C3", "仮"));
        workbook.SetFormula("一覧", "C3", "\"金属\"", "金属");
        workbook.Save();

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadAsync(workbook.Path, Path.GetTempPath()));

        Assert.Contains("数式", error.Message);
    }

    [CatalogExcelFact]
    public async Task ReadAsync_ReadsConfiguredRealWorkbook()
    {
        var path = Environment.GetEnvironmentVariable("WASTE_CATALOG_TEST_EXCEL")!;

        var entries = await ReadAsync(path, Path.GetTempPath());

        Assert.Equal(397, entries.Count);
        Assert.Contains(new WasteCatalogEntry("空き缶", "複合金属くず"), entries);
        Assert.Contains(new WasteCatalogEntry("アクリル板", "焼却廃プラ"), entries);
    }

    [Fact]
    public async Task ReadAsync_RejectsIncompleteColumnHeaders()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"),
            ("E1", "廃棄物名"), ("F1", "別の見出し"),
            ("B2", "紙"), ("C2", "紙類"), ("E2", "缶"), ("F2", "金属"));
        workbook.Save();

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadAsync(workbook.Path, Path.GetTempPath()));

        Assert.Contains("見出し", error.Message);
    }

    [Fact]
    public async Task ReadAsync_RejectsCorruptWorksheetXml()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.Save();
        using (var archive = ZipFile.Open(workbook.Path, ZipArchiveMode.Update))
        {
            var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")!;
            sheet.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("xl/worksheets/sheet1.xml").Open());
            writer.Write("<worksheet><broken>");
        }

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadAsync(workbook.Path, Path.GetTempPath()));

        Assert.Contains("Excel", error.Message);
    }

    [Fact]
    public async Task ReadAsync_ReadsRowsWithoutExplicitRowIndexes()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.RemoveRowIndexes("一覧");
        workbook.Save();

        var entries = await ReadAsync(workbook.Path, Path.GetTempPath());

        Assert.Equal([new WasteCatalogEntry("紙", "紙類")], entries);
    }

    [Fact]
    public async Task ReadAsync_ResolvesRelativePathAndPreservesDuplicates()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"),
            ("B2", "紙"), ("C2", "紙類"), ("B3", "紙"), ("C3", "紙類"));
        workbook.Save();
        using var reader = new WasteCatalogReader();

        var entries = await reader.ReadAsync(Path.GetFileName(workbook.Path), Path.GetDirectoryName(workbook.Path)!);

        Assert.Equal([new WasteCatalogEntry("紙", "紙類"), new WasteCatalogEntry("紙", "紙類")], entries);
    }

    [Fact]
    public async Task ReadAsync_RejectsUnpairedNameHeaderOnAnotherSheet()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧1", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.AddSheet("一覧2", ("B1", "廃棄物名"), ("C1", "別の見出し"), ("B2", "缶"), ("C2", "金属"));
        workbook.Save();

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => ReadAsync(workbook.Path, Path.GetTempPath()));

        Assert.Contains("見出し", error.Message);
    }

    private static async Task<IReadOnlyList<WasteCatalogEntry>> ReadAsync(string? path, string contentRoot)
    {
        using var reader = new WasteCatalogReader();
        return await reader.ReadAsync(path, contentRoot);
    }
}
