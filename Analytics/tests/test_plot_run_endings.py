"""Tests for plot_run_endings.py and send_test_event.py.

Run from the repository root:
    python -m unittest discover -s Analytics/tests -v
"""

import json
import os
import re
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))

import plot_run_endings as pre  # noqa: E402
import send_test_event as ste  # noqa: E402

FIXTURES = os.path.join(HERE, "fixtures")
HEX32 = re.compile(r"^[0-9a-f]{32}$")


def fixture(name):
    return pre.load_rows(os.path.join(FIXTURES, name))


def nonzero(agg):
    """{bucket: {cause: n}} with zero counts dropped, for compact asserts."""
    out = {}
    for bucket, causes in agg["counts"].items():
        kept = {c: n for c, n in causes.items() if n}
        if kept:
            out[bucket] = kept
    return out


class TestFiltering(unittest.TestCase):
    def setUp(self):
        self.rows = fixture("basic.csv")

    def test_test_rows_excluded_by_default(self):
        agg = pre.aggregate(self.rows)
        self.assertEqual(agg["started"], 9)
        self.assertEqual(agg["ended"], 8)
        self.assertEqual(agg["counts"]["1"]["enemy"], 0)
        self.assertEqual(agg["enemies"]["Grunt"]["total"], 2)

    def test_test_rows_included_with_flag(self):
        agg = pre.aggregate(self.rows, include_test=True)
        self.assertEqual(agg["started"], 11)
        self.assertEqual(agg["ended"], 9)
        self.assertEqual(agg["unfinished"], 3)
        self.assertEqual(agg["counts"]["1"]["enemy"], 1)
        self.assertEqual(agg["enemies"]["Grunt"]["total"], 3)

    def test_edition_filter(self):
        zh = pre.aggregate(self.rows, edition="zh")
        self.assertEqual((zh["started"], zh["ended"], zh["unfinished"]), (1, 1, 0))
        self.assertEqual(nonzero(zh), {"3": {"enemy": 1}})
        en = pre.aggregate(self.rows, edition="en")
        self.assertEqual((en["started"], en["ended"], en["unfinished"]), (8, 7, 2))


class TestBuckets(unittest.TestCase):
    def setUp(self):
        self.agg = pre.aggregate(fixture("basic.csv"))

    def test_counts_per_bucket_and_cause(self):
        self.assertEqual(nonzero(self.agg), {
            "2": {"restart": 1},
            "3": {"enemy": 2},
            "5": {"enemy": 1},
            "14": {"backfire": 1},
            "Boss": {"tyrant": 1, "victory": 2},
        })

    def test_final_boss_stage_lands_in_boss(self):
        self.assertEqual(self.agg["counts"]["Boss"]["tyrant"], 1)
        self.assertEqual(self.agg["counts"]["14"]["tyrant"], 0)

    def test_victory_always_lands_in_boss(self):
        # One victory row has stage "wave" and wave 9; it still counts under Boss.
        self.assertEqual(self.agg["counts"]["Boss"]["victory"], 2)
        self.assertEqual(sum(self.agg["counts"][b]["victory"] for b in pre.BUCKETS if b != "Boss"), 0)

    def test_bucket_of_clamps_wave(self):
        row = pre.normalize_row({"event": "run_end", "stage": "wave", "wave": "99"})
        self.assertEqual(pre.bucket_of(row), "14")
        row = pre.normalize_row({"event": "run_end", "stage": "build", "wave": "0"})
        self.assertEqual(pre.bucket_of(row), "1")

    def test_unknown_cause_becomes_unknown(self):
        row = pre.normalize_row({"event": "run_end", "outcome": "defeat", "cause": "meteor"})
        self.assertEqual(pre.cause_of(row), "unknown")

    def test_totals(self):
        self.assertEqual(sum(self.agg["bucket_totals"].values()), self.agg["ended"])
        self.assertEqual(sum(self.agg["cause_totals"].values()), self.agg["ended"])


