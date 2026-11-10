// V2 day campaign: end-to-end Play-mode check (2026-11-06). Menu: Anomaly Arena/V2 Campaign/Run Play-mode check (enter Play mode
// in ArenaV2 first). It plays from the title to the result with debug input and writes work/v2-campaign-1106/captures/driver-log.txt
// and screenshots there. Short wave timers and dusk are set on copies of the assets for this check only; on the last two dawns before
// the last day (days 12 and 13 of the 14 since 2026-11-06) it grants currency (logged) so the ship path finishes on the last day. The
// first version was a separate driver script. Since 2026-11-06 the visits are at night only.
// Since 2026-11-06 (15:5x) the afternoon has no clock: the check mines the day's ore (clearing any side-quest monster of the black hole),
// then takes the base door's "into the night"; a weapon bought at the base shows its weapon card, which the check closes before it shops on.
// Since 2026-11-10 Day 0 is the forced spotlight tutorial (the steps whose input the check cannot give are passed by the tutorial's own
// debug call; ore, crates, the fight and the pickups are done as a player would), every afternoon has a weapon crate, the black hole is
// a timed damage check (failed once on purpose, passed the day after, then the void trial and its gift), and the base's purchases go
// through the study window's own entries.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena.EditorTools
{
public static class V2CampaignCheck
{
    const string Tag = "[V2Drive] ";
    public static string Folder;
    static string logPath;
    static int step, day, diedAtNight = -1;
    // One night's base visit may shop in several turns (a bought weapon's card closes in between): what it bought so far, and on which day.
    static int shopDay = -1, grantDay = -1, boughtResearch, boughtNewWeapons, boughtUpgrades;
    static float wait, startedAt, stepSince, lastAct, nightSince;
    static bool ended, shotTaken, raidShot, shipTabOpened, sideBroken, darkNodeShot, nightDeathDone, deathWalletCheck, readyConfirm, readyNoted, holeNoted;
    static readonly Dictionary<Enemy, float> raiderSince = new();
    // Since 2026-11-10: the black hole's check (the day it is failed on purpose; passed on a later day), the study window's captures
    // and what one visit bought through its entries.
    static int holeFailDay = -1, studyStage, voidStudyStage;
    static bool holeFailNoted, holePassNoted, giftNoted;
    static float holeAngle, holdSince;
    static string holdKey;
    static readonly Dictionary<string, int> studyBought = new();
    static readonly HashSet<string> shots = new();
    static V2Campaign campaign;
    static CampaignStage lastStage = CampaignStage.Off;
    static ArenaUiState lastPhase = ArenaUiState.Title;

    // The same check with the art off (since 2026-11-10: the whitebox is what the course build shows), into its own folder.
    static bool whitebox;
    // Measure (since 2026-11-10): the waves keep their real length and a reference player kills (the day's reference DPS, the monster
    // nearest the base first), so each day's line tells how the day's numbers came out in play.
    static bool measure;
    static float damageBank, lastGameTime, dayStartedAt, baseAtDawn, shieldAtDawn;
    static int energyAtDawn, elitesKilled;
    static readonly Dictionary<EnemyKind, int> killedByKind = new();
    static readonly Dictionary<EnemyKind, float> hpByKind = new();

    [MenuItem("Anomaly Arena/V2 Campaign/Run Play-mode check")]
    public static void Begin() { whitebox = false; measure = false; Start(); }

    [MenuItem("Anomaly Arena/V2 Campaign/Run Play-mode check (measure, real wave lengths)")]
    public static void BeginMeasure() { whitebox = false; measure = true; Start(); }

    [MenuItem("Anomaly Arena/V2 Campaign/Run Play-mode check (whitebox)")]
    public static void BeginWhitebox() { whitebox = true; measure = false; Start(); }

    static void Start()
    {
        Folder = Path.GetFullPath(Path.Combine(Application.dataPath, measure ? "../work/v2-campaign-1106/captures-measure" : whitebox ? "../work/v2-campaign-1106/captures-whitebox" : "../work/v2-campaign-1106/captures"));
        Directory.CreateDirectory(Folder);
        logPath = Path.Combine(Folder, "driver-log.txt");
        File.WriteAllText(logPath, "");
        step = 0; day = 0; wait = 0f; ended = false; shots.Clear(); raiderSince.Clear(); raidShot = false; lastStage = CampaignStage.Off; campaign = null;
        shipTabOpened = sideBroken = darkNodeShot = nightDeathDone = deathWalletCheck = holeNoted = false; diedAtNight = -1;
        shopDay = grantDay = -1; boughtResearch = boughtNewWeapons = boughtUpgrades = 0;
        holeFailDay = -1; studyStage = voidStudyStage = 0; holeFailNoted = holePassNoted = giftNoted = false; holeAngle = 0f; holdKey = null; studyBought.Clear();
        startedAt = stepSince = lastAct = Time.realtimeSinceStartup;
        EditorApplication.LockReloadAssemblies();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        Note("driver started " + DateTime.Now.ToString("s") + " (script reloads held)");
    }

    static void Note(string line)
    {
        UnityEngine.Debug.Log(Tag + line);
        File.AppendAllText(logPath, $"{Time.realtimeSinceStartup - startedAt,7:0.0}s {line}\n");
    }

    static void Changed(PlayModeStateChange change) { if (change == PlayModeStateChange.ExitingPlayMode) End("play mode ended early"); }

    static void End(string why)
    {
        if (ended) return;
        ended = true;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= Changed;
        if (campaign) { campaign.debugHoldE = false; }
        EditorApplication.UnlockReloadAssemblies();
        Note($"END {why} after {Time.realtimeSinceStartup - startedAt:0}s; script reloads released");
    }

    static void Go(int next, float delay = 0f) { step = next; wait = Time.realtimeSinceStartup + delay; stepSince = Time.realtimeSinceStartup; shotTaken = false; }
    static float InStep => Time.realtimeSinceStartup - stepSince;

    static void Shot(string name)
    {
        if (!shots.Add(name)) return;
        ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name + ".png"));
        Note("capture " + name);
        // A text that kept glyph positions of a font texture that was rebuilt since shows fragments (FontAtlasGuard, 2026-11-10).
        Canvas.ForceUpdateCanvases(); // a text set in this frame has its new mesh before it is counted
        var stale = FontAtlasGuard.StaleTexts();
        if (stale.Count > 0) Note($"PROBLEM: stale glyphs at {name}: {string.Join(", ", stale)}");
    }

    static void Tick()
    {
        try { Advance(); }
        catch (Exception e) { UnityEngine.Debug.LogError(Tag + "step " + step + ": " + e); End("error at step " + step + ": " + e.Message); }
    }

    static void Teleport(GameManager gm, Vector3 spot)
    {
        spot.y = 0f;
        if (WorldMap.Active) spot = WorldMap.Active.ClampPoint(spot, 3f); // never off the map (the first run fell off the east edge)
        var body = gm.player.GetComponent<Rigidbody>();
        if (body) { body.position = spot; body.linearVelocity = Vector3.zero; }
        gm.player.transform.position = spot;
        Physics.SyncTransforms();
    }

    static void Cmd(GameManager gm, ArenaUiCommand command, int index = -1) => gm.ExecuteUiCommand(command, index);

    static string Wallet => $"A{campaign.Alloy} E{campaign.Energy} D{campaign.Dark}" + (campaign.VoidEnergy + campaign.VoidAlloy > 0 ? $" VE{campaign.VoidEnergy} VA{campaign.VoidAlloy}" : "");
    static bool PlayerLets(PlayerAct act) => PlayerController.InputGate == null || PlayerController.InputGate(act);
    static string Hands(GameManager gm) => $"{gm.runWeapons.Hand[0]?.Label} / {gm.runWeapons.Hand[1]?.Label}";

    /// <summary>True once seconds have passed since this key was first asked, so a capture of the moment exists before the check moves on.</summary>
    static bool Held(string key, float seconds)
    {
        if (holdKey != key) { holdKey = key; holdSince = Time.realtimeSinceStartup; }
        return Time.realtimeSinceStartup - holdSince >= seconds;
    }

    static void Watch(GameManager gm)
    {
        if (campaign.Stage != lastStage) { Note($"stage {lastStage} -> {campaign.Stage} (day {campaign.Day}, phase {gm.RunPhase}, wallet {Wallet}, base {campaign.BaseHp:0}/{campaign.BaseMaxHp:0}, shield {campaign.BaseShield:0})"); lastStage = campaign.Stage; }
        if (gm.RunPhase != lastPhase) { lastPhase = gm.RunPhase; }
        // Since 2026-11-06 the ore trail leads to the black hole: note the day it is first met (its cutscene, then the side quest's line),
        // with its shards on the ground and its hum (above 0 within rules.holeHumReach of the hole, louder the nearer).
        if (!holeNoted && campaign.HoleSeen)
        {
            holeNoted = true;
            Note($"black hole met on day {campaign.Day} ({campaign.Stage}), map seed {campaign.MapSeed}; shards {campaign.HoleShardCount} shown {campaign.HoleShardsShown}, hum {campaign.HoleHumLevel:0.000}");
            Shot($"16-black-hole-day{campaign.Day}");
        }
        if (!holeFailNoted && campaign.HoleFailedToday)
        {
            holeFailNoted = true;
            Note($"the black hole's check failed on day {campaign.Day} ({campaign.CheckState}, dealt {campaign.CheckDealt:0} of {campaign.CheckRequired:0}): hole hidden {WorldMap.Active.CampaignHoleHidden}, player alive {gm.player.IsAlive}");
            Shot("16d-hole-check-failed");
        }
        if (!holePassNoted && campaign.CheckState == HoleCheckState.Passed)
        {
            holePassNoted = true;
            Note($"the black hole's check passed on day {campaign.Day}: dealt {campaign.CheckDealt:0} of {campaign.CheckRequired:0}");
        }
        if (!giftNoted && campaign.ProphetBeaten)
        {
            giftNoted = true;
            Note($"the Nash Prophet is beaten on day {campaign.Day}: wallet {Wallet}, phase {gm.RunPhase}, level {gm.progression.Level}, banked picks {gm.progression.BankedDrafts}, energy regeneration {gm.progression.VoidEnergy}");
            Shot("16i-void-gift");
        }
    }

    /// <summary>Base visit purchases in the ledger's order; repairs first. True when a bought weapon's card opened over the base panel
    /// (since 2026-11-06): the caller closes it and calls again, and the visit shops on where it stopped.</summary>
    static bool Shop()
    {
        var c = campaign;
        if (shopDay != c.Day) { shopDay = c.Day; boughtResearch = boughtNewWeapons = boughtUpgrades = 0; studyBought.Clear(); }
        foreach (CampaignRoom room in Enum.GetValues(typeof(CampaignRoom))) if (c.Room(room).damaged && c.TryRepairRoom(room)) Note($"repaired {room}");
        bool any = true; int guard = 0;
        while (any && guard++ < 40)
        {
            any = false;
            foreach (var room in new[] { CampaignRoom.Armament, CampaignRoom.Energy, CampaignRoom.Ship })
                if (c.NextLevelCost(room) != null && c.TryUpgradeRoom(room)) { Note($"bought {room} level {c.Room(room).level} -> wallet {Wallet}"); any = true; }
            if (c.TryRepairEngine()) { Note("repaired the engine"); any = true; }
            if (c.TryRepairArmor()) { Note("repaired the armor"); any = true; }
            if (c.TrySynthesiseFuel()) { Note($"fuel {c.Fuel}/{c.FuelUnits}"); any = true; }
            if (c.TryUpgradeDurability()) { Note($"durability {c.Durability} (base max {c.BaseMaxHp:0})"); any = true; }
            if (c.TryUpgradeShield()) { Note($"shield level {c.ShieldLevel} (cap {c.BaseMaxShield:0})"); any = true; }
            // Since 2026-11-10 the Armament Bay, the Energy Lab and (once the Nash Prophet is beaten) the Void room sell item by item in the
            // study window: the check buys what the window offers through its own entries, at most twice the same entry a visit.
            while (boughtUpgrades < 12)
            {
                StudyEntry pick = null;
                foreach (var tab in c.BuildStudy())
                {
                    foreach (var entry in tab.entries)
                        if (entry.state == StudyState.Available && entry.buy != null && (!studyBought.TryGetValue(entry.id, out int times) || times < 2)) { pick = entry; break; }
                    if (pick != null) break;
                }
                if (pick == null) break;
                bool bought = pick.buy();
                studyBought[pick.id] = bought ? (studyBought.TryGetValue(pick.id, out int had) ? had : 0) + 1 : 99;
                Note($"study {pick.room} \"{pick.title}\" ({pick.rarity}) {pick.now} -> {pick.after} for {pick.costText}: {(bought ? "bought" : "refused")} -> wallet {Wallet}, team Mk {c.TeamMk}");
                if (!bought) continue;
                boughtUpgrades++; any = true;
                if (WeaponCardOpen) return true;
            }
            if (c.OverloadOpen)
                foreach (bool shield in new[] { true, false })
                    if (c.TryBuyOverload(shield, !shield)) { Note($"overload {(shield ? "shield" : "damage")}: shield buys {c.OverloadShieldBuys}, damage buys {c.OverloadDamageBuys} -> wallet {Wallet}"); any = true; }
        }
        if (c.LampStock < 3 && c.TryCraftLamp()) Note("crafted a lamp");
        if (c.BaseHp < c.BaseMaxHp - 1f && c.TryRepairHull()) Note("repaired the hull");
        return false;
    }

    /// <summary>A weapon bought at the base shows its weapon card (since 2026-11-06); the base panel comes back once it is closed.</summary>
    static bool WeaponCardOpen => GameManager.Instance && GameManager.Instance.RunPhase == ArenaUiState.WeaponIntro;

    static bool MineNearest(GameManager gm, V2Campaign.NodeKind kind)
    {
        if (!campaign.TryNearestNode(kind, gm.player.Position, out var at)) { campaign.debugHoldE = false; return false; }
        Vector3 from = gm.player.Position;
        if (Query.Flat(at - from).magnitude > 1.2f) Teleport(gm, at + Query.Flat(from - at).normalized * 1.1f);
        campaign.debugHoldE = true;
        return true;
    }

    static void Advance()
    {
        if (Time.realtimeSinceStartup - startedAt > 2700f) { End("timed out at step " + step); return; }
        if (Time.realtimeSinceStartup < wait) return;
        var gm = GameManager.Instance;
        if (step > 0 && (!gm || !campaign)) { End("the game went away"); return; }
        if (campaign) Watch(gm);
        bool act = Time.realtimeSinceStartup - lastAct > .3f;
        if (act) lastAct = Time.realtimeSinceStartup;
        switch (step)
        {
            case 0:
                if (!gm || !ArenaText.Ready || !WorldMap.Active) { if (InStep > 30f) End("the game never came up"); return; }
                campaign = gm.GetComponent<V2Campaign>();
                if (!campaign || !campaign.rules) { End("no V2Campaign with rules on the GameManager"); return; }
                UnityEngine.Random.InitState(1106);
                var waves = Object.Instantiate(gm.waves.waveSet);
                if (!measure) foreach (var w in waves.waves) w.duration = 7f;
                gm.waves.waveSet = waves;
                var rules = Object.Instantiate(campaign.rules);
                rules.duskSeconds = 9f;
                rules.holeCheckSeconds = 10f;
                campaign.rules = rules;
                Art.Enabled = !whitebox;
                Note($"boot: phase {gm.RunPhase}, scene {gm.gameObject.scene.name}, game view {Screen.width}x{Screen.height}, map {WorldMap.Active.biome?.displayName}, art {(campaign.art ? "set" : "none")}, shown as {(whitebox ? "whitebox" : "illustrated")}{(measure ? ", MEASURE: the waves keep their real length and a reference player kills; dusk 9 s and the black hole's check 10 s for this check only (copies, assets untouched)" : "; wave timers 7 s, dusk 9 s and the black hole's check 10 s for this check only (copies, assets untouched)")}");
                Shot("01-title");
                Go(1, 1.5f);
                return;
            case 1:
                if (campaign.Stage == CampaignStage.Off) { if (act) Cmd(gm, ArenaUiCommand.StartCampaign); if (InStep > 10f) End("StartCampaign never took"); return; }
                if (InStep > 1.2f) { Shot("02-day0-card"); Go(2); }
                return;
            case 2:
                if (campaign.Stage == CampaignStage.Tutorial) { Note($"start: phase {gm.RunPhase}, hands {gm.runWeapons.Hand[0]?.Label} / {gm.runWeapons.Hand[1]?.Label} (no crate, no loadout)"); Go(3, 1.5f); return; }
                if (gm.RunPhase is ArenaUiState.Loadout or ArenaUiState.SupplyDrop && !readyNoted) { readyNoted = true; Note($"PROBLEM: {gm.RunPhase} showed at the start"); }
                if (!act) return;
                if (gm.RunPhase == ArenaUiState.SupplyDrop && gm.SupplyDropReady) Cmd(gm, ArenaUiCommand.OpenSupply);
                else if (gm.RunPhase == ArenaUiState.Loadout) Cmd(gm, ArenaUiCommand.ChooseLoadout, 1);
                else if (gm.RunPhase == ArenaUiState.WeaponIntro) Cmd(gm, ArenaUiCommand.DismissWeaponIntro);
                if (InStep > 40f) End("no tutorial after the loadout");
                return;
            case 3:
                Shot("03-crash-site");
                if (campaign.Stage != CampaignStage.Tutorial) { campaign.debugHoldE = false; campaign.debugPressE = false; Go(4); return; }
                if (!act) return;
                gm.player.Heal(gm.player.maxHp);
                // Since 2026-11-10 Day 0 is the forced spotlight tutorial. A weapon crate's pick-one-of-three and the weapon card it shows:
                if (WeaponCardOpen) { Shot("03e-crate-weapon-card"); if (Held("tut-card", .8f)) Cmd(gm, ArenaUiCommand.DismissWeaponIntro); return; }
                if (campaign.CratePanelOpen)
                {
                    Shot(campaign.TutorialStepNow == TutorialStep.Crate ? "03d-crate-panel" : "03f-second-crate-panel");
                    if (Held("tut-crate-panel" + campaign.TutorialStepNow, 1.2f)) { campaign.DebugTakeCrate(0); Note($"day 0 crate: took the first weapon, hands {Hands(gm)}"); }
                    return;
                }
                var teach = campaign.TutorialStepNow;
                // The steps whose input the check cannot give (the walk, the shots, the sprint, the right button) are passed by the tutorial's own debug call.
                if (campaign.TutorialStepForced && teach is TutorialStep.Move or TutorialStep.Fire or TutorialStep.Sprint or TutorialStep.Switch)
                {
                    Shot($"03s-spotlight-{teach}");
                    if (Held("tut-" + teach, 1.2f)) { Note($"tutorial step {teach}: spotlight up, passed by the check (move allowed {PlayerLets(PlayerAct.Move)}, fire {PlayerLets(PlayerAct.Fire)}, sprint {PlayerLets(PlayerAct.Sprint)})"); campaign.DebugPassTutorialStep(); }
                    stepSince = Time.realtimeSinceStartup;
                    return;
                }
                var alive = new List<Enemy>();
                foreach (var e in gm.waves.Alive) if (e && e.IsAlive) alive.Add(e);
                if (alive.Count > 0)
                {
                    Shot("04-first-monsters");
                    if (Held("tut-fight", 2.5f)) foreach (var e in alive) e.Kill(gm.player);
                    return;
                }
                if (campaign.PickupCount > 0) { Shot("04b-energy-pickups"); if (Held("tut-pickups", 1f)) campaign.DebugCollectPickups(); return; }
                if (campaign.NodesLeft(V2Campaign.NodeKind.Ore) > 0)
                {
                    if (teach == TutorialStep.Mine && campaign.TutorialStepForced) Shot("03m-spotlight-Mine");
                    MineNearest(gm, V2Campaign.NodeKind.Ore);
                    return;
                }
                campaign.debugHoldE = false;
                if (campaign.ObjectivePoint is Vector3 goal)
                {
                    Vector3 from = gm.player.Position;
                    Teleport(gm, Query.Flat(goal - from).magnitude > 2.5f ? goal + Query.Flat(from - goal).normalized * 1.5f : goal);
                    // E opens a weapon crate once the player stands by it (the first one only after its spotlight is up).
                    if (campaign.CratesOnMap > 0 && teach is TutorialStep.Crate or TutorialStep.SecondCrate)
                    {
                        if (teach == TutorialStep.Crate && campaign.TutorialStepForced) Shot("03c-spotlight-Crate");
                        campaign.debugPressE = true;
                    }
                }
                else Teleport(gm, gm.player.Position + Vector3.right * 6f);
                stepSince = Time.realtimeSinceStartup;
                return;
            case 4: // arrival story -> first base visit
                if (campaign.Stage == CampaignStage.Story) { if (InStep > 2f) Shot("05-arrival"); return; }
                if (campaign.Stage != CampaignStage.Base) { if (InStep > 25f) End("no base visit after the tutorial"); return; }
                Shot("06-base-first");
                if (InStep < 3f) return;
                Note($"first visit wallet {Wallet}: armament {campaign.TryUpgradeRoom(CampaignRoom.Armament)}, energy lab {campaign.TryUpgradeRoom(CampaignRoom.Energy)} -> {Wallet}; level {gm.progression.Level}, xp {gm.progression.Xp:0.0}/{gm.progression.XpRequired}");
                Go(5, 1f);
                return;
            case 5:
                Shot("07-base-built");
                if (campaign.Ui.BaseOpen) { campaign.LeaveBase(); return; }
                if (campaign.Stage == CampaignStage.Story)
                {
                    if (InStep > 2.5f) Shot("08-memory-cg");
                    if (InStep > 7f) Shot("08b-memory-cg");
                    if (InStep > 8f) campaign.Ui.SkipCg();
                    return;
                }
                if (campaign.Stage == CampaignStage.Night) { nightSince = Time.realtimeSinceStartup; Go(6, 1.5f); }
                return;
            case 6: // tutorial night
                Shot("09-night0");
                if (campaign.Stage != CampaignStage.Night) { Go(7); return; }
                if (!act) return;
                if (campaign.LampCount == 0 && campaign.TryNearestNode(V2Campaign.NodeKind.Dark, gm.player.Position, out var cluster))
                {
                    Teleport(gm, cluster + new Vector3(1.2f, 0f, 0f));
                    campaign.debugPressF = true;
                    return;
                }
                if (campaign.LampCount > 0) Shot("10-night0-lamp");
                if (campaign.NodesLeft(V2Campaign.NodeKind.Dark) > 0) { MineNearest(gm, V2Campaign.NodeKind.Dark); return; }
                if (InStep < 14f && campaign.NodesLeft(V2Campaign.NodeKind.Ore) > 0 && campaign.TryNearestNode(V2Campaign.NodeKind.Ore, gm.player.Position, out var ore) && Query.Flat(ore - gm.player.Position).magnitude < 20f)
                {
                    // the tutorial ore lies beside the cluster, in the lamp's light
                    MineNearest(gm, V2Campaign.NodeKind.Ore); return;
                }
                campaign.debugHoldE = false;
                Note($"night 0 mined: wallet {Wallet}, shadows {campaign.ShadowCount}");
                Teleport(gm, campaign.DoorPoint);
                campaign.debugPressE = true;
                Go(7, .6f);
                return;
            case 7: // a base visit at dusk or at night (since 2026-11-06 there is no dawn panel: dawn starts the next day at once)
                if (campaign.Stage != CampaignStage.Base)
                {
                    if (campaign.Stage == CampaignStage.Day) { Go(8); return; }
                    if (InStep > 8f) { Note($"expected the base, stage {campaign.Stage}"); Go(9); }
                    return;
                }
                // A weapon bought here shows its weapon card over the base panel (since 2026-11-06): one capture a night, then close it;
                // the panel comes back and the visit shops on.
                if (WeaponCardOpen)
                {
                    string cardShot = $"11c-night{campaign.Day}-weapon-card";
                    if (!shots.Contains(cardShot)) { Shot(cardShot); wait = Time.realtimeSinceStartup + .6f; return; }
                    if (act) Cmd(gm, ArenaUiCommand.DismissWeaponIntro);
                    return;
                }
                if (!campaign.Ui.BaseOpen) { if (InStep > 30f) End("the base panel did not come back after a weapon card"); return; }
                // The first night's visit shows the ship tab once: its picture is the ship itself, drawn live (D1-C).
                if (campaign.Day == 1 && !shipTabOpened)
                {
                    shipTabOpened = true;
                    campaign.Ui.ShowTab(CampaignRoom.Ship);
                    wait = Time.realtimeSinceStartup + 1.2f;
                    return;
                }
                if (campaign.Day == 1 && shipTabOpened && !shots.Contains("12s-night1-ship-live-view"))
                {
                    Note($"ship tab: live view {(campaign.ShipViewTexture ? "drawn" : "missing")}");
                    Shot("12s-night1-ship-live-view");
                    wait = Time.realtimeSinceStartup + .6f;
                    return;
                }
                int lastDay = campaign.rules.days;
                if (campaign.Day >= lastDay - 2 && campaign.Day < lastDay && grantDay != campaign.Day)
                {
                    grantDay = campaign.Day;
                    campaign.DebugGrant(400, 400, 60);
                    Note($"DEBUG GRANT at the base on night {campaign.Day}: +400 alloy +400 energy +60 dark matter, so the check reaches the ending on day {lastDay}");
                }
                if (StudyCaptures()) return; // the study window is shown once per room (and once for the Void room), then closed
                if (Shop()) return; // a weapon card opened: closed on a later tick, then this visit shops on
                Shot($"11-base-night{campaign.Day}");
                if (campaign.Day >= campaign.rules.days && campaign.CanIgnite) { Note("ignition ready on the last night"); campaign.Ignite(); Go(20); return; }
                campaign.WaitForDawn();
                Go(8, .5f);
                return;
            case 8: // dawn build: air drops, picks, ready
                if (campaign.Stage == CampaignStage.Day) { day = campaign.Day; raiderSince.Clear(); raidShot = false; BeginMeasuredDay(gm); Note($"day {day} begins: level {gm.progression.Level}, banked picks {gm.progression.BankedDrafts}, weapons {gm.runWeapons.Hand[0]?.Label} / {gm.runWeapons.Hand[1]?.Label}"); Go(9); return; }
                if (!act) return;
                switch (gm.RunPhase)
                {
                    case ArenaUiState.SupplyDrop: if (gm.SupplyDropReady) Cmd(gm, ArenaUiCommand.OpenSupply); break;
                    case ArenaUiState.Armory:
                        int pick = Array.FindIndex(gm.runWeapons.Offers, o => o != null);
                        Note($"air drop: {string.Join(", ", Array.ConvertAll(gm.runWeapons.Offers, o => o == null ? "-" : o.FormName + " " + o.MkLabel))}");
                        Cmd(gm, pick >= 0 ? ArenaUiCommand.TakeArmory : ArenaUiCommand.SkipArmory, pick); break;
                    case ArenaUiState.ReplaceWeapon: Cmd(gm, ArenaUiCommand.ReplaceWeapon, 1); break;
                    case ArenaUiState.WeaponIntro: Cmd(gm, ArenaUiCommand.DismissWeaponIntro); break;
                    case ArenaUiState.Draft:
                        Note($"draft: {string.Join(" | ", Array.ConvertAll(gm.progression.CurrentCards, c => c.Kind + ":" + c.Title))}");
                        Cmd(gm, ArenaUiCommand.ChooseCard, 0); break;
                    case ArenaUiState.ReplaceSkill: Cmd(gm, ArenaUiCommand.ReplaceSkill, 0); break;
                    // Start, then confirm on the next tick: the game takes one menu command per 0.15 s, so a confirm sent in the same tick is dropped.
                    case ArenaUiState.Ready: if (!readyNoted) { readyNoted = true; Note("PROBLEM: the ready panel showed in the campaign"); } readyConfirm = !readyConfirm; Cmd(gm, readyConfirm ? ArenaUiCommand.StartNextWave : ArenaUiCommand.ConfirmEarlyStart); break;
                }
                if (InStep > 60f) End($"stuck in the dawn build at {gm.RunPhase}");
                return;
            case 9: // day
                // A death on the last night wakes the player into the ignition at dawn: the Tyrant comes next, not a day.
                if (campaign.Stage == CampaignStage.FinalBoss) { Note($"the ignition began at dawn after the last night (day {campaign.Day}): on to the Tyrant"); Go(20); return; }
                if (campaign.Stage == CampaignStage.Dusk) { EndMeasuredDay(gm); Note($"day {day} over: base {campaign.BaseHp:0}/{campaign.BaseMaxHp:0}, broken {string.Join(",", BrokenRooms())}, wallet {Wallet}, level {gm.progression.Level}, kills {gm.waves.Kills}; afternoon: ore nodes {campaign.NodesLeft(V2Campaign.NodeKind.Ore)}, crates {campaign.CratesOnMap}, void alloy nodes {campaign.NodesLeft(V2Campaign.NodeKind.VoidAlloy)}"); Go(10, 1f); return; }
                if (gm.State != GameState.Playing) { End($"run ended in the day: {gm.State}, base destroyed {campaign.BaseDestroyed}"); return; }
                gm.player.Heal(gm.player.maxHp);
                if (gm.RunPhase == ArenaUiState.LevelUp && act) { Note($"mid-day pick: {string.Join(" | ", Array.ConvertAll(gm.progression.CurrentCards, c => c.Kind + ":" + c.Title))}"); Cmd(gm, ArenaUiCommand.ChooseCard, 0); return; }
                if (gm.RunPhase == ArenaUiState.WeaponIntro && act) { Cmd(gm, ArenaUiCommand.DismissWeaponIntro); return; }
                if (gm.RunPhase == ArenaUiState.ReplaceSkill && act) { Cmd(gm, ArenaUiCommand.ReplaceSkill, 0); return; }
                if (measure) { MeasuredDay(gm); if (InStep > 900f) End("a measured day never ended"); return; }
                if (!act) return;
                int raiders = 0;
                foreach (var e in new List<Enemy>(gm.waves.Alive))
                {
                    if (!e || !e.IsAlive) continue;
                    if (e.IsBoss) continue;
                    if (e.Raider)
                    {
                        raiders++;
                        Vector3 rel = Query.Flat(e.Position - V2Campaign.BaseCentre);
                        bool atBase = Mathf.Abs(rel.x) < V2Campaign.BaseHalf + 2.5f && Mathf.Abs(rel.z) < V2Campaign.BaseHalf + 2.5f;
                        float now = Time.realtimeSinceStartup;
                        // At the base: two seconds of strikes, then the driver kills it. Not there yet: cleared after 12 s if it never arrives.
                        if (!raiderSince.TryGetValue(e, out float first)) raiderSince[e] = atBase ? now : now + 10f;
                        else if (atBase && first > now) raiderSince[e] = now;
                        if (raiderSince.TryGetValue(e, out float since) && Time.realtimeSinceStartup - since > 2f) e.Kill(gm.player); // about two strikes: a defended base
                        else if (!atBase && Query.Flat(e.Position - gm.player.Position).magnitude > 60f) e.Kill(gm.player);
                        continue;
                    }
                    e.Kill(gm.player);
                }
                if (!raidShot && raiderSince.Count > 0 && day <= 2) { raidShot = true; Teleport(gm, V2Campaign.BaseCentre + new Vector3(-9f, 0f, -9f)); Shot($"13-day{day}-raid"); }
                // Day 2 breaks the east side once on purpose (D3-A): only that side takes the damaged texture and burn marks; the dawn
                // shop repairs it through central control.
                if (day == 2 && !sideBroken && InStep > 2f && campaign.Room(CampaignRoom.Armament).level > 0)
                {
                    sideBroken = true;
                    campaign.DamageBase(1, campaign.BaseMaxHp * .3f, V2Campaign.BaseCentre + Vector3.right * (V2Campaign.BaseHalf + .5f) + Vector3.up);
                    Note($"east side hit for 30% on purpose: armament broken {campaign.Room(CampaignRoom.Armament).damaged}, model cut into sides {campaign.SidesSplit}, " +
                         $"damaged sides N {campaign.SideShowsDamage(0)} E {campaign.SideShowsDamage(1)} S {campaign.SideShowsDamage(2)} W {campaign.SideShowsDamage(3)}, burn marks E {campaign.ScorchCount(1)}");
                    Teleport(gm, V2Campaign.BaseCentre + new Vector3(10f, 0f, -6f));
                    wait = Time.realtimeSinceStartup + 1f;
                    return;
                }
                if (sideBroken && day == 2) Shot("13d-day2-east-side-broken");
                if (InStep > 240f) End("a day never ended");
                return;
            case 10: // the afternoon (CampaignStage.Dusk)
                Shot($"14-dusk{day}");
                if (campaign.Stage == CampaignStage.Night) { campaign.debugHoldE = false; nightSince = Time.realtimeSinceStartup; Note($"night {day}: shadows {campaign.ShadowCount}, overrun regions {campaign.OverrunRegions}, dark nodes {campaign.NodesLeft(V2Campaign.NodeKind.Dark)}, ore left {campaign.NodesLeft(V2Campaign.NodeKind.Ore)}"); Go(11, 1f); return; }
                // Since 2026-11-06 22:4x a fall in the afternoon wakes the player at the base and the afternoon goes on (the stage stays
                // Dusk); a morning here would mean the old rule (the night skipped) is back.
                if (campaign.Stage == CampaignStage.Day) { campaign.debugHoldE = false; Note($"afternoon {day} ended in a fall: day {campaign.Day} begins"); Go(8); return; }
                // Since 2026-11-06 the afternoon has no clock (CampaignRules.openEndedEvenings): its base visit's "into the night" ends it.
                if (campaign.Stage == CampaignStage.Base)
                {
                    campaign.debugHoldE = false;
                    if (!campaign.BaseFromDusk) return;
                    // The panel with its "into the night" button is captured first; the button is taken on a later tick.
                    string baseShot = $"14b-afternoon{day}-base";
                    if (!shots.Contains(baseShot)) { Shot(baseShot); wait = Time.realtimeSinceStartup + .6f; return; }
                    if (act)
                    {
                        Note($"afternoon {day}: into the night from the base (ore left today {campaign.NodesLeft(V2Campaign.NodeKind.Ore)}, wallet {Wallet})");
                        campaign.EnterNight();
                    }
                    return;
                }
                // The void trial behind the black hole's portal holds its own phase: the Nash Prophet is killed there to check the reward.
                if (WorldMap.Active && WorldMap.Active.TrialActive)
                {
                    gm.player.Heal(gm.player.maxHp);
                    Shot("16h-void-trial");
                    var prophet = WorldMap.Active.hollow;
                    if (act && Held("trial", 4f) && prophet && prophet.IsAlive) { Note($"killing the Nash Prophet (hp {prophet.Hp:0} of {prophet.maxHp:0}) to check the reward"); prophet.Kill(gm.player); }
                    return;
                }
                if (campaign.Stage != CampaignStage.Dusk) { if (InStep > 200f) End($"stuck in the afternoon at {campaign.Stage}"); return; }
                gm.player.Heal(gm.player.maxHp);
                if (!act) return;
                // An afternoon weapon crate's pick-one-of-three, then the card of the weapon taken.
                if (WeaponCardOpen) { Cmd(gm, ArenaUiCommand.DismissWeaponIntro); return; }
                if (campaign.CratePanelOpen)
                {
                    Shot($"14c-crate-day{day}");
                    if (Held("crate" + day, 1.2f)) { campaign.DebugTakeCrate(0); Note($"afternoon {day} crate: took the first weapon, hands {Hands(gm)}, autos {gm.runWeapons.Auto[0]?.Label} / {gm.runWeapons.Auto[1]?.Label}"); }
                    return;
                }
                // The black hole's side quest sends monsters in the afternoon when the ore trail comes near it: cleared like the day's.
                foreach (var e in new List<Enemy>(gm.waves.Alive)) if (e && e.IsAlive && !e.IsBoss) e.Kill(gm.player);
                if (campaign.PickupCount > 0) campaign.DebugCollectPickups();
                // The trail shows two nodes at a time and the next surfaces as one is mined; the few scattered nodes off the trail are mined
                // too, the nearest first. With every node mined (or after a while) the door is next.
                if (InStep < 45f && MineNearest(gm, V2Campaign.NodeKind.Ore)) return;
                campaign.debugHoldE = false;
                // The day's weapon crate lies on the ore trail: E opens it.
                if (InStep < 70f && campaign.TryNearestCrate(gm.player.Position, out var crateAt))
                {
                    Teleport(gm, crateAt + new Vector3(1.4f, 0f, 0f));
                    campaign.debugPressE = true;
                    return;
                }
                campaign.debugPressE = false;
                if (HoleDrill(gm)) return;
                // Void alloy grows where the hole stood once the Nash Prophet is beaten.
                if (InStep < 170f && MineNearest(gm, V2Campaign.NodeKind.VoidAlloy)) return;
                campaign.debugHoldE = false;
                if (!campaign.EveningsOpenEnded) return; // with a clock the afternoon ends by itself
                Teleport(gm, campaign.DoorPoint);
                campaign.debugPressE = true;
                if (InStep > 200f) End("the afternoon never went into the night");
                return;
            case 11: // night
                Shot($"15-night{day}");
                if (!gm.player.IsAlive && diedAtNight != day && !deathWalletCheck)
                {
                    diedAtNight = day;
                    var lost = campaign.TonightsHaul;
                    Note($"night {day}: the player died (haul at that moment A{lost.alloy} E{lost.energy} D{lost.dark}, wallet {Wallet})");
                }
                if (campaign.Stage == CampaignStage.Base) { campaign.debugHoldE = false; Go(7); return; }
                if (campaign.Stage != CampaignStage.Night) { campaign.debugHoldE = false; Note($"night ended without the base: {campaign.Stage}"); Go(7); return; }
                if (!act) return;
                // Night 3 dies once on purpose (the rule since 2026-11-06): tonight's haul is lost, the base's stock stays, the night ends at
                // the base and the next day starts as usual.
                if (day == 3 && !nightDeathDone && campaign.TonightsHaul.dark > 0 && Time.realtimeSinceStartup - nightSince > 6f)
                {
                    nightDeathDone = true;
                    campaign.debugHoldE = false;
                    var haul = campaign.TonightsHaul;
                    Note($"night 3 death on purpose: wallet {Wallet}, tonight's haul A{haul.alloy} E{haul.energy} D{haul.dark}");
                    gm.player.ReceiveDamage(new DamageInfo { amount = gm.player.maxHp * 10f, source = DamageSource.Hit, ignoreShield = true });
                    deathWalletCheck = true;
                    wait = Time.realtimeSinceStartup + .5f;
                    return;
                }
                if (deathWalletCheck) { deathWalletCheck = false; Shot("15d-night3-death"); Note($"after the death: alive {gm.player.IsAlive}, run {gm.State}, wallet {Wallet}, stage {campaign.Stage}"); return; }
                if (Time.realtimeSinceStartup - nightSince > 22f || campaign.NodesLeft(V2Campaign.NodeKind.Dark) == 0)
                {
                    campaign.debugHoldE = false;
                    // The black hole is put away at night: its shards hidden, its hum silent.
                    Note($"night {day} haul: wallet {Wallet}, hp {gm.player.Hp:0}/{gm.player.maxHp:0}, lamps left {campaign.LampStock}; hole shards shown {campaign.HoleShardsShown}, hum {campaign.HoleHumLevel:0.000}");
                    Teleport(gm, campaign.DoorPoint);
                    campaign.debugPressE = true;
                    return;
                }
                if (campaign.TryNearestNode(V2Campaign.NodeKind.Dark, gm.player.Position, out var dark))
                {
                    if (Query.Flat(dark - gm.player.Position).magnitude > 6f)
                    {
                        // a new cluster: walk there and set a lamp down first
                        Teleport(gm, dark + new Vector3(1.2f, 0f, 0f));
                        if (campaign.LampStock > 0) campaign.debugPressF = true;
                        return;
                    }
                    MineNearest(gm, V2Campaign.NodeKind.Dark);
                    if (!darkNodeShot && day >= 1) { darkNodeShot = true; Shot($"15b-night{day}-dark-node"); }
                }
                return;
            case 20: // ignition and the Tyrant
                if (InStep > 3f) Shot("18-ignition");
                if (InStep > 9.5f) Shot("18b-ignition-failed");
                if (campaign.Stage == CampaignStage.Core) { Go(22, 1.2f); return; }
                if (campaign.Stage != CampaignStage.FinalBoss) { if (InStep > 40f) End("no final boss after the ignition"); return; }
                var tyrant = gm.waves.tyrant;
                if (tyrant && tyrant.IsAlive)
                {
                    gm.player.Heal(gm.player.maxHp);
                    Shot("19-tyrant");
                    if (!shotTaken) { shotTaken = true; stepSince = Time.realtimeSinceStartup; return; }
                    if (InStep > 4f) { Note($"killing the Tyrant (hp {tyrant.Hp:0}) to check what follows"); tyrant.Kill(gm.player); }
                }
                return;
            case 21:
                return;
        }
        if (campaign && campaign.Stage == CampaignStage.Core && step != 22) Go(22, 1.2f);
        if (step == 22)
        {
            Shot("20-core");
            if (campaign.Stage == CampaignStage.Core && campaign.TryNearestNode(V2Campaign.NodeKind.Core, gm.player.Position, out var core))
            {
                Teleport(gm, core + new Vector3(1f, 0f, 0f));
                campaign.debugPressE = true;
            }
            if (campaign.Stage == CampaignStage.Ending) Go(23, 3f);
        }
        else if (step == 23)
        {
            Shot("21-ending");
            if (gm.State == GameState.Won) { Go(24, 1.5f); Note($"won: day {campaign.Day}, level {gm.progression.Level}, kills {gm.waves.Kills}, earned A{campaign.EarnedAlloy} E{campaign.EarnedEnergy} D{campaign.EarnedDark}"); }
        }
        else if (step == 24) { Shot("22-result"); Go(25, 1f); }
        else if (step == 25) End("the campaign was played from the title to the result");
    }

    /// <summary>The black hole's timed damage check (since 2026-11-10), driven in the afternoon: failed once on purpose (one small hit, then
    /// the clock runs out), passed on a later day (the rest of the damage at once), then the portal, its question and the void trial.
    /// The player keeps moving round the hole meanwhile: the Nash Prophet's pillars fall where they stood. True while it is busy.</summary>
    static bool HoleDrill(GameManager gm)
    {
        var map = WorldMap.Active;
        var hole = map ? map.CampaignHole : null;
        if (!hole || map.TrialWon || hole.Phase == HolePhase.Gone) return false;
        if (hole.Phase == HolePhase.Collapsing) return true;
        if (hole.Phase == HolePhase.Portal)
        {
            if (map.CampaignHoleHidden) return false;
            if (campaign.Ui.ConfirmOpen)
            {
                Shot("16g-portal-question");
                if (Held("portal-question", 1.2f)) { Note("the portal's question answered yes"); campaign.Ui.DebugAnswerConfirm(true); }
                return true;
            }
            // Standing on the portal shows its E; E then asks before the trial.
            Teleport(gm, hole.Centre);
            if (!Held("portal-stand", 1.5f)) return true;
            Shot("16f-portal-standing-on-it");
            if (Held("portal-stand", 2.5f)) Note($"E on the portal: {map.TryEnterPortal(gm.player)} (can enter {map.CanEnterPortal(gm.player, hole)})");
            return !Held("portal-stand", 12f);
        }
        if (map.CampaignHoleHidden || campaign.HoleFailedToday || campaign.CheckState is HoleCheckState.Passed or HoleCheckState.Failed) return false;
        float ring = hole.GravityRadius + 7f;
        if (!campaign.HoleSeen)
        {
            // Where the hole is on screen, until its first-sight cutscene has played.
            Teleport(gm, hole.Centre + new Vector3(-ring, 0f, 0f));
            return !Held("hole-sight" + campaign.Day, 16f);
        }
        var target = campaign.HoleTarget;
        if (!target || !target.gameObject.activeInHierarchy)
        {
            Teleport(gm, hole.Centre + new Vector3(-ring, 0f, 0f));
            return !Held("hole-target" + campaign.Day, 8f);
        }
        holeAngle += 55f;
        Teleport(gm, hole.Centre + Quaternion.Euler(0f, holeAngle, 0f) * Vector3.forward * ring);
        bool toFail = holeFailDay < 0 || holeFailDay == campaign.Day;
        if (campaign.CheckState == HoleCheckState.Idle)
        {
            if (holeFailDay < 0) holeFailDay = campaign.Day;
            target.ReceiveDamage(new DamageInfo { amount = 1f, dealer = gm.player, source = DamageSource.Hit });
            Note($"black hole hit once on day {campaign.Day} ({(toFail ? "to fail the check" : "to pass it")}): {campaign.CheckState}, needs {campaign.CheckRequired:0} within {campaign.CheckSecondsLeft:0.0} s, afternoon phase {gm.RunPhase}");
            return true;
        }
        Shot($"16c-hole-check-day{campaign.Day}");
        if (!toFail && Held("hole-pass" + campaign.Day, 2.5f))
            target.ReceiveDamage(new DamageInfo { amount = campaign.CheckRequired, dealer = gm.player, source = DamageSource.Hit });
        return true;
    }

    /// <summary>The study window shown once on its Armament and Energy tabs (once both rooms are built), and once on its Void tab after the
    /// Nash Prophet is beaten, each captured, then closed. True while it is busy.</summary>
    static bool StudyCaptures()
    {
        var c = campaign;
        float now = Time.realtimeSinceStartup;
        if (studyStage < 4 && c.Room(CampaignRoom.Armament).level >= 1 && c.Room(CampaignRoom.Energy).level >= 1)
        {
            switch (studyStage)
            {
                case 0: c.Ui.OpenStudy("arm.upgrade"); studyStage = 1; wait = now + 1f; return true;
                case 1: Shot("11s-study-armament"); studyStage = 2; wait = now + .5f; return true;
                case 2: c.Ui.OpenStudy("eng.body"); studyStage = 3; wait = now + 1f; return true;
                default: Shot("11s-study-energy"); studyStage = 4; wait = now + .5f; return true;
            }
        }
        if (studyStage == 4) { studyStage = 5; c.Ui.CloseStudy(); Note($"study window: {c.BuildStudy().Count} tabs, closed again (base panel open {c.Ui.BaseOpen})"); }
        if (voidStudyStage < 2 && c.ProphetBeaten)
        {
            string key = null;
            foreach (var tab in c.BuildStudy()) if (tab.key.StartsWith("void.")) { key = tab.key; break; }
            if (key == null) { voidStudyStage = 3; Note("PROBLEM: the study window has no Void tab after the Nash Prophet was beaten"); return false; }
            if (voidStudyStage == 0) { c.Ui.OpenStudy(key); voidStudyStage = 1; wait = now + 1f; return true; }
            Shot("11v-study-void"); voidStudyStage = 2; wait = now + .5f; return true;
        }
        if (voidStudyStage == 2) { voidStudyStage = 3; c.Ui.CloseStudy(); }
        return false;
    }

    /// <summary>Measure: a day's counters start (BeginMeasuredDay), the reference player kills (MeasuredDay) and the day's line is
    /// written (EndMeasuredDay).</summary>
    static void BeginMeasuredDay(GameManager gm)
    {
        if (!measure) return;
        damageBank = 0f; elitesKilled = 0; killedByKind.Clear(); hpByKind.Clear();
        lastGameTime = Time.time; dayStartedAt = Time.time;
        baseAtDawn = campaign.BaseHp; shieldAtDawn = campaign.BaseShield; energyAtDawn = campaign.EarnedEnergy;
        Teleport(gm, campaign.DoorPoint + new Vector3(0f, 0f, -3f));
    }

    static void MeasuredDay(GameManager gm)
    {
        gm.player.Heal(gm.player.maxHp);
        if (gm.RunPhase == ArenaUiState.WeaponIntro) { Cmd(gm, ArenaUiCommand.DismissWeaponIntro); return; }
        float dt = Mathf.Max(0f, Time.time - lastGameTime);
        lastGameTime = Time.time;
        if (campaign.Stage != CampaignStage.Day || gm.RunPhase != ArenaUiState.WaveActive) return;
        damageBank += campaign.rules.DayReferenceDps(campaign.Day) * dt;
        // The reference player takes the monster nearest the base, one at a time; damage is not saved up while nothing stands.
        for (int guard = 0; guard < 8; guard++)
        {
            Enemy target = null;
            float nearest = float.MaxValue;
            foreach (var e in gm.waves.Alive)
            {
                if (!e || !e.IsAlive || e.IsBoss) continue;
                float d = Query.Flat(e.Position - V2Campaign.BaseCentre).sqrMagnitude;
                if (d < nearest) { nearest = d; target = e; }
            }
            if (!target) { damageBank = 0f; return; }
            var status = ElementStatus.For(target);
            float need = target.Hp + (status ? status.Shield : 0f);
            if (damageBank < need) return;
            damageBank -= need;
            var kind = target.definition ? target.definition.kind : EnemyKind.Grunt;
            killedByKind[kind] = (killedByKind.TryGetValue(kind, out int n) ? n : 0) + 1;
            if (!hpByKind.ContainsKey(kind) || target.Elite) hpByKind[kind] = target.maxHp;
            if (target.Elite) elitesKilled++;
            target.Kill(gm.player);
        }
    }

    static void EndMeasuredDay(GameManager gm)
    {
        if (!measure) return;
        if (campaign.PickupCount > 0) campaign.DebugCollectPickups();
        var kinds = new List<string>();
        int kills = 0;
        foreach (var pair in killedByKind) { kinds.Add($"{pair.Key} x{pair.Value} (hp {hpByKind[pair.Key]:0})"); kills += pair.Value; }
        float lost = baseAtDawn - campaign.BaseHp, shieldLost = shieldAtDawn - campaign.BaseShield;
        Note($"MEASURE day {day}: morning {Time.time - dayStartedAt:0} s of game time, reference dps {campaign.rules.DayReferenceDps(day):0}, kills {kills} (elites {elitesKilled}): {string.Join(", ", kinds)}; "
             + $"base lost {lost:0} hp of {campaign.BaseMaxHp:0} ({(campaign.BaseMaxHp > 0f ? lost / campaign.BaseMaxHp * 100f : 0f):0}%) and {shieldLost:0} shield; "
             + $"energy earned today {campaign.EarnedEnergy - energyAtDawn} (table {campaign.rules.DayKillEnergy(day)}), player energy {gm.progression.Energy:0}/{gm.progression.Stats.maxEnergy:0}");
    }

    static List<string> BrokenRooms()
    {
        var list = new List<string>();
        foreach (CampaignRoom room in Enum.GetValues(typeof(CampaignRoom))) if (campaign.Room(room).damaged) list.Add(room.ToString());
        return list;
    }
}

}
