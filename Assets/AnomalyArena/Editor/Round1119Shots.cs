using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// Screenshots for the round of 2026-11-19, taken in the editor's Play mode: enter Play, wait for the title, then pick one of the
    /// menus (a fresh domain for each: RequestScriptReload while stopped, then Play). Story: the base tour's four steps on Day 0's arrival,
    /// Day 1's afternoon crate, an energy shard and the dark matter core beside the player, and the character screen. Open mode: the
    /// supply crate falling and landed, the battle HUD with its hint, the character screen and the void trial's platform with the player
    /// on it. Some things are put in front of the camera through the campaign's own private methods (the arrival, an energy shard, the
    /// core) rather than played up to. Saved to work/gold-1117/captures-1120/ with a log beside them; script reloads are held meanwhile.
    /// </summary>
    static class Round1119Shots
    {
        const string Tag = "[R1119Shots] ";
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static string folder, logPath;

        [MenuItem("Anomaly Arena/Checks/Round 11-19 shots (story)")]
        static void Story() => Launch("story", StoryRun);

        [MenuItem("Anomaly Arena/Checks/Round 11-19 shots (open mode)")]
        static void Open() => Launch("open", OpenRun);

        static async void Launch(string name, Func<Task> run)
        {
            if (!EditorApplication.isPlaying) { Debug.LogError(Tag + "enter Play and wait for the title first"); return; }
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../work/gold-1117/captures-1120"));
            Directory.CreateDirectory(folder);
            logPath = Path.Combine(folder, "shots-log-" + name + ".txt");
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

        static async Task StoryRun()
        {
            var gm = GameManager.Instance;
            var campaign = gm.GetComponent<V2Campaign>();
            await Until(() => ArenaText.Ready, 20f, "localization");
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!gm.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("StartCampaign never took, phase " + gm.RunPhase);
                gm.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Frame();
            }
            await Until(() => campaign.Stage == CampaignStage.Tutorial && gm.State == GameState.Playing, 20f, "Day 0's walk");
            gm.player.ProtectFor(900f);

            // The arrival at the end of Day 0, then the base tour as a player would take it.
            var arrival = (IEnumerator)typeof(V2Campaign).GetMethod("Arrival", Private).Invoke(campaign, null);
            campaign.StartCoroutine(arrival);
            var ui = campaign.Ui;
            await Until(() => ui.BaseTourStepNow == BaseTourStep.Control, 45f, "the tour's first step");
            await Seconds(1.6f);
            await Shot("t1-tour-control");
            var control = ui.transform.Find("BasePanel/Page5/ZoneControl");
            var button = control ? control.GetComponentInChildren<UnityEngine.UI.Button>(true) : null;
            if (!button) throw new Exception("no button on BasePanel/Page5/ZoneControl");
            button.onClick.Invoke();
            await Until(() => ui.BaseTourStepNow == BaseTourStep.ArmamentZone, 10f, "the tour's Armament Bay step");
            await Seconds(1.8f);
            await Shot("t2-tour-armament");
            ui.ShowTab(CampaignRoom.Armament);
            await Until(() => ui.BaseTourStepNow == BaseTourStep.BuildArmament, 10f, "the tour's build step");
            await Seconds(1.8f);
            await Shot("t3-tour-build");
            campaign.TryUpgradeRoom(CampaignRoom.Armament);
            await Until(() => ui.BaseTourStepNow == BaseTourStep.OpenStudy, 10f, "the tour's study step");
            await Seconds(1.8f);
            await Shot("t4-tour-study");

            // Day 1's afternoon: its crate on the ore trail, then an energy shard and the core put beside the player.
            // The afternoon begins at the base door, but the debug call leaves Day 0's base page up: it is closed as leaving would.
            campaign.DebugBeginAfternoon(1);
            ui.HideBase();
            ui.HideSpotlight();
            await Seconds(1.2f);
            if (campaign.TryNearestCrate(gm.player.Position, out var crateAt))
            {
                campaign.DebugPlacePlayer(crateAt + new Vector3(-2.5f, 0f, -2f));
                await Seconds(1.2f);
                await Shot("s1-weapon-crate");
            }
            else Note("no crate on the map for the afternoon");
            var here = Query.Flat(gm.player.Position);
            var pickupKind = typeof(V2Campaign).GetNestedType("PickupKind", BindingFlags.NonPublic);
            typeof(V2Campaign).GetMethod("SpawnPickup", Private).Invoke(campaign, new object[] { Enum.Parse(pickupKind, "Energy"), 3, here + new Vector3(4f, 0f, 1.5f) });
            typeof(V2Campaign).GetMethod("AddNode", Private).Invoke(campaign, new object[] { V2Campaign.NodeKind.Core, here + new Vector3(-3f, 0f, 1.8f), 1, false });
            await Seconds(1.2f);
            await Shot("s2-energy-shard-and-core");

            // The character screen as the I key opens it, after a few study purchases so it has something to list: the HUD with
            // its hint first, then the page.
            campaign.DebugGrant(160, 160, 0);
            Note($"study: weapon Mk {campaign.TryBuyWeaponDamage()}, armor {campaign.TryBuyPlayerLevel()}, energy lab {campaign.TryUpgradeRoom(CampaignRoom.Energy)}");
            await Seconds(.6f);
            await Shot("s3-hud-hint");
            await Character("s4-character-story");
        }

        static async Task OpenRun()
        {
            var gm = GameManager.Instance;
            var world = WorldMap.Active;
            await Until(() => ArenaText.Ready, 20f, "localization");
            await Command(gm, ArenaUiCommand.Start, () => gm.RunPhase == ArenaUiState.SupplyDrop || gm.RunPhase == ArenaUiState.Loadout);
            if (gm.RunPhase == ArenaUiState.SupplyDrop)
            {
                await Seconds(.35f);
                await Shot("o1-supply-crate-falling");
                await Until(() => gm.SupplyDropReady || gm.RunPhase != ArenaUiState.SupplyDrop, 6f, "the crate lands");
                if (gm.RunPhase == ArenaUiState.SupplyDrop) await Shot("o2-supply-crate-landed");
                if (CrateNote() is Vector2 crateOnScreen) await ClickAt(crateOnScreen, "the crate's model");
                await Command(gm, ArenaUiCommand.OpenSupply, () => gm.RunPhase == ArenaUiState.Loadout);
            }
            await Command(gm, ArenaUiCommand.ChooseLoadout, () => gm.RunPhase == ArenaUiState.WeaponIntro, 1);
            await Command(gm, ArenaUiCommand.DismissWeaponIntro, () => gm.RunPhase != ArenaUiState.WeaponIntro);
            gm.player.ProtectFor(900f);
            float deadline = Time.realtimeSinceStartup + 20f;
            while (gm.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("the first wave never started, phase " + gm.RunPhase);
                if (gm.RunPhase == ArenaUiState.Cutscene) world.SkipHoleIntro();
                await Frame();
            }
            await Seconds(1.5f);
            await Shot("o3-hud-hint");

            await Character("o3b-character-open");

            var hole = world.holes.First(h => h && h.Phase == HolePhase.Active);
            hole.Feed(hole.feedsToOpen, gm.waves.WaveIndex + 1);
            hole.Tick(2f);
            Teleport(gm.player, hole.Centre);
            if (!world.TryEnterPortal(gm.player)) throw new Exception("the open portal did not take the player");
            await Seconds(3f);
            await Shot("o4-void-platform");
        }

        /// <summary>Opens the character screen as its key would, shoots it, notes what it lists, and closes it again.</summary>
        static async Task Character(string name)
        {
            await Until(() => V5CharacterScreen.CanOpen, 8f, "the character screen may open");
            if (!V5CharacterScreen.Open()) throw new Exception("the character screen did not open");
            await Seconds(1f);
            await Shot(name);
            Note("rows: " + string.Join(" | ", V5CharacterScreen.Rows.Select(r => r.id + "=" + r.value)));
            V5CharacterScreen.Close();
            await Seconds(.6f);
            Note($"closed: open {V5CharacterScreen.IsOpen}, phase {GameManager.Instance.RunPhase}, time scale {Time.timeScale}");
        }

        /// <summary>The Game view with its interface, written at the end of this frame; the next frame starts after it.</summary>
        static async Task Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            Note("capture " + name);
            await Frame();
            await Frame();
        }

        /// <summary>Notes whether the supply crate's model is up and where it and the crate's picture (the click area) are on the
        /// screen, in pixels from the bottom left, so the click area can be checked against the model. Returns the model's middle
        /// on the screen, or null without a model.</summary>
        static Vector2? CrateNote()
        {
            var drop = UnityEngine.Object.FindAnyObjectByType<V3SupplyDropPresentation>();
            if (!drop || !drop.crateRoot || !drop.worldCamera) { Note("crate: no supply-drop presentation"); return null; }
            var model = drop.crateRoot.Find("CrateModel");
            string modelRect = "-";
            Vector2? centre = null;
            if (model && model.gameObject.activeInHierarchy)
            {
                var parts = model.GetComponentsInChildren<Renderer>();
                if (parts.Length > 0)
                {
                    var box = parts[0].bounds;
                    foreach (var part in parts) box.Encapsulate(part.bounds);
                    Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = box.center + Vector3.Scale(box.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector2 p = drop.worldCamera.WorldToScreenPoint(corner);
                        min = Vector2.Min(min, p); max = Vector2.Max(max, p);
                    }
                    modelRect = $"({min.x:0},{min.y:0})-({max.x:0},{max.y:0}), {parts.Length} renderers";
                    centre = (min + max) / 2f;
                }
            }
            string pictureRect = "-";
            if (drop.uiCrate)
            {
                var corners = new Vector3[4];
                drop.uiCrate.rectTransform.GetWorldCorners(corners);
                var canvas = drop.overlayCanvas;
                var uiCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 a = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]), b = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
                pictureRect = $"({a.x:0},{a.y:0})-({b.x:0},{b.y:0}), alpha {drop.uiCrate.color.a:0.00}, raycast {drop.uiCrate.raycastTarget}";
            }
            string cubes = string.Join(",", (drop.crateSurfaces ?? new Renderer[0]).Select(r => r && r.enabled ? "on" : "off").Distinct());
            string state = model ? (model.gameObject.activeInHierarchy ? "shown" : "hidden") : "missing";
            Note($"crate: model {state} {modelRect}; picture {pictureRect}; cubes {cubes}; art {Art.Enabled}");
            return centre;
        }

        /// <summary>A left click at a screen point as the event system would deliver it: what the interface's raycast hits there
        /// (top first), then a click on the topmost hit's click handler, and the run's phase two frames later.</summary>
        static async Task ClickAt(Vector2 at, string what)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null) { Note("click: no event system"); return; }
            var data = new UnityEngine.EventSystems.PointerEventData(events) { position = at, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            events.RaycastAll(data, hits);
            Note($"click on {what} at ({at.x:0},{at.y:0}): hits {(hits.Count == 0 ? "nothing" : string.Join(", ", hits.Take(3).Select(h => h.gameObject.name)))}");
            if (hits.Count == 0) return;
            data.pointerPressRaycast = data.pointerCurrentRaycast = hits[0];
            var handler = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject);
            if (handler) UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            await Frame();
            await Frame();
            Note($"after the click: handler {(handler ? handler.name : "none")}, phase {GameManager.Instance.RunPhase}");
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
