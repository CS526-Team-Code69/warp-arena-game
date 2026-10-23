/**
 * WARP ARENA analytics endpoint (Metric 2: how runs end).
 *
 * The WebGL build posts one JSON event per request as a text/plain body.
 * doPost validates and sanitizes it, then appends one row to the "runs" sheet.
 * Run setup() once from the editor before deploying this project as a web app.
 */

var RUNS_SHEET = 'runs';
var SUMMARY_SHEET = 'Summary';
var SPREADSHEET_ID_KEY = 'SPREADSHEET_ID';
var CHART_TITLE = 'How runs end, by wave';

var MAX_BODY = 4096;
var MAX_TEXT = 64;
var MAX_WAVE = 14;
var MAX_LEVEL = 12;
var MAX_KILLS = 1000000;
var MAX_RUN_SECONDS = 36000;
var LOCK_WAIT_MS = 10000;

var HEADER = [
  'received_at', 'event', 'run_id', 'session_id', 'edition', 'build',
  'outcome', 'cause', 'enemy', 'elite', 'stage', 'wave', 'run_seconds',
  'level', 'kills', 'starting_weapon'
];

// Column order of the Summary grid and the stacking order of the chart.
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
var TEXT_COLUMNS = ['B:I', 'K:K', 'P:P'];

var EVENTS = ['run_start', 'run_end'];
var EDITIONS = ['en', 'zh', 'test'];
var STAGES = ['wave', 'build', 'void_trial', 'final_boss'];
var ID_PATTERN = /^[0-9a-f]{32}$/;

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
    var sheet = openSpreadsheet_().getSheetByName(RUNS_SHEET);
    var rows = sheet ? Math.max(0, sheet.getLastRow() - 1) : 0;
    return reply_({ ok: true, status: status, rows: rows });
  } catch (err) {
    console.error('doGet failed: ' + (err && err.stack ? err.stack : err));
    return reply_({ ok: false, status: status, error: 'spreadsheet not found, run setup()' });
  }
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

  var outcome = '';
  var cause = '';
  if (event === 'run_end') {
    outcome = cleanText_(data.outcome);
    if (!Object.prototype.hasOwnProperty.call(CAUSES_BY_OUTCOME, outcome)) return { error: 'bad outcome' };
    cause = cleanText_(data.cause);
    // A victory has only one cause; any other cause that does not fit the outcome is stored as "unknown".
    if (outcome === 'victory') cause = 'victory';
    else if (CAUSES_BY_OUTCOME[outcome].indexOf(cause) < 0) cause = 'unknown';
  }

  var stage = cleanText_(data.stage);
  if (STAGES.indexOf(stage) < 0) stage = 'wave';
  var wave = clampInt_(data.wave, 1, MAX_WAVE, 1);
  // A victory is only won against the final boss, so it always lands in the Boss bucket.
  if (outcome === 'victory') {
    stage = 'final_boss';
    wave = MAX_WAVE;
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
      cleanText_(data.starting_weapon)
    ]
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

// Opens the spreadsheet saved by setup(); a container-bound script falls back to its own sheet.
function openSpreadsheet_() {
  var id = PropertiesService.getScriptProperties().getProperty(SPREADSHEET_ID_KEY);
  if (id) return SpreadsheetApp.openById(id);
  var active = SpreadsheetApp.getActiveSpreadsheet();
  if (active) return active;
  throw new Error('No spreadsheet configured. Run setup() first.');
}

function writeHeader_(sheet) {
  if (sheet.getLastRow() > 0) {
    var first = sheet.getRange(1, 1, 1, HEADER.length).getValues()[0];
    // Keep any data already in row 1 by pushing it down.
    if (String(first[0]) !== HEADER[0]) sheet.insertRowBefore(1);
  }
  sheet.getRange(1, 1, 1, HEADER.length).setValues([HEADER]).setFontWeight('bold');
  sheet.setFrozenRows(1);
  TEXT_COLUMNS.forEach(function (a1) {
    sheet.getRange(a1).setNumberFormat('@');
  });
}

