/**
 * WARP ARENA analytics endpoint.
 *
 * Metric 2, how runs end: run_start / run_end events, one row each in the "runs" sheet.
 * Beta metrics: upgrade_pick -> "upgrade_picks", supply_pick -> "supply_picks",
 * wave_damage -> "damage" (one row per damage source in the event).
 * Story mode (the V2 day campaign): v2_day -> "v2_days", v2_purchase -> "v2_purchases",
 * v2_event -> "v2_events". A story-mode run also sends run_start, run_end and wave_damage; their
 * rows end with mode "story" (open-mode rows with "open") and the open-mode chart leaves them out.
 * Summaries: setup() builds two tabs. "Dashboard" holds titles, captions and charts only: the four story-mode metrics,
 * each as a live chart with an example chart under it, and the open mode's run endings. The hidden "calc" holds every
 * table those charts read.
 *
 * The WebGL build posts one JSON event per request as a text/plain body.
 * doPost validates and sanitizes it, then appends its rows to the event's sheet.
 * Run setup() once from the editor before deploying this project as a web app.
 */

var RUNS_SHEET = 'runs';
var SPREADSHEET_ID_KEY = 'SPREADSHEET_ID';

var MAX_BODY = 4096;
var MAX_TEXT = 64;
var MAX_WAVE = 14;
var MAX_LEVEL = 12;
var MAX_KILLS = 1000000;
var MAX_RUN_SECONDS = 36000;
var LOCK_WAIT_MS = 10000;

// Q mode comes last so that no older column moves; rows written before it existed have it empty and count as the open mode.
var HEADER = [
  'received_at', 'event', 'run_id', 'session_id', 'edition', 'build',
  'outcome', 'cause', 'enemy', 'elite', 'stage', 'wave', 'run_seconds',
  'level', 'kills', 'starting_weapon', 'mode'
];

// The mode of a "runs" or "damage" row: the story mode (the V2 day campaign) only when the game says exactly "story"; anything else,
// including builds that send no mode, is the open mode. The open-mode chart leaves story rows out.
var MODE_OPEN = 'open';
var MODE_STORY = 'story';

// Column order of the open-mode table of calc and stacking order of its chart.
var CAUSES = [
  'enemy', 'tyrant', 'backfire', 'wall_slam', 'status', 'black_hole',
  'void_trial', 'unknown', 'victory', 'restart', 'quit_to_title'
];

var CAUSES_BY_OUTCOME = {
  defeat: ['enemy', 'tyrant', 'backfire', 'wall_slam', 'status', 'black_hole', 'void_trial', 'unknown'],
  victory: ['victory'],
  abandoned: ['restart', 'quit_to_title']
};

// Text columns of "runs". Plain-text format stops appendRow from turning a value such as
// build "2" or an id like "1234e567..." into a number.
var TEXT_COLUMNS = ['B:I', 'K:K', 'P:Q'];

var EVENTS = ['run_start', 'run_end'];

// ---------------------------------------------------------------------------
// Beta metrics: upgrade picks at level-up, supply drop choices, damage sources
// ---------------------------------------------------------------------------

var UPGRADE_SHEET = 'upgrade_picks';
var SUPPLY_SHEET = 'supply_picks';
var DAMAGE_SHEET = 'damage';

var COMMON_HEADER = ['received_at', 'event', 'run_id', 'session_id', 'edition', 'build'];
// A..P: G context, H wave, I level, J..L offers, M picked, N picked_kind, O weapon_offered, P rerolled.
var UPGRADE_HEADER = COMMON_HEADER.concat([
  'context', 'wave', 'level', 'offer_1', 'offer_2', 'offer_3', 'picked', 'picked_kind', 'weapon_offered', 'rerolled'
]);
// A..O: G wave, H level, I..K offers, L action, M picked, N replaced, O replace_cancels.
var SUPPLY_HEADER = COMMON_HEADER.concat([
  'wave', 'level', 'offer_1', 'offer_2', 'offer_3', 'action', 'picked', 'replaced', 'replace_cancels'
]);
// A..O: G stage, H wave, I bucket, J source, K dot, L damage, M damage_pct, N hits, O mode (last, as in "runs").
var DAMAGE_HEADER = COMMON_HEADER.concat([
  'stage', 'wave', 'bucket', 'source', 'dot', 'damage', 'damage_pct', 'hits', 'mode'
]);

var UPGRADE_TEXT_COLUMNS = ['B:G', 'J:N'];
var SUPPLY_TEXT_COLUMNS = ['B:F', 'I:N'];
var DAMAGE_TEXT_COLUMNS = ['B:G', 'I:J', 'O:O'];

var UPGRADE_CONTEXTS = ['levelup', 'build'];
var CARD_KINDS = ['Weapon', 'Skill', 'Stat'];
var SUPPLY_ACTIONS = ['take_free', 'merge', 'replace', 'skip', 'timeout'];
var SUPPLY_TAKES = ['take_free', 'merge', 'replace'];
var MAX_DAMAGE_ROWS = 24;
var MAX_DAMAGE = 10000000;
var MAX_DAMAGE_PCT = 1000000;
var MAX_HITS = 1000000;
var MAX_CANCELS = 100;
var BOSS_BUCKET = 'W15 Boss';

var BETA_EVENTS = {
  upgrade_pick: { sheet: UPGRADE_SHEET, header: UPGRADE_HEADER, text: UPGRADE_TEXT_COLUMNS, sanitize: sanitizeUpgrade_ },
  supply_pick: { sheet: SUPPLY_SHEET, header: SUPPLY_HEADER, text: SUPPLY_TEXT_COLUMNS, sanitize: sanitizeSupply_ },
  wave_damage: { sheet: DAMAGE_SHEET, header: DAMAGE_HEADER, text: DAMAGE_TEXT_COLUMNS, sanitize: sanitizeDamage_ }
};
var EDITIONS = ['en', 'zh', 'test'];
var STAGES = ['wave', 'build', 'void_trial', 'final_boss'];
var ID_PATTERN = /^[0-9a-f]{32}$/;

// ---------------------------------------------------------------------------
// Story mode (the V2 day campaign): one row per day, per purchase, per moment worth knowing
// ---------------------------------------------------------------------------

var V2_DAYS_SHEET = 'v2_days';
var V2_PURCHASES_SHEET = 'v2_purchases';
var V2_EVENTS_SHEET = 'v2_events';

// A..AJ: G day, H overtime, I reached_dawn, J end_stage, K end_cause, L kills, M..O the player's damage,
// P..T the base's damage and state, U..W earned, X..Z spent, AA..AC wallet, AD night_death, AE..AG lost,
// AH shadows, AI overrun_regions, AJ repairs.
var V2_DAY_HEADER = COMMON_HEADER.concat([
  'day', 'overtime', 'reached_dawn', 'end_stage', 'end_cause', 'kills',
  'player_damage', 'player_damage_pct', 'player_hits',
  'base_damage', 'base_damage_hp', 'base_hp', 'base_shield', 'base_max_hp',
  'earned_alloy', 'earned_energy', 'earned_dark', 'spent_alloy', 'spent_energy', 'spent_dark',
  'alloy', 'energy', 'dark', 'night_death', 'lost_alloy', 'lost_energy', 'lost_dark',
  'shadows', 'overrun_regions', 'repairs'
]);
// A..N: G day, H stage, I item, J detail, K room, L..N the price.
var V2_PURCHASE_HEADER = COMMON_HEADER.concat(['day', 'stage', 'item', 'detail', 'room', 'alloy', 'energy', 'dark']);
// A..K: G day, H stage, I kind, J detail, K value.
var V2_EVENT_HEADER = COMMON_HEADER.concat(['day', 'stage', 'kind', 'detail', 'value']);

var V2_DAY_TEXT_COLUMNS = ['B:F', 'J:K'];
var V2_PURCHASE_TEXT_COLUMNS = ['B:F', 'H:K'];
var V2_EVENT_TEXT_COLUMNS = ['B:F', 'H:J'];

// The campaign's stages, as RunAnalytics names them; any other stage is stored as "other".
var V2_STAGES = ['tutorial', 'day', 'dusk', 'night', 'dawn', 'void_trial', 'final_boss', 'ending'];
// What ended a run in the campaign: the "runs" causes plus the base falling.
var V2_END_CAUSES = CAUSES.concat(['base_destroyed']);
// The kinds of v2_event and the details each accepts: 'required' (any text, not empty), 'optional' (any text) or a list.
var V2_EVENT_DETAILS = {
  tutorial_step: 'required',
  crate: 'required',
  crate_offer: 'required',
  hole_check: ['start', 'passed', 'failed_timeout', 'failed_death', 'failed_night'],
  prophet: 'optional',
  void_purchase: 'required',
  void_crystal: 'optional'
};
var MAX_DAY = 100;
var MAX_CURRENCY = 10000000;
var MAX_COUNT = 100000;
var MAX_V2_VALUE = 1000000;

var V2_EVENTS = {
  v2_day: { sheet: V2_DAYS_SHEET, header: V2_DAY_HEADER, text: V2_DAY_TEXT_COLUMNS, sanitize: sanitizeV2Day_ },
  v2_purchase: { sheet: V2_PURCHASES_SHEET, header: V2_PURCHASE_HEADER, text: V2_PURCHASE_TEXT_COLUMNS, sanitize: sanitizeV2Purchase_ },
  v2_event: { sheet: V2_EVENTS_SHEET, header: V2_EVENT_HEADER, text: V2_EVENT_TEXT_COLUMNS, sanitize: sanitizeV2Event_ }
};

// ---------------------------------------------------------------------------
// The two tabs setup() builds: "Dashboard" (titles, captions and charts) and the hidden "calc" (every table the charts read)
// ---------------------------------------------------------------------------

