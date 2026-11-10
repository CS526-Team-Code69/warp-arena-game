using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine.Networking;
#endif

namespace AnomalyArena
{
    /// <summary>
    /// GDD Metric 2, "How runs end": one row when a run starts and one when it ends, sent from the WebGL build to the team's
    /// Google Sheet (Analytics/apps-script/Code.gs appends them; Analytics/plot_run_endings.py charts them). A run that starts
    /// and never ends was left by closing the tab. Nothing is sent from the editor or from tests unless a test sets Transport.
    /// The endpoint is read from StreamingAssets/analytics.json, so the published site can switch it on or move it without a
    /// rebuild; an empty endpoint turns sending off, and a page opened from this machine (IsLocalPage) counts as empty, so a local
    /// check of a build never reaches the sheet. Only random ids are sent, never anything that identifies the player.
    /// </summary>
    public static class RunAnalytics
    {
        /// <summary>One sheet row; the field names are the sheet's column names.</summary>
        [Serializable]
        public class RunEvent
        {
            public int schema = 1;
            public string @event = "";
            public string run_id = "";
            public string session_id = "";
            public string edition = "";
            public string build = "";
            public string outcome = "";
            public string cause = "";
            public string enemy = "";
            public bool elite;
            public string stage = "wave";
            public int wave = 1;
            public double run_seconds;
            public int level = 1;
            public int kills;
            public string starting_weapon = "";
            public string mode = ModeOpen;
        }

        // Beta metrics. Every beta event starts with the same six fields as the "runs" sheet, so its rows join "runs" on run_id.
        [Serializable]
        public class BetaEvent
        {
            public int schema = 1;
            public string @event = "";
            public string run_id = "";
            public string session_id = "";
            public string edition = "";
            public string build = "";
        }

        // One card picked from a level-up draft, mid-wave ("levelup") or in the build phase ("build"). Sheet "upgrade_picks".
        [Serializable]
        public class UpgradePickEvent : BetaEvent
        {
            public string context = "";
            public int wave = 1;
            public int level = 1;
            public string offer_1 = "";
            public string offer_2 = "";
            public string offer_3 = "";
            public string picked = "";
            public bool rerolled;
        }

        // What the player did with one supply drop (the armory's three weapons). Sheet "supply_picks".
        [Serializable]
        public class SupplyPickEvent : BetaEvent
        {
            public int wave = 1;
            public int level = 1;
            public string offer_1 = "";
            public string offer_2 = "";
            public string offer_3 = "";
            public string action = "";
            public string picked = "";
            public string replaced = "";
            public int replace_cancels;
        }

        // Damage the player took from one source in one wave and stage. Sheet "damage", one row per entry.
        [Serializable]
        public class DamageRow
        {
            public string source = "";
            public bool dot;
            public string stage = "wave";
            public int wave = 1;
            public double damage;
            public double damage_pct;
            public int hits;
        }

        [Serializable]
        public class DamageEvent : BetaEvent
        {
            public DamageRow[] rows = Array.Empty<DamageRow>();
            public string mode = ModeOpen;
        }

        public const int DamageRowsPerEvent = 16;
        /// <summary>The "mode" of the run_start, run_end and wave_damage rows: a V2 campaign run is the story mode, any other run the open
        /// mode. The sheet keeps the two apart, so a story day never counts as an open-mode wave.</summary>
        public const string ModeOpen = "open", ModeStory = "story";
        public const string SupplyTakeFree = "take_free", SupplyMerge = "merge", SupplyReplace = "replace", SupplySkip = "skip", SupplyTimeout = "timeout";
        static string Edition => ArenaText.IsChinese ? "zh" : "en";

        [Serializable]
        class Settings
        {
            public string endpoint = "";
        }

        /// <summary>Tests replace the network with this: when set, it receives every event's JSON, also outside the WebGL build.</summary>
        public static Action<string> Transport;

