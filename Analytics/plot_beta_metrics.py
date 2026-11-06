"""Charts for the three beta metrics, from CSV exports of their sheets.

    python Analytics/plot_beta_metrics.py --upgrades upgrade_picks.csv --supply supply_picks.csv --damage damage.csv
    python Analytics/plot_beta_metrics.py --demo --out-dir mock-charts

Writes up to three PNGs into --out-dir:
  upgrade-picks.png   Metric: weapon upgrade pick rate at level-up, by direction and by card type
  supply-picks.png    Metric: what players do with a supply drop, and which weapons they replace
  damage-sources.png  Metric: damage taken by source (enemy kind, own weapon, wall), overall and by wave

--demo makes made-up data with a fixed seed (no sheet needed), writes it next to the charts as
demo-*.csv in the same columns as the sheets, and charts it. Use it to preview the charts.

Needs matplotlib for the charts. Parsing and aggregation use only the standard library.
"""

import argparse
import csv
import io
import os
import random
import sys
import urllib.request

AXES = ["Power", "Tempo", "Reach", "Infusion", "Twist", "Tame"]
AXIS_LABELS = {
    "Power": "Power (damage)", "Tempo": "Tempo (cooldown)", "Reach": "Reach (range / area)",
    "Infusion": "Infusion (element)", "Twist": "Twist", "Tame": "Tame",
}
CARD_KINDS = ["Weapon", "Skill", "Stat"]
ACTIONS = ["take_free", "merge", "replace", "skip", "timeout"]
ACTION_LABELS = {
    "take_free": "Took it into a free slot", "merge": "Merged (higher Mk)", "replace": "Replaced a weapon",
    "skip": "Skipped", "timeout": "Ran out of time",
}
BOSS_BUCKET = "W15 Boss"
BUCKETS = ["W%02d" % w for w in range(1, 15)] + [BOSS_BUCKET]
BUCKET_TICKS = [str(w) for w in range(1, 15)] + ["Boss"]
SOURCE_LABELS = {"backfire": "Own weapon", "wall_slam": "Wall slam", "other": "Other / unknown"}
MAX_SOURCE_BARS = 12
MAX_WEAPON_BARS = 10

# Same palette as plot_run_endings.py. Own weapon, wall slam and the Tyrant keep the colours they
# have there; the four enemy kinds that hurt most take the remaining slots, in this fixed stack order.
# Validated as adjacent pairs (CVD dE >= 9.1, normal >= 19.6); three slots are under 3:1 on the
# surface, so every chart carries a legend or direct labels and the script prints the tables.
FIXED_SOURCE_COLORS = {"Tyrant": "#eb6834", "backfire": "#1baf7a", "wall_slam": "#eda100"}
ENEMY_SLOTS = ["#2a78d6", "#e87ba4", "#6250d6", "#e34948"]
STACK_ORDER = ["enemy1", "Tyrant", "backfire", "wall_slam", "enemy2", "enemy3", "enemy4", "Other"]
OTHER_COLOR = "#b1afa8"
DARK_FILLS = {"#2a78d6", "#eb6834", "#6250d6", "#e34948"}  # white labels read on these; the rest take ink
BAR_COLOR = "#2a78d6"

SURFACE = "#fcfcfb"
INK = "#0b0b0b"
INK_2 = "#52514e"
INK_MUTED = "#898781"
GRID = "#e1e0d9"
AXIS = "#c3c2b7"

BOM = "﻿"
TRUE_TEXT = {"true", "1", "yes", "y", "t"}


# ---------------------------------------------------------------- parsing

def _num(value, default=0.0):
    try:
        return float(str(value).strip().replace(",", ""))
    except (TypeError, ValueError):
        return default


def _bool(value):
    return str(value).strip().lower() in TRUE_TEXT


def parse_csv_text(text):
    """Rows as dicts with stripped keys and values; a leading byte order mark is dropped."""
    if text.startswith(BOM):
        text = text[len(BOM):]
    reader = csv.DictReader(io.StringIO(text))
    rows = []
    for raw in reader:
        rows.append({(k or "").strip(): (v or "").strip() for k, v in raw.items() if k is not None})
    return rows


