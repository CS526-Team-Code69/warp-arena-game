"""Tests for plot_story_metrics.py. No network: python -m unittest discover -s Analytics/tests -v"""

import contextlib
import io
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import plot_story_metrics as m  # noqa: E402

FIXTURES = os.path.join(os.path.dirname(__file__), "fixtures")
DAYS, PURCHASES, EVENTS = (os.path.join(FIXTURES, "story_v2_%s.csv" % n) for n in ("days", "purchases", "events"))
CHARTS = ("story-days.png", "story-purchases.png", "story-hole-checks.png", "story-tutorial.png")


def nonzero(counts):
    return {k: v for k, v in counts.items() if v}


class DayTests(unittest.TestCase):
    def test_each_day_counts_how_it_ended_and_test_rows_are_left_out(self):
        agg = m.aggregate_days(m.load_rows(DAYS))
        self.assertEqual((agg["rows"], len(agg["runs"])), (11, 5))
        self.assertEqual(nonzero(agg["days"][0]), {"dawn": 3, "left": 1})
        self.assertEqual(nonzero(agg["days"][1]), {"dawn": 2, "died": 1, "unknown": 1})
        self.assertEqual(nonzero(agg["days"][2]), {"dawn_night_death": 1, "won": 1})
        self.assertEqual(nonzero(agg["days"][3]), {"base": 1})
        self.assertEqual(m.aggregate_days(m.load_rows(DAYS), include_test=True)["rows"], 12)

    def test_the_last_day_says_where_the_run_ended(self):
        agg = m.aggregate_days(m.load_rows(DAYS))
        self.assertEqual({s: nonzero(c) for s, c in agg["stages"].items()},
                         {"night": {"base": 1}, "day": {"died": 1}, "tutorial": {"left": 1}, "ending": {"won": 1}, "other": {"unknown": 1}})

    def test_day_end_reads_dawn_before_the_cause(self):
        self.assertEqual(m.day_end({"reached_dawn": "TRUE", "night_death": "TRUE", "end_cause": "enemy"}), "dawn_night_death")
        self.assertEqual(m.day_end({"reached_dawn": "FALSE", "end_cause": "restart"}), "left")
        self.assertEqual(m.day_end({"reached_dawn": "FALSE", "end_cause": "tyrant"}), "died")


class PurchaseTests(unittest.TestCase):
    def test_items_per_day_with_the_void_purchases_from_the_events(self):
        agg = m.aggregate_purchases(m.load_rows(PURCHASES), m.load_rows(EVENTS))
        self.assertEqual(agg["items"], {"repair": {1: 1, 2: 1}, "new_weapon": {1: 1}, "lamp": {1: 1}, "room_upgrade": {1: 1},
                                        "void_skill": {3: 1}})
        self.assertEqual(nonzero(agg["rooms"]), {"Armament": 3, "Control": 1, "Energy": 1, "Void": 1})
        self.assertEqual({d: len(runs) for d, runs in agg["runs_by_day"].items()}, {1: 3, 2: 1, 3: 1})
        self.assertEqual(m.item_order(agg), ["repair", "lamp", "new_weapon", "room_upgrade", "void_skill"])
        self.assertEqual(m.label_item("void_skill"), "Void: skill")

    def test_without_events_only_the_base_purchases_count_and_rows_without_a_day_are_skipped(self):
        self.assertEqual(m.aggregate_purchases(m.load_rows(PURCHASES))["total"], 5)


class HoleTests(unittest.TestCase):
    def test_results_per_day_and_checks_never_settled(self):
        agg = m.aggregate_hole(m.load_rows(EVENTS))
        self.assertEqual(agg["days"][1], {"started": 3, "passed": 0, "failed_timeout": 1, "failed_death": 1, "failed_night": 1, "unsettled": 0})
        self.assertEqual(agg["days"][2], {"started": 2, "passed": 1, "failed_timeout": 0, "failed_death": 0, "failed_night": 0, "unsettled": 1})
        self.assertEqual(agg["failure_shares"], {"failed_timeout": [0.6], "failed_death": [0.4], "failed_night": [0.9]})


class TutorialTests(unittest.TestCase):
    def test_seconds_per_step_in_teaching_order_with_other_steps_last(self):
        agg = m.aggregate_tutorial(m.load_rows(EVENTS))
        self.assertEqual(m.tutorial_order(agg), m.TUTORIAL_STEPS + ["Swim"])
        self.assertEqual(agg["steps"]["Move"], [10, 20, 30, 40], "the test row's 999 is left out")
        self.assertEqual([m._quantile(agg["steps"]["Move"], q) for q in (.25, .5, .75)], [17.5, 25, 32.5])
        self.assertEqual({s: len(r) for s, r in agg["runs"].items()},
                         {"Move": 4, "Fire": 4, "Mine": 2, "Crate": 2, "Sprint": 2, "Fight": 2, "Collect": 2, "Lamp": 1, "DarkMatter": 1,
                          "Swim": 1})
        self.assertEqual(m.crystal_share(agg), (2, 4))

    def test_quantile_of_one_value_or_none(self):
        self.assertEqual(m._quantile([7.0], .75), 7.0)
        self.assertEqual(m._quantile([], .5), 0.0)


class RenderTests(unittest.TestCase):
    def setUp(self):
        try:
            import matplotlib  # noqa: F401
        except ImportError:
            self.skipTest("matplotlib is not installed")

    def run_main(self, args):
        with contextlib.redirect_stdout(io.StringIO()):
            return m.main(args)

    def test_demo_writes_four_charts_and_three_csvs(self):
        with tempfile.TemporaryDirectory() as out:
            self.assertEqual(self.run_main(["--demo", "--out-dir", out]), 0)
            for name in CHARTS + ("demo-v2_days.csv", "demo-v2_purchases.csv", "demo-v2_events.csv"):
                self.assertGreater(os.path.getsize(os.path.join(out, name)), 1000, name)

    def test_the_fixture_exports_render_all_four_charts(self):
        with tempfile.TemporaryDirectory() as out:
            self.assertEqual(self.run_main(["--days", DAYS, "--purchases", PURCHASES, "--events", EVENTS, "--out-dir", out]), 0)
            for name in CHARTS:
                self.assertGreater(os.path.getsize(os.path.join(out, name)), 1000, name)

    def test_empty_exports_still_render(self):
        with tempfile.TemporaryDirectory() as out:
            paths = [os.path.join(out, name) for name in CHARTS]
            m.render_days(m.aggregate_days([]), paths[0], "test")
            m.render_purchases(m.aggregate_purchases([]), paths[1], "test")
            m.render_hole(m.aggregate_hole([]), paths[2], "test")
            m.render_tutorial(m.aggregate_tutorial([]), paths[3], "test")
            for path in paths:
                self.assertTrue(os.path.exists(path), path)


if __name__ == "__main__":
    unittest.main()
