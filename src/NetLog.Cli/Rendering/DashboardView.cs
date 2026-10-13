using NetLog.Cli.Tui;
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

/// <summary>The run dashboard, laid out for a <see cref="ScreenSize"/>:
/// title and scope tag are spread to the frame edges; the hosts table is left-packed with content-sized
/// columns and a 4-space gap that shrinks when the frame is tight; the top lists sit in one row of up to
/// three panels (users hidden when three cannot fit); the host list and the top lists are viewports that
/// share the rows the chrome leaves (top lists keep 10 rows while 3 hosts fit, then give way first).</summary>
public static class DashboardView
{
    private const int Label = 13;
    private const int Dir = 3, Count = 6, MinPanelName = 18, TopRows = 10;
    private const int HostGap = 4;

    public static IRenderable Render(DashboardViewState s, TimeZoneInfo tz, ScreenSize size)
    {
        var w = size.Width;
        var r = s.Report;
        var focused = s.FocusedIndex is int fi && fi >= 0 && fi < r.Hosts.Count ? r.Hosts[fi] : null;
        var stats = focused?.Stats ?? r.Stats;
        var chip = focused is null ? "All hosts" : focused.HostName;

        var summary = Summary(r, s, tz, stats, chip, w).ToList();
        var footer = Ui.FooterLines(Footer(onFocusedHost: s.FocusedIndex is not null && s.FocusedIndex == s.CursorIndex, r.ArchiveLevel == "Full"), w);
        var panels = Panels(stats, w);

        // ── height policy ──────────────────────────────────────────────────────────────────────
        var chrome = 1 + 1 + summary.Count + (s.StaleFor is null ? 0 : 2) + 1 + 1 + 1 + 1 + 1 + footer.Count;
        var avail = size.Height == int.MaxValue ? int.MaxValue / 2 : Math.Max(2, size.Height - chrome);
        var hostCount = Math.Max(1, r.Hosts.Count);                       // the "no hosts" row counts as one
        var topCount = Math.Min(TopRows, panels.Shown.Count == 0 ? 0 : panels.Shown.Max(p => p.Items.Count));
        var topRows = topCount;
        var hostRows = Math.Min(hostCount, avail - topRows);
        if (hostRows < Math.Min(hostCount, 3))
        {
            topRows = Math.Min(topCount, Math.Max(3, avail - Math.Min(hostCount, 3)));
            hostRows = Math.Min(hostCount, avail - topRows);
        }
        hostRows = Math.Max(1, hostRows);

        var rows = new List<IRenderable> { new Markup(Header(r, s, w)), Ui.Rule(w) };
        rows.AddRange(summary);

        if (s.StaleFor is { } stale)
        {
            rows.Add(Ui.Rule(w));
            rows.Add(new Markup($"[{Theme.Amber}]!  Share unreachable — showing the last good frame " +
                                $"(read {(int)stale.TotalSeconds}s ago). Retrying…[/]"));
        }

        rows.Add(Ui.Rule(w));
        rows.AddRange(Hosts(r, s, w, Viewport.Of(hostCount, s.CursorIndex, hostRows)));
        rows.Add(Ui.Rule(w));
        rows.AddRange(TopStats(panels, focused?.HostName, w, Viewport.Of(topCount, 0, topRows)));
        rows.Add(Ui.Rule(w));
        rows.AddRange(footer.Select(f => new Markup(f)));
        return new Rows(rows);
    }

    private static string Header(RunStatusReport r, DashboardViewState s, int w)
    {
        var timer = s.StaleFor is { } stale
            ? $"[{Theme.Amber}]! stale {(int)stale.TotalSeconds}s[/]"
            : s.SecondsToRefresh is { } secs
                ? $"[{Theme.Gray}]○ {secs}s[/] [{Theme.Green}]●[/]"
                : "";
        var left = $"[{Theme.Green}]► netlog[/]   [{Theme.White}]{Markup.Escape(r.RunName)}[/]    {Theme.RunStateMarkup(r.State)}";
        var right = $"[{Theme.Text}]{TimeFormat.Duration(r.Elapsed)} elapsed[/]" + (timer.Length == 0 ? "" : "    " + timer);
        return Ui.Spread(left, right, w);
    }

