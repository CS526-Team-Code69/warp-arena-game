using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Fixed scene-authored label pool. Cloaked Shades do not reveal themselves through a health bar. A monster kind the player meets
    /// for the first time is named over its bar for that morning or wave, and its first body on the screen brings a one-sentence notice;
    /// the notices of this queue (Notice) show one at a time.</summary>
    public class V2WorldLabels : MonoBehaviour
    {
        [Serializable]
        public struct EnemyLabel
        {
            public RectTransform root;
            public UnityEngine.UI.Image hp, shield, affix;
            public UnityEngine.UI.Text name;
            public UnityEngine.UI.Image[] statusIcons;
            public UnityEngine.UI.Text[] statusCounts;
        }

        public V2ArtSet art;
        public EnemyLabel[] enemyLabels;
        [Tooltip("Ordinary enemies (Grunt, Skitter, Brute, Splitter, Spitter, Warden) also show their name above the bar. " +
                 "Off since 2026-11-06: only specials, elites and bosses are named, so names do not cover the models.")]
        public bool showOrdinaryEnemyNames;
        public UnityEngine.UI.Text[] holeLabels;
        public UnityEngine.UI.Text[] edgeLabels;
        [Tooltip("Follows the nearest XP orb until the player has collected any XP in the run (created by the interface installer).")]
        public UnityEngine.UI.Text orbHintLabel;
        [Tooltip("After the first pickup an edge notice still points at off-screen XP once at least this many XP orbs lie on the ground ...")]
        public int xpEdgeMinOrbs = 6;
        [Tooltip("... or once the XP on the ground is at least this share of the XP the next level needs.")] [Range(0f, 1f)]
        public float xpEdgeMinShareOfLevel = .3f;
        [Header("Offscreen black hole marker")]
        public Color holeEdgeColor = new(.78f, .36f, 1f);
        public Color holeEdgeGlow = new(1f, .84f, 1f);
        [Min(0f)] public float holeEdgePulsePerSecond = 1.8f;
        [Min(1f)] public float holeEdgeScale = 1.35f;
        [Tooltip("Canvas units kept clear above the bottom row and below the top texts for edge labels (the bottom one is set by V2HudLayout).")]
        public float edgeBottomInset = 200f, edgeTopInset = 140f;
        [Header("Execute line (Railgun Execute held)")]
        [Tooltip("Colour of an HP bar whose enemy is already below the held Execute's threshold.")]
        public Color executeBarColor = new(1f, .06f, .1f);
        [Tooltip("Colour of the tick that marks the execute threshold on every non-boss HP bar.")]
        public Color executeTickColor = Color.white;
        [Min(1f), Tooltip("Width of that tick in canvas units.")] public float executeTickWidth = 2f;
        [Header("Enemy label over a 3D model (2026-11-06)")]
        [Tooltip("Canvas units an enemy's label is raised above the top of its 3D model. The label is centred on its point and its status icons hang 17 units below it, so 18 keeps them off the model. Whitebox bodies keep the old head point and get no lift.")]
        public float modelLabelLift = 18f;
        Enemy[] bindings;
        // The 3D model of each bound enemy (looked up when the binding changes), for LabelPoint.
        V2ModelPresentation[] models;
        float refresh;
        RectTransform canvasRect;
        readonly List<Vector2> placedEdges = new();
        static readonly Color XpEdgeColor = new(.45f, .8f, 1f);
        // Created at runtime on first use: one tick per saved label, plus each bar's own colour to restore.
        UnityEngine.UI.Image[] executeTicks;
        Color[] hpColors;
        bool[] executeBelow;
        // The fifth pass's edge notices (V5BattleEdge) and the way each one placed this frame points; they sit below the plates along the top.
        /// <summary>The fifth pass's top inset in the open mode; while the story runs V5BattleHud keeps the notices lower, under its goal
        /// plate.</summary>
        internal const float FifthTopInset = 190f;
        V5BattleEdge[] fifthEdges;
        Vector2[] edgeWays;

        void Awake()
        {
            active = this;
            bindings = new Enemy[enemyLabels.Length];
            models = new V2ModelPresentation[enemyLabels.Length];
            canvasRect = GetComponentInParent<Canvas>().transform as RectTransform;
            executeTicks = new UnityEngine.UI.Image[enemyLabels.Length];
            hpColors = new Color[enemyLabels.Length];
            executeBelow = new bool[enemyLabels.Length];
            if (V5HudKit.On) DressFifthLabels();
            for (int i = 0; i < enemyLabels.Length; i++)
            {
                hpColors[i] = enemyLabels[i].hp ? enemyLabels[i].hp.color : Color.white;
                if (enemyLabels[i].name) AddNameShadow(enemyLabels[i].name);
            }
            if (V5HudKit.On) BuildFifthEdges();
        }

        void OnDestroy()
        {
            if (active == this) active = null;
        }

        // ───── A monster kind met for the first time, and the notices shown one at a time ─────

        /// <summary>Seconds a first-sight notice stays on the screen, and the pause after any notice of this queue before the next one.</summary>
        public const float FirstSightSeconds = 5f, NoticeGap = .5f;

        struct QueuedNotice
        {
            public Func<string> words;
            public float seconds;
        }

        // The kinds met for the first time on this profile (ArenaCodexProgress.MeetEnemy), each with the stretch of play it was met in
        // (PlayStretch); the kinds whose notice has been queued; the notices waiting, shown one at a time; and the time before the next.
        static readonly Dictionary<EnemyKind, int> firstSight = new();
        static readonly HashSet<EnemyKind> introduced = new();
        static readonly List<QueuedNotice> notices = new();
        static float noticeWait;
        static V2WorldLabels active;
        // The run these belong to: a new run reloads the scene and so has a new GameManager (FollowRun).
        static GameManager run;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            ForgetFirstSights();
            active = null;
            run = null;
        }

        /// <summary>A new run (a new GameManager: restarting or leaving a run reloads the scene) starts with nothing named for a first sight
        /// and no notice waiting, whenever its labels wake.</summary>
        static void FollowRun()
        {
            var gm = GameManager.Instance;
            if (run == gm) return;
            run = gm;
            ForgetFirstSights();
        }

        /// <summary>Forgets the kinds met and the notices waiting: a new run starts with none (FollowRun), and the tests clear them.</summary>
        public static void ForgetFirstSights()
        {
            firstSight.Clear();
            introduced.Clear();
            notices.Clear();
            noticeWait = 0f;
            LastShown = "";
        }

        /// <summary>A body of a kind the collection had never met was set up (Enemy.Configure). On the whole game's pages the kind is named
        /// over its bars for the stretch of play it was met in (the story's morning, the open mode's wave), with the art on as in whitebox,
        /// and its first body on the screen queues its one-sentence notice. The Grunt, every player's first monster, and the bosses, whose
        /// own plates and cutscenes name them, get neither.</summary>
        public static void FirstMeeting(EnemyKind kind)
        {
            if (!V5HudKit.On || !IntroducesOnFirstSight(kind)) return;
            FollowRun();
            if (!firstSight.ContainsKey(kind)) firstSight[kind] = PlayStretch();
        }

        /// <summary>The kinds a first sight names and introduces: every kind but the Grunt and the bosses.</summary>
        public static bool IntroducesOnFirstSight(EnemyKind kind) => kind != EnemyKind.Grunt && !EnemyRoles.IsBoss(kind);

        /// <summary>A kind met for the first time is named over its bars while the stretch of play it was met in lasts.</summary>
        public static bool NamedForFirstSight(EnemyKind kind) => firstSight.TryGetValue(kind, out int stretch) && stretch == PlayStretch();

        /// <summary>The stretch of play now: in the story the day and the part of it (a morning's waves and the lull between them are one
        /// stretch), in the open mode the wave.</summary>
        static int PlayStretch()
        {
            var campaign = V2Campaign.Active;
            if (campaign && campaign.Running)
                return 1 + campaign.Day * 32 + (campaign.Stage is CampaignStage.Day or CampaignStage.Lull ? 0 : 1 + (int)campaign.Stage);
            var gm = GameManager.Instance;
            return gm && gm.waves ? -2 - gm.waves.WaveIndex : -1;
        }

        /// <summary>Queues a notice (a one-line toast, or a banner's title and line split by a line break) for the moment play allows, after
        /// any notice of this queue still on the screen, so two never cover each other. first puts it ahead of those waiting, for a notice
        /// about what has just happened (the black hole's first sight). Without the world labels it shows at once.</summary>
        public static void Notice(Func<string> words, float seconds, bool first = false)
        {
            if (words == null) return;
            FollowRun();
            if (!active)
            {
                var hud = GameManager.Instance ? GameManager.Instance.hud : null;
                if (hud) hud.Toast(words(), Color.white, seconds);
                return;
            }
            var notice = new QueuedNotice { words = words, seconds = seconds };
            if (first) notices.Insert(0, notice);
            else notices.Add(notice);
        }

        /// <summary>How many notices of this queue wait to show.</summary>
        public static int NoticesWaiting => notices.Count;

        /// <summary>The words of the last notice this queue showed (empty before any), for checks.</summary>
        public static string LastShown { get; private set; } = "";

        /// <summary>A body of a kind met for the first time is on the screen: its notice is queued, once.</summary>
        static void SeeFirstSight(EnemyKind kind)
        {
            if (!firstSight.ContainsKey(kind) || !introduced.Add(kind)) return;
            Notice(() => FirstSightNotice(kind), FirstSightSeconds);
        }

        /// <summary>Shows the next notice once play runs (no card, pick, pause or cutscene over it) and the last one has had its time.</summary>
        static void TickNotices(GameManager gm)
        {
            noticeWait = Mathf.Max(0f, noticeWait - Time.unscaledDeltaTime);
            if (noticeWait > 0f || notices.Count == 0 || !gm.hud || !gm.CanPlayerMove) return;
            var next = notices[0];
            notices.RemoveAt(0);
            string words = next.words();
            if (string.IsNullOrEmpty(words)) return;
            gm.hud.Toast(words, Color.white, next.seconds);
            LastShown = words;
            noticeWait = next.seconds + NoticeGap;
        }

        /// <summary>The sentence that introduces a kind the first time it is met: what it does and what to do about it. Empty for the kinds
        /// a first sight does not introduce.</summary>
        public static string FirstSightNotice(EnemyKind kind)
        {
            string name = KindName(kind);
            return kind switch
            {
                EnemyKind.Brute => ArenaText.Pair($"新怪物 · {name}：它走得慢，但打得疼，也更耐打。", $"New monster · {name}: it is slow, but it hits hard and takes more to bring down."),
                EnemyKind.Skitter => ArenaText.Pair($"新怪物 · {name}：它跑得最快，伤害不高，总是第一个贴上来。", $"New monster · {name}: it is the fastest one; it hits lightly but reaches you first."),
                EnemyKind.Spitter => ArenaText.Pair($"新怪物 · {name}：它隔着一段距离吐毒刺，看到它蓄力就横着走开。", $"New monster · {name}: it spits poison darts from a distance. Sidestep when it winds up."),
                EnemyKind.Warden => ArenaText.Pair($"新怪物 · {name}：它自己不出手，只给身边的怪加护盾，先打掉它。", $"New monster · {name}: it never attacks but shields the monsters near it. Kill it first."),
                EnemyKind.Spire => ArenaText.Pair($"新怪物 · {name}：它不会移动。地上的光波追上你之前，离开它的圆圈。", $"New monster · {name}: it never moves. Leave its circle before the light wave reaches you."),
                EnemyKind.Chanter => ArenaText.Pair($"新怪物 · {name}：它躲在后面让身边的怪打得更狠，先打掉它。", $"New monster · {name}: it stays back and makes nearby monsters hit harder. Kill it first."),
                EnemyKind.Bloater => ArenaText.Pair($"新怪物 · {name}：它靠近你或被打死时都会爆炸，离远一点打它。", $"New monster · {name}: it explodes when it gets close or dies. Shoot it from a distance."),
                EnemyKind.Rammer => ArenaText.Pair($"新怪物 · {name}：地上出现直线时往旁边躲，它会沿着线冲过来。", $"New monster · {name}: when a line appears on the ground, step aside. It charges along it."),
                EnemyKind.Stomper => ArenaText.Pair($"新怪物 · {name}：它会把正前方扇形里的一切跺飞，别站在它面前。", $"New monster · {name}: its stamp hurls everything in front of it. Keep out of that cone."),
                EnemyKind.Shade => ArenaText.Pair($"新怪物 · {name}：它隐身靠近，一击就能要你的命；打中它就会现形。", $"New monster · {name}: it creeps up unseen and can kill in one strike. Any hit reveals it."),
                EnemyKind.Splitter => ArenaText.Pair($"新怪物 · {name}：它被打死时会裂成两只小的。", $"New monster · {name}: it splits into two small ones when it dies."),
                _ => "",
            };
        }

        /// <summary>The fifth pass's colours on the saved labels: each life bar in the battle HUD's life red over its dark track, and the
        /// black hole's labels in the void's light violet (the names take the title's ink in RefreshEnemyData). It runs before each bar's
        /// own colour is kept (hpColors), so the execute line still turns a bar red and gives it this colour back.</summary>
        void DressFifthLabels()
        {
            foreach (var view in enemyLabels)
            {
                if (view.hp) view.hp.color = V5BattleKit.LifeTop;
                DressTrack(view.hp, view.root);
                DressTrack(view.shield, view.root);
            }
            if (holeLabels != null)
                foreach (var label in holeLabels)
                    if (label) label.color = V5HudKit.VioletHi;
        }

        /// <summary>The dark track behind a bar's fill: the fill's parent, when it has one of its own.</summary>
        static void DressTrack(UnityEngine.UI.Image fill, RectTransform root)
        {
            var bar = fill ? fill.transform.parent : null;
            if (bar && bar != root && bar.TryGetComponent<UnityEngine.UI.Image>(out var track)) track.color = V5HudKit.Track;
        }

        /// <summary>Shows or hides a bar (its fill's parent, else the fill itself).</summary>
        static void ShowBar(UnityEngine.UI.Image fill, RectTransform root, bool shown)
        {
            if (!fill) return;
            var bar = fill.transform.parent && fill.transform.parent != root ? fill.transform.parent.gameObject : fill.gameObject;
            if (bar.activeSelf != shown) bar.SetActive(shown);
        }

        /// <summary>The fifth pass's edge notices (V5BattleEdge): a triangle in the notice's own colour pointing off the screen beside a
        /// small plate with its words. The saved label keeps its words (with the arrow they start with) but is not drawn.</summary>
        void BuildFifthEdges()
        {
            if (edgeLabels == null) return;
            fifthEdges = new V5BattleEdge[edgeLabels.Length];
            edgeWays = new Vector2[edgeLabels.Length];
            for (int i = 0; i < edgeLabels.Length; i++)
            {
                if (!edgeLabels[i]) continue;
                edgeLabels[i].enabled = false;
                fifthEdges[i] = V5BattleEdge.Create(edgeLabels[i].transform, "V5Edge");
            }
            edgeTopInset = FifthTopInset;
        }

        /// <summary>The notices placed this frame (the first `placed` labels) as the fifth pass's markers: the words without the saved
        /// arrow, the triangle in the notice's colour (violet for the void, light blue for experience) pointing the way that arrow did.</summary>
        void ShowFifthEdges(int placed)
        {
            for (int i = 0; i < Mathf.Min(placed, fifthEdges.Length); i++)
            {
                var label = edgeLabels[i];
                if (!label || !fifthEdges[i]) continue;
                string text = label.text.Length > 2 && "◀▶▼▲".IndexOf(label.text[0]) >= 0 ? label.text.Substring(2) : label.text;
                fifthEdges[i].Show(text, V5TitleKit.Ink, label.color, edgeWays[i], 16f);
            }
        }

        /// <summary>Whether this enemy's bar shows the execute tick now, and whether the bar is red (below the line).</summary>
        public bool ShowsExecuteLine(Enemy enemy, out bool belowLine)
        {
            belowLine = false;
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i] != enemy) continue;
                var tick = executeTicks[i];
                belowLine = executeBelow[i];
                return tick && tick.gameObject.activeSelf;
            }
            return false;
        }

        /// <summary>Execute held: a tick at the threshold on every non-boss bar, and the bar turns red once the enemy is below it.</summary>
        void ShowExecuteLine(int index, Enemy enemy, float threshold)
        {
            var view = enemyLabels[index];
            if (!view.hp) return;
            bool show = threshold > 0f && !enemy.IsBoss;
            var tick = executeTicks[index];
            if (show && !tick)
            {
                var go = new GameObject("ExecuteTick", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(view.hp.rectTransform, false);
                tick = executeTicks[index] = go.GetComponent<UnityEngine.UI.Image>();
                tick.raycastTarget = false;
            }
            if (tick)
            {
                if (tick.gameObject.activeSelf != show) tick.gameObject.SetActive(show);
                if (show)
                {
                    var rect = tick.rectTransform;
                    rect.anchorMin = new Vector2(threshold, -.25f); rect.anchorMax = new Vector2(threshold, 1.25f);
                    rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(executeTickWidth, 0f); rect.anchoredPosition = Vector2.zero;
                    tick.color = executeTickColor;
                }
            }
            bool below = executeBelow[index] = show && enemy.maxHp > 0f && enemy.Hp / enemy.maxHp < threshold;
            Color color = below ? executeBarColor : hpColors[index];
            if (view.hp.color != color) view.hp.color = color;
        }

        void Update()
        {
            if (!ArenaText.Ready) return;
            var gm = GameManager.Instance;
            if (!gm || !gm.cam || !gm.waves || !canvasRect) return;
            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0f) { refresh = .1f; RefreshEnemyData(gm); }
            FollowRun();
            TickNotices(gm);
            bool firstSightsPending = firstSight.Count > introduced.Count;
            for (int i = 0; i < bindings.Length; i++)
            {
                var enemy = bindings[i]; var view = enemyLabels[i];
                bool show = enemy && enemy.IsAlive && enemy.gameObject.activeInHierarchy && !enemy.Cloaked;
                if (show) NarrowBars(view, enemy);
                if (show)
                {
                    // Over the top of the 3D model as this camera sees it, so a lower camera does not lay the label across the body; in
                    // whitebox over the top of the whitebox look (Combatant.TryModelTop, 2026-11-10).
                    bool onModel = LabelPoint(models[i], gm.cam, out var top) || enemy.TryModelTop(gm.cam, out top);
                    show = PlaceWorld(view.root, onModel ? top : enemy.HeadPoint(gm.cam), gm.cam, onModel ? modelLabelLift : 0f);
                    // The first body of a kind met for the first time is on the screen: its notice joins the queue.
                    if (show && firstSightsPending && enemy.definition) SeeFirstSight(enemy.definition.kind);
                }
                if (view.root.gameObject.activeSelf != show) view.root.gameObject.SetActive(show);
            }
            int edges = 0;
            placedEdges.Clear();
            var world = WorldMap.Active;
            if (world && world.TrialActive) PlaceTrialPortalLabel(world, gm);
            else if (world && world.holes != null)
            {
                // The V2 day campaign's black hole (since 2026-11-06): its count over it, and over the hidden gate an E that says where it
                // leads; no notice at the screen's edge (the afternoon's ore trail leads there). Its other labels stay hidden.
                bool campaign = world.CampaignMapApplied;
                for (int i = 0; i < Mathf.Min(holeLabels.Length, world.holes.Length); i++)
                {
                    var hole = world.holes[i]; var label = holeLabels[i];
                    bool show = hole && hole.Phase != HolePhase.Gone && !world.TrialActive;
                    if (!show) { label.gameObject.SetActive(false); continue; }
                    // The label offers E only while E works (on the portal, free to act, in a wave or walking the build phase).
                    bool canEnter = world.CanEnterPortal(gm.player, hole);
                    string text = campaign ? CampaignHoleText(world, hole, canEnter)
                        : hole.Phase == HolePhase.Portal ? (canEnter ? PortalEnterText() : ArenaText.Text("ui.hole.portal")) + "\n" + ArenaText.Text("ui.hole.remaining", hole.WavesLeft)
                        : world.CanFeed && hole.Phase == HolePhase.Active ? $"{hole.Feeds} / {hole.feedsToOpen}" : "";
                    if (label.text != text) label.text = text;
                    // The campaign's gate is smaller than the hole it was: its label sits lower, clear of the base bar at the screen's top
                    // while the player stands on it (the classic run keeps its height).
                    float lift = campaign && hole.Phase == HolePhase.Portal ? 2.2f : hole.size * .5f + 1f;
                    bool visible = PlaceWorld(label.rectTransform, hole.Centre + gm.cam.transform.up * lift, gm.cam);
                    label.gameObject.SetActive(visible && text.Length > 0);
                    if (!campaign && Offscreen(hole.Centre, gm.cam) && edges < edgeLabels.Length)
                    {
                        float distance = gm.player ? Query.Flat(hole.Centre - gm.player.Position).magnitude : 0f;
                        if (hole.Phase == HolePhase.Portal)
                            PlaceEdge(edgeLabels[edges++], hole.Centre, gm.cam, ArenaText.Text("ui.hole.offscreen", hole.WavesLeft), true);
                        else
                            // The whole game's pages say the hole is dangerous before how far it is (the English Alpha keeps the name alone).
                            PlaceHoleEdge(edgeLabels[edges++], hole.Centre, gm.cam,
                                (V5HudKit.On ? ArenaText.Pair("黑洞 · 危险", "BLACK HOLE · DANGER") : ArenaText.Pair("黑洞", "BLACK HOLE")) + $"  {distance:0}m");
                    }
                }
                // The campaign keeps one hole in play (none at night): the labels of the holes put away hide.
                if (campaign)
                    for (int i = world.holes.Length; i < holeLabels.Length; i++)
                        if (holeLabels[i] && holeLabels[i].gameObject.activeSelf) holeLabels[i].gameObject.SetActive(false);
            }
            // Living enemy directions/counts use their dedicated eight saved, merged slots.
            // Hole directions and pre-spawn Shade notices retain this separate source.
            if (gm.waves.preparedMarkers != null)
                foreach (var marker in gm.waves.preparedMarkers)
                {
                    if (!marker || !marker.gameObject.activeInHierarchy || !marker.definition || marker.definition.kind != EnemyKind.Shade
                        || edges >= edgeLabels.Length || !Offscreen(marker.transform.position, gm.cam)) continue;
                    PlaceEdge(edgeLabels[edges++], marker.transform.position, gm.cam, ArenaText.Text("ui.shade.incoming"), true);
                }
            // XP on the ground. Teaching: until the first XP is collected in a run, the nearest orb carries a label (an edge notice when
            // it is off screen). Afterwards an edge notice still points at the nearest off-screen orb while a lot of XP lies around.
            var orbs = OrbPool.Instance;
            bool orbLabelOn = false;
            if (orbs && gm.player && gm.IsCombatActive)
            {
                var cam = gm.cam;
                var nearest = orbs.NearestXp(gm.player.Position);
                if (orbs.XpUncollected && nearest && orbHintLabel)
                {
                    string text = ArenaText.Pair("经验 · 捡起来升级", "XP · PICK UP TO LEVEL UP");
                    if (Offscreen(nearest.Position, cam))
                    {
                        if (edges < edgeLabels.Length) { PlaceEdge(edgeLabels[edges++], nearest.Position, cam, text, true); edgeLabels[edges - 1].color = XpEdgeColor; }
                    }
                    else
                    {
                        if (orbHintLabel.text != text) orbHintLabel.text = text;
                        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * 2f);
                        orbHintLabel.color = Color.Lerp(new Color(.45f, .8f, 1f), Color.white, pulse);
                        orbLabelOn = PlaceWorld(orbHintLabel.rectTransform, nearest.Position + cam.transform.up * 1.2f, cam);
                    }
                }
                else if (nearest)
                {
                    int onGround = orbs.CountXp(out float xpOnGround);
                    float needed = orbs.progression ? Mathf.Max(1, orbs.progression.XpRequired) : 1f;
                    if (onGround >= xpEdgeMinOrbs || xpOnGround >= xpEdgeMinShareOfLevel * needed)
                    {
                        var away = orbs.NearestXp(gm.player.Position, p => Offscreen(p, cam));
                        if (away && edges < edgeLabels.Length)
                        {
                            PlaceEdge(edgeLabels[edges++], away.Position, cam, ArenaText.Pair($"经验球 ×{onGround}", $"XP ×{onGround}"), true);
                            edgeLabels[edges - 1].color = XpEdgeColor;
                        }
                    }
                }
            }
            if (orbHintLabel && orbHintLabel.gameObject.activeSelf != orbLabelOn) orbHintLabel.gameObject.SetActive(orbLabelOn);
            for (int i = edges; i < edgeLabels.Length; i++) edgeLabels[i].gameObject.SetActive(false);
            if (fifthEdges != null) ShowFifthEdges(edges);
        }

        /// <summary>The open mode's portal while the interact key enters it: a sentence naming the key the player has interacting on
        /// (ArenaKeys), then the text table's sentences on what entering costs (ui.hole.enter).</summary>
        static string PortalEnterText()
        {
            string key = ArenaKeys.InteractKey;
            return ArenaText.Pair($"按 {key} 进入虚空试炼", $"Press {key} to enter the Void Trial") + "\n" + ArenaText.Text("ui.hole.enter");
        }

        /// <summary>The V2 day campaign's black hole: how many it has swallowed of what opens it, then over the hidden gate the E that takes
        /// the player in while it does (a question follows), else what the gate is.</summary>
        static string CampaignHoleText(WorldMap world, BlackHole hole, bool canEnter) => hole.Phase switch
        {
            HolePhase.Active => hole.campaignLabel ?? (world.CanFeed ? $"{hole.Feeds} / {hole.feedsToOpen}" : ""),
            // 2026-11-10: the portal carries no text of its own; only standing on it shows the key that enters.
            HolePhase.Portal => canEnter ? ArenaText.Pair($"按 {ArenaKeys.InteractKey} 进入", $"Press {ArenaKeys.InteractKey} to enter") : "",
            _ => ""
        };

        /// <summary>Void trial: the map's holes are frozen, so their saved labels are free. The first offers E on the trial portal the player
        /// stands on while E would take it (WorldMap.CanTakeTrialPortal), placed like a portal's label on the map; the others stay hidden.</summary>
        void PlaceTrialPortalLabel(WorldMap world, GameManager gm)
        {
            bool on = world.CanTakeTrialPortal(gm.player, out var portal);
            for (int i = 0; i < holeLabels.Length; i++)
            {
                var label = holeLabels[i];
                if (!label) continue;
                bool show = i == 0 && on;
                if (show)
                {
                    string text = ArenaText.Pair($"按 {ArenaKeys.InteractKey} 传送", $"Press {ArenaKeys.InteractKey} to travel");
                    if (label.text != text) label.text = text;
                    show = PlaceWorld(label.rectTransform, portal.Centre + gm.cam.transform.up * (portal.size * .5f + 1f), gm.cam);
                }

                if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
            }
        }

        /// <summary>A common enemy's bar stays narrower than the body under it; a boss or an elite keeps the full width. The same in whitebox and in the art.</summary>
        static void NarrowBars(EnemyLabel view, Enemy enemy)
        {
            float width = enemy.IsBoss || enemy.Elite ? 1f : Mathf.Clamp(enemy.radius * .85f, .42f, .78f);
            NarrowBar(view.hp ? view.hp.rectTransform.parent as RectTransform : null, width);
            NarrowBar(view.shield ? view.shield.rectTransform.parent as RectTransform : null, width);
        }

        /// <summary>Scales a bar about its own middle, whatever its pivot.</summary>
        static void NarrowBar(RectTransform bar, float width)
        {
            if (!bar || Mathf.Approximately(bar.localScale.x, width)) return;
            float toMiddle = bar.rect.width * (.5f - bar.pivot.x);
            var at = bar.anchoredPosition;
            at.x += toMiddle * (bar.localScale.x - width);
            bar.anchoredPosition = at;
            bar.localScale = new Vector3(width, 1f, 1f);
        }

        static bool Offscreen(Vector3 world, Camera camera)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            return viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f;
        }

        void RefreshEnemyData(GameManager gm)
        {
            int index = 0;
            var weapons = gm.runWeapons; var held = weapons ? weapons.Active : null;
            float executeLine = held != null && held.revealed && held.form == WeaponForm.Railgun && held.variant == 1 ? weapons.ExecuteThreshold(held) : 0f;
            foreach (var enemy in gm.waves.Alive)
            {
                if (!enemy || !enemy.IsAlive || !enemy.gameObject.activeInHierarchy || index >= bindings.Length) continue;
                if (bindings[index] != enemy) models[index] = enemy.GetComponent<V2ModelPresentation>();
                bindings[index] = enemy;
                ShowExecuteLine(index, enemy, executeLine);
                var view = enemyLabels[index++]; var status = ElementStatus.For(enemy);
                view.hp.fillAmount = enemy.maxHp > 0f ? Mathf.Clamp01(enemy.Hp / enemy.maxHp) : 0f;
                view.shield.fillAmount = status && enemy.maxHp > 0f ? Mathf.Clamp01(status.Shield / enemy.maxHp) : 0f;
                view.shield.gameObject.SetActive(status && status.Shield > 0f);
                // On the whole game's pages the boss plate at the top of the screen carries a boss's name and life (V5BattleHud): over
                // the boss stays only what the plate does not say, its statuses and its ward.
                bool plateCarries = V5HudKit.On && enemy.IsBoss;
                ShowBar(view.hp, view.root, !plateCarries);
                ShowBar(view.shield, view.root, !plateCarries);
                // Elites show their affix beside the bar; bodies inside a Chanter aura show the buff, an elite after its affix (document 9.2).
                // Since 2026-10-28 every other enemy names its kind above its bar too, as the bosses do, in a softer white.
                // Since 2026-11-06 ordinary enemies (the basics in EnemyRoles) show only their bar, so the name no longer covers the
                // model; specials, elites and bosses keep their name. Status words (Chanted, Marked) still show on any enemy.
                string kind = enemy.definition ? KindName(enemy.definition.kind) : "";
                // Whitebox names every enemy's kind (OrdinaryNamesShown): there the bodies are plain shapes. A kind met for the first time is
                // named with the art on too, for the morning or the wave it was met in (NamedForFirstSight).
                bool named = showOrdinaryEnemyNames || OrdinaryNamesShown(!Art.Enabled) || enemy.IsBoss || enemy.IsSpecial
                             || firstSight.Count > 0 && enemy.definition && NamedForFirstSight(enemy.definition.kind);
                string name = plateCarries ? ""
                    : enemy.IsBoss ? enemy.definition.kind == EnemyKind.Hollow ? ArenaText.Pair("纳什先知", "THE NASH PROPHET") : ArenaText.Pair("暴虐君王", "THE TYRANT KING")
                    : enemy.Elite ? kind + " · " + ArenaText.Text("ui.elite") + (enemy.Affix != ElementKind.None ? " · " + ArenaTerms.Element(enemy.Affix) : "") : named ? kind : "";
                // The fifth pass writes the names in the title's ink, an ordinary kind's a little fainter.
                Color nameColor = V5HudKit.On ? enemy.IsBoss || enemy.Elite ? V5TitleKit.Ink : FifthCommonName
                    : enemy.IsBoss || enemy.Elite ? Color.white : CommonNameColor;
                if (view.name.color != nameColor) view.name.color = nameColor;
                if (enemy.Chanted) name = (name.Length > 0 ? name + " · " : "") + ArenaText.Pair("吟唱强化", "CHANTED");
                // Execute Twist: a marked enemy says so above its bar.
                if (weapons && weapons.IsExecuteMarked(enemy)) name = (name.Length > 0 ? name + " · " : "") + ArenaText.Pair("处决标记", "MARKED");
                // The Nash Prophet takes no damage while one of its summons lives (system doc 13.1).
                if (enemy.ProphetWarded) name = (name.Length > 0 ? name + " · " : "") + ArenaText.Pair("无敌", "INVULNERABLE");
                if (view.name.text != name) view.name.text = name;
                V2Hud.SetElementIcon(view.affix, art, (int)enemy.Affix - 1);
                for (int j = 0; j < 4; j++)
                {
                    float count = status ? j switch { 0 => status.Fire, 1 => status.Poison, 2 => status.Frost, _ => status.Lightning } : 0f;
                    // An enemy's Fire is its burn per second: shown whenever it burns, rounded and at least 1.
                    bool burning = j == 0 && status && status.Burning;
                    bool on = count >= 1f || burning || (j == 2 && status && status.Frozen) || (j == 3 && status && status.Paralysed);
                    V2Hud.SetElementIcon(view.statusIcons[j], art, on ? j : -1);
                    view.statusCounts[j].gameObject.SetActive(on);
                    string value = j == 2 && status && status.Frozen ? ArenaText.Text("ui.freeze.short") : j == 3 && status && status.Paralysed ? ArenaText.Text("ui.paralysis.short")
                        : burning ? Mathf.Max(1, Mathf.RoundToInt(count)).ToString() : Mathf.FloorToInt(count).ToString();
                    if (on && view.statusCounts[j].text != value) view.statusCounts[j].text = value;
                }
            }
            for (int i = index; i < bindings.Length; i++) { bindings[i] = null; models[i] = null; }
        }

        /// <summary>
        /// Where an enemy's label sits over its shown 3D model (2026-11-10, camera-P47): the highest point, along the camera's up, of the
        /// ellipsoid that fills the model's bounds. Combatant.TryModelTop takes the box's highest corner instead; in the 2.5D view (pitched
        /// 50°, turned 45°) that corner is raised by half the box's width and depth as well as its height, so over a small, wide model
        /// (a Grunt or a Skitter, legs spread) the bar floated about 1.5 m above the head. A rounded body's top is where the ellipsoid's
        /// is; modelLabelLift still raises the label off it. False while no model is shown (whitebox): the caller keeps HeadPoint.
        /// </summary>
        static bool LabelPoint(V2ModelPresentation model, Camera cam, out Vector3 point)
        {
            point = default;
            if (!model || !cam || !model.UsingModel || model.modelRenderers == null) return false;
            bool any = false;
            Bounds bounds = default;
            foreach (var shown in model.modelRenderers)
            {
                if (!shown || !shown.enabled || !shown.gameObject.activeInHierarchy) continue;
                if (any) bounds.Encapsulate(shown.bounds);
                else { bounds = shown.bounds; any = true; }
            }
            if (!any) return false;
            Vector3 up = cam.transform.up, half = bounds.extents;
            float x = up.x * half.x, y = up.y * half.y, z = up.z * half.z;
            point = bounds.center + up * Mathf.Sqrt(x * x + y * y + z * z);
            return true;
        }

        static readonly Color CommonNameColor = new(.86f, .89f, .94f, .92f);
        static readonly Color FifthCommonName = new(V5TitleKit.Ink.r, V5TitleKit.Ink.g, V5TitleKit.Ink.b, .86f);

        /// <summary>Whether ordinary enemies (the basics) name their kind above their bar: in whitebox, on every page, where every body is a
        /// plain shape and the name says what it is. With the art on, names stay with specials, elites and bosses, so they do not cover
        /// the models.</summary>
        public static bool OrdinaryNamesShown(bool whitebox) => whitebox;
        static readonly string[] kindNames = new string[Enum.GetValues(typeof(EnemyKind)).Length];
        static bool kindNamesChinese;

        /// <summary>The enemy's name from the text tables (enemy.Grunt and so on), kept until the language changes.</summary>
        static string KindName(EnemyKind kind)
        {
            if (kindNamesChinese != ArenaText.IsChinese) { Array.Clear(kindNames, 0, kindNames.Length); kindNamesChinese = ArenaText.IsChinese; }
            int i = (int)kind;
            if (i < 0 || i >= kindNames.Length) return "";
            return kindNames[i] ??= ArenaText.Text("enemy." + kind);
        }

        /// <summary>A dark edge so the small name stays legible on bright ground.</summary>
        static void AddNameShadow(UnityEngine.UI.Text label)
        {
            var shadow = label.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .8f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>Puts a label on the screen point of world, raised by lift canvas units; false when the point is off screen.</summary>
        bool PlaceWorld(RectTransform label, Vector3 world, Camera cam, float lift = 0f)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f || screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            local.y += lift;
            label.position = canvasRect.TransformPoint(local);
            return true;
        }

        /// <summary>Danger marker: pulsing purple, bold and larger than the other edge notices.</summary>
        void PlaceHoleEdge(UnityEngine.UI.Text label, Vector3 world, Camera cam, string value)
        {
            PlaceEdge(label, world, cam, value, false, holeEdgeScale * 1.08f); // at its largest pulse
            // The fifth pass's notice keeps still (no continuous shimmer): the void's violet marks it.
            if (fifthEdges != null) { label.color = holeEdgeColor; return; }
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * holeEdgePulsePerSecond * Mathf.PI * 2f);
            label.color = Color.Lerp(holeEdgeColor, holeEdgeGlow, pulse);
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.localScale = Vector3.one * holeEdgeScale * (1f + .08f * pulse);
        }

        /// <summary>An edge notice pointing at world; scale is how large the notice is drawn (the hole's is larger), so that all of it
        /// stays inside the notice area.</summary>
        void PlaceEdge(UnityEngine.UI.Text label, Vector3 world, Camera cam, string value, bool bright, float scale = 1f)
        {
            label.fontStyle = FontStyle.Normal;
            label.rectTransform.localScale = Vector3.one;
            float unit = canvasRect.lossyScale.x > 0f ? canvasRect.lossyScale.x : 1f;
            // The notice sits where the line from the middle of the notice area toward the place leaves the area, so it is on the edge that
            // faces the place, and its arrow points out through that edge.
            // The insets bound the whole notice, not only its middle: half its drawn height is kept clear above and below.
            float half = label.rectTransform.rect.height * .5f * Mathf.Max(1f, scale);
            var area = new Rect(110f * unit, (edgeBottomInset + half) * unit, Mathf.Max(1f, Screen.width - 220f * unit),
                Mathf.Max(1f, Screen.height - (edgeBottomInset + edgeTopInset + 2f * half) * unit));
            Vector2 at = EdgePoint(FacingScreenPoint(cam, world), area, out bool onSide);
            string arrow = onSide ? at.x < area.center.x ? "◀ " : "▶ " : at.y < area.center.y ? "▼ " : "▲ ";
            int fifth = fifthEdges != null ? Array.IndexOf(edgeLabels, label) : -1;
            if (fifth >= 0) edgeWays[fifth] = onSide ? new Vector2(at.x < area.center.x ? -1f : 1f, 0f) : new Vector2(0f, at.y < area.center.y ? -1f : 1f);
            // Notices that point the same way stack instead of drawing over each other.
            float step = 30f * unit;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                bool clash = false;
                foreach (var p in placedEdges) if (Mathf.Abs(p.x - at.x) < 170f * unit && Mathf.Abs(p.y - at.y) < step * .9f) { clash = true; break; }
                if (!clash) break;
                at.y += at.y > Screen.height * .5f ? -step : step;
            }
            // A banner or notice at the top centre (a wave, a hit from the player's own weapon, a monster met for the first time) stays
            // readable: a marker on the top edge that would lie over it steps down below it while it shows.
            if (!onSide && at.y > area.center.y)
            {
                float w = Mathf.Max(80f, label.preferredWidth) * unit, h = 2f * half * unit;
                if (V5BattleHud.PlateOver(new Rect(at.x - w * .5f, at.y - h * .5f, w, h), out float bottom)) at.y = bottom - h * .5f - 8f * unit;
            }

            placedEdges.Add(at);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, at, null, out var local);
            label.rectTransform.position = canvasRect.TransformPoint(local);
            if (label.text != arrow + value) label.text = arrow + value;
            label.color = bright ? V2Hud.ElementColors[6] : new Color(.63f, .67f, .76f);
            label.gameObject.SetActive(true);
        }

        /// <summary>The screen point of world, for a notice that points at it. A perspective camera mirrors a point behind it. An
        /// orthographic camera (the game's 2.5D view) does not: its screen point is right on either side of the camera, and mirroring it
        /// turned a far place on the camera's side round to the opposite edge.</summary>
        public static Vector2 FacingScreenPoint(Camera cam, Vector3 world)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f && !cam.orthographic) { screen.x = cam.pixelWidth - screen.x; screen.y = cam.pixelHeight - screen.y; }
            return new Vector2(screen.x, screen.y);
        }

        /// <summary>Where the line from the middle of area toward point leaves area (point itself while it lies inside), and whether it
        /// leaves through the left or the right side rather than the top or the bottom.</summary>
        public static Vector2 EdgePoint(Vector2 point, Rect area, out bool onSide)
        {
            Vector2 middle = area.center, way = point - middle;
            onSide = Mathf.Abs(way.x) * area.height >= Mathf.Abs(way.y) * area.width;
            float reach = 1f;
            if (Mathf.Abs(way.x) > 1e-4f) reach = Mathf.Min(reach, ((way.x > 0f ? area.xMax : area.xMin) - middle.x) / way.x);
            if (Mathf.Abs(way.y) > 1e-4f) reach = Mathf.Min(reach, ((way.y > 0f ? area.yMax : area.yMin) - middle.y) / way.y);
            return middle + way * reach;
        }
    }
}
