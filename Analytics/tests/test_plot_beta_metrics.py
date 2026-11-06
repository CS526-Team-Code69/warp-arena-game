"""Tests for plot_beta_metrics.py. No network: python -m unittest discover -s Analytics/tests -v"""

import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import plot_beta_metrics as m  # noqa: E402

COMMON = "received_at,event,run_id,session_id,edition,build"


class UpgradeTests(unittest.TestCase):
    def test_pick_rates_by_direction_and_kind_leave_test_rows_out(self):
        text = (COMMON + ",context,wave,level,offer_1,offer_2,offer_3,picked,picked_kind,weapon_offered,rerolled\n"
                "t,upgrade_pick,a,s,en,b,levelup,1,1,Weapon:Rifle:Power,Skill:Blink,Weapon:Knife:Tempo,Weapon:Rifle:Power,Weapon,2,FALSE\n"
                "t,upgrade_pick,a,s,en,b,build,2,2,Weapon:Rifle:Power,Stat:Damage,Stat:MaxHp,Stat:MaxHp,Stat,1,FALSE\n"
                "t,upgrade_pick,a,s,en,b,levelup,3,3,Skill:Blink,Stat:Damage,Stat:MaxHp,Skill:Blink,Skill,0,TRUE\n"
                "t,upgrade_pick,a,s,test,b,levelup,3,3,Weapon:Rifle:Power,Stat:Damage,Stat:MaxHp,Weapon:Rifle:Power,Weapon,1,FALSE\n")
        agg = m.aggregate_upgrades(m.parse_csv_text("﻿" + text))
        self.assertEqual(agg["picks"], 3)
        self.assertEqual(agg["axis"]["Power"], {"offered": 2, "picked": 1})
        self.assertEqual(agg["axis"]["Tempo"], {"offered": 1, "picked": 0})
        self.assertEqual(agg["kind"]["Stat"], {"offered": 4, "picked": 1})
        self.assertEqual((agg["weapon_on_offer"], agg["weapon_taken"]), (2, 1))
        self.assertEqual(agg["context"], {"levelup": 2, "build": 1})


class SupplyTests(unittest.TestCase):
    def test_actions_and_replaced_weapons(self):
        text = (COMMON + ",wave,level,offer_1,offer_2,offer_3,action,picked,replaced,replace_cancels\n"
                "t,supply_pick,a,s,en,b,1,1,Minigun,Rifle,Knife,replace,Minigun,Knife,1\n"
                "t,supply_pick,a,s,en,b,2,2,Minigun,Rifle,Knife,replace,Rifle,Knife,0\n"
                "t,supply_pick,a,s,en,b,3,3,Minigun,Rifle,Knife,skip,,,2\n"
                "t,supply_pick,a,s,en,b,3,3,Minigun,Rifle,Knife,bogus,,,0\n")
        agg = m.aggregate_supply(m.parse_csv_text(text))
        self.assertEqual(agg["drops"], 3)
        self.assertEqual(agg["actions"]["replace"], 2)
        self.assertEqual(agg["replaced"], {"Knife": 2})
        self.assertEqual(agg["cancels"], 3)


class DamageTests(unittest.TestCase):
    TEXT = (COMMON + ",stage,wave,bucket,source,dot,damage,damage_pct,hits\n"
            "t,wave_damage,a,s,en,b,wave,1,W01,Grunt,FALSE,10,20,1\n"
            "t,wave_damage,a,s,en,b,wave,1,W01,Grunt_elite,FALSE,5,10,1\n"
            "t,wave_damage,a,s,en,b,wave,2,W02,Spitter,TRUE,5,10,3\n"
            "t,wave_damage,b,s,en,b,final_boss,14,W15 Boss,Tyrant,FALSE,30,60,2\n")

    def test_sources_elites_and_dot(self):
        agg = m.aggregate_damage(m.parse_csv_text(self.TEXT))
        self.assertEqual(agg["total"], 100)
        self.assertEqual(agg["sources"]["Grunt_elite"], 10)
        self.assertEqual(agg["dot"], {"Spitter": 10})
        self.assertEqual(agg["by_bucket"]["W01"], {"Grunt": 30}, "elites are counted with their kind by wave")
        self.assertEqual(agg["by_bucket"][m.BOSS_BUCKET], {"Tyrant": 60})
        self.assertEqual(len(agg["runs"]), 2)

    def test_colour_groups_keep_fixed_sources_and_fold_the_rest(self):
        agg = {"sources": {"backfire": 5, "wall_slam": 4, "Tyrant": 3, "Grunt": 50, "Brute": 40, "Spitter": 30, "Rammer": 20,
                           "Skitter": 10, "Skitter_elite": 1, "other": 2}}
        groups, colors = m.damage_groups(agg)
        self.assertEqual(groups["Grunt"], "enemy1")
        self.assertEqual(groups["Rammer"], "enemy4")
        self.assertEqual(groups["Skitter"], "Other")
        self.assertEqual(groups["other"], "Other")
        self.assertEqual(colors["backfire"], "#1baf7a", "own weapon keeps its colour from the run-endings chart")
        self.assertEqual(m.label_of("Brute_elite"), "Brute (elite)")


class RenderTests(unittest.TestCase):
    def test_demo_writes_three_charts_and_three_csvs(self):
        try:
            import matplotlib  # noqa: F401
        except ImportError:
            self.skipTest("matplotlib is not installed")
        with tempfile.TemporaryDirectory() as out:
            self.assertEqual(m.main(["--demo", "--out-dir", out]), 0)
            for name in ("upgrade-picks.png", "supply-picks.png", "damage-sources.png",
                         "demo-upgrade_picks.csv", "demo-supply_picks.csv", "demo-damage.csv"):
                self.assertGreater(os.path.getsize(os.path.join(out, name)), 1000, name)

    def test_empty_exports_still_render(self):
        try:
            import matplotlib  # noqa: F401
        except ImportError:
            self.skipTest("matplotlib is not installed")
        with tempfile.TemporaryDirectory() as out:
            path = os.path.join(out, "empty.png")
            m.render_damage(m.aggregate_damage([]), path, "test")
            m.render_supply(m.aggregate_supply([]), os.path.join(out, "s.png"), "test")
            m.render_upgrades(m.aggregate_upgrades([]), os.path.join(out, "u.png"), "test")
            self.assertTrue(os.path.exists(path))


if __name__ == "__main__":
    unittest.main()
