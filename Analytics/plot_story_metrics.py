"""Charts for the story mode (the V2 day campaign), from CSV exports of its three sheets.

    python Analytics/plot_story_metrics.py --days v2_days.csv --purchases v2_purchases.csv --events v2_events.csv
    python Analytics/plot_story_metrics.py --demo --out-dir mock-charts

Writes up to four PNGs into --out-dir:
  story-days.png         How each day ends: reached dawn (after a night death or not), or the run ended that day, how and where
  story-purchases.png    Purchases per day and item: at the base (--purchases) and with the void currencies (--events)
  story-hole-checks.png  The black hole's timed check per day: passed or failed (time ran out, fell, nightfall), and how close failures came
  story-tutorial.png     Seconds spent on each Day 0 tutorial step, and how many runs passed each step

--days draws the first chart, --purchases the second (with the void purchases when --events is given too), --events the last two.
--demo makes made-up data with a fixed seed (no sheet needed), writes it next to the charts as demo-*.csv in the same columns as
the sheets, and charts it. Use it to preview the charts.

Needs matplotlib for the charts. Parsing and aggregation use only the standard library; the CSV parsing and the chart style come
from plot_beta_metrics.py in this folder.
"""

import argparse
import os
import random
import sys

from plot_beta_metrics import (BAR_COLOR, GRID, INK, INK_2, OTHER_COLOR, SURFACE, _bool, _empty, _figure, _finish, _num, _pct,
                               _plt, _subtitle, _to_text, keep, load_rows, parse_csv_text, write_csv)

# How a day ends, in stack order. Categorical slots 1-6 of the shared palette in their validated order (adjacent pairs CVD
# dE >= 9.1, normal >= 19.6), then the neutral grey for an unknown cause. Three slots are under 3:1 on the surface, so every chart
# carries a legend or direct labels and the script prints the tables.
DAY_ENDS = ["dawn", "dawn_night_death", "base", "died", "won", "left", "unknown"]
DAY_END_LABELS = {
    "dawn": "Reached dawn", "dawn_night_death": "Reached dawn after dying at night", "base": "Run over: base destroyed",
    "died": "Run over: the player died", "won": "Run over: won", "left": "Run over: left the run", "unknown": "Run over: cause unknown",
}
DAY_END_COLORS = {"dawn": "#2a78d6", "dawn_night_death": "#eb6834", "base": "#1baf7a", "died": "#eda100",
                  "won": "#e87ba4", "left": "#008300", "unknown": OTHER_COLOR}
RUN_OVER = DAY_ENDS[2:]
LEFT_CAUSES = {"restart", "quit_to_title"}
END_STAGES = ["tutorial", "day", "dusk", "night", "dawn", "void_trial", "final_boss", "ending", "other"]
END_STAGE_LABELS = {
    "tutorial": "Day 0 (tutorial)", "day": "Morning", "dusk": "Afternoon", "night": "Night", "dawn": "Dawn (at the base)",
    "void_trial": "Void trial", "final_boss": "Final boss", "ending": "Ending", "other": "Other",
}

# The black hole's check, per day, in stack order (same slots as above; grey for a check the run never settled).
HOLE_RESULTS = ["passed", "failed_timeout", "failed_death", "failed_night", "unsettled"]
HOLE_LABELS = {"passed": "Passed", "failed_timeout": "Failed: time ran out", "failed_death": "Failed: the player fell",
               "failed_night": "Failed: nightfall", "unsettled": "Not settled (run over)"}
HOLE_COLORS = {"passed": "#2a78d6", "failed_timeout": "#eb6834", "failed_death": "#1baf7a", "failed_night": "#eda100",
               "unsettled": OTHER_COLOR}
HOLE_FAILURES = HOLE_RESULTS[1:4]

# Day 0's steps in the order they are taught; a step not listed here is drawn after them.
TUTORIAL_STEPS = ["Move", "Fire", "Mine", "Crate", "Sprint", "Fight", "Collect", "Lamp", "DarkMatter"]
TUTORIAL_LABELS = {
    "Move": "1 Walk to the marker", "Fire": "2 Fire the pistol", "Mine": "3 Mine the ore", "Crate": "4 Open the weapon crate",
    "Sprint": "5 Sprint once", "Fight": "6 Defeat the monsters", "Collect": "7 Collect the energy",
    "Lamp": "Night: set a lamp", "DarkMatter": "Night: mine dark matter",
}

