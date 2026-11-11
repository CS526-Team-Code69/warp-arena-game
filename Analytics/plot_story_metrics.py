"""Charts for the story mode (the V2 day campaign), from CSV exports of its sheets.

    python Analytics/plot_story_metrics.py --runs runs.csv --purchases v2_purchases.csv --events v2_events.csv --damage damage.csv
    python Analytics/plot_story_metrics.py --days v2_days.csv --purchases v2_purchases.csv --events v2_events.csv
    python Analytics/plot_story_metrics.py --demo --out-dir Analytics/mock-charts

Writes up to eight PNGs into --out-dir. The four metrics of the design document:
  story-endings.png      How runs end: the runs ended on each day and in the final battle, by cause, and the enemy kinds behind them
  story-spending.png     Where the resources go: the room and the item of each run's first purchase, and each day's spending by room
  story-weapons.png      Weapon choice: how often each weapon is taken when a crate offers it, by its place on the crate's panel,
                         and the crates left without taking one
  story-damage.png       What hurts the player: damage taken by source, overall and per day
and four closer looks:
  story-days.png         How each day ends: reached dawn (after a night death or not), or the run ended that day, how and where
  story-purchases.png    Purchases per day and item: at the base (--purchases) and with the void currencies (--events)
  story-hole-checks.png  The black hole's timed check per day: passed or failed (time ran out, fell, nightfall), and how close failures came
  story-tutorial.png     Seconds spent on each Day 0 tutorial step, and how many runs passed each step

--runs ("runs" export) draws story-endings and --damage ("damage" export) story-damage; both sheets also hold the open mode, and only
their story rows are read. --days draws story-days, --purchases story-spending and story-purchases (with the void purchases when
--events is given too), --events story-weapons, story-hole-checks and story-tutorial.
--demo makes made-up runs with a fixed seed (no sheet needed), writes them next to the charts as demo-story-*.csv in the same columns
as the sheets, and charts them. Every made-up row has the build "simulated-demo". Use it to preview the charts.

Needs matplotlib for the charts. Parsing and aggregation use only the standard library; the CSV parsing and the chart style come
from plot_beta_metrics.py in this folder.
"""

import argparse
import os
import random
import sys

from plot_beta_metrics import (BAR_COLOR, GRID, INK, INK_2, INK_MUTED, OTHER_COLOR, SOURCE_LABELS, SURFACE, _bool, _empty, _figure,
                               _finish, _num, _pct, _plt, _style, _subtitle, _to_text, base_kind, is_story, keep, load_rows,
                               parse_csv_text, write_csv)

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
DARK_FILLS = {"#2a78d6", "#008300", "#6250d6", "#e34948", "#5f5e5a"}  # white labels read on these; the rest take ink

# ---- How runs end. A cause keeps the colour it has in plot_run_endings.py; the base falling takes the free slot (magenta), a black
# hole and the void trial share one class, and the two ways of leaving another. In this stack order the eight hues pass the palette
# check as adjacent pairs (CVD dE >= 9.1, normal >= 20.8); the greys are neutral.
END_CAUSES = ["enemy", "tyrant", "backfire", "wall_slam", "status", "void", "base", "victory", "unknown", "left"]
END_LABELS = {"enemy": "Enemy", "tyrant": "Tyrant King", "backfire": "Own weapon", "wall_slam": "Wall slam", "status": "Burn / poison",
              "void": "Black hole / void trial", "base": "Base destroyed", "victory": "Victory", "unknown": "Unknown",
              "left": "Left the run"}
END_COLORS = {"enemy": "#2a78d6", "tyrant": "#eb6834", "backfire": "#1baf7a", "wall_slam": "#eda100", "status": "#e34948",
              "void": "#6250d6", "base": "#e87ba4", "victory": "#008300", "unknown": "#5f5e5a", "left": OTHER_COLOR}
END_CLASS = {"enemy": "enemy", "tyrant": "tyrant", "backfire": "backfire", "wall_slam": "wall_slam", "status": "status",
             "black_hole": "void", "void_trial": "void", "base_destroyed": "base", "victory": "victory",
             "restart": "left", "quit_to_title": "left"}
ENEMY_CAUSES = ["enemy", "status", "tyrant"]  # the endings whose row names an enemy kind
FINAL = "final"  # the bucket of the final battle: an ending at the stage final_boss or ending, and every victory
FINAL_STAGES = {"final_boss", "ending"}
LAST_DAY = 7  # the columns always run from Day 0 to Day 7; an overtime day adds its own
MAX_ENEMY_BARS = 8

# ---- Where the resources go. The four rooms in the order the base lists them; categorical slots 1-4 in their validated order.
SPEND_ROOMS = ["Armament", "Energy", "Ship", "Control", "Other"]
ROOM_LABELS = {"Armament": "Armament Bay", "Energy": "Energy Lab", "Ship": "Ship", "Control": "Control Center", "Other": "Other"}
ROOM_COLORS = {"Armament": "#2a78d6", "Energy": "#eb6834", "Ship": "#1baf7a", "Control": "#eda100", "Other": OTHER_COLOR}
CURRENCIES = ["alloy", "energy", "dark"]
ROOM_UPGRADE = "room_upgrade"  # building a room or raising its level: most runs must buy one first, so the items are ranked without it
MAX_FIRST_ITEMS = 6

# ---- Weapon choice. The names a crate shows (the game's English names), by the form the sheet stores.
FORM_NAMES = {
    "Rifle": "Rifle", "Knife": "Knife", "RocketLauncher": "Rocket Launcher", "GravGun": "Grav Gun", "ScatterCannon": "Scatter Cannon",
    "PhaseDagger": "Phase Dagger", "PlasmaLauncher": "Plasma Launcher", "Flamethrower": "Flamethrower", "CryoCannon": "Cryo Cannon",
    "ArcCoil": "Arc Cannon", "SporeLauncher": "Venom Launcher", "Railgun": "Railgun", "Minigun": "Gatling",
    "SwarmLauncher": "Swarm Launcher", "OrbitBlades": "Orbit Blades", "SentryDrone": "Sentry Drone", "ShieldRing": "Energy Ring",
    "Pistol": "Pistol",
}
CRATE_NOT_TAKEN = {"left", "empty", "no_room"}
FIRST_CRATE = "TutorialFirst"  # Day 0's first crate cannot be left, so it is not counted among the crates that could be
MIN_OFFERS = 20  # a weapon offered fewer times than this is left out of the spread between the most and the least taken

# ---- What hurts the player. As in plot_beta_metrics.py: the Tyrant, the player's own weapon and wall slams keep their colours;
# shadows take violet and the four enemy kinds that hurt most the other slots. This stack order is the palette's validated order.
STORY_FIXED_SOURCES = {"Tyrant": "#eb6834", "backfire": "#1baf7a", "wall_slam": "#eda100", "shadow": "#6250d6"}
STORY_ENEMY_SLOTS = ["#2a78d6", "#e87ba4", "#008300", "#e34948"]
STORY_STACK_ORDER = ["enemy1", "Tyrant", "backfire", "wall_slam", "enemy2", "enemy3", "shadow", "enemy4", "Other"]
STORY_SOURCE_LABELS = dict(SOURCE_LABELS, shadow="Shadow (night)", Tyrant="Tyrant King", Hollow="Nash Prophet")
MAX_SOURCE_BARS = 12


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


def _lower(row, name):
    return row.get(name, "").strip().lower()


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
    return item.replace("_", " ").replace(" mk", " Mk")


def crystal_share(agg):
    """Runs that pressed E at the void crystals, of the runs that started the tutorial: (pressed, started)."""
    return len(agg["crystal_runs"] & agg["tutorial_runs"]), len(agg["tutorial_runs"])


# ---- How runs end ("runs" export, story rows)

def story_run_ids(rows):
    """run_ids with any row in the story mode. Such a run is a story run whole, also when its run_start went out as the open mode
    because the game marked the run as a campaign run only later."""
    return {row.get("run_id", "") for row in rows if is_story(row)} - {""}


def end_cause(row):
    """The class of a run_end row: one of END_CAUSES."""
    if _lower(row, "outcome") == "victory":
        return "victory"
    return END_CLASS.get(_lower(row, "cause"), "unknown")


def end_bucket(row):
    """Where a run_end row counts: its day (the story mode keeps it in "wave"), or FINAL for the final battle and every victory."""
    if _lower(row, "stage") in FINAL_STAGES or end_cause(row) == "victory":
        return FINAL
    return min(max(int(_num(row.get("wave"), 0)), 0), 100)


