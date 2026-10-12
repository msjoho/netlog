using NetLog.Core.Models;
using NetLog.Core.RunSpec;

namespace NetLog.Core.Tests;

public class ExclusionRuleParserTests
{
    [Theory]
    [InlineData("process:svchost (Dnscache)", "process", "svchost (Dnscache)", "both")]
    [InlineData("host:10.20.0.0/16@out", "host", "10.20.0.0/16", "out")]
    [InlineData("user:CORP\\svc-*@in", "user", "CORP\\svc-*", "in")]
    [InlineData("  HOST : nas01* ", "host", "nas01*", "both")]            // trims, lower-cases kind
    [InlineData("host:fe80::/10", "host", "fe80::/10", "both")]            // colons inside the pattern
    [InlineData("user:alice@corp.local", "user", "alice@corp.local", "both")] // '@' not followed by in/out stays
    public void Parses_valid_text(string text, string kind, string pattern, string direction)
    {
        var r = ExclusionRuleParser.Parse(text);
        Assert.Equal(kind, r.Kind);
        Assert.Equal(pattern, r.Pattern);
        Assert.Equal(direction, r.Direction);
        Assert.Null(r.Note);
    }

    [Theory]
    [InlineData("svchost", "kind:pattern")]
    [InlineData("port:443", "Unknown kind")]
    [InlineData("process:", "empty")]
    [InlineData("host:10.0.0.0/33", "CIDR")]
    [InlineData("host:10.0.0/8", "CIDR")]
    [InlineData("process:app[1]", "[")]
    public void Rejects_invalid_text_with_a_clear_message(string text, string fragment)
    {
        var ok = ExclusionRuleParser.TryParse(text, out var rule, out var error);
        Assert.False(ok);
        Assert.Null(rule);
        Assert.Contains(fragment, error, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<FormatException>(() => ExclusionRuleParser.Parse(text));
    }

    [Theory]
    [InlineData("host:go.microsoft.com@out:443", "host", "go.microsoft.com", "out", "443")]
    [InlineData("process:System@in:445", "process", "System", "in", "445")]
    [InlineData("host:nas01*@445", "host", "nas01*", "both", "445")]              // port alone = any direction
    [InlineData("host:nas01*@both:445", "host", "nas01*", "both", "445")]
    [InlineData("user:CORP\\svc-*@out:80,443", "user", "CORP\\svc-*", "out", "80,443")]
    [InlineData("host:10.0.0.0/8@out: 1024 - 65535", "host", "10.0.0.0/8", "out", "1024-65535")] // spaces stripped
    [InlineData("host:fe80::/10@in:5985", "host", "fe80::/10", "in", "5985")]      // colons inside the pattern
    [InlineData("user:alice@corp.local", "user", "alice@corp.local", "both", null)] // not a port: stays in the pattern
    [InlineData("host:x@out", "host", "x", "out", null)]
    [InlineData("process:app@2024", "process", "app", "both", "2024")]            // a trailing @digits IS a port ...
    [InlineData("process:app@2024@both", "process", "app@2024", "both", null)]    // ... append @both to keep it in the pattern
    [InlineData("process:app@2024?", "process", "app@2024?", "both", null)]
    [InlineData("host:x@-", "host", "x@-", "both", null)]                         // no digit: not a port spec
    public void Parses_port_qualifier(string text, string kind, string pattern, string direction, string? port)
    {
        var r = ExclusionRuleParser.Parse(text);
        Assert.Equal(kind, r.Kind);
        Assert.Equal(pattern, r.Pattern);
        Assert.Equal(direction, r.Direction);
        Assert.Equal(port, r.Port);
    }

    [Theory]
    [InlineData("host:x@out:0", "port")]
    [InlineData("host:x@out:65536", "port")]
    [InlineData("host:x@out:44x", "port")]
    [InlineData("host:x@out:443-80", "port")]
    [InlineData("host:x@out:", "port")]
    [InlineData("host:x@both:80,", "port")]
    [InlineData("host:x@445:out", "direction before the port")]   // wrong order is an error, not a pattern
    [InlineData("host:x@:445", "direction before the port")]
    public void Rejects_bad_port_spec(string text, string fragment)
    {
        var ok = ExclusionRuleParser.TryParse(text, out var rule, out var error);
        Assert.False(ok);
        Assert.Null(rule);
        Assert.Contains(fragment, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_reports_bad_port_from_a_file()
    {
        Assert.Contains("port", ExclusionRuleParser.Validate(new ExclusionRule("host", "x", "out", null, "99999")), StringComparison.OrdinalIgnoreCase);
        Assert.Null(ExclusionRuleParser.Validate(new ExclusionRule("host", "x", "out", null, "80,443,1024-2048")));
    }

    [Theory]
    [InlineData("host:nas01*@out:445")]
    [InlineData("host:nas01*@445")]
    [InlineData("process:System@in:135,445")]
    public void Format_roundtrips_port(string text)
    {
        Assert.Equal(text, ExclusionRuleParser.Format(ExclusionRuleParser.Parse(text)));
    }

    [Fact]
    public void Normalized_blanks_port_to_null_and_strips_spaces()
    {
        Assert.Null(new ExclusionRule("host", "x", "out", null, "  ").Normalized().Port);
        Assert.Equal("80,443", new ExclusionRule("host", "x", "out", null, " 80 , 443 ").Normalized().Port);
    }

    [Fact]
    public void Validate_reports_bad_direction_from_a_file()
    {
        Assert.Contains("direction", ExclusionRuleParser.Validate(new ExclusionRule("host", "x", "sideways")));
        Assert.Null(ExclusionRuleParser.Validate(new ExclusionRule("host", "10.0.0.0/8", "both")));
        Assert.Null(ExclusionRuleParser.Validate(new ExclusionRule("host", "2001:db8::/32", "in")));
    }

    [Fact]
    public void Format_roundtrips_and_omits_both()
    {
        Assert.Equal("host:10.20.0.0/16@out", ExclusionRuleParser.Format(ExclusionRuleParser.Parse("host:10.20.0.0/16@out")));
        Assert.Equal("process:Teams*", ExclusionRuleParser.Format(ExclusionRuleParser.Parse("process:Teams*")));
    }

    [Fact]
    public void Normalized_lower_cases_and_defaults_direction()
    {
        var n = new ExclusionRule("Process", "x", "", "n").Normalized();
        Assert.Equal("process", n.Kind);
        Assert.Equal("both", n.Direction);
        Assert.Equal("n", n.Note);
        var m = new ExclusionRule("host", "y", "OUT").Normalized();
        Assert.Equal("out", m.Direction);
    }
}
