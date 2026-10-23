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
  'level', 'kills', 'starting_weapon'
];
const CAUSES = [
  'enemy', 'tyrant', 'backfire', 'wall_slam', 'status', 'black_hole',
  'void_trial', 'unknown', 'victory', 'restart', 'quit_to_title'
];
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
  const m = /^=IFERROR\(COUNTUNIQUE\(FILTER\(runs!\$C\$2:\$C,runs!\$B\$2:\$B="(\w+)",runs!\$E\$2:\$E<>"test"(?:,COUNTIFS\(runs!\$C\$2:\$C,runs!\$C\$2:\$C,runs!\$B\$2:\$B,"(\w+)"\)=0)?\)\),0\)$/.exec(formula);
  if (!m) throw new Error('Unexpected distinct formula: ' + formula);
  const rows = runs.dataRows();
  // The optional COUNTIFS(...)=0 term drops every run_id that has a row with that event, in any edition.
  const excluded = new Set(m[2] ? rows.filter((r) => r[COL.event] === m[2]).map((r) => r[COL.run_id]) : []);
  const ids = new Set(rows
    .filter((r) => r[COL.event] === m[1] && r[COL.edition] !== 'test' && !excluded.has(r[COL.run_id]))
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
  assert.strictEqual(row.length, 16);
  assert.ok(isDate(row[0]), 'received_at is a Date');
  assert.ok(Math.abs(Date.now() - row[0].getTime()) < 5000, 'received_at is server time');
  assert.deepStrictEqual(row.slice(1), [
    'run_end', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9', 'defeat', 'enemy', 'Spitter',
    true, 'wave', 9, 512.3, 7, 183, 'Rifle',
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
    'run_start', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9', '', '', '', false, 'wave', 1, 0, 1, 0, 'Knife',
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
  const TEXT = ['event', 'run_id', 'session_id', 'edition', 'build', 'outcome', 'cause', 'enemy', 'stage', 'starting_weapon'];
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

  const unbound = makeEnv({ bound: false });
  const json2 = JSON.parse(unbound.gs.doGet({}).getContent());
  assert.strictEqual(json2.ok, false);
  assert.ok(json2.status.indexOf('running') >= 0);
});

test('setup creates the header, stores the id, builds the Summary grid and one chart', () => {
  const env = makeEnv();
  env.gs.setup();
  assert.deepStrictEqual(env.ss.getSheets().map((s) => s.name), ['runs', 'Summary'], 'empty Sheet1 is reused as "runs"');
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
      const expected = { B: 'run_end', E: '<>test', H: cause };
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
  assert.strictEqual(unfinished, '=IFERROR(COUNTUNIQUE(FILTER(runs!$C$2:$C,runs!$B$2:$B="run_start",runs!$E$2:$E<>"test",' +
    'COUNTIFS(runs!$C$2:$C,runs!$C$2:$C,runs!$B$2:$B,"run_end")=0)),0)');

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
  assert.deepStrictEqual(env.ss.getSheets().map((s) => s.name), ['runs', 'Summary']);
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