        static readonly string SessionId = NewId();
        static string runId = "", startingWeapon = "";
        static bool runOpen;
        static string[] supplyOffers; // the open supply drop's offers, null when none is waiting for a choice
        static int supplyCancels;
        static readonly Dictionary<(int wave, string stage, string source, bool dot), DamageRow> damage = new();

        public static string NewId() => Guid.NewGuid().ToString("N");

        /// <summary>The first wave of a run is about to start (the starting weapon's card was just closed).</summary>
        public static void RunStarted(GameManager game)
        {
            if (!game || game.waves && game.waves.labMode) return;
            runId = NewId();
            runOpen = true;
            supplyOffers = null;
            damage.Clear();
            milestonesSent.Clear();
            milestoneRows = 0;
            // The GameManager is the same in both modes, so a campaign mark left by a run that never ended must not carry over. The mark
            // is set before the row is built: run_start reads its mode and stage from it.
            campaignGame = null;
            if (game.CampaignRunning) StartCampaign(game, 0);
            var first = game.runWeapons ? game.runWeapons.Hand[0] : null;
            startingWeapon = first != null ? first.form.ToString() : "";
            Send(Build(game, "run_start", StageOf(game.RunPhase)));
        }

        /// <summary>The run just ended: won, lost, or left from the pause menu (abandonedCause "restart" or "quit_to_title").
        /// phase is where the run was at that moment (for a paused run, the phase under the pause menu). Sent once per run.</summary>
        public static void RunEnded(GameManager game, ArenaUiState phase, string abandonedCause = null)
        {
            if (!runOpen || !game || game.waves && game.waves.labMode) return;
            FlushDamage(game);
            supplyOffers = null;
            runOpen = false;
            var e = Build(game, "run_end", StageOf(phase));
            if (abandonedCause != null)
            {
                e.outcome = "abandoned";
                e.cause = abandonedCause;
            }
            else if (game.State == GameState.Won)
            {
                e.outcome = "victory";
                e.cause = "victory";
            }
            else
            {
                var player = game.player;
                var by = player ? player.LastDamagedBy : null;
                var enemy = by as Enemy;
                bool known = enemy && enemy.definition;
                e.outcome = "defeat";
                (e.cause, e.enemy, e.elite) = DefeatCause(phase, player ? player.DeathCause : DeathCause.None,
                    player ? player.LastDamageSource : DamageSource.Hit, by && by == player,
                    known ? enemy.definition.kind : (EnemyKind?)null, known && enemy.Elite);
            }
            if (Campaigning)
            {
                CampaignRunEnd(game, e);
                SendLastDay(game, e); // the day that never reached dawn carries the run's end, so it is sent once that is known
            }
            Send(e);
            campaignGame = null;
        }

        /// <summary>Why a lost run ended, checked in this order: died in the void trial; swallowed by a black hole; the player's own
        /// weapon; a wall slam; burn or poison; the Tyrant; any other enemy. The enemy kind is kept when an enemy dealt the last damage
        /// (in the void trial only the Hollow, since the wave outside is frozen and an older hit says nothing about the trial).</summary>
        public static (string cause, string enemy, bool elite) DefeatCause(ArenaUiState phase, DeathCause death, DamageSource source,
            bool bySelf, EnemyKind? byEnemy, bool byElite)
        {
            string kind = byEnemy?.ToString() ?? "";
            if (phase == ArenaUiState.VoidTrial) return byEnemy == EnemyKind.Hollow ? ("void_trial", kind, false) : ("void_trial", "", false);
            if (death == DeathCause.FellIntoGap) return ("black_hole", "", false);
            if (bySelf) return ("backfire", "", false);
            if (source == DamageSource.Wall) return ("wall_slam", "", false);
            if (source is DamageSource.Burn or DamageSource.Poison) return ("status", kind, byElite);
            if (byEnemy == EnemyKind.Tyrant) return ("tyrant", kind, byElite);
            if (byEnemy != null) return ("enemy", kind, byElite);
            return ("unknown", "", false);
        }