ROOMS = ["Control", "Armament", "Energy", "Ship", "Void"]
VOID_PREFIX = "void_"
MAX_ITEM_ROWS = 16
# Sequential blue for counts (shared palette steps 100, 250, 400, 550, 650), light to dark.
SEQUENTIAL = ["#cde2fb", "#86b6ef", "#3987e5", "#1c5cab", "#104281"]
DARK_FILLS = {"#2a78d6", "#008300"}  # white labels read on these; the rest take ink


def _day(row):
    return int(_num(row.get("day"), -1))


def _quantile(sorted_values, q):
    """Linear interpolation between the closest ranks (the spreadsheet PERCENTILE rule); 0 for no values."""
    if not sorted_values:
        return 0.0
    pos = (len(sorted_values) - 1) * q
    lo = int(pos)
    hi = min(lo + 1, len(sorted_values) - 1)
    return sorted_values[lo] + (sorted_values[hi] - sorted_values[lo]) * (pos - lo)


# ---------------------------------------------------------------- aggregation

def day_end(row):
    """How a v2_days row ended: one of DAY_ENDS."""
    if _bool(row.get("reached_dawn")):
        return "dawn_night_death" if _bool(row.get("night_death")) else "dawn"
    cause = row.get("end_cause", "")
    if cause == "base_destroyed":
        return "base"
    if cause == "victory":
        return "won"
    if cause in LEFT_CAUSES:
        return "left"
    if cause in ("", "unknown"):
        return "unknown"
    return "died"


def aggregate_days(rows, include_test=False):
    agg = {"days": {}, "stages": {}, "runs": set(), "rows": 0}
    for row in rows:
        if not keep(row, include_test) or row.get("event") != "v2_day" or _day(row) < 0:
            continue
        end = day_end(row)
        agg["rows"] += 1
        agg["runs"].add(row.get("run_id", ""))
        agg["days"].setdefault(_day(row), {e: 0 for e in DAY_ENDS})[end] += 1
        if end in RUN_OVER:
            stage = row.get("end_stage", "")
            stage = stage if stage in END_STAGES else "other"
            agg["stages"].setdefault(stage, {e: 0 for e in RUN_OVER})[end] += 1
    return agg


def aggregate_purchases(purchase_rows, event_rows=None, include_test=False):
    """Counts per item and day; void purchases (v2_event kind void_purchase) count as item void_<what> in the room Void."""
    agg = {"items": {}, "rooms": {r: 0 for r in ROOMS}, "runs_by_day": {}, "total": 0}

    def add(row, item, room):
        day = _day(row)
        if day < 0 or not item:
            return
        by_day = agg["items"].setdefault(item, {})
        by_day[day] = by_day.get(day, 0) + 1
        agg["rooms"][room] = agg["rooms"].get(room, 0) + 1
        agg["runs_by_day"].setdefault(day, set()).add(row.get("run_id", ""))
        agg["total"] += 1

    for row in purchase_rows or []:
        if keep(row, include_test) and row.get("event") == "v2_purchase":
            add(row, row.get("item", ""), row.get("room", "") or "other")
    for row in event_rows or []:
        if keep(row, include_test) and row.get("event") == "v2_event" and row.get("kind") == "void_purchase":
            what = row.get("detail", "").split(":")[0]
            add(row, VOID_PREFIX + what if what else "", "Void")
    return agg


def aggregate_hole(rows, include_test=False):
    agg = {"days": {}, "failure_shares": {f: [] for f in HOLE_FAILURES}}
    for row in rows:
        if not keep(row, include_test) or row.get("event") != "v2_event" or row.get("kind") != "hole_check" or _day(row) < 0:
            continue
        detail = row.get("detail", "")
        counts = agg["days"].setdefault(_day(row), dict({"started": 0}, **{r: 0 for r in HOLE_RESULTS}))
        if detail == "start":
            counts["started"] += 1
        elif detail in HOLE_RESULTS[:4]:
            counts[detail] += 1
            if detail in HOLE_FAILURES:
                agg["failure_shares"][detail].append(min(1.0, max(0.0, _num(row.get("value"), 0.0))))
    for counts in agg["days"].values():
        counts["unsettled"] = max(0, counts["started"] - sum(counts[r] for r in HOLE_RESULTS[:4]))
    return agg


