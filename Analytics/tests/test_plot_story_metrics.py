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
RUNS, DAMAGE, CRATES = (os.path.join(FIXTURES, "story_%s.csv" % n) for n in ("runs", "damage", "crates"))
METRIC_CHARTS = ("story-endings.png", "story-spending.png", "story-weapons.png", "story-damage.png")
CHARTS = METRIC_CHARTS + ("story-days.png", "story-purchases.png", "story-hole-checks.png", "story-tutorial.png")
COMMON = "received_at,event,run_id,session_id,edition,build"


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
        self.assertEqual(m.label_item("weapon_mk"), "weapon Mk")

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


class EndingTests(unittest.TestCase):
    def test_story_endings_per_day_and_cause_with_the_final_battle_apart(self):
        agg = m.aggregate_endings(m.load_rows(RUNS))
        self.assertEqual({b: nonzero(c) for b, c in agg["buckets"].items()},
                         {3: {"enemy": 1, "base": 1, "status": 1}, 0: {"left": 1}, m.FINAL: {"victory": 1, "tyrant": 1},
                          4: {"void": 1}, 2: {"unknown": 1}})
        self.assertEqual(nonzero(agg["causes"]), {"enemy": 1, "tyrant": 1, "status": 1, "void": 1, "base": 1, "victory": 1,
                                                  "unknown": 1, "left": 1})
        self.assertEqual(m.ending_buckets(agg), [0, 1, 2, 3, 4, 5, 6, 7, m.FINAL])
        self.assertEqual([m.bucket_label(b) for b in (0, 7, m.FINAL)], ["Day 0", "Day 7", "Final boss"])

    def test_a_run_ends_once_and_open_test_and_unfinished_runs_are_told_apart(self):
        agg = m.aggregate_endings(m.load_rows(RUNS))
        self.assertEqual((agg["started"], agg["ended"], agg["unfinished"], agg["open_runs"]), (9, 8, 1, 2),
                         "run e started as the open mode and ended as the story mode: a story run; run a's second run_end is ignored")
        with_test = m.aggregate_endings(m.load_rows(RUNS), include_test=True)
        self.assertEqual((with_test["started"], with_test["ended"], nonzero(with_test["buckets"][1])), (10, 9, {"enemy": 1}))

    def test_defeats_per_day_and_the_enemy_kinds_behind_them(self):
        agg = m.aggregate_endings(m.load_rows(RUNS))
        self.assertEqual(agg["defeats"], {3: 3, m.FINAL: 1, 4: 1, 2: 1})
        self.assertEqual(m.worst_day(agg), (3, 3, 6), "the final battle is not a day")
        self.assertEqual(agg["enemies"], {"Brute": {"enemy": 1, "status": 0, "tyrant": 0, "elite": 1, "total": 1},
                                          "Tyrant": {"enemy": 0, "status": 0, "tyrant": 1, "elite": 0, "total": 1},
                                          "Spitter": {"enemy": 0, "status": 1, "tyrant": 0, "elite": 0, "total": 1}})
        self.assertIsNone(m.worst_day(m.aggregate_endings([])))

    def test_a_day_after_the_seventh_adds_its_own_column(self):
        text = (COMMON + ",outcome,cause,enemy,elite,stage,wave,run_seconds,level,kills,starting_weapon,mode\n"
                "t,run_end,a,s,en,b,defeat,enemy,Shade,FALSE,day,9,100,1,1,Pistol,story\n"
                "t,run_end,b,s,en,b,victory,victory,,FALSE,ending,9,100,1,1,Pistol,story\n")
        agg = m.aggregate_endings(m.parse_csv_text(text))
        self.assertEqual(m.ending_buckets(agg), list(range(10)) + [m.FINAL])
        self.assertEqual({b: nonzero(c) for b, c in agg["buckets"].items()}, {9: {"enemy": 1}, m.FINAL: {"victory": 1}})


