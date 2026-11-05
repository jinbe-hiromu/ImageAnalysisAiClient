using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ImageAnalysisAiClient.Web.Tests;

internal sealed class ExcelTestWorkbook : IDisposable
{
    private readonly SpreadsheetDocument document;
    private readonly WorkbookPart workbookPart;
    private uint nextSheetId = 1;

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

    public ExcelTestWorkbook()
    {
        document = SpreadsheetDocument.Create(Path, SpreadsheetDocumentType.Workbook);
        workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook(new Sheets());
    }

    public void AddSheet(string name, params (string Reference, string Value)[] cells)
    {
        var part = workbookPart.AddNewPart<WorksheetPart>();
        var data = new SheetData();
        foreach (var group in cells.GroupBy(cell => uint.Parse(new string(cell.Reference.Where(char.IsDigit).ToArray()))))
        {
            var row = new Row { RowIndex = group.Key };
            foreach (var (reference, value) in group)
            {
                row.Append(new Cell
                {
                    CellReference = reference,
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(value))
                });
            }

            data.Append(row);
        }

        part.Worksheet = new Worksheet(data);
        workbookPart.Workbook!.GetFirstChild<Sheets>()!.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(part),
            SheetId = nextSheetId++,
            Name = name
        });
    }

    public void Save()
    {
        document.Dispose();
    }

    public void SetSharedString(string sheetName, string reference, SharedStringItem value)
    {
        var part = workbookPart.SharedStringTablePart ?? workbookPart.AddNewPart<SharedStringTablePart>();
        part.SharedStringTable ??= new SharedStringTable();
        var index = part.SharedStringTable.Elements<SharedStringItem>().Count();
        part.SharedStringTable.Append(value);
        var cell = FindCell(sheetName, reference);
        cell.InlineString = null;
        cell.DataType = CellValues.SharedString;
        cell.CellValue = new CellValue(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public void SetInlineString(string sheetName, string reference, InlineString value)
    {
        FindCell(sheetName, reference).InlineString = value;
    }

    public void SetFormula(string sheetName, string reference, string formula, string cachedValue)
    {
        var cell = FindCell(sheetName, reference);
        cell.InlineString = null;
        cell.DataType = CellValues.String;
        cell.CellFormula = new CellFormula(formula);
        cell.CellValue = new CellValue(cachedValue);
    }

    public void RemoveRowIndexes(string sheetName)
    {
        var sheet = workbookPart.Workbook!.Sheets!.Elements<Sheet>().Single(item => item.Name == sheetName);
        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        foreach (var row in part.Worksheet!.Descendants<Row>())
        {
            row.RowIndex = null;
        }
    }

    private Cell FindCell(string sheetName, string reference)
    {
        var sheet = workbookPart.Workbook!.Sheets!.Elements<Sheet>().Single(item => item.Name == sheetName);
        var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        return part.Worksheet!.Descendants<Cell>().Single(item => item.CellReference == reference);
    }

    public void Dispose()
    {
        document.Dispose();
        File.Delete(Path);
    }
}