function reply_(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj)).setMimeType(ContentService.MimeType.JSON);
}

// ---------------------------------------------------------------------------
// One-time setup (run by hand from the Apps Script editor)
// ---------------------------------------------------------------------------

// Safe to run again: rebuilds the Summary sheet and its chart, never removes rows from "runs".
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
  buildSummary_(ss);
  Logger.log('Analytics spreadsheet ready: ' + ss.getUrl());
}

function buildSummary_(ss) {
  var sheet = ss.getSheetByName(SUMMARY_SHEET);
  if (sheet) {
    sheet.getCharts().forEach(function (chart) {
      sheet.removeChart(chart);
    });
    sheet.clear();
  } else {
    sheet = ss.insertSheet(SUMMARY_SHEET);
  }

  // Grid: one row per wave bucket (waves 1..14, then the final boss), one column per cause.
  var bucketCount = MAX_WAVE + 1;
  var header = ['Wave'].concat(CAUSES);
  var labels = [];
  var formulas = [];
  for (var w = 1; w <= bucketCount; w++) {
    var wave = w <= MAX_WAVE ? w : null;
    labels.push([wave ? 'W' + wave : 'Boss']);
    formulas.push(CAUSES.map(function (cause) {
      return countFormula_(cause, wave);
    }));
  }
  sheet.getRange(1, 1, 1, header.length).setValues([header]).setFontWeight('bold');
  sheet.getRange(2, 1, bucketCount, 1).setValues(labels);
  sheet.getRange(2, 2, bucketCount, CAUSES.length).setFormulas(formulas);
  sheet.setFrozenRows(1);

  // Totals below the grid. Runs are counted by distinct run_id; test rows are left out.
  var top = bucketCount + 3;
  sheet.getRange(top, 1, 3, 1)
    .setValues([['Runs started'], ['Runs ended'], ['Unfinished (tab closed)']])
    .setFontWeight('bold');
  sheet.getRange(top, 2, 3, 1).setFormulas([
    [distinctRunsFormula_('run_start')],
    [distinctRunsFormula_('run_end')],
    [unfinishedRunsFormula_()]
  ]);
  sheet.getRange(top + 4, 1).setValue('This sheet is rebuilt by setup(); edits here are overwritten.');

  var chart = sheet.newChart()
    .asColumnChart()
    .setStacked()
    .addRange(sheet.getRange(1, 1, bucketCount + 1, header.length))
    .setNumHeaders(1)
    .setTitle(CHART_TITLE)
    .setXAxisTitle('Wave')
    .setYAxisTitle('Runs ended')
    .setPosition(1, header.length + 2, 0, 0)
    .build();
  sheet.insertChart(chart);
}

// Counts run_end rows (excluding test rows) with this cause in one wave, or in the final boss when wave is null.
function countFormula_(cause, wave) {
  var formula = '=COUNTIFS(runs!$B:$B,"run_end",runs!$E:$E,"<>test",runs!$H:$H,"' + cause + '"';
  if (wave === null) {
    formula += ',runs!$K:$K,"final_boss"';
  } else {
    formula += ',runs!$K:$K,"<>final_boss",runs!$L:$L,' + wave;
  }
  return formula + ')';
}

function distinctRunsFormula_(event) {
  return '=IFERROR(COUNTUNIQUE(FILTER(runs!$C$2:$C,runs!$B$2:$B="' + event + '",runs!$E$2:$E<>"test")),0)';
}

// Distinct runs that sent run_start and never sent run_end (the tab was closed).
function unfinishedRunsFormula_() {
  return '=IFERROR(COUNTUNIQUE(FILTER(runs!$C$2:$C,runs!$B$2:$B="run_start",runs!$E$2:$E<>"test",' +
    'COUNTIFS(runs!$C$2:$C,runs!$C$2:$C,runs!$B$2:$B,"run_end")=0)),0)';
}