class SpendingTests(unittest.TestCase):
    def test_spending_per_day_and_room_adds_the_three_currencies(self):
        agg = m.aggregate_spending(m.load_rows(PURCHASES))
        self.assertEqual({d: nonzero(rooms) for d, rooms in agg["days"].items()},
                         {1: {"Armament": 190, "Control": 15, "Energy": 210}, 2: {"Armament": 30}})
        self.assertEqual(agg["rooms"]["Energy"], {"alloy": 150, "energy": 50, "dark": 10})
        self.assertEqual(agg["rooms"]["Armament"], {"alloy": 180, "energy": 40, "dark": 0})
        self.assertEqual({d: len(runs) for d, runs in agg["runs_by_day"].items()}, {1: 3, 2: 1})
        self.assertEqual((agg["purchases"], agg["runs"]), (5, 3), "the test row and the row without a day are left out")
        self.assertAlmostEqual(m.spending_shares(agg, 1)["Armament"], 100 * 190 / 415)
        self.assertEqual(m.spending_shares(agg, 2)["Armament"], 100)

    def test_the_first_purchase_of_each_run_by_room_and_item(self):
        agg = m.aggregate_spending(m.load_rows(PURCHASES))
        self.assertEqual(nonzero(agg["first_room"]), {"Armament": 1, "Control": 1, "Energy": 1})
        self.assertEqual(agg["first_item"], {("Armament", "repair"): 1, ("Control", "lamp"): 1, ("Energy", "room_upgrade"): 1})
        self.assertEqual((agg["first_extra"], agg["extra_runs"]), ({("Armament", "repair"): 1, ("Control", "lamp"): 1}, 2),
                         "run e bought a room upgrade only")
        self.assertEqual(agg["first_day"], {1: 3})
        self.assertEqual(m.first_items(agg)[0], (("Armament", "repair"), 1), "ties: by the order of the rooms, then by name")

    def test_the_first_purchase_is_the_lowest_day_then_the_order_of_the_rows(self):
        text = (COMMON + ",day,stage,item,detail,room,alloy,energy,dark\n"
                "t,v2_purchase,x,s,en,b,2,dusk,fuel,1/10,Ship,0,0,10\n"
                "t,v2_purchase,x,s,en,b,1,dusk,room_upgrade,Armament level 1,Armament,10,0,0\n"
                "t,v2_purchase,x,s,en,b,1,dusk,weapon_mk,Rifle Mk2,Armament,8,0,0\n"
                "t,v2_purchase,x,s,en,b,1,dusk,charm,,Attic,1,0,0\n")
        agg = m.aggregate_spending(m.parse_csv_text(text))
        self.assertEqual(nonzero(agg["first_room"]), {"Armament": 1})
        self.assertEqual(agg["first_extra"], {("Armament", "weapon_mk"): 1})
        self.assertEqual(nonzero(agg["days"][1]), {"Armament": 18, "Other": 1}, "a room the base does not have counts as Other")


class WeaponTests(unittest.TestCase):
    def test_take_rates_by_weapon_and_by_place(self):
        agg = m.aggregate_weapons(m.load_rows(CRATES))
        self.assertEqual(agg["forms"], {"Rifle": {"offered": 2, "taken": 1}, "Knife": {"offered": 3, "taken": 0},
                                        "Railgun": {"offered": 3, "taken": 1}, "Minigun": {"offered": 2, "taken": 0},
                                        "OrbitBlades": {"offered": 1, "taken": 0}})
        self.assertEqual(agg["places"], {1: {"offered": 4, "taken": 1}, 2: {"offered": 4, "taken": 1}, 3: {"offered": 3, "taken": 0}})
        self.assertEqual(m.form_order(agg), ["Rifle", "Railgun", "Knife", "Minigun", "OrbitBlades"])
        self.assertEqual(m.overall_take_rate(agg), (2, 11))
        self.assertEqual(m.take_rate_spread(agg, least=2), (("Rifle", 50.0), ("Knife", 0.0)))
        self.assertIsNone(m.take_rate_spread(agg), "no weapon was offered twenty times")
        self.assertEqual(m.aggregate_weapons(m.load_rows(CRATES), include_test=True)["forms"]["Rifle"], {"offered": 3, "taken": 2})

    def test_crates_left_empty_or_without_recorded_offers(self):
        agg = m.aggregate_weapons(m.load_rows(CRATES))
        self.assertEqual((agg["crates"], len(agg["runs"])), (6, 2), "a crate row without a colon is skipped")
        self.assertEqual((agg["left"], agg["empty"], agg["no_room"], agg["unrecorded"]), (1, 1, 1, 1),
                         "the Plasma Launcher came from a crate with no crate_offer rows: it is in no rate")
        self.assertEqual(agg["days"], {0: {"opened": 1, "left": 1}, 1: {"opened": 2, "left": 0}, 3: {"opened": 1, "left": 0}},
                         "Day 0's first crate cannot be left and an empty crate holds nothing to leave")
        self.assertEqual(m.crates_left(agg), (1, 4))

    def test_a_weapon_is_named_as_the_crate_names_it(self):
        self.assertEqual([m.label_form(f) for f in ("ArcCoil", "Minigun", "ShieldRing", "NewForm")],
                         ["Arc Cannon", "Gatling", "Energy Ring", "NewForm"])


