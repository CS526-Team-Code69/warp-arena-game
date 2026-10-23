using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Collections.Generic;
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

        [Serializable]
        class Settings
        {
            public string endpoint = "";
        }

                                                                                                                                           
        public static Action<string> Transport;

        static readonly string SessionId = NewId();
        static string runId = "", startingWeapon = "";
        static bool runOpen;

        public static string NewId() => Guid.NewGuid().ToString("N");

                                                                                                                      
        public static void RunStarted(GameManager game)
        {
            if (!game || game.waves && game.waves.labMode) return;
            runId = NewId();
            runOpen = true;
            var first = game.runWeapons ? game.runWeapons.Hand[0] : null;
            startingWeapon = first != null ? first.form.ToString() : "";
            Send(Build(game, "run_start", StageOf(game.RunPhase)));
        }

                                                                                                                              
                                                                                                                                      
        public static void RunEnded(GameManager game, ArenaUiState phase, string abandonedCause = null)
        {
            if (!runOpen || !game || game.waves && game.waves.labMode) return;
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
                edition = "en",
                build = BuildLabel(),
                stage = stage,
                wave = waves ? Mathf.Clamp(waves.WaveIndex + 1, 1, Mathf.Max(1, waves.WaveCount)) : 1,
                run_seconds = Math.Round(game.Elapsed, 1),
                level = game.progression ? game.progression.Level : 1,
                kills = waves ? waves.Kills : 0,
                starting_weapon = startingWeapon,
            };
        }

        static string BuildLabel()
        {
            string guid = Application.buildGUID;
            return string.IsNullOrEmpty(guid) ? Application.version : Application.version + "+" + guid.Substring(0, Mathf.Min(8, guid.Length));
        }

        static void Send(RunEvent e)
        {
            string json = JsonUtility.ToJson(e);
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
                if (waiting.Count < 8) waiting.Add(json);
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
                endpoint = url.StartsWith("https://", StringComparison.Ordinal) ? url : "";
                foreach (var json in waiting) Post(json);
                waiting.Clear();
            };
        }
#endif
    }
}