class TestUnfinished(unittest.TestCase):
    def test_unfinished_by_distinct_run_id(self):
        # Starts: 001-008 and 010 (002 and 008 sent twice). Ends: 001-006, 009 (no start), 010.
        # Unfinished: 007 and 008 only.
        agg = pre.aggregate(fixture("basic.csv"))
        self.assertEqual(agg["unfinished"], 2)

    def test_repeated_run_end_counts_once(self):
        start = {"event": "run_start", "run_id": "c" * 32, "edition": "en"}
        end = {"event": "run_end", "run_id": "c" * 32, "edition": "en", "outcome": "defeat",
               "cause": "enemy", "enemy": "Grunt", "wave": "2"}
        rows = [pre.normalize_row(r) for r in (start, end, end)]
        agg = pre.aggregate(rows)
        self.assertEqual((agg["started"], agg["ended"], agg["unfinished"]), (1, 1, 0))

    def test_never_negative(self):
        end = {"event": "run_end", "run_id": "d" * 32, "edition": "en", "outcome": "defeat", "cause": "status"}
        agg = pre.aggregate([pre.normalize_row(end)])
        self.assertEqual((agg["started"], agg["ended"], agg["unfinished"]), (0, 1, 0))


class TestEnemyBreakdown(unittest.TestCase):
    def test_counts_and_order(self):
        agg = pre.aggregate(fixture("basic.csv"))
        self.assertEqual(agg["enemies"], {
            "Grunt": {"enemy": 2, "tyrant": 0, "elite": 0, "total": 2},
            "Spitter": {"enemy": 1, "tyrant": 0, "elite": 1, "total": 1},
            "Tyrant": {"enemy": 0, "tyrant": 1, "elite": 0, "total": 1},
        })
        self.assertEqual([k for k, _ in pre.enemies_sorted(agg)], ["Grunt", "Spitter", "Tyrant"])

    def test_many_kinds_fold_into_other(self):
        agg = pre.aggregate(fixture("realistic.csv"))
        bars = pre.enemy_bars(agg, limit=10)
        self.assertEqual(len(bars), 10)
        name, other = bars[-1]
        self.assertTrue(name.startswith("Other ("))
        shown = sum(entry["total"] for _, entry in bars)
        self.assertEqual(shown, sum(entry["total"] for entry in agg["enemies"].values()))
        self.assertEqual(pre.enemy_bars(pre.aggregate(fixture("basic.csv"))),
                         pre.enemies_sorted(pre.aggregate(fixture("basic.csv"))))

    def test_other_causes_not_counted(self):
        agg = pre.aggregate(fixture("basic.csv"))
        total = sum(entry["total"] for entry in agg["enemies"].values())
        self.assertEqual(total, agg["cause_totals"]["enemy"] + agg["cause_totals"]["tyrant"])


class TestParsing(unittest.TestCase):
    def test_bom_booleans_and_shuffled_columns(self):
        rows = fixture("messy.csv")
        self.assertEqual(len(rows), 7)  # blank and all-empty lines skipped
        ends = [r for r in rows if r["event"] == "run_end"]
        self.assertEqual([r["elite"] for r in ends], [True, False, False, True])
        self.assertEqual([r["wave"] for r in ends], [7, 4, 4, 14])
        self.assertEqual(ends[1]["kills"], 61)
        self.assertEqual(ends[0]["level"], 4)
        self.assertAlmostEqual(ends[0]["run_seconds"], 95.4)
        self.assertTrue(all(HEX32.match(r["run_id"]) for r in rows))

    def test_messy_aggregate(self):
        agg = pre.aggregate(fixture("messy.csv"))
        self.assertEqual(nonzero(agg), {
            "4": {"enemy": 1, "status": 1},
            "7": {"enemy": 1},
            "Boss": {"tyrant": 1},
        })
        self.assertEqual((agg["started"], agg["ended"], agg["unfinished"]), (3, 4, 0))
        self.assertEqual(agg["enemies"]["Grunt"]["elite"], 1)
        self.assertEqual(agg["enemies"]["Brute"]["elite"], 0)
        self.assertEqual(agg["enemies"]["Tyrant"]["elite"], 1)

    def test_missing_event_column_is_an_error(self):
        with self.assertRaises(ValueError):
            pre.parse_csv_text("a,b,c\n1,2,3\n")

    def test_empty_csv_gives_zeros(self):
        agg = pre.aggregate(fixture("empty.csv"))
        self.assertEqual((agg["started"], agg["ended"], agg["unfinished"]), (0, 0, 0))
        self.assertEqual(nonzero(agg), {})
        self.assertEqual(agg["enemies"], {})
        self.assertIn("No run_end rows yet", pre.format_summary(agg))

    def test_completely_empty_text(self):
        self.assertEqual(pre.parse_csv_text(""), [])