def aggregate_endings(rows, include_test=False):
    """Run endings of the story mode per bucket and cause.

      buckets     {day or FINAL: {cause: n}}
      causes      {cause: n}
      defeats     {day or FINAL: n} for the endings with the outcome "defeat"
      enemies     {enemy kind: {"enemy": n, "status": n, "tyrant": n, "elite": n, "total": n}}
      started, ended, unfinished   distinct runs with a run_start row, run endings counted (one per run), started and never ended
      open_runs   distinct open-mode runs left out
    """
    story = story_run_ids(rows)
    agg = {"buckets": {}, "causes": {c: 0 for c in END_CAUSES}, "defeats": {}, "enemies": {}, "ended": 0}
    started, ended_ids, open_ids = set(), set(), set()
    for row in rows:
        event = _lower(row, "event")
        if not keep(row, include_test) or event not in ("run_start", "run_end"):
            continue
        run = row.get("run_id", "")
        if run not in story and not is_story(row):
            open_ids.add(run)
            continue
        if event == "run_start":
            if run:
                started.add(run)
            continue
        if run:
            if run in ended_ids:
                continue  # a run ends once; ignore repeats
            ended_ids.add(run)
        agg["ended"] += 1
        cause, bucket = end_cause(row), end_bucket(row)
        agg["buckets"].setdefault(bucket, {c: 0 for c in END_CAUSES})[cause] += 1
        agg["causes"][cause] += 1
        if _lower(row, "outcome") == "defeat":
            agg["defeats"][bucket] = agg["defeats"].get(bucket, 0) + 1
        if cause in ENEMY_CAUSES and row.get("enemy"):
            entry = agg["enemies"].setdefault(row["enemy"], dict({c: 0 for c in ENEMY_CAUSES}, elite=0, total=0))
            entry[cause] += 1
            entry["total"] += 1
            if _bool(row.get("elite")):
                entry["elite"] += 1
    agg.update(started=len(started), unfinished=len(started - ended_ids), open_runs=len(open_ids))
    return agg


def ending_buckets(agg):
    """The columns: Day 0 to Day 7 (and any later day a run ended on), then the final battle."""
    days = [b for b in agg["buckets"] if b != FINAL]
    return list(range(max([LAST_DAY] + days) + 1)) + [FINAL]


def bucket_label(bucket):
    return "Final boss" if bucket == FINAL else "Day %d" % bucket


def worst_day(agg):
    """The day (the final battle left out) with the most defeats: (day, its defeats, all defeats), or None without a defeat."""
    total = sum(agg["defeats"].values())
    days = {b: n for b, n in agg["defeats"].items() if b != FINAL}
    if not days:
        return None
    day = min(days, key=lambda b: (-days[b], b))
    return day, days[day], total


# ---- Where the resources go ("v2_purchases" export)

def aggregate_spending(rows, include_test=False):
    """Spending by room, and each run's first purchase.

      days         {day: {room: currency units spent}}: alloy + energy + dark matter as paid
      runs_by_day  {day: runs that bought anything that day}
      rooms        {room: {currency: units}} over all days
      first_room   {room: runs whose first purchase was in it}
      first_item   {(room, item): runs whose first purchase it was}
      first_extra  {(room, item): runs whose first purchase other than a room upgrade it was}; extra_runs: the runs with one
      first_day    {day: runs whose first purchase came that day}
    A run's first purchase is its earliest row: the lowest day, then the order of the rows in the sheet.
    """
    agg = {"days": {}, "runs_by_day": {}, "rooms": {r: {c: 0.0 for c in CURRENCIES} for r in SPEND_ROOMS},
           "first_room": {r: 0 for r in SPEND_ROOMS}, "first_item": {}, "first_extra": {}, "first_day": {}, "purchases": 0,
           "runs": 0, "extra_runs": 0}
    first, extra = {}, {}
    for index, row in enumerate(rows or []):
        day = _day(row)
        if not keep(row, include_test) or row.get("event") != "v2_purchase" or day < 0 or not row.get("item"):
            continue
        room = row.get("room", "")
        room = room if room in SPEND_ROOMS else "Other"
        paid = {c: max(0.0, _num(row.get(c), 0.0)) for c in CURRENCIES}
        agg["days"].setdefault(day, {r: 0.0 for r in SPEND_ROOMS})[room] += sum(paid.values())
        for c in CURRENCIES:
            agg["rooms"][room][c] += paid[c]
        run = row.get("run_id", "")
        agg["runs_by_day"].setdefault(day, set()).add(run)
        agg["purchases"] += 1
        for table in (first, extra) if row["item"] != ROOM_UPGRADE else (first,):
            if run not in table or (day, index) < table[run][0]:
                table[run] = ((day, index), room, row["item"])
    for (day, _), room, item in first.values():
        agg["first_room"][room] += 1
        agg["first_item"][(room, item)] = agg["first_item"].get((room, item), 0) + 1
        agg["first_day"][day] = agg["first_day"].get(day, 0) + 1
    for _, room, item in extra.values():
        agg["first_extra"][(room, item)] = agg["first_extra"].get((room, item), 0) + 1
    agg.update(runs=len(first), extra_runs=len(extra))
    return agg


def spending_shares(agg, day):
    """The share of a day's spending per room, in percent."""
    total = sum(agg["days"][day].values())
    return {room: _pct(agg["days"][day][room], total) for room in SPEND_ROOMS}


def first_items(agg, table="first_item"):
    """First purchases by (room, item), most runs first, then by room and name; the table "first_extra" leaves room upgrades out."""
    return sorted(agg[table].items(), key=lambda kv: (-kv[1], SPEND_ROOMS.index(kv[0][0]), kv[0][1]))


# ---- Weapon choice ("v2_events" export: kinds crate_offer and crate)

def aggregate_weapons(rows, include_test=False):
    """What the crates offered and what was taken.

      forms    {weapon form: {"offered": n, "taken": n}}
      places   {place on the panel, 1 up: {"offered": n, "taken": n}}
      days     {day: {"opened": n, "left": n}} for the crates that could be left (every crate but Day 0's first, with a weapon in it)
      crates   crate rows; runs: the runs that opened one; left, empty, no_room: how the crates without a weapon taken ended
      unrecorded  weapons taken from a crate with no crate_offer rows (opened before they were sent): in no rate
    A crate_offer row is one weapon on a crate's panel; a crate row is the crate's end: the weapon taken, or left, empty, no_room.
    A run opens each kind of crate at most once a day, so run, day and crate kind tell which offers belong to which crate.
    """
    agg = {"forms": {}, "places": {}, "days": {}, "crates": 0, "runs": set(), "left": 0, "empty": 0, "no_room": 0, "unrecorded": 0}
    crates, offered = [], set()

    def count(table, key, field):
        table.setdefault(key, {"offered": 0, "taken": 0})[field] += 1

    for row in rows or []:
        kind = row.get("kind", "")
        if not keep(row, include_test) or row.get("event") != "v2_event" or kind not in ("crate", "crate_offer"):
            continue
        crate, _, what = row.get("detail", "").partition(":")
        if not crate or not what:
            continue
        place = int(_num(row.get("value"), 0))
        key = (row.get("run_id", ""), _day(row), crate)
        if kind == "crate":
            crates.append((key, what, place))
            continue
        offered.add(key)
        count(agg["forms"], what, "offered")
        if place >= 1:
            count(agg["places"], place, "offered")
    for key, what, place in crates:
        (run, day, crate) = key
        agg["crates"] += 1
        agg["runs"].add(run)
        if what in CRATE_NOT_TAKEN:
            agg[what] += 1
        elif key in offered:
            count(agg["forms"], what, "taken")
            if place >= 1:
                count(agg["places"], place, "taken")
        else:
            agg["unrecorded"] += 1
        if crate != FIRST_CRATE and what != "empty" and day >= 0:
            counts = agg["days"].setdefault(day, {"opened": 0, "left": 0})
            counts["opened"] += 1
            counts["left"] += what == "left"
    return agg


def take_rate(entry):
    return _pct(entry["taken"], entry["offered"])


def form_order(agg):
    """Weapons by take rate, highest first, then by name."""
    forms = agg["forms"]
    return sorted(forms, key=lambda f: (-take_rate(forms[f]), f))


def overall_take_rate(agg):
    """Weapons taken per weapon offered: (taken, offered)."""
    return sum(e["taken"] for e in agg["forms"].values()), sum(e["offered"] for e in agg["forms"].values())


def take_rate_spread(agg, least=MIN_OFFERS):
    """The most and the least taken weapon among those offered at least `least` times: ((form, rate), (form, rate)), or None."""
    rated = [(f, take_rate(e)) for f, e in agg["forms"].items() if e["offered"] >= least]
    if len(rated) < 2:
        return None
    return max(rated, key=lambda fr: (fr[1], fr[0])), min(rated, key=lambda fr: (fr[1], fr[0]))