        public static string StageOf(ArenaUiState phase) => phase switch
        {
            ArenaUiState.VoidTrial => "void_trial",
            ArenaUiState.FinalBoss => "final_boss",
            _ when Campaigning => campaignStage,
            ArenaUiState.WaveIntro or ArenaUiState.WaveActive or ArenaUiState.LevelUp or ArenaUiState.TrialReturn
                or ArenaUiState.Cutscene or ArenaUiState.WeaponIntro or ArenaUiState.Paused => "wave",
            _ => "build",
        };

        static RunEvent Build(GameManager game, string kind, string stage)
        {
            var waves = game.waves;
            return new RunEvent
            {
                @event = kind,
                run_id = runId,
                session_id = SessionId,
                edition = Edition,
                build = BuildLabel(),
                stage = stage,
                wave = WaveOf(game),
                run_seconds = Math.Round(game.Elapsed, 1),
                level = game.progression ? game.progression.Level : 1,
                kills = waves ? waves.Kills : 0,
                starting_weapon = startingWeapon,
                mode = Mode,
            };
        }

        // ---------------------------------------------------------------- Beta metric 1: upgrade picks at level-up

        // Language-free card id: "Weapon:<form>:<direction>", "Skill:<id>" or "Stat:<stat>".
        public static string CardId(DraftCard card)
        {
            if (card == null) return "";
            return card.Payload switch
            {
                WeaponUpgradeOffer w => "Weapon:" + (w.Weapon != null ? w.Weapon.form.ToString() : "") + ":" + w.Axis,
                SkillId s => "Skill:" + s,
                StatCardOffer o => "Stat:" + o.Stat,
                _ => card.Kind + ":",
            };
        }

        // The three offered ids, read before the pick changes the draft.
        public static string[] CardIds(DraftCard[] cards)
        {
            var ids = new string[3];
            for (int i = 0; i < ids.Length; i++) ids[i] = cards != null && i < cards.Length ? CardId(cards[i]) : "";
            return ids;
        }

        public static void UpgradePicked(GameManager game, bool atLevelUp, string[] offers, int index, bool rerolled)
        {
            if (Campaigning) return;
            if (!runOpen || !game || offers == null || offers.Length < 3 || index < 0 || index >= offers.Length || string.IsNullOrEmpty(offers[index])) return;
            var e = new UpgradePickEvent
            {
                @event = "upgrade_pick",
                context = atLevelUp ? "levelup" : "build",
                wave = WaveOf(game),
                level = game.progression ? game.progression.Level : 1,
                offer_1 = offers[0],
                offer_2 = offers[1],
                offer_3 = offers[2],
                picked = offers[index],
                rerolled = rerolled,
            };
            Stamp(e);
            Send(JsonUtility.ToJson(e));
        }

        // ---------------------------------------------------------------- Beta metric 2: supply drop choices

        // "<form>" for a new weapon, "<form>:upgrade" for a higher Mk of a weapon the player already has.
        public static string OfferId(RunWeapon w) => w == null ? "" : w.form + (w.upgradeOf != null ? ":upgrade" : "");

        // The armory was rolled for this build phase; remember its offers until the player decides.
        public static void SupplyOpened(RunWeapon[] offers)
        {
            supplyOffers = null;
            supplyCancels = 0;
            if (Campaigning) return;
            if (!runOpen || offers == null) return;
            var ids = new string[3];
            bool any = false;
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = i < offers.Length ? OfferId(offers[i]) : "";
                any |= ids[i].Length > 0;
            }
            if (any) supplyOffers = ids;
        }

        public static void SupplyReplaceCancelled()
        {
            if (supplyOffers != null) supplyCancels++;
        }

        // Sends one row for the open supply drop; later calls for the same drop do nothing.
        public static void SupplyResolved(GameManager game, string action, RunWeapon picked = null, RunWeapon replaced = null)
        {
            var offers = supplyOffers;
            supplyOffers = null;
            if (offers == null || !runOpen || !game) return;
            bool takes = action is SupplyTakeFree or SupplyMerge or SupplyReplace;
            var e = new SupplyPickEvent
            {
                @event = "supply_pick",
                wave = WaveOf(game),
                level = game.progression ? game.progression.Level : 1,
                offer_1 = offers[0],
                offer_2 = offers[1],
                offer_3 = offers[2],
                action = action,
                picked = takes && picked != null ? picked.form.ToString() : "",
                replaced = action == SupplyReplace && replaced != null ? replaced.form.ToString() : "",
                replace_cancels = supplyCancels,
            };
            Stamp(e);
            Send(JsonUtility.ToJson(e));
        }