var DASHBOARD_SHEET = 'Dashboard';
var CALC_SHEET = 'calc';
// The summary tabs that earlier layouts of this script built. setup() deletes them: the Dashboard shows their charts in one
// place, and a tab named "Upgrade picks" beside the data tab "upgrade_picks" reads as the same thing twice.
var OLD_SUMMARY_SHEETS = ['Summary', 'Upgrade picks', 'Supply picks', 'Damage sources',
  'Story endings', 'Story spending', 'Story weapons', 'Story damage'];
var CALC_NOTE = 'calc: the tables the charts of "Dashboard" read. setup() rebuilds this tab, so anything typed here is lost. ' +
  'LIVE tables are formulas over the data tabs, rows with the edition "test" left out; EXAMPLE tables are fixed numbers from ' +
  'simulated runs, not from players.';

// The campaign's last planned day. Day 0 is the tutorial; the days after STORY_DAYS (overtime) share one row of a table.
var STORY_DAYS = 7;
var STORY_BOSS_ROW = 'Final boss';
// The stages of the final battle: the fight with the final boss, and the ending after it, which is the stage of every victory.
var STORY_FINAL_STAGES = ['final_boss', 'ending'];
// Damage is dealt in the fight only, so the final battle's row of the damage table reads this one stage.
var STORY_FINAL_FIGHT = 'final_boss';

// One colour for one thing on every chart: an enemy's attack is blue on card 1, on card 4 and on the open-mode chart, the
// Tyrant orange, the player's own weapon green, a wall slam yellow. A live chart and the example under it take the same list.

// Card 1, how runs end. The series of its chart in stacking order: a label, and the causes of "runs" it adds up. The labels
// are the columns of the example table, so the live chart and its example show the same series; a cause no group lists
// (a black hole, the void trial, "unknown") counts under OTHER_GROUP.
var END_GROUPS = [
  { label: 'Enemy', causes: ['enemy'] },
  { label: 'Tyrant King', causes: ['tyrant'] },
  { label: 'Own weapon', causes: ['backfire'] },
  { label: 'Wall slam', causes: ['wall_slam'] },
  { label: 'Burn / poison', causes: ['status'] },
  { label: 'Base destroyed', causes: ['base_destroyed'] },
  { label: 'Victory', causes: ['victory'] },
  { label: 'Left the run', causes: ['restart', 'quit_to_title'] }
];
var OTHER_GROUP = 'Other';
var END_COLOURS = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e34948', '#e87ba4', '#008300', '#b1afa8', '#5f5e5a'];

// Card 2, where the resources go. The base's rooms as the game names them in "v2_purchases", in the order the base shows
// them, and the names the charts give them; a room the game adds later counts under OTHER_ROOMS until it is listed here.
var STORY_ROOMS = ['Armament', 'Energy', 'Ship', 'Control'];
var ROOM_LABELS = ['Armament Bay', 'Energy Lab', 'Ship', 'Control Center'];
var OTHER_ROOMS = 'Other rooms';
var ROOM_COLOURS = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#b1afa8'];
// The price fields of "v2_purchases".
var CURRENCIES = ['alloy', 'energy', 'dark'];
// calc lists every run's first purchase in four columns from this cell down (N5: run_id, day, room, item). The list grows
// with the data, so it stands right of every table, where nothing is under it.
var FIRST_PURCHASE_COLUMN = 14;
var FIRST_PURCHASE_ROW = 5;

// Card 3, weapon choice. The weapons a crate can hold: every weapon form of the game but the pistol. A form the game adds
// later is counted in the totals row "Offers of weapons the list lacks" of calc until it is listed here.
var STORY_WEAPONS = ['Rifle', 'Knife', 'RocketLauncher', 'GravGun', 'ScatterCannon', 'PhaseDagger', 'PlasmaLauncher', 'Flamethrower',
  'CryoCannon', 'ArcCoil', 'SporeLauncher', 'Railgun', 'Minigun', 'SwarmLauncher', 'OrbitBlades', 'SentryDrone', 'ShieldRing'];
// Day 0's first crate cannot be left, so it is not among the crates that could be.
var FIRST_CRATE = 'TutorialFirst';
// A crate's panel shows at most this many weapons.
var CRATE_PLACES = 3;
var PLACE_NAMES = ['1st', '2nd', '3rd'];
var WEAPON_COLOUR = '#2a78d6';

// Card 4, what hurts the player. The series of its chart in stacking order: a label, and the sources of "damage" it adds up
// (an elite counts with its kind). The labels are the columns of the example table; every other source counts under
// OTHER_SOURCES, the example's last column.
var DAMAGE_GROUPS = [
  { label: 'Grunt', sources: ['Grunt', 'Grunt_elite'] },
  { label: 'Tyrant King', sources: ['Tyrant', 'Tyrant_elite'] },
  { label: 'Own weapon', sources: ['backfire'] },
  { label: 'Wall slam', sources: ['wall_slam'] },
  { label: 'Brute', sources: ['Brute', 'Brute_elite'] },
  { label: 'Spitter', sources: ['Spitter', 'Spitter_elite'] },
  { label: 'Shadow (night)', sources: ['shadow'] },
  { label: 'Skitter', sources: ['Skitter', 'Skitter_elite'] }
];
var OTHER_SOURCES = 'Other enemies';
var DAMAGE_COLOURS = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#6250d6', '#e34948', '#b1afa8'];

// The open-mode chart: the names its series give the CAUSES, in their order, and their colours.
var OPEN_CAUSE_LABELS = ['Enemy', 'Tyrant King', 'Own weapon', 'Wall slam', 'Burn / poison', 'Black hole', 'Void trial', 'Unknown',
  'Victory', 'Restart', 'Quit to title'];
var OPEN_COLOURS = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e34948', '#6250d6', '#9c3d8c', '#5f5e5a', '#008300', '#b1afa8', '#d6d4cc'];

// The rows of the totals table of calc, in order: the name the script knows a total by, and the label the table shows.
var TOTALS = [
  ['started', 'Story runs started'],
  ['ended', 'Story runs ended'],
  ['purchases', 'Purchases'],
  ['crates', 'Crates opened'],
  ['since', 'Crate offers recorded since'],
  ['offered', 'Weapons offered'],
  ['taken', 'Weapons taken'],
  ['unlisted', 'Offers of weapons the list lacks'],
  ['hits', 'Hits on the player'],
  ['openEnded', 'Open-mode runs ended']
];

// The Dashboard's grid. Columns: a margin (A), the left card (B to G), a gutter (H), the right card (I to N), a margin (O).
// A chart is exactly as wide as its card and as tall as the rows under it, so no chart covers a text row or another chart.
var MARGIN_WIDTH = 20;
var GUTTER_WIDTH = 40;
var CARD_COLUMNS = 6;
var CARD_COLUMN_WIDTH = 100;
var LEFT_CARD = 2;
var RIGHT_CARD = LEFT_CARD + CARD_COLUMNS + 1;
var WIDE_COLUMNS = 2 * CARD_COLUMNS + 1;
var CARD_WIDTH = CARD_COLUMNS * CARD_COLUMN_WIDTH;
var WIDE_WIDTH = 2 * CARD_WIDTH + GUTTER_WIDTH;
var ROW_HEIGHT = 21;
var TITLE_ROW_HEIGHT = 38;
var HEADER_ROW_HEIGHT = 28;
// The caption of a card is a sentence of up to some 120 characters in a cell 600 px wide. It wraps onto a second line, and a
// merged cell does not make its row grow, so the row is given the height of two lines.
var CAPTION_ROW_HEIGHT = 36;
var TITLE_ROW = 1;
var INTRO_ROW = 2;
var TOTALS_ROW = 3;
// The rows of a band of two cards: the header, the caption, the "LIVE DATA" label, the first row of the live chart, the
// example label and the first row of the example chart. A chart covers chartRows rows.
var UPPER_BAND = { header: 5, caption: 6, liveLabel: 7, live: 8, exampleLabel: 22, example: 23, chartRows: 14 };
var LOWER_BAND = { header: 38, caption: 39, liveLabel: 40, live: 41, exampleLabel: 59, example: 60, chartRows: 18 };
// The open mode has one chart as wide as both cards, and no example.
var OPEN_BAND = { header: 79, caption: 80, chart: 81, chartRows: 14 };
var DASHBOARD_LAST_ROW = 94;

var HEADER_FILL = '#1F2937';
var HEADER_INK = '#FFFFFF';
var LIVE_FILL = '#DBEAFE';
// The ground of everything that is an example, so that it cannot be taken for players' data.
var EXAMPLE_FILL = '#f6b26b';
var CAPTION_INK = '#6B7280';

// " \u00B7 " is a middle dot between two spaces; written as an escape, this file stays plain ASCII.
var DOT = ' \u00B7 ';
var DASHBOARD_TITLE = 'WARP ARENA' + DOT + 'analytics dashboard';
var DASHBOARD_INTRO = 'Story mode: the four metrics of the design document. Each card shows the live chart from players\' rows ' +
  'and, under it, an example drawn from simulated runs.';
var LIVE_LABEL = 'LIVE DATA';
var EXAMPLE_LABEL = 'EXAMPLE (simulated data, not real players)';

// ---------------------------------------------------------------------------
// The example tables: fixed numbers from simulated runs, charted under each live chart as a reference
// ---------------------------------------------------------------------------