def crates_left(agg):
    """Crates left without taking a weapon, of the crates that could be left: (left, opened)."""
    return sum(d["left"] for d in agg["days"].values()), sum(d["opened"] for d in agg["days"].values())


def label_form(form):
    return FORM_NAMES.get(form, form)


# ---- What hurts the player ("damage" export, story rows)

def damage_bucket(row):
    """Where a story damage row counts: its day, or FINAL for the final battle."""
    if _lower(row, "stage") == "final_boss":
        return FINAL
    return min(max(int(_num(row.get("wave"), 0)), 0), 100)


def aggregate_story_damage(rows, include_test=False):
    """The player's damage by source, as % of max HP: sources {source: pct}, dot {source: the burn and poison part},
    by_bucket {day or FINAL: {source with elites counted with their kind: pct}}, total, runs, and the open-mode rows left out."""
    agg = {"total": 0.0, "sources": {}, "dot": {}, "by_bucket": {}, "runs": set(), "open_rows": 0}
    for row in rows or []:
        if not keep(row, include_test) or row.get("event") != "wave_damage":
            continue
        if not is_story(row):
            agg["open_rows"] += 1
            continue
        pct = _num(row.get("damage_pct"), 0.0)
        if pct <= 0:
            continue
        source = row.get("source") or "other"
        agg["runs"].add(row.get("run_id", ""))
        agg["total"] += pct
        agg["sources"][source] = agg["sources"].get(source, 0.0) + pct
        if _bool(row.get("dot")):
            agg["dot"][source] = agg["dot"].get(source, 0.0) + pct
        by_kind = agg["by_bucket"].setdefault(damage_bucket(row), {})
        by_kind[base_kind(source)] = by_kind.get(base_kind(source), 0.0) + pct
    return agg


def damage_buckets(agg):
    """The columns: Day 0 to Day 7 (and any later day with damage), then the final battle when it has damage."""
    days = [b for b in agg["by_bucket"] if b != FINAL]
    return list(range(max([LAST_DAY] + days) + 1)) + ([FINAL] if FINAL in agg["by_bucket"] else [])


def story_damage_groups(agg):
    """Colour group per source (elites with their kind): the fixed ones, the four enemy kinds that hurt most, then Other."""
    totals = {}
    for source, pct in agg["sources"].items():
        totals[base_kind(source)] = totals.get(base_kind(source), 0.0) + pct
    enemies = sorted((k for k in totals if k not in STORY_FIXED_SOURCES and k != "other"), key=lambda k: (-totals[k], k))
    groups = {k: (k if k in STORY_FIXED_SOURCES else "Other") for k in totals}
    for i, k in enumerate(enemies[:len(STORY_ENEMY_SLOTS)]):
        groups[k] = "enemy%d" % (i + 1)
    colors = {k: STORY_FIXED_SOURCES.get(k) or (STORY_ENEMY_SLOTS[int(g[-1]) - 1] if g.startswith("enemy") else OTHER_COLOR)
              for k, g in groups.items()}
    return groups, colors


def source_share(agg, bucket, kind):
    """The share of a bucket's damage that one source caused (elites counted with their kind), in percent."""
    by_kind = agg["by_bucket"].get(bucket, {})
    return _pct(by_kind.get(kind, 0.0), sum(by_kind.values()))


def label_source(source):
    if source.endswith("_elite"):
        return label_source(base_kind(source)) + " (elite)"
    return STORY_SOURCE_LABELS.get(source, source)


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


def format_endings(agg):
    lines = ["Story runs started: %d   ended: %d   unfinished (closed tab): %d" % (agg["started"], agg["ended"], agg["unfinished"])]
    shown = [c for c in END_CAUSES if agg["causes"][c]]
    if not shown:
        return "\n".join(lines + ["  No story run has ended yet."])
    lines.append("  %-11s %6s " % ("Ended on", "Runs") + " ".join("%9s" % c[:9] for c in shown))
    for bucket in ending_buckets(agg):
        counts = agg["buckets"].get(bucket)
        if counts:
            lines.append("  %-11s %6d " % (bucket_label(bucket), sum(counts.values())) + " ".join("%9d" % counts[c] for c in shown))
    worst = worst_day(agg)
    if worst:
        lines.append("  Most defeats: Day %d with %d of %d (%.0f%%)" % (worst[0], worst[1], worst[2], _pct(worst[1], worst[2])))
    if agg["started"]:
        day0 = sum(agg["buckets"].get(0, {}).values())
        lines.append("  Ended on Day 0: %d of %d runs started (%.0f%%)" % (day0, agg["started"], _pct(day0, agg["started"])))
    if agg["enemies"]:
        lines.append("  Killed by: " + ", ".join("%s %d" % (label_source(k), e["total"]) for k, e in sorted(
            agg["enemies"].items(), key=lambda kv: (-kv[1]["total"], kv[0]))))
    if agg["open_runs"]:
        lines.append("  Open-mode runs left out: %d (plot_run_endings.py charts them)" % agg["open_runs"])
    return "\n".join(lines)


def format_spending(agg):
    lines = ["Purchases: %d in %d runs" % (agg["purchases"], agg["runs"]),
             "  First purchase by room: " + ", ".join("%s %d (%.0f%%)" % (r, n, _pct(n, agg["runs"]))
                                                      for r, n in agg["first_room"].items() if n),
             "  First purchase by item: " + ", ".join("%s %s %d" % (room, label_item(item), n)
                                                      for (room, item), n in first_items(agg)[:MAX_FIRST_ITEMS]),
             "  First purchase other than a room upgrade (%d runs): " % agg["extra_runs"] + ", ".join(
                 "%s %s %d" % (room, label_item(item), n) for (room, item), n in first_items(agg, "first_extra")[:MAX_FIRST_ITEMS]),
             "  %-10s %8s %8s %8s" % ("Room", "Alloy", "Energy", "Dark")]
    for room in SPEND_ROOMS:
        paid = agg["rooms"][room]
        if any(paid.values()):
            lines.append("  %-10s %8.0f %8.0f %8.0f" % (room, paid["alloy"], paid["energy"], paid["dark"]))
    rooms = [r for r in SPEND_ROOMS if any(agg["days"][d][r] for d in agg["days"])]
    lines.append("  Share of the day's spending (alloy + energy + dark matter)")
    lines.append("  %-5s %6s " % ("Day", "Runs") + " ".join("%9s" % r for r in rooms))
    for day in sorted(agg["days"]):
        shares = spending_shares(agg, day)
        lines.append("  %-5d %6d " % (day, len(agg["runs_by_day"][day])) + " ".join("%8.0f%%" % shares[r] for r in rooms))
    return "\n".join(lines)


def format_weapons(agg):
    taken, offered = overall_take_rate(agg)
    left, could = crates_left(agg)
    lines = ["Weapon crates: %d opened in %d runs; %d weapons offered, %d taken (%.0f%%)"
             % (agg["crates"], len(agg["runs"]), offered, taken, _pct(taken, offered)),
             "  %-18s %8s %8s %8s" % ("Weapon", "Offered", "Taken", "Rate")]
    for form in form_order(agg):
        e = agg["forms"][form]
        lines.append("  %-18s %8d %8d %7.0f%%" % (label_form(form), e["offered"], e["taken"], take_rate(e)))
    spread = take_rate_spread(agg)
    if spread:
        lines.append("  Offered at least %d times: most taken %s %.0f%%, least taken %s %.0f%%"
                     % (MIN_OFFERS, label_form(spread[0][0]), spread[0][1], label_form(spread[1][0]), spread[1][1]))
    for place in sorted(agg["places"]):
        e = agg["places"][place]
        lines.append("  Place %d: taken %d of %d (%.0f%%)" % (place, e["taken"], e["offered"], take_rate(e)))
    lines.append("  Left without taking a weapon: %d of %d crates that could be left (%.0f%%); nothing new inside %d, no room %d"
                 % (left, could, _pct(left, could), agg["empty"], agg["no_room"]))
    if agg["unrecorded"]:
        lines.append("  Weapons taken from crates with no offers recorded (in no rate): %d" % agg["unrecorded"])
    return "\n".join(lines)


def format_story_damage(agg):
    lines = ["Damage taken in the story mode: %.0f%% of max HP in total over %d runs" % (agg["total"], len(agg["runs"])),
             "  %-24s %9s %8s %10s" % ("Source", "% max HP", "Share", "Burn/poi.")]
    for source, pct in sorted(agg["sources"].items(), key=lambda kv: (-kv[1], kv[0])):
        lines.append("  %-24s %9.0f %7.1f%% %10.0f" % (label_source(source), pct, _pct(pct, agg["total"]), agg["dot"].get(source, 0.0)))
    lines.append("  Own weapon, share per day: " + ", ".join(
        "%s %.0f%%" % (bucket_label(b), source_share(agg, b, "backfire")) for b in damage_buckets(agg) if agg["by_bucket"].get(b)))
    if agg["open_rows"]:
        lines.append("  Open-mode rows left out: %d (plot_beta_metrics.py charts them)" % agg["open_rows"])
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