        // ---------------------------------------------------------------- Beta metric 3: damage sources

        // Burn and poison count for whoever applied them; wall slams are their own source, as in the death causes.
        public static string DamageSourceOf(DamageSource source, bool bySelf, EnemyKind? byEnemy, bool byElite)
        {
            if (source == DamageSource.Wall) return "wall_slam";
            if (bySelf) return "backfire";
            if (byEnemy != null) return byElite ? byEnemy + "_elite" : byEnemy.ToString();
            return "other";
        }

        // Called for every hit, burn or poison tick and wall slam that took HP or shield from the player.
        public static void PlayerDamaged(GameManager game, DamageInfo info, float amount)
        {
            if (!runOpen || !game || amount <= 0f) return;
            var player = game.player;
            var owner = info.dealer?.Owner;
            var enemy = owner as Enemy;
            bool known = enemy && enemy.definition;
            string source = DamageSourceOf(info.source, player && owner == player, known ? enemy.definition.kind : (EnemyKind?)null, known && enemy.Elite);
            bool dot = info.source is DamageSource.Burn or DamageSource.Poison;
            string stage = StageOf(game.RunPhase);
            int wave = WaveOf(game);
            if (Campaigning) source = CampaignDamageSource(source, info.source, stage);
            var key = (wave, stage, source, dot);
            if (!damage.TryGetValue(key, out var row)) damage[key] = row = new DamageRow { source = source, dot = dot, stage = stage, wave = wave };
            float maxHp = player ? Mathf.Max(1f, player.maxHp) : 1f;
            row.damage += amount;
            row.damage_pct += amount / maxHp * 100.0;
            row.hits++;
            if (Campaigning) CountPlayerDamage(amount, amount / maxHp * 100.0);
        }

        // Sends what was collected since the last flush: at the end of every wave and when the run ends.
        public static void FlushDamage(GameManager game)
        {
            if (damage.Count == 0) return;
            var rows = new List<DamageRow>(damage.Values);
            damage.Clear();
            if (!game) return;
            for (int i = 0; i < rows.Count; i += DamageRowsPerEvent)
            {
                var chunk = rows.GetRange(i, Math.Min(DamageRowsPerEvent, rows.Count - i));
                foreach (var r in chunk)
                {
                    r.damage = Math.Round(r.damage, 1);
                    r.damage_pct = Math.Round(r.damage_pct, 1);
                }
                var e = new DamageEvent { @event = "wave_damage", rows = chunk.ToArray(), mode = Mode };
                Stamp(e);
                Send(JsonUtility.ToJson(e));
            }
        }

        static void Stamp(BetaEvent e)
        {
            e.run_id = runId;
            e.session_id = SessionId;
            e.edition = Edition;
            e.build = BuildLabel();
        }

        static int WaveOf(GameManager game)
        {
            if (Campaigning && game == campaignGame) return campaignDay;
            var waves = game.waves;
            return waves ? Mathf.Clamp(waves.WaveIndex + 1, 1, Mathf.Max(1, waves.WaveCount)) : 1;
        }

        // ---------------------------------------------------------------- V2 day campaign

