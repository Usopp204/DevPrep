using Microsoft.VisualBasic.FileIO;
using Xunit;

namespace AuthAPI.Tests;

public class WorkbookReconciliationTests
{
    [Fact]
    public void Case_mapping_preserves_workbook_cardinality_and_unique_ids()
    {
        var rows = ReadRows(Path.Combine(AppContext.BaseDirectory, "WorkbookCaseMapping.csv"));

        Assert.Equal(1253, rows.Count);
        Assert.Equal(209, rows.Select(row => row[0]).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(1253, rows.Select(row => row[3]).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(835, rows.Count(row => row[4] == "N"));
        Assert.Equal(209, rows.Count(row => row[4] == "A"));
        Assert.Equal(209, rows.Count(row => row[4] == "B"));
        Assert.Equal(1253, rows.Count(row => row[5] == "IMPLEMENTED"));
        Assert.Equal(0, rows.Count(row => row[5] == "BLOCKED"));
        Assert.All(rows, row => Assert.Contains(row[5], new[] { "IMPLEMENTED", "BLOCKED" }));
    }

    private static List<string[]> ReadRows(string path)
    {
        using var parser = new TextFieldParser(path);
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        _ = parser.ReadFields();

        var rows = new List<string[]>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            Assert.NotNull(fields);
            Assert.Equal(6, fields!.Length);
            rows.Add(fields);
        }

        return rows;
    }
}