def aggregate_tutorial(rows, include_test=False):
    agg = {"steps": {}, "runs": {}, "tutorial_runs": set(), "crystal_runs": set()}
    for row in rows:
        if not keep(row, include_test) or row.get("event") != "v2_event":
            continue
        run = row.get("run_id", "")
        if row.get("kind") == "tutorial_step" and row.get("detail"):
            step = row["detail"]
            agg["steps"].setdefault(step, []).append(max(0.0, _num(row.get("value"), 0.0)))
            agg["runs"].setdefault(step, set()).add(run)
            agg["tutorial_runs"].add(run)
        elif row.get("kind") == "void_crystal":
            agg["crystal_runs"].add(run)
    for values in agg["steps"].values():
        values.sort()
    return agg


def tutorial_order(agg):
    """The steps seen, in teaching order, then any other step by name."""
    return [s for s in TUTORIAL_STEPS if s in agg["steps"]] + sorted(s for s in agg["steps"] if s not in TUTORIAL_STEPS)


def item_order(agg):
    """Items by total count, largest first, then by name."""
    totals = {item: sum(days.values()) for item, days in agg["items"].items()}
    return sorted(totals, key=lambda i: (-totals[i], i))


def label_item(item):
    if item.startswith(VOID_PREFIX):
        return "Void: " + item[len(VOID_PREFIX):]
    return item.replace("_", " ")


def crystal_share(agg):
    """Runs that pressed E at the void crystals, of the runs that started the tutorial: (pressed, started)."""
    return len(agg["crystal_runs"] & agg["tutorial_runs"]), len(agg["tutorial_runs"])


# ---------------------------------------------------------------- text tables

def format_days(agg):
    lines = ["Day rows: %d from %d runs" % (agg["rows"], len(agg["runs"])),
             "  %-5s %6s " % ("Day", "Rows") + " ".join("%9s" % e[:9] for e in DAY_ENDS)]
    for day in sorted(agg["days"]):
        c = agg["days"][day]
        lines.append("  %-5d %6d " % (day, sum(c.values())) + " ".join("%9d" % c[e] for e in DAY_ENDS))
    lines.append("  Where the last day ended: " + ", ".join(
        "%s %d" % (s, sum(agg["stages"][s].values())) for s in END_STAGES if s in agg["stages"]))
    return "\n".join(lines)


def format_purchases(agg):
    lines = ["Purchases: %d (rooms: %s)" % (agg["total"], ", ".join("%s %d" % (r, n) for r, n in agg["rooms"].items() if n))]
    days = sorted(agg["runs_by_day"])
    lines.append("  %-22s " % "Item" + " ".join("%5s" % ("D%d" % d) for d in days))
    for item in item_order(agg):
        lines.append("  %-22s " % label_item(item)[:22] + " ".join("%5d" % agg["items"][item].get(d, 0) for d in days))
    return "\n".join(lines)


def format_hole(agg):
    lines = ["Black hole checks", "  %-5s %8s " % ("Day", "Started") + " ".join("%9s" % r[:9] for r in HOLE_RESULTS)]
    for day in sorted(agg["days"]):
        c = agg["days"][day]
        lines.append("  %-5d %8d " % (day, c["started"]) + " ".join("%9d" % c[r] for r in HOLE_RESULTS))
    shares = sorted(v for f in HOLE_FAILURES for v in agg["failure_shares"][f])
    if shares:
        lines.append("  Failed checks reached a median of %.0f%% of the damage asked for (%d failures)"
                     % (100 * _quantile(shares, .5), len(shares)))
    return "\n".join(lines)


def format_tutorial(agg):
    lines = ["Tutorial steps (seconds): %d runs started the tutorial" % len(agg["tutorial_runs"]),
             "  %-26s %6s %8s %8s %8s" % ("Step", "Runs", "Median", "25%", "75%")]
    for step in tutorial_order(agg):
        v = agg["steps"][step]
        lines.append("  %-26s %6d %8.1f %8.1f %8.1f" % (TUTORIAL_LABELS.get(step, step), len(agg["runs"][step]),
                                                      _quantile(v, .5), _quantile(v, .25), _quantile(v, .75)))
    lines.append("  Pressed E at the void crystals: %d of %d runs" % crystal_share(agg))
    return "\n".join(lines)


# ---------------------------------------------------------------- charts

