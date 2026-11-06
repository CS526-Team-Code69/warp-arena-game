                                                                                                                                 
                                                                                                                                        
                                                                                                                                      
                                                                                                                                      
                                                                                       
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
    static float wait, startedAt, stepSince, lastAct, nightSince;
    static bool ended, shotTaken, raidShot, shipTabOpened, sideBroken, darkNodeShot, nightDeathDone, deathWalletCheck, readyConfirm;
    static readonly Dictionary<Enemy, float> raiderSince = new();
    static readonly HashSet<string> shots = new();
    static V2Campaign campaign;
    static CampaignStage lastStage = CampaignStage.Off;
    static ArenaUiState lastPhase = ArenaUiState.Title;

    [MenuItem("Anomaly Arena/V2 Campaign/Run Play-mode check")]
    public static void Begin()
    {
        Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../work/v2-campaign-1106/captures"));
        Directory.CreateDirectory(Folder);
        logPath = Path.Combine(Folder, "driver-log.txt");
        File.WriteAllText(logPath, "");
        step = 0; day = 0; wait = 0f; ended = false; shots.Clear(); raiderSince.Clear(); raidShot = false; lastStage = CampaignStage.Off; campaign = null;
        shipTabOpened = sideBroken = darkNodeShot = nightDeathDone = deathWalletCheck = false; diedAtNight = -1;
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
    }

    static void Tick()
    {
        try { Advance(); }
        catch (Exception e) { UnityEngine.Debug.LogError(Tag + "step " + step + ": " + e); End("error at step " + step + ": " + e.Message); }
    }

    static void Teleport(GameManager gm, Vector3 spot)
    {
        spot.y = 0f;
        if (WorldMap.Active) spot = WorldMap.Active.ClampPoint(spot, 3f);                                                            
        var body = gm.player.GetComponent<Rigidbody>();
        if (body) { body.position = spot; body.linearVelocity = Vector3.zero; }
        gm.player.transform.position = spot;
        Physics.SyncTransforms();
    }

    static void Cmd(GameManager gm, ArenaUiCommand command, int index = -1) => gm.ExecuteUiCommand(command, index);

    static string Wallet => $"A{campaign.Alloy} E{campaign.Energy} D{campaign.Dark}";

    static void Watch(GameManager gm)
    {
        if (campaign.Stage != lastStage) { Note($"stage {lastStage} -> {campaign.Stage} (day {campaign.Day}, phase {gm.RunPhase}, wallet {Wallet}, base {campaign.BaseHp:0}/{campaign.BaseMaxHp:0}, shield {campaign.BaseShield:0})"); lastStage = campaign.Stage; }
        if (gm.RunPhase != lastPhase) { lastPhase = gm.RunPhase; }
    }

                                                                                     
    static void Shop()
    {
        var c = campaign;
        int boughtResearch = 0, boughtEnergyPicks = 0, boughtArmamentPicks = 0;
        foreach (CampaignRoom room in Enum.GetValues(typeof(CampaignRoom))) if (c.Room(room).damaged && c.TryRepairRoom(room)) Note($"repaired {room}");
        bool any = true; int guard = 0;
        while (any && guard++ < 40)
        {
            any = false;
            foreach (var room in new[] { CampaignRoom.Armament, CampaignRoom.Energy, CampaignRoom.Ship })
                if (c.NextLevelCost(room) != null && c.TryUpgradeRoom(room)) { Note($"bought {room} level {c.Room(room).level} -> wallet {Wallet}"); any = true; }
            if (c.TryRepairEngine()) { Note("repaired the engine"); any = true; }
            if (c.TryRepairArmor()) { Note("repaired the armor"); any = true; }
            if (c.TrySynthesiseFuel()) { Note($"fuel {c.Fuel}/{c.rules.fuelUnits}"); any = true; }
            if (c.TryUpgradeDurability()) { Note($"durability {c.Durability} (base max {c.BaseMaxHp:0})"); any = true; }
            if (c.TryUpgradeShield()) { Note($"shield level {c.ShieldLevel} (cap {c.BaseMaxShield:0})"); any = true; }
            if (c.TryBuyPlayerLevel()) { var gm = GameManager.Instance; Note($"bought player level {gm.progression.Level}: max HP {gm.player.maxHp:0}, max energy {gm.progression.Stats.maxEnergy:0} -> wallet {Wallet}"); any = true; }
                                                                                                                                     
            if (boughtResearch < 2 && c.TryBuyWeaponResearch()) { boughtResearch++; Note($"bought weapon research (booked {c.ResearchQueued}) -> wallet {Wallet}"); any = true; }
            if (boughtEnergyPicks < 2 && c.TryBuyCardPick(CampaignRoom.Energy)) { boughtEnergyPicks++; Note($"bought an Energy Lab card pick -> wallet {Wallet}"); any = true; }
            if (boughtArmamentPicks < 2 && c.TryBuyCardPick(CampaignRoom.Armament)) { boughtArmamentPicks++; Note($"bought an Armament Bay card pick -> wallet {Wallet}"); any = true; }
        }
        if (c.LampStock < 3 && c.TryCraftLamp()) Note("crafted a lamp");
        if (c.BaseHp < c.BaseMaxHp - 1f && c.TryRepairHull()) Note("repaired the hull");
    }

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
                foreach (var w in waves.waves) w.duration = 7f;
                gm.waves.waveSet = waves;
                var rules = Object.Instantiate(campaign.rules);
                rules.duskSeconds = 9f;
                campaign.rules = rules;
                Art.Enabled = true;
                Note($"boot: phase {gm.RunPhase}, scene {gm.gameObject.scene.name}, game view {Screen.width}x{Screen.height}, map {WorldMap.Active.biome?.displayName}, art {(campaign.art ? "set" : "none")}; wave timers 7 s and dusk 9 s for this check only (copies, assets untouched)");
                Shot("01-title");
                Go(1, 1.5f);
                return;
            case 1:
                if (campaign.Stage == CampaignStage.Off) { if (act) Cmd(gm, ArenaUiCommand.StartCampaign); if (InStep > 10f) End("StartCampaign never took"); return; }
                if (InStep > 1.2f) { Shot("02-day0-card"); Go(2); }
                return;
            case 2:
                if (campaign.Stage == CampaignStage.Tutorial) { Go(3, 1.5f); return; }
                if (!act) return;
                if (gm.RunPhase == ArenaUiState.SupplyDrop && gm.SupplyDropReady) Cmd(gm, ArenaUiCommand.OpenSupply);
                else if (gm.RunPhase == ArenaUiState.Loadout) Cmd(gm, ArenaUiCommand.ChooseLoadout, 1);
                else if (gm.RunPhase == ArenaUiState.WeaponIntro) Cmd(gm, ArenaUiCommand.DismissWeaponIntro);
                if (InStep > 40f) End("no tutorial after the loadout");
                return;
            case 3:
                Shot("03-crash-site");
                if (campaign.Stage != CampaignStage.Tutorial) { campaign.debugHoldE = false; Go(4); return; }
                if (!act) return;
                gm.player.Heal(gm.player.maxHp);
                var alive = new List<Enemy>();
                foreach (var e in gm.waves.Alive) if (e && e.IsAlive) alive.Add(e);
                if (alive.Count > 0)
                {
                    Shot("04-first-monsters");
                    if (InStep > 2.5f) foreach (var e in alive) e.Kill(gm.player);
                    return;
                }
                if (campaign.PickupCount > 0) { campaign.DebugCollectPickups(); return; }
                if (campaign.NodesLeft(V2Campaign.NodeKind.Ore) > 0) { MineNearest(gm, V2Campaign.NodeKind.Ore); return; }
                campaign.debugHoldE = false;
                if (campaign.ObjectivePoint is Vector3 goal)
                {
                    Vector3 from = gm.player.Position;
                    Teleport(gm, Query.Flat(goal - from).magnitude > 2.5f ? goal + Query.Flat(from - goal).normalized * 1.5f : goal);
                }
                else Teleport(gm, gm.player.Position + Vector3.right * 6f);
                stepSince = Time.realtimeSinceStartup;
                return;
            case 4:                                     
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
            case 6:                  
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
                                                                                    
                    MineNearest(gm, V2Campaign.NodeKind.Ore); return;
                }
                campaign.debugHoldE = false;
                Note($"night 0 mined: wallet {Wallet}, shadows {campaign.ShadowCount}");
                Teleport(gm, campaign.DoorPoint);
                campaign.debugPressE = true;
                Go(7, .6f);
                return;
            case 7:                                    
                if (campaign.Stage != CampaignStage.Base) { if (InStep > 8f) { Note($"expected the base, stage {campaign.Stage}"); Go(9); } return; }
                if (campaign.BaseAtDawn)
                {
                                                                                                                 
                    if (campaign.Day == 1 && !shipTabOpened)
                    {
                        shipTabOpened = true;
                        campaign.Ui.ShowTab(CampaignRoom.Ship);
                        wait = Time.realtimeSinceStartup + 1.2f;
                        return;
                    }
                    if (campaign.Day == 1 && shipTabOpened && !shots.Contains("12s-dawn1-ship-live-view"))
                    {
                        Note($"ship tab: live view {(campaign.ShipViewTexture ? "drawn" : "missing")}");
                        Shot("12s-dawn1-ship-live-view");
                        wait = Time.realtimeSinceStartup + .6f;
                        return;
                    }
                    int last = campaign.rules.days;
                    if (campaign.Day >= last - 2 && campaign.Day < last)
                    {
                        campaign.DebugGrant(400, 400, 60);
                        Note($"DEBUG GRANT on the dawn after night {campaign.Day}: +400 alloy +400 energy +60 dark matter, so the check reaches the ending on day {last}");
                    }
                    Shop();
                    Shot($"12-dawn{campaign.Day}");
                    if (campaign.Day >= campaign.rules.days && campaign.CanIgnite) { Note("ignition ready on the last dawn"); campaign.Ignite(); Go(20); return; }
                    campaign.LeaveBase();
                    Go(8, .5f);
                    return;
                }
                Shot($"11-base-night{campaign.Day}");
                Shop();
                campaign.WaitForDawn();
                return;
            case 8:                                       
                if (campaign.Stage == CampaignStage.Day) { day = campaign.Day; raiderSince.Clear(); raidShot = false; Note($"day {day} begins: level {gm.progression.Level}, banked picks {gm.progression.BankedDrafts}, weapons {gm.runWeapons.Hand[0]?.Label} / {gm.runWeapons.Hand[1]?.Label}"); Go(9); return; }
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
                                                                                                                                                       
                    case ArenaUiState.Ready: readyConfirm = !readyConfirm; Cmd(gm, readyConfirm ? ArenaUiCommand.StartNextWave : ArenaUiCommand.ConfirmEarlyStart); break;
                }
                if (InStep > 60f) End($"stuck in the dawn build at {gm.RunPhase}");
                return;
            case 9:       
                if (campaign.Stage == CampaignStage.Dusk) { Note($"day {day} over: base {campaign.BaseHp:0}/{campaign.BaseMaxHp:0}, broken {string.Join(",", BrokenRooms())}, wallet {Wallet}, level {gm.progression.Level}, kills {gm.waves.Kills}"); Go(10, 1f); return; }
                if (gm.State != GameState.Playing) { End($"run ended in the day: {gm.State}, base destroyed {campaign.BaseDestroyed}"); return; }
                gm.player.Heal(gm.player.maxHp);
                if (gm.RunPhase == ArenaUiState.LevelUp && act) { Note($"mid-day pick: {string.Join(" | ", Array.ConvertAll(gm.progression.CurrentCards, c => c.Kind + ":" + c.Title))}"); Cmd(gm, ArenaUiCommand.ChooseCard, 0); return; }
                if (gm.RunPhase == ArenaUiState.WeaponIntro && act) { Cmd(gm, ArenaUiCommand.DismissWeaponIntro); return; }
                if (gm.RunPhase == ArenaUiState.ReplaceSkill && act) { Cmd(gm, ArenaUiCommand.ReplaceSkill, 0); return; }
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
                                                                                                                                                
                        if (!raiderSince.TryGetValue(e, out float first)) raiderSince[e] = atBase ? now : now + 10f;
                        else if (atBase && first > now) raiderSince[e] = now;
                        if (raiderSince.TryGetValue(e, out float since) && Time.realtimeSinceStartup - since > 2f) e.Kill(gm.player);                                      
                        else if (!atBase && Query.Flat(e.Position - gm.player.Position).magnitude > 60f) e.Kill(gm.player);
                        continue;
                    }
                    e.Kill(gm.player);
                }
                if (!raidShot && raiderSince.Count > 0 && day <= 2) { raidShot = true; Teleport(gm, V2Campaign.BaseCentre + new Vector3(-9f, 0f, -9f)); Shot($"13-day{day}-raid"); }
                                                                                                                                       
                                                           
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
            case 10:        
                Shot($"14-dusk{day}");
                if (campaign.Stage == CampaignStage.Night) { campaign.debugHoldE = false; nightSince = Time.realtimeSinceStartup; Note($"night {day}: shadows {campaign.ShadowCount}, overrun regions {campaign.OverrunRegions}, dark nodes {campaign.NodesLeft(V2Campaign.NodeKind.Dark)}, ore left {campaign.NodesLeft(V2Campaign.NodeKind.Ore)}"); Go(11, 1f); return; }
                if (act && campaign.PickupCount > 0) campaign.DebugCollectPickups();
                if (act) MineNearest(gm, V2Campaign.NodeKind.Ore);
                return;
            case 11:         
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
                    Note($"night {day} haul: wallet {Wallet}, hp {gm.player.Hp:0}/{gm.player.maxHp:0}, lamps left {campaign.LampStock}");
                    Teleport(gm, campaign.DoorPoint);
                    campaign.debugPressE = true;
                    return;
                }
                if (campaign.TryNearestNode(V2Campaign.NodeKind.Dark, gm.player.Position, out var dark))
                {
                    if (Query.Flat(dark - gm.player.Position).magnitude > 6f)
                    {
                                                                              
                        Teleport(gm, dark + new Vector3(1.2f, 0f, 0f));
                        if (campaign.LampStock > 0) campaign.debugPressF = true;
                        return;
                    }
                    MineNearest(gm, V2Campaign.NodeKind.Dark);
                    if (!darkNodeShot && day >= 1) { darkNodeShot = true; Shot($"15b-night{day}-dark-node"); }
                }
                return;
            case 20:                           
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

    static List<string> BrokenRooms()
    {
        var list = new List<string>();
        foreach (CampaignRoom room in Enum.GetValues(typeof(CampaignRoom))) if (campaign.Room(room).damaged) list.Add(room.ToString());
        return list;
    }
}

}