// The aggregates of the made-up runs in Analytics/mock-charts/demo-story-*.csv (150 runs, seed 526, build "simulated-demo"),
// worked out once by the aggregate_* functions of Analytics/plot_story_metrics.py. They are plain numbers: no formula reads a
// data sheet for them, and they are never written to one.
var STORY_EXAMPLES = {
  // demo-story-runs.csv, aggregate_endings: runs ended per day and in the final battle, by cause class (classes no run ended with are left out).
  endings: {
    header: ['Day', 'Enemy', 'Tyrant King', 'Own weapon', 'Wall slam', 'Burn / poison', 'Base destroyed', 'Victory', 'Left the run'],
    rows: [
      ['Day 0', 0, 0, 0, 0, 0, 0, 0, 9],
      ['Day 1', 0, 0, 0, 0, 0, 1, 0, 1],
      ['Day 2', 2, 0, 0, 0, 0, 1, 0, 0],
      ['Day 3', 5, 0, 2, 0, 1, 0, 0, 0],
      ['Day 4', 4, 0, 1, 2, 2, 2, 0, 1],
      ['Day 5', 7, 0, 0, 1, 1, 6, 0, 1],
      ['Day 6', 3, 0, 3, 3, 2, 4, 0, 1],
      ['Day 7', 7, 0, 0, 0, 1, 2, 0, 1],
      ['Final boss', 0, 15, 2, 1, 4, 0, 34, 1]
    ],
    runs: { started: 150, ended: 134, unfinished: 16 }
  },
  // demo-story-v2_purchases.csv, aggregate_spending: each day's spending per room (alloy + energy + dark matter), and the room of each run's first purchase.
  spending: {
    header: ['Day', 'Armament Bay', 'Energy Lab', 'Ship', 'Control Center'],
    rows: [
      ['Day 0', 1260, 1650, 922, 190],
      ['Day 1', 1501, 1908, 764, 890],
      ['Day 2', 1800, 3548, 894, 1110],
      ['Day 3', 2709, 5938, 1595, 1255],
      ['Day 4', 3557, 7728, 1810, 1465],
      ['Day 5', 4436, 7646, 3913, 1935],
      ['Day 6', 4636, 10978, 4487, 1700],
      ['Day 7', 6269, 7745, 3661, 3115]
    ],
    firstHeader: ['Room of a run\'s first purchase', 'Runs'],
    firstRows: [
      ['Armament Bay', 42],
      ['Energy Lab', 47],
      ['Ship', 39],
      ['Control Center', 11]
    ],
    runs: 139
  },
  // demo-story-v2_events.csv, aggregate_weapons in form_order: times offered and taken per weapon, most taken first.
  weapons: {
    header: ['Weapon', 'Offered', 'Taken', 'Take rate'],
    rows: [
      ['RocketLauncher', 130, 54, 0.415],
      ['Minigun', 146, 57, 0.39],
      ['Flamethrower', 153, 57, 0.373],
      ['ArcCoil', 133, 49, 0.368],
      ['Railgun', 146, 48, 0.329],
      ['PlasmaLauncher', 143, 47, 0.329],
      ['SentryDrone', 105, 34, 0.324],
      ['GravGun', 160, 49, 0.306],
      ['Rifle', 164, 48, 0.293],
      ['CryoCannon', 158, 45, 0.285],
      ['SwarmLauncher', 140, 36, 0.257],
      ['OrbitBlades', 106, 26, 0.245],
      ['ScatterCannon', 159, 39, 0.245],
      ['SporeLauncher', 184, 40, 0.217],
      ['PhaseDagger', 154, 30, 0.195],
      ['ShieldRing', 109, 20, 0.183],
      ['Knife', 170, 26, 0.153]
    ],
    unrecorded: 0
  },
  // demo-story-damage.csv, aggregate_story_damage with story_damage_groups: each day's damage per source group, in percent of the day.
  damage: {
    header: ['Day', 'Grunt', 'Tyrant King', 'Own weapon', 'Wall slam', 'Brute', 'Spitter', 'Shadow (night)', 'Skitter', 'Other enemies'],
    rows: [
      ['Day 0', 79.2, 0, 7.1, 0, 0, 0, 13.7, 0, 0],
      ['Day 1', 23.9, 0, 6, 2, 17.2, 0, 18.4, 14.2, 18.3],
      ['Day 2', 15.5, 0, 6.8, 2.1, 14.8, 13.7, 18.9, 10.9, 17.5],
      ['Day 3', 13.3, 0, 7.8, 2, 12.2, 12.8, 22, 9, 20.8],
      ['Day 4', 11.1, 0, 8.7, 3.7, 8.9, 8.8, 20.5, 6.8, 31.5],
      ['Day 5', 8.8, 0, 10.4, 4.1, 7.9, 7.6, 17.3, 5.4, 38.5],
      ['Day 6', 9.4, 0, 10.9, 4, 7.5, 7.6, 20.2, 5.2, 35.2],
      ['Day 7', 6.7, 0, 11.2, 4.1, 5.9, 5.7, 21.2, 5.4, 39.8],
      ['Final boss', 0, 80.8, 13.6, 5.6, 0, 0, 0, 0, 0]
    ]
  }
};

// ---------------------------------------------------------------------------
// Web app entry points
// ---------------------------------------------------------------------------

function doPost(e) {
  try {
    var body = e && e.postData && e.postData.contents != null ? String(e.postData.contents) : '';
    if (!body) return reply_({ ok: false, error: 'empty body' });
    if (body.length > MAX_BODY) return reply_({ ok: false, error: 'body too large' });

    var data;
    try {
      data = JSON.parse(body);
    } catch (parseError) {
      return reply_({ ok: false, error: 'invalid json' });
    }

    var beta = data && typeof data === 'object' && !Array.isArray(data) && typeof data.event === 'string' ?
      rowEventOf_(data.event) : null;
    if (beta) {
      var betaResult = sanitizeBeta_(data, beta);
      if (betaResult.error) return reply_({ ok: false, error: betaResult.error });
      appendRows_(beta, betaResult.rows);
      return reply_({ ok: true });
    }

    var result = sanitizeEvent_(data);
    if (result.error) return reply_({ ok: false, error: result.error });

    appendRun_(result.row);
    return reply_({ ok: true });
  } catch (err) {
    console.error('doPost failed: ' + (err && err.stack ? err.stack : err));
    return reply_({ ok: false, error: 'server error' });
  }
}

function doGet() {
  var status = 'WARP ARENA analytics endpoint is running';
  try {
    var ss = openSpreadsheet_();
    var sheet = ss.getSheetByName(RUNS_SHEET);
    var rows = sheet ? Math.max(0, sheet.getLastRow() - 1) : 0;
    return reply_({ ok: true, status: status, rows: rows, beta_rows: dataRows_(ss, BETA_EVENTS), v2_rows: dataRows_(ss, V2_EVENTS) });
  } catch (err) {
    console.error('doGet failed: ' + (err && err.stack ? err.stack : err));
    return reply_({ ok: false, status: status, error: 'spreadsheet not found, run setup()' });
  }
}

// The beta or story-mode event with this name, or null for "runs" events and unknown names.
function rowEventOf_(event) {
  if (Object.prototype.hasOwnProperty.call(BETA_EVENTS, event)) return BETA_EVENTS[event];
  if (Object.prototype.hasOwnProperty.call(V2_EVENTS, event)) return V2_EVENTS[event];
  return null;
}

// Data rows (header left out) in each sheet of these events, by sheet name.
function dataRows_(ss, events) {
  var counts = {};
  Object.keys(events).forEach(function (event) {
    var name = events[event].sheet;
    var s = ss.getSheetByName(name);
    counts[name] = s ? Math.max(0, s.getLastRow() - 1) : 0;
  });
  return counts;
}

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

// Returns { row: [...] } in HEADER order, or { error: '...' }.
function sanitizeEvent_(data) {
  if (!data || typeof data !== 'object' || Array.isArray(data)) return { error: 'not an object' };
  if (data.schema !== 1) return { error: 'bad schema' };

  var event = cleanText_(data.event);
  if (EVENTS.indexOf(event) < 0) return { error: 'bad event' };

  var runId = rawText_(data.run_id);
  if (!ID_PATTERN.test(runId)) return { error: 'bad run_id' };

  var sessionId = rawText_(data.session_id);
  if (!ID_PATTERN.test(sessionId)) return { error: 'bad session_id' };

  var edition = cleanText_(data.edition);
  if (EDITIONS.indexOf(edition) < 0) return { error: 'bad edition' };

  var mode = modeOf_(data);
  var story = mode === MODE_STORY;

  var outcome = '';
  var cause = '';
  if (event === 'run_end') {
    outcome = cleanText_(data.outcome);
    if (!Object.prototype.hasOwnProperty.call(CAUSES_BY_OUTCOME, outcome)) return { error: 'bad outcome' };
    cause = cleanText_(data.cause);
    // A victory has only one cause; any other cause that does not fit the outcome is stored as "unknown".
    if (outcome === 'victory') cause = 'victory';
    else if ((story ? storyCausesOf_(outcome) : CAUSES_BY_OUTCOME[outcome]).indexOf(cause) < 0) cause = 'unknown';
  }

  var stage;
  var wave;
  if (story) {
    // A story row keeps the campaign's own stage and its day, 0 (the tutorial) to MAX_DAY, in "wave".
    stage = v2Stage_(data.stage);
    wave = clampInt_(data.wave, 0, MAX_DAY, 0);
  } else {
    stage = cleanText_(data.stage);
    if (STAGES.indexOf(stage) < 0) stage = 'wave';
    wave = clampInt_(data.wave, 1, MAX_WAVE, 1);
    // A victory is only won against the final boss, so it always lands in the Boss bucket.
    if (outcome === 'victory') {
      stage = 'final_boss';
      wave = MAX_WAVE;
    }
  }

  var runSeconds = toNumber_(data.run_seconds, 0);
  runSeconds = Math.min(MAX_RUN_SECONDS, Math.max(0, Math.round(runSeconds * 10) / 10));

  var elite = data.elite === true || data.elite === 'true' || data.elite === 1 || data.elite === '1';

  return {
    row: [
      new Date(),
      event,
      runId,
      sessionId,
      edition,
      cleanText_(data.build),
      outcome,
      cause,
      cleanText_(data.enemy),
      elite,
      stage,
      wave,
      runSeconds,
      clampInt_(data.level, 1, MAX_LEVEL, 1),
      clampInt_(data.kills, 0, MAX_KILLS, 0),
      cleanText_(data.starting_weapon),
      mode
    ]
  };
}

