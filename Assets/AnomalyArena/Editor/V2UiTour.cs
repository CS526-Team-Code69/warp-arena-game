// An interface tour for review (2026-11-10). Menu: Anomaly Arena/V2 Campaign/Run interface tour (and its whitebox twin); enter Play mode
// on the title first. It captures, in the art chosen: the title, the open mode's map pick, the how-to-play pages' first page, then an
// open-mode run's panels as they come (the first supply crate, the starting weapon pick, the weapon card, the HUD in a wave, the pause
// screen, the level-up pick, the armory, the card pick of the build phase, the ready panel) and the result screen, into
// work/v2-campaign-1106/captures-tour-<art>/. Wave timers are 6 s on a copy of the wave set for this tour only, it kills what a wave
// spawns so the build phase comes quickly, and it earns two levels in the first wave (one picked at once, one kept for the build
// phase). Every capture also lists the texts that show stale glyphs (FontAtlasGuard.StaleTexts) as a PROBLEM line. For a tour that
// starts like a freshly loaded page, run "Reset the font texture" before entering Play mode: the font's texture then has to grow
// while the title appears, which is when texts were left with stale glyphs on 2026-11-10.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena.EditorTools
{
public static class V2UiTour
{
    static string folder, logPath;
    static int step;
    static float stepSince, startedAt, wait, waveSince;
    static bool whitebox, paused, shortened;
    static int levelsEarned;
    const string FontPath = "Assets/AnomalyArena/Fonts/NotoSansCJKsc-Regular.otf";

    [MenuItem("Anomaly Arena/V2 Campaign/Reset the font texture (before Play, for a fresh-start tour)")]
    public static void ResetFontTexture()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[V2UiTour] reset the font texture before entering Play mode"); return; }
        AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var texture = font && font.material ? font.material.mainTexture : null;
        Debug.Log($"[V2UiTour] font texture reset: {(texture ? texture.width : 0)} x {(texture ? texture.height : 0)}");
    }
    static readonly HashSet<string> shots = new();

    [MenuItem("Anomaly Arena/V2 Campaign/Run interface tour")]
    public static void Begin() { whitebox = false; Start(); }

    [MenuItem("Anomaly Arena/V2 Campaign/Run interface tour (whitebox)")]
    public static void BeginWhitebox() { whitebox = true; Start(); }

    static void Start()
    {
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, whitebox ? "../work/v2-campaign-1106/captures-tour-whitebox" : "../work/v2-campaign-1106/captures-tour-illustrated"));
        Directory.CreateDirectory(folder);
        logPath = Path.Combine(folder, "tour-log.txt");
        File.WriteAllText(logPath, "");
        step = 0; wait = 0f; paused = shortened = false; shots.Clear(); levelsEarned = 0; waveSince = 0f;
        startedAt = stepSince = Time.realtimeSinceStartup;
        EditorApplication.LockReloadAssemblies();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Note("tour started, shown as " + (whitebox ? "whitebox" : "illustrated"));
    }

    static void Note(string line) => File.AppendAllText(logPath, $"{Time.realtimeSinceStartup - startedAt,6:0.0}s {line}\n");

    static void End(string why)
    {
        EditorApplication.update -= Tick;
        EditorApplication.UnlockReloadAssemblies();
        Note($"END {why}; {shots.Count} captures");
    }

    /// <summary>Captures once under this name; true once the capture is at least `after` seconds old (so the next action comes after it
    /// was taken).</summary>
    static readonly Dictionary<string, float> shotAt = new();
    static bool Shot(string name, float after = .5f)
    {
        float now = Time.realtimeSinceStartup;
        if (shots.Add(name))
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            shotAt[name] = now;
            Note("capture " + name);
            Canvas.ForceUpdateCanvases(); // a text set in this frame has its new mesh before it is counted
            var stale = FontAtlasGuard.StaleTexts();
            if (stale.Count > 0) Note($"PROBLEM: stale glyphs at {name}: {string.Join(", ", stale)}");
            return false;
        }
        return now - shotAt[name] >= after;
    }

    static void Go(int next, float delay = 0f) { step = next; stepSince = Time.realtimeSinceStartup; wait = stepSince + delay; }
    static float InStep => Time.realtimeSinceStartup - stepSince;

    static void Tick()
    {
        try { Advance(); }
        catch (System.Exception e) { Note("ERROR " + e); End("error"); }
    }

    static void Advance()
    {
        if (!EditorApplication.isPlaying) { End("play mode ended"); return; }
        float now = Time.realtimeSinceStartup;
        if (now - startedAt > 240f) { End("timed out at step " + step); return; }
        if (now < wait) return;
        var gm = GameManager.Instance;
        if (!gm || !ArenaText.Ready || !WorldMap.Active) return;
        var hud = Object.FindAnyObjectByType<V2Hud>();
        switch (step)
        {
            case 0: // the title in the art asked for
                if (Art.Enabled == whitebox) { Art.Enabled = !whitebox; Go(0, .8f); return; }
                if (InStep < 1f) return;
                if (Shot("u01-title")) Go(1);
                return;
            case 1: // the open mode's map pick
                if (!hud) { Go(2); return; }
                if (InStep < .1f) { hud.OpenMapSelect(); wait = now + .8f; return; }
                if (Shot("u02-map-pick")) { hud.CloseMapSelect(); Go(2, .4f); }
                return;
            case 2: // the how-to-play pages (the story mode's, as the title opens them)
                if (!V2Tutorial.IsOpen && !shots.Contains("u03-how-to-play")) { V2Tutorial.ShowStory(ArenaText.Font); wait = now + .9f; return; }
                if (Shot("u03-how-to-play")) { V2Tutorial.Close(); Go(3, .5f); }
                return;
            case 3: // the open mode on the Dunes
                gm.ExecuteUiCommand(ArenaUiCommand.ChooseMap, 0);
                Note("open mode asked for on map 0");
                Go(4, 1.5f);
                return;
            case 4: // the run's panels as they come
                RunPanels(gm);
                return;
            case 5: // the result screen
                if (gm.State == GameState.Playing)
                {
                    if (gm.player && gm.player.IsAlive) gm.player.ReceiveDamage(new DamageInfo { amount = gm.player.maxHp * 20f, source = DamageSource.Hit, ignoreShield = true });
                    wait = now + 1.5f;
                    return;
                }
                if (InStep < 2.5f) return;
                if (Shot("u12-result", 1f)) End("the tour reached the result screen");
                return;
        }
    }

    static void RunPanels(GameManager gm)
    {
        float now = Time.realtimeSinceStartup;
        if (InStep > 150f) { Note($"the run's panels took too long (phase {gm.RunPhase})"); Go(5); return; }
        if (!shortened && gm.waves && gm.waves.waveSet)
        {
            shortened = true;
            var waves = Object.Instantiate(gm.waves.waveSet);
            foreach (var w in waves.waves) w.duration = 6f;
            gm.waves.waveSet = waves;
        }
        // The first weapon card of a first run opens the how-to-play pages by itself: captured once, then closed as a player would.
        if (V2Tutorial.IsOpen)
        {
            if (Shot("u06b-first-time-pages", .8f)) { V2Tutorial.Close(); wait = now + .5f; }
            return;
        }
        switch (gm.RunPhase)
        {
            case ArenaUiState.SupplyDrop:
                if (!gm.SupplyDropReady) return;
                if (Shot("u04-supply-crate")) { gm.ExecuteUiCommand(ArenaUiCommand.OpenSupply, -1); wait = now + .5f; }
                return;
            case ArenaUiState.Loadout:
                if (Shot("u05-starting-weapon", .8f)) { gm.ExecuteUiCommand(ArenaUiCommand.ChooseLoadout, 1); wait = now + .5f; }
                return;
            case ArenaUiState.WeaponIntro:
                if (Shot("u06-weapon-card", .8f)) { gm.ExecuteUiCommand(ArenaUiCommand.DismissWeaponIntro, -1); wait = now + .5f; }
                return;
            case ArenaUiState.WaveActive:
                if (waveSince <= 0f) waveSince = now;
                if (gm.player) gm.player.Heal(gm.player.maxHp);
                if (now - waveSince < 3f) return;
                if (!shots.Contains("u07-hud-in-a-wave")) { Shot("u07-hud-in-a-wave"); wait = now + .6f; return; }
                if (!paused && !shots.Contains("u08-pause")) { paused = true; gm.ExecuteUiCommand(ArenaUiCommand.Pause, -1); wait = now + .8f; return; }
                // Two levels: the first pick opens at once and is taken, the second is kept (Cancel) for the build phase's card pick.
                if (levelsEarned < 2 && gm.progression) { levelsEarned++; gm.progression.GainXp(gm.progression.XpRequired - gm.progression.Xp + .01f); wait = now + .6f; return; }
                foreach (var e in new List<Enemy>(gm.waves.Alive)) if (e && e.IsAlive && !e.IsBoss) e.Kill(gm.player);
                return;
            case ArenaUiState.LevelUp:
                if (levelsEarned >= 2) { gm.ExecuteUiCommand(ArenaUiCommand.Cancel, -1); wait = now + .5f; return; } // kept for the build phase
                if (Shot("u09b-level-up-pick", .8f)) { gm.ExecuteUiCommand(ArenaUiCommand.ChooseCard, 0); wait = now + .5f; }
                return;
            case ArenaUiState.Armory:
                if (Shot("u09-armory", .8f)) { gm.ExecuteUiCommand(ArenaUiCommand.SkipArmory, -1); wait = now + .5f; }
                return;
            case ArenaUiState.Draft:
                if (Shot("u10-card-pick", .8f)) { gm.ExecuteUiCommand(ArenaUiCommand.ChooseCard, 0); wait = now + .5f; }
                return;
            case ArenaUiState.ReplaceWeapon:
                gm.ExecuteUiCommand(ArenaUiCommand.ReplaceWeapon, 1); wait = now + .5f;
                return;
            case ArenaUiState.ReplaceSkill:
                gm.ExecuteUiCommand(ArenaUiCommand.ReplaceSkill, 0); wait = now + .5f;
                return;
            case ArenaUiState.Ready:
                if (Shot("u11-ready", .8f)) Go(5);
                return;
            default:
                // The pause screen (whatever state it reports): captured once, then resumed.
                if (paused && !shots.Contains("u08-pause")) { Shot("u08-pause"); wait = now + .8f; return; }
                if (paused && shots.Contains("u08-pause")) { paused = false; gm.ExecuteUiCommand(ArenaUiCommand.Resume, -1); wait = now + .5f; }
                return;
        }
    }
}
}
