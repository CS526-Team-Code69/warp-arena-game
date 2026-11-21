using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// What the base's two pages and the HUD ask of the campaign (work/redesign-1121/接口.md). The workshop opens at the base door: the
    /// weapon column and the energy research (BuildStudy, StudyEntry.buy), the base's fortification in its top bar. The rocket page opens
    /// at the ship: the ship's three parts, the fuel, the mining lamps and the ignition. Both pages end with the same two ways out of the
    /// visit (BaseExits). Here too: which workshop feature opens on which day. The pages and the HUD read and buy only through these
    /// members, BuildStudy and StudyEntry.buy.
    /// </summary>
    public sealed partial class V2Campaign
    {
        // ───── The two pages ─────

        /// <summary>The base's two pages: the workshop (weapons and energy research) and the rocket (the ship, the fuel and the lamps).</summary>
        public enum BaseScreen { Workshop, Rocket }

        /// <summary>A page was asked for (RequestBaseScreen), after the base visit is open: the screen layer shows that page.</summary>
        public event Action<BaseScreen> BaseScreenRequested;

        /// <summary>The page the open base visit shows: the workshop when it came in at the base door (or on Day 0's arrival), the rocket
        /// when it came in at the ship, and then whichever RequestBaseScreen asked for last. The screen layer opens on this page when the
        /// base opens (V2CampaignUi.ShowBase).</summary>
        public BaseScreen BaseScreenNow { get; private set; }

        /// <summary>Asks for one of the base's pages. Out in the afternoon or at night (the ship's E) it opens a base visit on that page,
        /// as the base door does (tonight's haul is kept, the clock waits); during a visit it switches to it (each page's button to the
        /// other). Then BaseScreenRequested is raised. Anywhere else (the morning, Day 0's walk, a cutscene, the void trial, a fallen
        /// player) it does nothing.</summary>
        public void RequestBaseScreen(BaseScreen screen)
        {
            if (!Running) return;
            if (Stage is CampaignStage.Dusk or CampaignStage.Night)
            {
                if (WorldMap.Active && WorldMap.Active.TrialActive || !gm || !gm.player || !gm.player.IsAlive) return;
                OpenBase(screen);
            }
            else if (Stage == CampaignStage.Base) BaseScreenNow = screen;
            else return;
            BaseScreenRequested?.Invoke(screen);
        }

        // ───── The ways out of a base visit ─────

        /// <summary>One way out of a base visit, as the pages' bottom row shows it: its label, a short line under it (empty for none),
        /// whether it is the main (orange) button, and what pressing it does.</summary>
        public sealed class BaseExit
        {
            public string label, note;
            public bool primary;
            public Action go;
        }

        /// <summary>The ways out of the open base visit, the secondary first and the main one last. In the afternoon: "Head out · back to the
        /// afternoon" and "Nightfall" (its line: take the lamps and mine dark matter). At night: "Head out · back to the night" and "Wait
        /// for dawn", whose line from the last planned day on says whether the ignition follows or another day begins. On Day 0's arrival
        /// only "Head out (night falls)", or "Head out (day 1 begins)" once Day 0 was skipped (V2Campaign.DayZeroSkip.cs). Empty when no
        /// visit is open. Each go closes the base (V2CampaignUi.HideBase) as it leaves.</summary>
        public List<BaseExit> BaseExits()
        {
            var exits = new List<BaseExit>();
            if (!Running || !rules || Stage != CampaignStage.Base) return exits;
            if (BaseAfterTutorial)
            {
                string label = dayZeroSkipped ? ArenaText.Pair("出发（进入第 1 天）", "Head out (day 1 begins)") : ArenaText.Pair("出发（夜幕降临）", "Head out (night falls)");
                exits.Add(new BaseExit { label = label, note = "", primary = true, go = LeaveBase });
                return exits;
            }
            if (!OutdoorVisit) return exits;
            // Only with a clock on the evenings (CampaignRules.openEndedEvenings off): the time left goes on the way back out.
            string left = !EveningsOpenEnded && NightTimeLeft(out float seconds)
                ? ArenaText.Pair($"（还剩 {Mathf.CeilToInt(seconds)} 秒）", $" ({Mathf.CeilToInt(seconds)} s left)") : "";
            if (BaseFromDusk)
            {
                exits.Add(new BaseExit { label = ArenaText.Pair("出门 · 继续下午", "Head out · back to the afternoon") + left, note = "", primary = false, go = LeaveBase });
                exits.Add(new BaseExit { label = ArenaText.Pair("入夜", "Nightfall"), note = ArenaText.Pair("带上矿灯去采暗物质", "Take your lamps and mine dark matter"), primary = true, go = EnterNight });
            }
            else
            {
                exits.Add(new BaseExit { label = ArenaText.Pair("出门 · 继续今晚", "Head out · back to the night") + left, note = "", primary = false, go = LeaveBase });
                exits.Add(new BaseExit { label = ArenaText.Pair("等待天亮", "Wait for dawn"), note = DawnNote, primary = true, go = WaitForDawn });
            }
            return exits;
        }

        /// <summary>The line under "Wait for dawn" from the last planned day on: the ignition follows (the ship is ready), or another day
        /// begins; empty before that day.</summary>
        string DawnNote => Day < rules.days ? ""
            : CanIgnite ? ArenaText.Pair("随后点火返航", "Then the ignition starts")
            : ArenaText.Pair($"飞船还走不了，进入第 {Day + 1} 天", $"The ship can't leave yet; day {Day + 1} begins");

        // ───── What opens on which day ─────

        /// <summary>The workshop's features that open on a day of their own. The weapon Mk and armor are open from Day 0, as the body is.</summary>
        public enum StudyFeature { Body, Skills, Tame, Infuse, Twist }

        /// <summary>The day a feature opens (CampaignRules: skillsOpenDay 0, tameOpenDay 1, infuseOpenDay 2, twistOpenDay 4; the body Day 0).</summary>
        public int OpensOnDay(StudyFeature feature) => rules ? rules.OpenDay(feature) : 0;

        /// <summary>The feature is open today.</summary>
        public bool FeatureOpen(StudyFeature feature) => Day >= OpensOnDay(feature);

        /// <summary>When a feature that is not open yet opens, as a locked tab or row says it: "Opens tomorrow" or "Opens on day N"; empty
        /// once it is open.</summary>
        public string OpensNote(StudyFeature feature)
        {
            int day = OpensOnDay(feature);
            return day <= Day ? "" : day == Day + 1 ? ArenaText.Pair("明天开放", "Opens tomorrow") : ArenaText.Pair($"第 {day} 天开放", $"Opens on day {day}");
        }

        /// <summary>A feature's name, as its tab shows it.</summary>
        public static string FeatureName(StudyFeature feature) => feature switch
        {
            StudyFeature.Body => ArenaText.Pair("体质", "Body"),
            StudyFeature.Skills => ArenaText.Pair("技能", "Skills"),
            StudyFeature.Tame => ArenaTerms.Upgrade(UpgradeAxis.Tame),
            StudyFeature.Infuse => ArenaTerms.Upgrade(UpgradeAxis.Infusion),
            _ => ArenaTerms.Upgrade(UpgradeAxis.Twist)
        };

        /// <summary>What opens tomorrow, in one sentence ("Opens tomorrow: Tame"): the features opening then, and the final overload the day
        /// before it opens. Empty when nothing opens tomorrow.</summary>
        public string TomorrowOpens()
        {
            if (!Running || !rules) return "";
            var names = new List<string>();
            foreach (StudyFeature feature in Enum.GetValues(typeof(StudyFeature)))
                if (OpensOnDay(feature) == Day + 1) names.Add(FeatureName(feature));
            if (rules.overloadDay == Day + 1) names.Add(ArenaText.Pair("决战过载", "Final overload"));
            if (names.Count == 0) return "";
            string list = "";
            for (int i = 0; i < names.Count; i++)
                list += (i == 0 ? "" : i == names.Count - 1 ? ArenaText.Pair("、", " and ") : ArenaText.Pair("、", ", ")) + names[i];
            return ArenaText.Pair($"明天开放：{list}", $"Opens tomorrow: {list}");
        }

        // ───── Base fortification (the workshop's top bar) ─────

        /// <summary>The base's fortification level, 1 to FortifyTop: each level raises its max HP (CampaignRules.baseLevelHp).</summary>
        public int FortifyLevel => Durability;
        public int FortifyTop => MaxDurability;
        /// <summary>The base's max HP at its level now, and at the next level (the same at the top).</summary>
        public float FortifyHpNow => BaseMaxHp;
        public float FortifyHpNext => rules.BaseHpAt(Mathf.Min(Durability + 1, MaxDurability));
        /// <summary>The next level's price in alloy (CampaignRules.baseLevelAlloy), or null at the top.</summary>
        public CampaignCost? FortifyPrice => rules.BaseLevelPrice(Durability);

        /// <summary>Fortifies the base one level: its max HP rises, and it keeps the share of HP it had. False at the top or short of alloy.</summary>
        public bool TryFortify()
        {
            var price = FortifyPrice;
            if (!Running || price == null || !TrySpend(price.Value)) return false;
            float before = BaseMaxHp, share = before > 0f ? BaseHp / before : 1f;
            Durability++;
            BaseHp = Mathf.Max(BaseHp, share * BaseMaxHp);
            Bought("fortify", $"level {Durability}", CampaignRoom.Control, price.Value);
            ui.Flash(ArenaText.Pair($"基地加固到 {Durability} 级：血量上限 {before:0} → {BaseMaxHp:0}", $"Base fortified to level {Durability}: max HP {before:0} → {BaseMaxHp:0}"));
            return true;
        }

        // ───── The ship (the rocket page) ─────

        /// <summary>The ship's three parts, each bought once and in any order.</summary>
        public enum ShipPartKind { Engine, Hull, Tank }

        /// <summary>One of the ship's parts as the rocket page and the HUD's way-home bar show it. All texts arrive localized.</summary>
        public sealed class ShipPart
        {
            public ShipPartKind kind;
            /// <summary>Its name: engine, hull, fuel tank.</summary>
            public string name;
            /// <summary>What it gives once done, with the rules' numbers (the engine: afternoon alloy +30%).</summary>
            public string effect;
            /// <summary>The word on its button: repair (the engine, the hull) or fit (the fuel tank).</summary>
            public string verb;
            /// <summary>The line under its name: done (repaired, fitted, with a check mark) or still to do.</summary>
            public string status;
            public bool done;
            /// <summary>Its price in alloy; a page shows it only while the part is not done.</summary>
            public CampaignCost price;
            /// <summary>Not done, and the stock pays for it.</summary>
            public bool affordable;
        }

        readonly ShipPart[] shipParts = { new() { kind = ShipPartKind.Engine }, new() { kind = ShipPartKind.Hull }, new() { kind = ShipPartKind.Tank } };

        /// <summary>The engine, the hull and the fuel tank as they stand, in that order. The same three objects come back from every call,
        /// brought up to date, so a page or the HUD can read them every frame.</summary>
        public IReadOnlyList<ShipPart> ShipParts()
        {
            foreach (var part in shipParts)
            {
                bool tank = part.kind == ShipPartKind.Tank;
                part.done = PartDone(part.kind);
                part.price = rules ? rules.ShipPartPrice(part.kind) : default;
                part.affordable = Running && !part.done && CanAfford(part.price);
                part.name = ShipPartName(part.kind);
                part.effect = ShipPartEffect(part.kind);
                part.verb = tank ? ArenaText.Pair("安装", "Fit") : ArenaText.Pair("修理", "Repair");
                part.status = part.done ? tank ? ArenaText.Pair("已装好 ✓", "Fitted ✓") : ArenaText.Pair("已修好 ✓", "Repaired ✓")
                    : tank ? ArenaText.Pair("待安装", "Not fitted") : ArenaText.Pair("待修理", "Needs repair");
            }
            return shipParts;
        }

        bool PartDone(ShipPartKind kind) => kind switch
        {
            ShipPartKind.Engine => engineFixed,
            ShipPartKind.Hull => armorFixed,
            _ => fuelTankFitted
        };

        public static string ShipPartName(ShipPartKind kind) => kind switch
        {
            ShipPartKind.Engine => ArenaText.Pair("引擎", "Engine"),
            ShipPartKind.Hull => ArenaText.Pair("船体", "Hull"),
            _ => ArenaText.Pair("燃料舱", "Fuel tank")
        };

        /// <summary>What a part gives once done, with the rules' numbers.</summary>
        string ShipPartEffect(ShipPartKind kind)
        {
            if (!rules) return "";
            string Percent(float share) => $"{share * 100f:0}";
            switch (kind)
            {
                case ShipPartKind.Engine:
                    return ArenaText.Pair($"下午的合金 +{Percent(rules.shipOreBonus)}%", $"Afternoon alloy +{Percent(rules.shipOreBonus)}%");
                case ShipPartKind.Hull:
                    return ArenaText.Pair($"基地受到的伤害 −{Percent(rules.shipBaseDamageCut)}%，矿灯光照 +{Percent(rules.shipLampRadiusBonus)}%",
                        $"Base damage taken −{Percent(rules.shipBaseDamageCut)}%, lamp light +{Percent(rules.shipLampRadiusBonus)}%");
                default:
                    string skills = Mathf.Approximately(rules.shipSkillCooldownCut, rules.shipSkillCostCut)
                        ? ArenaText.Pair($"技能冷却和充能消耗 −{Percent(rules.shipSkillCooldownCut)}%", $"Skill cooldowns and charge costs −{Percent(rules.shipSkillCooldownCut)}%")
                        : ArenaText.Pair($"技能冷却 −{Percent(rules.shipSkillCooldownCut)}%，充能消耗 −{Percent(rules.shipSkillCostCut)}%",
                            $"Skill cooldowns −{Percent(rules.shipSkillCooldownCut)}%, charge costs −{Percent(rules.shipSkillCostCut)}%");
                    return skills + ArenaText.Pair("，暗物质可以加进燃料", "; dark matter can go into the fuel");
            }
        }

        /// <summary>Buys one of the ship's parts, once each and in any order: the engine's afternoon alloy from the next day on, the hull's
        /// lighter raids and wider lamp light, the fuel tank's cheaper and quicker skills and the way into the fuel. False when it is done
        /// already or the stock does not pay for it.</summary>
        public bool TryBuyShipPart(ShipPartKind kind)
        {
            if (!Running || !rules || PartDone(kind)) return false;
            var price = rules.ShipPartPrice(kind);
            if (!TrySpend(price)) return false;
            switch (kind)
            {
                case ShipPartKind.Engine:
                    engineFixed = true;
                    shipOreFromDay = Day + 1;
                    Say(Line.S08);
                    break;
                case ShipPartKind.Hull:
                    armorFixed = true;
                    Say(Line.S09);
                    break;
                default:
                    fuelTankFitted = true;
                    CampaignAudio.Play(CampaignSfx.AreaBuild);
                    ui.Flash(ArenaText.Pair("燃料舱装好了：现在可以把暗物质加进燃料。", "The fuel tank is fitted: dark matter can go into the fuel now."));
                    break;
            }
            RefreshShip();
            ApplyShipEffects();
            Bought("ship_part", kind switch { ShipPartKind.Engine => "engine", ShipPartKind.Hull => "hull", _ => "fuel_tank" }, CampaignRoom.Ship, price);
            return true;
        }

        /// <summary>The dark matter of a full tank (CampaignRules.fuelTankDark, 100). The fuel itself is Fuel.</summary>
        public int FuelTop => rules ? rules.FuelTop : 100;
        /// <summary>The hull is repaired.</summary>
        public bool HullFixed => armorFixed;
        /// <summary>The fuel tank is fitted.</summary>
        public bool FuelTankFitted => fuelTankFitted;
        /// <summary>Refuelling would pour something in: the fuel tank is fitted, the fuel is not full and some dark matter is held.</summary>
        public bool CanAddFuel => Running && fuelTankFitted && fuel < FuelTop && Dark > 0;

        /// <summary>Pours the dark matter held into the fuel, up to a full tank, in one press. False when nothing could go in.</summary>
        public bool TryAddFuel()
        {
            if (!CanAddFuel) return false;
            int amount = CampaignRules.FuelPoured(Dark, fuel, FuelTop);
            var cost = new CampaignCost(0, 0, amount);
            if (amount <= 0 || !TrySpend(cost)) return false;
            fuel += amount;
            RefreshShip();
            Bought("fuel", $"{fuel}/{FuelTop}", CampaignRoom.Ship, cost);
            return true;
        }

        /// <summary>Steps still between the player and the ignition: the engine, the hull, the fuel tank and a full tank, one each (0 to 4).
        /// At 0 CanIgnite holds, and Ignite starts the ignition (the failed one, then the Tyrant).</summary>
        public int HomeStepsLeft => CampaignRules.HomeSteps(engineFixed, armorFixed, fuelTankFitted, fuel, FuelTop);

        // ───── Mining lamps (the rocket page) ─────

        /// <summary>One mining lamp's price (CampaignRules.lampCost alloy).</summary>
        public CampaignCost LampPrice => new(rules ? rules.lampCost : 0);

        /// <summary>Crafts one mining lamp into the stock (LampStock). False when the stock does not pay for it.</summary>
        public bool TryCraftLamp()
        {
            var price = LampPrice;
            if (!LampsCraftable || !TrySpend(price)) return false;
            lampStock++;
            Bought("lamp", $"stock {lampStock}", CampaignRoom.Ship, price);
            return true;
        }
    }
}