    private static IEnumerable<IRenderable> Summary(RunStatusReport r, DashboardViewState s, TimeZoneInfo tz, RunStats stats, string chip, int w)
    {
        var window = "  " + L("Window", Label, Theme.Gray) +
                     $"[{Theme.Text}]{TimeFormat.Local(r.StartUtc, tz)} → {TimeFormat.Local(r.StopUtc, tz)}[/] " +
                     $"[{Theme.Dim}]{TimeFormat.OffsetLabel(tz, r.StartUtc)}[/]";
        yield return new Markup(Ui.Spread(window, $"[{Theme.Green}][[ {Markup.Escape(chip)} ]][/]", w));

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

    // ── hosts table: content-sized columns, 4-space gap stepping down to 1, then the name is cut ──
    private static IEnumerable<IRenderable> Hosts(RunStatusReport r, DashboardViewState s, int w, Window win)
    {
        var cells = r.Hosts.Select(h => (
            Name: h.HostName,
            Live: $"{Liveness(h.Liveness).glyph} {Liveness(h.Liveness).label}",
            Conns: h.ConnectionCount.ToString("n0"),
            Polls: h.TotalPolls?.ToString("n0") ?? "–",
            Excl: h.ExcludedCount?.ToString("n0") ?? "n/a",
            Hb: h.LastRunUtc is { } hl ? TimeFormat.Ago(hl, s.Now) : "never")).ToList();

        int Longest(Func<(string Name, string Live, string Conns, string Polls, string Excl, string Hb), string> f, int min) =>
            cells.Count == 0 ? min : Math.Max(min, cells.Max(c => f(c).Length));
        var nameW = Longest(c => c.Name, "HOSTS".Length);
        var liveW = Longest(c => c.Live, 0);
        var connsW = Longest(c => c.Conns, "conns".Length);
        var pollsW = Longest(c => c.Polls, "polls".Length);
        var exclW = Longest(c => c.Excl, "excl".Length);
        var hbW = Longest(c => c.Hb, "last heartbeat".Length);

        int Total(int gap, int nw) => 2 + nw + gap + liveW + gap + connsW + gap + pollsW + gap + exclW + gap + hbW;
        var g = HostGap;
        while (Total(g, nameW) > w && g > 1) g--;
        if (Total(g, nameW) > w) nameW = Math.Max(8, nameW - (Total(g, nameW) - w));
        var sp = new string(' ', g);

        yield return new Markup("  " + L("HOSTS", nameW, Theme.Gray) + sp + new string(' ', liveW) + sp +
                                R("conns", connsW, Theme.Gray) + sp + R("polls", pollsW, Theme.Gray) + sp +
                                R("excl", exclW, Theme.Gray) + sp + L("last heartbeat", hbW, Theme.Gray));

        if (r.Hosts.Count == 0)
        {
            yield return new Markup("  " + L("no hosts", nameW, Theme.Dim));
            yield break;
        }

        if (win.Above > 0) yield return new Markup($"  [{Theme.Amber}]▲ {win.Above} more[/]");
        for (int i = win.Start; i < win.Start + win.Count; i++)
        {
            var h = r.Hosts[i];
            var c = cells[i];
            var dim = s.FocusedIndex is int f && f != i;
            var nameColor = dim ? Theme.Dim : Theme.White;
            var cellColor = dim ? Theme.Dim : Theme.Text;
            var liveColor = dim ? Theme.Dim : Liveness(h.Liveness).color;
            var cursor = i == s.CursorIndex ? $"[{Theme.Green}]{Theme.Cursor}[/] " : "  ";

            yield return new Markup(
                cursor +
                $"[{nameColor}]{Markup.Escape(Ui.Fit(c.Name, nameW))}[/]" + sp +
                $"[{liveColor}]{Markup.Escape(c.Live.PadRight(liveW))}[/]" + sp +
                R(c.Conns, connsW, cellColor) + sp + R(c.Polls, pollsW, cellColor) + sp +
                R(c.Excl, exclW, cellColor) + sp + L(c.Hb, hbW, cellColor));
        }
        if (win.Below > 0) yield return new Markup($"  [{Theme.Amber}]▼ {win.Below} more[/]");
    }

    // ── top panels: targets, processes, users in one row ──────────────────────────────────────
    private sealed record Panel(string Title, IReadOnlyList<TopItem> Items, int NameW);
    private sealed record PanelRow(IReadOnlyList<Panel> Shown, bool UsersHidden);

    /// <summary>Each name column starts at its longest entry (floor 18); when the row is wider than the
    /// frame the widest panel shrinks first; when three panels cannot fit at the floor, users are dropped.</summary>
    private static PanelRow Panels(RunStats stats, int w)
    {
        var all = new List<Panel>
        {
            new("TOP TARGETS", stats.TopTargets, 0),
            new("TOP PROCESSES", stats.TopProcesses, 0),
        };
        if (stats.TopUsers.Count > 0) all.Add(new("TOP USERS", stats.TopUsers, 0));

        List<Panel> Sized(List<Panel> panels)
        {
            var widths = panels.Select(p => Math.Max(MinPanelName, p.Items.Count == 0 ? 0 : p.Items.Max(i => i.Name.Length))).ToList();
            int Total() => 2 + widths.Sum(nw => Dir + 1 + nw + Count) + 3 * (widths.Count - 1);
            while (Total() > w && widths.Max() > MinPanelName)
                widths[widths.IndexOf(widths.Max())]--;
            return Total() <= w ? panels.Select((p, i) => p with { NameW = widths[i] }).ToList() : null!;
        }

        var sized = Sized(all);
        if (sized is not null) return new PanelRow(sized, false);
        return new PanelRow(Sized(all.Take(2).ToList()) ?? all.Take(2).Select(p => p with { NameW = MinPanelName }).ToList(), all.Count == 3);
    }

    private static IEnumerable<IRenderable> TopStats(PanelRow panels, string? focusedHost, int w, Window win)
    {
        int PanelW(Panel p) => Dir + 1 + p.NameW + Count;
        // Titles are padded to their panel width except the last, so the line can carry a right-aligned tag.
        var titles = panels.Shown.Select((p, i) => i == panels.Shown.Count - 1
            ? $"[{Theme.Gray}]{Markup.Escape(p.Title)}[/]" : L(p.Title, PanelW(p), Theme.Gray));
        var head = "  " + string.Join("   ", titles);
        var headLen = Markup.Remove(head).Length;
        if (focusedHost is not null)
        {
            var suffix = $"· {focusedHost}";
            if (headLen + 2 + suffix.Length <= w) { head += $"  [{Theme.Dim}]{Markup.Escape(suffix)}[/]"; headLen += 2 + suffix.Length; }
        }
        if (panels.UsersHidden)
        {
            const string tag = "· users hidden";
            if (headLen + 3 + tag.Length <= w) head = Ui.Spread(head, $"[{Theme.Dim}]{tag}[/]", w);
        }
        yield return new Markup(head);

        for (int i = win.Start; i < win.Start + win.Count; i++)
        {
            var cells = panels.Shown.Select(p => i < p.Items.Count
                ? Tag(p.Items[i].Direction) + $"[{Theme.Text}]{Markup.Escape(Ui.FitLabel(p.Items[i].Name, p.NameW))}[/]" +
                  R(p.Items[i].Count.ToString("n0"), Count, Theme.Text)
                : new string(' ', PanelW(p)));
            yield return new Markup("  " + string.Join("   ", cells));
        }
        if (win.Below > 0) yield return new Markup($"  [{Theme.Amber}]▼ {win.Below} more[/]");
    }

    /// <summary>Dim direction tag ("in" / "out" / "io"), fixed width, with a trailing space.</summary>
    private static string Tag(string direction) => $"[{Theme.Gray}]{Markup.Escape(Fit(direction, Dir, false))}[/] ";

    private static (string glyph, string label, string color) Liveness(HostLiveness l) => l switch
    {
        HostLiveness.Live        => ("●", "live", Theme.Green),
        HostLiveness.Stalled     => ("◌", "stalled", Theme.Amber),
        _                        => ("○", "no heartbeat", Theme.Dim),
    };

    private static IEnumerable<string> Footer(bool onFocusedHost, bool showMerge)
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
        return keys;
    }

    // ── fixed-width cell helpers (pad plain text, then colour) ────────────────
    private static string L(string text, int w, string color) => $"[{color}]{Markup.Escape(Fit(text, w, false))}[/]";
    private static string R(string text, int w, string color) => $"[{color}]{Markup.Escape(Fit(text, w, true))}[/]";
    private static string Fit(string s, int w, bool right)
    {
        if (s.Length >= w) return s.Length > w ? s[..w] : s;
        return right ? s.PadLeft(w) : s.PadRight(w);
    }
}