def load_rows(source):
    """A path or an http(s) CSV export URL."""
    if source.startswith("http://") or source.startswith("https://"):
        with urllib.request.urlopen(source, timeout=30) as resp:
            return parse_csv_text(resp.read().decode("utf-8", errors="replace"))
    with open(source, encoding="utf-8", newline="") as f:
        return parse_csv_text(f.read())


def keep(row, include_test=False):
    return include_test or row.get("edition", "").lower() != "test"


# ---------------------------------------------------------------- aggregation

def card_kind(card_id):
    kind = card_id.split(":")[0]
    return kind if kind in CARD_KINDS else "other"


def card_axis(card_id):
    parts = card_id.split(":")
    return parts[2] if len(parts) >= 3 and parts[0] == "Weapon" else ""


def aggregate_upgrades(rows, include_test=False):
    agg = {
        "picks": 0,
        "axis": {a: {"offered": 0, "picked": 0} for a in AXES},
        "kind": {k: {"offered": 0, "picked": 0} for k in CARD_KINDS},
        "weapon_on_offer": 0, "weapon_taken": 0,
        "context": {"levelup": 0, "build": 0},
    }
    for row in rows:
        if not keep(row, include_test) or row.get("event") != "upgrade_pick":
            continue
        offers = [row.get("offer_%d" % i, "") for i in (1, 2, 3)]
        picked = row.get("picked", "")
        if not picked:
            continue
        agg["picks"] += 1
        if row.get("context") in agg["context"]:
            agg["context"][row["context"]] += 1
        for o in offers:
            if card_kind(o) in agg["kind"]:
                agg["kind"][card_kind(o)]["offered"] += 1
            if card_axis(o) in agg["axis"]:
                agg["axis"][card_axis(o)]["offered"] += 1
        if card_kind(picked) in agg["kind"]:
            agg["kind"][card_kind(picked)]["picked"] += 1
        if card_axis(picked) in agg["axis"]:
            agg["axis"][card_axis(picked)]["picked"] += 1
        if any(card_kind(o) == "Weapon" for o in offers):
            agg["weapon_on_offer"] += 1
            if card_kind(picked) == "Weapon":
                agg["weapon_taken"] += 1
    return agg


def aggregate_supply(rows, include_test=False):
    agg = {"drops": 0, "actions": {a: 0 for a in ACTIONS}, "replaced": {}, "taken": {}, "cancels": 0}
    for row in rows:
        if not keep(row, include_test) or row.get("event") != "supply_pick":
            continue
        action = row.get("action", "")
        if action not in agg["actions"]:
            continue
        agg["drops"] += 1
        agg["actions"][action] += 1
        agg["cancels"] += int(_num(row.get("replace_cancels"), 0))
        if action == "replace" and row.get("replaced"):
            agg["replaced"][row["replaced"]] = agg["replaced"].get(row["replaced"], 0) + 1
        if row.get("picked"):
            agg["taken"][row["picked"]] = agg["taken"].get(row["picked"], 0) + 1
    return agg


def base_kind(source):
    return source[:-len("_elite")] if source.endswith("_elite") else source


def aggregate_damage(rows, include_test=False):
    agg = {"total": 0.0, "sources": {}, "dot": {}, "by_bucket": {b: {} for b in BUCKETS}, "runs": set()}
    for row in rows:
        if not keep(row, include_test) or row.get("event") != "wave_damage":
            continue
        source = row.get("source") or "other"
        pct = _num(row.get("damage_pct"), 0.0)
        if pct <= 0:
            continue
        agg["runs"].add(row.get("run_id", ""))
        agg["total"] += pct
        agg["sources"][source] = agg["sources"].get(source, 0.0) + pct
        if _bool(row.get("dot")):
            agg["dot"][source] = agg["dot"].get(source, 0.0) + pct
        bucket = row.get("bucket", "")
        if bucket in agg["by_bucket"]:
            kind = base_kind(source)
            agg["by_bucket"][bucket][kind] = agg["by_bucket"][bucket].get(kind, 0.0) + pct
    return agg


