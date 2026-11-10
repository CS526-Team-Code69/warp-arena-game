'use strict';
// Tests for Code.gs. Plain node, no packages: node Analytics/apps-script/test/run-tests.js
// Code.gs runs in a vm context with small mocks of the Apps Script services it uses.

const fs = require('fs');
const path = require('path');
const vm = require('vm');
const assert = require('assert');

const CODE = fs.readFileSync(path.join(__dirname, '..', 'Code.gs'), 'utf8');

const HEADER = [
  'received_at', 'event', 'run_id', 'session_id', 'edition', 'build',
  'outcome', 'cause', 'enemy', 'elite', 'stage', 'wave', 'run_seconds',
  'level', 'kills', 'starting_weapon', 'mode'
];
const CAUSES = [
  'enemy', 'tyrant', 'backfire', 'wall_slam', 'status', 'black_hole',
  'void_trial', 'unknown', 'victory', 'restart', 'quit_to_title'
];
const ALL_SHEETS = ['runs', 'Summary', 'upgrade_picks', 'supply_picks', 'damage', 'Upgrade picks', 'Supply picks', 'Damage sources',
  'v2_days', 'v2_purchases', 'v2_events'];
const UPGRADE_HEADER = ['received_at', 'event', 'run_id', 'session_id', 'edition', 'build',
  'context', 'wave', 'level', 'offer_1', 'offer_2', 'offer_3', 'picked', 'picked_kind', 'weapon_offered', 'rerolled'];
const SUPPLY_HEADER = ['received_at', 'event', 'run_id', 'session_id', 'edition', 'build',
  'wave', 'level', 'offer_1', 'offer_2', 'offer_3', 'action', 'picked', 'replaced', 'replace_cancels'];
const DAMAGE_HEADER = ['received_at', 'event', 'run_id', 'session_id', 'edition', 'build',
  'stage', 'wave', 'bucket', 'source', 'dot', 'damage', 'damage_pct', 'hits', 'mode'];
const BUCKETS = ['W1', 'W2', 'W3', 'W4', 'W5', 'W6', 'W7', 'W8', 'W9', 'W10', 'W11', 'W12', 'W13', 'W14', 'Boss'];
const COL = { received_at: 0 };
HEADER.forEach((name, i) => { COL[name] = i; });

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

function isEmpty(v) {
  return v === undefined || v === null || v === '';
}

function colLetter(n) {
  let s = '';
  while (n > 0) {
    const m = (n - 1) % 26;
    s = String.fromCharCode(65 + m) + s;
    n = Math.floor((n - 1) / 26);
  }
  return s;
}

class MockRange {
  constructor(sheet, row, col, numRows, numCols) {
    for (const v of [row, col, numRows, numCols]) {
      if (!Number.isInteger(v) || v < 1) throw new Error('Bad range argument: ' + v);
    }
    this.sheet = sheet;
    this.row = row;
    this.col = col;
    this.numRows = numRows;
    this.numCols = numCols;
  }
  checkShape(grid) {
    if (!grid || grid.length !== this.numRows) throw new Error('Row count does not match range ' + this.getA1Notation());
    for (const r of grid) {
      if (!r || r.length !== this.numCols) throw new Error('Column count does not match range ' + this.getA1Notation());
    }
  }
  setValues(values) {
    this.checkShape(values);
    values.forEach((r, i) => r.forEach((v, j) => this.sheet.setCell(this.row + i, this.col + j, v, '')));
    return this;
  }
  setFormulas(formulas) {
    this.checkShape(formulas);
    formulas.forEach((r, i) => r.forEach((f, j) => this.sheet.setCell(this.row + i, this.col + j, '', f)));
    return this;
  }
  setValue(value) {
    return this.setValues([[value]]);
  }
  getValues() {
    const out = [];
    for (let i = 0; i < this.numRows; i++) {
      const r = [];
      for (let j = 0; j < this.numCols; j++) r.push(this.sheet.getCell(this.row + i, this.col + j).value);
      out.push(r);
    }
    return out;
  }
  setFontWeight() {
    return this;
  }
  setNumberFormat(format) {
    this.numberFormat = format;
    return this;
  }
  getA1Notation() {
    return colLetter(this.col) + this.row + ':' + colLetter(this.col + this.numCols - 1) + (this.row + this.numRows - 1);
  }
}

function colNumber(letters) {
  let n = 0;
  for (const ch of letters) n = n * 26 + (ch.charCodeAt(0) - 64);
  return n;
}

// Whole-column range from A1 notation such as "B:I".
class MockColumnRange {
  constructor(sheet, a1) {
    const m = /^([A-Z]+):([A-Z]+)$/.exec(a1);
    if (!m) throw new Error('Only whole-column A1 notation is supported by the mock: ' + a1);
    this.sheet = sheet;
    this.first = colNumber(m[1]);
    this.last = colNumber(m[2]);
    if (this.last < this.first) throw new Error('Bad column range: ' + a1);
  }
  setNumberFormat(format) {
    for (let c = this.first; c <= this.last; c++) this.sheet.columnFormats[c] = format;
    return this;
  }
}

// appendRow parses a string the way Sheets parses typed input, unless the column is plain text.
const NUMBER_TEXT = /^\s*[+-]?(\d+\.?\d*|\.\d+)(e[+-]?\d+)?\s*$/i;

class MockChartBuilder {
  constructor(sheet) {
    this.sheet = sheet;
    this.spec = { type: 'DEFAULT', stacked: false, ranges: [], numHeaders: null, title: null, position: null, options: {} };
  }
  addRange(range) {
    if (!(range instanceof MockRange)) throw new Error('addRange expects a Range');
    this.spec.ranges.push(range.getA1Notation());
    return this;
  }
  setNumHeaders(n) { this.spec.numHeaders = n; return this; }
  setOption(k, v) { this.spec.options[k] = v; return this; }
  setPosition(row, col, offsetX, offsetY) {
    if ([row, col, offsetX, offsetY].some((v) => !Number.isInteger(v))) throw new Error('setPosition expects four integers');
    this.spec.position = { row, col, offsetX, offsetY };
    return this;
  }
  asColumnChart() {
    this.spec.type = 'COLUMN';
    return new MockColumnChartBuilder(this);
  }
  asBarChart() {
    this.spec.type = 'BAR';
    return new MockColumnChartBuilder(this);
  }
  asPieChart() {
    this.spec.type = 'PIE';
    return new MockPieChartBuilder(this);
  }
  build() {
    if (!this.spec.ranges.length) throw new Error('Chart has no range');
    return { spec: JSON.parse(JSON.stringify(this.spec)) };
  }
}

// Only the column builder has setStacked / setTitle / axis titles, as in Apps Script.
class MockColumnChartBuilder extends MockChartBuilder {
  constructor(base) {
    super(base.sheet);
    this.spec = base.spec;
  }
  setStacked() { this.spec.stacked = true; return this; }
  setTitle(t) { this.spec.title = t; return this; }
  setXAxisTitle(t) { this.spec.xTitle = t; return this; }
  setYAxisTitle(t) { this.spec.yTitle = t; return this; }
}

// The pie builder has setTitle but no stacking or axis titles.
class MockPieChartBuilder extends MockChartBuilder {
  constructor(base) {
    super(base.sheet);
    this.spec = base.spec;
  }
  setTitle(t) { this.spec.title = t; return this; }
}

class MockSheet {
  constructor(env, name) {
    this.env = env;
    this.name = name;
    this.cells = []; // cells[row - 1][col - 1] = { value, formula }
    this.charts = [];
    this.frozenRows = 0;
    this.appendsWithoutLock = 0;
    this.columnFormats = {}; // column number -> number format
  }
  getName() { return this.name; }
  setName(n) { this.name = n; return this; }
  setCell(row, col, value, formula) {
    while (this.cells.length < row) this.cells.push([]);
    const r = this.cells[row - 1];
    while (r.length < col) r.push({ value: '', formula: '' });
    r[col - 1] = { value: isEmpty(value) ? '' : value, formula: formula || '' };
  }
  getCell(row, col) {
    const r = this.cells[row - 1];
    return (r && r[col - 1]) || { value: '', formula: '' };
  }
  getLastRow() {
    for (let i = this.cells.length; i > 0; i--) {
      if (this.cells[i - 1].some((c) => !isEmpty(c.value) || c.formula)) return i;
    }
    return 0;
  }
  getRange(row, col, numRows, numCols) {
    if (typeof row === 'string') return new MockColumnRange(this, row);
    return new MockRange(this, row, col, numRows === undefined ? 1 : numRows, numCols === undefined ? 1 : numCols);
  }
  appendRow(row) {
    if (!this.env.lock.held) this.appendsWithoutLock++;
    const at = this.getLastRow() + 1;
    Array.from(row).forEach((v, j) => {
      const plainText = this.columnFormats[j + 1] === '@';
      const value = typeof v === 'string' && !plainText && NUMBER_TEXT.test(v) ? Number(v) : v;
      this.setCell(at, j + 1, value, '');
    });
    return this;
  }
  insertRowBefore(row) {
    this.cells.splice(row - 1, 0, []);
    return this;
  }
  clear() {
    this.cells = [];
    return this;
  }
  setFrozenRows(n) { this.frozenRows = n; }
  getCharts() { return this.charts.slice(); }
  removeChart(chart) {
    const i = this.charts.indexOf(chart);
    if (i < 0) throw new Error('Chart not on this sheet');
    this.charts.splice(i, 1);
  }
  insertChart(chart) { this.charts.push(chart); }
  newChart() { return new MockChartBuilder(this); }
  // Test helpers
  rowValues(row) {
    const r = this.cells[row - 1] || [];
    return r.map((c) => c.value);
  }
  dataRows() {
    const out = [];
    for (let i = 2; i <= this.getLastRow(); i++) out.push(this.rowValues(i));
    return out;
  }
}