def _figure_split(plt, title, ratios, tall):
    """One tall panel and two stacked beside it. tall is "left" or "right"; returns the figure and (tall, upper, lower)."""
    fig = plt.figure(figsize=(13, 6.2), dpi=150, facecolor=SURFACE, layout="constrained")
    grid = fig.add_gridspec(2, 2, width_ratios=ratios)
    wide, narrow = (0, 1) if tall == "left" else (1, 0)
    axes = [fig.add_subplot(grid[:, wide]), fig.add_subplot(grid[0, narrow]), fig.add_subplot(grid[1, narrow])]
    for ax in axes:
        _style(ax)
    fig.suptitle(title, x=0.01, ha="left", fontsize=14, fontweight="semibold", color=INK)
    return fig, axes


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


def _share_columns(ax, x, keys, shares, colors):
    """100% stacked columns: shares[key] is a list of percentages over x; a figure inside each segment tall enough for it."""
    bottoms = [0.0] * len(x)
    for k in keys:
        heights = shares[k]
        ax.bar(x, heights, width=0.66, bottom=bottoms, color=colors[k], edgecolor=SURFACE, linewidth=1.0, zorder=3)
        for xi, h, b in zip(x, heights, bottoms):
            if h >= 9:
                ax.text(xi, b + h / 2, "%.0f" % h, ha="center", va="center", fontsize=7.5,
                        color=SURFACE if colors[k] in DARK_FILLS else INK, zorder=4)
        bottoms = [b + h for b, h in zip(bottoms, heights)]
    ax.set_xlim(-0.6, len(x) - 0.4)
    ax.set_ylim(0, 100)
    ax.yaxis.set_major_formatter(lambda v, _: "%d%%" % v)
    ax.grid(axis="y", color=GRID, linewidth=0.8, zorder=0)


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


def _count_bars(ax, labels, counts, whole, xlabel, colors=None):
    """Horizontal count bars labelled 'count (share of whole)'."""
    y = list(range(len(labels)))
    ax.barh(y, counts, height=0.55, color=colors or BAR_COLOR, edgecolor=SURFACE, linewidth=2, zorder=3)
    top = max(counts) or 1
    for yi, n in zip(y, counts):
        ax.text(n + top * 0.03, yi, "%d  (%.0f%%)" % (n, _pct(n, whole)), va="center", fontsize=9, color=INK_2, zorder=4)
    ax.set_yticks(y, labels)
    ax.set_ylim(len(labels) - 0.5, -0.5)
    ax.set_xlim(0, top * 1.4)
    ax.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
    _integer_axis(ax.xaxis)
    ax.set_xlabel(xlabel, color=INK_2)


def _rate_axis(ax, top, xlabel):
    ax.set_xlim(0, top * 1.5)
    ax.xaxis.set_major_formatter(lambda v, _: "%d%%" % v)
    ax.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
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


def render_endings(agg, out_path, source):
    plt = _plt()
    fig, (left, right) = _figure(plt, "Story mode: how runs end (%d run%s ended)" % (agg["ended"], "" if agg["ended"] == 1 else "s"),
                                 [2.7, 1.0])
    left.set_title("Runs ended on each day, by cause", loc="left", color=INK, fontweight="semibold")
    buckets = ending_buckets(agg)
    x = list(range(len(buckets)))
    shown = [c for c in END_CAUSES if agg["causes"][c]]
    if not shown:
        _empty(left, "No story run has ended yet")
    else:
        values = {c: [agg["buckets"].get(b, {}).get(c, 0) for b in buckets] for c in shown}
        totals, top = _stacked_columns(left, x, shown, values, END_COLORS)
        for xi, total in zip(x, totals):
            if total:
                left.text(xi, total + top * 0.015, "%d" % total, ha="center", va="bottom", fontsize=9, color=INK_2, zorder=4)
        left.set_xticks(x, [bucket_label(b) for b in buckets])
        left.set_xlim(-0.6, len(buckets) - 0.4)
        left.set_ylim(0, top * 1.1)
        left.grid(axis="y", color=GRID, linewidth=0.8, zorder=0)
        _integer_axis(left.yaxis)
        left.set_ylabel("Runs ended", color=INK_2)
        _legend(left, shown, END_LABELS, END_COLORS, 5)
        notes = []
        if agg["started"]:
            day0 = sum(agg["buckets"].get(0, {}).values())
            notes.append("Day 0 ended %.0f%% of the runs started (%d of %d)." % (_pct(day0, agg["started"]), day0, agg["started"]))
        worst = worst_day(agg)
        if worst:
            notes.append("Day %d holds the most defeats: %.0f%% of all defeats (%d of %d)."
                         % (worst[0], _pct(worst[1], worst[2]), worst[1], worst[2]))
        if notes:
            _subtitle(fig, " ".join(notes))

    right.set_title("Killed by", loc="left", color=INK, fontweight="semibold")
    kinds = sorted(agg["enemies"].items(), key=lambda kv: (-kv[1]["total"], kv[0]))[:MAX_ENEMY_BARS]
    if not kinds:
        _empty(right, "No enemy kind recorded yet")
    else:
        y = list(range(len(kinds)))
        lefts = [0] * len(kinds)
        for cause in ENEMY_CAUSES:
            widths = [entry[cause] for _, entry in kinds]
            if any(widths):
                right.barh(y, widths, height=0.6, left=lefts, color=END_COLORS[cause], edgecolor=SURFACE, linewidth=1.0, zorder=3)
            lefts = [a + w for a, w in zip(lefts, widths)]
        top = max(lefts) or 1
        for yi, (_, entry) in zip(y, kinds):
            elite = "  (%d elite)" % entry["elite"] if entry["elite"] else ""
            right.text(entry["total"] + top * 0.03, yi, "%d%s" % (entry["total"], elite), va="center", fontsize=9, color=INK_2, zorder=4)
        right.set_yticks(y, [label_source(k) for k, _ in kinds])
        right.set_ylim(max(len(kinds), MAX_ENEMY_BARS) - 0.5, -0.5)
        right.set_xlim(0, top * 1.6)
        right.grid(axis="x", color=GRID, linewidth=0.8, zorder=0)
        _integer_axis(right.xaxis)
        right.set_xlabel("Runs ended (colours as on the left)", color=INK_2)
    footer = ("Runs started: %d · ended: %d · unfinished (closed tab): %d. 'Final boss' holds every ending of the final battle, "
              "victories included. Source: %s." % (agg["started"], agg["ended"], agg["unfinished"], source))
    return _finish(fig, footer, out_path)


def render_spending(agg, out_path, source):
    plt = _plt()
    fig, (right, upper, lower) = _figure_split(
        plt, "Story mode: where the resources go (%d purchases in %d runs)" % (agg["purchases"], agg["runs"]), [1.0, 1.6], "right")
    upper.set_title("Room of each run's first purchase", loc="left", color=INK, fontweight="semibold")
    lower.set_title("First item bought, room upgrades aside (top %d)" % MAX_FIRST_ITEMS, loc="left", color=INK,
                    fontweight="semibold")
    right.set_title("Share of each day's spending, by room", loc="left", color=INK, fontweight="semibold")
    rooms = [r for r in SPEND_ROOMS if r != "Other" or agg["first_room"]["Other"] or any(agg["rooms"]["Other"].values())]
    if not agg["purchases"]:
        for ax in (upper, lower, right):
            _empty(ax, "No purchases yet")
    else:
        _count_bars(upper, [ROOM_LABELS[r] for r in rooms], [agg["first_room"][r] for r in rooms], agg["runs"],
                    "Runs", [ROOM_COLORS[r] for r in rooms])
        ranked = first_items(agg, "first_extra")[:MAX_FIRST_ITEMS]
        if not ranked:
            _empty(lower, "Only room upgrades bought yet")
        else:
            _count_bars(lower, ["%s: %s" % (room, label_item(item)) for (room, item), _ in ranked], [n for _, n in ranked],
                        agg["extra_runs"], "Runs (bar colour: the item's room)", [ROOM_COLORS[room] for (room, _), _ in ranked])
            lower.set_ylim(max(len(ranked), MAX_FIRST_ITEMS) - 0.5, -0.5)
        days = sorted(agg["days"])
        x = list(range(len(days)))
        shares = {r: [spending_shares(agg, d)[r] for d in days] for r in rooms}
        shown = [r for r in rooms if any(shares[r])]
        _share_columns(right, x, shown, shares, ROOM_COLORS)
        right.set_xticks(x, ["Day %d\n%d run%s" % (d, len(agg["runs_by_day"][d]), "" if len(agg["runs_by_day"][d]) == 1 else "s")
                             for d in days])
        right.tick_params(axis="x", labelsize=8.5)
        _legend(right, shown, ROOM_LABELS, ROOM_COLORS, len(shown))
    footer = ("A run's first purchase is its earliest row; a room upgrade builds a room or raises its level. Spending adds the three "
              "currencies as paid (alloy + energy + dark matter). Source: %s." % source)
    return _finish(fig, footer, out_path)