def damage_groups(agg):
    """Colour group per base source: fixed ones, the four enemy kinds that hurt most, then Other."""
    totals = {}
    for source, pct in agg["sources"].items():
        totals[base_kind(source)] = totals.get(base_kind(source), 0.0) + pct
    enemies = sorted((k for k in totals if k not in FIXED_SOURCE_COLORS and k != "other"),
                     key=lambda k: (-totals[k], k))
    groups, colors = {}, {}
    for k in totals:
        groups[k] = k if k in FIXED_SOURCE_COLORS else "Other"
    for i, k in enumerate(enemies[:len(ENEMY_SLOTS)]):
        groups[k] = "enemy%d" % (i + 1)
    for k, g in groups.items():
        colors[k] = FIXED_SOURCE_COLORS.get(k) or (ENEMY_SLOTS[int(g[-1]) - 1] if g.startswith("enemy") else OTHER_COLOR)
    return groups, colors


def label_of(source):
    if source.endswith("_elite"):
        return label_of(base_kind(source)) + " (elite)"
    return SOURCE_LABELS.get(source, source)


# ---------------------------------------------------------------- text tables

def _pct(part, whole):
    return 100.0 * part / whole if whole else 0.0


def format_upgrades(agg):
    lines = ["Upgrade picks: %d (level-up %d, build phase %d)" % (agg["picks"], agg["context"]["levelup"], agg["context"]["build"]),
             "  %-22s %8s %8s %9s" % ("Direction", "Offered", "Picked", "Rate")]
    for a in AXES:
        e = agg["axis"][a]
        lines.append("  %-22s %8d %8d %8.1f%%" % (AXIS_LABELS[a], e["offered"], e["picked"], _pct(e["picked"], e["offered"])))
    lines.append("  %-22s %8s %8s %9s" % ("Card type", "Offered", "Picked", "Rate"))
    for k in CARD_KINDS:
        e = agg["kind"][k]
        lines.append("  %-22s %8d %8d %8.1f%%" % (k, e["offered"], e["picked"], _pct(e["picked"], e["offered"])))
    lines.append("  Weapon upgrade on offer in %d picks; taken in %d (%.1f%%)" % (
        agg["weapon_on_offer"], agg["weapon_taken"], _pct(agg["weapon_taken"], agg["weapon_on_offer"])))
    return "\n".join(lines)


def format_supply(agg):
    lines = ["Supply drops: %d (replace screen cancelled %d times)" % (agg["drops"], agg["cancels"])]
    for a in ACTIONS:
        lines.append("  %-26s %6d %8.1f%%" % (ACTION_LABELS[a], agg["actions"][a], _pct(agg["actions"][a], agg["drops"])))
    lines.append("  Replaced most often: " + ", ".join("%s %d" % kv for kv in sorted(agg["replaced"].items(), key=lambda kv: (-kv[1], kv[0]))[:MAX_WEAPON_BARS]))
    return "\n".join(lines)


def format_damage(agg):
    lines = ["Damage taken: %.0f%% of max HP in total over %d runs" % (agg["total"], len(agg["runs"])),
             "  %-24s %9s %8s %10s" % ("Source", "% max HP", "Share", "Burn/poi.")]
    for source, pct in sorted(agg["sources"].items(), key=lambda kv: (-kv[1], kv[0])):
        lines.append("  %-24s %9.0f %7.1f%% %10.0f" % (label_of(source), pct, _pct(pct, agg["total"]), agg["dot"].get(source, 0.0)))
    return "\n".join(lines)


# ---------------------------------------------------------------- charts

def _plt():
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    plt.rcParams.update({"font.family": ["Segoe UI", "DejaVu Sans", "sans-serif"], "font.size": 10,
                         "axes.titlesize": 11, "axes.labelsize": 10})
    return plt


