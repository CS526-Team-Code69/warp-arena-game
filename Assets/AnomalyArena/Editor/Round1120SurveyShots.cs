using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// Screenshots of the Beta survey's fixes (2026-11-20), taken in the editor's Play mode at the title (a fresh domain: RequestScriptReload
    /// while stopped, then Play). An open run: its first weapon card, a hit from the player's own weapon (the dock's red flash and the banner
    /// naming the weapon), a Spitter met for the first time (its name over the bar and the notice), the black hole's first sight (the open
    /// mode's warning) and its pull (the hint plate naming the sprint key), then a defeat by the Spitter (the result screen's first line says
    /// what ended the run). The Spitter's collection entry is put back as it was. Saved under work/gold-1117/captures-1120/survey with a log.
    /// </summary>
    static class Round1120SurveyShots
    {
        const string Tag = "[R1120Survey] ";
        static string folder, logPath;

        [MenuItem("Anomaly Arena/Checks/Round 11-20 survey shots")]
        static async void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogError(Tag + "enter Play and wait for the title first"); return; }
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../work/gold-1117/captures-1120/survey"));
            Directory.CreateDirectory(folder);
            logPath = Path.Combine(folder, "log.txt");
            File.WriteAllText(logPath, "");
            EditorApplication.LockReloadAssemblies();
            string spitterKey = ArenaCodexProgress.Key("enemy", (int)EnemyKind.Spitter);
            string spitterWas = ArenaPrefs.GetString(spitterKey);
            try
            {
                Note("started " + DateTime.Now.ToString("s"));
                await Shots(spitterKey);
                Note("END ok");
            }
            catch (Exception e) { Note("END error: " + e); }
            finally
            {
                if (string.IsNullOrEmpty(spitterWas)) ArenaPrefs.Delete(spitterKey);
                else ArenaPrefs.SetString(spitterKey, spitterWas);
                ArenaCodexProgress.ForgetCache();
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        static async Task Shots(string spitterKey)
        {
            var gm = GameManager.Instance;
            var world = WorldMap.Active;
            await Until(() => gm && world && ArenaText.Ready, 30f, "the game and its texts");
            await Command(gm, ArenaUiCommand.Start, () => gm.RunPhase == ArenaUiState.SupplyDrop || gm.RunPhase == ArenaUiState.Loadout);
            if (gm.RunPhase == ArenaUiState.SupplyDrop)
            {
                await Until(() => gm.SupplyDropReady || gm.RunPhase != ArenaUiState.SupplyDrop, 6f, "the crate lands");
                await Command(gm, ArenaUiCommand.OpenSupply, () => gm.RunPhase == ArenaUiState.Loadout);
            }

            await Command(gm, ArenaUiCommand.ChooseLoadout, () => gm.RunPhase == ArenaUiState.WeaponIntro, 1);
            await Seconds(.8f);
            await Shot("01-weapon-card");
            await Command(gm, ArenaUiCommand.DismissWeaponIntro, () => gm.RunPhase != ArenaUiState.WeaponIntro, -1, 6f);
            await Until(() => gm.RunPhase == ArenaUiState.WaveActive || gm.RunPhase == ArenaUiState.Cutscene, 20f, "the first wave");
            var player = gm.player;
            player.Invulnerable = true;
            Clear();

            // The player's own weapon: the dock's slot flashes red and a banner names the weapon.
            var weapon = gm.runWeapons.Hand[0];
            await Seconds(.5f);
            var hit = DamageInfo.Attack(2f, player);
            hit.weapon = weapon;
            player.ReceiveDamage(hit);
            await Seconds(.12f);
            await Shot("02-self-hit");
            Note($"self-hit by {weapon?.Label}: notices shown {gm.SelfHitNoticesShown(weapon.form, weapon.variant)}");
            await Seconds(4.8f);

            // A Spitter the collection has never met, set down beside the player: its name over its bar and its notice.
            ArenaPrefs.Delete(spitterKey);
            ArenaCodexProgress.ForgetCache();
            Clear();
            Vector3 side = Query.Flat(gm.cam.transform.right).normalized;
            var spitter = gm.waves.SpawnLab(EnemyKind.Spitter, player.Position + side * 4f, false, ElementKind.None);
            string notice = V2WorldLabels.FirstSightNotice(EnemyKind.Spitter);
            await Until(() => V2WorldLabels.LastShown == notice, 12f, "the Spitter's first-sight notice");
            await Seconds(.6f);
            await Shot("03-first-sight-spitter");
            Note($"first sight: named {V2WorldLabels.NamedForFirstSight(EnemyKind.Spitter)}, notice \"{notice}\"");

            // The black hole: its first sight in the open mode ends with a warning, and its pull puts the sprint key on the hint plate.
            var hole = world.holes.FirstOrDefault(h => h && h.isActiveAndEnabled && h.Phase == HolePhase.Active && h.GravityRadius > h.KillRadius + .5f);
            if (!hole) throw new Exception("no active black hole on this map");
            Vector3 home = player.Position;
            if (!world.HoleIntroDone)
            {
                Teleport(player, hole.Centre + Vector3.left * (hole.GravityRadius + 4f));
                float until = Time.realtimeSinceStartup + 8f;
                while (!world.HoleIntroPlaying && !world.HoleIntroDone && Time.realtimeSinceStartup < until) await Frame();
                if (world.HoleIntroPlaying)
                {
                    await Seconds(1.5f);
                    world.SkipHoleIntro();
                }

                await Seconds(1.2f);
                await Shot("04-hole-warning");
                Note($"hole warning: last shown \"{V2WorldLabels.LastShown}\"");
            }
            else Note("the hole's first sight had already played: no warning shot");

            Vector3 ring = hole.Centre + Vector3.left * ((hole.KillRadius + hole.GravityRadius) * .5f);
            Note($"before the pull: phase {gm.RunPhase}, waves {gm.waves.Phase}, combat {gm.IsCombatActive}, can move {gm.CanPlayerMove}, " +
                 $"feeds {hole.Feeds}, on screen {SkillWorldAPI.IsOnScreen?.Invoke(hole.Centre)}, intro done {world.HoleIntroDone}, pull at the ring {world.GetPull(ring, player).magnitude:0.0}");
            float pullUntil = Time.realtimeSinceStartup + 1.6f;
            while (Time.realtimeSinceStartup < pullUntil)
            {
                Place(player, ring);
                await Frame();
            }

            Place(player, ring);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "05-hole-pull.png"));
            var s = gm.UiSnapshot;
            Note($"capture 05-hole-pull: hint \"{s.hintLead}[{s.hintKey}]{s.hintTail}\"");
            await Frame();
            Place(player, ring);
            await Frame();
            Place(player, home);
            await Seconds(.5f);

            // A defeat by the Spitter: the result screen's first line says what ended the run.
            Clear(spitter);
            if (spitter && spitter.IsAlive) Teleport(spitter, player.Position + side * 4f);
            player.Invulnerable = false;
            typeof(Combatant).GetField("protectTimer", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(player, 0f);
            player.ReceiveDamage(DamageInfo.Attack(player.maxHp * 10f, spitter));
            await Until(() => gm.RunPhase == ArenaUiState.Result, 12f, "the result screen");
            await Seconds(2f);
            await Shot("06-result-cause");
        }

        /// <summary>The open mode's first sight of a black hole, step for step as HoleIntroTest takes it (the player protected for 30 s, set
        /// down just outside a ring), with what the cutscene asks noted every quarter second for six seconds.</summary>
        [MenuItem("Anomaly Arena/Checks/Round 11-20 hole intro probe")]
        static async void HoleProbe()
        {
            if (!EditorApplication.isPlaying) { Debug.LogError(Tag + "enter Play and wait for the title first"); return; }
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../work/gold-1117/captures-1120/survey"));
            Directory.CreateDirectory(folder);
            logPath = Path.Combine(folder, "hole-probe.txt");
            File.WriteAllText(logPath, "");
            EditorApplication.LockReloadAssemblies();
            try
            {
                var gm = GameManager.Instance;
                var map = WorldMap.Active;
                await Until(() => gm && map && ArenaText.Ready, 30f, "the game and its texts");
                await Command(gm, ArenaUiCommand.Start, () => gm.RunPhase == ArenaUiState.SupplyDrop);
                await Until(() => gm.SupplyDropReady, 6f, "the crate lands");
                await Command(gm, ArenaUiCommand.OpenSupply, () => gm.RunPhase == ArenaUiState.Loadout);
                await Command(gm, ArenaUiCommand.ChooseLoadout, () => gm.RunPhase == ArenaUiState.WeaponIntro, 1);
                await Command(gm, ArenaUiCommand.DismissWeaponIntro, () => gm.RunPhase != ArenaUiState.WeaponIntro, -1, 6f);
                gm.player.ProtectFor(30f);
                await Until(() => gm.RunPhase == ArenaUiState.WaveActive || gm.RunPhase == ArenaUiState.Cutscene, 10f, "the first wave");
                var hole = map.holes.FirstOrDefault(h => h && h.Phase == HolePhase.Active);
                Note($"holes {map.holes.Length}; chosen {(hole ? hole.name : "none")} at {(hole ? hole.Centre : Vector3.zero)}; phase {gm.RunPhase}");
                if (hole && gm.RunPhase != ArenaUiState.Cutscene)
                {
                    Vector3 away = Query.Flat(gm.player.Position - hole.Centre);
                    away = away.sqrMagnitude > .01f ? away.normalized : Vector3.forward;
                    Teleport(gm.player, hole.Centre + away * (hole.GravityRadius + 3f));
                }

                for (int i = 0; i < 24 && gm.RunPhase != ArenaUiState.Cutscene; i++)
                {
                    string holes = string.Join("; ", map.holes.Where(h => h).Select(h =>
                        $"{h.name} {h.Phase} feeds {h.Feeds} on screen {SkillWorldAPI.IsOnScreen?.Invoke(h.Centre)} dist {Query.Flat(h.Centre - gm.player.Position).magnitude:0.0} active {h.isActiveAndEnabled}"));
                    Note($"phase {gm.RunPhase}, waves {gm.waves.Phase}, combat {gm.IsCombatActive}, trial {map.TrialActive}, campaign map {map.CampaignMapApplied}, " +
                         $"intro playing {map.HoleIntroPlaying} done {map.HoleIntroDone}, time scale {Time.timeScale} | {holes}");
                    await Seconds(.25f);
                }

                Note($"END phase {gm.RunPhase}, intro playing {map.HoleIntroPlaying} done {map.HoleIntroDone}");
            }
            catch (Exception e) { Note("END error: " + e); }
            finally { EditorApplication.UnlockReloadAssemblies(); }
        }

        static void Clear(Enemy keep = null)
        {
            foreach (var e in GameManager.Instance.waves.Alive.ToArray())
                if (e && e != keep && e.IsAlive && !e.IsBoss) e.DiscardWithoutReward();
        }

        static void Place(Combatant body, Vector3 at)
        {
            body.CancelDisplacement();
            var rb = body.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.position = at;
                rb.linearVelocity = Vector3.zero;
            }

            body.transform.position = at;
            Physics.SyncTransforms();
        }

        static void Teleport(Combatant body, Vector3 spot)
        {
            spot.y = 0f;
            Place(body, spot);
        }

        /// <summary>The Game view with its interface, written at the end of this frame; the next frame starts after it.</summary>
        static async Task Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            Note("capture " + name);
            await Frame();
            await Frame();
        }

        static void Note(string line)
        {
            string text = $"{Time.realtimeSinceStartup,8:0.0}s {line}";
            Debug.Log(Tag + text);
            if (logPath != null) File.AppendAllText(logPath, text + Environment.NewLine);
        }

        static async Task Frame() => await Awaitable.NextFrameAsync();

        static async Task Seconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) await Frame();
        }

        static async Task Until(Func<bool> done, float timeout, string what)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("timed out waiting for " + what);
                await Frame();
            }
        }

        static async Task Command(GameManager gm, ArenaUiCommand command, Func<bool> done, int index = -1, float timeout = 12f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception(command + " timed out in " + gm.RunPhase);
                gm.ExecuteUiCommand(command, index);
                await Frame();
            }
        }
    }
}