def _legend(ax, keys, labels, colors, ncol):
    from matplotlib.patches import Patch
    handles = [Patch(facecolor=colors[k], edgecolor="none", label=labels[k]) for k in keys]
    ax.legend(handles=handles, loc="lower left", bbox_to_anchor=(0.0, 1.06), ncol=ncol, frameon=False, fontsize=9,
              handlelength=1.1, handleheight=1.1, columnspacing=1.4, labelcolor=INK_2)


def _integer_axis(axis):
    from matplotlib.ticker import MaxNLocator
    axis.set_major_locator(MaxNLocator(integer=True))


def _stacked_columns(ax, x, keys, values, colors):
    """values[key] is a list over x; 2px surface gaps between segments; a count inside each segment tall enough for it.
    Returns the column totals and the tallest one."""
    totals = [sum(values[k][i] for k in keys) for i in range(len(x))]
    top = max(totals or [0]) or 1
    bottoms = [0] * len(x)
    for k in keys:
        heights = values[k]
        ax.bar(x, heights, width=0.66, bottom=bottoms, color=colors[k], edgecolor=SURFACE, linewidth=1.0, zorder=3)
        for xi, h, b in zip(x, heights, bottoms):
            if h and h >= top * 0.07:
                ax.text(xi, b + h / 2, "%d" % h, ha="center", va="center", fontsize=7.5,
                        color=SURFACE if colors[k] in DARK_FILLS else INK, zorder=4)
        bottoms = [b + h for b, h in zip(bottoms, heights)]
    return totals, top


def _day_columns(ax, days, keys, values, colors, labels, note, ylabel):
    """Stacked columns per day, a note over each column (note(index, total)) and the legend above."""
    x = list(range(len(days)))
    shown = [k for k in keys if any(values[k])]
    totals, top = _stacked_columns(ax, x, shown, values, colors)
    for xi, total in zip(x, totals):
        ax.text(xi, total + top * 0.015, note(xi, total), ha="center", va="bottom", fontsize=8, color=INK_2, zorder=4)
    ax.set_xticks(x, ["Day %d" % d for d in days])
    ax.set_xlim(-0.6, len(days) - 0.4)
    ax.set_ylim(0, top * 1.12)
    ax.grid(axis="y", color=GRID, linewidth=0.8, zorder=0)
    _integer_axis(ax.yaxis)
    ax.set_ylabel(ylabel, color=INK_2)
    _legend(ax, shown, labels, colors, 3)


def _count_bars(ax, labels, counts, whole, xlabel):
    """Horizontal count bars labelled 'count (share of whole)'."""
    y = list(range(len(labels)))
    ax.barh(y, counts, height=0.55, color=BAR_COLOR, edgecolor=SURFACE, linewidth=2, zorder=3)
    top = max(counts) or 1
    for yi, n in zip(y, counts):
        ax.text(n + top * 0.03, yi, "%d  (%.0f%%)" % (n, _pct(n, whole)), va="center", fontsize=9, color=INK_2, zorder=4)
    ax.set_yticks(y, labels)
    ax.set_ylim(len(labels) - 0.5, -0.5)
    ax.set_xlim(0, top * 1.4)
    ax.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
    _integer_axis(ax.xaxis)
    ax.set_xlabel(xlabel, color=INK_2)


def render_days(agg, out_path, source):
    plt = _plt()
    fig, (left, right) = _figure(plt, "Story mode: how each day ends (%d runs)" % len(agg["runs"]), [1.6, 1.0])
    left.set_title("Runs that played each day, by how the day ended", loc="left", color=INK, fontweight="semibold")
    days = sorted(agg["days"])
    if not days:
        _empty(left, "No day rows yet")
    else:
        values = {e: [agg["days"][d][e] for d in days] for e in DAY_ENDS}
        _day_columns(left, days, DAY_ENDS, values, DAY_END_COLORS, DAY_END_LABELS,
                     lambda i, total: "%.0f%%" % _pct(values["dawn"][i] + values["dawn_night_death"][i], total), "Runs")

    right.set_title("Where the last day ended", loc="left", color=INK, fontweight="semibold")
    stages = [s for s in END_STAGES if s in agg["stages"]]
    if not stages:
        _empty(right, "No run has ended yet")
    else:
        y = list(range(len(stages)))
        lefts = [0] * len(stages)
        for e in RUN_OVER:
            widths = [agg["stages"][s][e] for s in stages]
            if any(widths):
                right.barh(y, widths, height=0.6, left=lefts, color=DAY_END_COLORS[e], edgecolor=SURFACE, linewidth=1.0, zorder=3)
            lefts = [a + w for a, w in zip(lefts, widths)]
        top = max(lefts) or 1
        for yi, total in zip(y, lefts):
            right.text(total + top * 0.02, yi, "%d" % total, va="center", fontsize=9, color=INK_2, zorder=4)
        right.set_yticks(y, [END_STAGE_LABELS[s] for s in stages])
        right.set_ylim(len(stages) - 0.5, -0.5)
        right.set_xlim(0, top * 1.25)
        right.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
        _integer_axis(right.xaxis)
        right.set_xlabel("Runs that ended there (colours as on the left)", color=INK_2)
    footer = ("Over each column: the share that reached dawn. A day counts from the dawn before it, so a dawn's base visit belongs to "
              "the coming day. Source: %s." % source)
    return _finish(fig, footer, out_path)


