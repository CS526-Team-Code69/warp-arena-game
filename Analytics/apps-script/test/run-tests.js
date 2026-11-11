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
const DATA_SHEETS = ['runs', 'upgrade_picks', 'supply_picks', 'damage', 'v2_days', 'v2_purchases', 'v2_events'];
// The tabs of a new spreadsheet after setup(): the Dashboard first, the data tabs, then calc, which is hidden.
const SHEETS_AFTER_SETUP = ['Dashboard'].concat(DATA_SHEETS, ['calc']);
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
  // A cell under a merged range, other than its first, shows nothing: writing there is a mistake of the layout.
  checkShown() {
    for (const a1 of this.sheet.merges) {
      const m = /^([A-Z]+)(\d+):/.exec(a1);
      if (this.meets(a1) && (this.numRows > 1 || this.numCols > 1 || this.row !== Number(m[2]) || this.col !== colNumber(m[1]))) {
        throw new Error('Writing ' + this.getA1Notation() + ' under the merged cells ' + a1);
      }
    }
  }
  setValues(values) {
    this.checkShape(values);
    this.checkShown();
    values.forEach((r, i) => r.forEach((v, j) => this.sheet.setCell(this.row + i, this.col + j, v, '')));
    return this;
  }
  setFormulas(formulas) {
    this.checkShape(formulas);
    this.checkShown();
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
  // Formats are kept per cell, so that a test can read them and a snapshot of the sheet holds them.
  format(name, value) {
    for (let i = 0; i < this.numRows; i++) {
      for (let j = 0; j < this.numCols; j++) {
        const key = (this.row + i) + ',' + (this.col + j);
        (this.sheet.formats[key] = this.sheet.formats[key] || {})[name] = value;
      }
    }
    return this;
  }
  setFontWeight(weight) { return this.format('weight', weight); }
  setFontSize(size) { return this.format('size', size); }
  setFontColor(colour) { return this.format('colour', colour); }
  setFontStyle(style) { return this.format('style', style); }
  setWrap(wrap) { return this.format('wrap', wrap); }
  setVerticalAlignment(alignment) { return this.format('valign', alignment); }
  setNumberFormat(format) { return this.format('number', format); }
  // Whether this range and a range in A1 notation share a cell.
  meets(a1) {
    const m = /^([A-Z]+)(\d+):([A-Z]+)(\d+)$/.exec(a1);
    return this.row <= Number(m[4]) && Number(m[2]) <= this.row + this.numRows - 1 &&
      this.col <= colNumber(m[3]) && colNumber(m[1]) <= this.col + this.numCols - 1;
  }
  // Merging over a part of other merged cells is an error in Sheets.
  merge() {
    const a1 = this.getA1Notation();
    for (const other of this.sheet.merges) {
      if (other !== a1 && this.meets(other)) throw new Error('Cannot merge ' + a1 + ' over ' + other);
    }
    if (this.sheet.merges.indexOf(a1) < 0) this.sheet.merges.push(a1);
    return this;
  }
  breakApart() {
    this.sheet.merges = this.sheet.merges.filter((other) => !this.meets(other));
    return this;
  }
  setBackground(colour) {
    for (let i = 0; i < this.numRows; i++) {
      for (let j = 0; j < this.numCols; j++) this.sheet.backgrounds[(this.row + i) + ',' + (this.col + j)] = colour;
    }
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
    this.spec.ranges.push(range.sheet.name + '!' + range.getA1Notation()); // a chart may read another sheet
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

// Only the column builder has setStacked / setTitle / axis titles / colours / the legend, as in Apps Script.
class MockColumnChartBuilder extends MockChartBuilder {
  constructor(base) {
    super(base.sheet);
    this.spec = base.spec;
  }
  setStacked() { this.spec.stacked = true; return this; }
  setTitle(t) { this.spec.title = t; return this; }
  setXAxisTitle(t) { this.spec.xTitle = t; return this; }
  setYAxisTitle(t) { this.spec.yTitle = t; return this; }
  setColors(colours) {
    if (!Array.isArray(colours) || !colours.every((c) => /^#[0-9a-fA-F]{6}$/.test(c))) throw new Error('setColors expects CSS colours');
    this.spec.colours = colours.slice();
    return this;
  }
  setLegendPosition(position) {
    if (['TOP', 'RIGHT', 'BOTTOM', 'NONE'].indexOf(position) < 0) throw new Error('setLegendPosition expects a Charts.Position');
    this.spec.legend = position;
    return this;
  }
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
  constructor(env, name, ss) {
    this.env = env;
    this.ss = ss;
    this.name = name;
    this.cells = []; // cells[row - 1][col - 1] = { value, formula }
    this.charts = [];
    this.frozenRows = 0;
    this.appendsWithoutLock = 0;
    this.columnFormats = {}; // column number -> number format
    this.backgrounds = {}; // "row,column" -> background colour
    this.formats = {}; // "row,column" -> { weight, size, colour, style, wrap, valign, number }
    this.merges = []; // merged cells, in A1 notation
    this.columnWidths = {}; // column number -> px
    this.rowHeights = {}; // row number -> px
    this.gridlinesHidden = false;
    this.hidden = false;
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
  // clear() takes the formats with the contents. Merged cells, sizes, gridlines and a hidden tab stay as they are: what
  // must not survive a rebuild has to be undone by the script itself.
  clear() {
    this.cells = [];
    this.backgrounds = {};
    this.formats = {};
    return this;
  }
  getMaxRows() { return 1000; }
  getMaxColumns() { return 26; }
  setSizes(sizes, first, count, px) {
    for (const v of [first, count, px]) {
      if (!Number.isInteger(v) || v < 1) throw new Error('Bad size argument: ' + v);
    }
    for (let i = first; i < first + count; i++) sizes[i] = px;
    return this;
  }
  setColumnWidth(column, width) { return this.setSizes(this.columnWidths, column, 1, width); }
  setColumnWidths(first, count, width) { return this.setSizes(this.columnWidths, first, count, width); }
  setRowHeight(row, height) { return this.setSizes(this.rowHeights, row, 1, height); }
  setRowHeights(first, count, height) { return this.setSizes(this.rowHeights, first, count, height); }
  setHiddenGridlines(hidden) {
    this.gridlinesHidden = hidden === true;
    return this;
  }
  hideSheet() {
    if (!this.ss.sheets.some((s) => s !== this && !s.hidden)) throw new Error('Cannot hide the only visible sheet');
    this.hidden = true;
    return this;
  }
  showSheet() {
    this.hidden = false;
    return this;
  }
  isSheetHidden() { return this.hidden; }
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
    this.active = null;
  }
  getId() { return this.id; }
  getUrl() { return 'https://docs.example.invalid/' + this.id; }
  getSheets() { return this.sheets.slice(); }
  getSheetByName(name) { return this.sheets.find((s) => s.name === name) || null; }
  insertSheet(name) {
    if (this.getSheetByName(name)) throw new Error('A sheet with the name "' + name + '" already exists.');
    const s = new MockSheet(this.env, name, this);
    this.sheets.push(s);
    this.active = s;
    return s;
  }
  deleteSheet(sheet) {
    const i = this.sheets.indexOf(sheet);
    if (i < 0) throw new Error('Not a sheet of this spreadsheet');
    if (this.sheets.length === 1) throw new Error('Cannot delete the only sheet');
    this.sheets.splice(i, 1);
    if (this.active === sheet) this.active = this.sheets[0];
  }
  setActiveSheet(sheet) {
    if (this.sheets.indexOf(sheet) < 0) throw new Error('Not a sheet of this spreadsheet');
    this.active = sheet;
    return sheet;
  }
  // Positions count from 1, as in Apps Script.
  moveActiveSheet(position) {
    if (!this.active) throw new Error('No active sheet');
    if (!Number.isInteger(position) || position < 1 || position > this.sheets.length) throw new Error('Bad position: ' + position);
    this.sheets.splice(this.sheets.indexOf(this.active), 1);
    this.sheets.splice(position - 1, 0, this.active);
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
    Charts: { Position: { TOP: 'TOP', RIGHT: 'RIGHT', BOTTOM: 'BOTTOM', NONE: 'NONE' } },
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

// ---------------------------------------------------------------------------
// What the formulas give: a small evaluator for the formulas of "calc" and the sentences of "Dashboard"
// ---------------------------------------------------------------------------

// One criterion of COUNTIFS and its kin against a cell: a number, TRUE, "text" with * and ? wildcards, "<>text", ">n" and ">=n".
// A date counts as its time in milliseconds.
function criterionMatches(cell, crit) {
  if (crit === true) return cell === true;
  if (typeof crit === 'number') return typeof cell === 'number' && cell === crit;
  const wild = (pattern) => new RegExp('^' + pattern.split(/([*?])/).map((s) => (
    s === '*' ? '.*' : s === '?' ? '.' : s.replace(/[.+^${}()|[\]\\]/g, '\\$&'))).join('') + '$', 'i');
  const compare = /^(>=|>)(-?\d+(?:\.\d+)?)$/.exec(crit);
  if (compare) {
    const n = isDate(cell) ? cell.getTime() : cell;
    return typeof n === 'number' && (compare[1] === '>' ? n > Number(compare[2]) : n >= Number(compare[2]));
  }
  if (crit.startsWith('<>')) return !wild(crit.slice(2)).test(String(cell));
  return typeof cell === 'string' && wild(crit).test(cell);
}

// One COUNTIFS / SUMIFS / MINIFS term over whole columns of one data sheet (any column letters). As a whole-column reference
// does, it reads the header row and the empty rows under the data too.
function ifsTerm(term, env) {
  const m = /^(COUNTIFS|SUMIFS|MINIFS)\((.*)\)$/.exec(term);
  if (!m) throw new Error('Unexpected term: ' + term);
  const args = m[2].match(/\w+!\$[A-Z]+:\$[A-Z]+|"[^"]*"|TRUE|-?\d+(?:\.\d+)?/g);
  assert.strictEqual(args.join(','), m[2], 'every argument is understood: ' + term);
  const refOf = (arg) => {
    const r = /^(\w+)!\$([A-Z]+):\$\2$/.exec(arg);
    if (!r) throw new Error('Not a whole column: ' + arg + ' in ' + term);
    return { sheet: r[1], col: colNumber(r[2]) };
  };
  const valueRef = m[1] === 'COUNTIFS' ? null : refOf(args[0]);
  const pairs = [];
  for (let i = valueRef ? 1 : 0; i < args.length; i += 2) {
    const raw = args[i + 1];
    pairs.push([refOf(args[i]), raw === 'TRUE' ? true : raw.startsWith('"') ? raw.slice(1, -1) : Number(raw)]);
  }
  assert.ok(pairs.length > 0, term);
  const names = new Set(pairs.map((p) => p[0].sheet).concat(valueRef ? [valueRef.sheet] : []));
  assert.strictEqual(names.size, 1, 'one data sheet per term: ' + term);
  const sheet = sheetOf(env, pairs[0][0].sheet);
  const picked = [];
  for (let r = 1; r <= sheet.getLastRow() + 3; r++) {
    if (!pairs.every(([ref, crit]) => criterionMatches(sheet.getCell(r, ref.col).value, crit))) continue;
    const v = valueRef ? sheet.getCell(r, valueRef.col).value : 1;
    picked.push(isDate(v) ? v.getTime() : v);
  }
  const numbers = picked.filter((v) => typeof v === 'number');
  if (m[1] === 'COUNTIFS') return picked.length;
  if (m[1] === 'SUMIFS') return numbers.reduce((a, b) => a + b, 0);
  return numbers.length ? Math.min.apply(null, numbers) : 0;
}

// COUNTIFS(...)+COUNTIFS(...)+...: the terms added up.
function ifsSum(body, env) {
  return body.split(/\+(?=[A-Z]+IFS\()/).reduce((sum, term) => sum + ifsTerm(term, env), 0);
}

// The one formula of calc that counts distinct runs: story runs that sent run_start.
const STARTED_FORMULA = '=IFERROR(ROWS(UNIQUE(FILTER(runs!$C$2:$C,runs!$B$2:$B="run_start",runs!$E$2:$E<>"test",' +
  'COUNTIFS(runs!$C$2:$C,runs!$C$2:$C,runs!$Q$2:$Q,"story")>0))),0)';
// The one formula of calc whose result has no fixed size: the first purchase of each run. The mock does not work it out;
// spillFirstPurchases() writes what it reads.
const FIRST_PURCHASES_FORMULA = '=IFERROR(FILTER({v2_purchases!$C$2:$C,v2_purchases!$G$2:$G,v2_purchases!$K$2:$K,v2_purchases!$I$2:$I},' +
  'v2_purchases!$C$2:$C<>"",v2_purchases!$E$2:$E<>"test",IFERROR(MATCH(v2_purchases!$C$2:$C,v2_purchases!$C$2:$C,0),0)=ROW(v2_purchases!$C$2:$C)-1),{"","","",""})';

// What a cell shows: its value, or what its formula gives.
function shown(env, sheetName, row, col) {
  const cell = sheetOf(env, sheetName).getCell(row, col);
  return cell.formula ? evalFormula(env, sheetName, cell.formula) : cell.value;
}

// The same by address: "B5" or "$B$5".
function shownAt(env, sheetName, a1) {
  const m = /^\$?([A-Z]+)\$?(\d+)$/.exec(a1);
  if (!m) throw new Error('Not a cell: ' + a1);
  return shown(env, sheetName, Number(m[2]), colNumber(m[1]));
}

// A sentence of the Dashboard: "text" pieces, cells of calc and TEXT(cell,"0%"), joined by &.
function sentence(env, body) {
  const pieces = body.match(/"(?:[^"]|"")*"|TEXT\(calc!\$[A-Z]+\$\d+,"[^"]*"\)|calc!\$[A-Z]+\$\d+/g) || [];
  assert.strictEqual(pieces.join('&'), body, 'every piece of the sentence is understood: ' + body);
  return pieces.map((piece) => {
    if (piece.startsWith('"')) return piece.slice(1, -1).replace(/""/g, '"');
    let m = /^TEXT\(calc!(\$[A-Z]+\$\d+),"0%"\)$/.exec(piece);
    if (m) return Math.round(shownAt(env, 'calc', m[1]) * 100) + '%';
    m = /^calc!(\$[A-Z]+\$\d+)$/.exec(piece);
    if (!m) throw new Error('Unexpected piece: ' + piece);
    return String(shownAt(env, 'calc', m[1]));
  }).join('');
}

// Every shape of formula the two tabs hold. A formula of another shape throws, so nothing is passed unread.
function evalFormula(env, sheetName, formula) {
  const f = formula.replace(/^=/, '');
  const here = (letters, row) => shown(env, sheetName, Number(row), colNumber(letters));
  let m;
  if (f.startsWith('"')) return sentence(env, f);
  if (formula === FIRST_PURCHASES_FORMULA) return ''; // an empty list until spillFirstPurchases() fills it
  if (formula === STARTED_FORMULA) {
    const rows = runsSheet(env).dataRows();
    const story = new Set(rows.filter((r) => r[COL.mode] === 'story').map((r) => r[COL.run_id]));
    return new Set(rows.filter((r) => r[COL.event] === 'run_start' && r[COL.edition] !== 'test' && story.has(r[COL.run_id]))
      .map((r) => r[COL.run_id])).size;
  }
  // =IFERROR(C81/B81,0): a share, 0 while there is nothing to divide by.
  if ((m = /^IFERROR\(([A-Z]+)(\d+)\/([A-Z]+)(\d+),0\)$/.exec(f))) {
    const whole = here(m[3], m[4]);
    return whole ? here(m[1], m[2]) / whole : 0;
  }
  // =SUM(B18:J27)
  if ((m = /^SUM\(([A-Z]+)(\d+):([A-Z]+)(\d+)\)$/.exec(f))) {
    let sum = 0;
    for (let r = Number(m[2]); r <= Number(m[4]); r++) {
      for (let c = colNumber(m[1]); c <= colNumber(m[3]); c++) {
        const v = shown(env, sheetName, r, c);
        if (typeof v === 'number') sum += v;
      }
    }
    return sum;
  }
  // =INDEX(SORT($A$81:$D$97,4,FALSE),1,1): one value of the table sorted by a column, largest first; equal rows keep their order.
  if ((m = /^INDEX\(SORT\(\$([A-Z]+)\$(\d+):\$([A-Z]+)\$(\d+),(\d+),FALSE\),(\d+),(\d+)\)$/.exec(f))) {
    const rows = [];
    for (let r = Number(m[2]); r <= Number(m[4]); r++) {
      const row = [];
      for (let c = colNumber(m[1]); c <= colNumber(m[3]); c++) row.push(shown(env, sheetName, r, c));
      rows.push(row);
    }
    const by = Number(m[5]) - 1;
    const sorted = rows.map((row, i) => [row, i]).sort((a, b) => (b[0][by] - a[0][by]) || (a[1] - b[1])).map((pair) => pair[0]);
    return sorted[Number(m[6]) - 1][Number(m[7]) - 1];
  }
  // =COUNTIFS($N$5:$N,"?*",$P$5:$P,"Armament"): columns of the tab itself, from a row down.
  if ((m = /^COUNTIFS\((\$[A-Z]+\$\d+:\$[A-Z]+,.*)\)$/.exec(f))) {
    const args = m[1].match(/\$[A-Z]+\$\d+:\$[A-Z]+|"[^"]*"/g);
    assert.strictEqual(args.join(','), m[1], 'every argument is understood: ' + f);
    const pairs = [];
    for (let i = 0; i < args.length; i += 2) {
      const r = /^\$([A-Z]+)\$(\d+):\$\1$/.exec(args[i]);
      pairs.push({ col: colNumber(r[1]), from: Number(r[2]), crit: args[i + 1].slice(1, -1) });
    }
    assert.ok(pairs.every((p) => p.from === pairs[0].from), 'the columns start on the same row: ' + f);
    const sheet = sheetOf(env, sheetName);
    let n = 0;
    for (let r = pairs[0].from; r <= sheet.getLastRow() + 3; r++) {
      if (pairs.every((p) => criterionMatches(sheet.getCell(r, p.col).value, p.crit))) n++;
    }
    return n;
  }
  // =IF(ISNUMBER($B$9),COUNTIFS(...,">="&$B$9),0): nothing is counted while the cell holds text.
  if ((m = /^IF\(ISNUMBER\((\$[A-Z]+\$\d+)\),(.*),0\)$/.exec(f))) {
    const since = shownAt(env, sheetName, m[1]);
    if (typeof since !== 'number') return 0;
    return ifsSum(m[2].split('"&' + m[1]).join(since + '"'), env);
  }
  // =IF(COUNTIFS(...)=0,"text",MINIFS(...)): the text while nothing is counted.
  if ((m = /^IF\((COUNTIFS\(.*\))=0,"([^"]*)",(MINIFS\(.*\))\)$/.exec(f))) return ifsTerm(m[1], env) === 0 ? m[2] : ifsTerm(m[3], env);
  // =COUNTIFS(...)-$B$10
  if ((m = /^(COUNTIFS\(.*\))-(\$[A-Z]+\$\d+)$/.exec(f))) return ifsTerm(m[1], env) - shownAt(env, sheetName, m[2]);
  return ifsSum(f, env);
}

// The first purchase of each run as FIRST_PURCHASES_FORMULA reads "v2_purchases": the first row of each run_id, when that row
// is not a test row. Written where the formula's result would spill, from N5 down.
function spillFirstPurchases(env) {
  const calc = sheetOf(env, 'calc');
  assert.strictEqual(calc.getCell(5, 14).formula, FIRST_PURCHASES_FORMULA);
  const rows = sheetOf(env, 'v2_purchases').dataRows();
  const ids = rows.map((r) => r[2]);
  const firsts = rows.filter((r, i) => r[2] !== '' && r[4] !== 'test' && ids.indexOf(r[2]) === i).map((r) => [r[2], r[6], r[10], r[8]]);
  firsts.forEach((row, i) => row.forEach((v, j) => calc.setCell(5 + i, 14 + j, v, '')));
  return firsts;
}

// A table of calc by the start of its title: where it stands, and its header.
function tableOf(env, titleStart) {
  const calc = sheetOf(env, 'calc');
  for (let r = 2; r <= calc.getLastRow(); r++) {
    const title = calc.getCell(r, 1).value;
    if (typeof title !== 'string' || !title.startsWith(titleStart)) continue;
    const header = [];
    for (let c = 1; calc.getCell(r + 1, c).value !== ''; c++) header.push(calc.getCell(r + 1, c).value);
    let last = r + 1;
    while (calc.getCell(last + 1, 1).value !== '' || calc.getCell(last + 1, 1).formula) last++;
    return { title: title, top: r + 1, first: r + 2, last: last, width: header.length, header: header };
  }
  throw new Error('No table "' + titleStart + '" in calc');
}

// The numbers of a table that are not 0, as { 'row label/column label': n }. Every cell must show a number.
function gridOf(env, table) {
  const out = {};
  for (let r = table.first; r <= table.last; r++) {
    for (let c = 2; c <= table.width; c++) {
      const v = shown(env, 'calc', r, c);
      assert.strictEqual(typeof v, 'number', table.title + ': a number at row ' + r + ', column ' + c);
      if (v) out[shown(env, 'calc', r, 1) + '/' + table.header[c - 1]] = v;
    }
  }
  return out;
}

// What a range of a chart shows ("calc!A17:J27"), row by row.
function rangeShown(env, a1) {
  const m = /^(\w+)!([A-Z]+)(\d+):([A-Z]+)(\d+)$/.exec(a1);
  if (!m) throw new Error('Not a range: ' + a1);
  const out = [];
  for (let r = Number(m[3]); r <= Number(m[5]); r++) {
    const row = [];
    for (let c = colNumber(m[2]); c <= colNumber(m[4]); c++) row.push(shown(env, m[1], r, c));
    out.push(row);
  }
  return out;
}

// Everything a sheet holds, as text: its cells (values and formulas), formats, merged cells, sizes, charts and whether it shows.
function snapshot(sheet) {
  return JSON.stringify({ name: sheet.name, cells: sheet.cells, backgrounds: sheet.backgrounds, formats: sheet.formats,
    merges: sheet.merges.slice().sort(), widths: sheet.columnWidths, heights: sheet.rowHeights, gridlines: sheet.gridlinesHidden,
    hidden: sheet.hidden, charts: sheet.charts.map((c) => c.spec), frozen: sheet.frozenRows });
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

test('setup makes the data tabs with their headers, stores the id, and builds "Dashboard" first and "calc" hidden', () => {
  const env = makeEnv();
  env.gs.setup();
  assert.deepStrictEqual(env.ss.getSheets().map((s) => s.name), SHEETS_AFTER_SETUP, 'empty Sheet1 is reused as "runs"; the Dashboard comes first');
  const runs = runsSheet(env);
  assert.deepStrictEqual(runs.rowValues(1), HEADER);
  assert.strictEqual(runs.frozenRows, 1);
  assert.strictEqual(env.props.SPREADSHEET_ID, env.ss.id);

  assert.deepStrictEqual(env.ss.getSheets().filter((s) => s.hidden).map((s) => s.name), ['calc'], 'calc is the one hidden tab');
  assert.strictEqual(env.ss.active.name, 'Dashboard', 'the Dashboard is the tab shown after setup');
  assert.strictEqual(env.ss.getSheetByName('Dashboard').charts.length, 9);
  assert.strictEqual(env.ss.getSheetByName('calc').charts.length, 0, 'calc holds tables only');
  for (const name of DATA_SHEETS) {
    const sheet = env.ss.getSheetByName(name);
    assert.strictEqual(sheet.getLastRow(), 1, name + ': the header and no row');
    assert.strictEqual(sheet.charts.length, 0, name);
    assert.deepStrictEqual(sheet.merges, [], name);
  }
});

test('setup is safe to run twice: the same tabs, nine charts, rows in "runs" kept', () => {
  const env = readyEnv();
  post(env, startEvent());
  post(env, endEvent());
  post(env, endEvent({ run_id: 'a'.repeat(32), outcome: 'victory', stage: 'final_boss', wave: 14 }));
  const before = runsSheet(env).dataRows().map((r) => r.slice(1));
  env.gs.setup();
  env.gs.setup();
  assert.deepStrictEqual(env.ss.getSheets().map((s) => s.name), SHEETS_AFTER_SETUP);
  assert.strictEqual(env.ss.getSheetByName('Dashboard').charts.length, 9);
  assert.deepStrictEqual(runsSheet(env).rowValues(1), HEADER);
  assert.deepStrictEqual(runsSheet(env).dataRows().map((r) => r.slice(1)), before);
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
    [v2Event({ kind: 'crate_offer', detail: '' }), 'bad detail'],
    [v2Event({ kind: 'crate_offers', detail: 'Afternoon:Railgun' }), 'bad kind'],
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
  assert.deepStrictEqual(env.ss.getSheets().map((s) => s.name), SHEETS_AFTER_SETUP, 'setup twice adds no sheet');
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

// ---------------------------------------------------------------------------
// "Dashboard" and "calc": the two tabs setup() builds
// ---------------------------------------------------------------------------

const OLD_SUMMARIES = ['Summary', 'Upgrade picks', 'Supply picks', 'Damage sources', 'Story endings', 'Story spending', 'Story weapons',
  'Story damage'];
const DAY_LABELS = ['Day 0', 'Day 1', 'Day 2', 'Day 3', 'Day 4', 'Day 5', 'Day 6', 'Day 7', 'Day 8+'];
const END_LABELS = ['Enemy', 'Tyrant King', 'Own weapon', 'Wall slam', 'Burn / poison', 'Base destroyed', 'Victory', 'Left the run', 'Other'];
const ROOM_LABELS = ['Armament Bay', 'Energy Lab', 'Ship', 'Control Center', 'Other rooms'];
const DAMAGE_LABELS = ['Grunt', 'Tyrant King', 'Own weapon', 'Wall slam', 'Brute', 'Spitter', 'Shadow (night)', 'Skitter', 'Other enemies'];
const OPEN_LABELS = ['Enemy', 'Tyrant King', 'Own weapon', 'Wall slam', 'Burn / poison', 'Black hole', 'Void trial', 'Unknown', 'Victory',
  'Restart', 'Quit to title'];
const STORY_WEAPONS = ['Rifle', 'Knife', 'RocketLauncher', 'GravGun', 'ScatterCannon', 'PhaseDagger', 'PlasmaLauncher', 'Flamethrower',
  'CryoCannon', 'ArcCoil', 'SporeLauncher', 'Railgun', 'Minigun', 'SwarmLauncher', 'OrbitBlades', 'SentryDrone', 'ShieldRing'];
const DOT = ' \u00B7 ';
const EXAMPLE_LABEL = 'EXAMPLE (simulated data, not real players)';
const END_COLOURS = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e34948', '#e87ba4', '#008300', '#b1afa8', '#5f5e5a'];
const ROOM_COLOURS = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#b1afa8'];
const DAMAGE_COLOURS = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#6250d6', '#e34948', '#b1afa8'];
const OPEN_COLOURS = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e34948', '#6250d6', '#9c3d8c', '#5f5e5a', '#008300', '#b1afa8', '#d6d4cc'];

// The nine charts of the Dashboard in the order setup() adds them: where each stands (the cell under its top left corner),
// its size in pixels, and the ranges of calc it reads. A card is 600 px wide: six columns of 100 px from B, or from I.
const CHARTS = [
  { name: 'card 1, live', type: 'COLUMN', stacked: true, row: 8, col: 2, width: 600, height: 294, ranges: ['calc!A17:J27'],
    title: 'How story runs end, by day', colours: END_COLOURS },
  { name: 'card 1, example', type: 'COLUMN', stacked: true, row: 23, col: 2, width: 600, height: 294, ranges: ['calc!A30:J39'],
    title: 'Example: how story runs end, by day', colours: END_COLOURS },
  { name: 'card 2, live', type: 'COLUMN', stacked: true, row: 8, col: 9, width: 600, height: 294, ranges: ['calc!A42:F51'],
    title: 'Resources spent per day, by room', colours: ROOM_COLOURS },
  { name: 'card 2, example', type: 'COLUMN', stacked: true, row: 23, col: 9, width: 600, height: 294, ranges: ['calc!A62:F70'],
    title: 'Example: resources spent per day, by room', colours: ROOM_COLOURS },
  { name: 'card 3, live', type: 'BAR', stacked: false, row: 41, col: 2, width: 600, height: 378, ranges: ['calc!A100:A117', 'calc!B100:B117'],
    title: 'Take rate per weapon, when a crate offers it', colours: ['#2a78d6'] },
  { name: 'card 3, example', type: 'BAR', stacked: false, row: 60, col: 2, width: 600, height: 378, ranges: ['calc!A130:A147', 'calc!D130:D147'],
    title: 'Example: take rate per weapon, when a crate offers it', colours: ['#2a78d6'] },
  { name: 'card 4, live', type: 'COLUMN', stacked: true, percent: true, row: 41, col: 9, width: 600, height: 378, ranges: ['calc!A150:J160'],
    title: 'Damage to the player per day, by source', colours: DAMAGE_COLOURS },
  { name: 'card 4, example', type: 'COLUMN', stacked: true, percent: true, row: 60, col: 9, width: 600, height: 378, ranges: ['calc!A163:J172'],
    title: 'Example: damage to the player per day, by source', colours: DAMAGE_COLOURS },
  { name: 'open mode', type: 'COLUMN', stacked: true, row: 81, col: 2, width: 1240, height: 294, ranges: ['calc!A175:L190'],
    title: 'How open-mode runs end, by wave', colours: OPEN_COLOURS },
];
const LIVE_CHARTS = CHARTS.filter((c) => !/example/.test(c.name));
// The text rows of the Dashboard, each one merged cell: the title, the line on what the page is and the totals, then for each
// band of two cards its headers, captions and labels, and the open mode's header and caption.
const TEXT_ROWS = ['B1:N1', 'B2:N2', 'B3:N3',
  'B5:G5', 'I5:N5', 'B6:G6', 'I6:N6', 'B7:G7', 'I7:N7', 'B22:G22', 'I22:N22',
  'B38:G38', 'I38:N38', 'B39:G39', 'I39:N39', 'B40:G40', 'I40:N40', 'B59:G59', 'I59:N59',
  'B79:N79', 'B80:N80'];

const idOf = (n) => n.toString(16).padStart(32, '0');
const rowsFrom = (first, last) => Array.from({ length: last - first + 1 }, (_, i) => first + i);
const dashboardOf = (env) => sheetOf(env, 'Dashboard');
const calcOf = (env) => sheetOf(env, 'calc');
// What a text row of the Dashboard says, by its first cell.
const says = (env, a1) => shownAt(env, 'Dashboard', a1);
const chartSpecs = (env) => dashboardOf(env).charts.map((c) => c.spec);

function storyEnd(n, overrides) {
  return endEvent(Object.assign({ run_id: idOf(n), mode: 'story', stage: 'day', wave: 3, cause: 'enemy' }, overrides));
}

test('crate_offer is a v2_event kind: one row for each weapon a crate offered, stored as crate rows are', () => {
  const env = readyEnv();
  for (const ev of [
    v2Event({ kind: 'crate_offer', detail: 'Afternoon:Railgun', value: 1 }),
    v2Event({ kind: 'crate_offer', detail: ' Afternoon:Minigun ', value: '2' }),
    v2Event({ kind: 'crate', detail: 'Afternoon:Minigun', value: 2 }),
  ]) assert.deepStrictEqual(post(env, ev).json, { ok: true });
  const sheet = sheetOf(env, 'v2_events');
  assert.deepStrictEqual(sheet.dataRows().map((r) => r.slice(1)), [
    ['v2_event', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9', 2, 'dusk', 'crate_offer', 'Afternoon:Railgun', 1],
    ['v2_event', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9', 2, 'dusk', 'crate_offer', 'Afternoon:Minigun', 2],
    ['v2_event', RUN_ID, SESSION_ID, 'en', '1.0.0+3fa2b1c9', 2, 'dusk', 'crate', 'Afternoon:Minigun', 2],
  ]);
  for (const detail of ['', undefined, '%%']) {
    assert.deepStrictEqual(post(env, v2Event({ kind: 'crate_offer', detail: detail })).json, { ok: false, error: 'bad detail' });
  }
  assert.strictEqual(sheet.getLastRow(), 4, 'an offer that names no weapon writes nothing');
});

test('setup deletes the eight summary tabs of earlier layouts; data rows and every other tab stay as they are', () => {
  const env = makeEnv();
  for (const ev of [
    startEvent({ mode: 'story', stage: 'tutorial', wave: 0 }), storyEnd(0, { run_id: RUN_ID }), endEvent({ run_id: idOf(2) }),
    upgradeEvent(), supplyEvent(),
    damageEvent([{ source: 'shadow', stage: 'night', wave: 2, damage: 3, damage_pct: 6, hits: 2 }], { mode: 'story' }),
    v2DayEvent(), v2PurchaseEvent(),
    v2Event({ kind: 'crate_offer', detail: 'Afternoon:Railgun', value: 1 }), v2Event({ kind: 'crate', detail: 'Afternoon:Railgun', value: 1 }),
  ]) assert.deepStrictEqual(post(env, ev).json, { ok: true });

  // The eight summary tabs as an earlier setup() left them: formulas and a chart each.
  for (const name of OLD_SUMMARIES) {
    const old = env.ss.insertSheet(name);
    old.getRange(1, 1, 1, 2).setFormulas([['=COUNTA(runs!A:A)', '=SUM(damage!L:L)']]);
    old.insertChart(old.newChart().asColumnChart().addRange(old.getRange(1, 1, 1, 2)).build());
  }
  // Tabs this script does not own: those of a still older layout, which it stopped writing, and one somebody made by hand.
  const oldRuns = env.ss.insertSheet('v2_runs');
  oldRuns.appendRow(['received_at', 'event', 'run_id']);
  oldRuns.appendRow(['2026-11-06', 'v2_run_start', idOf(1)]);
  const oldSummary = env.ss.insertSheet('V2 Summary');
  oldSummary.getRange(1, 1, 1, 2).setFormulas([['=COUNTA(v2_runs!A:A)', '=SUM(v2_runs!L:L)']]);
  oldSummary.insertChart(oldSummary.newChart().asColumnChart().addRange(oldSummary.getRange(1, 1, 1, 2)).build());
  env.ss.insertSheet('Notes').getRange(1, 1).setValue('my own notes');
  const KEPT = ['Sheet1', 'v2_runs', 'V2 Summary', 'Notes'];

  const before = {};
  for (const name of KEPT) before[name] = snapshot(sheetOf(env, name));
  const rowsBefore = {};
  for (const name of DATA_SHEETS) rowsBefore[name] = JSON.stringify(sheetOf(env, name).dataRows());
  assert.ok(DATA_SHEETS.every((name) => sheetOf(env, name).getLastRow() > 1), 'every data tab holds a row');

  for (let run = 1; run <= 2; run++) {
    env.gs.setup();
    const names = env.ss.getSheets().map((s) => s.name);
    for (const name of OLD_SUMMARIES) assert.ok(names.indexOf(name) < 0, 'run ' + run + ': "' + name + '" is gone');
    assert.strictEqual(names[0], 'Dashboard', 'run ' + run + ': the Dashboard comes first');
    assert.deepStrictEqual(names.slice().sort(), ['Dashboard', 'calc'].concat(DATA_SHEETS, KEPT).sort(), 'run ' + run + ': no other tab added or removed');
    for (const name of DATA_SHEETS) {
      assert.strictEqual(JSON.stringify(sheetOf(env, name).dataRows()), rowsBefore[name], 'run ' + run + ': rows of "' + name + '" untouched');
      assert.strictEqual(sheetOf(env, name).hidden, false, name + ' stays in sight');
    }
    for (const name of KEPT) assert.strictEqual(snapshot(sheetOf(env, name)), before[name], 'run ' + run + ': "' + name + '" as it was');
  }
});

test('the Dashboard is a fixed grid: set sizes, no gridlines, and nothing but titles, captions and labels in merged rows', () => {
  const env = readyEnv();
  const sheet = dashboardOf(env);

  // Columns: a margin, six of 100 px (card 1), a gutter, six of 100 px (card 2), a margin.
  assert.deepStrictEqual(sheet.columnWidths, { 1: 20, 2: 100, 3: 100, 4: 100, 5: 100, 6: 100, 7: 100, 8: 40,
    9: 100, 10: 100, 11: 100, 12: 100, 13: 100, 14: 100, 15: 20 });
  const heights = {};
  for (let r = 1; r <= 94; r++) heights[r] = 21;
  Object.assign(heights, { 1: 38, 5: 28, 38: 28, 79: 28, 6: 36, 39: 36 }); // the title, the headers, the two-line captions
  assert.deepStrictEqual(sheet.rowHeights, heights);
  assert.strictEqual(sheet.gridlinesHidden, true);
  assert.strictEqual(sheet.frozenRows, 0);

  // Every text is the first cell of a merged row, and the sheet holds nothing else: no table, no note beside a chart.
  assert.deepStrictEqual(sheet.merges.slice().sort(), TEXT_ROWS.slice().sort());
  const filled = [];
  sheet.cells.forEach((row, i) => row.forEach((cell, j) => {
    if (cell.value !== '' || cell.formula) filled.push(colLetter(j + 1) + (i + 1));
  }));
  assert.deepStrictEqual(filled.sort(), TEXT_ROWS.map((a1) => a1.split(':')[0]).sort());
  for (const a1 of filled) {
    const cell = sheet.getCell(Number(a1.slice(1)), colNumber(a1[0]));
    if (cell.formula) assert.ok(/^="/.test(cell.formula) && !/[A-Za-z_]+!/.test(cell.formula.replace(/calc!/g, '')), a1 + ' is a sentence over calc: ' + cell.formula);
  }

  // The fixed texts.
  assert.strictEqual(says(env, 'B1'), 'WARP ARENA' + DOT + 'analytics dashboard');
  assert.ok(/^Story mode: the four metrics of the design document\. .* an example drawn from simulated runs\.$/.test(says(env, 'B2')));
  assert.deepStrictEqual(['B5', 'I5', 'B38', 'I38', 'B79'].map((a1) => says(env, a1)), ['1' + DOT + 'How runs end',
    '2' + DOT + 'Where the resources go', '3' + DOT + 'Weapon choice', '4' + DOT + 'What hurts the player', 'Open mode (15 waves)']);
  assert.deepStrictEqual(['B22', 'I22', 'B59', 'I59'].map((a1) => says(env, a1)), [EXAMPLE_LABEL, EXAMPLE_LABEL, EXAMPLE_LABEL, EXAMPLE_LABEL]);
  assert.strictEqual(says(env, 'B6'), 'Runs ended on each day and in the final battle, by cause.');
  assert.strictEqual(says(env, 'I39'), 'Share of each day\'s damage to the player, by source.');

  // The sentences that count, while no player has sent a row.
  assert.strictEqual(says(env, 'B3'), 'Story runs started 0' + DOT + 'ended 0' + DOT + 'purchases 0' + DOT + 'crates opened 0');
  assert.strictEqual(says(env, 'I6'), 'First purchase of a run: Armament 0' + DOT + 'Energy 0' + DOT + 'Ship 0' + DOT + 'Control 0' + DOT +
    'Other 0. Chart: each day\'s spending by room.');
  assert.strictEqual(says(env, 'B7'), 'LIVE DATA' + DOT + 'story runs ended: 0');
  assert.strictEqual(says(env, 'I7'), 'LIVE DATA' + DOT + 'purchases: 0');
  assert.strictEqual(says(env, 'B39'), 'Taken by place: 1st 0%' + DOT + '2nd 0%' + DOT + '3rd 0%' + DOT +
    'crates left 0%. Chart: how often each weapon is taken when a crate offers it.');
  assert.strictEqual(says(env, 'B40'), 'LIVE DATA' + DOT + 'weapons offered: 0' + DOT + 'taken: 0');
  assert.strictEqual(says(env, 'I40'), 'LIVE DATA' + DOT + 'hits on the player: 0');
  assert.strictEqual(says(env, 'B80'), 'Runs ended on each wave, by cause. Total: 0.');

  // The grounds and the type: dark headers in white, blue "LIVE DATA" rows, orange example rows, grey italic captions.
  const ground = (row, first, last) => Array.from(new Set(rowsFrom(first, last).map((c) => sheet.backgrounds[row + ',' + c] || ''))).join(',');
  const type = (a1) => sheet.formats[a1.slice(1) + ',' + colNumber(a1[0])] || {};
  for (const [row, first, last] of [[5, 2, 7], [5, 9, 14], [38, 2, 7], [38, 9, 14], [79, 2, 14]]) {
    assert.strictEqual(ground(row, first, last), '#1F2937', 'header in row ' + row);
    assert.deepStrictEqual(type(colLetter(first) + row), { weight: 'bold', size: 12, colour: '#FFFFFF', valign: 'middle' });
  }
  for (const row of [7, 40]) for (const [first, last] of [[2, 7], [9, 14]]) assert.strictEqual(ground(row, first, last), '#DBEAFE', 'live label in row ' + row);
  for (const row of [22, 59]) for (const [first, last] of [[2, 7], [9, 14]]) assert.strictEqual(ground(row, first, last), '#f6b26b', 'example label in row ' + row);
  for (const a1 of ['B7', 'I7', 'B40', 'I40', 'B22', 'I22', 'B59', 'I59']) assert.deepStrictEqual(type(a1), { weight: 'bold', size: 9, valign: 'middle' }, a1);
  for (const a1 of ['B6', 'I6', 'B39', 'I39', 'B80']) {
    assert.deepStrictEqual(type(a1), { colour: '#6B7280', style: 'italic', size: 9, wrap: true, valign: 'top' }, a1);
  }
  assert.deepStrictEqual(type('B1'), { weight: 'bold', size: 18, valign: 'middle' });
  assert.deepStrictEqual(type('B2'), { colour: '#6B7280' });
  assert.strictEqual(ground(8, 1, 15), '', 'no ground under a chart');
  assert.strictEqual(Object.keys(sheet.backgrounds).length, 12 * 6 + 13, 'four headers, four live and four example labels of six cells, one header of thirteen');
});

test('the Dashboard has nine charts: four live, an example under each, one for the open mode; anchors, sizes and ranges on calc', () => {
  const env = readyEnv();
  const specs = chartSpecs(env);
  assert.strictEqual(specs.length, 9, '4 live + 4 example + 1 open');
  CHARTS.forEach((want, i) => {
    const spec = specs[i];
    assert.deepStrictEqual([spec.type, spec.stacked, spec.ranges, spec.numHeaders], [want.type, want.stacked, want.ranges, 1], want.name);
    assert.deepStrictEqual(spec.position, { row: want.row, col: want.col, offsetX: 0, offsetY: 0 }, want.name + ': its top left corner on a cell');
    assert.deepStrictEqual([spec.options.width, spec.options.height], [want.width, want.height], want.name + ': its size');
    assert.strictEqual(spec.title, want.title, want.name);
    assert.deepStrictEqual(spec.colours, want.colours, want.name + ': one fixed colour list');
    assert.strictEqual(spec.options.isStacked, want.percent ? 'percent' : undefined, want.name);
    assert.ok(spec.ranges.every((range) => range.startsWith('calc!')), want.name + ': reads calc only');
    assert.strictEqual(spec.legend, want.type === 'BAR' ? 'NONE' : undefined, want.name + ': one series needs no legend');
  });

  // A chart covers its card's columns and the rows under its label, and nothing else: no text row, no other chart.
  const texts = TEXT_ROWS.map((a1) => Number(/\d+/.exec(a1)[0]));
  const rectangles = CHARTS.map((c) => ({ name: c.name, top: c.row, bottom: c.row + c.height / 21 - 1, left: c.col, right: c.col + (c.width === 600 ? 5 : 12) }));
  for (const r of rectangles) {
    assert.ok(Number.isInteger(r.bottom), r.name + ': a whole number of 21 px rows');
    assert.ok(texts.every((row) => row < r.top || row > r.bottom), r.name + ': no text row under it');
    assert.ok(texts.indexOf(r.top - 1) >= 0, r.name + ': right under its label or caption');
    for (const o of rectangles) {
      if (o !== r) assert.ok(o.bottom < r.top || o.top > r.bottom || o.right < r.left || o.left > r.right, r.name + ' is clear of ' + o.name);
    }
    assert.ok(r.left >= 2 && r.right <= 14 && r.bottom <= 94, r.name + ': inside the margins');
  }

  // A live chart and the example under it: the same kind, the same colours, the same series by name and order.
  for (let i = 0; i < 8; i += 2) {
    const [live, example] = [specs[i], specs[i + 1]];
    assert.deepStrictEqual([example.type, example.stacked, example.colours, example.options.isStacked, example.legend, example.xTitle, example.yTitle],
      [live.type, live.stacked, live.colours, live.options.isStacked, live.legend, live.xTitle, live.yTitle], CHARTS[i].name);
    assert.strictEqual(example.title, 'Example: ' + live.title.charAt(0).toLowerCase() + live.title.slice(1));
    assert.deepStrictEqual([example.position.col, example.position.row > live.position.row], [live.position.col, true], 'the example stands under the live chart');
    const series = (spec) => spec.ranges.length > 1 ? rangeShown(env, spec.ranges[spec.ranges.length - 1])[0] : rangeShown(env, spec.ranges[0])[0].slice(1);
    assert.deepStrictEqual(series(example), series(live), CHARTS[i].name + ': the series of the example are those of the live chart');
    if (live.type === 'COLUMN') assert.strictEqual(series(live).length, live.colours.length, CHARTS[i].name + ': a colour for each series');
  }
  assert.deepStrictEqual(rangeShown(env, 'calc!A17:J27')[0], ['Day'].concat(END_LABELS));
  assert.deepStrictEqual(rangeShown(env, 'calc!A42:F51')[0], ['Day'].concat(ROOM_LABELS));
  assert.deepStrictEqual(rangeShown(env, 'calc!A150:J160')[0], ['Day'].concat(DAMAGE_LABELS));
  assert.deepStrictEqual(rangeShown(env, 'calc!A175:L190')[0], ['Wave'].concat(OPEN_LABELS));
  assert.strictEqual(OPEN_LABELS.length, CAUSES.length, 'a series for every cause of the open mode');
});

test('no chart can come up empty: every live chart reads a table of fixed size that holds a header and zeros before any row arrives', () => {
  const env = readyEnv();
  const calc = calcOf(env);
  const cellsOf = (a1) => {
    const m = /^calc!([A-Z]+)(\d+):([A-Z]+)(\d+)$/.exec(a1);
    const out = [];
    for (let r = Number(m[2]); r <= Number(m[4]); r++) for (let c = colNumber(m[1]); c <= colNumber(m[3]); c++) out.push(calc.getCell(r, c));
    return out;
  };
  for (const chart of LIVE_CHARTS) {
    chart.ranges.forEach((range, n) => {
      const rows = rangeShown(env, range);
      rows[0].forEach((v) => assert.ok(typeof v === 'string' && v !== '', chart.name + ': a header cell in ' + range));
      rows.slice(1).forEach((row) => row.forEach((v, j) => {
        // The first column of a chart's first range names the rows; every other cell is a number.
        if (n === 0 && j === 0) assert.ok(typeof v === 'string' && v !== '', chart.name + ': a row name in ' + range);
        else assert.strictEqual(v, 0, chart.name + ': 0 in ' + range);
      }));
      // No cell is filled by a formula that stands in another cell: a result that spills has no rows while there is no data.
      for (const cell of cellsOf(range)) {
        assert.ok(cell.value !== '' || cell.formula, chart.name + ': no empty cell in ' + range);
        assert.ok(!/\b(QUERY|FILTER|UNIQUE|ARRAYFORMULA)\(/.test(cell.formula), chart.name + ': ' + cell.formula);
        if (/\bSORT\(/.test(cell.formula)) assert.ok(/^=INDEX\(SORT\(/.test(cell.formula), 'one value picked out of the sorted table: ' + cell.formula);
      }
    });
  }
  assert.deepStrictEqual(rangeShown(env, 'calc!A17:J27').slice(1).map((r) => r[0]), DAY_LABELS.concat(['Final boss']));
  assert.deepStrictEqual(rangeShown(env, 'calc!A42:F51').slice(1).map((r) => r[0]), DAY_LABELS);
  assert.deepStrictEqual(rangeShown(env, 'calc!A100:A117').slice(1).map((r) => r[0]), STORY_WEAPONS, 'the 17 weapons, in the list\'s order while none is taken');
  assert.deepStrictEqual(rangeShown(env, 'calc!A150:J160').slice(1).map((r) => r[0]), DAY_LABELS.concat(['Final boss']));
  assert.deepStrictEqual(rangeShown(env, 'calc!A175:L190').slice(1).map((r) => r[0]), BUCKETS);

  // The ranges do not move when rows arrive and setup() runs again.
  for (const ev of [
    storyEnd(1), v2PurchaseEvent({ run_id: idOf(1) }), v2Event({ kind: 'crate_offer', detail: 'Afternoon:Railgun', value: 1 }),
    damageEvent([{ source: 'Bloater', stage: 'day', wave: 55, damage: 3, damage_pct: 6, hits: 2 }], { mode: 'story' }), endEvent({ run_id: idOf(2) }),
  ]) assert.strictEqual(post(env, ev).json.ok, true);
  env.gs.setup();
  assert.deepStrictEqual(chartSpecs(env).map((spec) => spec.ranges), CHARTS.map((c) => c.ranges));
});

test('calc: a first row that says what the tab is, tables one under another with a one-cell title each, the growing list right of them', () => {
  const env = readyEnv();
  const calc = calcOf(env);
  assert.ok(/^calc: the tables the charts of "Dashboard" read\. setup\(\) rebuilds this tab/.test(calc.getCell(1, 1).value));
  assert.deepStrictEqual(calc.rowValues(1).slice(1).filter((v) => v !== ''), [], 'one cell in row 1');

  // Titles down column A, each with an empty row above it, its header under it and its rows under that.
  const titles = [];
  for (let r = 2; r <= calc.getLastRow(); r++) {
    const v = calc.getCell(r, 1).value;
    if (typeof v === 'string' && /^(LIVE|EXAMPLE) /.test(v)) titles.push([r, v]);
  }
  assert.deepStrictEqual(titles.map((t) => [t[0], t[1].split(':')[0]]), [
    [3, 'LIVE totals'], [16, 'LIVE 1'], [29, 'EXAMPLE 1'], [41, 'LIVE 2'], [53, 'LIVE 2'], [61, 'EXAMPLE 2'], [72, 'EXAMPLE 2'],
    [79, 'LIVE 3'], [99, 'LIVE 3'], [119, 'LIVE 3'], [125, 'LIVE 3'], [129, 'EXAMPLE 3'], [149, 'LIVE 4'], [162, 'EXAMPLE 4'], [174, 'LIVE open mode'],
  ]);
  const filledIn = (r, first, last) => rowsFrom(first, last).filter((c) => calc.getCell(r, c).value !== '' || calc.getCell(r, c).formula).length;
  titles.forEach(([r, title], i) => {
    const table = tableOf(env, title);
    assert.strictEqual(filledIn(r - 1, 1, 12), 0, title + ': an empty row above the title');
    assert.strictEqual(filledIn(r, 2, 12), 0, title + ': the title is one cell');
    assert.ok(table.header.length >= 2 && table.header.every((h) => typeof h === 'string'), title + ': a header row');
    for (let row = table.first; row <= table.last; row++) assert.strictEqual(filledIn(row, 1, table.width), table.width, title + ': row ' + row + ' is full');
    assert.strictEqual(table.last + 2, i + 1 < titles.length ? titles[i + 1][0] : calc.getLastRow() + 2, title + ': the next table starts two rows under its last row');
  });

  // The tables keep to columns A to L; M stays empty; N to Q hold the one list that grows, and nothing stands under its formula.
  const outside = [];
  calc.cells.forEach((row, i) => row.forEach((cell, j) => {
    if ((cell.value !== '' || cell.formula) && j + 1 > 12) outside.push(colLetter(j + 1) + (i + 1));
  }));
  assert.deepStrictEqual(outside.sort(), ['N3', 'N4', 'N5', 'O4', 'P4', 'Q4']);
  assert.deepStrictEqual(calc.rowValues(4).slice(13, 17), ['run_id', 'day', 'room', 'item']);
  assert.strictEqual(calc.getCell(5, 14).formula, FIRST_PURCHASES_FORMULA);
  assert.deepStrictEqual(Object.keys(calc.backgrounds), [], 'no colours on a tab nobody looks at');
  assert.deepStrictEqual(calc.merges, []);
});

test('the example tables of calc are the constants of Code.gs: plain numbers, a catch-all column of zeros where the live chart has one', () => {
  const env = readyEnv();
  const examples = JSON.parse(JSON.stringify(env.gs.STORY_EXAMPLES));
  const calc = calcOf(env);
  const valuesOf = (table) => calc.getRange(table.top, 1, table.last - table.top + 1, table.width).getValues();
  const withZeros = (table, label) => [table.header.concat([label])].concat(table.rows.map((row) => row.concat([0])));

  const endings = tableOf(env, 'EXAMPLE 1:');
  const spending = tableOf(env, 'EXAMPLE 2: the same table');
  const first = tableOf(env, 'EXAMPLE 2: simulated runs by the room');
  const weapons = tableOf(env, 'EXAMPLE 3:');
  const damage = tableOf(env, 'EXAMPLE 4:');
  assert.deepStrictEqual(valuesOf(endings), withZeros(examples.endings, 'Other'));
  assert.deepStrictEqual(valuesOf(spending), withZeros(examples.spending, 'Other rooms'));
  assert.deepStrictEqual(valuesOf(first), [examples.spending.firstHeader].concat(examples.spending.firstRows));
  assert.deepStrictEqual(valuesOf(weapons), [examples.weapons.header].concat(examples.weapons.rows));
  assert.deepStrictEqual(valuesOf(damage), [examples.damage.header].concat(examples.damage.rows));
  for (const table of [endings, spending, first, weapons, damage]) {
    for (let r = table.top; r <= table.last; r++) {
      for (let c = 1; c <= table.width; c++) assert.strictEqual(calc.getCell(r, c).formula, '', table.title + ': no formula in an example');
    }
  }
  // The example charts read exactly these tables.
  assert.deepStrictEqual(CHARTS.filter((c) => /example/.test(c.name)).map((c) => c.ranges), [
    ['calc!A' + endings.top + ':J' + endings.last], ['calc!A' + spending.top + ':F' + spending.last],
    ['calc!A' + weapons.top + ':A' + weapons.last, 'calc!D' + weapons.top + ':D' + weapons.last], ['calc!A' + damage.top + ':J' + damage.last],
  ]);
  // The series of the live charts carry the names of the example's columns.
  assert.deepStrictEqual(examples.endings.header.slice(1).concat(['Other']), END_LABELS);
  assert.deepStrictEqual(examples.spending.header.slice(1).concat(['Other rooms']), ROOM_LABELS);
  assert.deepStrictEqual(examples.damage.header.slice(1), DAMAGE_LABELS);
  assert.deepStrictEqual(examples.weapons.rows.map((row) => row[0]).sort(), STORY_WEAPONS.slice().sort(), 'the same 17 weapons');

  // The numbers hang together as the aggregation gives them; the zeros of the catch-all columns are true of them.
  const sum = (row) => row.slice(1).reduce((a, b) => a + b, 0);
  assert.strictEqual(examples.endings.rows.reduce((a, row) => a + sum(row), 0), examples.endings.runs.ended, 'every ended run is in the endings table once');
  assert.strictEqual(examples.endings.runs.started - examples.endings.runs.ended, examples.endings.runs.unfinished);
  assert.strictEqual(examples.spending.firstRows.reduce((a, row) => a + row[1], 0), examples.spending.runs, 'one first purchase for each run that bought');
  const rates = examples.weapons.rows.map((row) => row[3]);
  assert.deepStrictEqual(rates.slice().sort((a, b) => b - a), rates, 'the most taken weapon first');
  for (const [weapon, offered, taken, rate] of examples.weapons.rows) {
    assert.ok(taken <= offered && Math.abs(rate - taken / offered) < 0.0006, weapon + ': the take rate is taken over offered');
  }
  for (const row of examples.damage.rows) assert.ok(Math.abs(sum(row) - 100) < 0.5, row[0] + ': the shares of a day add up to 100');

  // An example never reaches a data tab.
  for (const name of DATA_SHEETS) assert.strictEqual(sheetOf(env, name).getLastRow(), 1, name + ': the header and no row');
});

test('a second setup() gives the same two tabs; what was typed, merged, charted, resized, moved or shown is put back', () => {
  const env = readyEnv();
  const OWN = ['Dashboard', 'calc'];
  const before = OWN.map((name) => snapshot(sheetOf(env, name)));
  env.gs.setup();
  assert.deepStrictEqual(OWN.map((name) => snapshot(sheetOf(env, name))), before, 'the same cells, formats, sizes and charts after a second setup');
  assert.strictEqual(chartSpecs(env).filter((spec) => /^Example: /.test(spec.title)).length, 4, 'still four example charts');

  const dash = dashboardOf(env);
  dash.getRange(30, 3).setValue('a note of mine');
  dash.getRange(96, 2, 1, 3).merge();
  dash.insertChart(dash.newChart().asColumnChart().addRange(dash.getRange(1, 2)).build());
  dash.setColumnWidth(3, 250);
  dash.setRowHeight(8, 60);
  dash.setHiddenGridlines(false);
  const calc = calcOf(env);
  calc.getRange(20, 3).setValue(999);
  calc.getRange(200, 1).setValue('a table of mine');
  calc.showSheet();
  env.ss.setActiveSheet(dash);
  env.ss.moveActiveSheet(env.ss.getSheets().length);
  assert.notDeepStrictEqual(env.ss.getSheets().map((s) => s.name), SHEETS_AFTER_SETUP);

  env.gs.setup();
  assert.deepStrictEqual(OWN.map((name) => snapshot(sheetOf(env, name))), before, 'both tabs as setup() builds them');
  assert.deepStrictEqual(env.ss.getSheets().map((s) => s.name), SHEETS_AFTER_SETUP, 'the Dashboard is first again');
  assert.strictEqual(calcOf(env).hidden, true, 'calc is hidden again');
  for (const name of DATA_SHEETS) assert.strictEqual(sheetOf(env, name).getLastRow(), 1, name + ': still no row');
});

test('card 1 counts story run_end rows by day and cause group; the final battle (stages final_boss and ending) is a row of its own', () => {
  const env = readyEnv();
  const start = (n, overrides) => startEvent(Object.assign({ run_id: idOf(n), mode: 'story', stage: 'tutorial', wave: 0 }, overrides));
  const events = [
    start(1), storyEnd(1), // Day 3, enemy, by day
    start(2), storyEnd(2, { stage: 'night', cause: 'base_destroyed' }), // Day 3, the base fell at night
    start(3), storyEnd(3, { outcome: 'victory', cause: 'victory', stage: 'ending', wave: 7 }), // won: the ending of the final battle
    start(4), storyEnd(4, { stage: 'final_boss', cause: 'tyrant', enemy: 'Tyrant', wave: 7 }), // lost to the final boss
    start(5), storyEnd(5, { outcome: 'abandoned', cause: 'quit_to_title', stage: 'dawn', wave: 9 }), // left in overtime
    start(6), storyEnd(6, { stage: 'tutorial', wave: 0 }), // Day 0
    start(7, { edition: 'test' }), storyEnd(7, { edition: 'test' }), // a test run
    startEvent({ run_id: idOf(8) }), endEvent({ run_id: idOf(8), wave: 3 }), // an open-mode run that ended in wave 3
    start(9), // a story run left by closing the tab
    startEvent({ run_id: idOf(10) }), storyEnd(10, { stage: 'dusk', cause: 'status', wave: 2 }), // marked as a story run late
    start(11), storyEnd(11, { outcome: 'abandoned', cause: 'restart', stage: 'night' }), // the other way of leaving, Day 3
    start(12), storyEnd(12, { cause: 'black_hole', wave: 4 }), // three causes no group lists
    start(13), storyEnd(13, { cause: 'void_trial', stage: 'void_trial', wave: 4 }),
    start(14), storyEnd(14, { cause: 'lava', wave: 1 }), // stored as "unknown"
    start(15), storyEnd(15, { cause: 'black_hole', stage: 'final_boss', wave: 7 }), // a black hole in the final fight
    start(16), storyEnd(16, { outcome: 'abandoned', cause: 'quit_to_title', stage: 'ending', wave: 7 }), // left during the ending
  ];
  for (const ev of events) assert.strictEqual(post(env, ev).json.ok, true);

  const table = tableOf(env, 'LIVE 1:');
  assert.deepStrictEqual(table.header, ['Day'].concat(END_LABELS));
  assert.deepStrictEqual(gridOf(env, table), {
    'Day 0/Enemy': 1, 'Day 1/Other': 1, 'Day 2/Burn / poison': 1, 'Day 3/Enemy': 1, 'Day 3/Base destroyed': 1, 'Day 3/Left the run': 1,
    'Day 4/Other': 2, 'Day 8+/Left the run': 1,
    'Final boss/Tyrant King': 1, 'Final boss/Victory': 1, 'Final boss/Left the run': 1, 'Final boss/Other': 1,
  }, 'every story run that ended is in the table once: the final battle is on no day (Day 7 stays empty), test and open-mode rows are left out');

  // The formulas themselves, as the sheet gets them.
  const calc = calcOf(env);
  const story = 'COUNTIFS(runs!$B:$B,"run_end",runs!$E:$E,"<>test",runs!$Q:$Q,"story",';
  const byDay = 'runs!$K:$K,"<>final_boss",runs!$K:$K,"<>ending",runs!$L:$L,';
  assert.strictEqual(calc.getCell(21, 2).formula, '=' + story + 'runs!$H:$H,"enemy",' + byDay + '3)', 'Day 3 / Enemy');
  assert.strictEqual(calc.getCell(26, 9).formula, '=' + story + 'runs!$H:$H,"restart",' + byDay + '">7")+' +
    story + 'runs!$H:$H,"quit_to_title",' + byDay + '">7")', 'overtime / Left the run: both ways of leaving');
  assert.strictEqual(calc.getCell(27, 3).formula, '=' + story + 'runs!$H:$H,"tyrant",runs!$K:$K,"final_boss")+' +
    story + 'runs!$H:$H,"tyrant",runs!$K:$K,"ending")', 'Final boss / Tyrant King: both stages of the final battle');
  const others = ['enemy', 'tyrant', 'backfire', 'wall_slam', 'status', 'base_destroyed', 'victory', 'restart', 'quit_to_title']
    .map((cause) => 'runs!$H:$H,"<>' + cause + '",').join('');
  assert.strictEqual(calc.getCell(18, 10).formula, '=' + story + others + byDay + '0)', 'Day 0 / Other: every cause no group lists');

  // The totals and the sentences that show them.
  assert.strictEqual(calc.getCell(5, 2).formula, STARTED_FORMULA);
  assert.strictEqual(calc.getCell(6, 2).formula, '=SUM(B18:J27)');
  assert.strictEqual(says(env, 'B7'), 'LIVE DATA' + DOT + 'story runs ended: 13');
  assert.strictEqual(says(env, 'B3'), 'Story runs started 14' + DOT + 'ended 13' + DOT + 'purchases 0' + DOT + 'crates opened 0',
    'started: runs 1 to 6 and 9 to 16, also the one whose start went out as open; the test run is not counted');
  assert.strictEqual(says(env, 'B80'), 'Runs ended on each wave, by cause. Total: 1.', 'the open-mode run is on the open-mode chart only');
});

test('card 2 adds up each day\'s spending by room and counts the room of each run\'s first purchase', () => {
  const env = readyEnv();
  const buy = (n, overrides) => v2PurchaseEvent(Object.assign({ run_id: idOf(n), day: 1, detail: '', alloy: 0, energy: 0, dark: 0 }, overrides));
  const events = [
    buy(1, { room: 'Armament', item: 'new_weapon', alloy: 100, energy: 20 }), // run 1 buys a weapon first
    buy(2, { room: 'Control', item: 'room_upgrade', alloy: 60, energy: 10 }), // run 2 upgrades a room first
    buy(1, { room: 'Control', item: 'repair', alloy: 30 }),
    buy(1, { day: 2, room: 'Ship', item: 'ship_part', alloy: 50, dark: 5 }),
    buy(2, { day: 2, room: 'Hangar', item: 'crane', alloy: 11 }), // a room this script does not list yet
    buy(2, { day: 9, room: 'Energy', item: 'lamp', energy: 7 }), // overtime
    buy(3, { edition: 'test', room: 'Armament', item: 'new_weapon', alloy: 999 }),
    buy(4, { day: 0, room: 'Hangar', item: 'crane', alloy: 5 }), // run 4's first purchase is in that room
    buy(4, { day: 0, room: 'Ship', item: 'fuel', energy: 2 }),
  ];
  for (const ev of events) assert.strictEqual(post(env, ev).json.ok, true);

  const table = tableOf(env, 'LIVE 2: resources spent');
  assert.deepStrictEqual(table.header, ['Day'].concat(ROOM_LABELS));
  assert.deepStrictEqual(gridOf(env, table), {
    'Day 0/Ship': 2, 'Day 0/Other rooms': 5, 'Day 1/Armament Bay': 120, 'Day 1/Control Center': 100, 'Day 2/Ship': 55, 'Day 2/Other rooms': 11,
    'Day 8+/Energy Lab': 7,
  }, 'alloy, energy and dark matter added up; the test run and the header row left out');
  const calc = calcOf(env);
  const bought = 'v2_purchases!$B:$B,"v2_purchase",v2_purchases!$E:$E,"<>test",';
  assert.strictEqual(calc.getCell(45, 2).formula, '=' + ['L', 'M', 'N'].map((c) => (
    'SUMIFS(v2_purchases!$' + c + ':$' + c + ',' + bought + 'v2_purchases!$K:$K,"Armament",v2_purchases!$G:$G,2)')).join('+'), 'Day 2 / Armament Bay: the three currencies');
  assert.ok(calc.getCell(51, 6).formula.endsWith(bought + 'v2_purchases!$K:$K,"<>Armament",v2_purchases!$K:$K,"<>Energy",' +
    'v2_purchases!$K:$K,"<>Ship",v2_purchases!$K:$K,"<>Control",v2_purchases!$G:$G,">7")'), 'overtime / other rooms: ' + calc.getCell(51, 6).formula);

  // The first purchase of each run: its first row, not its largest or its last; test rows left out.
  const firsts = tableOf(env, 'LIVE 2: runs by the room');
  assert.deepStrictEqual(rowsFrom(firsts.first, firsts.last).map((r) => calc.getCell(r, 1).value), ROOM_LABELS);
  assert.strictEqual(calc.getCell(55, 2).formula, '=COUNTIFS($N$5:$N,"?*",$P$5:$P,"Armament")');
  assert.strictEqual(calc.getCell(59, 2).formula, '=COUNTIFS($N$5:$N,"?*",$P$5:$P,"<>Armament",$P$5:$P,"<>Energy",$P$5:$P,"<>Ship",$P$5:$P,"<>Control")');
  assert.deepStrictEqual(rowsFrom(55, 59).map((r) => shown(env, 'calc', r, 2)), [0, 0, 0, 0, 0], 'no run counted while the list is empty');
  assert.deepStrictEqual(spillFirstPurchases(env), [[idOf(1), 1, 'Armament', 'new_weapon'], [idOf(2), 1, 'Control', 'room_upgrade'],
    [idOf(4), 0, 'Hangar', 'crane']]);
  assert.deepStrictEqual(rowsFrom(55, 59).map((r) => shown(env, 'calc', r, 2)), [1, 0, 0, 1, 1]);
  assert.strictEqual(says(env, 'I6'), 'First purchase of a run: Armament 1' + DOT + 'Energy 0' + DOT + 'Ship 0' + DOT + 'Control 1' + DOT +
    'Other 1. Chart: each day\'s spending by room.');
  assert.strictEqual(says(env, 'I7'), 'LIVE DATA' + DOT + 'purchases: 8');
  assert.strictEqual(says(env, 'B3'), 'Story runs started 0' + DOT + 'ended 0' + DOT + 'purchases 8' + DOT + 'crates opened 0');
});

test('card 3 counts offers and takes per weapon from the first recorded offer on, and ranks the weapons by take rate', () => {
  const offer = (n, detail, value, overrides) => v2Event(Object.assign({ run_id: idOf(n), kind: 'crate_offer', detail: detail, value: value }, overrides));
  const settle = (n, detail, value, overrides) => v2Event(Object.assign({ run_id: idOf(n), kind: 'crate', detail: detail, value: value }, overrides));
  // A crate opened before any offer was recorded: the Railgun taken from a panel nobody knows.
  const old = (env) => {
    assert.strictEqual(post(env, settle(9, 'Afternoon:Railgun', 1)).json.ok, true);
    const events = sheetOf(env, 'v2_events');
    events.setCell(events.getLastRow(), 1, new Date(Date.now() - 86400000), '');
  };
  const counts = (env) => {
    const table = tableOf(env, 'LIVE 3: each weapon');
    const out = {};
    rowsFrom(table.first, table.last).forEach((r) => {
      const pair = [shown(env, 'calc', r, 2), shown(env, 'calc', r, 3)];
      if (pair[0] || pair[1]) out[shown(env, 'calc', r, 1)] = pair;
    });
    return out;
  };

  const early = readyEnv();
  old(early);
  assert.strictEqual(shownAt(early, 'calc', 'B9'), 'no offers recorded yet');
  assert.deepStrictEqual(counts(early), {}, 'without offers no take is counted');
  assert.strictEqual(says(early, 'B40'), 'LIVE DATA' + DOT + 'weapons offered: 0' + DOT + 'taken: 0');
  assert.strictEqual(says(early, 'B3'), 'Story runs started 0' + DOT + 'ended 0' + DOT + 'purchases 0' + DOT + 'crates opened 1');

  const env = readyEnv();
  old(env);
  const events = [
    offer(8, 'Afternoon:Railgun', 1, { edition: 'test' }),
    offer(1, 'TutorialFirst:Railgun', 1), offer(1, 'TutorialFirst:Minigun', 2), offer(1, 'TutorialFirst:ArcCoil', 3),
    settle(1, 'TutorialFirst:Minigun', 2),
    offer(1, 'Afternoon:Railgun', 1), offer(1, 'Afternoon:Knife', 2),
    settle(1, 'Afternoon:left', 0),
    offer(2, 'Afternoon:Railgun', 1),
    settle(2, 'Afternoon:Railgun', 1),
    settle(2, 'Afternoon:empty', 0),
    offer(2, 'Afternoon:Minigun', 1), offer(2, 'Afternoon:Railgun', 2), offer(2, 'Afternoon:Laser', 3), // a weapon the script does not list
    settle(2, 'Afternoon:no_room', 2),
    settle(8, 'Afternoon:Knife', 1, { edition: 'test' }),
  ];
  for (const ev of events) assert.strictEqual(post(env, ev).json.ok, true);

  const calc = calcOf(env);
  assert.strictEqual(typeof shownAt(env, 'calc', 'B9'), 'number', 'the time of the first recorded offer');
  assert.deepStrictEqual(counts(env), { Railgun: [4, 1], Minigun: [2, 1], ArcCoil: [1, 0], Knife: [1, 0] },
    'offered and taken; the Railgun of the crate without offers and the test rows are not counted');
  const offered = 'v2_events!$I:$I,"crate_offer",v2_events!$E:$E,"<>test"';
  const settled = 'v2_events!$I:$I,"crate",v2_events!$E:$E,"<>test",';
  const railgun = 81 + STORY_WEAPONS.indexOf('Railgun');
  assert.strictEqual(calc.getCell(railgun, 1).value, 'Railgun');
  assert.strictEqual(calc.getCell(railgun, 2).formula, '=COUNTIFS(' + offered + ',v2_events!$J:$J,"*:Railgun")');
  assert.strictEqual(calc.getCell(railgun, 3).formula, '=IF(ISNUMBER($B$9),COUNTIFS(' + settled + 'v2_events!$J:$J,"*:Railgun",v2_events!$A:$A,">="&$B$9),0)');
  assert.strictEqual(calc.getCell(railgun, 4).formula, '=IFERROR(C' + railgun + '/B' + railgun + ',0)');
  assert.strictEqual(calc.getCell(9, 2).formula, '=IF(COUNTIFS(' + offered + ')=0,"no offers recorded yet",MINIFS(v2_events!$A:$A,' + offered + '))');
  assert.deepStrictEqual([10, 11, 12].map((r) => calc.getCell(r, 2).formula), ['=SUM(B81:B97)', '=SUM(C81:C97)', '=COUNTIFS(' + offered + ')-$B$10']);
  assert.deepStrictEqual([10, 11, 12].map((r) => shown(env, 'calc', r, 2)), [8, 2, 1], 'eight offers of listed weapons, two taken, one offer of a weapon the list lacks');

  // The chart's table: the most taken weapon first; weapons with the same rate keep the list's order.
  assert.strictEqual(calc.getCell(101, 1).formula, '=INDEX(SORT($A$81:$D$97,4,FALSE),1,1)');
  assert.strictEqual(calc.getCell(117, 2).formula, '=INDEX(SORT($A$81:$D$97,4,FALSE),17,4)');
  const ranked = rangeShown(env, 'calc!A101:B117');
  assert.deepStrictEqual(ranked.slice(0, 3), [['Minigun', 0.5], ['Railgun', 0.25], ['Rifle', 0]]);
  assert.deepStrictEqual(ranked.map((row) => row[0]).sort(), STORY_WEAPONS.slice().sort(), 'every weapon once');
  assert.deepStrictEqual(ranked.slice(2).map((row) => row[0]), STORY_WEAPONS.filter((w) => w !== 'Minigun' && w !== 'Railgun'));

  // By place on the panel, and the crates left: the pick that did not fit is not a take; a crate with nothing new inside and
  // Day 0's first crate could not be left.
  assert.deepStrictEqual([121, 122, 123].map((r) => [shown(env, 'calc', r, 2), shown(env, 'calc', r, 3)]), [[4, 1], [3, 1], [2, 0]]);
  assert.strictEqual(calc.getCell(122, 3).formula, '=IF(ISNUMBER($B$9),COUNTIFS(' + settled + 'v2_events!$K:$K,2,v2_events!$J:$J,"<>*:no_room",v2_events!$A:$A,">="&$B$9),0)');
  assert.deepStrictEqual([2, 3].map((c) => shown(env, 'calc', 127, c)), [3, 1], 'three crates could be left (left, taken, no room), one was');
  assert.strictEqual(calc.getCell(127, 2).formula, '=IF(ISNUMBER($B$9),COUNTIFS(' + settled + 'v2_events!$J:$J,"<>TutorialFirst:*",v2_events!$J:$J,"<>*:empty",v2_events!$A:$A,">="&$B$9),0)');
  assert.strictEqual(says(env, 'B39'), 'Taken by place: 1st 25%' + DOT + '2nd 33%' + DOT + '3rd 0%' + DOT +
    'crates left 33%. Chart: how often each weapon is taken when a crate offers it.');
  assert.strictEqual(says(env, 'B40'), 'LIVE DATA' + DOT + 'weapons offered: 8' + DOT + 'taken: 2');
  assert.strictEqual(says(env, 'B3'), 'Story runs started 0' + DOT + 'ended 0' + DOT + 'purchases 0' + DOT + 'crates opened 6',
    'every crate row but the test one, also the crate from before the offers were recorded');
});

test('card 4 adds up the damage to the player by day and source group from story rows; the final fight is a row of its own', () => {
  const env = readyEnv();
  const events = [
    damageEvent([
      { source: 'shadow', stage: 'night', wave: 2, damage: 3, damage_pct: 6, hits: 2 },
      { source: 'Spitter', dot: true, stage: 'day', wave: 2, damage: 25, damage_pct: 50, hits: 3 },
      { source: 'Spitter', stage: 'day', wave: 3, damage: 5, damage_pct: 10, hits: 1 },
      { source: 'Grunt_elite', stage: 'day', wave: 2, damage: 4, damage_pct: 8, hits: 1 }, // an elite counts with its kind
      { source: 'Splitter', stage: 'day', wave: 2, damage: 2, damage_pct: 4, hits: 1 }, // not a Spitter: a kind no group lists
      { source: '', stage: 'dusk', wave: 3, damage: 1, damage_pct: 2, hits: 1 }, // stored as "other"
      { source: 'Tyrant', stage: 'final_boss', wave: 7, damage: 15, damage_pct: 30, hits: 2 },
      { source: 'backfire', stage: 'final_boss', wave: 7, damage: 2, damage_pct: 5, hits: 1 },
      { source: 'wall_slam', stage: 'day', wave: 9, damage: 2, damage_pct: 3, hits: 1 }, // overtime
      { source: 'backfire', stage: 'tutorial', wave: 0, damage: 1, damage_pct: 1, hits: 1 },
    ], { mode: 'story' }),
    damageEvent([{ source: 'Spitter', dot: true, wave: 2, damage: 5, damage_pct: 10, hits: 3 }]), // open mode
    damageEvent([{ source: 'Spitter', dot: true, stage: 'day', wave: 2, damage: 45, damage_pct: 90, hits: 3 }], { mode: 'story', edition: 'test' }),
  ];
  for (const ev of events) assert.strictEqual(post(env, ev).json.ok, true);

  const table = tableOf(env, 'LIVE 4:');
  assert.deepStrictEqual(table.header, ['Day'].concat(DAMAGE_LABELS));
  const grid = gridOf(env, table);
  assert.deepStrictEqual(grid, {
    'Day 0/Own weapon': 1, 'Day 2/Grunt': 8, 'Day 2/Spitter': 50, 'Day 2/Shadow (night)': 6, 'Day 2/Other enemies': 4,
    'Day 3/Spitter': 10, 'Day 3/Other enemies': 2, 'Day 8+/Wall slam': 3, 'Final boss/Tyrant King': 30, 'Final boss/Own weapon': 5,
  }, 'summed as % of max HP; the open-mode row and the test row are left out, the final fight is on no day');
  assert.strictEqual(Object.keys(grid).reduce((sum, key) => sum + grid[key], 0), 119, 'every story row is in the table once');

  const calc = calcOf(env);
  const story = 'damage!$B:$B,"wave_damage",damage!$E:$E,"<>test",damage!$O:$O,"story",';
  assert.strictEqual(calc.getCell(153, 2).formula, '=SUMIFS(damage!$M:$M,' + story + 'damage!$J:$J,"Grunt",damage!$G:$G,"<>final_boss",damage!$H:$H,2)+' +
    'SUMIFS(damage!$M:$M,' + story + 'damage!$J:$J,"Grunt_elite",damage!$G:$G,"<>final_boss",damage!$H:$H,2)', 'Day 2 / Grunt: the kind and its elite');
  assert.strictEqual(calc.getCell(160, 3).formula, '=SUMIFS(damage!$M:$M,' + story + 'damage!$J:$J,"Tyrant",damage!$G:$G,"final_boss")+' +
    'SUMIFS(damage!$M:$M,' + story + 'damage!$J:$J,"Tyrant_elite",damage!$G:$G,"final_boss")', 'Final boss / Tyrant King');
  const others = ['Grunt', 'Grunt_elite', 'Tyrant', 'Tyrant_elite', 'backfire', 'wall_slam', 'Brute', 'Brute_elite', 'Spitter', 'Spitter_elite',
    'shadow', 'Skitter', 'Skitter_elite'].map((source) => 'damage!$J:$J,"<>' + source + '",').join('');
  assert.strictEqual(calc.getCell(159, 10).formula, '=SUMIFS(damage!$M:$M,' + story + others + 'damage!$G:$G,"<>final_boss",damage!$H:$H,">7")',
    'overtime / Other enemies: every source no group lists');
  assert.strictEqual(calc.getCell(13, 2).formula, '=SUMIFS(damage!$N:$N,' + story.slice(0, -1) + ')');
  assert.strictEqual(says(env, 'I40'), 'LIVE DATA' + DOT + 'hits on the player: 14');
});

test('the open-mode table counts run_end rows by wave and cause, the final boss apart, without test rows', () => {
  const env = readyEnv();
  const events = [
    startEvent({ run_id: idOf(1) }), endEvent({ run_id: idOf(1), wave: 9 }),
    startEvent({ run_id: idOf(2), edition: 'zh' }), endEvent({ run_id: idOf(2), edition: 'zh', wave: 9 }),
    startEvent({ run_id: idOf(3) }), endEvent({ run_id: idOf(3), cause: 'backfire', wave: 3 }),
    startEvent({ run_id: idOf(4) }), endEvent({ run_id: idOf(4), outcome: 'victory', cause: 'victory', stage: 'final_boss', wave: 14 }),
    startEvent({ run_id: idOf(5) }), endEvent({ run_id: idOf(5), cause: 'tyrant', enemy: 'Tyrant', stage: 'final_boss', wave: 14 }),
    startEvent({ run_id: idOf(6) }), endEvent({ run_id: idOf(6), cause: 'enemy', stage: 'wave', wave: 14 }),
    startEvent({ run_id: idOf(7) }), endEvent({ run_id: idOf(7), outcome: 'abandoned', cause: 'restart', stage: 'build', wave: 5 }),
    startEvent({ run_id: idOf(8), edition: 'test' }), endEvent({ run_id: idOf(8), edition: 'test', wave: 9 }),
    startEvent({ run_id: idOf(9) }), // closed the tab
    endEvent({ run_id: idOf(11), cause: 'status', wave: 6 }), // its run_start was lost
    startEvent({ run_id: idOf(12) }), endEvent({ run_id: idOf(12), outcome: 'victory', cause: 'victory', stage: 'wave', wave: 9 }),
  ];
  for (const ev of events) assert.strictEqual(post(env, ev).json.ok, true);

  const table = tableOf(env, 'LIVE open mode:');
  assert.deepStrictEqual(table.header, ['Wave'].concat(OPEN_LABELS));
  assert.deepStrictEqual(rowsFrom(table.first, table.last).map((r) => calcOf(env).getCell(r, 1).value), BUCKETS);
  assert.deepStrictEqual(gridOf(env, table), {
    'W3/Own weapon': 1, 'W5/Restart': 1, 'W6/Burn / poison': 1, 'W9/Enemy': 2, 'W14/Enemy': 1, 'Boss/Tyrant King': 1, 'Boss/Victory': 2,
  }, 'a wave 14 death is not a final boss death; every victory is in the Boss row');
  const calc = calcOf(env);
  const open = '=COUNTIFS(runs!$B:$B,"run_end",runs!$E:$E,"<>test",runs!$Q:$Q,"<>story",';
  assert.strictEqual(calc.getCell(178, 2).formula, open + 'runs!$H:$H,"enemy",runs!$K:$K,"<>final_boss",runs!$L:$L,3)', 'W3 / Enemy');
  assert.strictEqual(calc.getCell(190, 10).formula, open + 'runs!$H:$H,"victory",runs!$K:$K,"final_boss")', 'Boss / Victory');
  CAUSES.forEach((cause, j) => assert.ok(calc.getCell(176, j + 2).formula.indexOf('runs!$H:$H,"' + cause + '"') > 0, OPEN_LABELS[j] + ' counts ' + cause));
  assert.strictEqual(calc.getCell(14, 2).formula, '=SUM(B176:L190)');
  assert.strictEqual(says(env, 'B80'), 'Runs ended on each wave, by cause. Total: 9.');
  assert.strictEqual(says(env, 'B7'), 'LIVE DATA' + DOT + 'story runs ended: 0', 'no open-mode run on a story card');
});

test('the open-mode table leaves story rows out; rows written before the mode column existed count as open', () => {
  const env = readyEnv();
  const events = [
    startEvent({ run_id: idOf(1) }), endEvent({ run_id: idOf(1), wave: 3 }), // open: ends in wave 3
    startEvent({ run_id: idOf(2), mode: 'story', stage: 'tutorial', wave: 0 }),
    endEvent({ run_id: idOf(2), mode: 'story', stage: 'day', wave: 3 }), // story: day 3 is not wave 3
    startEvent({ run_id: idOf(3) }), endEvent({ run_id: idOf(3), wave: 3, mode: 'story' }), // marked as a campaign run late
  ];
  for (const ev of events) assert.strictEqual(post(env, ev).json.ok, true);
  // A row written before the mode column existed: 16 cells, no mode.
  const runs = runsSheet(env);
  runs.appendRow([new Date(), 'run_end', idOf(6), SESSION_ID, 'en', 'old', 'defeat', 'enemy', 'Grunt', false, 'wave', 3, 100, 2, 10, 'Rifle']);

  assert.deepStrictEqual(gridOf(env, tableOf(env, 'LIVE open mode:')), { 'W3/Enemy': 2 }, 'runs 1 and 6 only');
  assert.deepStrictEqual(gridOf(env, tableOf(env, 'LIVE 1:')), { 'Day 3/Enemy': 2 }, 'the two story endings are on card 1');
  assert.strictEqual(says(env, 'B80'), 'Runs ended on each wave, by cause. Total: 2.');
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