class StoryDamageTests(unittest.TestCase):
    def test_story_rows_by_source_and_day_with_the_final_battle_apart(self):
        agg = m.aggregate_story_damage(m.load_rows(DAMAGE))
        self.assertEqual(agg["total"], 160)
        self.assertEqual(agg["sources"], {"Grunt": 20, "backfire": 10, "shadow": 10, "Spire_elite": 30, "Spitter": 10, "Tyrant": 60,
                                          "wall_slam": 20})
        self.assertEqual(agg["dot"], {"Spitter": 10})
        self.assertEqual(agg["by_bucket"], {1: {"Grunt": 20, "backfire": 10, "shadow": 10}, 3: {"Spire": 30, "Spitter": 10},
                                            m.FINAL: {"Tyrant": 60}, 7: {"wall_slam": 20}})
        self.assertEqual((len(agg["runs"]), agg["open_rows"]), (2, 2), "a row with no mode is the open mode")
        self.assertEqual(m.damage_buckets(agg), [0, 1, 2, 3, 4, 5, 6, 7, m.FINAL])
        self.assertEqual((m.source_share(agg, 1, "backfire"), m.source_share(agg, 5, "backfire")), (25.0, 0.0))

    def test_colours_follow_the_source(self):
        groups, colors = m.story_damage_groups(m.aggregate_story_damage(m.load_rows(DAMAGE)))
        self.assertEqual(groups, {"Grunt": "enemy2", "backfire": "backfire", "shadow": "shadow", "Spire": "enemy1", "Spitter": "enemy3",
                                  "Tyrant": "Tyrant", "wall_slam": "wall_slam"})
        self.assertEqual((colors["shadow"], colors["Spire"]), (m.STORY_FIXED_SOURCES["shadow"], m.STORY_ENEMY_SLOTS[0]))
        self.assertEqual([m.label_source(s) for s in ("Spire_elite", "shadow", "Hollow", "backfire")],
                         ["Spire (elite)", "Shadow (night)", "Nash Prophet", "Own weapon"])


class DemoTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.demo = m.demo_rows(runs=40)

    def test_every_made_up_row_is_marked_and_the_same_seed_gives_the_same_rows(self):
        self.assertEqual(set(self.demo), set(m.DEMO_TABS))
        for tab in m.DEMO_TABS:
            self.assertTrue(self.demo[tab], tab)
            self.assertEqual({row["build"] for row in self.demo[tab]}, {m.DEMO_BUILD}, tab)
            self.assertEqual(len({tuple(row) for row in self.demo[tab]}), 1, tab + ": one set of columns, so the CSV is whole")
        self.assertEqual(m.demo_rows(runs=40), self.demo)

    def test_the_five_sheets_describe_the_same_story_runs(self):
        started = {row["run_id"] for row in self.demo["runs"] if row["event"] == "run_start"}
        self.assertEqual(len(started), 40)
        self.assertEqual({row["mode"] for tab in ("runs", "damage") for row in self.demo[tab]}, {"story"})
        for tab in m.DEMO_TABS:
            self.assertLessEqual({row["run_id"] for row in self.demo[tab]}, started, tab)
        spent = {}
        for row in self.demo["v2_purchases"]:
            key = (row["run_id"], row["day"])
            spent[key] = spent.get(key, 0) + row["alloy"]
        for row in self.demo["v2_days"]:
            self.assertEqual(row["spent_alloy"], spent.get((row["run_id"], row["day"]), 0), "a day's spending is the sum of its purchases")

    def test_a_crate_row_follows_its_offers(self):
        agg = m.aggregate_weapons(m.parse_csv_text(m._to_text(self.demo["v2_events"])))
        self.assertEqual(agg["unrecorded"], 0)
        self.assertEqual(sum(e["offered"] for e in agg["places"].values()), sum(e["offered"] for e in agg["forms"].values()))
        self.assertLessEqual(set(agg["forms"]), set(m.FORM_NAMES))


