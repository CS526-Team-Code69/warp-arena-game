/**
 * WARP ARENA analytics endpoint.
 *
 * Metric 2, how runs end: run_start / run_end events, one row each in the "runs" sheet.
 * Beta metrics: upgrade_pick -> "upgrade_picks", supply_pick -> "supply_picks",
 * wave_damage -> "damage" (one row per damage source in the event).
 *
 * The WebGL build posts one JSON event per request as a text/plain body.
 * doPost validates and sanitizes it, then appends its rows to the event's sheet.
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

// ---------------------------------------------------------------------------
// Beta metrics: upgrade picks at level-up, supply drop choices, damage sources
// ---------------------------------------------------------------------------

var UPGRADE_SHEET = 'upgrade_picks';
var SUPPLY_SHEET = 'supply_picks';
var DAMAGE_SHEET = 'damage';
var UPGRADE_SUMMARY = 'Upgrade picks';
var SUPPLY_SUMMARY = 'Supply picks';
var DAMAGE_SUMMARY = 'Damage sources';

var COMMON_HEADER = ['received_at', 'event', 'run_id', 'session_id', 'edition', 'build'];
// A..P: G context, H wave, I level, J..L offers, M picked, N picked_kind, O weapon_offered, P rerolled.
var UPGRADE_HEADER = COMMON_HEADER.concat([
  'context', 'wave', 'level', 'offer_1', 'offer_2', 'offer_3', 'picked', 'picked_kind', 'weapon_offered', 'rerolled'
]);
// A..O: G wave, H level, I..K offers, L action, M picked, N replaced, O replace_cancels.
var SUPPLY_HEADER = COMMON_HEADER.concat([
  'wave', 'level', 'offer_1', 'offer_2', 'offer_3', 'action', 'picked', 'replaced', 'replace_cancels'
]);
// A..N: G stage, H wave, I bucket, J source, K dot, L damage, M damage_pct, N hits.
var DAMAGE_HEADER = COMMON_HEADER.concat([
  'stage', 'wave', 'bucket', 'source', 'dot', 'damage', 'damage_pct', 'hits'
]);

var UPGRADE_TEXT_COLUMNS = ['B:G', 'J:N'];
var SUPPLY_TEXT_COLUMNS = ['B:F', 'I:N'];
var DAMAGE_TEXT_COLUMNS = ['B:G', 'I:J'];

var UPGRADE_CONTEXTS = ['levelup', 'build'];
var UPGRADE_AXES = ['Power', 'Tempo', 'Reach', 'Infusion', 'Twist', 'Tame'];
var CARD_KINDS = ['Weapon', 'Skill', 'Stat'];
var SUPPLY_ACTIONS = ['take_free', 'merge', 'replace', 'skip', 'timeout'];
var SUPPLY_TAKES = ['take_free', 'merge', 'replace'];
var MAX_DAMAGE_ROWS = 24;
var MAX_DAMAGE = 10000000;
var MAX_DAMAGE_PCT = 1000000;
var MAX_HITS = 1000000;
var MAX_CANCELS = 100;
var DAMAGE_SOURCE_COLUMNS = 40; // room for every enemy kind, its elite and the other sources in the by-wave pivot
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

    var beta = data && typeof data === 'object' && !Array.isArray(data) && typeof data.event === 'string' &&
      Object.prototype.hasOwnProperty.call(BETA_EVENTS, data.event) ? BETA_EVENTS[data.event] : null;
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
    var betaRows = {};
    Object.keys(BETA_EVENTS).forEach(function (event) {
      var name = BETA_EVENTS[event].sheet;
      var s = ss.getSheetByName(name);
      betaRows[name] = s ? Math.max(0, s.getLastRow() - 1) : 0;
    });
    return reply_({ ok: true, status: status, rows: rows, beta_rows: betaRows });
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

function round1_(value, max) {
  var n = toNumber_(value, 0);
  return Math.min(max, Math.max(0, Math.round(n * 10) / 10));
}

function sanitizeDamage_(data, cells) {
  var list = data.rows;
  if (!Array.isArray(list) || list.length === 0) return { error: 'bad rows' };
  if (list.length > MAX_DAMAGE_ROWS) return { error: 'too many rows' };
  var rows = [];
  for (var i = 0; i < list.length; i++) {
    var r = list[i];
    if (!r || typeof r !== 'object' || Array.isArray(r)) return { error: 'bad rows' };
    var stage = cleanText_(r.stage);
    if (STAGES.indexOf(stage) < 0) stage = 'wave';
    var wave = clampInt_(r.wave, 1, MAX_WAVE, 1);
    if (stage === 'final_boss') wave = MAX_WAVE;
    rows.push(cells.concat([
      stage,
      wave,
      waveBucket_(stage, wave),
      cleanText_(r.source) || 'other',
      toBool_(r.dot),
      round1_(r.damage, MAX_DAMAGE),
      round1_(r.damage_pct, MAX_DAMAGE_PCT),
      clampInt_(r.hits, 0, MAX_HITS, 0)
    ]));
  }
  return { rows: rows };
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

// Writes all rows of one beta event in a single call, under the same script lock as "runs".
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

  Object.keys(BETA_EVENTS).forEach(function (event) {
    var beta = BETA_EVENTS[event];
    writeHeader_(ss.getSheetByName(beta.sheet) || ss.insertSheet(beta.sheet), beta.header, beta.text);
  });
  buildUpgradeSummary_(ss);
  buildSupplySummary_(ss);
  buildDamageSummary_(ss);
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

// ROWS(UNIQUE(...)), not COUNTUNIQUE: when FILTER matches nothing it returns #N/A, and COUNTUNIQUE counts that error
// as one value (seen in the live sheet), while ROWS(UNIQUE(#N/A)) stays an error that IFERROR turns into 0.
function distinctRunsFormula_(event) {
  return '=IFERROR(ROWS(UNIQUE(FILTER(runs!$C$2:$C,runs!$B$2:$B="' + event + '",runs!$E$2:$E<>"test"))),0)';
}

// ---------------------------------------------------------------------------
// Beta summaries (rebuilt by setup(); live formulas over the data sheets)
// ---------------------------------------------------------------------------

// Clears a summary sheet and its charts, or creates it.
function resetSheet_(ss, name) {
  var sheet = ss.getSheetByName(name);
  if (sheet) {
    sheet.getCharts().forEach(function (chart) {
      sheet.removeChart(chart);
    });
    sheet.clear();
  } else {
    sheet = ss.insertSheet(name);
  }
  return sheet;
}

var NOT_TEST = function (sheetName) { return sheetName + '!$E:$E,"<>test"'; };

// Times a card matching pattern was offered: the three offer columns J, K, L of "upgrade_picks".
function upgradeOfferedFormula_(pattern) {
  return '=' + ['J', 'K', 'L'].map(function (c) {
    return 'COUNTIFS(' + UPGRADE_SHEET + '!$' + c + ':$' + c + ',"' + pattern + '",' + NOT_TEST(UPGRADE_SHEET) + ')';
  }).join('+');
}

function upgradePickedFormula_(pattern) {
  return '=COUNTIFS(' + UPGRADE_SHEET + '!$M:$M,"' + pattern + '",' + NOT_TEST(UPGRADE_SHEET) + ')';
}

// Rows top..top+labels.length: label, offered, picked, pick rate.
function writeRateTable_(sheet, top, title, labels, patterns) {
  sheet.getRange(top, 1, 1, 4).setValues([[title, 'Offered', 'Picked', 'Pick rate']]).setFontWeight('bold');
  sheet.getRange(top + 1, 1, labels.length, 1).setValues(labels.map(function (l) { return [l]; }));
  sheet.getRange(top + 1, 2, labels.length, 3).setFormulas(patterns.map(function (p, i) {
    var row = top + 1 + i;
    return [upgradeOfferedFormula_(p), upgradePickedFormula_(p), '=IFERROR(C' + row + '/B' + row + ',0)'];
  }));
  sheet.getRange(top + 1, 4, labels.length, 1).setNumberFormat('0.0%');
}

function buildUpgradeSummary_(ss) {
  var sheet = resetSheet_(ss, UPGRADE_SUMMARY);
  var axes = UPGRADE_AXES.concat(['All weapon upgrades']);
  writeRateTable_(sheet, 1, 'Upgrade direction', axes, UPGRADE_AXES.map(function (a) { return 'Weapon:*:' + a; }).concat(['Weapon:*']));
  var kindTop = axes.length + 3; // row 10
  writeRateTable_(sheet, kindTop, 'Card type', CARD_KINDS, CARD_KINDS.map(function (k) { return k + ':*'; }));

  var shareTop = kindTop + CARD_KINDS.length + 2; // row 15
  sheet.getRange(shareTop, 1, 3, 1).setValues([
    ['Picks with a weapon upgrade on offer'],
    ['Of which a weapon upgrade was picked'],
    ['Weapon upgrade share']
  ]).setFontWeight('bold');
  sheet.getRange(shareTop, 2, 3, 1).setFormulas([
    ['=COUNTIFS(' + UPGRADE_SHEET + '!$O:$O,">0",' + NOT_TEST(UPGRADE_SHEET) + ')'],
    ['=COUNTIFS(' + UPGRADE_SHEET + '!$O:$O,">0",' + UPGRADE_SHEET + '!$N:$N,"Weapon",' + NOT_TEST(UPGRADE_SHEET) + ')'],
    ['=IFERROR(B' + (shareTop + 1) + '/B' + shareTop + ',0)']
  ]);
  sheet.getRange(shareTop + 2, 2, 1, 1).setNumberFormat('0.0%');
  sheet.getRange(shareTop + 4, 1).setValue('Level-up and build-phase drafts; void-trial rewards are not counted. Rebuilt by setup(); edits here are overwritten.');
  sheet.setFrozenRows(1);

  sheet.insertChart(sheet.newChart()
    .asColumnChart()
    .addRange(sheet.getRange(1, 1, UPGRADE_AXES.length + 1, 1))
    .addRange(sheet.getRange(1, 4, UPGRADE_AXES.length + 1, 1))
    .setNumHeaders(1)
    .setTitle('Weapon upgrade pick rate, by direction')
    .setXAxisTitle('Upgrade direction')
    .setYAxisTitle('Picked / offered')
    .setOption('vAxis.format', 'percent')
    .setPosition(1, 6, 0, 0)
    .build());
  sheet.insertChart(sheet.newChart()
    .asColumnChart()
    .addRange(sheet.getRange(kindTop, 1, CARD_KINDS.length + 1, 1))
    .addRange(sheet.getRange(kindTop, 4, CARD_KINDS.length + 1, 1))
    .setNumHeaders(1)
    .setTitle('Level-up pick rate, by card type')
    .setXAxisTitle('Card type')
    .setYAxisTitle('Picked / offered')
    .setOption('vAxis.format', 'percent')
    .setPosition(20, 6, 0, 0)
    .build());
}

// A QUERY over a data sheet; if it has no rows yet the header alone is shown.
function queryFormula_(sheetName, lastColumn, query, header) {
  return '=IFERROR(QUERY(' + sheetName + '!$A$2:$' + lastColumn + ',"' + query + '",0),{"' + header.join('","') + '"})';
}

function buildSupplySummary_(ss) {
  var sheet = resetSheet_(ss, SUPPLY_SUMMARY);
  var n = SUPPLY_ACTIONS.length;
  sheet.getRange(1, 1, 1, 3).setValues([['Choice', 'Count', 'Share']]).setFontWeight('bold');
  sheet.getRange(2, 1, n, 1).setValues(SUPPLY_ACTIONS.map(function (a) { return [a]; }));
  sheet.getRange(2, 2, n, 2).setFormulas(SUPPLY_ACTIONS.map(function (a, i) {
    return ['=COUNTIFS(' + SUPPLY_SHEET + '!$L:$L,"' + a + '",' + NOT_TEST(SUPPLY_SHEET) + ')', '=IFERROR(B' + (i + 2) + '/$B$' + (n + 2) + ',0)'];
  }));
  sheet.getRange(n + 2, 1).setValue('Total').setFontWeight('bold');
  sheet.getRange(n + 2, 2).setFormulas([['=SUM(B2:B' + (n + 1) + ')']]);
  sheet.getRange(2, 3, n, 1).setNumberFormat('0.0%');
  sheet.getRange(n + 4, 1).setValue('Replace screen cancelled').setFontWeight('bold');
  sheet.getRange(n + 4, 2).setFormulas([['=SUMIFS(' + SUPPLY_SHEET + '!$O:$O,' + NOT_TEST(SUPPLY_SHEET) + ')']]);
  sheet.getRange(n + 6, 1).setValue('Rebuilt by setup(); edits here are overwritten.');

  // Which weapons get replaced, and which weapons are taken at all.
  sheet.getRange(1, 5).setFormulas([[queryFormula_(SUPPLY_SHEET, 'O',
    "select N, count(N) where L = 'replace' and E <> 'test' and N <> '' group by N order by count(N) desc label N 'Weapon replaced', count(N) 'Times replaced'",
    ['Weapon replaced', 'Times replaced'])]]);
  sheet.getRange(1, 8).setFormulas([[queryFormula_(SUPPLY_SHEET, 'O',
    "select M, count(M) where E <> 'test' and M <> '' group by M order by count(M) desc label M 'Weapon taken', count(M) 'Times taken'",
    ['Weapon taken', 'Times taken'])]]);
  sheet.setFrozenRows(1);

  sheet.insertChart(sheet.newChart()
    .asPieChart()
    .addRange(sheet.getRange(1, 1, n + 1, 2))
    .setNumHeaders(1)
    .setTitle('Supply drop choices')
    .setPosition(1, 11, 0, 0)
    .build());
  sheet.insertChart(sheet.newChart()
    .asBarChart()
    .addRange(sheet.getRange(1, 5, 18, 2))
    .setNumHeaders(1)
    .setTitle('Weapons replaced most often')
    .setXAxisTitle('Times replaced')
    .setPosition(20, 11, 0, 0)
    .build());
}

function buildDamageSummary_(ss) {
  var sheet = resetSheet_(ss, DAMAGE_SUMMARY);
  var rows = DAMAGE_SOURCE_COLUMNS;
  // A..C: totals per source, largest first.
  sheet.getRange(1, 1).setFormulas([[queryFormula_(DAMAGE_SHEET, 'N',
    "select J, sum(M), sum(N) where E <> 'test' and J <> '' group by J order by sum(M) desc label J 'Source', sum(M) 'Damage (% of max HP)', sum(N) 'Hits'",
    ['Source', 'Damage (% of max HP)', 'Hits'])]]);
  // D: share of all damage; E: the part of it that was burn or poison.
  sheet.getRange(1, 4, 1, 2).setValues([['Share', 'Of which burn / poison']]).setFontWeight('bold');
  var formulas = [];
  for (var r = 2; r <= rows + 1; r++) {
    formulas.push([
      '=IF($A' + r + '="","",IFERROR($B' + r + '/SUM($B$2:$B$' + (rows + 1) + '),0))',
      '=IF($A' + r + '="","",SUMIFS(' + DAMAGE_SHEET + '!$M:$M,' + DAMAGE_SHEET + '!$J:$J,$A' + r + ',' + DAMAGE_SHEET + '!$K:$K,TRUE,' + NOT_TEST(DAMAGE_SHEET) + '))'
    ]);
  }
  sheet.getRange(2, 4, rows, 2).setFormulas(formulas);
  sheet.getRange(2, 4, rows, 1).setNumberFormat('0.0%');

  // H onward: damage per wave bucket (W01..W14, then the final boss) and source.
  sheet.getRange(1, 8).setFormulas([[queryFormula_(DAMAGE_SHEET, 'N',
    "select I, sum(M) where E <> 'test' and J <> '' group by I pivot J label I 'Wave'",
    ['Wave'])]]);
  sheet.getRange(rows + 3, 1).setValue('Damage is summed as % of the player\'s max HP at the time of each hit. Rebuilt by setup(); edits here are overwritten.');
  sheet.setFrozenRows(1);

  sheet.insertChart(sheet.newChart()
    .asBarChart()
    .addRange(sheet.getRange(1, 1, rows + 1, 2))
    .setNumHeaders(1)
    .setTitle('Damage taken, by source')
    .setXAxisTitle('Damage (% of max HP)')
    .setPosition(rows + 5, 1, 0, 0) // below the tables, left
    .build());
  sheet.insertChart(sheet.newChart()
    .asColumnChart()
    .setStacked()
    .addRange(sheet.getRange(1, 8, MAX_WAVE + 2, rows + 1))
    .setNumHeaders(1)
    .setTitle('Damage sources, by wave')
    .setXAxisTitle('Wave')
    .setYAxisTitle('Share of damage taken')
    .setOption('isStacked', 'percent')
    .setPosition(rows + 5, 8, 0, 0) // below the tables, under the by-wave pivot
    .build());
}

// Distinct runs that sent run_start and never sent run_end (the tab was closed).
function unfinishedRunsFormula_() {
  return '=IFERROR(ROWS(UNIQUE(FILTER(runs!$C$2:$C,runs!$B$2:$B="run_start",runs!$E$2:$E<>"test",' +
    'COUNTIFS(runs!$C$2:$C,runs!$C$2:$C,runs!$B$2:$B,"run_end")=0))),0)';
}