        /// <summary>V2 campaign, one row per day (sheet "v2_days"): sent at dawn for the day that just ended, counted from the dawn
        /// before (so a dawn's base visit and purchases belong to the coming day), and once more when the run ends, for the day that
        /// never reached dawn. repairs counts the day's purchases of the item "repair". end_stage and end_cause are empty on a day that
        /// reached dawn; on the run's last day they are the run_end row's stage and cause (where and how the run ended).</summary>
        [Serializable]
        public class V2DayEvent : BetaEvent
        {
            public int day;
            public bool overtime;
            public bool reached_dawn;
            public string end_stage = "";
            public string end_cause = "";
            public int kills;
            public double player_damage;
            public double player_damage_pct;
            public int player_hits;
            public double base_damage;
            public double base_damage_hp;
            public double base_hp;
            public double base_shield;
            public double base_max_hp;
            public int earned_alloy;
            public int earned_energy;
            public int earned_dark;
            public int spent_alloy;
            public int spent_energy;
            public int spent_dark;
            public int alloy;
            public int energy;
            public int dark;
            public bool night_death;
            public int lost_alloy;
            public int lost_energy;
            public int lost_dark;
            public int shadows;
            public int overrun_regions;
            public int repairs;
        }

        /// <summary>V2 campaign, one row per purchase at the base or at its door (sheet "v2_purchases"), sent by CampaignEvents.Purchased:
        /// what was bought (item), which one (detail), the room (Control, Armament, Energy, Ship) and the price; day is the v2_days row it
        /// counts in.</summary>
        [Serializable]
        public class V2PurchaseEvent : BetaEvent
        {
            public int day;
            public string stage = "";
            public string item = "";
            public string detail = "";
            public string room = "";
            public int alloy;
            public int energy;
            public int dark;
        }

        /// <summary>V2 campaign, one row per moment worth knowing (sheet "v2_events"), sent by CampaignEvents.Milestone: its kind, which one
        /// (detail) and a number (value), as listed on CampaignEvents.Milestone; day is the v2_days row it counts in, as for a purchase.</summary>
        [Serializable]
        public class V2Event : BetaEvent
        {
            public int day;
            public string stage = "";
            public string kind = "";
            public string detail = "";
            public double value;
        }

        /// <summary>At most this many v2_event rows a run, so a record point that fires every frame by mistake cannot flood the sheet.</summary>
        public const int MilestoneRowsPerRun = 300;

        const string ShadowSource = "shadow", BaseDestroyedCause = "base_destroyed";
        static GameManager campaignGame; // the GameManager running a V2 campaign; null (or destroyed) during a classic run
        static int campaignDay;
        static string campaignStage = "tutorial";
        static V2DayEvent today; // the v2_days row being counted
        static int killsBefore;
        static CampaignCost earnedBefore;
        static readonly HashSet<string> milestonesSent = new(); // the once-only keys (MilestoneOnce) sent in this run
        static int milestoneRows;

        /// <summary>True while a V2 campaign run is on. A classic run raises no CampaignEvents and is not a campaign at its start, so
        /// this stays false and every row is exactly what the classic run sends.</summary>
        static bool Campaigning => campaignGame != null;

        /// <summary>The mode of the rows sent now. A run marked as a campaign only at its first campaign event (InCampaign) has already
        /// sent its run_start as "open"; its later rows say "story".</summary>
        static string Mode => Campaigning ? ModeStory : ModeOpen;

        /// <summary>Listens to the V2 campaign's record points once per play: statics outlive a play when entering play mode does not
        /// reload the scripts, so every handler is removed before it is added.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ListenToCampaign()
        {
            campaignGame = null;
            CampaignEvents.DayStarted -= OnDayStarted;
            CampaignEvents.DayStarted += OnDayStarted;
            CampaignEvents.PhaseChanged -= OnPhaseChanged;
            CampaignEvents.PhaseChanged += OnPhaseChanged;
            CampaignEvents.BaseDamaged -= OnBaseDamaged;
            CampaignEvents.BaseDamaged += OnBaseDamaged;
            CampaignEvents.NightDeath -= OnNightDeath;
            CampaignEvents.NightDeath += OnNightDeath;
            CampaignEvents.Purchased -= OnPurchased;
            CampaignEvents.Purchased += OnPurchased;
            CampaignEvents.Dawn -= OnDawn;
            CampaignEvents.Dawn += OnDawn;
            CampaignEvents.Milestone -= OnMilestone;
            CampaignEvents.Milestone += OnMilestone;
        }

