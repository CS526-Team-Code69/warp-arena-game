# Analytics: how runs end

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
| P | starting_weapon | text | `RocketLauncher`, `Rifle`, `Knife`, or empty |

Text fields are trimmed and cut to 64 characters, and the text columns are formatted as plain text so values are stored exactly as sent. Out-of-range numbers are clamped, an unknown stage becomes `wave`, and an unknown cause becomes `unknown`.

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
| `apps-script/appsscript.json` | Manifest: V8 runtime, web app run as the owner, open to anonymous callers |
| `apps-script/test/run-tests.js` | Offline tests for `Code.gs` with mocked Apps Script services |
| `plot_run_endings.py` | Draws the chart from a CSV export of the "runs" sheet |
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

- New builds: put the file at `Assets/StreamingAssets/analytics.json` before building.
- The published site, without rebuilding: upload the same file to `StreamingAssets/analytics.json` of both builds, the English one at `/` and the Chinese one at `/zh/`.

With no file or an empty endpoint, the game sends nothing.

## Check a deployment

```
python Analytics/send_test_event.py "https://script.google.com/macros/s/<DEPLOYMENT_ID>/exec"
```

This sends one event with `edition = "test"`. A new row should appear at the bottom of "runs", and the row count in the browser check should go up by one. Test rows stay in the sheet but are excluded from the Summary sheet and from the Python chart.

## Make the chart

**In the sheet:** the "Summary" sheet always shows the current counts per wave bucket and cause, the stacked column chart "How runs end, by wave", and three totals: runs started, runs ended (both by distinct `run_id`, test rows excluded) and unfinished runs (distinct `run_id`s with a `run_start` row and no `run_end` row).

**With Python** (needs `matplotlib`):

1. Get a CSV of the "runs" sheet, either
   - **File > Download > Comma-separated values (.csv)** while "runs" is the open sheet, or
   - the export URL, when the sheet is shared as "Anyone with the link: Viewer":
     `https://docs.google.com/spreadsheets/d/<SPREADSHEET_ID>/export?format=csv&gid=<GID>`
     (`<GID>` is the number after `gid=` in the address bar while "runs" is open).
2. Run the script:

```
python Analytics/plot_run_endings.py runs.csv --out run_endings.png
python Analytics/plot_run_endings.py "https://docs.google.com/spreadsheets/d/<SPREADSHEET_ID>/export?format=csv&gid=<GID>" --out run_endings.png
```

The chart stacks run endings by cause for each wave bucket and reports the number of unfinished runs. Run `python Analytics/plot_run_endings.py --help` for all options.

## Tests

```
node Analytics/apps-script/test/run-tests.js
python -m pytest Analytics/tests
```

The Node tests load `Code.gs` with mocked Apps Script services and send no network requests.