// Exactly "story" is the story mode; anything else (missing, misspelt, another case) is the open mode.
function modeOf_(data) {
  return data.mode === MODE_STORY ? MODE_STORY : MODE_OPEN;
}

// The causes a story-mode run_end keeps: those of its outcome in the open mode, and for a defeat also the base falling
// (V2_END_CAUSES holds both lists).
function storyCausesOf_(outcome) {
  var causes = CAUSES_BY_OUTCOME[outcome];
  return V2_END_CAUSES.filter(function (cause) {
    return causes.indexOf(cause) >= 0 || (outcome === 'defeat' && CAUSES.indexOf(cause) < 0);
  });
}

// ---------------------------------------------------------------------------
// Beta validation
// ---------------------------------------------------------------------------

// The six fields every event starts with. Returns { cells: [...] } or { error: '...' }.
function sanitizeCommon_(data) {
  if (data.schema !== 1) return { error: 'bad schema' };
  var event = cleanText_(data.event);
  var runId = rawText_(data.run_id);
  if (!ID_PATTERN.test(runId)) return { error: 'bad run_id' };
  var sessionId = rawText_(data.session_id);
  if (!ID_PATTERN.test(sessionId)) return { error: 'bad session_id' };
  var edition = cleanText_(data.edition);
  if (EDITIONS.indexOf(edition) < 0) return { error: 'bad edition' };
  return { cells: [new Date(), event, runId, sessionId, edition, cleanText_(data.build)] };
}

// Returns { rows: [[...], ...] } in the event's header order, or { error: '...' }.
function sanitizeBeta_(data, beta) {
  var common = sanitizeCommon_(data);
  if (common.error) return common;
  return beta.sanitize(data, common.cells);
}

function toBool_(value) {
  return value === true || value === 'true' || value === 1 || value === '1';
}

// "Weapon:Rifle:Power" -> "Weapon". Anything else is "other".
function cardKind_(id) {
  var kind = String(id || '').split(':')[0];
  return CARD_KINDS.indexOf(kind) >= 0 ? kind : 'other';
}

function sanitizeUpgrade_(data, cells) {
  var context = cleanText_(data.context);
  if (UPGRADE_CONTEXTS.indexOf(context) < 0) return { error: 'bad context' };
  var offers = [cleanText_(data.offer_1), cleanText_(data.offer_2), cleanText_(data.offer_3)];
  var picked = cleanText_(data.picked);
  if (!picked) return { error: 'bad picked' };
  var weaponOffered = offers.filter(function (o) { return cardKind_(o) === 'Weapon'; }).length;
  return {
    rows: [cells.concat([
      context,
      clampInt_(data.wave, 1, MAX_WAVE, 1),
      clampInt_(data.level, 1, MAX_LEVEL, 1),
      offers[0], offers[1], offers[2],
      picked,
      cardKind_(picked),
      weaponOffered,
      toBool_(data.rerolled)
    ])]
  };
}

function sanitizeSupply_(data, cells) {
  var action = cleanText_(data.action);
  if (SUPPLY_ACTIONS.indexOf(action) < 0) return { error: 'bad action' };
  var takes = SUPPLY_TAKES.indexOf(action) >= 0;
  var picked = takes ? cleanText_(data.picked) : '';
  if (takes && !picked) return { error: 'bad picked' };
  return {
    rows: [cells.concat([
      clampInt_(data.wave, 1, MAX_WAVE, 1),
      clampInt_(data.level, 1, MAX_LEVEL, 1),
      cleanText_(data.offer_1), cleanText_(data.offer_2), cleanText_(data.offer_3),
      action,
      picked,
      action === 'replace' ? cleanText_(data.replaced) : '',
      clampInt_(data.replace_cancels, 0, MAX_CANCELS, 0)
    ])]
  };
}

function waveBucket_(stage, wave) {
  if (stage === 'final_boss') return BOSS_BUCKET;
  return 'W' + (wave < 10 ? '0' : '') + wave;
}

// A story-mode damage row's bucket: its day, D00 (the tutorial) to D100.
function dayBucket_(day) {
  return 'D' + (day < 10 ? '0' : '') + day;
}

function round1_(value, max) {
  var n = toNumber_(value, 0);
  return Math.min(max, Math.max(0, Math.round(n * 10) / 10));
}

function sanitizeDamage_(data, cells) {
  var list = data.rows;
  if (!Array.isArray(list) || list.length === 0) return { error: 'bad rows' };
  if (list.length > MAX_DAMAGE_ROWS) return { error: 'too many rows' };
  // The mode is the event's: every row of one wave_damage event belongs to the same run.
  var mode = modeOf_(data);
  var rows = [];
  for (var i = 0; i < list.length; i++) {
    var r = list[i];
    if (!r || typeof r !== 'object' || Array.isArray(r)) return { error: 'bad rows' };
    var stage;
    var wave;
    var bucket;
    if (mode === MODE_STORY) {
      // As in "runs": the campaign's own stage, the day (0 to MAX_DAY) in "wave", and a day bucket.
      stage = v2Stage_(r.stage);
      wave = clampInt_(r.wave, 0, MAX_DAY, 0);
      bucket = dayBucket_(wave);
    } else {
      stage = cleanText_(r.stage);
      if (STAGES.indexOf(stage) < 0) stage = 'wave';
      wave = clampInt_(r.wave, 1, MAX_WAVE, 1);
      if (stage === 'final_boss') wave = MAX_WAVE;
      bucket = waveBucket_(stage, wave);
    }
    rows.push(cells.concat([
      stage,
      wave,
      bucket,
      cleanText_(r.source) || 'other',
      toBool_(r.dot),
      round1_(r.damage, MAX_DAMAGE),
      round1_(r.damage_pct, MAX_DAMAGE_PCT),
      clampInt_(r.hits, 0, MAX_HITS, 0),
      mode
    ]));
  }
  return { rows: rows };
}

// ---------------------------------------------------------------------------
// Story-mode validation
// ---------------------------------------------------------------------------

// The day number every story-mode row needs: a number, clamped to 0..MAX_DAY, or null when it is missing or not a number.
function v2Day_(value) {
  if (toNumber_(value, null) === null) return null;
  return clampInt_(value, 0, MAX_DAY, 0);
}

function v2Stage_(value) {
  var stage = cleanText_(value);
  return V2_STAGES.indexOf(stage) >= 0 ? stage : 'other';
}

function amount_(value) {
  return clampInt_(value, 0, MAX_CURRENCY, 0);
}

// A day that reached dawn ended at dawn: its end_stage and end_cause stay empty. A day that did not is the run's last one,
// with the run's stage and cause at its end (an unknown cause is stored as "unknown").
function sanitizeV2Day_(data, cells) {
  var day = v2Day_(data.day);
  if (day === null) return { error: 'bad day' };
  var dawn = toBool_(data.reached_dawn);
  var endCause = dawn ? '' : cleanText_(data.end_cause);
  if (!dawn && V2_END_CAUSES.indexOf(endCause) < 0) endCause = 'unknown';
  return {
    rows: [cells.concat([
      day,
      toBool_(data.overtime),
      dawn,
      dawn ? '' : v2Stage_(data.end_stage),
      endCause,
      clampInt_(data.kills, 0, MAX_KILLS, 0),
      round1_(data.player_damage, MAX_DAMAGE),
      round1_(data.player_damage_pct, MAX_DAMAGE_PCT),
      clampInt_(data.player_hits, 0, MAX_HITS, 0),
      round1_(data.base_damage, MAX_DAMAGE),
      round1_(data.base_damage_hp, MAX_DAMAGE),
      round1_(data.base_hp, MAX_DAMAGE),
      round1_(data.base_shield, MAX_DAMAGE),
      round1_(data.base_max_hp, MAX_DAMAGE),
      amount_(data.earned_alloy), amount_(data.earned_energy), amount_(data.earned_dark),
      amount_(data.spent_alloy), amount_(data.spent_energy), amount_(data.spent_dark),
      amount_(data.alloy), amount_(data.energy), amount_(data.dark),
      toBool_(data.night_death),
      amount_(data.lost_alloy), amount_(data.lost_energy), amount_(data.lost_dark),
      clampInt_(data.shadows, 0, MAX_COUNT, 0),
      clampInt_(data.overrun_regions, 0, MAX_COUNT, 0),
      clampInt_(data.repairs, 0, MAX_COUNT, 0)
    ])]
  };
}

function sanitizeV2Purchase_(data, cells) {
  var day = v2Day_(data.day);
  if (day === null) return { error: 'bad day' };
  var item = cleanText_(data.item);
  if (!item) return { error: 'bad item' };
  return {
    rows: [cells.concat([
      day,
      v2Stage_(data.stage),
      item,
      cleanText_(data.detail),
      cleanText_(data.room),
      amount_(data.alloy), amount_(data.energy), amount_(data.dark)
    ])]
  };
}

// value: seconds, a share, a day number, a place or a price, depending on the kind; rounded to 2 decimals, never negative.
function sanitizeV2Event_(data, cells) {
  var day = v2Day_(data.day);
  if (day === null) return { error: 'bad day' };
  var kind = cleanText_(data.kind);
  if (!Object.prototype.hasOwnProperty.call(V2_EVENT_DETAILS, kind)) return { error: 'bad kind' };
  var detail = cleanText_(data.detail);
  var accepts = V2_EVENT_DETAILS[kind];
  if (accepts === 'required' ? !detail : Array.isArray(accepts) && accepts.indexOf(detail) < 0) return { error: 'bad detail' };
  var value = toNumber_(data.value, 0);
  return {
    rows: [cells.concat([
      day,
      v2Stage_(data.stage),
      kind,
      detail,
      Math.min(MAX_V2_VALUE, Math.max(0, Math.round(value * 100) / 100))
    ])]
  };
}