        /// <summary>Marks the run of this GameManager as a campaign run: at run_start when the GameManager says a campaign is running
        /// (day 0), or at the first campaign event of an open run.</summary>
        static void StartCampaign(GameManager game, int day)
        {
            campaignGame = game;
            campaignDay = Mathf.Max(0, day);
            campaignStage = campaignDay == 0 ? "tutorial" : "day";
            today = new V2DayEvent { @event = "v2_day", day = campaignDay };
            killsBefore = game.waves ? game.waves.Kills : 0;
            earnedBefore = game.TryGetComponent(out V2Campaign campaign) ? new CampaignCost(campaign.EarnedAlloy, campaign.EarnedEnergy, campaign.EarnedDark) : default;
        }

        /// <summary>True when a campaign event belongs to the open run, which it then marks as a campaign run if nothing did yet.</summary>
        static bool InCampaign(int day)
        {
            if (Campaigning) return runOpen;
            var game = GameManager.Instance;
            if (!runOpen || !game) return false;
            StartCampaign(game, day);
            return true;
        }

        /// <summary>Sends the player's damage collected under the old day or stage, then moves on.</summary>
        static void MoveTo(int day, string stage)
        {
            day = Mathf.Max(0, day);
            if (stage == null) stage = campaignStage;
            if (day != campaignDay || stage != campaignStage) FlushDamage(campaignGame);
            campaignDay = day;
            campaignStage = stage;
        }

        static void OnDayStarted(int day, bool overtime)
        {
            if (!InCampaign(day)) return;
            MoveTo(day, "day");
            today.day = day;
            today.overtime = overtime;
        }

        static void OnPhaseChanged(string phase, int day)
        {
            if (InCampaign(day)) MoveTo(day, StageName(phase));
        }

        /// <summary>The "stage" sent for a phase name: trimmed and in lower case ("dusk", "night", "dawn"); "lull" counts as "day" and
        /// "build" as "dawn". Null for an empty name, which keeps the stage as it was.</summary>
        static string StageName(string phase)
        {
            if (string.IsNullOrWhiteSpace(phase)) return null;
            string name = phase.Trim().ToLowerInvariant();
            return name switch
            {
                "lull" => "day",
                "build" => "dawn",
                _ => name,
            };
        }

        /// <summary>Damage without a dealer at night is a shadow's touch (V2Campaign.HitPlayerAtNight); any other damage keeps its source.</summary>
        static string CampaignDamageSource(string source, DamageSource kind, string stage) =>
            source == "other" && kind == DamageSource.Hit && stage == "night" ? ShadowSource : source;

        /// <summary>The player's damage in a campaign run also counts in the day's row.</summary>
        static void CountPlayerDamage(float amount, double pct)
        {
            if (today == null) return;
            today.player_damage += amount;
            today.player_damage_pct += pct;
            today.player_hits++;
        }

        static void OnBaseDamaged(float amount, float toHp, CampaignRoom room)
        {
            if (amount <= 0f || !InCampaign(campaignDay)) return;
            today.base_damage += amount;
            today.base_damage_hp += Mathf.Max(0f, toHp);
        }

        static void OnNightDeath(CampaignCost lost, int day)
        {
            if (!InCampaign(day)) return;
            today.night_death = true;
            today.lost_alloy += lost.alloy;
            today.lost_energy += lost.energy;
            today.lost_dark += lost.dark;
        }

        /// <summary>One v2_purchase row, and its price in the day's spending. Its day is the v2_days row being counted (a dawn purchase
        /// belongs to the coming day), not the campaign's day number that comes with the event. The day's spending is counted here and
        /// not from CampaignEvents.Spent: a payment the campaign gives back (a weapon offer that could not be taken) raises Spent but
        /// no Purchased, so the day's spending is the sum of its v2_purchase rows.</summary>
        static void OnPurchased(string item, string detail, CampaignRoom room, CampaignCost cost, int day)
        {
            if (!InCampaign(campaignDay)) return;
            today.spent_alloy += cost.alloy;
            today.spent_energy += cost.energy;
            today.spent_dark += cost.dark;
            if (item == "repair") today.repairs++;
            var e = new V2PurchaseEvent { @event = "v2_purchase", day = today.day, stage = campaignStage, item = item ?? "", detail = detail ?? "",
                room = room.ToString(), alloy = cost.alloy, energy = cost.energy, dark = cost.dark };
            Stamp(e);
            Send(JsonUtility.ToJson(e));
        }