def render_purchases(agg, out_path, source):
    plt = _plt()
    from matplotlib.colors import LinearSegmentedColormap
    fig, (left, right) = _figure(plt, "Story mode: purchases per day (%d purchases)" % agg["total"], [2.2, 1.0])
    left.set_title("Times bought, per item and day", loc="left", color=INK, fontweight="semibold")
    days = sorted(agg["runs_by_day"])
    items = item_order(agg)
    if not items:
        _empty(left, "No purchases yet")
    else:
        shown, rest = items[:MAX_ITEM_ROWS], items[MAX_ITEM_ROWS:]
        grid = [[agg["items"][i].get(d, 0) for d in days] for i in shown]
        labels = [label_item(i) for i in shown]
        if rest:
            grid.append([sum(agg["items"][i].get(d, 0) for i in rest) for d in days])
            labels.append("%d more items" % len(rest))
        top = max(max(r) for r in grid) or 1
        cmap = LinearSegmentedColormap.from_list("story_blue", SEQUENTIAL).with_extremes(bad=SURFACE)
        left.imshow([[v if v else float("nan") for v in r] for r in grid], cmap=cmap, vmin=0.5, vmax=max(top, 1.5),
                    aspect="auto", interpolation="nearest", zorder=2)
        for yi, r in enumerate(grid):
            for xi, v in enumerate(r):
                if v:
                    left.text(xi, yi, "%d" % v, ha="center", va="center", fontsize=8, color=SURFACE if v >= top * 0.5 else INK, zorder=4)
        runs = [len(agg["runs_by_day"][d]) for d in days]
        left.set_xticks(range(len(days)), ["Day %d\n%d run%s" % (d, n, "" if n == 1 else "s") for d, n in zip(days, runs)])
        left.set_yticks(range(len(labels)), labels)
        left.tick_params(axis="x", labelsize=8.5)
        for x in range(1, len(days)):  # 2px surface gaps between the cells
            left.axvline(x - 0.5, color=SURFACE, linewidth=2, zorder=3)
        for y in range(1, len(labels)):
            left.axhline(y - 0.5, color=SURFACE, linewidth=2, zorder=3)
        left.spines["bottom"].set_visible(False)

    right.set_title("Purchases by room", loc="left", color=INK, fontweight="semibold")
    rooms = [(r, n) for r, n in agg["rooms"].items() if n]
    if not rooms:
        _empty(right, "No purchases yet")
    else:
        _count_bars(right, [r for r, _ in rooms], [n for _, n in rooms], agg["total"], "Times bought")
    footer = ("Darker cells were bought more often; under each day, the runs that bought anything that day. Void items are bought "
              "with the void currencies (v2_events). Source: %s." % source)
    return _finish(fig, footer, out_path)