def render_weapons(agg, out_path, source):
    plt = _plt()
    fig, (left, upper, lower) = _figure_split(
        plt, "Story mode: weapon choice at the crates (%d crates opened in %d runs)" % (agg["crates"], len(agg["runs"])),
        [1.35, 1.0], "left")
    left.set_title("Taken when a crate offers it, by weapon", loc="left", color=INK, fontweight="semibold")
    upper.set_title("Taken, by the weapon's place on the crate's panel", loc="left", color=INK, fontweight="semibold")
    lower.set_title("Crates left without taking a weapon, by day", loc="left", color=INK, fontweight="semibold")
    forms = form_order(agg)
    taken, offered = overall_take_rate(agg)
    if not forms:
        _empty(left, "No weapon crates yet")
    else:
        y = list(range(len(forms)))
        rates = [take_rate(agg["forms"][f]) for f in forms]
        left.barh(y, rates, height=0.6, color=BAR_COLOR, edgecolor=SURFACE, linewidth=2, zorder=3)
        top = max(rates) or 1
        for yi, f, r in zip(y, forms, rates):
            left.text(r + top * 0.015, yi, "%.0f%%  (%d / %d)" % (r, agg["forms"][f]["taken"], agg["forms"][f]["offered"]),
                      va="center", fontsize=9, color=INK_2, zorder=4, bbox={"facecolor": SURFACE, "edgecolor": "none", "pad": 1.5})
        if offered:
            left.axvline(_pct(taken, offered), color=INK_MUTED, linewidth=1.0, zorder=2)
        left.set_yticks(y, [label_form(f) for f in forms])
        left.set_ylim(len(forms) - 0.5, -0.5)
        _rate_axis(left, top, "Share of the times it was offered" + (" (line: all weapons, %.0f%%)" % _pct(taken, offered) if offered else ""))

    places = sorted(agg["places"])
    if not places:
        _empty(upper, "No offers recorded yet")
    else:
        y = list(range(len(places)))
        rates = [take_rate(agg["places"][p]) for p in places]
        upper.barh(y, rates, height=0.5, color=BAR_COLOR, edgecolor=SURFACE, linewidth=2, zorder=3)
        top = max(rates) or 1
        for yi, p, r in zip(y, places, rates):
            upper.text(r + top * 0.02, yi, "%.0f%%  (%d / %d)" % (r, agg["places"][p]["taken"], agg["places"][p]["offered"]),
                       va="center", fontsize=9, color=INK_2, zorder=4)
        upper.set_yticks(y, ["Place %d" % p for p in places])
        upper.set_ylim(len(places) - 0.5, -0.5)
        _rate_axis(upper, top, "Share of the weapons shown in that place")

    days = sorted(agg["days"])
    if not days:
        _empty(lower, "No crates that could be left yet")
    else:
        x = list(range(len(days)))
        shares = [_pct(agg["days"][d]["left"], agg["days"][d]["opened"]) for d in days]
        lower.bar(x, shares, width=0.6, color=BAR_COLOR, edgecolor=SURFACE, linewidth=2, zorder=3)
        top = max(shares) or 1
        for xi, d, s in zip(x, days, shares):
            lower.text(xi, s + top * 0.03, "%.0f%%" % s, ha="center", va="bottom", fontsize=8.5, color=INK_2, zorder=4)
        lower.set_xticks(x, ["Day %d\n%d" % (d, agg["days"][d]["opened"]) for d in days])
        lower.tick_params(axis="x", labelsize=8.5)
        lower.set_xlim(-0.6, len(days) - 0.4)
        lower.set_ylim(0, top * 1.25)
        lower.yaxis.set_major_formatter(lambda v, _: "%d%%" % v)
        lower.grid(axis="y", color=GRID, linewidth=0.8, zorder=0)
        lower.set_xlabel("Under each day: the crates opened that could be left", color=INK_2)
    _subtitle(fig, "A crate shows each weapon's name and Mk level only; its attack and its anomaly stay hidden until it is taken.")
    left_n, could = crates_left(agg)
    old = " %d weapons from crates opened before offers were recorded are in no rate." % agg["unrecorded"] if agg["unrecorded"] else ""
    footer = ("%d of %d crates that could be left were left (%.0f%%); Day 0's first crate cannot be left, and Day 0's column is its "
              "second crate.%s Source: %s." % (left_n, could, _pct(left_n, could), old, source))
    return _finish(fig, footer, out_path)


def render_story_damage(agg, out_path, source):
    plt = _plt()
    from matplotlib.patches import Patch
    fig, (left, right) = _figure(plt, "Story mode: what hurts the player (%d runs)" % len(agg["runs"]), [1.0, 1.6])
    groups, colors = story_damage_groups(agg)

    left.set_title("All damage taken, by source", loc="left", color=INK, fontweight="semibold")
    ranked = sorted(agg["sources"].items(), key=lambda kv: (-kv[1], kv[0]))
    if not ranked:
        _empty(left, "No story damage rows yet")
    else:
        shown = ranked[:MAX_SOURCE_BARS]
        rest = sum(p for _, p in ranked[MAX_SOURCE_BARS:])
        labels = [label_source(s) for s, _ in shown] + (["%d more sources" % (len(ranked) - MAX_SOURCE_BARS)] if rest else [])
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

    right.set_title("Share of each day's damage (elites counted with their kind)", loc="left", color=INK, fontweight="semibold")
    buckets = damage_buckets(agg)
    totals = [sum(agg["by_bucket"].get(b, {}).values()) for b in buckets]
    if not any(totals):
        _empty(right, "No story damage rows yet")
    else:
        names = {}
        for kind, g in groups.items():
            names.setdefault(g, []).append(kind)
        order = [g for g in STORY_STACK_ORDER if names.get(g)]
        shares = {g: [_pct(sum(agg["by_bucket"].get(b, {}).get(k, 0.0) for k in names[g]), t) for b, t in zip(buckets, totals)]
                  for g in order}
        group_colors = {g: OTHER_COLOR if g == "Other" else colors[names[g][0]] for g in order}
        _share_columns(right, list(range(len(buckets))), order, shares, group_colors)
        right.set_xticks(range(len(buckets)), [bucket_label(b) for b in buckets])
        handles = [Patch(facecolor=group_colors[g], edgecolor="none", label="Other enemies" if g == "Other" else label_source(names[g][0]))
                   for g in order]
        right.legend(handles=handles, loc="lower left", bbox_to_anchor=(0.0, 1.06), ncol=5, frameon=False, fontsize=9,
                     handlelength=1.1, handleheight=1.1, columnspacing=1.4, labelcolor=INK_2)
        own = [source_share(agg, b, "backfire") for b in buckets if b != FINAL and b >= 2 and agg["by_bucket"].get(b)]
        if own:
            _subtitle(fig, "The player's own weapons cause %.0f%% of all damage taken, and %.0f%% to %.0f%% of a day's damage from Day 2 on."
                      % (_pct(sum(p for s, p in agg["sources"].items() if base_kind(s) == "backfire"), agg["total"]), min(own), max(own)))
    footer = ("Damage summed as %% of the player's max HP at each hit; burn and poison count for the enemy that applied them; "
              "'Final boss' is the final battle. Source: %s." % source)
    return _finish(fig, footer, out_path)


# ---------------------------------------------------------------- demo data

