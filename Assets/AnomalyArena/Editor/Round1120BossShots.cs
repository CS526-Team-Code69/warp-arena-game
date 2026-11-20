using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// Screenshots of the bosses, taken in the editor's Play mode for the round of 2026-11-20: enter Play, wait for the title, then pick a
    /// menu (a fresh domain for each: RequestScriptReload while stopped, then Play). The Tyrant: an open run is started up to its first wave
    /// (the character screen shot there, its parts still empty), the wave's monsters are sent away, and the enemy lab's Tyrant is put a few
    /// cells in front of the player, who cannot die. Each move is forced and shot at its strongest moment: the Tail Sweep half way across
    /// its fan, the Ground Pound's wave part way down its rectangle, the Rampage Charge's landing and the Star Throw's impact; then a hit
    /// past half its HP and its change of phase (gathering, a hit glancing off, the burst, phase 2) and a phase-2 pound. The Nash Prophet:
    /// the same run into the void trial, the boss bar with its phase lines, then its first change of phase. Saved under
    /// work/gold-1117/captures-1120/ in a folder named by the menu, with a log beside them; script reloads are held meanwhile.
    /// </summary>
    static class Round1120BossShots
    {
        const string Tag = "[R1120Boss] ";
        static string folder, logPath;

        [MenuItem("Anomaly Arena/Checks/Round 11-20 Tyrant shots")]
        static void After() => Launch("boss-after", TyrantRun);

        [MenuItem("Anomaly Arena/Checks/Round 11-20 Nash Prophet shots")]
        static void Nash() => Launch("boss-nash", NashRun);

        static async void Launch(string name, Func<Task> run)
        {
            if (!EditorApplication.isPlaying) { Debug.LogError(Tag + "enter Play and wait for the title first"); return; }
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../work/gold-1117/captures-1120/" + name));
            Directory.CreateDirectory(folder);
            logPath = Path.Combine(folder, "log.txt");
            File.WriteAllText(logPath, "");
            EditorApplication.LockReloadAssemblies();
            try
            {
                Note("started " + DateTime.Now.ToString("s"));
                await run();
                Note("END ok");
            }
            catch (Exception e) { Note("END error: " + e); }
            finally { EditorApplication.UnlockReloadAssemblies(); }
        }

        /// <summary>An open run up to its first wave, the player unable to die and every hole fed once (so no hole's teaching cutscene
        /// stops the world in the middle of a shot).</summary>
        static async Task ToFirstWave()
        {
            var gm = GameManager.Instance;
            var world = WorldMap.Active;
            await Until(() => ArenaText.Ready, 20f, "localization");
            await Command(gm, ArenaUiCommand.Start, () => gm.RunPhase == ArenaUiState.SupplyDrop || gm.RunPhase == ArenaUiState.Loadout);
            if (gm.RunPhase == ArenaUiState.SupplyDrop)
            {
                await Until(() => gm.SupplyDropReady || gm.RunPhase != ArenaUiState.SupplyDrop, 6f, "the crate lands");
                await Command(gm, ArenaUiCommand.OpenSupply, () => gm.RunPhase == ArenaUiState.Loadout);
            }
            await Command(gm, ArenaUiCommand.ChooseLoadout, () => gm.RunPhase == ArenaUiState.WeaponIntro, 1);
            await Command(gm, ArenaUiCommand.DismissWeaponIntro, () => gm.RunPhase != ArenaUiState.WeaponIntro);
            float deadline = Time.realtimeSinceStartup + 20f;
            while (gm.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("the first wave never started, phase " + gm.RunPhase);
                if (gm.RunPhase == ArenaUiState.Cutscene) world.SkipHoleIntro();
                await Frame();
            }
            gm.player.ProtectFor(900f);
            gm.player.Invulnerable = true;
            // Each hole fed once, so no hole's teaching cutscene stops the world in the middle of a move.
            world.SkipHoleIntro();
            foreach (var hole in world.holes)
                if (hole && hole.Phase == HolePhase.Active && hole.Feeds == 0) hole.Feed(1, gm.waves.WaveIndex + 1);
            Clear(null);
        }

        static async Task<Enemy> StartWithTyrant()
        {
            var gm = GameManager.Instance;
            await ToFirstWave();
            // The character screen at the start of a run: its five parts stand with their captions, the empty ones saying so.
            await Seconds(1f);
            if (V5CharacterScreen.Open())
            {
                await Seconds(1f);
                await Shot("character-empty-parts");
                V5CharacterScreen.Close();
                await Seconds(.6f);
            }
            else Note("the character screen did not open");
            var tyrant = gm.waves.SpawnLabTyrant(gm.player.Position + Vector3.forward * 6f);
            if (!tyrant) throw new Exception("no lab Tyrant in this scene");
            Physics.SyncTransforms();
            await Seconds(1.2f);
            Note($"tyrant at {tyrant.Position}, player at {gm.player.Position}, phase {tyrant.BossPhase}, hp {tyrant.Hp:0}/{tyrant.maxHp:0}");
            return tyrant;
        }

        static async Task TyrantRun()
        {
            var tyrant = await StartWithTyrant();
            await Move(tyrant, Enemy.TailSweepMove, () => tyrant.TailSweeping && tyrant.TailAngle > -12f, "tyrant-sweep");
            await Move(tyrant, Enemy.GroundPoundMove, () => tyrant.PoundStep >= 2, "tyrant-pound");
            bool leapt = false;
            await Move(tyrant, Enemy.RampageChargeMove, () => { if (tyrant.RushLeaping) leapt = true; return leapt && !tyrant.RushLeaping; }, "tyrant-charge-landing", .18f);
            bool thrown = false;
            await Move(tyrant, Enemy.StarThrowMove, () => { if (tyrant.MeteorFlying) thrown = true; return thrown && !tyrant.MeteorFlying; }, "tyrant-meteor-impact", .15f);
            await Move(tyrant, Enemy.TailSweepMove, () => tyrant.WindingUp, "tyrant-sweep-windup", .45f);

            // A hit past half its HP: it stops on the line and changes phase, the player close enough to be thrown back.
            var gm = GameManager.Instance;
            Clear(tyrant);
            await Until(() => !tyrant.WindingUp && !tyrant.TailSweeping && !tyrant.Charging && !tyrant.RushLeaping, 8f, "the Tyrant free before its change");
            Vector3 away = Query.Flat(gm.player.Position - tyrant.Position);
            if (away.sqrMagnitude < .01f) away = Vector3.back;
            Teleport(gm.player, tyrant.Position + away.normalized * (tyrant.radius + 2f));
            ElementStatus.For(tyrant)?.SetShield(0f);
            tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.maxHp * .8f, source = DamageSource.Wall, ignoreShield = true });
            Note($"after a hit of 80%: hp {tyrant.Hp:0}/{tyrant.maxHp:0} ({tyrant.Hp / tyrant.maxHp:0.000}), phase {tyrant.BossPhase}, changing {tyrant.PhaseShifting}");
            await PhaseShots(tyrant, "tyrant");
            await Move(tyrant, Enemy.GroundPoundMove, () => tyrant.PoundStep >= 2, "tyrant-pound-phase2");
            await Move(tyrant, Enemy.TailSweepMove, () => tyrant.TailSweeping && tyrant.TailAngle > -12f, "tyrant-sweep-phase2");
        }

        static async Task NashRun()
        {
            var gm = GameManager.Instance;
            var world = WorldMap.Active;
            await ToFirstWave();
            var hole = world.holes.First(h => h && h.Phase == HolePhase.Active);
            hole.Feed(hole.feedsToOpen, gm.waves.WaveIndex + 1);
            hole.Tick(2f);
            Teleport(gm.player, hole.Centre);
            if (!world.TryEnterPortal(gm.player)) throw new Exception("the open portal did not take the player");
            await Until(() => world.TrialActive && world.hollow && world.hollow.IsAlive, 10f, "the void trial");
            var hollow = world.hollow;
            gm.player.ProtectFor(900f);
            gm.player.Invulnerable = true;
            await Seconds(3f);
            await Shot("nash-phase1-bar");
            Vector3 away = Query.Flat(gm.player.Position - hollow.Position);
            if (away.sqrMagnitude < .01f) away = Vector3.back;
            Teleport(gm.player, hollow.Position + away.normalized * (hollow.radius + 2.2f));
            await Until(() => !hollow.WindingUp, 6f, "the Prophet between moves");
            hollow.ReceiveDamage(new DamageInfo { amount = hollow.maxHp * .6f, source = DamageSource.Wall, ignoreShield = true });
            Note($"after a hit of 60%: hp {hollow.Hp:0}/{hollow.maxHp:0} ({hollow.Hp / hollow.maxHp:0.000}), phase {hollow.BossPhase}, changing {hollow.PhaseShifting}");
            await PhaseShots(hollow, "nash");
        }

        /// <summary>A change of phase as it runs: the gathering, a hit glancing off, the burst, and the boss in its new phase after it.</summary>
        static async Task PhaseShots(Enemy boss, string name)
        {
            var gm = GameManager.Instance;
            if (!boss.PhaseShifting) { Note(name + ": no change of phase began"); return; }
            await Seconds(.6f);
            await Shot(name + "-change-gather");
            bool landed = boss.ReceiveDamage(DamageInfo.Attack(30f, gm.player, Vector3.forward));
            Note($"{name}: a hit during the change landed {landed}, hp {boss.Hp:0}");
            await Seconds(.06f);
            await Shot(name + "-change-glance");
            float before = Query.Flat(gm.player.Position - boss.Position).magnitude;
            await Until(() => boss.PhaseBurstSequence > 0 || !boss.PhaseShifting, 5f, name + "'s burst");
            await Seconds(.08f);
            await Shot(name + "-change-burst");
            await Until(() => !boss.PhaseShifting, 5f, name + "'s change to end");
            float after = Query.Flat(gm.player.Position - boss.Position).magnitude;
            Note($"{name}: change over, phase {boss.BossPhase}, hp {boss.Hp:0}, player {before:0.0} -> {after:0.0} cells from it");
            await Seconds(1.5f);
            await Shot(name + "-phase-after");
        }

        /// <summary>Puts the player five cells from the Tyrant, forces the move, waits for its moment (and after for that long), and shoots.</summary>
        static async Task Move(Enemy tyrant, int move, Func<bool> moment, string name, float after = 0f)
        {
            var gm = GameManager.Instance;
            Clear(tyrant);
            await Until(() => !tyrant.WindingUp && !tyrant.TailSweeping && !tyrant.Charging && !tyrant.RushLeaping, 8f, "the Tyrant free before " + name);
            Vector3 away = Query.Flat(gm.player.Position - tyrant.Position);
            if (away.sqrMagnitude < .01f) away = Vector3.back;
            Teleport(gm.player, tyrant.Position + away.normalized * 5f);
            tyrant.ForceNextBossMove(move);
            await Until(moment, 10f, name);
            if (after > 0f) await Seconds(after);
            await Shot(name);
            Note($"{name}: phase {tyrant.BossPhase}, hp {tyrant.Hp:0}");
            await Seconds(1.2f);
        }

        /// <summary>Sends away every monster but the one kept, so the shots show the Tyrant alone.</summary>
        static void Clear(Enemy keep)
        {
            var gm = GameManager.Instance;
            foreach (var e in gm.waves.Alive.ToArray())
                if (e && e != keep && e.IsAlive && !e.IsBoss) e.DiscardWithoutReward();
        }

        /// <summary>The Game view with its interface, written at the end of this frame; the next frame starts after it.</summary>
        static async Task Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            Note("capture " + name);
            await Frame();
            await Frame();
        }

        static void Teleport(PlayerController player, Vector3 spot)
        {
            spot.y = 0f;
            var body = player.GetComponent<Rigidbody>();
            if (body) { body.position = spot; body.linearVelocity = Vector3.zero; }
            player.transform.position = spot;
            Physics.SyncTransforms();
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

        static async Task Command(GameManager gm, ArenaUiCommand command, Func<bool> done, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception(command + " timed out in " + gm.RunPhase);
                gm.ExecuteUiCommand(command, index);
                await Frame();
            }
        }
    }
}