def _style(ax):
    ax.set_facecolor(SURFACE)
    for side in ("top", "right", "left"):
        ax.spines[side].set_visible(False)
    ax.spines["bottom"].set_color(AXIS)
    ax.tick_params(colors=INK_MUTED, labelcolor=INK_2, length=0)


def _empty(ax, text):
    ax.set_xticks([])
    ax.set_yticks([])
    ax.spines["bottom"].set_visible(False)
    ax.text(0.5, 0.5, text, transform=ax.transAxes, ha="center", va="center", fontsize=11, color=INK_2)


def _figure(plt, title, ratios):
    fig = plt.figure(figsize=(13, 6.2), dpi=150, facecolor=SURFACE, layout="constrained")
    grid = fig.add_gridspec(1, len(ratios), width_ratios=ratios)
    axes = [fig.add_subplot(grid[0, i]) for i in range(len(ratios))]
    for ax in axes:
        _style(ax)
    fig.suptitle(title, x=0.01, ha="left", fontsize=14, fontweight="semibold", color=INK)
    return fig, axes


def _subtitle(fig, text):
    fig.text(0.01, 0.925, text, ha="left", va="top", fontsize=10.5, color=INK_2)
    fig._has_subtitle = True


def _finish(fig, footer, out_path):
    top = 0.9 if getattr(fig, "_has_subtitle", False) else 0.945
    fig.get_layout_engine().set(rect=(0, 0.04, 1, top), w_pad=0.3)
    fig.text(0.01, 0.005, footer, ha="left", va="bottom", fontsize=9, color=INK_MUTED)
    fig.savefig(out_path, facecolor=SURFACE)
    import matplotlib.pyplot as plt
    plt.close(fig)
    return out_path


def _rate_bars(ax, labels, picked, offered, title):
    """Horizontal pick-rate bars, labelled 'rate (picked / offered)'."""
    ax.set_title(title, loc="left", color=INK, fontweight="semibold")
    if not any(offered):
        _empty(ax, "No picks yet")
        return
    rates = [_pct(p, o) for p, o in zip(picked, offered)]
    y = list(range(len(labels)))
    ax.barh(y, rates, height=0.6, color=BAR_COLOR, edgecolor=SURFACE, linewidth=2, zorder=3)
    top = max(rates) or 1
    for yi, r, p, o in zip(y, rates, picked, offered):
        ax.text(r + top * 0.02, yi, "%.0f%%  (%d / %d)" % (r, p, o), va="center", fontsize=9, color=INK_2, zorder=4)
    ax.set_yticks(y, labels)
    ax.set_ylim(len(labels) - 0.5, -0.5)
    ax.set_xlim(0, top * 1.45)
    ax.xaxis.set_major_formatter(lambda v, _: "%d%%" % v)
    ax.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
    ax.set_xlabel("Picked when offered", color=INK_2)


def render_upgrades(agg, out_path, source):
    plt = _plt()
    fig, (left, right) = _figure(plt, "Level-up picks: how often each weapon upgrade is chosen (%d picks)" % agg["picks"], [1.5, 1.0])
    _rate_bars(left, [AXIS_LABELS[a] for a in AXES], [agg["axis"][a]["picked"] for a in AXES],
               [agg["axis"][a]["offered"] for a in AXES], "Weapon upgrade cards, by direction")
    _rate_bars(right, CARD_KINDS, [agg["kind"][k]["picked"] for k in CARD_KINDS],
               [agg["kind"][k]["offered"] for k in CARD_KINDS], "All cards, by type")
    if agg["weapon_on_offer"]:
        _subtitle(fig, "When at least one weapon upgrade was on offer, a weapon upgrade was taken %.0f%% of the time (%d / %d)."
                  % (_pct(agg["weapon_taken"], agg["weapon_on_offer"]), agg["weapon_taken"], agg["weapon_on_offer"]))
    footer = "Level-up (mid-wave) and build-phase drafts, %d and %d picks; void-trial rewards not counted. Source: %s." % (
        agg["context"]["levelup"], agg["context"]["build"], source)
    return _finish(fig, footer, out_path)