DEMO_BUILD = "simulated-demo"  # in the "build" column of every made-up row, so none can be taken for a real player's
DEMO_TABS = ("runs", "damage", "v2_days", "v2_purchases", "v2_events")
DEMO_STEP_SECONDS = {"Move": 9, "Fire": 4, "Mine": 22, "Crate": 26, "Sprint": 5, "Fight": 30, "Collect": 8, "Lamp": 18, "DarkMatter": 14}
# Income per day, Day 0 to Day 7: Days 1-7 from the design's table, Day 0 made up.
DEMO_ALLOY = [22, 20, 30, 45, 70, 105, 160, 240]
DEMO_ENERGY = [16, 25, 40, 65, 100, 150, 220, 300]
DEMO_DARK = [6, 10, 12, 15, 18, 28, 36, 45]
DEMO_KILLS = [3, 22, 30, 40, 52, 64, 78, 95]
DEMO_BASE_HP = [500, 500, 750, 1100, 1600, 2400, 3600, 5500]
# The chance that a morning ends the run for a player of average skill: the player falls, or the base does.
DEMO_DEATH = [0, .02, .035, .055, .08, .095, .11, .12]
DEMO_BASE_FALL = [0, .006, .012, .025, .035, .04, .045, .05]
# The kinds that attack on each day, with how much of the morning's damage they tend to deal (the supports never attack).
DEMO_ROSTER = {1: {"Grunt": 10, "Brute": 8, "Skitter": 6, "Splitter": 4}, 2: {"Spitter": 8}, 3: {"Spire": 6},
               4: {"Bloater": 7, "Rammer": 8}, 5: {"Stomper": 10}, 6: {"Shade": 10}}
DEMO_SPECIALS = {"Spire", "Bloater", "Rammer", "Stomper", "Shade"}
DEMO_DOT = {"Spitter", "Bloater"}
DEMO_HAND = ["Rifle", "Knife", "RocketLauncher", "GravGun", "ScatterCannon", "PhaseDagger", "PlasmaLauncher", "Flamethrower",
             "CryoCannon", "ArcCoil", "SporeLauncher", "Railgun", "Minigun", "SwarmLauncher"]
DEMO_AUTO = ["OrbitBlades", "SentryDrone", "ShieldRing"]
# How strongly a weapon's name alone draws a pick, and how strongly its place on the panel does (both made up).
DEMO_NAME_PULL = {"Rifle": 1.2, "Knife": 0.7, "RocketLauncher": 1.55, "GravGun": 0.95, "ScatterCannon": 1.1, "PhaseDagger": 0.8,
                  "PlasmaLauncher": 1.25, "Flamethrower": 1.4, "CryoCannon": 1.0, "ArcCoil": 1.05, "SporeLauncher": 0.75,
                  "Railgun": 1.3, "Minigun": 1.15, "SwarmLauncher": 0.9, "OrbitBlades": 1.0, "SentryDrone": 1.2, "ShieldRing": 0.65}
DEMO_PLACE_PULL = [1.4, 1.0, 0.8]
DEMO_STATS = ["MaxHp", "MoveSpeed", "Damage", "AttackSpeed", "Armor"]
DEMO_SKILLS = ["FrostNova", "StormCall", "Bulwark", "Radiance", "Shockwave", "Overcharge"]
DEMO_ELEMENTS = ["Fire", "Poison", "Frost", "Lightning", "Earth", "Light"]


def _common(n, event, run_id, session_id):
    return {"received_at": "2026-11-1%d 20:%02d:00" % (n % 9, n % 60), "event": event, "run_id": run_id,
            "session_id": session_id, "edition": "en", "build": DEMO_BUILD}


def _demo_roster(day):
    """The attackers of a day's morning: every kind met so far; the Shade takes the Stomper's place on Day 6, and Day 7 has all."""
    roster = {}
    for d in range(1, min(day, 6) + 1):
        roster.update(DEMO_ROSTER[d])
    if day == 6:
        del roster["Stomper"]
    return roster