class MockSpreadsheet {
  constructor(env, id) {
    this.env = env;
    this.id = id;
    this.sheets = [];
  }
  getId() { return this.id; }
  getUrl() { return 'https://docs.example.invalid/' + this.id; }
  getSheets() { return this.sheets.slice(); }
  getSheetByName(name) { return this.sheets.find((s) => s.name === name) || null; }
  insertSheet(name) {
    if (this.getSheetByName(name)) throw new Error('A sheet with the name "' + name + '" already exists.');
    const s = new MockSheet(this.env, name);
    this.sheets.push(s);
    return s;
  }
}

// opts.bound: container-bound script (getActiveSpreadsheet returns the spreadsheet).
function makeEnv(opts) {
  opts = Object.assign({ bound: true }, opts);
  const env = {
    props: {},
    registry: {},
    logs: [],
    errors: [],
    lock: { held: false, waits: [], releases: 0, fail: false },
  };
  env.ss = new MockSpreadsheet(env, 'sheet-main');
  env.ss.insertSheet('Sheet1');
  env.registry[env.ss.id] = env.ss;

  const lock = {
    waitLock(ms) {
      env.lock.waits.push(ms);
      if (env.lock.fail) throw new Error('Lock timeout');
      env.lock.held = true;
    },
    releaseLock() {
      env.lock.releases++;
      env.lock.held = false;
    },
  };

  const sandbox = {
    SpreadsheetApp: {
      openById(id) {
        if (!env.registry[id]) throw new Error('Spreadsheet not found: ' + id);
        return env.registry[id];
      },
      getActiveSpreadsheet() { return opts.bound ? env.ss : null; },
      create(name) {
        const s = new MockSpreadsheet(env, 'sheet-created-' + Object.keys(env.registry).length);
        s.title = name;
        s.insertSheet('Sheet1');
        env.registry[s.id] = s;
        env.created = s;
        return s;
      },
    },
    PropertiesService: {
      getScriptProperties() {
        return {
          getProperty(k) { return Object.prototype.hasOwnProperty.call(env.props, k) ? env.props[k] : null; },
          setProperty(k, v) { env.props[k] = String(v); return this; },
        };
      },
    },
    LockService: { getScriptLock() { return lock; } },
    ContentService: {
      MimeType: { JSON: 'JSON', TEXT: 'TEXT' },
      createTextOutput(content) {
        return {
          content: content,
          mime: null,
          setMimeType(m) { this.mime = m; return this; },
          getContent() { return this.content; },
        };
      },
    },
    Logger: { log(m) { env.logs.push(String(m)); } },
    console: { log() {}, warn() {}, error(m) { env.errors.push(String(m)); } },
  };
  vm.createContext(sandbox);
  vm.runInContext(CODE, sandbox, { filename: 'Code.gs' });
  env.gs = sandbox;
  return env;
}

function readyEnv() {
  const env = makeEnv();
  env.gs.setup();
  return env;
}

function runsSheet(env, ss) {
  return (ss || env.ss).getSheetByName('runs');
}

const RUN_ID = '0123456789abcdef0123456789abcdef';
const SESSION_ID = 'fedcba9876543210fedcba9876543210';

function endEvent(overrides) {
  return Object.assign({
    schema: 1,
    event: 'run_end',
    run_id: RUN_ID,
    session_id: SESSION_ID,
    edition: 'en',
    build: '1.0.0+3fa2b1c9',
    outcome: 'defeat',
    cause: 'enemy',
    enemy: 'Spitter',
    elite: false,
    stage: 'wave',
    wave: 9,
    run_seconds: 512.3,
    level: 7,
    kills: 183,
    starting_weapon: 'Rifle',
  }, overrides);
}

function startEvent(overrides) {
  return endEvent(Object.assign({
    event: 'run_start', outcome: '', cause: '', enemy: '', wave: 1, run_seconds: 0, level: 1, kills: 0,
  }, overrides));
}

function post(env, body) {
  const contents = typeof body === 'string' ? body : JSON.stringify(body);
  const out = env.gs.doPost({ postData: { contents: contents, type: 'text/plain', length: contents.length } });
  return { mime: out.mime, json: JSON.parse(out.getContent()) };
}

function lastRow(env) {
  const sheet = runsSheet(env);
  return sheet.rowValues(sheet.getLastRow());
}

function isDate(v) {
  return Object.prototype.toString.call(v) === '[object Date]' && !isNaN(v.getTime());
}

// Minimal evaluator for the Summary formulas, used to check them against real rows.
function criteriaOf(formula) {
  const out = {};
  const re = /runs!\$([A-Z]):\$\1,("([^"]*)"|-?\d+)/g;
  let m;
  while ((m = re.exec(formula))) out[m[1]] = m[3] !== undefined ? m[3] : Number(m[2]);
  return out;
}

function matches(cell, criterion) {
  if (typeof criterion === 'number') return typeof cell === 'number' && cell === criterion;
  if (criterion.startsWith('<>')) return String(cell).toLowerCase() !== criterion.slice(2).toLowerCase();
  return typeof cell === 'string' && cell.toLowerCase() === criterion.toLowerCase();
}

function evalCountifs(formula, runs) {
  const crit = criteriaOf(formula);
  const rows = [];
  for (let i = 1; i <= runs.getLastRow(); i++) rows.push(runs.rowValues(i)); // includes the header, like $B:$B
  return rows.filter((r) => Object.keys(crit).every((c) => matches(r[c.charCodeAt(0) - 65], crit[c]))).length;
}

