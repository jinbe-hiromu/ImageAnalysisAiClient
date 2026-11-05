using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml;

namespace ImageAnalysisAiClient.Web;

internal sealed class WasteCatalogReader(string? configuredPath, string contentRoot)
{
    public async Task<IReadOnlyList<WasteCatalogEntry>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException("WasteCatalog:ExcelPath が設定されていません。");
        }

        var data = await File.ReadAllBytesAsync(Path.GetFullPath(configuredPath, contentRoot), cancellationToken);
        try
        {
            using var stream = new MemoryStream(data);
            using var document = SpreadsheetDocument.Open(stream, false);
            var workbook = document.WorkbookPart ?? throw new InvalidDataException("Excelにブックがありません。");
            var sheets = workbook.Workbook?.Sheets ?? throw new InvalidDataException("Excelにシートがありません。");
            var strings = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().ToArray() ?? [];
            var entries = new List<WasteCatalogEntry>();
            foreach (var sheet in sheets.Elements<Sheet>())
            {
                var part = workbook.GetPartById(sheet.Id!) as WorksheetPart
                    ?? throw new InvalidDataException("Excelのシートが不正です。");
                var sheetData = part.Worksheet?.GetFirstChild<SheetData>()
                    ?? throw new InvalidDataException("Excelのシートデータがありません。");
                var rows = sheetData.Elements<Row>().ToArray();
                foreach (var (header, rowIndex) in rows.Select((row, index) => (row, index)))
                {
                    var cells = header.Elements<Cell>().ToDictionary(cell => Column(cell.CellReference!), cell => cell);
                    var names = cells.Where(item => CellText(item.Value, strings).StartsWith("廃棄物名", StringComparison.Ordinal))
                        .Select(item => item.Key).ToArray();
                    var destinations = cells.Where(item => CellText(item.Value, strings).StartsWith("廃棄先", StringComparison.Ordinal))
                        .Select(item => item.Key).ToHashSet();
                    if (names.Any(name => !destinations.Contains(name + 1)) ||
                        (names.Length > 0 && destinations.Any(destination => !names.Contains(destination - 1))))
                    {
                        throw new InvalidDataException("Excelの索引の見出しが不完全です。");
                    }

                    foreach (var (column, cell) in cells)
                    {
                        if (!CellText(cell, strings).StartsWith("廃棄物名", StringComparison.Ordinal) ||
                            !cells.TryGetValue(column + 1, out var destinationHeader) ||
                            !CellText(destinationHeader, strings).StartsWith("廃棄先", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var dataRows = rows.Skip(rowIndex + 1)
                            .Select(row => row.Elements<Cell>().ToDictionary(item => Column(item.CellReference!)))
                            .ToArray();
                        var indexCells = dataRows.SelectMany(row => row
                                .Where(item => item.Key == column || item.Key == column + 1)
                                .Select(item => item.Value))
                            .Where(item => item.CellFormula is not null || !string.IsNullOrWhiteSpace(CellText(item, strings)))
                            .ToArray();
                        if (indexCells.Length > 0 && indexCells.All(item => item.CellFormula is not null))
                        {
                            continue;
                        }
                        if (indexCells.Any(item => item.CellFormula is not null))
                        {
                            throw new InvalidDataException("Excelの元索引に未対応の数式があります。");
                        }

                        foreach (var row in dataRows)
                        {
                            var name = row.TryGetValue(column, out var nameCell) ? CellText(nameCell, strings) : null;
                            var destination = row.TryGetValue(column + 1, out var destinationCell) ? CellText(destinationCell, strings) : null;
                            var hasName = !string.IsNullOrWhiteSpace(name);
                            var hasDestination = !string.IsNullOrWhiteSpace(destination);
                            if (hasName != hasDestination)
                            {
                                throw new InvalidDataException("Excelの索引に対応先を特定できない項目があります。");
                            }

                            if (hasName && hasDestination)
                            {
                                entries.Add(new WasteCatalogEntry(name!, destination!));
                            }
                        }
                    }
                }
            }

            if (entries.Count == 0)
            {
                throw new InvalidDataException("Excelに分別索引がありません。");
            }

            return entries;
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException or InvalidDataException or System.Xml.XmlException)
        {
            throw new InvalidDataException($"Excelの形式または索引が不正です: {ex.Message}", ex);
        }

    }

    private static int Column(string reference)
    {
        var result = 0;
        foreach (var character in reference.TakeWhile(char.IsAsciiLetterUpper))
        {
            result = result * 26 + character - 'A' + 1;
        }

        return result;
    }

    private static string CellText(Cell cell, SharedStringItem[] strings)
    {
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            if (!int.TryParse(cell.CellValue?.Text, out var index) || index < 0 || index >= strings.Length)
            {
                throw new InvalidDataException("Excelの共有文字列が不正です。");
            }

            return DisplayText(strings[index]);
        }

        return cell.InlineString is { } inline ? DisplayText(inline) : cell.CellValue?.Text ?? string.Empty;
    }

    private static string DisplayText(OpenXmlElement element) =>
        string.Concat(element.ChildElements.Select(child => child switch
        {
            Text text => text.Text,
            Run run => string.Concat(run.Elements<Text>().Select(text => text.Text)),
            _ => string.Empty
        }));
}