// Keeps letters, digits, spaces and . _ : + - only, and drops a leading + or -,
// so a posted value can never be entered as a formula by appendRow.
function cleanText_(value) {
  if (value === null || value === undefined || typeof value === 'object') return '';
  var text = String(value).replace(/[^A-Za-z0-9 _.:+\-]/g, '').trim().replace(/^[+\-\s]+/, '');
  return text.slice(0, MAX_TEXT).trim();
}

function rawText_(value) {
  if (value === null || value === undefined || typeof value === 'object') return '';
  return String(value).trim().slice(0, MAX_TEXT);
}

function toNumber_(value, fallback) {
  if (value === null || value === undefined || value === '' || typeof value === 'boolean') return fallback;
  var n = Number(value);
  return isFinite(n) ? n : fallback;
}

function clampInt_(value, min, max, fallback) {
  var n = Math.round(toNumber_(value, fallback));
  return Math.min(max, Math.max(min, n));
}

// ---------------------------------------------------------------------------
// Sheet access
// ---------------------------------------------------------------------------

function appendRun_(row) {
  var lock = LockService.getScriptLock();
  lock.waitLock(LOCK_WAIT_MS);
  try {
    var ss = openSpreadsheet_();
    var sheet = ss.getSheetByName(RUNS_SHEET);
    if (!sheet) {
      sheet = ss.insertSheet(RUNS_SHEET);
      writeHeader_(sheet);
    }
    sheet.appendRow(row);
  } finally {
    lock.releaseLock();
  }
}

// Writes all rows of one beta or story-mode event in a single call, under the same script lock as "runs".
function appendRows_(beta, rows) {
  var lock = LockService.getScriptLock();
  lock.waitLock(LOCK_WAIT_MS);
  try {
    var ss = openSpreadsheet_();
    var sheet = ss.getSheetByName(beta.sheet);
    if (!sheet) {
      sheet = ss.insertSheet(beta.sheet);
      writeHeader_(sheet, beta.header, beta.text);
    }
    sheet.getRange(sheet.getLastRow() + 1, 1, rows.length, beta.header.length).setValues(rows);
  } finally {
    lock.releaseLock();
  }
}

// Opens the spreadsheet saved by setup(); a container-bound script falls back to its own sheet.
function openSpreadsheet_() {
  var id = PropertiesService.getScriptProperties().getProperty(SPREADSHEET_ID_KEY);
  if (id) return SpreadsheetApp.openById(id);
  var active = SpreadsheetApp.getActiveSpreadsheet();
  if (active) return active;
  throw new Error('No spreadsheet configured. Run setup() first.');
}

// header and textColumns default to the "runs" sheet.
function writeHeader_(sheet, header, textColumns) {
  header = header || HEADER;
  textColumns = textColumns || TEXT_COLUMNS;
  if (sheet.getLastRow() > 0) {
    var first = sheet.getRange(1, 1, 1, header.length).getValues()[0];
    // Keep any data already in row 1 by pushing it down.
    if (String(first[0]) !== header[0]) sheet.insertRowBefore(1);
  }
  sheet.getRange(1, 1, 1, header.length).setValues([header]).setFontWeight('bold');
  sheet.setFrozenRows(1);
  textColumns.forEach(function (a1) {
    sheet.getRange(a1).setNumberFormat('@');
  });
}

function reply_(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj)).setMimeType(ContentService.MimeType.JSON);
}

// ---------------------------------------------------------------------------
// One-time setup (run by hand from the Apps Script editor)
// ---------------------------------------------------------------------------

// Safe to run again: it rebuilds its own two tabs, "Dashboard" and "calc", with their charts, deletes the summary tabs of its
// earlier layouts, never removes or moves a row of a data tab, and leaves every other tab of the spreadsheet as it is.
function setup() {
  var props = PropertiesService.getScriptProperties();
  var ss = SpreadsheetApp.getActiveSpreadsheet();
  if (!ss) {
    var storedId = props.getProperty(SPREADSHEET_ID_KEY);
    ss = storedId ? SpreadsheetApp.openById(storedId) : SpreadsheetApp.create('WARP ARENA analytics');
  }
  props.setProperty(SPREADSHEET_ID_KEY, ss.getId());

  var runs = ss.getSheetByName(RUNS_SHEET);
  if (!runs) {
    var sheets = ss.getSheets();
    if (sheets.length === 1 && sheets[0].getLastRow() === 0) {
      runs = sheets[0];
      runs.setName(RUNS_SHEET);
    } else {
      runs = ss.insertSheet(RUNS_SHEET);
    }
  }
  writeHeader_(runs);
  [BETA_EVENTS, V2_EVENTS].forEach(function (events) {
    Object.keys(events).forEach(function (event) {
      var kind = events[event];
      writeHeader_(ss.getSheetByName(kind.sheet) || ss.insertSheet(kind.sheet), kind.header, kind.text);
    });
  });

  var built = buildCalc_(ss);
  var dashboard = buildDashboard_(ss, built);
  OLD_SUMMARY_SHEETS.forEach(function (name) {
    var old = ss.getSheetByName(name);
    if (old) ss.deleteSheet(old);
  });
  // The Dashboard is the first tab and the one shown after setup(); calc is out of sight, since nobody reads it.
  ss.setActiveSheet(dashboard);
  ss.moveActiveSheet(1);
  built.sheet.hideSheet();
  Logger.log('Analytics spreadsheet ready: ' + ss.getUrl());
}

// One of the two tabs this script owns, emptied of its charts, contents, formats and merged cells, or made when it is missing.
function ownSheet_(ss, name) {
  var sheet = ss.getSheetByName(name);
  if (!sheet) return ss.insertSheet(name);
  sheet.getCharts().forEach(function (chart) {
    sheet.removeChart(chart);
  });
  sheet.getRange(1, 1, sheet.getMaxRows(), sheet.getMaxColumns()).breakApart();
  sheet.clear();
  return sheet;
}

// ---------------------------------------------------------------------------
// Formulas over the data tabs
// ---------------------------------------------------------------------------

// Column letters from a column number: "A" to "Z", then "AA" and on.
function letters_(n) {
  var letters = '';
  while (n > 0) {
    var m = (n - 1) % 26;
    letters = String.fromCharCode(65 + m) + letters;
    n = Math.floor((n - 1) / 26);
  }
  return letters;
}

// The column letters of a field in a data sheet, from its header.
function columnOf_(header, name) {
  var n = header.indexOf(name) + 1;
  if (n < 1) throw new Error('No column "' + name + '"');
  return letters_(n);
}

// A cell as a formula names it, fixed: cell_(5, 2) is "$B$5".
function cell_(row, column) {
  return '$' + letters_(column) + '$' + row;
}

// A whole column of a data sheet, for COUNTIFS and its kin: "v2_days!$P:$P".
function columnRef_(sheetName, header, name) {
  var column = columnOf_(header, name);
  return sheetName + '!$' + column + ':$' + column;
}

// The same column from row 2 down, for FILTER: "v2_purchases!$C$2:$C".
function dataRef_(sheetName, header, name) {
  var column = columnOf_(header, name);
  return sheetName + '!$' + column + '$2:$' + column;
}

// "range,criterion" pairs for COUNTIFS, SUMIFS and MINIFS, from [[field name, criterion], ...].
function criteria_(sheetName, header, pairs) {
  return pairs.map(function (pair) {
    return columnRef_(sheetName, header, pair[0]) + ',' + pair[1];
  }).join(',');
}

// The day rows of the story tables: Day 0 (the tutorial) to Day STORY_DAYS, then one row for every overtime day together.
// criterion is what a COUNTIFS puts on the day column.
function storyDayRows_() {
  var rows = [];
  for (var day = 0; day <= STORY_DAYS; day++) rows.push({ label: 'Day ' + day, criterion: String(day) });
  rows.push({ label: 'Day ' + (STORY_DAYS + 1) + '+', criterion: '">' + STORY_DAYS + '"' });
  return rows;
}

// Every value the groups of a chart list under key ("causes" or "sources"), in one list.
function listed_(groups, key) {
  return groups.reduce(function (all, group) { return all.concat(group[key]); }, []);
}

// The criteria that pick the rows of one group of a chart: a list of pairs for each value the group names, or, with values
// null, one list that leaves out every value in listed (the "other" group). A formula adds up one term for each list.
function groupCriteria_(field, values, listed) {
  if (values) return values.map(function (value) { return [[field, '"' + value + '"']]; });
  return [listed.map(function (value) { return [field, '"<>' + value + '"']; })];
}

// Every count below names its event or kind first. Besides picking the rows, that keeps the header row and the empty rows
// under the data out of the "<>" criteria, which those rows would match.

// ----- Card 1, from "runs": the run_end rows with mode "story"

function storyEndCriteria_(pairs) {
  return criteria_(RUNS_SHEET, HEADER, [['event', '"run_end"'], ['edition', '"<>test"'], ['mode', '"' + MODE_STORY + '"']].concat(pairs));
}

// Story run_end rows of one cause group (causes null: OTHER_GROUP) on the days dayCriterion names; a story row keeps its day
// in "wave". A run that ended in the final battle is counted under that battle and on no day: dayCriterion null counts
// those, one COUNTIFS for each of the battle's stages.
function storyEndFormula_(causes, dayCriterion) {
  var places = dayCriterion === null ?
    STORY_FINAL_STAGES.map(function (stage) { return [['stage', '"' + stage + '"']]; }) :
    [STORY_FINAL_STAGES.map(function (stage) { return ['stage', '"<>' + stage + '"']; }).concat([['wave', dayCriterion]])];
  var terms = [];
  groupCriteria_('cause', causes, listed_(END_GROUPS, 'causes')).forEach(function (cause) {
    places.forEach(function (place) {
      terms.push('COUNTIFS(' + storyEndCriteria_(cause.concat(place)) + ')');
    });
  });
  return '=' + terms.join('+');
}

