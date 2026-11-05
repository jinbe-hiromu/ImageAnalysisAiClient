using ImageAnalysisAiClient.Web;
using System.Buffers.Binary;
using Xunit;

namespace ImageAnalysisAiClient.Web.Tests;

public class WasteCatalogReaderCacheTests
{
    [Fact]
    public async Task ReadAsync_ReusesUnchangedContent()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.Save();
        using var reader = new WasteCatalogReader();

        var first = await reader.ReadAsync(workbook.Path, Path.GetTempPath());
        var second = await reader.ReadAsync(workbook.Path, Path.GetTempPath());

        Assert.Equal([new WasteCatalogEntry("紙", "紙類")], first);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task ReadAsync_IgnoresTimestampChangesWhenContentIsUnchanged()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.Save();
        using var reader = new WasteCatalogReader();
        var first = await reader.ReadAsync(workbook.Path, Path.GetTempPath());
        File.SetLastWriteTimeUtc(workbook.Path, File.GetLastWriteTimeUtc(workbook.Path).AddHours(-1));

        var second = await reader.ReadAsync(workbook.Path, Path.GetTempPath());

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ReadAsync_ReparsesReplacementWithSameSizeAndTimestamp()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.Save();
        using var replacement = new ExcelTestWorkbook();
        replacement.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "缶"), ("C2", "金属"));
        replacement.Save();
        var targetLength = (int)Math.Max(new FileInfo(workbook.Path).Length, new FileInfo(replacement.Path).Length);
        PadZipComment(workbook.Path, targetLength);
        PadZipComment(replacement.Path, targetLength);
        Assert.Equal(new FileInfo(workbook.Path).Length, new FileInfo(replacement.Path).Length);
        using var reader = new WasteCatalogReader();
        var first = await reader.ReadAsync(workbook.Path, Path.GetTempPath());
        var timestamp = File.GetLastWriteTimeUtc(workbook.Path);
        File.Copy(replacement.Path, workbook.Path, overwrite: true);
        File.SetLastWriteTimeUtc(workbook.Path, timestamp);

        var second = await reader.ReadAsync(workbook.Path, Path.GetTempPath());

        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(workbook.Path));
        Assert.NotSame(first, second);
        Assert.Equal([new WasteCatalogEntry("缶", "金属")], second);
    }

    [Fact]
    public async Task ReadAsync_DoesNotUseCacheWhenFileDisappears()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.Save();
        using var reader = new WasteCatalogReader();
        await reader.ReadAsync(workbook.Path, Path.GetTempPath());
        File.Delete(workbook.Path);

        await Assert.ThrowsAsync<FileNotFoundException>(() => reader.ReadAsync(workbook.Path, Path.GetTempPath()));
    }

    [Fact]
    public async Task ReadAsync_RejectsInvalidReplacementAndRecovers()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.Save();
        using var replacement = new ExcelTestWorkbook();
        replacement.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "缶"), ("C2", "金属"));
        replacement.Save();
        using var reader = new WasteCatalogReader();
        await reader.ReadAsync(workbook.Path, Path.GetTempPath());
        await File.WriteAllTextAsync(workbook.Path, "broken");

        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(workbook.Path, Path.GetTempPath()));
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(workbook.Path, Path.GetTempPath()));
        File.Copy(replacement.Path, workbook.Path, overwrite: true);
        var recovered = await reader.ReadAsync(workbook.Path, Path.GetTempPath());

        Assert.Equal([new WasteCatalogEntry("缶", "金属")], recovered);
        Assert.Same(recovered, await reader.ReadAsync(workbook.Path, Path.GetTempPath()));
    }

    [Fact]
    public async Task ReadAsync_UsesNewPathAndRejectsMissingPathEvenWithCachedData()
    {
        using var firstWorkbook = new ExcelTestWorkbook();
        firstWorkbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        firstWorkbook.Save();
        using var secondWorkbook = new ExcelTestWorkbook();
        secondWorkbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "缶"), ("C2", "金属"));
        secondWorkbook.Save();
        using var reader = new WasteCatalogReader();
        await reader.ReadAsync(firstWorkbook.Path, Path.GetTempPath());

        var second = await reader.ReadAsync(secondWorkbook.Path, Path.GetTempPath());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(null, Path.GetTempPath()));

        Assert.Equal([new WasteCatalogEntry("缶", "金属")], second);
        Assert.Contains("WasteCatalog:ExcelPath", error.Message);
    }

    [Fact]
    public async Task ReadAsync_SharesResultBetweenConcurrentRequests()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.Save();
        using var reader = new WasteCatalogReader();

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => reader.ReadAsync(workbook.Path, Path.GetTempPath())));

        Assert.All(results, result => Assert.Same(results[0], result));
        Assert.Equal([new WasteCatalogEntry("紙", "紙類")], results[0]);
    }

    [Fact]
    public async Task ReadAsync_ProtectsCachedEntriesFromMutation()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.Save();
        using var reader = new WasteCatalogReader();
        var entries = await reader.ReadAsync(workbook.Path, Path.GetTempPath());

        var list = Assert.IsAssignableFrom<IList<WasteCatalogEntry>>(entries);
        Assert.Throws<NotSupportedException>(() => list[0] = new WasteCatalogEntry("缶", "金属"));
        Assert.Equal([new WasteCatalogEntry("紙", "紙類")], await reader.ReadAsync(workbook.Path, Path.GetTempPath()));
    }

    [Fact]
    public async Task ReadAsync_HonorsCancellation()
    {
        using var workbook = new ExcelTestWorkbook();
        workbook.AddSheet("一覧", ("B1", "廃棄物名"), ("C1", "廃棄先"), ("B2", "紙"), ("C2", "紙類"));
        workbook.Save();
        using var reader = new WasteCatalogReader();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.ReadAsync(workbook.Path, Path.GetTempPath(), cancellation.Token));
        Assert.Equal([new WasteCatalogEntry("紙", "紙類")], await reader.ReadAsync(workbook.Path, Path.GetTempPath()));
    }

    private static void PadZipComment(string path, int targetLength)
    {
        var bytes = File.ReadAllBytes(path);
        var originalLength = bytes.Length;
        Assert.Equal(new byte[] { 0x50, 0x4b, 0x05, 0x06 }, bytes[(originalLength - 22)..(originalLength - 18)]);
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(originalLength - 2)));
        Array.Resize(ref bytes, targetLength);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(originalLength - 2), checked((ushort)(targetLength - originalLength)));
        File.WriteAllBytes(path, bytes);
    }
}