def render_supply(agg, out_path, source):
    plt = _plt()
    fig, (left, right) = _figure(plt, "Supply drops: what players do with the three weapons (%d drops)" % agg["drops"], [1.4, 1.0])
    left.set_title("Choice", loc="left", color=INK, fontweight="semibold")
    if not agg["drops"]:
        _empty(left, "No supply drops yet")
    else:
        shares = [_pct(agg["actions"][a], agg["drops"]) for a in ACTIONS]
        y = list(range(len(ACTIONS)))
        left.barh(y, shares, height=0.6, color=BAR_COLOR, edgecolor=SURFACE, linewidth=2, zorder=3)
        top = max(shares) or 1
        for yi, s, a in zip(y, shares, ACTIONS):
            left.text(s + top * 0.02, yi, "%.0f%%  (%d)" % (s, agg["actions"][a]), va="center", fontsize=9, color=INK_2, zorder=4)
        left.set_yticks(y, [ACTION_LABELS[a] for a in ACTIONS])
        left.set_ylim(len(ACTIONS) - 0.5, -0.5)
        left.set_xlim(0, top * 1.35)
        left.xaxis.set_major_formatter(lambda v, _: "%d%%" % v)
        left.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
        left.set_xlabel("Share of supply drops", color=INK_2)

    right.set_title("Weapons replaced most often", loc="left", color=INK, fontweight="semibold")
    ranked = sorted(agg["replaced"].items(), key=lambda kv: (-kv[1], kv[0]))[:MAX_WEAPON_BARS]
    if not ranked:
        _empty(right, "No replacements yet")
    else:
        y = list(range(len(ranked)))
        counts = [c for _, c in ranked]
        right.barh(y, counts, height=0.6, color=BAR_COLOR, edgecolor=SURFACE, linewidth=2, zorder=3)
        total = sum(agg["replaced"].values())
        for yi, c in zip(y, counts):
            right.text(c + max(counts) * 0.03, yi, "%d  (%.0f%%)" % (c, _pct(c, total)), va="center", fontsize=9, color=INK_2, zorder=4)
        right.set_yticks(y, [w for w, _ in ranked])
        right.set_ylim(max(len(ranked), MAX_WEAPON_BARS) - 0.5, -0.5)
        right.set_xlim(0, max(counts) * 1.4)
        right.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
        from matplotlib.ticker import MaxNLocator
        right.xaxis.set_major_locator(MaxNLocator(integer=True))
        right.set_xlabel("Times replaced", color=INK_2)
    footer = "Replace screen opened and cancelled %d times. Source: %s." % (agg["cancels"], source)
    return _finish(fig, footer, out_path)


