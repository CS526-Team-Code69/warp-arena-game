// V2 day campaign: "can a monster standing by the base or the ship be shot?" (2026-11-10). Menu: Anomaly Arena/V2 Campaign/Run wall
// check (and its whitebox twin); enter Play mode on the title first. It starts the story mode and, on Day 0 (the base and the wreck
// stand from the start), puts one grunt against the base at eight bearings and against the ship at four, fires the pistol at each from a
// slant, and writes whether each was hit to work/handover-1109/captures-1110/wallcheck-<art>.txt, with where the shot wall and the body
// wall lie on each bearing (seen from 9.5 cells out, so a neighbouring wall is not taken for the one meant). One capture per art.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
public static class V2WallCheck
{
    const float LookFrom = 9.5f;
    static string folder, logPath;
    static int step, index, hits, misses;
    static float stepSince, caseSince, startedAt;
    static bool whitebox, fired, shotTaken;
    static V2Campaign campaign;
    static Enemy target;
    static float hpBefore;
    static readonly List<(string name, Vector3 centre, float bearing, float reach)> cases = new();

    [MenuItem("Anomaly Arena/V2 Campaign/Run wall check")]
    public static void Begin() { whitebox = false; Start(); }

    [MenuItem("Anomaly Arena/V2 Campaign/Run wall check (whitebox)")]
    public static void BeginWhitebox() { whitebox = true; Start(); }

    static void Start()
    {
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../work/handover-1109/captures-1110"));
        Directory.CreateDirectory(folder);
        logPath = Path.Combine(folder, whitebox ? "wallcheck-whitebox.txt" : "wallcheck-illustrated.txt");
        File.WriteAllText(logPath, "");
        step = 0; index = 0; hits = misses = 0; target = null; campaign = null; shotTaken = false;
        startedAt = stepSince = Time.realtimeSinceStartup;
        cases.Clear();
        cases.Add(("control: open ground", V2Campaign.BaseCentre + new Vector3(-32f, 0f, -4f), 0f, 2f));
        for (int i = 0; i < 8; i++) cases.Add(("base " + i * 45 + " deg", V2Campaign.BaseCentre, i * 45f, 0f));
        for (int i = 0; i < 4; i++) cases.Add(("ship " + i * 90 + " deg", V2Campaign.ShipCentre, i * 90f, 0f));
        EditorApplication.LockReloadAssemblies();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Note("wall check started, shown as " + (whitebox ? "whitebox" : "illustrated"));
    }

    static void Note(string line) => File.AppendAllText(logPath, $"{Time.realtimeSinceStartup - startedAt,6:0.0}s {line}\n");

    static void End(string why)
    {
        EditorApplication.update -= Tick;
        EditorApplication.UnlockReloadAssemblies();
        PlayerController.InputGate = null;
        Note($"END {why}: hit {hits}, not hit {misses}, of {cases.Count}");
    }

    static void Teleport(GameManager gm, Vector3 spot)
    {
        spot.y = 0f;
        var body = gm.player.GetComponent<Rigidbody>();
        if (body) { body.position = spot; body.linearVelocity = Vector3.zero; }
        gm.player.transform.position = spot;
        Physics.SyncTransforms();
    }

    static Vector3 Dir(float degrees) => new(Mathf.Sin(degrees * Mathf.Deg2Rad), 0f, Mathf.Cos(degrees * Mathf.Deg2Rad));

    /// <summary>How far from centre the first collider of the layer lies along the bearing, seen from LookFrom cells out at the height
    /// shots fly; -1 where none lies within.</summary>
    static float WallAt(Vector3 centre, float bearing, int layer)
    {
        Vector3 dir = Dir(bearing), from = centre + dir * LookFrom + Vector3.up * Query.CastHeight;
        return Physics.Raycast(from, -dir, out var hit, LookFrom, 1 << layer, QueryTriggerInteraction.Ignore) ? LookFrom - hit.distance : -1f;
    }

    static void Tick()
    {
        try { Advance(); }
        catch (System.Exception e) { Note("ERROR " + e); End("error"); }
    }

    static void Advance()
    {
        if (!EditorApplication.isPlaying) { End("play mode ended"); return; }
        if (Time.realtimeSinceStartup - startedAt > 180f) { End("timed out at step " + step); return; }
        var gm = GameManager.Instance;
        float now = Time.realtimeSinceStartup;
        if (step == 0)
        {
            if (!gm || !ArenaText.Ready || !WorldMap.Active) return;
            campaign = gm.GetComponent<V2Campaign>();
            Art.Enabled = !whitebox;
            gm.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
            step = 1; stepSince = now;
            return;
        }
        if (step == 1)
        {
            if (campaign.Stage == CampaignStage.Off && now - stepSince > 1f) { gm.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1); stepSince = now; }
            if (campaign.Stage != CampaignStage.Tutorial) { if (campaign.Stage != CampaignStage.Off) stepSince = now; return; }
            if (now - stepSince < 1.5f) return;
            PlayerController.InputGate = _ => false; // the check gives the weapon its input itself
            Art.Enabled = !whitebox;
            step = 2; stepSince = now;
            return;
        }
        if (index >= cases.Count) { End("all cases done"); return; }
        var c = cases[index];
        gm.player.Heal(gm.player.maxHp);
        if (target == null)
        {
            float shotWall = c.reach > 0f ? -1f : WallAt(c.centre, c.bearing, GameManager.WallLayer);
            float bodyWall = c.reach > 0f ? -1f : WallAt(c.centre, c.bearing, Combatant.BodyOnlyWallLayer);
            float wall = c.reach > 0f ? c.reach : Mathf.Max(shotWall, bodyWall);
            if (wall < 0f) { Note($"{c.name}: no wall within {LookFrom} cells on this bearing"); index++; return; }
            // The grunt as close to the wall as a body gets; the player 9 cells further out and 30 degrees round, so the shot runs at a
            // slant past the wall.
            target = gm.waves.SpawnCampaign(EnemyKind.Grunt, c.centre + Dir(c.bearing) * (wall + .75f));
            if (!target) { Note($"{c.name}: no grunt"); index++; return; }
            hpBefore = target.Hp;
            Teleport(gm, c.centre + Dir(c.bearing + 30f) * (wall + 9f));
            caseSince = now; fired = false;
            Note($"{c.name}: shot wall at {shotWall:0.00}, body wall at {bodyWall:0.00} from the centre; grunt placed at {wall + .75f:0.00}; weapon {gm.runWeapons.Active?.Label}");
            return;
        }
        bool dead = !target || !target.IsAlive;
        float lost = dead ? hpBefore : hpBefore - target.Hp;
        if (!dead)
        {
            gm.player.DebugSetAimPoint(target.Position);
            if (now - caseSince > .35f) { gm.runWeapons.HandleInput(!fired, true, false); fired = true; }
            if (!shotTaken && index == 2 && now - caseSince > .5f)
            {
                shotTaken = true;
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, whitebox ? "wallcheck-whitebox.png" : "wallcheck-illustrated.png"));
            }
        }
        if (!dead && lost <= 0f && now - caseSince <= 3f) return;
        bool hit = dead || lost > 0f;
        if (hit) hits++; else misses++;
        float gap = target ? Query.Flat(target.Position - c.centre).magnitude : -1f;
        Note($"{c.name}: {(hit ? "HIT" : "NOT HIT")} after {now - caseSince:0.00}s (lost {lost:0.#} hp; grunt then {gap:0.00} from the centre)");
        gm.runWeapons.HandleInput(false, false, true);
        if (target && target.IsAlive) target.Kill(gm.player);
        target = null;
        index++;
    }
}
}