// Distinct story runs that sent run_start. FILTER keeps a "runs" row only when a row of its run_id says "story", so a run
// whose run_start went out as open and its later rows as story is counted, whole.
// ROWS(UNIQUE(...)), not COUNTUNIQUE: when FILTER matches nothing it returns #N/A, and COUNTUNIQUE counts that error as one
// value, while ROWS(UNIQUE(#N/A)) stays an error that IFERROR turns into 0.
function storyRunsStartedFormula_() {
  return '=IFERROR(ROWS(UNIQUE(FILTER(runs!$C$2:$C,runs!$B$2:$B="run_start",runs!$E$2:$E<>"test",' +
    'COUNTIFS(runs!$C$2:$C,runs!$C$2:$C,runs!$Q$2:$Q,"story")>0))),0)';
}

// ----- Card 2, from "v2_purchases"

function purchaseCriteria_(pairs) {
  return criteria_(V2_PURCHASES_SHEET, V2_PURCHASE_HEADER, [['event', '"v2_purchase"'], ['edition', '"<>test"']].concat(pairs));
}

// The criteria that pick one room's purchases, or with OTHER_ROOMS those of every room STORY_ROOMS does not name.
function roomCriteria_(room) {
  if (room !== OTHER_ROOMS) return [['room', '"' + room + '"']];
  return STORY_ROOMS.map(function (r) { return ['room', '"<>' + r + '"']; });
}

// Everything the matching purchases cost: alloy, energy and dark matter added up, a unit of each counting the same.
function purchaseSpentFormula_(pairs) {
  return '=' + CURRENCIES.map(function (currency) {
    return 'SUMIFS(' + columnRef_(V2_PURCHASES_SHEET, V2_PURCHASE_HEADER, currency) + ',' + purchaseCriteria_(pairs) + ')';
  }).join('+');
}

// One row for each run that bought anything, holding its first purchase: run_id, day, room, item. The rows of "v2_purchases"
// are in the order they arrived, and MATCH finds the first row of a run_id, so a row is kept when that first row is itself.
function firstPurchasesFormula_() {
  var column = function (name) { return dataRef_(V2_PURCHASES_SHEET, V2_PURCHASE_HEADER, name); };
  var ids = column('run_id');
  return '=IFERROR(FILTER({' + [ids, column('day'), column('room'), column('item')].join(',') + '},' + ids + '<>"",' +
    column('edition') + '<>"test",IFERROR(MATCH(' + ids + ',' + ids + ',0),0)=ROW(' + ids + ')-1),{"","","",""})';
}

// How many runs made their first purchase in this room, counted in the list firstPurchasesFormula_ fills: its first column
// holds the run_id, its third the room.
function firstPurchaseCountFormula_(room) {
  var from = function (offset) {
    var column = letters_(FIRST_PURCHASE_COLUMN + offset);
    return '$' + column + '$' + FIRST_PURCHASE_ROW + ':$' + column;
  };
  var rooms = room !== OTHER_ROOMS ? [room] : STORY_ROOMS.map(function (r) { return '<>' + r; });
  return '=COUNTIFS(' + from(0) + ',"?*",' + rooms.map(function (r) { return from(2) + ',"' + r + '"'; }).join(',') + ')';
}

// ----- Card 3, from "v2_events": the crate_offer and crate rows

function eventCriteria_(kind, pairs) {
  return criteria_(V2_EVENTS_SHEET, V2_EVENT_HEADER, [['kind', '"' + kind + '"'], ['edition', '"<>test"']].concat(pairs));
}

// crate_offer rows: one for each weapon shown on a crate's panel.
function crateOfferFormula_(pairs) {
  return '=COUNTIFS(' + eventCriteria_('crate_offer', pairs) + ')';
}

// crate rows (what the player did with a crate), counted from the first recorded offer on; sinceCell holds its time, or text
// while there is none. A crate opened by a build that did not record offers yet has a result and no offers, and counting it
// beside the offers would push every take rate up.
function crateResultFormula_(pairs, sinceCell) {
  return '=IF(ISNUMBER(' + sinceCell + '),COUNTIFS(' +
    eventCriteria_('crate', pairs.concat([['received_at', '">="&' + sinceCell]])) + '),0)';
}

// The time of the first recorded offer, or a text while there is none.
function firstOfferFormula_() {
  return '=IF(' + crateOfferFormula_([]).slice(1) + '=0,"no offers recorded yet",MINIFS(' +
    columnRef_(V2_EVENTS_SHEET, V2_EVENT_HEADER, 'received_at') + ',' + eventCriteria_('crate_offer', []) + '))';
}

// ----- Card 4, from "damage": the rows with mode "story"

function storyDamageCriteria_(pairs) {
  return criteria_(DAMAGE_SHEET, DAMAGE_HEADER,
    [['event', '"wave_damage"'], ['edition', '"<>test"'], ['mode', '"' + MODE_STORY + '"']].concat(pairs));
}

// The damage of one source group (sources null: OTHER_SOURCES) to the player on the days dayCriterion names, summed as % of
// max HP; a story row keeps its day in "wave". dayCriterion null is the fight with the final boss, which counts on no day.
function storyDamageFormula_(sources, dayCriterion) {
  var place = dayCriterion === null ? [['stage', '"' + STORY_FINAL_FIGHT + '"']] :
    [['stage', '"<>' + STORY_FINAL_FIGHT + '"'], ['wave', dayCriterion]];
  var summed = columnRef_(DAMAGE_SHEET, DAMAGE_HEADER, 'damage_pct');
  return '=' + groupCriteria_('source', sources, listed_(DAMAGE_GROUPS, 'sources')).map(function (source) {
    return 'SUMIFS(' + summed + ',' + storyDamageCriteria_(source.concat(place)) + ')';
  }).join('+');
}

// ----- The open mode, from "runs"

// Counts open-mode run_end rows (excluding test rows) with this cause in one wave, or in the final boss when wave is null.
// "<>story" also matches an empty mode, so rows written before the column existed still count.
function openEndFormula_(cause, wave) {
  var formula = '=COUNTIFS(runs!$B:$B,"run_end",runs!$E:$E,"<>test",runs!$Q:$Q,"<>story",runs!$H:$H,"' + cause + '"';
  if (wave === null) {
    formula += ',runs!$K:$K,"final_boss"';
  } else {
    formula += ',runs!$K:$K,"<>final_boss",runs!$L:$L,' + wave;
  }
  return formula + ')';
}

// ---------------------------------------------------------------------------
// "calc": the tables, one under another
// ---------------------------------------------------------------------------

// Starts a table at the cursor of calc: a one-cell title and the header row under it. Returns where the table stands
// ({ top: the header's row, first and last: its data rows, rows: how many, width: its columns }) and moves the cursor under
// it, one empty row left between two tables.
function startTable_(calc, cursor, title, header, rowCount) {
  var top = cursor.row + 1;
  calc.getRange(cursor.row, 1).setValue(title).setFontWeight('bold');
  calc.getRange(top, 1, 1, header.length).setValues([header]).setFontWeight('bold');
  cursor.row = top + rowCount + 2;
  return { top: top, first: top + 1, last: top + rowCount, rows: rowCount, width: header.length };
}

// A table of formulas: labels down the first column, and a grid of formulas right of them.
function liveTable_(calc, cursor, title, header, labels, formulas) {
  var table = startTable_(calc, cursor, title, header, labels.length);
  calc.getRange(table.first, 1, table.rows, 1).setValues(labels.map(function (label) { return [label]; }));
  calc.getRange(table.first, 2, table.rows, table.width - 1).setFormulas(formulas);
  return table;
}

// A table of fixed numbers: { header: [...], rows: [[...], ...] }.
function fixedTable_(calc, cursor, title, table) {
  var placed = startTable_(calc, cursor, title, table.header, table.rows.length);
  calc.getRange(placed.first, 1, placed.rows, placed.width).setValues(table.rows);
  return placed;
}

// An example table with one more column, all zeros, named as the catch-all series of the live chart, so that the example
// chart shows the same series as the live chart above it. The zeros are true of the simulated runs: the rows of the endings
// example add up to every run that ended, and every simulated purchase was made in one of the four rooms.
function withCatchAll_(table, label) {
  return {
    header: table.header.concat([label]),
    rows: table.rows.map(function (row) { return row.concat([0]); })
  };
}

// The cells of a table with its header: the range a chart reads.
function tableRange_(calc, table) {
  return calc.getRange(table.top, 1, table.rows + 1, table.width);
}

// One column of a table with its header cell.
function tableColumn_(calc, table, column) {
  return calc.getRange(table.top, column, table.rows + 1, 1);
}

// The numbers of a table, without its header and its label column, as a formula of calc names them: "B15:J24".
function bodyOf_(table) {
  return letters_(2) + table.first + ':' + letters_(table.width) + table.last;
}

