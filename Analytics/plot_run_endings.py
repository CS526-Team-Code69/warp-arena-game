"""Chart how runs end, by wave and cause, from a CSV export of the "runs" sheet.

    python Analytics/plot_run_endings.py --csv runs.csv
    python Analytics/plot_run_endings.py --url "<sheet CSV export URL>" --out run-endings.png

Needs matplotlib for the chart. Parsing and aggregation use only the standard
library, so load_rows(), parse_csv_text() and aggregate() can be used on their own.
"""

import argparse
import csv
import io
import math
import os
import sys
import urllib.request

# Fixed order: table columns, chart stacking and legend.
CAUSES = [
    "enemy", "tyrant", "backfire", "wall_slam", "status", "black_hole", "void_trial", "unknown",
    "victory",
    "restart", "quit_to_title",
]
ABANDON_CAUSES = ("restart", "quit_to_title")
ENEMY_CAUSES = ("enemy", "tyrant")

MAX_WAVE = 14
BOSS = "Boss"
BUCKETS = [str(w) for w in range(1, MAX_WAVE + 1)] + [BOSS]
MAX_ENEMY_BARS = 10  # more enemy kinds than this fold into one "Other" bar

# One colour per cause. Every pair stays at least 11 CIELAB dE76 apart under normal
# vision and under protan/deutan simulation (Machado 2009, severity 1.0). "unknown"
# is a dark grey, victory is green, and the two ways of leaving are light greys.
CAUSE_COLORS = {
    "enemy": "#2a78d6",
    "tyrant": "#eb6834",
    "backfire": "#1baf7a",
    "wall_slam": "#eda100",
    "status": "#e34948",
    "black_hole": "#6250d6",
    "void_trial": "#9c3d8c",
    "unknown": "#5f5e5a",
    "victory": "#008300",
    "restart": "#b1afa8",
    "quit_to_title": "#d6d4cc",
}
CAUSE_LABELS = {
    "enemy": "Enemy",
    "tyrant": "Tyrant (boss)",
    "backfire": "Own weapon",
    "wall_slam": "Wall slam",
    "status": "Burn / poison",
    "black_hole": "Black hole",
    "void_trial": "Void trial",
    "unknown": "Unknown",
    "victory": "Victory",
    "restart": "Restart (left run)",
    "quit_to_title": "Quit to title (left run)",
}

SURFACE = "#fcfcfb"
INK = "#0b0b0b"
INK_2 = "#52514e"
INK_MUTED = "#898781"
GRID = "#e1e0d9"
AXIS = "#c3c2b7"

BOM = "\ufeff"  # byte order mark some spreadsheet exports put before the header
TRUE_TEXT = {"true", "1", "yes", "y", "t"}
TEXT_FIELDS = ("event", "run_id", "session_id", "edition", "build", "outcome", "cause",
               "enemy", "stage", "starting_weapon")
LOWER_FIELDS = ("event", "run_id", "session_id", "edition", "outcome", "cause", "stage")


# ---------------------------------------------------------------- parsing

def _to_bool(value):
    return str(value).strip().lower() in TRUE_TEXT


def _to_number(value, default=0.0):
    try:
        n = float(str(value).strip().replace(",", ""))
    except ValueError:
        return default
    return n if math.isfinite(n) else default  # float() accepts "nan", "inf", "1e400"


def _to_int(value, default=0):
    return int(round(_to_number(value, default)))


def normalize_row(raw):
    """Turn one CSV record (header name -> text) into a typed event dict."""
    row = {}
    for name in TEXT_FIELDS:
        text = (raw.get(name) or "").strip()
        row[name] = text.lower() if name in LOWER_FIELDS else text
    row["received_at"] = (raw.get("received_at") or "").strip()
    row["elite"] = _to_bool(raw.get("elite") or "")
    row["wave"] = _to_int(raw.get("wave") or "")
    row["run_seconds"] = _to_number(raw.get("run_seconds") or "")
    row["level"] = _to_int(raw.get("level") or "")
    row["kills"] = _to_int(raw.get("kills") or "")
    return row


def parse_csv_text(text):
    """Parse CSV text with a header row. Columns are matched by name, in any order."""
    if text.startswith(BOM):
        text = text[1:]
    reader = csv.reader(io.StringIO(text, newline=""))
    header = None
    rows = []
    for record in reader:
        if not any(cell.strip() for cell in record):
            continue  # blank line or a row of empty cells
        if header is None:
            header = [cell.strip().lstrip(BOM).lower() for cell in record]
            if "event" not in header:
                raise ValueError("the CSV has no 'event' column; is this an export of the 'runs' sheet?")
            continue
        raw = {name: (record[i] if i < len(record) else "") for i, name in enumerate(header)}
        event = (raw.get("event") or "").strip().lower()
        if not event or event == "event":
            continue  # empty event or a repeated header row
        rows.append(normalize_row(raw))
    return rows