def render_damage(agg, out_path, source):
    plt = _plt()
    from matplotlib.patches import Patch
    fig, (left, right) = _figure(plt, "Where the player's damage comes from (%d runs)" % len(agg["runs"]), [1.0, 1.6])
    groups, colors = damage_groups(agg)

    left.set_title("All damage taken, by source", loc="left", color=INK, fontweight="semibold")
    ranked = sorted(agg["sources"].items(), key=lambda kv: (-kv[1], kv[0]))
    if not ranked:
        _empty(left, "No damage rows yet")
    else:
        shown = ranked[:MAX_SOURCE_BARS]
        rest = sum(p for _, p in ranked[MAX_SOURCE_BARS:])
        labels = [label_of(s) for s, _ in shown] + (["%d more sources" % (len(ranked) - MAX_SOURCE_BARS)] if rest else [])
        values = [_pct(p, agg["total"]) for _, p in shown] + ([_pct(rest, agg["total"])] if rest else [])
        bar_colors = [colors[base_kind(s)] for s, _ in shown] + ([OTHER_COLOR] if rest else [])
        y = list(range(len(values)))
        left.barh(y, values, height=0.62, color=bar_colors, edgecolor=SURFACE, linewidth=2, zorder=3)
        top = max(values)
        for yi, v, (s, p) in zip(y, values, shown + ([("", rest)] if rest else [])):
            dot = agg["dot"].get(s, 0.0)
            extra = "  · %.0f%% burn/poison" % _pct(dot, p) if dot else ""
            left.text(v + top * 0.02, yi, "%.0f%%%s" % (v, extra), va="center", fontsize=9, color=INK_2, zorder=4)
        left.set_yticks(y, labels)
        left.set_ylim(max(len(values), MAX_SOURCE_BARS) - 0.5, -0.5)
        left.set_xlim(0, top * 1.7)
        left.xaxis.set_major_formatter(lambda v, _: "%d%%" % v)
        left.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
        left.set_xlabel("Share of all damage taken", color=INK_2)

    right.set_title("Share of damage per wave (elites counted with their kind)", loc="left", color=INK, fontweight="semibold")
    x = list(range(len(BUCKETS)))
    totals = [sum(agg["by_bucket"][b].values()) for b in BUCKETS]
    if not any(totals):
        _empty(right, "No damage rows yet")
    else:
        names = {}
        for kind, g in groups.items():
            names.setdefault(g, []).append(kind)
        bottoms = [0.0] * len(BUCKETS)
        handles = []
        for g in STACK_ORDER:
            kinds = names.get(g, [])
            if not kinds:
                continue
            heights = [_pct(sum(agg["by_bucket"][b].get(k, 0.0) for k in kinds), t) for b, t in zip(BUCKETS, totals)]
            color = colors[kinds[0]] if g != "Other" else OTHER_COLOR
            right.bar(x, heights, width=0.66, bottom=bottoms, color=color, edgecolor=SURFACE, linewidth=1.0, zorder=3)
            for xi, h, b in zip(x, heights, bottoms):
                if h >= 12:
                    right.text(xi, b + h / 2, "%.0f" % h, ha="center", va="center", fontsize=7.5,
                               color=SURFACE if color in DARK_FILLS else INK, zorder=4)
            bottoms = [b + h for b, h in zip(bottoms, heights)]
            label = "Other enemies" if g == "Other" else label_of(kinds[0])
            handles.append(Patch(facecolor=color, edgecolor="none", label=label))
        right.set_xticks(x, BUCKET_TICKS)
        right.set_xlim(-0.6, len(BUCKETS) - 0.4)
        right.set_ylim(0, 100)
        right.yaxis.set_major_formatter(lambda v, _: "%d%%" % v)
        right.grid(axis="y", color=GRID, linewidth=0.8, zorder=0)
        right.set_xlabel("Wave", color=INK_2)
        right.legend(handles=handles, loc="lower left", bbox_to_anchor=(0.0, 1.06), ncol=4, frameon=False, fontsize=9,
                     handlelength=1.1, handleheight=1.1, columnspacing=1.4, labelcolor=INK_2)
    footer = "Damage summed as %% of the player's max HP at each hit; burn and poison count for the enemy that applied them. Source: %s." % source
    return _finish(fig, footer, out_path)


# ---------------------------------------------------------------- demo data

DEMO_ENEMIES = {"Grunt": 9, "Brute": 7, "Skitter": 6, "Spitter": 6, "Rammer": 4, "Bloater": 3, "Warden": 3,
                "Splitter": 3, "Spire": 2, "Stomper": 3, "Shade": 2, "Chanter": 2}
DEMO_WEAPONS = ["Rifle", "Knife", "RocketLauncher", "GravGun", "ScatterCannon", "PhaseDagger", "PlasmaLauncher",
                "Flamethrower", "CryoCannon", "ArcCoil", "SporeLauncher", "Railgun", "Minigun", "SwarmLauncher",
                "OrbitBlades", "SentryDrone", "ShieldRing"]
DEMO_SKILLS = ["FlameWake", "Contagion", "FrostNova", "StormCall", "Bulwark", "Radiance", "Devour", "Blink",
               "Shockwave", "GravityWell", "TimeSlip", "Decoy", "Overcharge"]
DEMO_STATS = ["Damage", "MaxHp", "MoveSpeed", "CritChance", "Cooldown", "Pickup"]