def render_hole(agg, out_path, source):
    plt = _plt()
    days = sorted(agg["days"])
    started = sum(agg["days"][d]["started"] for d in days)
    fig, (left, right) = _figure(plt, "Story mode: the black hole's timed check (%d started)" % started, [1.6, 1.0])
    left.set_title("Checks started each day, by result", loc="left", color=INK, fontweight="semibold")
    if not started:
        _empty(left, "No black hole checks yet")
    else:
        values = {r: [agg["days"][d][r] for d in days] for r in HOLE_RESULTS}
        _day_columns(left, days, HOLE_RESULTS, values, HOLE_COLORS, HOLE_LABELS,
                     lambda i, total: "%.0f%% passed" % _pct(values["passed"][i], total), "Checks")

    right.set_title("How close the failed checks came", loc="left", color=INK, fontweight="semibold")
    shares = sorted(v for f in HOLE_FAILURES for v in agg["failure_shares"][f])
    if not shares:
        _empty(right, "No failed checks yet")
    else:
        bins = [0] * 10
        for v in shares:
            bins[min(9, int(v * 10))] += 1
        x = list(range(10))
        right.bar(x, bins, width=0.8, color=BAR_COLOR, edgecolor=SURFACE, linewidth=2, zorder=3)
        top = max(bins) or 1
        for xi, n in zip(x, bins):
            if n:
                right.text(xi, n + top * 0.02, "%d" % n, ha="center", va="bottom", fontsize=8.5, color=INK_2, zorder=4)
        right.set_xticks(x, ["%d-%d" % (10 * i, 10 * i + 10) for i in x])
        right.tick_params(axis="x", labelsize=8)
        right.set_ylim(0, top * 1.15)
        right.grid(axis="y", color=GRID, linewidth=0.8, zorder=0)
        _integer_axis(right.yaxis)
        right.set_xlabel("Damage dealt, % of the damage asked for", color=INK_2)
        _subtitle(fig, "Failed checks reached a median of %.0f%% of the damage asked for (%d failures)."
                  % (100 * _quantile(shares, .5), len(shares)))
    footer = "At most one check a day. 'Not settled': the run ended or the tab closed during the check. Source: %s." % source
    return _finish(fig, footer, out_path)


def render_tutorial(agg, out_path, source):
    plt = _plt()
    steps = tutorial_order(agg)
    fig, (left, right) = _figure(plt, "Story mode: the Day 0 tutorial, step by step (%d runs)" % len(agg["tutorial_runs"]), [1.5, 1.0])
    left.set_title("Seconds per step: median (bar), middle half of runs (line)", loc="left", color=INK, fontweight="semibold")
    right.set_title("Runs that passed each step", loc="left", color=INK, fontweight="semibold")
    if not steps:
        _empty(left, "No tutorial steps yet")
        _empty(right, "No tutorial steps yet")
    else:
        y = list(range(len(steps)))
        med = [_quantile(agg["steps"][s], .5) for s in steps]
        q1 = [_quantile(agg["steps"][s], .25) for s in steps]
        q3 = [_quantile(agg["steps"][s], .75) for s in steps]
        left.barh(y, med, height=0.55, color=BAR_COLOR, edgecolor=SURFACE, linewidth=2, zorder=3)
        left.hlines(y, q1, q3, color=INK_2, linewidth=2, zorder=4)
        top = max(q3 + med) or 1
        for yi, m, a, b in zip(y, med, q1, q3):
            left.text(max(m, b) + top * 0.02, yi, "%.0f s  (%.0f-%.0f)" % (m, a, b), va="center", fontsize=9, color=INK_2, zorder=4)
        left.set_yticks(y, [TUTORIAL_LABELS.get(s, s) for s in steps])
        left.set_ylim(len(steps) - 0.5, -0.5)
        left.set_xlim(0, top * 1.4)
        left.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
        left.set_xlabel("Seconds from the step's start to its pass (real time)", color=INK_2)
        _count_bars(right, [""] * len(steps), [len(agg["runs"][s]) for s in steps], len(agg["tutorial_runs"]),
                    "Runs (share of the runs that started the tutorial)")
        pressed, started = crystal_share(agg)
        _subtitle(fig, "%d of %d runs (%.0f%%) pressed E at the void crystals, which cannot be mined." % (pressed, started, _pct(pressed, started)))
    footer = "Steps 1-7 are Day 0's walk; the two night steps come after the base. Source: %s." % source
    return _finish(fig, footer, out_path)


# ---------------------------------------------------------------- demo data

DEMO_ITEMS = [("repair", "Armament", 30, 0, 0), ("lamp", "Control", 10, 5, 0), ("player_level", "Armament", 60, 20, 0),
              ("weapon_damage", "Armament", 80, 30, 0), ("new_weapon", "Armament", 120, 40, 0), ("constitution", "Energy", 50, 40, 0),
              ("unlock_skill", "Energy", 40, 60, 5), ("room_upgrade", "Control", 150, 50, 10), ("shield", "Control", 40, 20, 0),
              ("ship_part", "Ship", 100, 80, 20), ("fuel", "Ship", 20, 40, 10)]
