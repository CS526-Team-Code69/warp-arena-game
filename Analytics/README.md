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
        --> sheet "Summary" (COUNTIFS grid + stacked column chart)
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

`mode` is `story` only when the game sends exactly `story`; anything else, and a build that sends no mode, is stored as `open`. A story-mode row keeps what is true of the campaign instead of the open-mode values: `stage` is the campaign's stage (`tutorial`, `day`, `dusk`, `night`, `dawn`, `void_trial`, `final_boss`, `ending`, or `other` for anything else), `wave` holds the day, 0 (the tutorial) to 100, with a missing day stored as 0, and a defeat may also have the cause `base_destroyed`. A story-mode victory keeps its own stage and day; it is not moved to the final boss. Every open-mode count (the Summary sheet, the Python chart) leaves story runs out.

## Causes

Charts and the Summary grid use this order.

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
| `plot_story_metrics.py` | Draws the story-mode charts from CSV exports of "v2_days", "v2_purchases" and "v2_events" |
| `send_test_event.py` | Sends one `edition = "test"` event to check a deployment |
| `tests/` | Tests for the Python script |

## Deploy the endpoint

1. Create a new Google Sheet (for example "WARP ARENA analytics") in the account that will own the data.
2. In the sheet, open **Extensions > Apps Script**.
3. Replace the contents of `Code.gs` with `apps-script/Code.gs` from this folder.
4. Open **Project Settings** (gear icon), tick **Show "appsscript.json" manifest file in editor**, go back to the editor and replace `appsscript.json` with `apps-script/appsscript.json`. Save.
5. Select the `setup` function in the toolbar and click **Run**. Accept the authorization prompt (it asks for access to your spreadsheets; if Google warns that the app is not verified, choose **Advanced > Go to ... (unsafe)**, since it is your own script). `setup` creates the "runs" sheet with its header row, saves the spreadsheet id in the script properties, and builds the "Summary" sheet with its chart. It is safe to run again: it rebuilds Summary and the chart and never deletes rows from "runs".
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

This sends one event with `edition = "test"`. A new row should appear at the bottom of "runs", and the row count in the browser check should go up by one. Test rows stay in the sheet but are excluded from the Summary sheet and from the Python chart.

## Make the chart

**In the sheet:** the "Summary" sheet always shows the current counts per wave bucket and cause, the stacked column chart "How runs end, by wave", and three totals: runs started, runs ended (both by distinct `run_id`, test rows excluded) and unfinished runs (distinct `run_id`s with a `run_start` row and no `run_end` row). It counts the open mode only: a `run_end` row with mode `story` is not in the grid, and a `run_id` with any `story` row is not in the totals, so a run whose `run_start` went out as `open` and its `run_end` as `story` is left out whole.

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

Story-mode runs are always left out, as in the Summary sheet (a CSV without the `mode` column is all open mode); the plain-text table says how many were left out. `plot_story_metrics.py` charts the story mode.

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

Directions: `Power` (damage), `Tempo` (cooldown), `Reach` (range / area), `Infusion` (element), `Twist`, `Tame`. Sheet "Upgrade picks" shows offered / picked / pick rate per direction and per card type, and how often a weapon upgrade was taken when one was on offer.

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

Leaving the build phase without choosing (starting the next wave) counts as `skip`. Sheet "Supply picks" shows the share of each choice, the weapons replaced most often and the weapons taken most often.

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

Burn and poison count for whoever applied them (a Spitter's poison is `Spitter`, with `dot = TRUE`; the player's own fire is `backfire`). Wall slams are always `wall_slam`, as in the death causes. Black holes and the void-trial fall kill without damage, so they show up only in "runs". Sheet "Damage sources" shows total damage and share per source with the burn / poison part, and a by-wave pivot with a 100% stacked chart, from open-mode rows only; `plot_beta_metrics.py` leaves story rows out the same way.

### Charts from CSV exports

```
python Analytics/plot_beta_metrics.py --upgrades upgrade_picks.csv --supply supply_picks.csv --damage damage.csv --out-dir charts
python Analytics/plot_beta_metrics.py --demo --out-dir Analytics/mock-charts
```

Each option takes a path or a CSV export URL, as for `plot_run_endings.py`. `--demo` charts made-up data (fixed seed) and writes it as `demo-*.csv` in the sheets' columns; `Analytics/mock-charts/` holds the demo charts.

### Deploying the beta metrics