def load_rows(path):
    """Read a CSV export file and return its typed event rows."""
    with open(path, "r", encoding="utf-8-sig", newline="") as f:
        return parse_csv_text(f.read())


def fetch_rows(url, timeout=30):
    """Download a CSV export URL and return its typed event rows."""
    request = urllib.request.Request(url, headers={"User-Agent": "warp-arena-analytics"})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        text = response.read().decode("utf-8-sig", errors="replace")
    head = text.lstrip()[:200].lower()
    if head.startswith("<!doctype html") or head.startswith("<html"):
        raise ValueError("the URL returned a web page, not CSV. Share the sheet as "
                         "'Anyone with the link: Viewer', or download the CSV and use --csv.")
    return parse_csv_text(text)


# ------------------------------------------------------------ aggregation

def bucket_of(row):
    """Wave bucket label: "1".."14", or "Boss" for the final boss and every victory."""
    if row["stage"] == "final_boss" or row["outcome"] == "victory" or row["cause"] == "victory":
        return BOSS
    return str(min(max(row["wave"], 1), MAX_WAVE))


def cause_of(row):
    if row["outcome"] == "victory":
        return "victory"
    return row["cause"] if row["cause"] in CAUSES else "unknown"


def keep_row(row, include_test=False, edition=None):
    if edition:
        return row["edition"] == edition.lower()
    return include_test or row["edition"] != "test"


def aggregate(rows, include_test=False, edition=None):
    """Count run endings per wave bucket and cause.

    Returns plain dicts and ints:
      counts        {bucket: {cause: n}} for every bucket and cause (zeros included)
      bucket_totals {bucket: n}
      cause_totals  {cause: n}
      enemies       {enemy kind: {"enemy": n, "tyrant": n, "elite": n, "total": n}}
      started       distinct run_id with a run_start row
      ended         run endings counted (one per run_id)
      unfinished    distinct run_id with a run_start row and no run_end row
    """
    counts = {b: {c: 0 for c in CAUSES} for b in BUCKETS}
    enemies = {}
    started_ids = set()
    ended_ids = set()
    ended = 0

    for row in rows:
        if not keep_row(row, include_test, edition):
            continue
        run_id = row["run_id"]
        if row["event"] == "run_start":
            if run_id:
                started_ids.add(run_id)
            continue
        if row["event"] != "run_end":
            continue
        if run_id:
            if run_id in ended_ids:
                continue  # a run ends once; ignore repeats
            ended_ids.add(run_id)
        ended += 1

        cause = cause_of(row)
        counts[bucket_of(row)][cause] += 1
        if cause in ENEMY_CAUSES:
            kind = row["enemy"] or "(not recorded)"
            entry = enemies.setdefault(kind, {"enemy": 0, "tyrant": 0, "elite": 0, "total": 0})
            entry[cause] += 1
            entry["total"] += 1
            if row["elite"]:
                entry["elite"] += 1

    return {
        "counts": counts,
        "bucket_totals": {b: sum(counts[b].values()) for b in BUCKETS},
        "cause_totals": {c: sum(counts[b][c] for b in BUCKETS) for c in CAUSES},
        "enemies": enemies,
        "started": len(started_ids),
        "ended": ended,
        "unfinished": len(started_ids - ended_ids),
    }


def causes_present(agg):
    return [c for c in CAUSES if agg["cause_totals"][c] > 0]


def enemies_sorted(agg):
    """Enemy kinds by count, most first; ties by name."""
    return sorted(agg["enemies"].items(), key=lambda item: (-item[1]["total"], item[0]))


def enemy_bars(agg, limit=MAX_ENEMY_BARS):
    """Enemy kinds for the chart: the top kinds, the rest summed into "Other (n kinds)"."""
    kinds = enemies_sorted(agg)
    if len(kinds) <= limit:
        return kinds
    head, tail = kinds[:limit - 1], kinds[limit - 1:]
    other = {key: sum(entry[key] for _, entry in tail) for key in ("enemy", "tyrant", "elite", "total")}
    return head + [("Other (%d kinds)" % len(tail), other)]


# ---------------------------------------------------------------- summary