class TestSummary(unittest.TestCase):
    def test_summary_mentions_totals(self):
        text = pre.format_summary(pre.aggregate(fixture("basic.csv")))
        self.assertIn("Runs started: 9   ended: 8   unfinished (closed tab): 2", text)
        self.assertIn("Boss", text)
        text.encode("ascii")  # plain ASCII, safe on any console

    def test_footer(self):
        agg = pre.aggregate(fixture("basic.csv"))
        self.assertEqual(pre.footer_text(agg, "runs.csv"),
                         "Runs started: 9 · ended: 8 · unfinished (closed tab): 2 · source: runs.csv")


class TestRender(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()

    def tearDown(self):
        self.tmp.cleanup()

    def assert_png(self, path):
        self.assertTrue(os.path.isfile(path))
        self.assertGreater(os.path.getsize(path), 1000)
        with open(path, "rb") as f:
            self.assertEqual(f.read(8), b"\x89PNG\r\n\x1a\n")

    def test_render_normal(self):
        out = os.path.join(self.tmp.name, "normal.png")
        pre.render(pre.aggregate(fixture("realistic.csv")), out, "realistic.csv")
        self.assert_png(out)

    def test_render_empty(self):
        out = os.path.join(self.tmp.name, "empty.png")
        pre.render(pre.aggregate(fixture("empty.csv")), out, "empty.csv")
        self.assert_png(out)

    def test_main_writes_png(self):
        out = os.path.join(self.tmp.name, "cli.png")
        import contextlib
        import io
        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            code = pre.main(["--csv", os.path.join(FIXTURES, "basic.csv"), "--out", out])
        self.assertEqual(code, 0)
        self.assert_png(out)
        self.assertIn("unfinished (closed tab): 2", buffer.getvalue())


class TestSendTestEvent(unittest.TestCase):
    def test_payload_has_every_field(self):
        for event in ("run_end", "run_start"):
            payload = ste.build_event(event)
            self.assertEqual(set(payload), set(ste.FIELDS))
            self.assertEqual(len(payload), 16)
            self.assertEqual(payload["schema"], 1)
            self.assertEqual(payload["event"], event)
            self.assertEqual(payload["edition"], "test")
            self.assertRegex(payload["run_id"], HEX32)
            self.assertRegex(payload["session_id"], HEX32)
            self.assertNotEqual(payload["run_id"], payload["session_id"])
            self.assertIsInstance(payload["elite"], bool)
            self.assertIn(payload["stage"], ("wave", "build", "void_trial", "final_boss"))
            self.assertTrue(1 <= payload["wave"] <= 14)
            self.assertTrue(1 <= payload["level"] <= 12)
            self.assertIn(payload["starting_weapon"], ("RocketLauncher", "Rifle", "Knife", ""))

    def test_run_start_and_run_end_values(self):
        start = ste.build_event("run_start")
        self.assertEqual((start["outcome"], start["cause"], start["enemy"]), ("", "", ""))
        end = ste.build_event("run_end")
        self.assertEqual(end["outcome"], "defeat")
        self.assertIn(end["cause"], pre.CAUSES)

    def test_ids_are_random(self):
        self.assertNotEqual(ste.build_event()["run_id"], ste.build_event()["run_id"])

    def test_request_is_text_plain_post(self):
        payload = ste.build_event()
        request = ste.build_request("https://example.invalid/exec", payload)
        self.assertEqual(request.get_method(), "POST")
        self.assertEqual(request.get_header("Content-type"), "text/plain;charset=utf-8")
        self.assertEqual(json.loads(request.data.decode("utf-8")), payload)

    def test_bad_event_rejected(self):
        with self.assertRaises(ValueError):
            ste.build_event("level_up")


if __name__ == "__main__":
    unittest.main()