        /// <summary>The key under which a milestone is sent at most once a run, or null when every one is sent (crates, void purchases):
        /// a tutorial step once for each step, the Prophet and the void crystals once, and the black hole's check once a day for its start,
        /// once for its pass and once for any of its failures.</summary>
        public static string MilestoneOnce(string kind, string detail, int day) => kind switch
        {
            CampaignEvents.MilestoneTutorialStep => kind + ":" + detail,
            CampaignEvents.MilestoneProphet or CampaignEvents.MilestoneVoidCrystal => kind,
            CampaignEvents.MilestoneHoleCheck => kind + ":" + day + ":" + (detail != null && detail.StartsWith("failed", StringComparison.Ordinal) ? "failed" : detail),
            _ => null,
        };

        /// <summary>One v2_event row. Its day is the v2_days row being counted (a dawn purchase belongs to the coming day), as for
        /// v2_purchase; the campaign's own day number comes with the event and only marks the run as a campaign run if nothing did yet.</summary>
        static void OnMilestone(string kind, string detail, float value, int day)
        {
            if (string.IsNullOrEmpty(kind) || !InCampaign(day)) return;
            string once = MilestoneOnce(kind, detail, day);
            if (once != null && !milestonesSent.Add(once) || milestoneRows >= MilestoneRowsPerRun) return;
            milestoneRows++;
            var e = new V2Event { @event = "v2_event", day = today != null ? today.day : Mathf.Max(0, day), stage = campaignStage, kind = kind,
                detail = detail ?? "", value = float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Round(value, 2) };
            Stamp(e);
            Send(JsonUtility.ToJson(e));
        }

        static void OnDawn(int day, CampaignCost wallet, CampaignCost earned)
        {
            if (!InCampaign(day)) return;
            today.day = day;
            SendDay(campaignGame, true, wallet, earned);
            today = new V2DayEvent { @event = "v2_day", day = day + 1 };
        }

        /// <summary>The row of a day that never reached dawn, when the run ends: the wallet and the earnings come from the campaign, the
        /// stage and cause of its end from the run_end row.</summary>
        static void SendLastDay(GameManager game, RunEvent end)
        {
            if (today != null)
            {
                today.end_stage = end.stage;
                today.end_cause = end.cause;
            }
            CampaignCost wallet = default, earned = earnedBefore;
            if (game.TryGetComponent(out V2Campaign campaign))
            {
                wallet = new CampaignCost(campaign.Alloy, campaign.Energy, campaign.Dark);
                earned = new CampaignCost(campaign.EarnedAlloy, campaign.EarnedEnergy, campaign.EarnedDark);
            }
            SendDay(game, false, wallet, earned);
            today = null;
        }

        static void SendDay(GameManager game, bool dawn, CampaignCost wallet, CampaignCost earned)
        {
            var e = today;
            if (e == null || !game) return;
            e.reached_dawn = dawn;
            int kills = game.waves ? game.waves.Kills : 0;
            e.kills = Mathf.Max(0, kills - killsBefore);
            killsBefore = kills;
            e.earned_alloy = Mathf.Max(0, earned.alloy - earnedBefore.alloy);
            e.earned_energy = Mathf.Max(0, earned.energy - earnedBefore.energy);
            e.earned_dark = Mathf.Max(0, earned.dark - earnedBefore.dark);
            earnedBefore = earned;
            e.alloy = wallet.alloy;
            e.energy = wallet.energy;
            e.dark = wallet.dark;
            if (game.TryGetComponent(out V2Campaign campaign) && campaign.rules)
            {
                e.base_hp = Math.Round(campaign.BaseHp, 1);
                e.base_shield = Math.Round(campaign.BaseShield, 1);
                e.base_max_hp = Math.Round(campaign.BaseMaxHp, 1);
                e.shadows = campaign.ShadowCount;
                e.overrun_regions = campaign.OverrunRegions;
            }
            e.player_damage = Math.Round(e.player_damage, 1);
            e.player_damage_pct = Math.Round(e.player_damage_pct, 1);
            e.base_damage = Math.Round(e.base_damage, 1);
            e.base_damage_hp = Math.Round(e.base_damage_hp, 1);
            Stamp(e);
            Send(JsonUtility.ToJson(e));
        }