def format_summary(agg):
    """Plain-text table of endings per wave bucket and cause."""
    lines = []
    present = causes_present(agg)
    if not present:
        lines.append("No run_end rows yet.")
    else:
        cols = present + ["total"]
        widths = [max(len(c), 3) for c in cols]
        lines.append("Wave  " + "  ".join(c.rjust(w) for c, w in zip(cols, widths)))
        lines.append("-" * len(lines[0]))
        for b in BUCKETS:
            if agg["bucket_totals"][b] == 0:
                continue
            values = [agg["counts"][b][c] for c in present] + [agg["bucket_totals"][b]]
            lines.append(b.ljust(6) + "  ".join(str(v).rjust(w) for v, w in zip(values, widths)))
        lines.append("-" * len(lines[0]))
        totals = [agg["cause_totals"][c] for c in present] + [agg["ended"]]
        lines.append("Total ".ljust(6) + "  ".join(str(v).rjust(w) for v, w in zip(totals, widths)))

    lines.append("")
    lines.append("Runs started: %d   ended: %d   unfinished (closed tab): %d"
                 % (agg["started"], agg["ended"], agg["unfinished"]))

    kinds = enemies_sorted(agg)
    if kinds:
        lines.append("")
        lines.append("Killed by (cause enemy or tyrant):")
        width = max(len(k) for k, _ in kinds)
        digits = max(len(str(entry["total"])) for _, entry in kinds)
        for kind, entry in kinds:
            elite = "  (%d elite)" % entry["elite"] if entry["elite"] else ""
            lines.append("  %s  %s%s" % (kind.ljust(width), str(entry["total"]).rjust(digits), elite))
    return "\n".join(lines)


# --------------------------------------------------------------- chart

def footer_text(agg, source):
    return ("Runs started: %d · ended: %d · unfinished (closed tab): %d · source: %s"
            % (agg["started"], agg["ended"], agg["unfinished"], source))