// Builds calc and returns what the Dashboard needs: the sheet, the cell of each total (total.started is "$B$5") and where the
// tables of the charts stand.
function buildCalc_(ss) {
  var calc = ownSheet_(ss, CALC_SHEET);
  calc.getRange(1, 1).setValue(CALC_NOTE).setFontWeight('bold');
  var cursor = { row: 3 };
  var built = { sheet: calc, total: {} };

  // The totals stand first, so the tables under them can name the cell with the time of the first recorded offer. Their
  // formulas are written last: some add up a table further down.
  var totals = startTable_(calc, cursor, 'LIVE totals', ['Total', 'Value'], TOTALS.length);
  calc.getRange(totals.first, 1, totals.rows, 1).setValues(TOTALS.map(function (total) { return [total[1]]; }));
  TOTALS.forEach(function (total, i) {
    built.total[total[0]] = cell_(totals.first + i, 2);
  });

  calcEndings_(calc, cursor, built);
  calcSpending_(calc, cursor, built);
  calcWeapons_(calc, cursor, built);
  calcDamage_(calc, cursor, built);
  calcOpen_(calc, cursor, built);

  var formulas = {
    started: storyRunsStartedFormula_(),
    ended: '=SUM(' + bodyOf_(built.endings) + ')',
    purchases: '=COUNTIFS(' + purchaseCriteria_([]) + ')',
    crates: '=COUNTIFS(' + eventCriteria_('crate', []) + ')',
    since: firstOfferFormula_(),
    offered: '=SUM(B' + built.weaponCounts.first + ':B' + built.weaponCounts.last + ')',
    taken: '=SUM(C' + built.weaponCounts.first + ':C' + built.weaponCounts.last + ')',
    unlisted: crateOfferFormula_([]) + '-' + built.total.offered,
    hits: '=SUMIFS(' + columnRef_(DAMAGE_SHEET, DAMAGE_HEADER, 'hits') + ',' + storyDamageCriteria_([]) + ')',
    openEnded: '=SUM(' + bodyOf_(built.open) + ')'
  };
  calc.getRange(totals.first, 2, totals.rows, 1).setFormulas(TOTALS.map(function (total) { return [formulas[total[0]]]; }));
  calc.getRange(totals.first + TOTALS.map(function (total) { return total[0]; }).indexOf('since'), 2).setNumberFormat('yyyy-mm-dd hh:mm');
  return built;
}

function calcEndings_(calc, cursor, built) {
  var days = storyDayRows_();
  var groups = END_GROUPS.map(function (group) { return group.causes; }).concat([null]);
  var header = ['Day'].concat(END_GROUPS.map(function (group) { return group.label; }), [OTHER_GROUP]);
  built.endings = liveTable_(calc, cursor, 'LIVE 1: story runs ended on each day and in the final battle, by cause', header,
    days.map(function (d) { return d.label; }).concat([STORY_BOSS_ROW]),
    days.map(function (d) { return d.criterion; }).concat([null]).map(function (criterion) {
      return groups.map(function (causes) { return storyEndFormula_(causes, criterion); });
    }));
  built.endingsExample = fixedTable_(calc, cursor, 'EXAMPLE 1: the same table from simulated runs',
    withCatchAll_(STORY_EXAMPLES.endings, OTHER_GROUP));
}

function calcSpending_(calc, cursor, built) {
  var days = storyDayRows_();
  var rooms = STORY_ROOMS.concat([OTHER_ROOMS]);
  var labels = ROOM_LABELS.concat([OTHER_ROOMS]);
  built.spending = liveTable_(calc, cursor, 'LIVE 2: resources spent on each day, by room (alloy + energy + dark matter)',
    ['Day'].concat(labels), days.map(function (d) { return d.label; }), days.map(function (d) {
      return rooms.map(function (room) { return purchaseSpentFormula_(roomCriteria_(room).concat([['day', d.criterion]])); });
    }));

  // The first purchase of every run, right of the tables: the one list of calc that grows.
  calc.getRange(FIRST_PURCHASE_ROW - 2, FIRST_PURCHASE_COLUMN)
    .setValue('LIVE 2: the first purchase of each run (this list grows with the data)').setFontWeight('bold');
  calc.getRange(FIRST_PURCHASE_ROW - 1, FIRST_PURCHASE_COLUMN, 1, 4).setValues([['run_id', 'day', 'room', 'item']]).setFontWeight('bold');
  calc.getRange(FIRST_PURCHASE_ROW, FIRST_PURCHASE_COLUMN).setFormulas([[firstPurchasesFormula_()]]);
  var first = liveTable_(calc, cursor, 'LIVE 2: runs by the room of their first purchase', ['Room', 'Runs'], labels,
    rooms.map(function (room) { return [firstPurchaseCountFormula_(room)]; }));
  built.firstPurchase = rooms.map(function (room, i) { return cell_(first.first + i, 2); });

  var example = STORY_EXAMPLES.spending;
  built.spendingExample = fixedTable_(calc, cursor, 'EXAMPLE 2: the same table from simulated runs', withCatchAll_(example, OTHER_ROOMS));
  fixedTable_(calc, cursor, 'EXAMPLE 2: simulated runs by the room of their first purchase',
    { header: example.firstHeader, rows: example.firstRows });
}

function calcWeapons_(calc, cursor, built) {
  var since = built.total.since;
  var n = STORY_WEAPONS.length;

  // How often a crate showed each weapon, how often it was taken, and the share.
  var counts = startTable_(calc, cursor, 'LIVE 3: each weapon, offered by a crate and taken', ['Weapon', 'Offered', 'Taken', 'Take rate'], n);
  calc.getRange(counts.first, 1, n, 1).setValues(STORY_WEAPONS.map(function (weapon) { return [weapon]; }));
  calc.getRange(counts.first, 2, n, 3).setFormulas(STORY_WEAPONS.map(function (weapon, i) {
    var row = counts.first + i;
    var where = [['detail', '"*:' + weapon + '"']];
    return [crateOfferFormula_(where), crateResultFormula_(where, since), '=IFERROR(C' + row + '/B' + row + ',0)'];
  }));
  calc.getRange(counts.first, 4, n, 1).setNumberFormat('0.0%');
  built.weaponCounts = counts;

  // The same weapons with the most taken first, as the chart shows them. Each cell picks one value out of the sorted table,
  // so this table keeps its size whatever the data: a chart that reads the output of one spilling formula shows nothing
  // while that output is empty.
  var ranked = startTable_(calc, cursor, 'LIVE 3: take rate, the most taken weapon first (the chart reads this table)',
    ['Weapon', 'Take rate'], n);
  var sorted = 'SORT($A$' + counts.first + ':$D$' + counts.last + ',4,FALSE)';
  calc.getRange(ranked.first, 1, n, 2).setFormulas(STORY_WEAPONS.map(function (weapon, i) {
    return ['=INDEX(' + sorted + ',' + (i + 1) + ',1)', '=INDEX(' + sorted + ',' + (i + 1) + ',4)'];
  }));
  calc.getRange(ranked.first, 2, n, 1).setNumberFormat('0%');
  built.weapons = ranked;

  // Picks by the weapon's place on the panel. A weapon that did not fit (no_room) was picked but not taken, so it is left out.
  var places = [];
  for (var place = 1; place <= CRATE_PLACES; place++) places.push(place);
  var byPlace = startTable_(calc, cursor, 'LIVE 3: weapons by their place on the crate\'s panel',
    ['Place', 'Shown', 'Taken', 'Taken when shown'], CRATE_PLACES);
  calc.getRange(byPlace.first, 1, CRATE_PLACES, 1).setValues(places.map(function (p) { return ['Place ' + p]; }));
  calc.getRange(byPlace.first, 2, CRATE_PLACES, 3).setFormulas(places.map(function (p, i) {
    var row = byPlace.first + i;
    return [
      crateOfferFormula_([['value', String(p)]]),
      crateResultFormula_([['value', String(p)], ['detail', '"<>*:no_room"']], since),
      '=IFERROR(C' + row + '/B' + row + ',0)'
    ];
  }));
  calc.getRange(byPlace.first, 4, CRATE_PLACES, 1).setNumberFormat('0.0%');
  built.places = places.map(function (p, i) { return cell_(byPlace.first + i, 4); });

  // Crates left without taking a weapon, of those that could be left: every crate but FIRST_CRATE that had a weapon to show.
  var left = startTable_(calc, cursor, 'LIVE 3: crates left without taking a weapon', ['Crates', 'Could be left', 'Left', 'Share left'], 1);
  calc.getRange(left.first, 1).setValue('All but ' + FIRST_CRATE);
  calc.getRange(left.first, 2, 1, 3).setFormulas([[
    crateResultFormula_([['detail', '"<>' + FIRST_CRATE + ':*"'], ['detail', '"<>*:empty"']], since),
    crateResultFormula_([['detail', '"*:left"']], since),
    '=IFERROR(C' + left.first + '/B' + left.first + ',0)'
  ]]);
  calc.getRange(left.first, 4).setNumberFormat('0.0%');
  built.left = cell_(left.first, 4);

  built.weaponsExample = fixedTable_(calc, cursor, 'EXAMPLE 3: weapons offered and taken in simulated runs, the most taken first',
    STORY_EXAMPLES.weapons);
  calc.getRange(built.weaponsExample.first, 4, built.weaponsExample.rows, 1).setNumberFormat('0.0%');
}

function calcDamage_(calc, cursor, built) {
  var days = storyDayRows_();
  var groups = DAMAGE_GROUPS.map(function (group) { return group.sources; }).concat([null]);
  var header = ['Day'].concat(DAMAGE_GROUPS.map(function (group) { return group.label; }), [OTHER_SOURCES]);
  built.damage = liveTable_(calc, cursor, 'LIVE 4: damage to the player on each day and in the final fight, by source (% of max HP, summed)',
    header, days.map(function (d) { return d.label; }).concat([STORY_BOSS_ROW]),
    days.map(function (d) { return d.criterion; }).concat([null]).map(function (criterion) {
      return groups.map(function (sources) { return storyDamageFormula_(sources, criterion); });
    }));
  built.damageExample = fixedTable_(calc, cursor, 'EXAMPLE 4: each day\'s damage in simulated runs, by source (% of the day)',
    STORY_EXAMPLES.damage);
}

function calcOpen_(calc, cursor, built) {
  var labels = [];
  var formulas = [];
  for (var w = 1; w <= MAX_WAVE + 1; w++) {
    var wave = w <= MAX_WAVE ? w : null;
    labels.push(wave ? 'W' + wave : 'Boss');
    formulas.push(CAUSES.map(function (cause) { return openEndFormula_(cause, wave); }));
  }
  built.open = liveTable_(calc, cursor, 'LIVE open mode: runs ended on each wave and against the final boss, by cause',
    ['Wave'].concat(OPEN_CAUSE_LABELS), labels, formulas);
}

// ---------------------------------------------------------------------------
// "Dashboard": titles, captions and charts on a fixed grid
// ---------------------------------------------------------------------------