def _demo_options(st, day, rng):
    """What the base sells this run right now: dicts with the row's fields, a weight and what buying it changes."""
    options = []
    level = st["level"]
    weapon = st["weapons"][-1] if st["weapons"] else "Pistol"

    def add(room, item, detail, weight, apply, alloy=0, energy=0, dark=0):
        options.append({"room": room, "item": item, "detail": detail, "weight": weight, "apply": apply,
                        "alloy": alloy, "energy": energy, "dark": dark})

    def bump(key, by=1):
        return lambda: st.__setitem__(key, st[key] + by)

    def room_level(room):
        return lambda: level.__setitem__(room, level[room] + 1)

    a, e, s = level["Armament"], level["Energy"], level["Ship"]
    if a < 3:
        add("Armament", "room_upgrade", "Armament level %d" % (a + 1), 3.0, room_level("Armament"), alloy=(10, 35, 75)[a])
    if a >= 1 and weapon != "Pistol":
        add("Armament", "weapon_mk", "%s Mk%d" % (weapon, st["mk"] + 2), 2.6, bump("mk"), alloy=8 + 7 * st["mk"])
    if a >= 1:
        add("Armament", "player_level", "level %d, armor %d" % (st["plevel"] + 2, st["plevel"] // 2), 1.4, bump("plevel"),
            alloy=10 + 6 * st["plevel"], energy=4 + 3 * st["plevel"])
    if a >= 2 and weapon != "Pistol":
        add("Armament", "weapon_tame", weapon, 0.9, bump("tame"), alloy=22 + 10 * st["tame"])
    if a >= 3 and weapon != "Pistol":
        add("Armament", "weapon_twist", weapon, 0.7, bump("twist"), alloy=40 + 15 * st["twist"])
    if e < 3:
        add("Energy", "room_upgrade", "Energy level %d" % (e + 1), 3.0, room_level("Energy"), energy=(15, 35, 70)[e])
    if e >= 1:
        add("Energy", "constitution", "%s %s" % (rng.choice(DEMO_STATS), rng.choice(["Common", "Common", "Rare", "Epic"])), 2.4,
            bump("stats"), energy=10 + 6 * st["stats"])
    if e >= 1 and st["tiers"] < 2:
        add("Energy", "research_tier", "body tier %d" % (st["tiers"] + 2), 0.6, bump("tiers"), energy=25 + 15 * st["tiers"])
    if e >= 2:
        add("Energy", "unlock_skill", "%s level %d" % (rng.choice(DEMO_SKILLS), 1 + st["skills"] // 3), 1.8, bump("skills"),
            energy=25 + 12 * st["skills"])
    if e >= 3 and weapon != "Pistol":
        add("Energy", "weapon_infusion", "%s %s" % (weapon, rng.choice(DEMO_ELEMENTS)), 1.0, bump("infusions"),
            energy=45 + 20 * st["infusions"])
    late = 1.0 + 0.45 * day  # the ship matters more as the last day comes closer
    if s < 3:
        add("Ship", "room_upgrade", "Ship level %d" % (s + 1), 1.6 * late, room_level("Ship"), alloy=(20, 45, 85)[s])
    if s >= 1 and not st["engine"]:
        add("Ship", "ship_part", "engine", 5.0, bump("engine"), alloy=6)
    if s >= 2 and not st["armor"]:
        add("Ship", "ship_part", "armor", 5.0, bump("armor"), alloy=9)
    if s >= 3 and st["fuel"] < 10:
        add("Ship", "fuel", "%d/10" % (st["fuel"] + 1), 9.0, bump("fuel"), dark=10)
    if st["lamps"] < 2:
        add("Ship", "lamp", "stock %d" % (st["lamps"] + 4), 0.5, bump("lamps"), alloy=3)
    if st["durability"] < 7:
        add("Control", "room_upgrade", "durability %d" % (st["durability"] + 1), 0.8, bump("durability"),
            alloy=(10, 20, 30, 40, 45, 55)[st["durability"] - 1])
    if st["shield"] < 3:
        add("Control", "shield", "level %d" % (st["shield"] + 1), 0.8, bump("shield"), energy=(20, 45, 85)[st["shield"]])
    if st["broken"]:
        room = st["broken"]
        add(room, "repair", room, 7.0, lambda: st.__setitem__("broken", ""), alloy=15)
    if st["hull"]:
        add("Control", "repair", "hull", 2.5, lambda: st.__setitem__("hull", 0), alloy=st["hull"])
    if day == LAST_DAY:
        add("Control", "overload", "%s %d" % (rng.choice(["shield", "damage"]), st["overload"] + 1), 3.0, bump("overload"),
            **rng.choice([{"alloy": 10}, {"energy": 15}]))
    return options


def _demo_run(rng, n, out):
    """One made-up story run, written into out[tab] for each of the five sheets. skill above 1 is a stronger player; path is the
    room the run favours."""
    ids = ("%032x" % rng.getrandbits(128), "%032x" % rng.getrandbits(128))
    skill = rng.lognormvariate(0, 0.35)
    path = rng.choices(["Armament", "Energy", "Ship"], weights=[44, 32, 24])[0]
    st = {"alloy": 0, "energy": 0, "dark": 0, "level": {"Armament": 0, "Energy": 0, "Ship": 0}, "durability": 1, "shield": 0,
          "engine": 0, "armor": 0, "fuel": 0, "mk": 0, "plevel": 0, "tame": 0, "twist": 0, "stats": 0, "tiers": 0, "skills": 0,
          "infusions": 0, "lamps": 0, "overload": 0, "weapons": [], "autos": 0, "broken": "", "hull": 0, "kills": 0,
          "seconds": 0.0, "prophet": False}
    today = {}

    def row(tab, event, **fields):
        r = _common(n, event, *ids)
        r.update(fields)
        out[tab].append(r)

    def run_row(event, day, stage, outcome="", cause="", enemy="", elite=False):
        row("runs", event, outcome=outcome, cause=cause, enemy=enemy, elite=str(elite).upper(), stage=stage, wave=day,
            run_seconds=round(st["seconds"], 1), level=1 + st["plevel"], kills=st["kills"], starting_weapon="Pistol", mode="story")

    def new_day():
        today.clear()
        today.update(kills=0, player_damage=0.0, player_damage_pct=0.0, player_hits=0, base_damage=0.0, base_damage_hp=0.0,
                     earned_alloy=0, earned_energy=0, earned_dark=0, spent_alloy=0, spent_energy=0, spent_dark=0,
                     night_death=False, lost_alloy=0, lost_energy=0, lost_dark=0, shadows=0, overrun_regions=0, repairs=0)

    def day_row(day, dawn, end_stage="", end_cause=""):
        base_max = DEMO_BASE_HP[day] + 150 * (st["durability"] - 1)
        row("v2_days", "v2_day", day=day, overtime="FALSE", reached_dawn=str(dawn).upper(), end_stage=end_stage, end_cause=end_cause,
            kills=today["kills"], player_damage=round(today["player_damage"], 1), player_damage_pct=round(today["player_damage_pct"], 1),
            player_hits=today["player_hits"], base_damage=round(today["base_damage"], 1), base_damage_hp=round(today["base_damage_hp"], 1),
            base_hp=round(max(0.0, base_max - today["base_damage_hp"]), 1), base_shield=(0, 250, 500, 850)[st["shield"]],
            base_max_hp=base_max, earned_alloy=today["earned_alloy"], earned_energy=today["earned_energy"],
            earned_dark=today["earned_dark"], spent_alloy=today["spent_alloy"], spent_energy=today["spent_energy"],
            spent_dark=today["spent_dark"], alloy=st["alloy"], energy=st["energy"], dark=st["dark"],
            night_death=str(today["night_death"]).upper(), lost_alloy=today["lost_alloy"], lost_energy=today["lost_energy"],
            lost_dark=today["lost_dark"], shadows=today["shadows"], overrun_regions=today["overrun_regions"], repairs=today["repairs"])

    def event(day, stage, kind, detail, value):
        row("v2_events", "v2_event", day=day, stage=stage, kind=kind, detail=detail, value=round(value, 2))

    def earn(currency, amount):
        st[currency] += amount
        today["earned_" + currency] += amount

    def hurt(day, stage, source, pct, dot=False):
        if pct < 0.05:
            return
        max_hp = 100 + 12 * st["stats"]
        hits = max(1, int(pct / 6))
        row("damage", "wave_damage", stage=stage, wave=day, bucket="D%02d" % day, source=source, dot=str(dot).upper(),
            damage=round(pct * max_hp / 100, 1), damage_pct=round(pct, 1), hits=hits, mode="story")
        today["player_damage"] += pct * max_hp / 100
        today["player_damage_pct"] += pct
        today["player_hits"] += hits

    def crate(day, stage, kind):
        """A crate opened: one crate_offer row per weapon on its panel, then the crate row (the weapon taken, or left)."""
        pool = [f for f in DEMO_HAND if f not in st["weapons"]]
        if kind == "Afternoon" and st["autos"] < 2:  # Day 0's crates hold hand weapons only
            pool += [f for f in DEMO_AUTO if f not in st["weapons"]]
        offers = rng.sample(pool, min(3, len(pool)))
        if not offers:
            event(day, stage, "crate", kind + ":empty", 0)
            return
        for place, form in enumerate(offers, 1):
            event(day, stage, "crate_offer", "%s:%s" % (kind, form), place)
        leave = 0.0 if kind == FIRST_CRATE else 0.2 if kind == "TutorialSecond" else 0.08 + 0.03 * day
        if rng.random() < leave:
            event(day, stage, "crate", kind + ":left", 0)
            return
        index = rng.choices(range(len(offers)), weights=[DEMO_NAME_PULL[f] * DEMO_PLACE_PULL[i] for i, f in enumerate(offers)])[0]
        st["weapons"].append(offers[index])
        st["autos"] += offers[index] in DEMO_AUTO
        event(day, stage, "crate", "%s:%s" % (kind, offers[index]), index + 1)

    def shop(day, stage):
        """A visit to the base: buys what the run favours until the wallet or the will runs out; some of the wallet stays."""
        bought = 0
        while bought < 8:
            options = [o for o in _demo_options(st, day, rng) if all(o[c] <= st[c] for c in CURRENCIES)]
            if not options or rng.random() < 0.06 + 0.05 * bought:
                return
            o = rng.choices(options, weights=[o["weight"] * (2.4 if o["room"] == path else 1.0) for o in options])[0]
            for c in CURRENCIES:
                st[c] -= o[c]
                today["spent_" + c] += o[c]
            o["apply"]()
            today["repairs"] += o["item"] == "repair"
            row("v2_purchases", "v2_purchase", day=day, stage=stage, item=o["item"], detail=o["detail"], room=o["room"],
                alloy=o["alloy"], energy=o["energy"], dark=o["dark"])
            bought += 1

    def end(day, stage, outcome, cause, enemy="", elite=False):
        day_row(day, False, stage, cause)
        run_row("run_end", day, stage, outcome, cause, enemy, elite)

    # Day 0: the tutorial's steps, its two crates and the first visit to the base; a few runs stop partway.
    run_row("run_start", 0, "tutorial")
    new_day()
    quit_at = rng.choices([None, "Mine", "Crate", "Fight", "Lamp"], weights=[90, 3, 3, 2, 2])[0]
    for step in TUTORIAL_STEPS:
        if step == quit_at:
            break
        seconds = DEMO_STEP_SECONDS[step] * rng.lognormvariate(0, 0.45)
        st["seconds"] += seconds
        stage = "night" if step in ("Lamp", "DarkMatter") else "tutorial"
        event(0, stage, "tutorial_step", step, seconds)
        if step == "Crate":
            crate(0, stage, FIRST_CRATE)
        elif step == "Fight":
            today["kills"] = DEMO_KILLS[0]
            st["kills"] += DEMO_KILLS[0]
            hurt(0, stage, "Grunt", rng.uniform(0, 16) / skill)
            hurt(0, stage, "backfire", rng.uniform(0, 1.5))
        elif step == "Collect":
            if rng.random() < 0.35:
                event(0, stage, "void_crystal", "", 0)
            crate(0, stage, "TutorialSecond")
            earn("alloy", DEMO_ALLOY[0])
            earn("energy", DEMO_ENERGY[0])
            shop(0, stage)
        elif step == "DarkMatter":
            earn("dark", DEMO_DARK[0])
            hurt(0, stage, "shadow", rng.uniform(0, 8) if rng.random() < 0.4 else 0)
    if quit_at:
        if rng.random() >= 0.3:  # the rest closed the tab: no run_end and no last day
            end(0, "night" if quit_at == "Lamp" else "tutorial", "abandoned", rng.choice(["quit_to_title", "quit_to_title", "restart"]))
        return
    day_row(0, True)

    for day in range(1, LAST_DAY + 1):
        new_day()
        shop(day, "dawn")  # the visit before the morning counts in the coming day
        # Morning: the fight at the base.
        share = min(1.3, 0.7 + 0.3 * skill) * rng.uniform(0.8, 1.2)
        today["kills"] = int(DEMO_KILLS[day] * share)
        st["kills"] += today["kills"]
        st["seconds"] += rng.uniform(150, 240)
        earn("energy", int(DEMO_ENERGY[day] * share))
        total = (20 + 12 * day) * rng.lognormvariate(0, 0.35) / skill ** 0.8
        back = total * (0.07 + 0.012 * day) * rng.uniform(0.6, 1.4) if st["weapons"] else 0.0
        wall = total * (0.03 + (0.03 if day >= 4 else 0)) * rng.uniform(0.3, 1.5)
        attackers = {k: w * rng.uniform(0.5, 1.5) for k, w in _demo_roster(day).items() if rng.random() < 0.8} or {"Grunt": 1.0}
        for kind, weight in attackers.items():
            elite = day >= 3 and kind in DEMO_SPECIALS and rng.random() < 0.18
            hurt(day, "day", kind + ("_elite" if elite else ""), (total - back - wall) * weight / sum(attackers.values()),
                 kind in DEMO_DOT)
        hurt(day, "day", "backfire", back)
        hurt(day, "day", "wall_slam", wall)
        today["base_damage"] = DEMO_BASE_HP[day] * rng.betavariate(1.5, 4) / min(1.5, 0.6 + 0.4 * skill)
        today["base_damage_hp"] = max(0.0, today["base_damage"] - (0, 250, 500, 850)[st["shield"]])
        if rng.random() < 0.08 + 0.03 * day:
            st["broken"] = rng.choice(["Armament", "Energy", "Ship", "Control"])
        if today["base_damage_hp"] > 0 and rng.random() < 0.5:
            st["hull"] = 5 + 5 * day
        # How the morning ends: the player falls, the base falls, the tab is closed, or the run is left.
        death = min(0.6, DEMO_DEATH[day] / skill ** 1.4)
        base_fall = DEMO_BASE_FALL[day] / (1 + 0.25 * (st["durability"] - 1) + 0.2 * st["shield"])
        roll = rng.random()
        if roll < death:
            causes = {"enemy": 60, "status": 10 if day >= 2 else 0, "backfire": 13 if st["weapons"] else 0,
                      "wall_slam": 9 if day >= 4 else 4, "unknown": 1.5}
            cause = rng.choices(list(causes), weights=list(causes.values()))[0]
            enemy, elite = "", False
            if cause in ("enemy", "status"):
                roster = _demo_roster(day)
                kinds = [k for k in roster if cause == "enemy" or k in DEMO_DOT]
                enemy = rng.choices(kinds, weights=[roster[k] * (2.0 if k == "Shade" else 1.0) for k in kinds])[0]
                elite = day >= 3 and enemy in DEMO_SPECIALS and rng.random() < 0.3
            end(day, "day", "defeat", cause, enemy, elite)
            return
        if roll < death + base_fall:
            end(day, "day", "defeat", "base_destroyed")
            return
        if roll < death + base_fall + 0.012:
            return
        if roll < death + base_fall + 0.024:
            end(day, rng.choice(["day", "dusk", "night"]), "abandoned", rng.choice(["quit_to_title", "restart"]))
            return
        # Afternoon: the ore trail, its crate and the black hole.
        st["seconds"] += rng.uniform(120, 200)
        earn("alloy", int(DEMO_ALLOY[day] * (1.3 if st["engine"] else 1.0) * rng.uniform(0.75, 1.0)))
        if rng.random() < 0.78:
            crate(day, "dusk", "Afternoon")
        if not st["prophet"] and rng.random() < 0.55:
            event(day, "dusk", "hole_check", "start", rng.uniform(0.01, 0.08))
            hurt(day, "dusk", "other", rng.uniform(1, 9))
            if rng.random() < 0.12 + 0.08 * day:
                event(day, "dusk", "hole_check", "passed", 1)
                if rng.random() < 0.55:
                    st["prophet"] = True
                    hurt(day, "void_trial", "Hollow", rng.uniform(15, 60) / skill)
                    event(day, "dusk", "prophet", "", day)
            else:
                event(day, "dusk", "hole_check", rng.choices(HOLE_FAILURES, weights=[6, 2, 1])[0], min(0.99, rng.betavariate(3, 2)))
        shop(day, "dusk")
        if st["prophet"] and rng.random() < 0.5:
            event(day, "dusk", "void_purchase", rng.choice(["skill:PocketVoid", "skill:VoidWalk", "enchant:Railgun", "mk:11", "armor"]), 30)
        # Night: dark matter by lamplight; more kills by day, more shadows.
        st["seconds"] += rng.uniform(90, 160)
        st["lamps"] = 0  # the night uses up the lamps
        today["shadows"] = int(today["kills"] * rng.uniform(0.15, 0.35))
        today["overrun_regions"] = today["shadows"] // 9
        today["night_death"] = rng.random() < min(0.5, (0.06 + 0.015 * day) * share / skill)
        hurt(day, "night", "shadow", (2.5 + 2.2 * day) * rng.lognormvariate(0, 0.5) * share
             + (rng.uniform(40, 90) if today["night_death"] else 0))
        haul = int(DEMO_DARK[day] * rng.uniform(0.7, 1.0))
        if today["night_death"]:
            today["lost_dark"] = haul
        else:
            earn("dark", haul)
        if day < LAST_DAY:
            if rng.random() < 0.4:
                shop(day, "night")
            day_row(day, True)
    # The final battle: every made-up run that lasts through Day 7 ignites the ship that night (no overtime days).
    shop(LAST_DAY, "night")
    st["seconds"] += rng.uniform(120, 260)
    hurt(LAST_DAY, "final_boss", "Tyrant", rng.uniform(50, 130) / skill ** 0.5)
    hurt(LAST_DAY, "final_boss", "backfire", rng.uniform(4, 22))
    hurt(LAST_DAY, "final_boss", "wall_slam", rng.uniform(0, 12))
    if rng.random() < min(0.85, 0.45 * skill ** 0.8 * (1 + 0.04 * st["mk"])):
        end(LAST_DAY, "ending", "victory", "victory")
        return
    cause = rng.choices(["tyrant", "backfire", "status", "wall_slam", "quit_to_title"], weights=[70, 12, 6, 7, 5])[0]
    end(LAST_DAY, "final_boss", "abandoned" if cause == "quit_to_title" else "defeat", cause,
        "Tyrant" if cause in ("tyrant", "status") else "")


def demo_rows(runs=150, seed=526):
    """Made-up story runs, plausible enough to judge the charts by: {sheet: rows} for "runs", "damage", "v2_days", "v2_purchases"
    and "v2_events". The same runs fill all five, in the sheets' own columns."""
    rng = random.Random(seed)
    out = {tab: [] for tab in DEMO_TABS}
    for n in range(runs):
        _demo_run(rng, n, out)
    return out


# ---------------------------------------------------------------- CLI

def build_parser():
    p = argparse.ArgumentParser(description="Chart the story mode from CSV exports of its sheets.")
    p.add_argument("--runs", metavar="CSV", help="'runs' export (path or CSV export URL); its story rows are read")
    p.add_argument("--damage", metavar="CSV", help="'damage' export (path or CSV export URL); its story rows are read")
    p.add_argument("--days", metavar="CSV", help="'v2_days' export (path or CSV export URL)")
    p.add_argument("--purchases", metavar="CSV", help="'v2_purchases' export (path or CSV export URL)")
    p.add_argument("--events", metavar="CSV", help="'v2_events' export (path or CSV export URL)")
    p.add_argument("--demo", action="store_true", help="chart made-up data instead (writes demo-story-*.csv too)")
    p.add_argument("--out-dir", default=".", help="folder for the PNGs (default: current folder)")
    p.add_argument("--include-test", action="store_true", help="keep edition = 'test' rows")
    return p


def main(argv=None):
    args = build_parser().parse_args(argv)
    given = {tab: getattr(args, name) for tab, name in zip(DEMO_TABS, ("runs", "damage", "days", "purchases", "events"))}
    if not args.demo and not any(given.values()):
        build_parser().error("give --demo or at least one of --runs, --damage, --days, --purchases, --events")
    os.makedirs(args.out_dir, exist_ok=True)
    if args.demo:
        demo = demo_rows()
        for tab in DEMO_TABS:
            write_csv(demo[tab], os.path.join(args.out_dir, "demo-story-%s.csv" % tab))
        tables = {tab: parse_csv_text(_to_text(demo[tab])) for tab in DEMO_TABS}
        source = "made-up demo data (seed 526), not real players"
    else:
        tables = {tab: load_rows(path) if path else None for tab, path in given.items()}
        source = "sheet export"
    runs, damage, days, purchases, events = (tables[tab] for tab in DEMO_TABS)
    written = []

    def chart(aggregate, describe, render, name, *rows):
        agg = aggregate(*rows, args.include_test)
        print(describe(agg) + "\n")
        written.append(render(agg, os.path.join(args.out_dir, name), source))

    if runs is not None:
        chart(aggregate_endings, format_endings, render_endings, "story-endings.png", runs)
    if purchases is not None:
        chart(aggregate_spending, format_spending, render_spending, "story-spending.png", purchases)
    if events is not None:
        chart(aggregate_weapons, format_weapons, render_weapons, "story-weapons.png", events)
    if damage is not None:
        chart(aggregate_story_damage, format_story_damage, render_story_damage, "story-damage.png", damage)
    if days is not None:
        chart(aggregate_days, format_days, render_days, "story-days.png", days)
    if purchases is not None:
        chart(aggregate_purchases, format_purchases, render_purchases, "story-purchases.png", purchases, events)
    if events is not None:
        chart(aggregate_hole, format_hole, render_hole, "story-hole-checks.png", events)
        chart(aggregate_tutorial, format_tutorial, render_tutorial, "story-tutorial.png", events)
    for path in written:
        print("Wrote " + path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
