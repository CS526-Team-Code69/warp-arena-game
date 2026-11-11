# Analytics: how runs end, and the beta metrics

This folder holds the data pipeline for Metric 2 of the course GDD, "How runs end": for every run, the wave it ended in, the run time, and what ended it (an enemy, the Tyrant, the player's own weapon, a wall slam, damage over time, a black hole, the void trial, a victory, or the player leaving). Grouped by wave and cause, it shows where the difficulty spikes and whether deaths come from enemies or from the player's own weapons and the terrain.

## What is collected

Two events per run, one JSON object each:

- `run_start` when a run begins.
- `run_end` when it ends in victory, defeat, or a restart / quit to title from the pause menu.

A run with a `run_start` and no `run_end` was abandoned by closing the tab; these are reported as "unfinished".

No personal data is sent or stored. The only ids are random: `run_id` is new for every run, `session_id` is new for every page load. There are no names, accounts or device details, and the sheet does not store IP addresses. The game sends events only from the WebGL build, never from the Unity editor or from tests.

## Data flow

```
WebGL build --(navigator.sendBeacon, POST, text/plain JSON)--> Apps Script web app (doPost)
    --> Google Sheet, sheet "runs" (one row per event)
        --> tab "Dashboard" (charts) over the hidden tab "calc" (COUNTIFS tables)
        --> CSV export --> plot_run_endings.py (PNG chart)
```

The body is sent as `text/plain`, so the browser needs no CORS preflight; the game never reads the reply. `doPost` checks and cleans every field, rejects anything malformed (bodies over 4096 characters, invalid JSON, wrong schema, unknown event, bad ids or edition) and appends one row under a script lock.

## The "runs" sheet

| Col | Field | Type | Notes |
|---|---|---|---|
| A | received_at | date | Server time when the row was written |
| B | event | text | `run_start` or `run_end` |
| C | run_id | text | 32 lowercase hex characters, random per run |
| D | session_id | text | 32 lowercase hex characters, random per page load |
| E | edition | text | `en`, `zh`, or `test` (test rows are left out of every chart) |
| F | build | text | Build label, for example `1.0.0+3fa2b1c9` |
| G | outcome | text | `victory`, `defeat`, `abandoned`; empty on `run_start` |
| H | cause | text | See the cause list below; empty on `run_start` |
| I | enemy | text | Enemy kind (for example `Grunt`, `Tyrant`) when the cause is `enemy` or `tyrant` |
| J | elite | boolean | The killing enemy was an elite |
| K | stage | text | `wave`, `build`, `void_trial`, `final_boss` |
| L | wave | integer | 1 to 14; 14 during the final boss |
| M | run_seconds | number | Combat time in seconds, 1 decimal, at most 36000 |
| N | level | integer | 1 to 12 |
| O | kills | integer | 0 or more |
| P | starting_weapon | text | `RocketLauncher`, `Rifle`, `Knife`, or empty (`Pistol` in the story mode) |
| Q | mode | text | `open` (the open mode), or `story` (the story mode, the V2 day campaign); empty on rows written before the column existed, which count as `open` |

Text fields are trimmed and cut to 64 characters, and the text columns are formatted as plain text so values are stored exactly as sent. Out-of-range numbers are clamped, an unknown stage becomes `wave`, and an unknown cause becomes `unknown`.

`mode` is `story` only when the game sends exactly `story`; anything else, and a build that sends no mode, is stored as `open`. A story-mode row keeps what is true of the campaign instead of the open-mode values: `stage` is the campaign's stage (`tutorial`, `day`, `dusk`, `night`, `dawn`, `void_trial`, `final_boss`, `ending`, or `other` for anything else), `wave` holds the day, 0 (the tutorial) to 100, with a missing day stored as 0, and a defeat may also have the cause `base_destroyed`. A story-mode victory keeps its own stage and day; it is not moved to the final boss. Every open-mode count (the open-mode chart of the Dashboard, the Python chart) leaves story runs out.

## Causes

The open-mode chart of the Dashboard and the Python chart use this order.

| Cause | Outcome | Meaning |
|---|---|---|
| `enemy` | defeat | Killed by a normal or special enemy's attack |
| `tyrant` | defeat | Killed by the final boss |
| `backfire` | defeat | Killed by the player's own weapon |
| `wall_slam` | defeat | Knocked into a wall |
| `status` | defeat | Burn or poison damage over time |
| `black_hole` | defeat | Swallowed by a black hole |
| `void_trial` | defeat | Lost the void trial |
| `unknown` | defeat | Anything else |
| `victory` | victory | Beat the final boss |
| `restart` | abandoned | Restarted from the pause menu mid-run |
| `quit_to_title` | abandoned | Quit to title mid-run |

`unknown` also covers an abandoned run whose cause was not recognized; it is counted with the other `unknown` endings.

Wave buckets: waves 1 to 14 for every stage except `final_boss`, plus one "Boss" bucket for `stage = final_boss`. A victory always lands in Boss: the endpoint stores every victory with `stage = final_boss` and wave 14.

## Files

| Path | Purpose |
|---|---|
| `apps-script/Code.gs` | The web app: `doPost`, `doGet`, `setup` |
| `apps-script/appsscript.json` | Manifest: V8 runtime, web app run as the deploying account, open to anonymous callers |
| `apps-script/test/run-tests.js` | Offline tests for `Code.gs` with mocked Apps Script services |
| `plot_run_endings.py` | Draws the chart from a CSV export of the "runs" sheet |
| `plot_story_metrics.py` | Draws the story-mode charts from CSV exports of "runs", "damage", "v2_days", "v2_purchases" and "v2_events" |
| `send_test_event.py` | Sends one `edition = "test"` event to check a deployment |
| `tests/` | Tests for the Python script |

## Deploy the endpoint

1. Create a new Google Sheet (for example "WARP ARENA analytics") in the account that will own the data.
2. In the sheet, open **Extensions > Apps Script**.
3. Replace the contents of `Code.gs` with `apps-script/Code.gs` from this folder.
4. Open **Project Settings** (gear icon), tick **Show "appsscript.json" manifest file in editor**, go back to the editor and replace `appsscript.json` with `apps-script/appsscript.json`. Save.
5. Select the `setup` function in the toolbar and click **Run**. Accept the authorization prompt (it asks for access to your spreadsheets; if Google warns that the app is not verified, choose **Advanced > Go to ... (unsafe)**, since it is your own script). `setup` creates the data sheets with their header rows, saves the spreadsheet id in the script properties, and builds the "Dashboard" tab with its charts and the hidden "calc" tab behind it (see "The Dashboard tab" below). It is safe to run again: it rebuilds those two tabs and never deletes a row of a data sheet.
6. Click **Deploy > New deployment**, choose the type **Web app**, set **Execute as: Me** and **Who has access: Anyone**, then **Deploy**.
7. Copy the **Web app URL** (it ends in `/exec`).
8. Open the URL in a browser. It should show `"ok":true`, `WARP ARENA analytics endpoint is running` and the current row count.

### Updating the script later

Edit the code, then use **Deploy > Manage deployments**, click the pencil on the existing deployment, set **Version: New version** and **Deploy**. This keeps the same `/exec` URL. A **New deployment** creates a new URL, and the game would keep sending to the old one until `analytics.json` is updated.

## Switch the game on

The game reads the endpoint from `analytics.json` in StreamingAssets:

```json
{ "endpoint": "https://script.google.com/macros/s/<DEPLOYMENT_ID>/exec" }
```

- The source of truth is `Assets/StreamingAssets/analytics.json` in the repository: every build copies it into its `StreamingAssets/` folder, so commit the endpoint there and the next deploy keeps it.
- To switch the live site on at once, also edit `StreamingAssets/analytics.json` on the `gh-pages` branch (the English build at `/`, the Chinese one at `/zh/`). A later deploy replaces that branch, which is why the repository file must hold the same URL.

With no file or an empty endpoint, the game sends nothing. A page opened from the same machine (localhost, 127.0.0.1, [::1], a host ending in `.localhost` or `.test`, or `file://`) also sends nothing, so a local test build never writes to the sheet. If the file cannot be read because of a network error, the game tries again on the next event, up to three times per page load.

## Check a deployment

```
python Analytics/send_test_event.py "https://script.google.com/macros/s/<DEPLOYMENT_ID>/exec"
```

This sends one event with `edition = "test"`. A new row should appear at the bottom of "runs", and the row count in the browser check should go up by one. Test rows stay in the sheet but are excluded from the Dashboard and from the Python chart.

## Make the chart

**In the sheet:** the last chart of the "Dashboard" tab, "How open-mode runs end, by wave", always shows the current counts per wave bucket and cause, with the number of runs ended above it (test rows excluded). It counts the open mode only: a `run_end` row with mode `story` is not in it. See "The Dashboard tab" below.

**With Python** (needs `matplotlib`):

1. Get a CSV of the "runs" sheet, either
   - **File > Download > Comma-separated values (.csv)** while "runs" is the open sheet, or
   - the export URL, when the sheet is shared as "Anyone with the link: Viewer":
     `https://docs.google.com/spreadsheets/d/<SPREADSHEET_ID>/export?format=csv&gid=<GID>`
     (`<GID>` is the number after `gid=` in the address bar while "runs" is open).
2. Run the script with exactly one of `--csv` or `--url`:

```
python Analytics/plot_run_endings.py --csv runs.csv --out run-endings.png
python Analytics/plot_run_endings.py --url "https://docs.google.com/spreadsheets/d/<SPREADSHEET_ID>/export?format=csv&gid=<GID>" --out run-endings.png
```

Options:

- `--out PATH` PNG to write (default `run-endings.png` in the current folder).
- `--include-test` keeps the `edition = "test"` rows, which are left out by default.
- `--edition en` or `--edition zh` counts one edition only.

Story-mode runs are always left out, as in the open-mode chart of the Dashboard (a CSV without the `mode` column is all open mode); the plain-text table says how many were left out. `plot_story_metrics.py` charts the story mode.

The main panel stacks run endings by cause for each wave bucket (1 to 14 and Boss); the side panel lists the enemy kinds behind `enemy` and `tyrant` deaths. A footer line gives runs started, runs ended and unfinished runs. The same counts are printed as a plain-text table. With no `run_end` rows yet, the script still writes a PNG that says so. Run `python Analytics/plot_run_endings.py --help` for all options.

## Beta metrics

Three more metrics for the beta, sent from the same build to the same endpoint. Every beta row starts with the same six columns as "runs" (`received_at`, `event`, `run_id`, `session_id`, `edition`, `build`), so it joins "runs" on `run_id`. Only real runs send them (never the enemy lab, the editor, tests or a local page).

### 1. Upgrade picks at level-up: `upgrade_pick` -> sheet "upgrade_picks"

One row per card picked from a level-up draft, mid-wave (`context = levelup`) or in the build phase (`context = build`). Void-trial rewards and drafts resolved automatically when the build timer runs out are not player choices and are not sent.

| Col | Field | Notes |
|---|---|---|
| G | context | `levelup` or `build` |
| H | wave | 1 to 14 |
| I | level | 1 to 12 |
| J-L | offer_1..3 | The three cards on offer: `Weapon:<form>:<direction>`, `Skill:<id>` or `Stat:<stat>` (never the translated title) |
| M | picked | The card picked, same format |
| N | picked_kind | `Weapon`, `Skill` or `Stat`, worked out by the endpoint |
| O | weapon_offered | How many of the three were weapon upgrades, worked out by the endpoint |
| P | rerolled | The draft was rerolled before this pick |

Directions: `Power` (damage), `Tempo` (cooldown), `Reach` (range / area), `Infusion` (element), `Twist`, `Tame`. The sheet has no summary page for these rows; `plot_beta_metrics.py` charts them from a CSV export.

### 2. Supply drop choices: `supply_pick` -> sheet "supply_picks"

One row per supply drop (the armory's three weapons in the build phase), sent when the player decides.

| Col | Field | Notes |
|---|---|---|
| G | wave | The wave just cleared |
| H | level | 1 to 12 |
| I-K | offer_1..3 | `<form>`, or `<form>:upgrade` for a higher Mk of a weapon the player already has |
| L | action | `take_free` (into a free slot), `merge` (higher Mk), `replace`, `skip`, `timeout` (the build timer ran out) |
| M | picked | The weapon taken; empty for `skip` and `timeout` |
| N | replaced | The weapon given up; only for `replace` |
| O | replace_cancels | Times the replace screen was opened and cancelled for this drop |

Leaving the build phase without choosing (starting the next wave) counts as `skip`. The sheet has no summary page for these rows either; `plot_beta_metrics.py` charts them from a CSV export.

### 3. Damage sources: `wave_damage` -> sheet "damage"

The game adds up every hit, burn or poison tick and wall slam that took HP or shield from the player, per wave, stage and source, and sends the totals at the end of each wave and when the run ends. The endpoint writes one row per source.

| Col | Field | Notes |
|---|---|---|
| G | stage | `wave`, `build`, `void_trial`, `final_boss` |
| H | wave | 1 to 14; 14 during the final boss |
| I | bucket | `W01` to `W14`, or `W15 Boss` |
| J | source | The enemy kind (`Grunt`, `Brute`, ..., `Tyrant`), with `_elite` for an elite (`Brute_elite`); `backfire` (the player's own weapon), `wall_slam`, or `other` |
| K | dot | The damage was burn or poison |
| L | damage | Damage taken |
| M | damage_pct | The same as % of the player's max HP at the time of each hit, so early and late waves compare |
| N | hits | Number of hits or ticks |
| O | mode | `open` or `story`, from the event (all rows of one event share it); empty on rows written before the column existed, which count as `open` |

In a `story` row, `stage` is the campaign's stage as in "runs", `wave` holds the day (0 to 100), and `bucket` is the day, `D00` to `D100`.

Burn and poison count for whoever applied them (a Spitter's poison is `Spitter`, with `dot = TRUE`; the player's own fire is `backfire`). Wall slams are always `wall_slam`, as in the death causes. Black holes and the void-trial fall kill without damage, so they show up only in "runs". The open-mode rows have no chart in the sheet: `plot_beta_metrics.py` charts them from a CSV export and leaves story rows out. The story rows are card 4 of the Dashboard.

### Charts from CSV exports

```
python Analytics/plot_beta_metrics.py --upgrades upgrade_picks.csv --supply supply_picks.csv --damage damage.csv --out-dir charts
python Analytics/plot_beta_metrics.py --demo --out-dir Analytics/mock-charts
```

Each option takes a path or a CSV export URL, as for `plot_run_endings.py`. `--demo` charts made-up data (fixed seed) and writes it as `demo-*.csv` in the sheets' columns; `Analytics/mock-charts/` holds the demo charts.

### Deploying the beta metrics

Paste the new `Code.gs`, then **Deploy > Manage deployments**, edit the existing deployment and pick **Version: New version** (the `/exec` URL stays the same). Run `setup()` once to create the three data sheets; it never removes rows. A browser check of the URL now also lists `beta_rows`.

## Story mode: the V2 day campaign

The story mode sends three more events to the same endpoint, each with the same six first columns as "runs". A classic run sends none of them, and a story-mode run sends no `upgrade_pick` or `supply_pick` rows. A story-mode run also sends `run_start`, `run_end` and `wave_damage`, marked with mode `story`: they go to "runs" and "damage" with the campaign's own stage and the day in `wave` (see those sheets above), and the open-mode charts leave them out.

### `v2_day` -> sheet "v2_days"

One row per day: sent at dawn for the day that just ended, and once more when the run ends, for the day that never reached dawn. A day counts from the dawn before it, so a dawn's base visit and its purchases belong to the coming day.

| Col | Field | Notes |
|---|---|---|
| G | day | 0 (the tutorial) to 100 |
| H | overtime | The day is an overtime day |
| I | reached_dawn | `FALSE` only on the run's last day |
| J | end_stage | Where the run ended: `tutorial`, `day`, `dusk`, `night`, `dawn`, `void_trial`, `final_boss`, `ending`, or `other`; empty on a day that reached dawn |
| K | end_cause | How the run ended: one of the causes above or `base_destroyed` (`unknown` when not recognized); empty on a day that reached dawn |
| L | kills | Kills that day |
| M-O | player_damage, player_damage_pct, player_hits | Damage the player took that day, the same as % of max HP, and the hits |
| P-T | base_damage, base_damage_hp, base_hp, base_shield, base_max_hp | Damage to the base (all of it, and the part its HP took), then its HP, shield and max HP at the end of the day |
| U-W | earned_alloy, earned_energy, earned_dark | Currency earned that day |
| X-Z | spent_alloy, spent_energy, spent_dark | Currency spent that day (the sum of its `v2_purchase` rows) |
| AA-AC | alloy, energy, dark | The wallet at the end of the day |
| AD | night_death | The player fell at night (tonight's haul is lost; the run goes on) |
| AE-AG | lost_alloy, lost_energy, lost_dark | What that night death lost |
| AH | shadows | Shadows on the map |
| AI | overrun_regions | Regions overrun |
| AJ | repairs | Purchases of `repair` that day |

The run's last day here has the same `end_stage` and `end_cause` as its `run_end` row in "runs" (mode `story`). A run the game marks as a campaign run only at its first campaign event sends its `run_start` as `open` and everything after it as `story`; in normal play the campaign is known when the run starts, so this does not happen.

### `v2_purchase` -> sheet "v2_purchases"

One row per purchase at the base or at its door.

| Col | Field | Notes |
|---|---|---|
| G | day | The `v2_days` row it counts in |
| H | stage | The stage at the time, as in `end_stage` |
| I | item | `player_level`, `weapon_mk`, `research_tier`, `overload`, `new_weapon`, `weapon_tame`, `weapon_twist`, `constitution`, `unlock_skill`, `weapon_infusion`, `room_upgrade`, `repair`, `shield`, `lamp`, `ship_part`, `fuel`; a new item is stored as sent |
| J | detail | Which one: a weapon and its Mk, the upgrade given, a room and its level, a ship part |
| K | room | `Control`, `Armament`, `Energy`, `Ship` |
| L-N | alloy, energy, dark | The price |

### `v2_event` -> sheet "v2_events"

One row per moment worth knowing in the play added on 2026-11-10.

| Col | Field | Notes |
|---|---|---|
| G | day | The `v2_days` row it counts in |
| H | stage | The stage at the time, as in `end_stage` |
| I | kind | See below |
| J | detail | See below |
| K | value | See below; 2 decimals, never negative |

| kind | detail | value | Sent |
|---|---|---|---|
| `tutorial_step` | The step passed: `Move`, `Fire`, `Mine`, `Crate`, `Sprint`, `Fight`, `Collect` (Day 0's walk), `Lamp`, `DarkMatter` (the tutorial night) | Seconds from the step's start to its pass, in real time (time on the crate panel or in the pause menu counts) | Once per step and run |
| `crate` | `<crate>:<weapon>` for the weapon taken, the crate being `TutorialFirst`, `TutorialSecond` or `Afternoon`; or `<crate>:left` (nothing taken), `<crate>:empty` (nothing new inside), `<crate>:no_room` | The place of the weapon taken on the crate's panel, 1 to 3; 0 when none was taken | Every crate opened |
| `crate_offer` | `<crate>:<weapon>`: one weapon on the panel of an opened crate | The weapon's place on the panel, 1 to 3 | One row for each weapon shown, as the panel opens, before the player chooses; a crate with nothing new inside sends none |
| `hole_check` | `start`, `passed`, `failed_timeout`, `failed_death` (the player fell), `failed_night` (the hole was put away for the night) | The damage dealt as a share of the damage asked for, 0 to 1 | At most one start, one pass and one failure a day |
| `prophet` | Empty | The day number | Once a run, when the Nash Prophet is beaten |
| `void_purchase` | `skill:<id>`, `enchant:<weapon>`, `mk:<level>` or `armor` | The price (void energy for a skill, void alloy for the rest) | Every purchase |
| `void_crystal` | Empty | 0 | Once a run, at the first E on Day 0's void crystals |

The game sends at most 300 `v2_event` rows a run. The endpoint stores an unknown stage as `other` and rejects a story-mode row without a day (`bad day`), a purchase without an item (`bad item`), an unknown kind (`bad kind`), and a detail that is empty where the table lists one or not in the list for `hole_check` (`bad detail`).

The `crate_offer` rows of a crate and its `crate` row together say how often a weapon was taken when it was on offer: card 3 of the Dashboard counts them.

### In the sheet

The four metrics of the story mode are the four cards of the "Dashboard" tab, each with a live chart and an example chart under it: see "The Dashboard tab" below.

### Charts from CSV exports

```
python Analytics/plot_story_metrics.py --runs runs.csv --damage damage.csv --days v2_days.csv --purchases v2_purchases.csv --events v2_events.csv --out-dir charts
python Analytics/plot_story_metrics.py --demo --out-dir Analytics/mock-charts
```

Each option takes a path or a CSV export URL, as for `plot_run_endings.py`, and the script uses `plot_beta_metrics.py` from the same folder for reading and styling. It writes the charts of the four metrics of the design document:

- `story-endings.png` (from `--runs`, story rows only): the runs ended on each day and in the final battle, by cause, and the enemy kinds behind the deaths.
- `story-spending.png` (from `--purchases`): the room and the item of each run's first purchase, and each day's spending by room.
- `story-weapons.png` (from `--events`): how often each weapon is taken when a crate offers it (`crate_offer` against `crate`), the picks by the option's place on the panel, and the crates left without taking one.
- `story-damage.png` (from `--damage`, story rows only): the damage the player took by source, overall and per day.

and four closer looks:

- `story-days.png` (from `--days`): for each day, the runs that played it, stacked by how the day ended (reached dawn, reached dawn after dying at night, or the run ended that day: base destroyed, the player died, won, left the run), with the share that reached dawn; beside it, the stage where the last day ended.
- `story-purchases.png` (from `--purchases`, and `--events` for the void purchases): how often each item was bought on each day, and the purchases per room.
- `story-hole-checks.png` (from `--events`): the black hole's checks started each day, stacked by result, and how much of the damage asked for the failed checks reached.
- `story-tutorial.png` (from `--events`): the median seconds per tutorial step with the middle half of runs, the runs that passed each step, and the share of runs that pressed E at the void crystals.

The same numbers are printed as plain-text tables. `--demo` charts made-up runs (fixed seed) and writes them as `demo-story-*.csv` in the sheets' columns, every row with the build `simulated-demo`; `Analytics/mock-charts/` holds the eight demo charts.

### Deploying the story-mode sheets

As for the beta metrics, in three steps: paste the new `Code.gs` into the Apps Script editor and save; select `setup` and click **Run** once; then **Deploy > Manage deployments**, the pencil on the existing deployment, **Version: New version**, **Deploy** (the `/exec` URL stays the same). `setup()` creates "v2_days", "v2_purchases" and "v2_events" with their headers where they are missing and builds the "Dashboard" and "calc" tabs. It never removes or moves a data row, and it leaves every tab it does not own as it is, but for the summary tabs of its earlier layouts, which it deletes (see "The Dashboard tab"). The tabs of the 2026-11-06 layout ("v2_runs", "v2_damage", "V2 Summary", "V2 Purchases", "V2 Damage sources") are no longer written or read. Until the new version is deployed, the endpoint answers what it does not know yet with `bad event` (the story-mode events) or `bad kind` (`crate_offer` rows) and those rows are lost: the game does not send them again. A browser check of the URL now also lists `v2_rows`.

The same `setup()` run adds `mode` as the last header of "runs" (column Q) and "damage" (column O). It rewrites the header rows only and never removes or moves a row; older rows keep an empty `mode` and count as `open`.

## The Dashboard tab

`setup()` builds two tabs and owns them. Every run rebuilds both, so anything typed into them is lost.

- **"Dashboard"**, the first tab: titles, short captions and charts on a fixed grid, and nothing else.
- **"calc"**, hidden: every table the charts read, one under another, each with a one-cell title. Its first row says what the tab is. To look at it, use **View > Hidden sheets** and pick calc; the next `setup()` hides it again.

Rows with `edition = "test"` are left out everywhere.

The Dashboard has two rows of two cards for the story mode, one card for each of the four metrics of the design document, and one chart for the open mode under them. From top to bottom a card is: a dark header; a caption; a blue row "LIVE DATA" with a count; the live chart, which reads players' rows; an orange row "EXAMPLE (simulated data, not real players)"; and the example chart, whose title starts with "Example:". A live chart and the example under it are the same kind of chart with the same series in the same colours, so the example shows what the live chart looks like once there is data.

| Card | Live chart | Reads | Caption and "LIVE DATA" row |
|---|---|---|---|
| 1 How runs end | Stacked columns: story runs ended per day, by cause | "runs": the `run_end` rows with mode `story` | The number of story runs ended |
| 2 Where the resources go | Stacked columns: what each day's purchases cost, by room. Alloy, energy and dark matter are added up, a unit of each counting the same | "v2_purchases" | How many runs made their first purchase in each room; the number of purchases |
| 3 Weapon choice | Bars: the take rate of each of the 17 weapons (times taken over times a crate offered it), the most taken first | "v2_events": the `crate_offer` and `crate` rows | The take rate by the weapon's place on the crate's panel, and the share of crates left without taking a weapon; weapons offered and taken |
| 4 What hurts the player | Columns stretched to 100%: each day's damage to the player, by source | "damage": the rows with mode `story` | The number of hits on the player |
| Open mode (15 waves) | Stacked columns: runs ended per wave (1 to 14) and against the final boss, by cause | "runs": the `run_end` rows whose mode is not `story` | The number of runs ended |

Above the cards, one line gives the story mode's totals: runs started (distinct `run_id`s that have a story row and sent `run_start`), runs ended, purchases, and crates opened.

How the rows are counted:

- **Days.** `Day 0` (the tutorial) to `Day 7`, then `Day 8+` for every overtime day together. On cards 1 and 4 the last row is "Final boss": what happened in the final battle counts there and on no day. On card 1 that is a run that ended at the stage `final_boss`, or at the stage `ending`, which every victory has. On card 4 it is the damage taken at the stage `final_boss`.
- **Cause groups of card 1.** Enemy, Tyrant King, Own weapon (`backfire`), Wall slam, Burn / poison (`status`), Base destroyed, Victory, Left the run (`restart` and `quit_to_title`), and Other for every other cause (`black_hole`, `void_trial`, `unknown`).
- **Rooms of card 2.** Armament Bay, Energy Lab, Ship, Control Center, and "Other rooms" for a room the script does not list yet. The first purchase of a run is the first of its rows in "v2_purchases" (the rows are in the order they arrived); calc lists it for every run, right of the tables, in the one list that grows with the data.
- **Weapons of card 3.** The list `STORY_WEAPONS` in `Code.gs`. Every count of `crate` rows starts at the first recorded offer: a crate opened by a build that did not send `crate_offer` yet has a result and no offers, and would push the take rates up. A weapon that did not fit (`no_room`) was picked but not taken. The crates that could be left are all but Day 0's first crate, without those that had nothing new inside. The totals row "Offers of weapons the list lacks" of calc is above 0 when the game has a weapon the list does not name.
- **Source groups of card 4.** Grunt, Tyrant King, Own weapon, Wall slam, Brute, Spitter, Shadow (night), Skitter, an elite counted with its kind, and "Other enemies" for every other source. These are the groups of the example: the four enemy kinds that hurt most in the simulated runs. Damage is summed as % of the player's max HP at the time of each hit.
- **Open mode.** Waves 1 to 14 and "Boss", with the causes in the order of the cause table above. A `run_end` row with mode `story` is not counted; a row with an empty mode is.

No chart can come up empty. Each live chart reads a table of calc that has a fixed size and holds zeros until rows arrive, so a chart without data shows its axes and its legend.

The example charts read fixed numbers: the aggregates of the 150 made-up runs in `mock-charts/demo-story-*.csv`, the runs behind the example charts of the design document. They stand in `Code.gs` as constants (`STORY_EXAMPLES`), no formula counts them, and they are never written to a data tab.

`setup()` deletes the summary tabs that its earlier layouts built, when they are there: "Summary", "Upgrade picks", "Supply picks", "Damage sources", "Story endings", "Story spending", "Story weapons" and "Story damage". It never deletes, hides or renames a data tab, and never touches a row of one. Upgrade picks and supply picks keep being collected as raw rows in "upgrade_picks" and "supply_picks"; they have no summary page in the sheet, and `plot_beta_metrics.py` charts them from CSV exports.

## Tests

Run from the repository root:

```
node Analytics/apps-script/test/run-tests.js
python -m unittest discover -s Analytics/tests -v
```

The Node tests load `Code.gs` with mocked Apps Script services and send no network requests; they cover the story-mode events and the "Dashboard" and "calc" tabs too. The mock works out the formulas of calc (COUNTIFS, SUMIFS, MINIFS, the sums and shares over them, the ranked weapon table) and the sentences of the Dashboard against posted rows. The two FILTER formulas are checked as text, and the charts by their kind, place, size, colours and ranges: nothing here draws a chart. The Python tests use the CSV files in `tests/fixtures/` (`story_v2_*.csv` for the story charts), render charts to a temporary folder (needs `matplotlib`) and also send no network requests.
