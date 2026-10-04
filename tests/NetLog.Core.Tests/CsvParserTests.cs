using NetLog.Core.Csv;

namespace NetLog.Core.Tests;

public class CsvParserTests
{
    [Fact]
    public void Splits_plain_fields()
    {
        var f = CsvParser.ParseLine("Outgoing,10.0.0.1,51001,10.0.0.5,443,chrome");
        Assert.Equal(new[] { "Outgoing", "10.0.0.1", "51001", "10.0.0.5", "443", "chrome" }, f);
    }

    [Fact]
    public void Handles_quoted_fields_with_commas()
    {
        var f = CsvParser.ParseLine("\"Outgoing\",\"a,b\",\"\",x,1,\"proc name\"");
        Assert.Equal(new[] { "Outgoing", "a,b", "", "x", "1", "proc name" }, f);
    }

    [Fact]
    public void Unescapes_doubled_quotes_inside_quoted_field()
    {
        var f = CsvParser.ParseLine("\"he said \"\"hi\"\"\",x");
        Assert.Equal(new[] { "he said \"hi\"", "x" }, f);
    }

    [Fact]
    public void Preserves_trailing_empty_field()
    {
        var f = CsvParser.ParseLine("Incoming,H,445,C,");
        Assert.Equal(new[] { "Incoming", "H", "445", "C", "" }, f);
    }

    [Fact]
    public void Empty_string_returns_one_empty_field()
    {
        Assert.Equal(new[] { "" }, CsvParser.ParseLine(""));
    }

    [Fact]
    public void Single_field_with_no_comma()
    {
        Assert.Equal(new[] { "singlevalue" }, CsvParser.ParseLine("singlevalue"));
    }
}
