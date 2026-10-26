using MiddiFranchi.Application.DataImport;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class CsvDocumentTests
{
    [Fact]
    public void Parse_ShouldHandleQuotedCommasAndEscapedQuotes()
    {
        var document = CsvDocument.Parse(
            "code,name,note\r\n1,\"Rossi, Mario\",\"Lui disse \"\"ciao\"\"\"\r\n",
            "test.csv");

        document.Headers.ShouldBe(["code", "name", "note"]);
        document.Rows.Count.ShouldBe(1);
        document.Rows[0].Get("name").ShouldBe("Rossi, Mario");
        document.Rows[0].Get("note").ShouldBe("Lui disse \"ciao\"");
    }

    [Fact]
    public void Parse_ShouldHandleNewLinesInsideQuotes()
    {
        var document = CsvDocument.Parse(
            "code,note\n1,\"prima\nseconda\"\n2,ok\n",
            "test.csv");

        document.Rows.Count.ShouldBe(2);
        document.Rows[0].Get("note").ShouldBe("prima\nseconda");
        document.Rows[1].Get("note").ShouldBe("ok");
    }

    [Fact]
    public void Parse_ShouldIgnoreBomAndEmptyLines()
    {
        var document = CsvDocument.Parse(
            "\uFEFFcode,name\n\n1,Test\n\n",
            "test.csv");

        document.Rows.Count.ShouldBe(1);
        document.Rows[0].Get("name").ShouldBe("Test");
    }

    [Fact]
    public void EnsureColumns_ShouldReportMissingHeaders()
    {
        var document = CsvDocument.Parse("code,name\n1,Test\n", "test.csv");

        var exception = Should.Throw<ImportValidationException>(() =>
            document.EnsureColumns("code", "missing"));

        exception.Message.ShouldContain("missing");
    }

    [Fact]
    public void Parse_ShouldRejectUnclosedQuote()
    {
        Should.Throw<ImportValidationException>(() =>
            CsvDocument.Parse("code,name\n1,\"abc\n", "test.csv"));
    }

    [Fact]
    public void Write_ShouldEscapeValuesAndRoundTrip()
    {
        var content = CsvDocument.Write(
            ["code", "name"],
            [new string?[] { "1", "Rossi, \"Mario\"" }]);

        var document = CsvDocument.Parse(content, "test.csv");

        document.Rows[0].Get("name").ShouldBe("Rossi, \"Mario\"");
    }
}