Paste the new `Code.gs`, then **Deploy > Manage deployments**, edit the existing deployment and pick **Version: New version** (the `/exec` URL stays the same). Run `setup()` once to create the three data sheets and the three summary sheets with their charts; it never removes rows. A browser check of the URL now also lists `beta_rows`.

## Story mode: the V2 day campaign

The story mode sends three more events to the same endpoint, each with the same six first columns as "runs". A classic run sends none of them, and a story-mode run sends no `upgrade_pick` or `supply_pick` rows. A story-mode run also sends `run_start`, `run_end` and `wave_damage`, marked with mode `story`: they go to "runs" and "damage" with the campaign's own stage and the day in `wave` (see those sheets above), and the open-mode summaries and charts leave them out.

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
| I | item | `player_level`, `weapon_damage`, `new_weapon`, `weapon_tame`, `weapon_twist`, `constitution`, `unlock_skill`, `weapon_infusion`, `room_upgrade`, `repair`, `shield`, `lamp`, `ship_part`, `fuel`; a new item is stored as sent |
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
| `hole_check` | `start`, `passed`, `failed_timeout`, `failed_death` (the player fell), `failed_night` (the hole was put away for the night) | The damage dealt as a share of the damage asked for, 0 to 1 | At most one start, one pass and one failure a day |
| `prophet` | Empty | The day number | Once a run, when the Nash Prophet is beaten |
| `void_purchase` | `skill:<id>`, `enchant:<weapon>`, `mk:<level>` or `armor` | The price (void energy for a skill, void alloy for the rest) | Every purchase |
| `void_crystal` | Empty | 0 | Once a run, at the first E on Day 0's void crystals |

The game sends at most 300 `v2_event` rows a run. The endpoint stores an unknown stage as `other` and rejects a story-mode row without a day (`bad day`), a purchase without an item (`bad item`), an unknown kind (`bad kind`), and a detail that is empty where the table lists one or not in the list for `hole_check` (`bad detail`).

### Charts from CSV exports

```
python Analytics/plot_story_metrics.py --days v2_days.csv --purchases v2_purchases.csv --events v2_events.csv --out-dir charts
python Analytics/plot_story_metrics.py --demo --out-dir Analytics/mock-charts
```

Each option takes a path or a CSV export URL, as for `plot_run_endings.py`, and the script uses `plot_beta_metrics.py` from the same folder for reading and styling. It writes:

- `story-days.png` (from `--days`): for each day, the runs that played it, stacked by how the day ended (reached dawn, reached dawn after dying at night, or the run ended that day: base destroyed, the player died, won, left the run), with the share that reached dawn; beside it, the stage where the last day ended.
- `story-purchases.png` (from `--purchases`, and `--events` for the void purchases): how often each item was bought on each day, and the purchases per room.
- `story-hole-checks.png` (from `--events`): the black hole's checks started each day, stacked by result, and how much of the damage asked for the failed checks reached.
- `story-tutorial.png` (from `--events`): the median seconds per tutorial step with the middle half of runs, the runs that passed each step, and the share of runs that pressed E at the void crystals.

The same numbers are printed as plain-text tables. `--demo` charts made-up data (fixed seed) and writes it as `demo-v2_*.csv`; `Analytics/mock-charts/` holds the four demo charts.

### Deploying the story-mode sheets

As for the beta metrics: paste the new `Code.gs`, publish a new version of the existing deployment, and run `setup()` once to create "v2_days", "v2_purchases" and "v2_events" with their headers (they have no summary sheets; the charts come from the script). Until the new version is deployed, the endpoint answers the story-mode rows with `bad event` and they are lost: the game does not send them again. A browser check of the URL now also lists `v2_rows`.

The same `setup()` run adds `mode` as the last header of "runs" (column Q) and "damage" (column O) and rebuilds "Summary" and "Damage sources" with formulas that leave story rows out. It rewrites the header rows only and never removes or moves a row; older rows keep an empty `mode` and count as `open`. Until it runs, the new version already writes the `mode` column, but the old summary formulas still count story rows.

## Tests

Run from the repository root:

```
node Analytics/apps-script/test/run-tests.js
python -m unittest discover -s Analytics/tests -v
```

The Node tests load `Code.gs` with mocked Apps Script services and send no network requests; they cover the story-mode events too. The Python tests use the CSV files in `tests/fixtures/` (`story_v2_*.csv` for the story charts), render charts to a temporary folder (needs `matplotlib`) and also send no network requests.