DEMO_WEAPONS = ["Railgun", "Minigun", "GravGun", "ScatterCannon", "PhaseDagger", "Flamethrower", "CryoCannon", "ArcCoil"]
DEMO_STEP_SECONDS = {"Move": 9, "Fire": 4, "Mine": 22, "Crate": 26, "Sprint": 5, "Fight": 30, "Collect": 8, "Lamp": 18, "DarkMatter": 14}
DEMO_DAYS = 8


def _common(n, event, run_id, session_id):
    return {"received_at": "2026-11-1%d 20:%02d:00" % (n % 9, n % 60), "event": event, "run_id": run_id,
            "session_id": session_id, "edition": "en", "build": "1.0.0+demo"}


def demo_rows(runs=120, seed=526):
    """Made-up rows for the three sheets, plausible enough to judge the charts by."""
    rng = random.Random(seed)
    days_rows, purchases, events = [], [], []

    def event(n, ids, day, stage, kind, detail, value):
        row = _common(n, "v2_event", *ids)
        row.update({"day": day, "stage": stage, "kind": kind, "detail": detail, "value": round(value, 2)})
        events.append(row)

    def day_row(n, ids, day, dawn, night_death, end_stage="", end_cause=""):
        row = _common(n, "v2_day", *ids)
        kills = rng.randint(10, 40) * max(1, day)
        row.update({"day": day, "overtime": "FALSE", "reached_dawn": str(dawn).upper(), "end_stage": end_stage, "end_cause": end_cause,
                    "kills": kills, "player_damage": round(rng.uniform(20, 200), 1), "player_damage_pct": round(rng.uniform(10, 120), 1),
                    "player_hits": rng.randint(2, 30), "base_damage": round(rng.uniform(0, 600) * day, 1),
                    "base_damage_hp": round(rng.uniform(0, 300) * day, 1), "base_hp": round(rng.uniform(300, 1000), 1),
                    "base_shield": 0, "base_max_hp": 1000, "earned_alloy": kills * 3, "earned_energy": kills,
                    "earned_dark": rng.randint(0, 30), "spent_alloy": 0, "spent_energy": 0, "spent_dark": 0,
                    "alloy": rng.randint(0, 200), "energy": rng.randint(0, 80), "dark": rng.randint(0, 30),
                    "night_death": str(night_death).upper(), "lost_alloy": rng.randint(20, 80) if night_death else 0,
                    "lost_energy": 0, "lost_dark": rng.randint(5, 20) if night_death else 0, "shadows": rng.randint(0, 3 * day + 1),
                    "overrun_regions": rng.randint(0, day), "repairs": 0})
        days_rows.append(row)

    for n in range(runs):
        ids = ("%032x" % rng.getrandbits(128), "%032x" % rng.getrandbits(128))
        # Day 0: the tutorial's steps; a few runs stop partway.
        quit_at = rng.choices([None, "Mine", "Crate", "Fight"], weights=[88, 4, 4, 4])[0]
        crystal = rng.random() < 0.35
        for step in TUTORIAL_STEPS:
            if step == quit_at:
                break
            event(n, ids, 0, "night" if step in ("Lamp", "DarkMatter") else "tutorial", "tutorial_step", step,
                  DEMO_STEP_SECONDS[step] * rng.lognormvariate(0, 0.45))
            if step == "Collect":
                if crystal:
                    event(n, ids, 0, "tutorial", "void_crystal", "", 0)
                event(n, ids, 0, "tutorial", "crate", "TutorialSecond:" + rng.choice(DEMO_WEAPONS + ["left"]), 2)
        if quit_at:
            day_row(n, ids, 0, False, False, "tutorial", "quit_to_title")
            continue
        day_row(n, ids, 0, True, False)
        prophet = False
        for day in range(1, DEMO_DAYS + 1):
            # Purchases at dawn (they belong to this day) and through the day.
            for _ in range(rng.randint(2, 6)):
                item, room, a, e, d = rng.choice(DEMO_ITEMS)
                row = _common(n, "v2_purchase", *ids)
                row.update({"day": day, "stage": rng.choice(["dawn", "dawn", "day"]), "item": item,
                            "detail": rng.choice(DEMO_WEAPONS) if "weapon" in item else room, "room": room,
                            "alloy": a, "energy": e, "dark": d})
                purchases.append(row)
            if prophet and rng.random() < 0.6:
                event(n, ids, day, "dawn", "void_purchase", rng.choice(["skill:PocketVoid", "skill:VoidWalk", "enchant:Railgun", "mk:4", "armor"]), 30)
            event(n, ids, day, "dusk", "crate", "Afternoon:" + rng.choice(DEMO_WEAPONS + ["left", "left"]), rng.randint(0, 3))
            # The black hole's check: tried on about half the days, passed more often later.
            if not prophet and rng.random() < 0.55:
                event(n, ids, day, "day", "hole_check", "start", rng.uniform(0.01, 0.08))
                if rng.random() < 0.15 + 0.08 * day:
                    event(n, ids, day, "day", "hole_check", "passed", 1)
                    if rng.random() < 0.55:
                        prophet = True
                        event(n, ids, day, "dusk", "prophet", "", day)
                else:
                    failure = rng.choices(HOLE_FAILURES, weights=[6, 2, 1])[0]
                    event(n, ids, day, "dusk", "hole_check", failure, min(0.99, rng.betavariate(3, 2)))
            # How the day ends.
            roll = rng.random()
            if day == DEMO_DAYS and roll < 0.7:
                day_row(n, ids, day, False, False, "ending", "victory")
                break
            if roll < 0.06 + 0.035 * day:
                cause, stage = rng.choices([("base_destroyed", "night"), ("enemy", "day"), ("status", "dusk"), ("quit_to_title", "dawn")],
                                           weights=[5, 3, 1, 1])[0]
                day_row(n, ids, day, False, False, stage, cause)
                break
            day_row(n, ids, day, True, rng.random() < 0.18)
    return days_rows, purchases, events