def _common(rng_id, event, run_id, session_id):
    return {"received_at": "2026-11-0%d 20:%02d:00" % (rng_id % 6 + 1, rng_id % 60), "event": event, "run_id": run_id,
            "session_id": session_id, "edition": "en", "build": "1.0.0+demo"}


def demo_rows(runs=150, seed=526):
    """Made-up rows for all three sheets, plausible enough to judge the charts by."""
    rng = random.Random(seed)
    up, sup, dmg = [], [], []
    axis_offer = {"Power": 25, "Tempo": 20, "Reach": 18, "Infusion": 15, "Twist": 12, "Tame": 10}
    axis_pull = {"Power": 1.7, "Tempo": 1.3, "Reach": 0.8, "Infusion": 1.1, "Twist": 0.7, "Tame": 0.45}
    for n in range(runs):
        run_id = "%032x" % rng.getrandbits(128)
        session_id = "%032x" % rng.getrandbits(128)
        last_wave = min(15, max(1, int(rng.triangular(1, 16, 6))))
        hand = [rng.choice(["Rifle", "Knife", "RocketLauncher"])]
        for wave in range(1, min(last_wave, 14) + 1):
            # Level-up drafts.
            for _ in range(rng.choice([0, 1, 1, 2])):
                offers = []
                for _ in range(3):
                    k = rng.choices(CARD_KINDS, weights=[45, 30, 25])[0]
                    if k == "Weapon":
                        a = rng.choices(AXES, weights=[axis_offer[a] for a in AXES])[0]
                        offers.append("Weapon:%s:%s" % (rng.choice(hand), a))
                    elif k == "Skill":
                        offers.append("Skill:" + rng.choice(DEMO_SKILLS))
                    else:
                        offers.append("Stat:" + rng.choice(DEMO_STATS))
                pulls = [axis_pull[card_axis(o)] if card_kind(o) == "Weapon" else (1.2 if card_kind(o) == "Skill" else 0.8) for o in offers]
                picked = rng.choices(offers, weights=pulls)[0]
                row = _common(n, "upgrade_pick", run_id, session_id)
                row.update({"context": rng.choices(["levelup", "build"], weights=[70, 30])[0], "wave": wave, "level": min(12, 1 + wave * 3 // 4),
                            "offer_1": offers[0], "offer_2": offers[1], "offer_3": offers[2], "picked": picked,
                            "picked_kind": card_kind(picked), "weapon_offered": sum(card_kind(o) == "Weapon" for o in offers),
                            "rerolled": str(rng.random() < 0.15).upper()})
                up.append(row)
            # Supply drop after the wave.
            if wave < last_wave:
                offers = rng.sample(DEMO_WEAPONS, 3)
                full = len(hand) >= 4
                weights = [5, 2, 0, 2, 1] if not full else [0, 3, 6, 3, 1]
                action = rng.choices(ACTIONS, weights=weights)[0]
                picked = rng.choice(offers) if action in ("take_free", "merge", "replace") else ""
                replaced = ""
                if action == "replace":
                    start = {"Knife": 4, "Rifle": 3, "RocketLauncher": 2}
                    replaced = rng.choices(hand, weights=[start.get(w, 1.5) for w in hand])[0]
                    hand[hand.index(replaced)] = picked
                elif action == "take_free" and picked not in hand:
                    hand.append(picked)
                row = _common(n, "supply_pick", run_id, session_id)
                row.update({"wave": wave, "level": min(12, wave), "offer_1": offers[0] + (":upgrade" if action == "merge" else ""),
                            "offer_2": offers[1], "offer_3": offers[2], "action": action, "picked": picked, "replaced": replaced,
                            "replace_cancels": 1 if action == "replace" and rng.random() < 0.2 else 0})
                sup.append(row)
        # Damage rows, one per source per wave.
        for wave in range(1, last_wave + 1):
            boss = wave == 15
            stage = "final_boss" if boss else "wave"
            bucket = BOSS_BUCKET if boss else "W%02d" % wave
            pool = {"Tyrant": 60, "Grunt": 6, "Skitter": 4} if boss else {
                k: w * (1 + (wave / 6 if k in ("Brute", "Rammer", "Stomper", "Shade", "Chanter", "Splitter") else 0))
                for k, w in DEMO_ENEMIES.items() if not (wave < 4 and k in ("Warden", "Spire", "Shade", "Chanter", "Stomper"))}
            pool["backfire"] = 3 + wave * 0.5
            pool["wall_slam"] = 2 + wave * 0.3
            for source, weight in pool.items():
                if rng.random() > 0.75:
                    continue
                pct = max(0.5, rng.gauss(weight, weight * 0.4)) * (1.4 if boss else 1.0)
                elite = source in DEMO_ENEMIES and wave >= 5 and rng.random() < 0.15
                row = _common(n, "wave_damage", run_id, session_id)
                row.update({"stage": stage, "wave": min(wave, 14), "bucket": bucket, "source": source + ("_elite" if elite else ""),
                            "dot": "TRUE" if source in ("Spitter", "Bloater") else "FALSE",
                            "damage": round(pct * 1.8, 1), "damage_pct": round(pct, 1), "hits": max(1, int(pct / 6))})
                dmg.append(row)
    return up, sup, dmg


def write_csv(rows, path):
    if not rows:
        return
    with open(path, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)


# ---------------------------------------------------------------- CLI

def build_parser():
    p = argparse.ArgumentParser(description="Chart the three beta metrics from CSV exports of their sheets.")
    p.add_argument("--upgrades", metavar="CSV", help="'upgrade_picks' export (path or CSV export URL)")
    p.add_argument("--supply", metavar="CSV", help="'supply_picks' export (path or CSV export URL)")
    p.add_argument("--damage", metavar="CSV", help="'damage' export (path or CSV export URL)")
    p.add_argument("--demo", action="store_true", help="chart made-up data instead (writes demo-*.csv too)")
    p.add_argument("--out-dir", default=".", help="folder for the PNGs (default: current folder)")
    p.add_argument("--include-test", action="store_true", help="keep edition = 'test' rows")
    return p


def main(argv=None):
    args = build_parser().parse_args(argv)
    if not args.demo and not (args.upgrades or args.supply or args.damage):
        build_parser().error("give --demo or at least one of --upgrades, --supply, --damage")
    os.makedirs(args.out_dir, exist_ok=True)
    if args.demo:
        up, sup, dmg = demo_rows()
        for rows, name in ((up, "upgrade_picks"), (sup, "supply_picks"), (dmg, "damage")):
            write_csv(rows, os.path.join(args.out_dir, "demo-%s.csv" % name))
        up, sup, dmg = (parse_csv_text(_to_text(r)) for r in (up, sup, dmg))
        source = "made-up demo data (seed 526), not real players"
    else:
        up = load_rows(args.upgrades) if args.upgrades else None
        sup = load_rows(args.supply) if args.supply else None
        dmg = load_rows(args.damage) if args.damage else None
        source = "sheet export"
    written = []
    if up is not None:
        agg = aggregate_upgrades(up, args.include_test)
        print(format_upgrades(agg) + "\n")
        written.append(render_upgrades(agg, os.path.join(args.out_dir, "upgrade-picks.png"), source))
    if sup is not None:
        agg = aggregate_supply(sup, args.include_test)
        print(format_supply(agg) + "\n")
        written.append(render_supply(agg, os.path.join(args.out_dir, "supply-picks.png"), source))
    if dmg is not None:
        agg = aggregate_damage(dmg, args.include_test)
        print(format_damage(agg) + "\n")
        written.append(render_damage(agg, os.path.join(args.out_dir, "damage-sources.png"), source))
    for path in written:
        print("Wrote " + path)
    return 0


def _to_text(rows):
    """Round-trip demo rows through CSV so they are parsed exactly like a sheet export."""
    buf = io.StringIO()
    if rows:
        w = csv.DictWriter(buf, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    return buf.getvalue()


if __name__ == "__main__":
    sys.exit(main())
