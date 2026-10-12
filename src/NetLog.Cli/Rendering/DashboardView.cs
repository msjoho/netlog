using NetLog.Core.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetLog.Cli.Rendering;

/// <summary>Inputs for one dashboard frame. A pure function of these produces the renderable, so the
/// live loop and the static `status` fallback share one renderer and it's unit-testable.</summary>
public sealed record DashboardViewState(
    RunStatusReport Report,
    int CursorIndex,
    int? FocusedIndex,        // null = All hosts (aggregate)
    DateTimeOffset Now,
    int? SecondsToRefresh,    // null in static (non-live) render
    TimeSpan? StaleFor);      // null = fresh; set when the last re-read failed

public static class DashboardView
{
    // Fixed column widths (chars) — hand-spaced to match the mockup and keep the live frame steady.
    private const int Label = 13;
    private const int Name = 20, Live = 15, Conns = 7, Polls = 9, Excl = 7;
    private const int Dir = 3, TgtName = 20, TgtCount = 6, ProcName = 18;

    public static IRenderable Render(DashboardViewState s, TimeZoneInfo tz)
    {
        var r = s.Report;
        var focused = s.FocusedIndex is int fi && fi >= 0 && fi < r.Hosts.Count ? r.Hosts[fi] : null;
        var stats = focused?.Stats ?? r.Stats;
        var chip = focused is null ? "All hosts" : focused.HostName;

        var rows = new List<IRenderable> { new Markup(Header(r, s)), RuleLine() };
        rows.AddRange(Summary(r, s, tz, stats, chip));

        if (s.StaleFor is { } stale)
        {
            rows.Add(RuleLine());
            rows.Add(new Markup($"[{Theme.Amber}]!  Share unreachable — showing the last good frame " +
                                $"(read {(int)stale.TotalSeconds}s ago). Retrying…[/]"));
        }

        rows.Add(RuleLine());
        rows.AddRange(Hosts(r, s));
        rows.Add(RuleLine());
        rows.AddRange(TopStats(stats, focused?.HostName));
        rows.Add(RuleLine());
        rows.Add(new Markup(Footer(onFocusedHost: s.FocusedIndex is not null && s.FocusedIndex == s.CursorIndex, r.ArchiveLevel == "Full")));
        return new Rows(rows);
    }

    private static string Header(RunStatusReport r, DashboardViewState s)
    {
        var right = s.StaleFor is { } stale
            ? $"[{Theme.Amber}]! stale {(int)stale.TotalSeconds}s[/]"
            : s.SecondsToRefresh is { } secs
                ? $"[{Theme.Gray}]○ {secs}s[/] [{Theme.Green}]●[/]"
                : "";
        return $"[{Theme.Green}]► netlog[/]   [{Theme.White}]{Markup.Escape(r.RunName)}[/]    " +
               $"{Theme.RunStateMarkup(r.State)}    [{Theme.Text}]{TimeFormat.Duration(r.Elapsed)} elapsed[/]    {right}";
    }

    private static IEnumerable<IRenderable> Summary(RunStatusReport r, DashboardViewState s, TimeZoneInfo tz, RunStats stats, string chip)
    {
        var window = $"[{Theme.Text}]{TimeFormat.Local(r.StartUtc, tz)} → {TimeFormat.Local(r.StopUtc, tz)}[/] " +
                     $"[{Theme.Dim}]{TimeFormat.OffsetLabel(tz, r.StartUtc)}[/]    [{Theme.Green}][[ {Markup.Escape(chip)} ]][/]";
        yield return new Markup("  " + L("Window", Label, Theme.Gray) + window);

        var conns = $"[{Theme.White}]{Fit(stats.TotalConnections.ToString("n0"), 14, false)}[/]" +
                    $"[{Theme.Text}]in {stats.IncomingCount:n0} / out {stats.OutgoingCount:n0}[/]";
        yield return new Markup("  " + L("Connections", Label, Theme.Gray) + conns);

        if (r.ExclusionRuleCount > 0 || r.ExcludedTotal is not null)
        {
            var dropped = r.ExcludedTotal is { } d ? d.ToString("n0") : "n/a";
            yield return new Markup("  " + L("Exclusions", Label, Theme.Gray) +
                $"[{Theme.Text}]{r.ExclusionRuleCount} rule{(r.ExclusionRuleCount == 1 ? "" : "s")} · {dropped} dropped[/]");
        }

        var last = r.LastUpdateUtc is { } lu
            ? $"[{Theme.Text}]{TimeFormat.Local(lu, tz)}[/]   [{Theme.Gray}]({TimeFormat.Ago(lu, s.Now)})[/]"
            : $"[{Theme.Dim}]none yet[/]";
        yield return new Markup("  " + L("Last update", Label, Theme.Gray) + last);
    }