def _row_major(items, ncol):
    """Reorder legend items so matplotlib's column-by-column fill reads left to right."""
    rows = -(-len(items) // ncol)
    return [items[r * ncol + c] for c in range(ncol) for r in range(rows) if r * ncol + c < len(items)]


def _style_axes(ax):
    ax.set_facecolor(SURFACE)
    for side in ("top", "right", "left"):
        ax.spines[side].set_visible(False)
    ax.spines["bottom"].set_color(AXIS)
    ax.tick_params(colors=INK_MUTED, labelcolor=INK_2, length=0)


def render(agg, out_path, source="sheet export"):
    """Draw the chart to a PNG file."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.patches import Patch
    from matplotlib.ticker import MaxNLocator

    plt.rcParams.update({
        "font.family": ["Segoe UI", "DejaVu Sans", "sans-serif"],
        "font.size": 10,
        "axes.titlesize": 11,
        "axes.labelsize": 10,
    })

    fig = plt.figure(figsize=(13, 6.6), dpi=150, facecolor=SURFACE, layout="constrained")
    grid = fig.add_gridspec(1, 2, width_ratios=[3.0, 1.15])
    ax = fig.add_subplot(grid[0, 0])
    side = fig.add_subplot(grid[0, 1])
    _style_axes(ax)
    _style_axes(side)

    present = causes_present(agg)
    fig.suptitle("How runs end, by wave (%d run%s)" % (agg["ended"], "" if agg["ended"] == 1 else "s"), x=0.01, ha="left",
                 fontsize=14, fontweight="semibold", color=INK)
    x = list(range(len(BUCKETS)))

    if not present:
        ax.set_xticks(x, BUCKETS)
        ax.set_yticks([])
        ax.set_xlim(-0.6, len(BUCKETS) - 0.4)
        ax.text(0.5, 0.5, "No run_end rows yet", transform=ax.transAxes, ha="center",
                va="center", fontsize=13, color=INK_2)
    else:
        bottoms = [0] * len(BUCKETS)
        for cause in present:
            heights = [agg["counts"][b][cause] for b in BUCKETS]
            drawn = [i for i, h in enumerate(heights) if h]
            # A thin edge in the surface colour leaves a gap between stacked segments.
            ax.bar([x[i] for i in drawn], [heights[i] for i in drawn], width=0.62,
                   bottom=[bottoms[i] for i in drawn], color=CAUSE_COLORS[cause],
                   edgecolor=SURFACE, linewidth=1.0, zorder=3)
            bottoms = [b + h for b, h in zip(bottoms, heights)]
        top = max(bottoms)
        for xi, total in zip(x, bottoms):
            if total:
                ax.text(xi, total + top * 0.012, str(total), ha="center", va="bottom",
                        fontsize=9, color=INK_2, zorder=4)
        ax.set_xticks(x, BUCKETS)
        ax.set_xlim(-0.6, len(BUCKETS) - 0.4)
        ax.set_ylim(0, top * 1.08)
        ax.yaxis.set_major_locator(MaxNLocator(integer=True))
        ax.grid(axis="y", color=GRID, linewidth=0.8, zorder=0)
        handles = [Patch(facecolor=CAUSE_COLORS[c], edgecolor="none", label=CAUSE_LABELS[c])
                   for c in present]
        ncol = len(handles) if len(handles) <= 6 else -(-len(handles) // 2)
        ax.legend(handles=_row_major(handles, ncol), loc="lower left", bbox_to_anchor=(0.0, 1.01),
                  ncol=ncol, frameon=False, fontsize=9,
                  handlelength=1.1, handleheight=1.1, columnspacing=1.4, labelcolor=INK_2)
    ax.set_xlabel("Wave", color=INK_2)
    ax.set_ylabel("Runs ended", color=INK_2)

    kinds = enemy_bars(agg)
    side.set_title("Killed by", loc="left", color=INK, fontweight="semibold")
    if not kinds:
        side.set_xticks([])
        side.set_yticks([])
        side.spines["bottom"].set_visible(False)
        side.text(0.5, 0.5, "No enemy deaths yet", transform=side.transAxes, ha="center",
                  va="center", fontsize=10, color=INK_2)
    else:
        names = [k for k, _ in kinds]
        y = list(range(len(kinds)))
        left = [0] * len(kinds)
        for cause in ENEMY_CAUSES:
            widths = [entry[cause] for _, entry in kinds]
            drawn = [i for i, w in enumerate(widths) if w]
            side.barh([y[i] for i in drawn], [widths[i] for i in drawn],
                      left=[left[i] for i in drawn], height=0.62, color=CAUSE_COLORS[cause],
                      edgecolor=SURFACE, linewidth=1.0, zorder=3)
            left = [l + w for l, w in zip(left, widths)]
        most = max(left)
        for yi, (_, entry) in zip(y, kinds):
            label = str(entry["total"])
            if entry["elite"]:
                label += "  (%d elite)" % entry["elite"]
            side.text(entry["total"] + most * 0.03, yi, label, va="center", fontsize=9,
                      color=INK_2, zorder=4)
        side.set_yticks(y, names)
        # Keep room for a full list so a few kinds do not turn into very thick bars.
        side.set_ylim(max(len(kinds), MAX_ENEMY_BARS) - 0.5, -0.5)
        side.set_xlim(0, most * 1.55)
        side.xaxis.set_major_locator(MaxNLocator(integer=True, nbins=4))
        side.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
        side.spines["bottom"].set_visible(False)
        side.set_xlabel("Runs ended", color=INK_2)

    # Leave a strip at the bottom for the footer line.
    fig.get_layout_engine().set(rect=(0, 0.04, 1, 0.945), w_pad=0.25)
    fig.text(0.01, 0.005, footer_text(agg, source), ha="left", va="bottom", fontsize=9,
             color=INK_MUTED)
    fig.savefig(out_path, facecolor=SURFACE)
    plt.close(fig)
    return out_path


# ------------------------------------------------------------------ CLI

def build_parser():
    parser = argparse.ArgumentParser(
        description="Chart how runs end, by wave and cause, from a CSV export of the 'runs' sheet.")
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--csv", metavar="PATH", help="CSV export of the 'runs' sheet")
    source.add_argument("--url", metavar="URL", help="CSV export URL of the 'runs' sheet")
    parser.add_argument("--out", metavar="PATH", default="run-endings.png",
                        help="PNG file to write (default: run-endings.png)")
    parser.add_argument("--include-test", action="store_true",
                        help="keep rows with edition 'test' (left out by default)")
    parser.add_argument("--edition", choices=("en", "zh"), help="only count one edition")
    return parser


def main(argv=None):
    args = build_parser().parse_args(argv)
    try:
        if args.csv:
            rows = load_rows(args.csv)
            source = os.path.basename(args.csv)
        else:
            rows = fetch_rows(args.url)
            source = "sheet export"
    except (OSError, ValueError) as error:
        print("error: %s" % error, file=sys.stderr)
        return 1

    agg = aggregate(rows, include_test=args.include_test, edition=args.edition)
    print(format_summary(agg))
    render(agg, args.out, source)
    print("")
    print("Wrote %s" % args.out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