class RenderTests(unittest.TestCase):
    def setUp(self):
        try:
            import matplotlib  # noqa: F401
        except ImportError:
            self.skipTest("matplotlib is not installed")

    def run_main(self, args):
        with contextlib.redirect_stdout(io.StringIO()):
            return m.main(args)

    def test_demo_writes_eight_charts_and_five_csvs(self):
        with tempfile.TemporaryDirectory() as out:
            self.assertEqual(self.run_main(["--demo", "--out-dir", out]), 0)
            for name in CHARTS + tuple("demo-story-%s.csv" % tab for tab in m.DEMO_TABS):
                self.assertGreater(os.path.getsize(os.path.join(out, name)), 1000, name)
            with open(os.path.join(out, "demo-story-runs.csv"), encoding="utf-8") as f:
                self.assertTrue(all(m.DEMO_BUILD in line for line in f.read().splitlines()[1:]))

    def test_the_fixture_exports_render_all_eight_charts(self):
        with tempfile.TemporaryDirectory() as out:
            self.assertEqual(self.run_main(["--runs", RUNS, "--damage", DAMAGE, "--days", DAYS, "--purchases", PURCHASES,
                                            "--events", EVENTS, "--out-dir", out]), 0)
            for name in CHARTS:
                self.assertGreater(os.path.getsize(os.path.join(out, name)), 1000, name)

    def test_each_export_draws_only_its_own_charts(self):
        with tempfile.TemporaryDirectory() as out:
            self.assertEqual(self.run_main(["--runs", RUNS, "--out-dir", out]), 0)
            self.assertEqual(os.listdir(out), ["story-endings.png"])
        with tempfile.TemporaryDirectory() as out:
            self.assertEqual(self.run_main(["--events", CRATES, "--out-dir", out]), 0)
            self.assertEqual(sorted(os.listdir(out)), ["story-hole-checks.png", "story-tutorial.png", "story-weapons.png"])

    def test_empty_exports_still_render(self):
        with tempfile.TemporaryDirectory() as out:
            paths = [os.path.join(out, name) for name in CHARTS]
            m.render_endings(m.aggregate_endings([]), paths[0], "test")
            m.render_spending(m.aggregate_spending([]), paths[1], "test")
            m.render_weapons(m.aggregate_weapons([]), paths[2], "test")
            m.render_story_damage(m.aggregate_story_damage([]), paths[3], "test")
            m.render_days(m.aggregate_days([]), paths[4], "test")
            m.render_purchases(m.aggregate_purchases([]), paths[5], "test")
            m.render_hole(m.aggregate_hole([]), paths[6], "test")
            m.render_tutorial(m.aggregate_tutorial([]), paths[7], "test")
            for path in paths:
                self.assertTrue(os.path.exists(path), path)

    def test_the_text_tables_name_what_the_charts_show(self):
        endings = m.format_endings(m.aggregate_endings(m.load_rows(RUNS)))
        self.assertIn("Story runs started: 9   ended: 8   unfinished (closed tab): 1", endings)
        self.assertIn("Most defeats: Day 3 with 3 of 6 (50%)", endings)
        self.assertIn("Open-mode runs left out: 2", endings)
        self.assertIn("First purchase by room: Armament 1 (33%), Energy 1 (33%), Control 1 (33%)",
                      m.format_spending(m.aggregate_spending(m.load_rows(PURCHASES))))
        weapons = m.format_weapons(m.aggregate_weapons(m.load_rows(CRATES)))
        self.assertIn("Place 1: taken 1 of 4 (25%)", weapons)
        self.assertIn("Left without taking a weapon: 1 of 4 crates that could be left (25%)", weapons)
        self.assertIn("Own weapon, share per day: Day 1 25%", m.format_story_damage(m.aggregate_story_damage(m.load_rows(DAMAGE))))


if __name__ == "__main__":
    unittest.main()