        /// <summary>The campaign's part of the run_end row. A won run ended in the ending; a run lost because the base fell has the cause
        /// "base_destroyed" instead of whatever hit the player last; a death by day keeps the cause found above (enemy, status, tyrant …),
        /// since the stage already says it happened by day.</summary>
        static void CampaignRunEnd(GameManager game, RunEvent e)
        {
            if (e.outcome == "victory") e.stage = "ending";
            else if (e.outcome == "defeat" && game.TryGetComponent(out V2Campaign campaign) && campaign.BaseDestroyed)
                (e.cause, e.enemy, e.elite) = (BaseDestroyedCause, "", false);
        }

        static string BuildLabel()
        {
            string guid = Application.buildGUID;
            return string.IsNullOrEmpty(guid) ? Application.version : Application.version + "+" + guid.Substring(0, Mathf.Min(8, guid.Length));
        }

        /// <summary>True for a page opened from this machine: localhost, 127.0.0.1, [::1], a host ending in .localhost or .test, or file://.
        /// An empty or unreadable address is not local, so a published page never stops reporting because of it.</summary>
        public static bool IsLocalPage(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var page)) return false;
            if (page.IsFile) return true;
            string host = page.Host.ToLowerInvariant();
            return host is "localhost" or "127.0.0.1" or "[::1]"
                || host.EndsWith(".localhost", StringComparison.Ordinal) || host.EndsWith(".test", StringComparison.Ordinal);
        }

        static void Send(RunEvent e) => Send(JsonUtility.ToJson(e));

        static void Send(string json)
        {
            if (Transport != null)
            {
                Transport(json);
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            Post(json);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int WarpAnalyticsSend(string url, string body);

        static string endpoint; // null until analytics.json is read; empty when sending is off
        static bool loading;
        static int failedReads;
        static readonly List<string> waiting = new();

        static void Post(string json)
        {
            if (endpoint == null)
            {
                if (waiting.Count < 32) waiting.Add(json);
                LoadEndpoint();
                return;
            }
            if (endpoint.Length == 0 || WarpAnalyticsSend(endpoint, json) != 0) return;
            // No sendBeacon: a text/plain POST needs no CORS preflight either, and the reply is not needed.
            var request = UnityWebRequest.Post(endpoint, json, "text/plain");
            request.SendWebRequest().completed += _ => request.Dispose();
        }

        static void LoadEndpoint()
        {
            if (loading) return;
            loading = true;
            var request = UnityWebRequest.Get(Application.streamingAssetsPath + "/analytics.json?t=" + DateTime.UtcNow.Ticks);
            request.SendWebRequest().completed += _ =>
            {
                string url = "";
                bool read = request.result == UnityWebRequest.Result.Success;
                if (read)
                {
                    try { url = (JsonUtility.FromJson<Settings>(request.downloadHandler.text)?.endpoint ?? "").Trim(); }
                    catch (ArgumentException) { }
                }
                // A missing file (404) is final; a dropped connection or a server error is tried again by the next event, up to 3 reads.
                bool final = read || request.responseCode == 404 || ++failedReads >= 3;
                request.Dispose();
                if (!final)
                {
                    loading = false;
                    return;
                }
                endpoint = url.StartsWith("https://", StringComparison.Ordinal) && !IsLocalPage(Application.absoluteURL) ? url : "";
                foreach (var json in waiting) Post(json);
                waiting.Clear();
            };
        }
#endif
    }
}