# ---------------------------------------------------------------- CLI

def build_parser():
    p = argparse.ArgumentParser(description="Chart the story mode from CSV exports of its sheets.")
    p.add_argument("--days", metavar="CSV", help="'v2_days' export (path or CSV export URL)")
    p.add_argument("--purchases", metavar="CSV", help="'v2_purchases' export (path or CSV export URL)")
    p.add_argument("--events", metavar="CSV", help="'v2_events' export (path or CSV export URL)")
    p.add_argument("--demo", action="store_true", help="chart made-up data instead (writes demo-*.csv too)")
    p.add_argument("--out-dir", default=".", help="folder for the PNGs (default: current folder)")
    p.add_argument("--include-test", action="store_true", help="keep edition = 'test' rows")
    return p


def main(argv=None):
    args = build_parser().parse_args(argv)
    if not args.demo and not (args.days or args.purchases or args.events):
        build_parser().error("give --demo or at least one of --days, --purchases, --events")
    os.makedirs(args.out_dir, exist_ok=True)
    if args.demo:
        days, purchases, events = demo_rows()
        for rows, name in ((days, "v2_days"), (purchases, "v2_purchases"), (events, "v2_events")):
            write_csv(rows, os.path.join(args.out_dir, "demo-%s.csv" % name))
        days, purchases, events = (parse_csv_text(_to_text(r)) for r in (days, purchases, events))
        source = "made-up demo data (seed 526), not real players"
    else:
        days = load_rows(args.days) if args.days else None
        purchases = load_rows(args.purchases) if args.purchases else None
        events = load_rows(args.events) if args.events else None
        source = "sheet export"
    written = []
    if days is not None:
        agg = aggregate_days(days, args.include_test)
        print(format_days(agg) + "\n")
        written.append(render_days(agg, os.path.join(args.out_dir, "story-days.png"), source))
    if purchases is not None:
        agg = aggregate_purchases(purchases, events, args.include_test)
        print(format_purchases(agg) + "\n")
        written.append(render_purchases(agg, os.path.join(args.out_dir, "story-purchases.png"), source))
    if events is not None:
        agg = aggregate_hole(events, args.include_test)
        print(format_hole(agg) + "\n")
        written.append(render_hole(agg, os.path.join(args.out_dir, "story-hole-checks.png"), source))
        agg = aggregate_tutorial(events, args.include_test)
        print(format_tutorial(agg) + "\n")
        written.append(render_tutorial(agg, os.path.join(args.out_dir, "story-tutorial.png"), source))
    for path in written:
        print("Wrote " + path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
