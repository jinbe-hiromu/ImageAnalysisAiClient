using Xunit;

namespace ImageAnalysisAiClient.Web.Tests;

public sealed class CatalogExcelFactAttribute : FactAttribute
{
    public CatalogExcelFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WASTE_CATALOG_TEST_EXCEL")))
        {
            Skip = "WASTE_CATALOG_TEST_EXCEL is not configured.";
        }
    }
}
