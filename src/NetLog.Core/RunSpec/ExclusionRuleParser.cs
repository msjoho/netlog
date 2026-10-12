using System.Net;
using System.Net.Sockets;
using NetLog.Core.Models;

namespace NetLog.Core.RunSpec;

/// <summary>Text form of a rule: <c>kind:pattern[@in|@out|@both][:port]</c>, or <c>kind:pattern@port</c> for
/// a port in either direction. The first ':' splits kind from pattern; only a trailing '@' qualifier that reads
/// as a direction and/or a port spec is taken, so 'host:fe80::/10' and 'user:alice@corp' parse as intended.
/// The port goes after the '@' because host patterns may themselves contain ':' (IPv6).</summary>
public static class ExclusionRuleParser
{
    public static readonly string[] Kinds = { ExclusionRule.KindProcess, ExclusionRule.KindUser, ExclusionRule.KindHost };
    public static readonly string[] Directions = { "in", "out", "both" };

    public static ExclusionRule Parse(string text)
    {
        if (!TryParse(text, out var rule, out var error)) throw new FormatException(error);
        return rule!;
    }

    public static bool TryParse(string text, out ExclusionRule? rule, out string? error)
    {
        rule = null;
        error = null;
        var t = (text ?? "").Trim();
        var colon = t.IndexOf(':');
        if (colon <= 0)
        {
            error = "Expected kind:pattern (kinds: process, user, host).";
            return false;
        }
        var kind = t[..colon];
        var rest = t[(colon + 1)..];
        var direction = "both";
        string? port = null;
        var at = rest.LastIndexOf('@');
        if (at >= 0)
        {
            // Qualifier grammar after the last '@': in|out|both[:port]  or  port
            var suffix = rest[(at + 1)..].Trim();
            var colon2 = suffix.IndexOf(':');
            var dirPart = (colon2 >= 0 ? suffix[..colon2] : suffix).Trim().ToLowerInvariant();
            if (Directions.Contains(dirPart))
            {
                direction = dirPart;
                rest = rest[..at];
                if (colon2 >= 0)
                {
                    port = suffix[(colon2 + 1)..];
                    if (string.IsNullOrWhiteSpace(port))
                    {
                        error = $"Port after '@{dirPart}:' must not be empty (e.g. 443, 80,443 or 1024-65535).";
                        return false;
                    }
                }
            }
            else if (colon2 < 0 && LooksLikePortSpec(suffix))
            {
                port = suffix;
                rest = rest[..at];
            }
            else if (colon2 >= 0 && (dirPart.Length == 0 || LooksLikePortSpec(dirPart)))
            {
                // '@445:out' or '@:445' — a qualifier in the wrong shape, not a pattern.
                error = "Put the direction before the port: @out:445, @in:445 or @both:445 (or just @445).";
                return false;
            }
        }
        var candidate = new ExclusionRule(kind, rest, direction, null, port).Normalized();
        error = Validate(candidate);
        if (error is not null) return false;
        rule = candidate;
        return true;
    }

    /// <summary>Null when valid, otherwise a one-line reason suitable for the TUI / CLI.</summary>
    public static string? Validate(ExclusionRule r)
    {
        if (!Kinds.Contains(r.Kind)) return $"Unknown kind '{r.Kind}' (use process, user or host).";
        if (string.IsNullOrWhiteSpace(r.Pattern)) return "Pattern must not be empty.";
        if (r.Pattern.IndexOfAny(new[] { '[', ']' }) >= 0) return "Pattern must not contain '[' or ']' (only * and ? wildcards are supported).";
        if (!Directions.Contains(r.Direction)) return $"Unknown direction '{r.Direction}' (use in, out or both).";
        if (r.Kind == ExclusionRule.KindHost && r.Pattern.Contains('/') && !IsCidr(r.Pattern))
            return $"'{r.Pattern}' is not a valid CIDR (e.g. 10.0.0.0/8 or fe80::/10).";
        if (r.Port is not null && !IsPortSpec(r.Port))
            return $"Port '{r.Port}' is not valid (use 1-65535 as a single port, a comma list or a low-high range, e.g. 443, 80,443 or 1024-65535).";
        return null;
    }

    public static string Format(ExclusionRule r)
    {
        var qualifier = (r.Direction, r.Port) switch
        {
            ("both", null) => "",
            ("both", var p) => "@" + p,
            (var d, null) => "@" + d,
            (var d, var p) => "@" + d + ":" + p,
        };
        return $"{r.Kind}:{r.Pattern}{qualifier}";
    }

    /// <summary>True for a comma list of ports / low-high ranges within 1-65535. Whitespace is not allowed
    /// (Normalized() strips it); an empty item ("80,") is rejected.</summary>
    public static bool IsPortSpec(string spec)
    {
        if (string.IsNullOrEmpty(spec)) return false;
        foreach (var item in spec.Split(','))
        {
            var bounds = item.Split('-');
            if (bounds.Length is < 1 or > 2) return false;
            if (!TryPort(bounds[0], out var low)) return false;
            if (bounds.Length == 2)
            {
                if (!TryPort(bounds[1], out var high) || high < low) return false;
            }
        }
        return true;
    }

    private static bool TryPort(string s, out int port) =>
        int.TryParse(s, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out port) && port is >= 1 and <= 65535;

    /// <summary>A bare '@suffix' is taken as a port only when it contains a digit and nothing but digits, ','
    /// and '-' (with optional spaces); anything else ('alice@corp.local', 'x@-') stays part of the pattern.
    /// A pattern that really ends in '@digits' can be kept by appending '@both'.</summary>
    private static bool LooksLikePortSpec(string s) =>
        s.Any(char.IsDigit) && s.All(c => char.IsDigit(c) || c is ',' or '-' || char.IsWhiteSpace(c));

    private static bool IsCidr(string s)
    {
        var parts = s.Split('/');
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var ip)) return false;
        if (!int.TryParse(parts[1], out var prefix)) return false;
        // IPAddress.TryParse accepts "10.0.0" (partial dotted) — insist on 4 octets for v4.
        if (ip.AddressFamily == AddressFamily.InterNetwork && parts[0].Split('.').Length != 4) return false;
        var max = ip.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;
        return prefix >= 0 && prefix <= max;
    }
}