// A formula that writes a sentence: a text piece goes in as it is, a cell of calc as [address] or, shown through TEXT, as
// [address, format]. A count follows its noun ("purchases: 1"), so the sentence reads right for one as for many.
function sentence_(parts) {
  return '=' + parts.map(function (part) {
    if (typeof part === 'string') return '"' + part.replace(/"/g, '""') + '"';
    var ref = CALC_SHEET + '!' + part[0];
    return part.length > 1 ? 'TEXT(' + ref + ',"' + part[1] + '")' : ref;
  }).join('&');
}

// Merges the cells of one row from a column on and writes a text into them; a text that starts with "=" is a formula.
// Returns the merged range, for its format.
function line_(sheet, row, column, width, text) {
  var range = sheet.getRange(row, column, 1, width);
  range.merge();
  var first = sheet.getRange(row, column);
  if (text.charAt(0) === '=') first.setFormulas([[text]]);
  else first.setValue(text);
  return range;
}

function headerLine_(sheet, row, column, width, text) {
  return line_(sheet, row, column, width, text).setBackground(HEADER_FILL).setFontColor(HEADER_INK).setFontWeight('bold')
    .setFontSize(12).setVerticalAlignment('middle');
}

function captionLine_(sheet, row, column, width, text) {
  return line_(sheet, row, column, width, text).setFontColor(CAPTION_INK).setFontStyle('italic').setFontSize(9).setWrap(true)
    .setVerticalAlignment('top');
}

function labelLine_(sheet, row, column, text, fill) {
  return line_(sheet, row, column, CARD_COLUMNS, text).setBackground(fill).setFontWeight('bold').setFontSize(9)
    .setVerticalAlignment('middle');
}

// "Example: " before the title of a chart of simulated numbers.
function chartTitle_(title, example) {
  return example ? 'Example: ' + title.charAt(0).toLowerCase() + title.slice(1) : title;
}

// A stacked column chart of one table of calc: its first column along the horizontal axis and a series for each other
// column, in the colours given. With percent, every column is stretched to 100%.
function stackedColumns_(sheet, range, title, xTitle, yTitle, colours, percent) {
  var builder = sheet.newChart().asColumnChart().setStacked().setTitle(title).setXAxisTitle(xTitle).setYAxisTitle(yTitle)
    .setColors(colours).addRange(range).setNumHeaders(1);
  return percent ? builder.setOption('isStacked', 'percent') : builder;
}

// A horizontal bar chart of one share per row: the names in the first range, the shares in the second. One series needs no legend.
function shareBars_(sheet, names, shares, title, xTitle, colour) {
  return sheet.newChart().asBarChart().setTitle(title).setXAxisTitle(xTitle).setColors([colour])
    .setLegendPosition(Charts.Position.NONE).addRange(names).addRange(shares).setNumHeaders(1).setOption('hAxis.format', 'percent');
}

// Puts a chart with its top left corner on a cell, at an exact size in pixels.
function placeChart_(sheet, builder, row, column, width, height) {
  sheet.insertChart(builder.setOption('width', width).setOption('height', height).setPosition(row, column, 0, 0).build());
}

// One card: its header, its caption and its "LIVE DATA" label, the live chart, then the example label and the example chart.
// card.chart(example) gives the builder of either chart, so that the two differ in their table and their title only.
function writeCard_(sheet, band, column, card) {
  var height = band.chartRows * ROW_HEIGHT;
  headerLine_(sheet, band.header, column, CARD_COLUMNS, card.title);
  captionLine_(sheet, band.caption, column, CARD_COLUMNS, card.caption);
  labelLine_(sheet, band.liveLabel, column, card.live, LIVE_FILL);
  placeChart_(sheet, card.chart(false), band.live, column, CARD_WIDTH, height);
  labelLine_(sheet, band.exampleLabel, column, EXAMPLE_LABEL, EXAMPLE_FILL);
  placeChart_(sheet, card.chart(true), band.example, column, CARD_WIDTH, height);
}

// The sizes of the grid. A cleared tab keeps the sizes it had, so every size the layout rests on is set on each run.
function layOutDashboard_(sheet) {
  sheet.setHiddenGridlines(true);
  sheet.setColumnWidth(1, MARGIN_WIDTH);
  sheet.setColumnWidths(LEFT_CARD, CARD_COLUMNS, CARD_COLUMN_WIDTH);
  sheet.setColumnWidth(LEFT_CARD + CARD_COLUMNS, GUTTER_WIDTH);
  sheet.setColumnWidths(RIGHT_CARD, CARD_COLUMNS, CARD_COLUMN_WIDTH);
  sheet.setColumnWidth(RIGHT_CARD + CARD_COLUMNS, MARGIN_WIDTH);
  sheet.setRowHeights(1, DASHBOARD_LAST_ROW, ROW_HEIGHT);
  sheet.setRowHeight(TITLE_ROW, TITLE_ROW_HEIGHT);
  [UPPER_BAND, LOWER_BAND, OPEN_BAND].forEach(function (band) {
    sheet.setRowHeight(band.header, HEADER_ROW_HEIGHT);
  });
  [UPPER_BAND, LOWER_BAND].forEach(function (band) {
    sheet.setRowHeight(band.caption, CAPTION_ROW_HEIGHT);
  });
}

function buildDashboard_(ss, built) {
  var sheet = ownSheet_(ss, DASHBOARD_SHEET);
  var calc = built.sheet;
  var total = built.total;
  layOutDashboard_(sheet);

  line_(sheet, TITLE_ROW, LEFT_CARD, WIDE_COLUMNS, DASHBOARD_TITLE).setFontWeight('bold').setFontSize(18).setVerticalAlignment('middle');
  line_(sheet, INTRO_ROW, LEFT_CARD, WIDE_COLUMNS, DASHBOARD_INTRO).setFontColor(CAPTION_INK);
  line_(sheet, TOTALS_ROW, LEFT_CARD, WIDE_COLUMNS, sentence_(['Story runs started ', [total.started], DOT + 'ended ', [total.ended],
    DOT + 'purchases ', [total.purchases], DOT + 'crates opened ', [total.crates]]));

  writeCard_(sheet, UPPER_BAND, LEFT_CARD, {
    title: '1' + DOT + 'How runs end',
    caption: 'Runs ended on each day and in the final battle, by cause.',
    live: sentence_([LIVE_LABEL + DOT + 'story runs ended: ', [total.ended]]),
    chart: function (example) {
      return stackedColumns_(sheet, tableRange_(calc, example ? built.endingsExample : built.endings),
        chartTitle_('How story runs end, by day', example), 'Day', 'Runs ended', END_COLOURS, false);
    }
  });

  var firstPurchase = [];
  STORY_ROOMS.concat(['Other']).forEach(function (room, i) {
    firstPurchase.push((i ? DOT : 'First purchase of a run: ') + room + ' ', [built.firstPurchase[i]]);
  });
  writeCard_(sheet, UPPER_BAND, RIGHT_CARD, {
    title: '2' + DOT + 'Where the resources go',
    caption: sentence_(firstPurchase.concat(['. Chart: each day\'s spending by room.'])),
    live: sentence_([LIVE_LABEL + DOT + 'purchases: ', [total.purchases]]),
    chart: function (example) {
      return stackedColumns_(sheet, tableRange_(calc, example ? built.spendingExample : built.spending),
        chartTitle_('Resources spent per day, by room', example), 'Day', 'Alloy + energy + dark matter', ROOM_COLOURS, false);
    }
  });

  var byPlace = [];
  built.places.forEach(function (cell, i) {
    byPlace.push((i ? DOT : 'Taken by place: ') + PLACE_NAMES[i] + ' ', [cell, '0%']);
  });
  writeCard_(sheet, LOWER_BAND, LEFT_CARD, {
    title: '3' + DOT + 'Weapon choice',
    caption: sentence_(byPlace.concat([DOT + 'crates left ', [built.left, '0%'],
      '. Chart: how often each weapon is taken when a crate offers it.'])),
    live: sentence_([LIVE_LABEL + DOT + 'weapons offered: ', [total.offered], DOT + 'taken: ', [total.taken]]),
    chart: function (example) {
      // The live chart reads the ranked table, two columns side by side; the example table is ranked as it stands, and its
      // take rate is the fourth column.
      var table = example ? built.weaponsExample : built.weapons;
      return shareBars_(sheet, tableColumn_(calc, table, 1), tableColumn_(calc, table, table.width),
        chartTitle_('Take rate per weapon, when a crate offers it', example), 'Taken / offered', WEAPON_COLOUR);
    }
  });

  writeCard_(sheet, LOWER_BAND, RIGHT_CARD, {
    title: '4' + DOT + 'What hurts the player',
    caption: 'Share of each day\'s damage to the player, by source.',
    live: sentence_([LIVE_LABEL + DOT + 'hits on the player: ', [total.hits]]),
    chart: function (example) {
      return stackedColumns_(sheet, tableRange_(calc, example ? built.damageExample : built.damage),
        chartTitle_('Damage to the player per day, by source', example), 'Day', 'Share of the day\'s damage', DAMAGE_COLOURS, true);
    }
  });

  headerLine_(sheet, OPEN_BAND.header, LEFT_CARD, WIDE_COLUMNS, 'Open mode (' + (MAX_WAVE + 1) + ' waves)');
  captionLine_(sheet, OPEN_BAND.caption, LEFT_CARD, WIDE_COLUMNS,
    sentence_(['Runs ended on each wave, by cause. Total: ', [total.openEnded], '.']));
  placeChart_(sheet, stackedColumns_(sheet, tableRange_(calc, built.open), 'How open-mode runs end, by wave', 'Wave', 'Runs ended',
    OPEN_COLOURS, false), OPEN_BAND.chart, LEFT_CARD, WIDE_WIDTH, OPEN_BAND.chartRows * ROW_HEIGHT);
  return sheet;
}
