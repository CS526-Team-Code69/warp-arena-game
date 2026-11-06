using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine.Networking;
#endif

namespace AnomalyArena
{
                 
                                                                                                                             
                                                                                                                               
                                                                                                                               
                                                                                                                             
                                                                                                                                   
                                                                                                                      
                  
    public static class RunAnalytics
    {
                                                                                           
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
        }

        public const int DamageRowsPerEvent = 16;
        public const string SupplyTakeFree = "take_free", SupplyMerge = "merge", SupplyReplace = "replace", SupplySkip = "skip", SupplyTimeout = "timeout";
        const string Edition = "en";

        [Serializable]
        class Settings
        {
            public string endpoint = "";
        }

                                                                                                                                           
        public static Action<string> Transport;

        static readonly string SessionId = NewId();
        static string runId = "", startingWeapon = "";
        static bool runOpen;
        static string[] supplyOffers; // the open supply drop's offers, null when none is waiting for a choice
        static int supplyCancels;
        static readonly Dictionary<(int wave, string stage, string source, bool dot), DamageRow> damage = new();

        public static string NewId() => Guid.NewGuid().ToString("N");

                                                                                                                      
        public static void RunStarted(GameManager game)
        {
            if (!game || game.waves && game.waves.labMode) return;
            runId = NewId();
            runOpen = true;
            supplyOffers = null;
            damage.Clear();
            var first = game.runWeapons ? game.runWeapons.Hand[0] : null;
            startingWeapon = first != null ? first.form.ToString() : "";
            Send(Build(game, "run_start", StageOf(game.RunPhase)));
        }

                                                                                                                              
                                                                                                                                      
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
            Send(e);
        }

                                                                                                                                     
                                                                                                                                        
                                                                                                                                          
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
                wave = waves ? Mathf.Clamp(waves.WaveIndex + 1, 1, Mathf.Max(1, waves.WaveCount)) : 1,
                run_seconds = Math.Round(game.Elapsed, 1),
                level = game.progression ? game.progression.Level : 1,
                kills = waves ? waves.Kills : 0,
                starting_weapon = startingWeapon,
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
            var key = (wave, stage, source, dot);
            if (!damage.TryGetValue(key, out var row)) damage[key] = row = new DamageRow { source = source, dot = dot, stage = stage, wave = wave };
            float maxHp = player ? Mathf.Max(1f, player.maxHp) : 1f;
            row.damage += amount;
            row.damage_pct += amount / maxHp * 100.0;
            row.hits++;
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
                var e = new DamageEvent { @event = "wave_damage", rows = chunk.ToArray() };
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
            var waves = game.waves;
            return waves ? Mathf.Clamp(waves.WaveIndex + 1, 1, Mathf.Max(1, waves.WaveCount)) : 1;
        }

        static string BuildLabel()
        {
            string guid = Application.buildGUID;
            return string.IsNullOrEmpty(guid) ? Application.version : Application.version + "+" + guid.Substring(0, Mathf.Min(8, guid.Length));
        }

                                                                                                                                             
                                                                                                                           
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