function evalDistinct(formula, runs) {
  const m = /^=IFERROR\(ROWS\(UNIQUE\(FILTER\(runs!\$C\$2:\$C,runs!\$B\$2:\$B="(\w+)",runs!\$E\$2:\$E<>"test",COUNTIFS\(runs!\$C\$2:\$C,runs!\$C\$2:\$C,runs!\$Q\$2:\$Q,"story"\)=0(?:,COUNTIFS\(runs!\$C\$2:\$C,runs!\$C\$2:\$C,runs!\$B\$2:\$B,"(\w+)"\)=0)?\)\)\),0\)$/.exec(formula);
  if (!m) throw new Error('Unexpected distinct formula: ' + formula);
  const rows = runs.dataRows();
  // A run_id with any row in the story mode is left out whole.
  const story = new Set(rows.filter((r) => r[COL.mode] === 'story').map((r) => r[COL.run_id]));
  // The optional COUNTIFS(...)=0 term drops every run_id that has a row with that event, in any edition.
  const excluded = new Set(m[2] ? rows.filter((r) => r[COL.event] === m[2]).map((r) => r[COL.run_id]) : []);
  const ids = new Set(rows
    .filter((r) => r[COL.event] === m[1] && r[COL.edition] !== 'test' && !story.has(r[COL.run_id]) && !excluded.has(r[COL.run_id]))
    .map((r) => r[COL.run_id]));
  return ids.size;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

const tests = [];
function test(name, fn) { tests.push({ name, fn }); }

test('valid run_end is appended once, in column order, with sanitized values', () => {
  const env = readyEnv();
  const res = post(env, endEvent({ build: '  1.0.0+3fa2b1c9  ', elite: 'true', wave: '9', run_seconds: 512.34, enemy: ' Spitter ' }));
  assert.strictEqual(res.mime, 'JSON');
  assert.deepStrictEqual(res.json, { ok: true });
  const sheet = runsSheet(env);
  assert.strictEqual(sheet.getLastRow(), 2, 'exactly one data row');
  const row = sheet.rowValues(2);
  assert.strictEqual(row.length, 17);
  assert.ok(isDate(row[0]), 'received_at is a Date');
  assert.ok(Math.abs(Date.now() - row[0].getTime()) < 5000, 'received_at is server time');
  assert.deepStrictEqual(row.slice(1), [
    'run_end', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9', 'defeat', 'enemy', 'Spitter',
    true, 'wave', 9, 512.3, 7, 183, 'Rifle', 'open',
  ]);
});

test('append happens inside the script lock (waitLock 10000, released)', () => {
  const env = readyEnv();
  post(env, endEvent());
  assert.deepStrictEqual(env.lock.waits, [10000]);
  assert.strictEqual(env.lock.releases, 1);
  assert.strictEqual(env.lock.held, false);
  assert.strictEqual(runsSheet(env).appendsWithoutLock, 0);
});

test('run_start with empty outcome and cause is accepted', () => {
  const env = readyEnv();
  const res = post(env, startEvent({ starting_weapon: 'Knife' }));
  assert.deepStrictEqual(res.json, { ok: true });
  const row = lastRow(env);
  assert.deepStrictEqual(row.slice(1), [
    'run_start', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9', '', '', '', false, 'wave', 1, 0, 1, 0, 'Knife', 'open',
  ]);
});

test('run_start never stores an outcome or cause', () => {
  const env = readyEnv();
  post(env, startEvent({ outcome: 'defeat', cause: 'enemy' }));
  const row = lastRow(env);
  assert.strictEqual(row[COL.outcome], '');
  assert.strictEqual(row[COL.cause], '');
});

test('bad requests are rejected with ok:false and nothing is appended', () => {
  const big = JSON.stringify(endEvent({ build: 'x'.repeat(5000) }));
  const cases = [
    ['', 'empty body'],
    ['{not json', 'invalid json'],
    ['[1,2,3]', 'not an object'],
    ['null', 'not an object'],
    ['"run_end"', 'not an object'],
    [big, 'body too large'],
    [endEvent({ schema: 2 }), 'bad schema'],
    [endEvent({ schema: '1' }), 'bad schema'],
    [endEvent({ schema: undefined }), 'bad schema'],
    [endEvent({ event: 'run_pause' }), 'bad event'],
    [endEvent({ event: '' }), 'bad event'],
    [endEvent({ run_id: RUN_ID.toUpperCase() }), 'bad run_id'],
    [endEvent({ run_id: RUN_ID.slice(1) }), 'bad run_id'],
    [endEvent({ run_id: RUN_ID + '0' }), 'bad run_id'],
    [endEvent({ run_id: undefined }), 'bad run_id'],
    [endEvent({ run_id: 12345 }), 'bad run_id'],
    [endEvent({ session_id: 'zz' + SESSION_ID.slice(2) }), 'bad session_id'],
    [endEvent({ edition: 'fr' }), 'bad edition'],
    [endEvent({ edition: '' }), 'bad edition'],
    [endEvent({ outcome: '' }), 'bad outcome'],
    [endEvent({ outcome: 'win' }), 'bad outcome'],
    [endEvent({ outcome: 'constructor' }), 'bad outcome'],
  ];
  assert.ok(big.length > 4096);
  for (const [body, error] of cases) {
    const env = readyEnv();
    const res = post(env, body);
    const label = typeof body === 'string' ? JSON.stringify(body.slice(0, 40)) : JSON.stringify(body).slice(0, 80);
    assert.strictEqual(res.mime, 'JSON', label);
    assert.strictEqual(res.json.ok, false, label);
    assert.strictEqual(res.json.error, error, label);
    assert.strictEqual(runsSheet(env).getLastRow(), 1, 'no row appended for ' + label);
  }
});

test('missing postData returns ok:false without throwing', () => {
  const env = readyEnv();
  for (const e of [undefined, null, {}, { postData: {} }, { postData: { contents: null } }]) {
    const out = env.gs.doPost(e);
    const json = JSON.parse(out.getContent());
    assert.strictEqual(json.ok, false);
  }
  assert.strictEqual(runsSheet(env).getLastRow(), 1);
});

test('numbers are clamped and rounded; stage and elite are normalized', () => {
  const env = readyEnv();
  const cases = [
    ['wave', 0, 1], ['wave', -3, 1], ['wave', 99, 14], ['wave', 3.6, 4], ['wave', '7', 7],
    ['wave', 'abc', 1], ['wave', null, 1], ['wave', true, 1],
    ['level', 50, 12], ['level', 0, 1], ['level', '5', 5], ['level', 11.4, 11],
    ['kills', -5, 0], ['kills', 7.6, 8], ['kills', 'x', 0], ['kills', 2e9, 1000000],
    ['run_seconds', -1, 0], ['run_seconds', 99999, 36000], ['run_seconds', 12.345, 12.3],
    ['run_seconds', '45.67', 45.7], ['run_seconds', 'abc', 0], ['run_seconds', Infinity, 0],
    ['stage', 'lobby', 'wave'], ['stage', '', 'wave'], ['stage', 'final_boss', 'final_boss'],
    ['stage', 'build', 'build'], ['stage', 'void_trial', 'void_trial'],
    ['elite', true, true], ['elite', 'true', true], ['elite', 1, true],
    ['elite', false, false], ['elite', 'false', false], ['elite', 0, false], ['elite', 'yes', false],
  ];
  for (const [field, input, expected] of cases) {
    const res = post(env, endEvent({ [field]: input }));
    assert.strictEqual(res.json.ok, true, field + '=' + input);
    assert.strictEqual(lastRow(env)[COL[field]], expected, field + '=' + String(input));
  }
});

test('unknown or mismatched causes become "unknown"; victory always stores "victory" in the Boss bucket', () => {
  const env = readyEnv();
  const cases = [
    ['defeat', 'lava', 'unknown'],
    ['defeat', '', 'unknown'],
    ['defeat', 'victory', 'unknown'],
    ['defeat', 'black_hole', 'black_hole'],
    ['defeat', 'tyrant', 'tyrant'],
    ['defeat', 'unknown', 'unknown'],
    ['abandoned', 'restart', 'restart'],
    ['abandoned', 'quit_to_title', 'quit_to_title'],
    ['abandoned', 'enemy', 'unknown'],
    ['victory', 'victory', 'victory'],
    ['victory', 'enemy', 'victory'],
    ['victory', '', 'victory'],
  ];
  for (const [outcome, cause, expected] of cases) {
    const res = post(env, endEvent({ outcome, cause }));
    assert.strictEqual(res.json.ok, true);
    const row = lastRow(env);
    assert.strictEqual(row[COL.cause], expected, outcome + '/' + cause);
    assert.strictEqual(row[COL.outcome], outcome);
    // A victory is moved to the Boss bucket; other outcomes keep the posted stage and wave.
    assert.strictEqual(row[COL.stage], outcome === 'victory' ? 'final_boss' : 'wave', outcome + '/' + cause + ' stage');
    assert.strictEqual(row[COL.wave], outcome === 'victory' ? 14 : 9, outcome + '/' + cause + ' wave');
  }
  for (const stage of ['', 'wave', 'build', 'void_trial', 'lobby']) {
    post(env, endEvent({ outcome: 'victory', cause: 'victory', stage, wave: 3 }));
    assert.strictEqual(lastRow(env)[COL.stage], 'final_boss', 'victory with stage "' + stage + '"');
    assert.strictEqual(lastRow(env)[COL.wave], 14, 'victory with stage "' + stage + '"');
  }
});

test('free text is trimmed, cut to 64 chars and can never become a formula', () => {
  const env = readyEnv();
  post(env, endEvent({ enemy: '=HYPERLINK("http://example.invalid","x")', build: 'b'.repeat(100), starting_weapon: '+SUM(A1)' }));
  let row = lastRow(env);
  for (const v of [row[COL.enemy], row[COL.build], row[COL.starting_weapon]]) {
    assert.ok(!/^[=+\-@]/.test(v), 'no formula prefix: ' + v);
    assert.ok(v.length <= 64);
  }
  assert.strictEqual(row[COL.build], 'b'.repeat(64));
  post(env, endEvent({ enemy: { name: 'Grunt' }, starting_weapon: ['Rifle'], build: '  -1.0  ' }));
  row = lastRow(env);
  assert.strictEqual(row[COL.enemy], '');
  assert.strictEqual(row[COL.starting_weapon], '');
  // Stays text only because the build column is plain text (the mock parses numbers like Sheets).
  assert.strictEqual(runsSheet(env).columnFormats[COL.build + 1], '@');
  assert.strictEqual(row[COL.build], '1.0');
});

test('text columns of "runs" are plain text, so number-like text is stored as sent', () => {
  const TEXT = ['event', 'run_id', 'session_id', 'edition', 'build', 'outcome', 'cause', 'enemy', 'stage', 'starting_weapon', 'mode'];
  const OTHER = ['received_at', 'elite', 'wave', 'run_seconds', 'level', 'kills'];
  const checkFormats = (sheet, label) => {
    for (const name of TEXT) assert.strictEqual(sheet.columnFormats[COL[name] + 1], '@', label + ': ' + name);
    for (const name of OTHER) assert.strictEqual(sheet.columnFormats[COL[name] + 1], undefined, label + ': ' + name);
  };

  // Without the format, the mock turns number-like text into numbers, as Sheets does.
  const plain = makeEnv().ss.insertSheet('scratch');
  plain.appendRow(['2', '1234e5', 'x']);
  assert.deepStrictEqual(plain.rowValues(1), [2, 123400000, 'x']);

  const env = readyEnv();
  checkFormats(runsSheet(env), 'after setup');
  const digitsId = '1234567890e1234567890123456789012'.slice(0, 32);
  assert.ok(/^[0-9a-f]{32}$/.test(digitsId));
  assert.deepStrictEqual(post(env, endEvent({ build: '2', run_id: digitsId, session_id: '9'.repeat(32) })).json, { ok: true });
  const row = lastRow(env);
  assert.strictEqual(row[COL.build], '2');
  assert.strictEqual(row[COL.run_id], digitsId);
  assert.strictEqual(row[COL.session_id], '9'.repeat(32));
  assert.strictEqual(row[COL.wave], 9);
  assert.strictEqual(row[COL.elite], false);

  const bound = makeEnv();
  post(bound, endEvent());
  checkFormats(runsSheet(bound), 'sheet created by doPost');
});

test('doPost never throws: missing spreadsheet or lock timeout give ok:false', () => {
  const unbound = makeEnv({ bound: false });
  const res = post(unbound, endEvent());
  assert.deepStrictEqual(res.json, { ok: false, error: 'server error' });
  assert.strictEqual(unbound.errors.length, 1);

  const env = readyEnv();
  env.lock.fail = true;
  const res2 = post(env, endEvent());
  assert.deepStrictEqual(res2.json, { ok: false, error: 'server error' });
  assert.strictEqual(runsSheet(env).getLastRow(), 1);
});

test('doPost on a container-bound sheet without setup creates "runs" with the header', () => {
  const env = makeEnv();
  const res = post(env, endEvent());
  assert.deepStrictEqual(res.json, { ok: true });
  const sheet = runsSheet(env);
  assert.deepStrictEqual(sheet.rowValues(1), HEADER);
  assert.strictEqual(sheet.getLastRow(), 2);
});

test('doGet reports status and row count', () => {
  const env = readyEnv();
  post(env, startEvent());
  post(env, endEvent());
  const out = env.gs.doGet({});
  assert.strictEqual(out.mime, 'JSON');
  const json = JSON.parse(out.getContent());
  assert.strictEqual(json.ok, true);
  assert.strictEqual(json.status, 'WARP ARENA analytics endpoint is running');
  assert.strictEqual(json.rows, 2);
  assert.deepStrictEqual(json.beta_rows, { upgrade_picks: 0, supply_picks: 0, damage: 0 });
  assert.deepStrictEqual(json.v2_rows, { v2_days: 0, v2_purchases: 0, v2_events: 0 });

  const unbound = makeEnv({ bound: false });
  const json2 = JSON.parse(unbound.gs.doGet({}).getContent());
  assert.strictEqual(json2.ok, false);
  assert.ok(json2.status.indexOf('running') >= 0);
});

test('setup creates the header, stores the id, builds the Summary grid and one chart', () => {
  const env = makeEnv();
  env.gs.setup();
  assert.deepStrictEqual(env.ss.getSheets().map((s) => s.name), ALL_SHEETS, 'empty Sheet1 is reused as "runs"');
  const runs = runsSheet(env);
  assert.deepStrictEqual(runs.rowValues(1), HEADER);
  assert.strictEqual(runs.frozenRows, 1);
  assert.strictEqual(env.props.SPREADSHEET_ID, env.ss.id);

  const summary = env.ss.getSheetByName('Summary');
  assert.deepStrictEqual(summary.rowValues(1), ['Wave'].concat(CAUSES));
  let formulaCount = 0;
  BUCKETS.forEach((label, i) => {
    const row = i + 2;
    assert.strictEqual(summary.getCell(row, 1).value, label);
    CAUSES.forEach((cause, j) => {
      const f = summary.getCell(row, j + 2).formula;
      assert.ok(f.startsWith('=COUNTIFS('), 'formula at ' + label + '/' + cause);
      formulaCount++;
      const crit = criteriaOf(f);
      const expected = { B: 'run_end', E: '<>test', Q: '<>story', H: cause };
      if (label === 'Boss') expected.K = 'final_boss';
      else { expected.K = '<>final_boss'; expected.L = i + 1; }
      assert.deepStrictEqual(crit, expected, f);
    });
    assert.strictEqual(summary.getCell(row, CAUSES.length + 2).formula, '', 'nothing right of the grid');
  });
  assert.strictEqual(formulaCount, 15 * 11);

  // Totals block below the grid.
  const labels = {};
  for (let r = 17; r <= summary.getLastRow(); r++) labels[summary.getCell(r, 1).value] = r;
  const started = summary.getCell(labels['Runs started'], 2).formula;
  const ended = summary.getCell(labels['Runs ended'], 2).formula;
  const unfinished = summary.getCell(labels['Unfinished (tab closed)'], 2).formula;
  assert.ok(started.indexOf('runs!$C$2:$C') > 0 && started.indexOf('runs!$B$2:$B="run_start"') > 0 && started.indexOf('runs!$E$2:$E<>"test"') > 0, started);
  assert.ok(ended.indexOf('runs!$B$2:$B="run_end"') > 0, ended);
  for (const f of [started, ended, unfinished]) assert.ok(f.startsWith('=IFERROR('), f);
  assert.strictEqual(unfinished, '=IFERROR(ROWS(UNIQUE(FILTER(runs!$C$2:$C,runs!$B$2:$B="run_start",runs!$E$2:$E<>"test",' +
    'COUNTIFS(runs!$C$2:$C,runs!$C$2:$C,runs!$Q$2:$Q,"story")=0,' +
    'COUNTIFS(runs!$C$2:$C,runs!$C$2:$C,runs!$B$2:$B,"run_end")=0))),0)');
  for (const f of [started, ended]) assert.ok(f.indexOf('runs!$Q$2:$Q,"story")=0') > 0, 'story runs left out: ' + f);
  for (const f of [started, ended, unfinished]) assert.ok(f.indexOf('COUNTUNIQUE') < 0, 'COUNTUNIQUE counts an empty FILTER (#N/A) as 1: ' + f);

  assert.strictEqual(summary.charts.length, 1);
  const spec = summary.charts[0].spec;
  assert.strictEqual(spec.type, 'COLUMN');
  assert.strictEqual(spec.stacked, true);
  assert.strictEqual(spec.title, 'How runs end, by wave');
  assert.deepStrictEqual(spec.ranges, ['A1:L16']);
  assert.strictEqual(spec.numHeaders, 1);
  assert.ok(spec.position.col > 12, 'chart sits right of the grid');
});

test('setup is safe to run twice: one chart, one Summary, rows in "runs" kept', () => {
  const env = readyEnv();
  post(env, startEvent());
  post(env, endEvent());
  post(env, endEvent({ run_id: 'a'.repeat(32), outcome: 'victory', stage: 'final_boss', wave: 14 }));
  const before = runsSheet(env).dataRows().map((r) => r.slice(1));
  env.gs.setup();
  env.gs.setup();
  assert.deepStrictEqual(env.ss.getSheets().map((s) => s.name), ALL_SHEETS);
  const summary = env.ss.getSheetByName('Summary');
  assert.strictEqual(summary.charts.length, 1);
  assert.deepStrictEqual(summary.charts[0].spec.ranges, ['A1:L16']);
  assert.deepStrictEqual(runsSheet(env).rowValues(1), HEADER);
  assert.deepStrictEqual(runsSheet(env).dataRows().map((r) => r.slice(1)), before);
  assert.ok(summary.getCell(16, 12).formula.startsWith('=COUNTIFS('));
});

test('setup keeps data that was already in row 1 of "runs"', () => {
  const env = makeEnv();
  const runs = env.ss.insertSheet('runs');
  runs.appendRow(['2026-10-01', 'run_end', RUN_ID]);
  env.gs.setup();
  assert.deepStrictEqual(runs.rowValues(1), HEADER);
  assert.deepStrictEqual(runs.rowValues(2), ['2026-10-01', 'run_end', RUN_ID]);
  assert.ok(env.ss.getSheetByName('Sheet1'), 'a non-empty workbook keeps its other sheets');
});

test('setup in a standalone script creates a spreadsheet that doPost then uses', () => {
  const env = makeEnv({ bound: false });
  env.gs.setup();
  assert.ok(env.created, 'a spreadsheet was created');
  assert.strictEqual(env.props.SPREADSHEET_ID, env.created.id);
  assert.deepStrictEqual(post(env, endEvent()).json, { ok: true });
  assert.strictEqual(runsSheet(env, env.created).getLastRow(), 2);
  assert.ok(env.logs.some((l) => l.indexOf(env.created.id) >= 0), 'setup logs the spreadsheet URL');
});

test('Summary formulas count the right rows (wave buckets, causes, test rows, totals)', () => {
  const env = readyEnv();
  const id = (n) => n.toString(16).padStart(32, '0');
  const events = [
    startEvent({ run_id: id(1) }), endEvent({ run_id: id(1), wave: 9 }),
    startEvent({ run_id: id(2), edition: 'zh' }), endEvent({ run_id: id(2), edition: 'zh', wave: 9 }),
    startEvent({ run_id: id(3) }), endEvent({ run_id: id(3), cause: 'backfire', wave: 3 }),
    startEvent({ run_id: id(4) }), endEvent({ run_id: id(4), outcome: 'victory', cause: 'victory', stage: 'final_boss', wave: 14 }),
    startEvent({ run_id: id(5) }), endEvent({ run_id: id(5), cause: 'tyrant', enemy: 'Tyrant', stage: 'final_boss', wave: 14 }),
    startEvent({ run_id: id(6) }), endEvent({ run_id: id(6), cause: 'enemy', stage: 'wave', wave: 14 }),
    startEvent({ run_id: id(7) }), endEvent({ run_id: id(7), outcome: 'abandoned', cause: 'restart', stage: 'build', wave: 5 }),
    startEvent({ run_id: id(8), edition: 'test' }), endEvent({ run_id: id(8), edition: 'test', wave: 9 }),
    startEvent({ run_id: id(9) }), // closed the tab
    startEvent({ run_id: id(10) }), startEvent({ run_id: id(10) }), // duplicate start, still one run, unfinished
    endEvent({ run_id: id(11), cause: 'status', wave: 6 }), // its run_start was lost
    startEvent({ run_id: id(12) }), endEvent({ run_id: id(12), outcome: 'victory', cause: 'victory', stage: 'wave', wave: 9 }),
  ];
  for (const ev of events) assert.strictEqual(post(env, ev).json.ok, true);

  const runs = runsSheet(env);
  const summary = env.ss.getSheetByName('Summary');
  const expected = {
    'W9/enemy': 2, 'W3/backfire': 1, 'Boss/victory': 2, 'Boss/tyrant': 1, 'W14/enemy': 1, 'W5/restart': 1, 'W6/status': 1,
  };
  BUCKETS.forEach((label, i) => {
    CAUSES.forEach((cause, j) => {
      const got = evalCountifs(summary.getCell(i + 2, j + 2).formula, runs);
      assert.strictEqual(got, expected[label + '/' + cause] || 0, label + '/' + cause);
    });
  });
  const started = evalDistinct(summary.getCell(18, 2).formula, runs);
  const ended = evalDistinct(summary.getCell(19, 2).formula, runs);
  const unfinished = evalDistinct(summary.getCell(20, 2).formula, runs);
  assert.strictEqual(started, 10);
  assert.strictEqual(ended, 9);
  // Runs 9 and 10 never ended; run 11 ended without a start, so started minus ended would give 1.
  assert.strictEqual(unfinished, 2);
});

// ---------------------------------------------------------------------------
// Beta metrics
// ---------------------------------------------------------------------------

function betaBase(event, overrides) {
  return Object.assign({ schema: 1, event: event, run_id: RUN_ID, session_id: SESSION_ID, edition: 'en', build: '1.0.0+3fa2b1c9' }, overrides);
}
function upgradeEvent(overrides) {
  return betaBase('upgrade_pick', Object.assign({
    context: 'levelup', wave: 4, level: 5,
    offer_1: 'Weapon:Rifle:Power', offer_2: 'Skill:FlameWake', offer_3: 'Weapon:Minigun:Tempo',
    picked: 'Weapon:Rifle:Power', rerolled: false,
  }, overrides));
}
function supplyEvent(overrides) {
  return betaBase('supply_pick', Object.assign({
    wave: 3, level: 4, offer_1: 'Minigun', offer_2: 'Rifle:upgrade', offer_3: 'OrbitBlades',
    action: 'replace', picked: 'Minigun', replaced: 'Knife', replace_cancels: 1,
  }, overrides));
}
function damageEvent(rows, overrides) {
  return betaBase('wave_damage', Object.assign({ rows: rows }, overrides));
}
function sheetOf(env, name) { return env.ss.getSheetByName(name); }

// Minimal COUNTIFS / SUMIFS evaluator for the beta summaries: equality, "<>x", ">0" and "*" wildcards.
function betaMatches(cell, criterion) {
  if (criterion === true) return cell === true;
  if (typeof criterion === 'number') return cell === criterion;
  if (criterion.startsWith('<>')) return String(cell).toLowerCase() !== criterion.slice(2).toLowerCase();
  if (criterion.startsWith('>')) return typeof cell === 'number' && cell > Number(criterion.slice(1));
  const re = new RegExp('^' + criterion.split('*').map((s) => s.replace(/[.+?^${}()|[\]\\]/g, '\\$&')).join('.*') + '$', 'i');
  return typeof cell === 'string' && re.test(cell);
}
function evalBetaFormula(formula, env) {
  const terms = formula.replace(/^=/, '').split(/\+(?=COUNTIFS)/);
  let total = 0;
  for (const term of terms) {
    const m = /^(COUNTIFS|SUMIFS)\((.*)\)$/.exec(term);
    if (!m) throw new Error('Unexpected formula: ' + formula);
    const args = m[2].match(/(\w+)!\$([A-Z]):\$\2|"[^"]*"|TRUE/g);
    let sumCol = null;
    let i = 0;
    if (m[1] === 'SUMIFS') { sumCol = args[0]; i = 1; }
    const pairs = [];
    for (; i < args.length; i += 2) {
      const crit = args[i + 1] === 'TRUE' ? true : args[i + 1].slice(1, -1);
      pairs.push([args[i], crit]);
    }
    const colIndex = (ref) => ref.split('$')[1].charCodeAt(0) - 65;
    const sheet = sheetOf(env, pairs[0][0].split('!')[0]);
    for (let r = 2; r <= sheet.getLastRow(); r++) {
      const row = sheet.rowValues(r);
      if (pairs.every(([ref, crit]) => betaMatches(row[colIndex(ref)], crit))) total += sumCol ? Number(row[colIndex(sumCol)]) || 0 : 1;
    }
  }
  return total;
}

test('upgrade_pick is stored in "upgrade_picks" with its kind and weapon count worked out', () => {
  const env = readyEnv();
  assert.deepStrictEqual(post(env, upgradeEvent({ rerolled: true, wave: 99, level: '7' })).json, { ok: true });
  const sheet = sheetOf(env, 'upgrade_picks');
  assert.deepStrictEqual(sheet.rowValues(1), UPGRADE_HEADER);
  const row = sheet.rowValues(2);
  assert.ok(isDate(row[0]));
  assert.deepStrictEqual(row.slice(1), ['upgrade_pick', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9', 'levelup', 14, 7,
    'Weapon:Rifle:Power', 'Skill:FlameWake', 'Weapon:Minigun:Tempo', 'Weapon:Rifle:Power', 'Weapon', 2, true]);
  assert.strictEqual(runsSheet(env).getLastRow(), 1, 'nothing written to "runs"');

  post(env, upgradeEvent({ context: 'build', picked: 'Stat:Damage', offer_1: 'Stat:Damage', offer_3: '=HYPERLINK("x")' }));
  const second = sheet.rowValues(3);
  assert.strictEqual(second[6], 'build');
  assert.strictEqual(second[11], 'HYPERLINKx', 'offers can never become a formula');
  assert.strictEqual(second[13], 'Stat');
  assert.strictEqual(second[14], 0);
});

test('supply_pick keeps picked only when a weapon was taken and replaced only for replace', () => {
  const env = readyEnv();
  for (const ev of [
    supplyEvent(),
    supplyEvent({ action: 'skip', picked: 'Minigun', replaced: 'Knife', replace_cancels: 500 }),
    supplyEvent({ action: 'merge', picked: 'Rifle', replaced: 'Knife' }),
  ]) assert.deepStrictEqual(post(env, ev).json, { ok: true });
  const sheet = sheetOf(env, 'supply_picks');
  assert.deepStrictEqual(sheet.rowValues(1), SUPPLY_HEADER);
  assert.deepStrictEqual(sheet.rowValues(2).slice(6), [3, 4, 'Minigun', 'Rifle:upgrade', 'OrbitBlades', 'replace', 'Minigun', 'Knife', 1]);
  assert.deepStrictEqual(sheet.rowValues(3).slice(11), ['skip', '', '', 100]);
  assert.deepStrictEqual(sheet.rowValues(4).slice(11), ['merge', 'Rifle', '', 1]);
});

test('wave_damage writes one row per source, with the wave bucket and clamped numbers', () => {
  const env = readyEnv();
  const res = post(env, damageEvent([
    { source: 'Brute_elite', dot: false, stage: 'wave', wave: 3, damage: 12.34, damage_pct: 24.68, hits: 2 },
    { source: 'Spitter', dot: true, stage: 'wave', wave: 3, damage: 4, damage_pct: 8, hits: 4 },
    { source: '', dot: 'true', stage: 'final_boss', wave: 2, damage: -5, damage_pct: 'x', hits: 1.6 },
  ]));
  assert.deepStrictEqual(res.json, { ok: true });
  const sheet = sheetOf(env, 'damage');
  assert.deepStrictEqual(sheet.rowValues(1), DAMAGE_HEADER);
  assert.strictEqual(sheet.getLastRow(), 4);
  assert.deepStrictEqual(sheet.rowValues(2).slice(6), ['wave', 3, 'W03', 'Brute_elite', false, 12.3, 24.7, 2, 'open']);
  assert.deepStrictEqual(sheet.rowValues(3).slice(6), ['wave', 3, 'W03', 'Spitter', true, 4, 8, 4, 'open']);
  assert.deepStrictEqual(sheet.rowValues(4).slice(6), ['final_boss', 14, 'W15 Boss', 'other', true, 0, 0, 2, 'open']);
  assert.ok(env.lock.releases >= 1 && !env.lock.held, 'written under the lock');
});

test('bad beta events are rejected and write nothing', () => {
  const tooMany = [];
  for (let i = 0; i < 25; i++) tooMany.push({ source: 'Grunt', wave: 1, damage: 1, damage_pct: 1, hits: 1 });
  const cases = [
    [upgradeEvent({ schema: 2 }), 'bad schema'],
    [upgradeEvent({ run_id: 'nope' }), 'bad run_id'],
    [upgradeEvent({ session_id: undefined }), 'bad session_id'],
    [upgradeEvent({ edition: 'fr' }), 'bad edition'],
    [upgradeEvent({ context: 'greater' }), 'bad context'],
    [upgradeEvent({ picked: '' }), 'bad picked'],
    [supplyEvent({ action: 'steal' }), 'bad action'],
    [supplyEvent({ action: 'take_free', picked: '' }), 'bad picked'],
    [damageEvent([]), 'bad rows'],
    [damageEvent('Grunt'), 'bad rows'],
    [damageEvent([null]), 'bad rows'],
    [damageEvent(tooMany), 'too many rows'],
  ];
  for (const [body, error] of cases) {
    const env = readyEnv();
    const res = post(env, body);
    assert.strictEqual(res.json.ok, false, error);
    assert.strictEqual(res.json.error, error, JSON.stringify(body).slice(0, 80));
    for (const name of ['runs', 'upgrade_picks', 'supply_picks', 'damage']) assert.strictEqual(sheetOf(env, name).getLastRow(), 1, name);
  }
});

test('a beta event creates its sheet with the header when setup has not run', () => {
  const env = makeEnv();
  assert.deepStrictEqual(post(env, supplyEvent()).json, { ok: true });
  const sheet = sheetOf(env, 'supply_picks');
  assert.deepStrictEqual(sheet.rowValues(1), SUPPLY_HEADER);
  assert.strictEqual(sheet.getLastRow(), 2);
  assert.strictEqual(sheet.columnFormats[9], '@', 'offer columns are plain text');
});

test('setup builds the three beta summaries with their charts', () => {
  const env = readyEnv();
  const upgrades = sheetOf(env, 'Upgrade picks');
  assert.deepStrictEqual(upgrades.rowValues(1), ['Upgrade direction', 'Offered', 'Picked', 'Pick rate']);
  assert.deepStrictEqual([2, 3, 4, 5, 6, 7, 8].map((r) => upgrades.getCell(r, 1).value),
    ['Power', 'Tempo', 'Reach', 'Infusion', 'Twist', 'Tame', 'All weapon upgrades']);
  assert.deepStrictEqual([11, 12, 13].map((r) => upgrades.getCell(r, 1).value), ['Weapon', 'Skill', 'Stat']);
  assert.deepStrictEqual(upgrades.charts.map((c) => [c.spec.type, c.spec.ranges]),
    [['COLUMN', ['A1:A7', 'D1:D7']], ['COLUMN', ['A10:A13', 'D10:D13']]]);

  const supply = sheetOf(env, 'Supply picks');
  assert.deepStrictEqual([2, 3, 4, 5, 6].map((r) => supply.getCell(r, 1).value), ['take_free', 'merge', 'replace', 'skip', 'timeout']);
  assert.ok(supply.getCell(1, 5).formula.indexOf("where L = 'replace'") > 0);
  assert.deepStrictEqual(supply.charts.map((c) => c.spec.type), ['PIE', 'BAR']);

  const damage = sheetOf(env, 'Damage sources');
  assert.ok(damage.getCell(1, 1).formula.startsWith('=IFERROR(QUERY(FILTER(damage!$A$2:$O,damage!$O$2:$O<>"story"),'));
  assert.ok(damage.getCell(1, 8).formula.indexOf('pivot Col10') > 0);
  assert.deepStrictEqual(damage.charts.map((c) => c.spec.type), ['BAR', 'COLUMN']);
  assert.strictEqual(damage.charts[1].spec.options.isStacked, 'percent');
  assert.deepStrictEqual(damage.charts[1].spec.ranges, ['H1:AV16']);

  env.gs.setup();
  assert.strictEqual(sheetOf(env, 'Upgrade picks').charts.length, 2, 'setup twice keeps one set of charts');
  assert.strictEqual(sheetOf(env, 'Damage sources').charts.length, 2);
});

test('beta summary formulas count the right rows', () => {
  const env = readyEnv();
  const events = [
    upgradeEvent(), // Rifle Power (picked), Minigun Tempo offered
    upgradeEvent({ picked: 'Skill:FlameWake' }),
    upgradeEvent({ offer_1: 'Stat:Damage', offer_2: 'Skill:Blink', offer_3: 'Stat:MaxHp', picked: 'Stat:MaxHp' }),
    upgradeEvent({ edition: 'test' }),
    supplyEvent(), supplyEvent({ replaced: 'Rifle' }), supplyEvent({ action: 'skip', replace_cancels: 2 }),
    supplyEvent({ action: 'take_free', edition: 'test' }),
    damageEvent([{ source: 'Grunt', wave: 1, damage: 10, damage_pct: 20, hits: 1 },
      { source: 'Spitter', dot: true, wave: 2, damage: 5, damage_pct: 10, hits: 3 }]),
    damageEvent([{ source: 'Grunt', wave: 2, damage: 5, damage_pct: 10, hits: 1 }], { edition: 'test' }),
  ];
  for (const ev of events) assert.strictEqual(post(env, ev).json.ok, true);

  const up = sheetOf(env, 'Upgrade picks');
  const rate = (row) => [evalBetaFormula(up.getCell(row, 2).formula, env), evalBetaFormula(up.getCell(row, 3).formula, env)];
  assert.deepStrictEqual(rate(2), [2, 1], 'Power offered twice, picked once (test row left out)');
  assert.deepStrictEqual(rate(3), [2, 0], 'Tempo');
  assert.deepStrictEqual(rate(8), [4, 1], 'all weapon upgrades');
  assert.deepStrictEqual(rate(11), [4, 1], 'Weapon cards');
  assert.deepStrictEqual(rate(12), [3, 1], 'Skill cards');
  assert.deepStrictEqual(rate(13), [2, 1], 'Stat cards');
  assert.strictEqual(evalBetaFormula(up.getCell(15, 2).formula, env), 2, 'picks with a weapon on offer');
  assert.strictEqual(evalBetaFormula(up.getCell(16, 2).formula, env), 1, 'of which a weapon was picked');

  const sp = sheetOf(env, 'Supply picks');
  assert.deepStrictEqual([2, 3, 4, 5, 6].map((r) => evalBetaFormula(sp.getCell(r, 2).formula, env)), [0, 0, 2, 1, 0]);
  assert.strictEqual(evalBetaFormula(sp.getCell(9, 2).formula, env), 4, 'replace screen cancels: 1 + 1 + 2');

  const dm = sheetOf(env, 'Damage sources');
  assert.strictEqual(evalBetaFormula(dm.getCell(2, 5).formula.replace(/^=IF\(\$A2="","",(.*)\)$/, '=$1').replace('$A2', '"Spitter"'), env), 10,
    'burn / poison part of Spitter damage');
});

// ---------------------------------------------------------------------------
// Story mode (the V2 day campaign)
// ---------------------------------------------------------------------------

const COMMON = ['received_at', 'event', 'run_id', 'session_id', 'edition', 'build'];
const V2_DAY_HEADER = COMMON.concat(['day', 'overtime', 'reached_dawn', 'end_stage', 'end_cause', 'kills',
  'player_damage', 'player_damage_pct', 'player_hits', 'base_damage', 'base_damage_hp', 'base_hp', 'base_shield', 'base_max_hp',
  'earned_alloy', 'earned_energy', 'earned_dark', 'spent_alloy', 'spent_energy', 'spent_dark', 'alloy', 'energy', 'dark',
  'night_death', 'lost_alloy', 'lost_energy', 'lost_dark', 'shadows', 'overrun_regions', 'repairs']);
const V2_PURCHASE_HEADER = COMMON.concat(['day', 'stage', 'item', 'detail', 'room', 'alloy', 'energy', 'dark']);
const V2_EVENT_HEADER = COMMON.concat(['day', 'stage', 'kind', 'detail', 'value']);
const V2_SHEETS = ['v2_days', 'v2_purchases', 'v2_events'];

function v2DayEvent(overrides) {
  return betaBase('v2_day', Object.assign({
    day: 3, overtime: false, reached_dawn: true, end_stage: '', end_cause: '', kills: 41,
    player_damage: 120.44, player_damage_pct: 60.22, player_hits: 9,
    base_damage: 300.06, base_damage_hp: 80.5, base_hp: 920.04, base_shield: 0, base_max_hp: 1000,
    earned_alloy: 210, earned_energy: 95, earned_dark: 12, spent_alloy: 150, spent_energy: 40, spent_dark: 0,
    alloy: 60, energy: 55, dark: 12, night_death: false, lost_alloy: 0, lost_energy: 0, lost_dark: 0,
    shadows: 6, overrun_regions: 1, repairs: 2,
  }, overrides));
}
function v2PurchaseEvent(overrides) {
  return betaBase('v2_purchase', Object.assign({
    day: 4, stage: 'dawn', item: 'new_weapon', detail: 'Railgun Mk 2', room: 'Armament', alloy: 120, energy: 30, dark: 0,
  }, overrides));
}
function v2Event(overrides) {
  return betaBase('v2_event', Object.assign({ day: 2, stage: 'dusk', kind: 'hole_check', detail: 'failed_timeout', value: 0.6349 }, overrides));
}

test('v2_day is stored in "v2_days"; a day that reached dawn keeps no end, the last day keeps the run\'s', () => {
  const env = readyEnv();
  for (const ev of [
    v2DayEvent({ end_stage: 'night', end_cause: 'enemy' }),
    v2DayEvent({ day: 250, overtime: 'true', reached_dawn: false, end_stage: 'night', end_cause: 'base_destroyed', kills: -3,
      alloy: 1e12, night_death: 1, repairs: 2.6 }),
    v2DayEvent({ reached_dawn: 'false', end_stage: 'lobby', end_cause: 'meteor' }),
    v2DayEvent({ day: '0', reached_dawn: false, end_stage: 'tutorial', end_cause: '=HYPERLINK("x")' }),
  ]) assert.deepStrictEqual(post(env, ev).json, { ok: true });
  const sheet = sheetOf(env, 'v2_days');
  assert.deepStrictEqual(sheet.rowValues(1), V2_DAY_HEADER);
  const first = sheet.rowValues(2);
  assert.ok(isDate(first[0]));
  assert.deepStrictEqual(first.slice(1), ['v2_day', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9', 3, false, true, '', '', 41,
    120.4, 60.2, 9, 300.1, 80.5, 920, 0, 1000, 210, 95, 12, 150, 40, 0, 60, 55, 12, false, 0, 0, 0, 6, 1, 2]);
  const last = sheet.rowValues(3);
  assert.deepStrictEqual(last.slice(6, 12), [100, true, false, 'night', 'base_destroyed', 0], 'day clamped, the run\'s end kept');
  assert.deepStrictEqual([last[26], last[29], last[35]], [10000000, true, 3], 'wallet clamped, night death, repairs rounded');
  assert.deepStrictEqual(sheet.rowValues(4).slice(8, 11), [false, 'other', 'unknown'], 'unknown stage and cause');
  assert.deepStrictEqual(sheet.rowValues(5).slice(6, 11), [0, false, false, 'tutorial', 'unknown'], 'a formula never gets in');
  assert.strictEqual(runsSheet(env).getLastRow(), 1, 'nothing written to "runs"');
  assert.ok(env.lock.releases >= 1 && !env.lock.held, 'written under the lock');
});

test('v2_purchase is stored in "v2_purchases"; an unknown stage is "other" and prices are clamped', () => {
  const env = readyEnv();
  assert.deepStrictEqual(post(env, v2PurchaseEvent()).json, { ok: true });
  assert.deepStrictEqual(post(env, v2PurchaseEvent({ stage: 'Night ', item: ' repair ', detail: '', room: 'Control', alloy: -5, energy: '7', dark: 2.4 })).json, { ok: true });
  assert.deepStrictEqual(post(env, v2PurchaseEvent({ stage: 'shop', room: 'Hangar' })).json, { ok: true });
  const sheet = sheetOf(env, 'v2_purchases');
  assert.deepStrictEqual(sheet.rowValues(1), V2_PURCHASE_HEADER);
  assert.deepStrictEqual(sheet.rowValues(2).slice(1), ['v2_purchase', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9',
    4, 'dawn', 'new_weapon', 'Railgun Mk 2', 'Armament', 120, 30, 0]);
  assert.deepStrictEqual(sheet.rowValues(3).slice(6), [4, 'other', 'repair', '', 'Control', 0, 7, 2], 'stage names are not case-folded');
  assert.deepStrictEqual(sheet.rowValues(4).slice(7, 11), ['other', 'new_weapon', 'Railgun Mk 2', 'Hangar'], 'a new room is kept as sent');
});

test('v2_event is stored in "v2_events" with its value rounded to 2 decimals and never negative', () => {
  const env = readyEnv();
  for (const ev of [
    v2Event(),
    v2Event({ day: 0, stage: 'tutorial', kind: 'tutorial_step', detail: 'Move', value: 12.3456 }),
    v2Event({ kind: 'crate', detail: 'Afternoon:Railgun', value: 2 }),
    v2Event({ kind: 'crate', detail: 'TutorialSecond:left', value: -1 }),
    v2Event({ day: 4, stage: 'day', kind: 'prophet', detail: '', value: '4' }),
    v2Event({ day: 5, stage: 'dawn', kind: 'void_purchase', detail: 'skill:PocketVoid', value: 30 }),
    v2Event({ day: 0, stage: 'tutorial', kind: 'void_crystal', detail: undefined, value: 'x' }),
    v2Event({ detail: 'start', value: 1e9 }),
  ]) assert.deepStrictEqual(post(env, ev).json, { ok: true });
  const sheet = sheetOf(env, 'v2_events');
  assert.deepStrictEqual(sheet.rowValues(1), V2_EVENT_HEADER);
  assert.deepStrictEqual(sheet.rowValues(2).slice(1), ['v2_event', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9', 2, 'dusk', 'hole_check', 'failed_timeout', 0.63]);
  assert.deepStrictEqual(sheet.dataRows().slice(1).map((r) => r.slice(6)), [
    [0, 'tutorial', 'tutorial_step', 'Move', 12.35],
    [2, 'dusk', 'crate', 'Afternoon:Railgun', 2],
    [2, 'dusk', 'crate', 'TutorialSecond:left', 0],
    [4, 'day', 'prophet', '', 4],
    [5, 'dawn', 'void_purchase', 'skill:PocketVoid', 30],
    [0, 'tutorial', 'void_crystal', '', 0],
    [2, 'dusk', 'hole_check', 'start', 1000000],
  ]);
});

test('bad story-mode events are rejected and write nothing', () => {
  const cases = [
    [v2DayEvent({ schema: 2 }), 'bad schema'],
    [v2DayEvent({ run_id: 'nope' }), 'bad run_id'],
    [v2DayEvent({ edition: 'fr' }), 'bad edition'],
    [v2DayEvent({ day: undefined }), 'bad day'],
    [v2DayEvent({ day: 'three' }), 'bad day'],
    [v2DayEvent({ day: true }), 'bad day'],
    [v2PurchaseEvent({ day: null }), 'bad day'],
    [v2PurchaseEvent({ item: '' }), 'bad item'],
    [v2PurchaseEvent({ item: '%%' }), 'bad item'],
    [v2Event({ day: '' }), 'bad day'],
    [v2Event({ kind: 'teleport' }), 'bad kind'],
    [v2Event({ kind: undefined }), 'bad kind'],
    [v2Event({ detail: 'failed' }), 'bad detail'],
    [v2Event({ detail: '' }), 'bad detail'],
    [v2Event({ kind: 'tutorial_step', detail: '' }), 'bad detail'],
    [v2Event({ kind: 'crate', detail: undefined }), 'bad detail'],
    [v2Event({ kind: 'void_purchase', detail: '%%' }), 'bad detail'],
  ];
  for (const [body, error] of cases) {
    const env = readyEnv();
    const res = post(env, body);
    assert.strictEqual(res.json.ok, false, error);
    assert.strictEqual(res.json.error, error, JSON.stringify(body).slice(0, 120));
    for (const name of ['runs'].concat(V2_SHEETS)) assert.strictEqual(sheetOf(env, name).getLastRow(), 1, name);
  }
});

test('a story-mode event creates its sheet with the header when setup has not run; text columns are plain text', () => {
  const env = makeEnv();
  assert.deepStrictEqual(post(env, v2Event()).json, { ok: true });
  assert.deepStrictEqual(post(env, v2DayEvent()).json, { ok: true });
  const events = sheetOf(env, 'v2_events');
  assert.deepStrictEqual(events.rowValues(1), V2_EVENT_HEADER);
  assert.strictEqual(events.getLastRow(), 2);
  assert.deepStrictEqual([7, 8, 9, 10, 11].map((c) => events.columnFormats[c] || ''), ['', '@', '@', '@', ''], 'stage, kind and detail are text');
  const days = sheetOf(env, 'v2_days');
  assert.deepStrictEqual(days.rowValues(1), V2_DAY_HEADER);
  assert.deepStrictEqual([9, 10, 11, 12].map((c) => days.columnFormats[c] || ''), ['', '@', '@', ''], 'end_stage and end_cause are text');
  assert.strictEqual(sheetOf(env, 'v2_purchases'), null, 'only the sheets that got a row are made');
});

test('setup makes the three story-mode sheets, keeps their rows, and doGet counts them', () => {
  const env = readyEnv();
  V2_SHEETS.forEach((name, i) => {
    const sheet = sheetOf(env, name);
    assert.deepStrictEqual(sheet.rowValues(1), [V2_DAY_HEADER, V2_PURCHASE_HEADER, V2_EVENT_HEADER][i], name);
    assert.strictEqual(sheet.frozenRows, 1, name);
    assert.strictEqual(sheet.charts.length, 0, name);
  });
  for (const ev of [v2DayEvent(), v2PurchaseEvent(), v2Event(), v2Event({ kind: 'prophet', detail: '' })]) post(env, ev);
  const before = sheetOf(env, 'v2_events').dataRows().map((r) => r.slice(1));
  env.gs.setup();
  assert.deepStrictEqual(env.ss.getSheets().map((s) => s.name), ALL_SHEETS, 'setup twice adds no sheet');
  assert.deepStrictEqual(sheetOf(env, 'v2_events').dataRows().map((r) => r.slice(1)), before);
  const json = JSON.parse(env.gs.doGet({}).getContent());
  assert.deepStrictEqual(json.v2_rows, { v2_days: 1, v2_purchases: 1, v2_events: 2 });
  assert.deepStrictEqual(json.beta_rows, { upgrade_picks: 0, supply_picks: 0, damage: 0 }, 'story rows are not beta rows');
});

// ---------------------------------------------------------------------------
// Mode: story-mode rows in "runs" and "damage"
// ---------------------------------------------------------------------------

test('mode is "story" only when the game sends exactly "story"; anything else, or nothing, is "open"', () => {
  const env = readyEnv();
  const sent = [undefined, 'open', 'story', 'Story', ' story', 'STORY', 1, true, null];
  sent.forEach((mode, i) => {
    const ev = endEvent({ run_id: i.toString(16).padStart(32, '0') });
    if (mode !== undefined) ev.mode = mode;
    assert.strictEqual(post(env, ev).json.ok, true, String(mode));
  });
  assert.deepStrictEqual(runsSheet(env).dataRows().map((r) => r[COL.mode]),
    ['open', 'open', 'story', 'open', 'open', 'open', 'open', 'open', 'open']);
  assert.strictEqual(runsSheet(env).columnFormats[COL.mode + 1], '@', 'mode is plain text');
});

test('a story-mode row in "runs" keeps its own stage, its day (0 to 100) and base_destroyed', () => {
  const env = readyEnv();
  const story = (overrides) => endEvent(Object.assign({ mode: 'story', stage: 'night', wave: 3, cause: 'base_destroyed', enemy: '' }, overrides));
  const cases = [
    [story(), ['defeat', 'base_destroyed', 'night', 3]],
    [startEvent({ mode: 'story', stage: 'tutorial', wave: 0 }), ['', '', 'tutorial', 0]],
    [story({ outcome: 'victory', cause: 'victory', stage: 'ending', wave: 7 }), ['victory', 'victory', 'ending', 7]],
    [story({ wave: 250, stage: 'lobby' }), ['defeat', 'base_destroyed', 'other', 100]],
    [story({ wave: undefined, stage: 'dusk', cause: 'status' }), ['defeat', 'status', 'dusk', 0]],
    [story({ wave: -4 }), ['defeat', 'base_destroyed', 'night', 0]],
    [story({ outcome: 'abandoned', cause: 'base_destroyed', stage: 'dawn' }), ['abandoned', 'unknown', 'dawn', 3]],
    [story({ cause: 'restart' }), ['defeat', 'unknown', 'night', 3]],
    [story({ outcome: 'abandoned', cause: 'quit_to_title', stage: 'void_trial' }), ['abandoned', 'quit_to_title', 'void_trial', 3]],
    [endEvent({ cause: 'base_destroyed', stage: 'night', wave: 0 }), ['defeat', 'unknown', 'wave', 1]],
  ];
  for (const [ev] of cases) assert.strictEqual(post(env, ev).json.ok, true);
  const rows = runsSheet(env).dataRows();
  cases.forEach(([ev, expected], i) => {
    const row = rows[i];
    assert.deepStrictEqual([row[COL.outcome], row[COL.cause], row[COL.stage], row[COL.wave]], expected, JSON.stringify(ev).slice(0, 140));
  });
  assert.deepStrictEqual(rows.map((r) => r[COL.mode]), cases.map((c, i) => (i < cases.length - 1 ? 'story' : 'open')));
});

test('wave_damage takes the mode of its event; story rows keep their stage and day, in a day bucket', () => {
  const env = readyEnv();
  post(env, damageEvent([
    { source: 'shadow', stage: 'night', wave: 0, damage: 3, damage_pct: 6, hits: 2 },
    { source: 'Grunt', stage: 'day', wave: 12, damage: 4, damage_pct: 8, hits: 1 },
    { source: 'Grunt', stage: 'lobby', wave: 250, damage: 1, damage_pct: 2, hits: 1 },
  ], { mode: 'story' }));
  post(env, damageEvent([{ source: 'Grunt', stage: 'night', wave: 0, damage: 4, damage_pct: 8, hits: 1 }], { mode: 'Story' }));
  const sheet = sheetOf(env, 'damage');
  assert.deepStrictEqual(sheet.rowValues(1), DAMAGE_HEADER);
  assert.deepStrictEqual(sheet.dataRows().map((r) => [r[6], r[7], r[8], r[9], r[14]]), [
    ['night', 0, 'D00', 'shadow', 'story'],
    ['day', 12, 'D12', 'Grunt', 'story'],
    ['other', 100, 'D100', 'Grunt', 'story'],
    ['wave', 1, 'W01', 'Grunt', 'open'],
  ]);
  assert.strictEqual(sheet.columnFormats[15], '@', 'mode is plain text');
});

test('the open-mode Summary leaves story runs out, also one whose start went out as open; rows without a mode count as open', () => {
  const env = readyEnv();
  const id = (n) => n.toString(16).padStart(32, '0');
  const story = { mode: 'story' };
  const events = [
    startEvent({ run_id: id(1) }), endEvent({ run_id: id(1), wave: 3 }), // open: ends in wave 3
    startEvent({ run_id: id(2), mode: 'story', stage: 'tutorial', wave: 0 }),
    endEvent({ run_id: id(2), mode: 'story', stage: 'day', wave: 3 }), // story: day 3 is not wave 3
    startEvent({ run_id: id(3) }), endEvent(Object.assign({ run_id: id(3), wave: 3 }, story)), // marked as a campaign run late
    startEvent({ run_id: id(4), mode: 'story' }), // a story run left by closing the tab is not an open unfinished run
    startEvent({ run_id: id(5) }), // an open run left by closing the tab
  ];
  for (const ev of events) assert.strictEqual(post(env, ev).json.ok, true);
  // A row written before the mode column existed: 16 cells, no mode.
  const runs = runsSheet(env);
  runs.appendRow([new Date(), 'run_end', id(6), SESSION_ID, 'en', 'old', 'defeat', 'enemy', 'Grunt', false, 'wave', 3, 100, 2, 10, 'Rifle']);
  runs.appendRow([new Date(), 'run_start', id(6), SESSION_ID, 'en', 'old', '', '', '', false, 'wave', 1, 0, 1, 0, 'Rifle']);

  const summary = env.ss.getSheetByName('Summary');
  const enemyCol = CAUSES.indexOf('enemy') + 2;
  assert.strictEqual(evalCountifs(summary.getCell(4, enemyCol).formula, runs), 2, 'W3 / enemy: runs 1 and 6 only');
  assert.strictEqual(evalDistinct(summary.getCell(18, 2).formula, runs), 3, 'started: runs 1, 5 and 6');
  assert.strictEqual(evalDistinct(summary.getCell(19, 2).formula, runs), 2, 'ended: runs 1 and 6');
  assert.strictEqual(evalDistinct(summary.getCell(20, 2).formula, runs), 1, 'unfinished: run 5');
  assert.ok(summary.getCell(22, 1).value.indexOf('story') >= 0, 'the note says story runs are left out');
});

test('the damage summary reads open-mode rows only, and its queries name the right columns', () => {
  const env = readyEnv();
  post(env, damageEvent([{ source: 'Spitter', dot: true, wave: 2, damage: 5, damage_pct: 10, hits: 3 }]));
  post(env, damageEvent([{ source: 'Spitter', dot: true, stage: 'night', wave: 2, damage: 25, damage_pct: 50, hits: 3 }], { mode: 'story' }));
  const dm = sheetOf(env, 'Damage sources');
  assert.strictEqual(evalBetaFormula(dm.getCell(2, 5).formula.replace(/^=IF\(\$A2="","",(.*)\)$/, '=$1').replace('$A2', '"Spitter"'), env), 10,
    'burn / poison part of Spitter damage, story rows left out');
  for (const [cell, used] of [[dm.getCell(1, 1), ['edition', 'source', 'damage_pct', 'hits']], [dm.getCell(1, 8), ['edition', 'bucket', 'source', 'damage_pct']]]) {
    const f = cell.formula;
    assert.ok(f.startsWith('=IFERROR(QUERY(FILTER(damage!$A$2:$O,damage!$O$2:$O<>"story"),"'), f);
    const names = Array.from(new Set((f.match(/Col\d+/g) || []).map((c) => DAMAGE_HEADER[Number(c.slice(3)) - 1]))).sort();
    assert.deepStrictEqual(names, used.slice().sort(), f);
  }
});

test('appsscript.json is a V8 web app open to anonymous callers', () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'appsscript.json'), 'utf8'));
  assert.strictEqual(manifest.runtimeVersion, 'V8');
  assert.strictEqual(manifest.timeZone, 'America/Los_Angeles');
  assert.strictEqual(manifest.exceptionLogging, 'STACKDRIVER');
  assert.deepStrictEqual(manifest.webapp, { executeAs: 'USER_DEPLOYING', access: 'ANYONE_ANONYMOUS' });
});

// ---------------------------------------------------------------------------

let failed = 0;
for (const t of tests) {
  try {
    t.fn();
    console.log('PASS  ' + t.name);
  } catch (err) {
    failed++;
    console.log('FAIL  ' + t.name);
    console.log('      ' + String(err && err.message ? err.message : err).split('\n').join('\n      '));
  }
}
console.log('\n' + (tests.length - failed) + ' passed, ' + failed + ' failed, ' + tests.length + ' total');
process.exit(failed ? 1 : 0);