    private static IEnumerable<IRenderable> Hosts(RunStatusReport r, DashboardViewState s)
    {
        yield return new Markup("  " + L("HOSTS", Name, Theme.Gray) + L("", Live, Theme.Gray) +
                                R("conns", Conns, Theme.Gray) + R("polls", Polls, Theme.Gray) +
                                R("excl", Excl, Theme.Gray) + "  " + L("last heartbeat", 14, Theme.Gray));

        if (r.Hosts.Count == 0)
        {
            yield return new Markup("  " + L("no hosts", Name, Theme.Dim));
            yield break;
        }

        for (int i = 0; i < r.Hosts.Count; i++)
        {
            var h = r.Hosts[i];
            var dim = s.FocusedIndex is int f && f != i;
            var nameColor = dim ? Theme.Dim : Theme.White;
            var cellColor = dim ? Theme.Dim : Theme.Text;
            var (glyph, label, liveColor) = Liveness(h.Liveness);
            if (dim) liveColor = Theme.Dim;

            var cursor = i == s.CursorIndex ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";
            var hb = h.LastRunUtc is { } hl ? TimeFormat.Ago(hl, s.Now) : "never";
            var excl = h.ExcludedCount?.ToString("n0") ?? "n/a";

            yield return new Markup(
                cursor +
                L(h.HostName, Name, nameColor) +
                $"[{liveColor}]{Markup.Escape(Fit($"{glyph} {label}", Live, false))}[/]" +
                R(h.ConnectionCount.ToString("n0"), Conns, cellColor) +
                R(h.TotalPolls?.ToString("n0") ?? "–", Polls, cellColor) +
                R(excl, Excl, cellColor) +
                "  " + L(hb, 14, cellColor));
        }
    }

    private static IEnumerable<IRenderable> TopStats(RunStats stats, string? focusedHost)
    {
        const int leftBlock = Dir + 1 + TgtName + TgtCount;
        var suffix = focusedHost is null ? "" : $"  [{Theme.Dim}]· {Markup.Escape(focusedHost)}[/]";
        yield return new Markup("  " + L("TOP TARGETS", leftBlock, Theme.Gray) + "   " +
                                L("TOP PROCESSES", Dir + 1 + ProcName + TgtCount, Theme.Gray) + suffix);

        int n = Math.Max(stats.TopTargets.Count, stats.TopProcesses.Count);
        for (int i = 0; i < n; i++)
        {
            var t = i < stats.TopTargets.Count ? stats.TopTargets[i] : null;
            var p = i < stats.TopProcesses.Count ? stats.TopProcesses[i] : null;
            var left = t is null
                ? Fit("", leftBlock, false)
                : Tag(t.Direction) + L(t.Name, TgtName, Theme.Text) + R(t.Count.ToString("n0"), TgtCount, Theme.Text);
            var right = p is null
                ? ""
                : Tag(p.Direction) + L(p.Name, ProcName, Theme.Text) + R(p.Count.ToString("n0"), TgtCount, Theme.Text);
            yield return new Markup("  " + left + "   " + right);
        }

        if (stats.TopUsers.Count > 0)
        {
            yield return new Markup("");
            yield return new Markup("  " + L("TOP USERS", leftBlock, Theme.Gray));
            var half = Math.Min(5, stats.TopUsers.Count);
            for (int i = 0; i < half; i++)
            {
                var u = stats.TopUsers[i];
                var left = Tag(u.Direction) + L(u.Name, TgtName, Theme.Text) + R(u.Count.ToString("n0"), TgtCount, Theme.Text);
                var j = i + 5;
                var right = j < stats.TopUsers.Count
                    ? Tag(stats.TopUsers[j].Direction) + L(stats.TopUsers[j].Name, ProcName, Theme.Text) +
                      R(stats.TopUsers[j].Count.ToString("n0"), TgtCount, Theme.Text)
                    : "";
                yield return new Markup("  " + left + "   " + right);
            }
        }
    }

    /// <summary>Dim direction tag ("in" / "out" / "io"), fixed width, with a trailing space.</summary>
    private static string Tag(string direction) => $"[{Theme.Gray}]{Markup.Escape(Fit(direction, Dir, false))}[/] ";

    private static (string glyph, string label, string color) Liveness(HostLiveness l) => l switch
    {
        HostLiveness.Live        => ("●", "live", Theme.Green),
        HostLiveness.Stalled     => ("◌", "stalled", Theme.Amber),
        _                        => ("○", "no heartbeat", Theme.Dim),
    };

    private static string Footer(bool onFocusedHost, bool showMerge)
    {
        string k(string key, string label) =>
            $"[{Theme.Gray}]{Markup.Escape("[")}[/][{Theme.Green}]{key}[/][{Theme.Gray}]{Markup.Escape("]")} {label}[/]";
        var keys = new List<string>
        {
            // "unfocus" only when the cursor sits on the focused host — anywhere else enter focuses.
            k("↑↓", "host"), k("enter", onFocusedHost ? "unfocus" : "focus"), k("a", "all"), k("s", "deploy"),
            k("x", "destroy"), k("n", "new window"), k("e", "edit"),
        };
        if (showMerge) keys.Add(k("m", "merge"));
        keys.Add(k("r", "refresh"));
        keys.Add(k("q", "back"));
        return string.Join("  ", keys);
    }

    // ── fixed-width cell helpers (pad plain text, then colour) ────────────────
    private static string L(string text, int w, string color) => $"[{color}]{Markup.Escape(Fit(text, w, false))}[/]";
    private static string R(string text, int w, string color) => $"[{color}]{Markup.Escape(Fit(text, w, true))}[/]";
    private static string Fit(string s, int w, bool right)
    {
        if (s.Length >= w) return s.Length > w ? s[..w] : s;
        return right ? s.PadLeft(w) : s.PadRight(w);
    }

    private static IRenderable RuleLine() => Ui.Rule();
}
