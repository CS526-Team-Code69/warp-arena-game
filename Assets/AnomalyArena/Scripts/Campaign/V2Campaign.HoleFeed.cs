using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The black hole is fed before it can be attacked (2026-11-10). After the first-sight cutscene the side quest asks for monsters to
    /// be lured into the hole. The campaign counts every monster its hole swallows from then on, alive or as a corpse (the cutscene's own
    /// three are its demonstration and do not count), and shows the count over the hole and in the side quest's line. By day the
    /// morning's monsters can be led there. In the afternoon none are out, so while the hole is being fed and the player is near it, it
    /// calls small packs of grunts that come for the player from behind (from the far side they would walk across the hole and fall in
    /// by themselves); they give nothing, and they leave when the player goes away, when the hole wakes, and at nightfall with the
    /// other stragglers. The afternoon is a fight meanwhile.
    /// A monster follows the player into the hole's pull and is drawn in, so the way to feed the hole is to stand in its pull oneself.
    /// For that the campaign's hole pulls only a little faster than the player walks (rules.holePullFactor): a walk loses ground slowly
    /// and a sprint gets out. With rules.holeFeedGoal swallowed the hole opens its portal at once (since 2026-11-24); with
    /// rules.holeCheckAfterFeed on it wakes instead for the rest of the run and is broken by the timed check of V2Campaign.Hole.cs. A goal
    /// of 0 skips the feeding.
    /// </summary>
    public sealed partial class V2Campaign
    {
        // A pack comes out behind the player as seen from the hole: this far further out than the player (or than the hole's pull, if the
        // player stands in it), within LureSpreadDegrees of the player's bearing, the first one this long after the player came near.
        // A pack leaves once the player is LureLeash cells further from the hole than the afternoon's fight reaches.
        const float LureNearest = 9f, LureFarthest = 14f, LureFirstDelay = 1.5f, LureSpreadDegrees = 55f, LureLeash = 12f;
        // A pack is born at least LureMapMargin cells inside the map's sides: a point past them turns round the hole in steps of
        // LureSweepDegrees, to either side of the player's bearing, until it lies on the map.
        const float LureMapMargin = 2f, LureSweepDegrees = 15f;
        const int LureSweepSteps = 9;
        static readonly Color HoleWakeColour = new(.64f, .3f, 1f);

        struct Lured { public Enemy body; public int version; }

        readonly List<Lured> lured = new();
        int holeFed, feedLabelFor = -1;
        bool holeAwake, lureStarted;
        float lureAt, classicHolePull = -1f;
        string feedLabel = "";

        /// <summary>Monsters the hole has swallowed toward its feeding (the side quest's line, automated checks).</summary>
        public int HoleFed => holeFed;
        /// <summary>How many it must swallow before it can be attacked.</summary>
        public int HoleFeedGoal => rules ? Mathf.Max(0, rules.holeFeedGoal) : 0;
        /// <summary>The hole has been fed and can be attacked (from the start when the goal is 0).</summary>
        public bool HoleAwake => holeAwake || HoleFeedGoal <= 0;
        /// <summary>Monsters of the hole's afternoon packs alive now.</summary>
        public int HoleLureAlive { get { PruneLured(); return lured.Count; } }

        void ResetHoleFeeding()
        {
            DismissLured();
            holeFed = 0;
            feedLabelFor = -1;
            holeAwake = lureStarted = false;
            lureAt = 0f;
        }

        /// <summary>The campaign's hole pulls a little faster than the player walks; the classic run's holes keep their own pull.</summary>
        void ApplyCampaignHolePull(BlackHole hole)
        {
            if (!hole || !rules) return;
            if (classicHolePull < 0f) classicHolePull = hole.pullSpeed;
            hole.pullSpeed = CampaignRules.HolePull(rules.holePullFactor, gm && gm.player ? gm.player.moveSpeed : 6f);
        }

        void RestoreClassicHolePull(BlackHole hole)
        {
            if (hole && classicHolePull >= 0f) hole.pullSpeed = classicHolePull;
            classicHolePull = -1f;
        }

        /// <summary>WorldMap.HoleFed: a hole swallowed a monster or a monster's corpse. The campaign's own hole counts it once the player
        /// has met it; what fell in before that, and the cutscene's three, are not the player's luring.</summary>
        void OnHoleFed(BlackHole hole)
        {
            var map = questMap;
            if (!Running || !map || hole != map.CampaignHole || HoleAwake || !holeSeen || map.HoleIntroPlaying || map.TrialActive) return;
            holeFed = Mathf.Min(HoleFeedGoal, holeFed + 1);
            if (holeFed >= HoleFeedGoal) WakeHole(hole);
        }

        void WakeHole(BlackHole hole)
        {
            holeAwake = true;
            DismissLured();
            labelPercent = labelSeconds = -1;
            if (!rules.holeCheckAfterFeed) { OpenFedHole(hole); return; }
            ui.Flash(ArenaText.Pair("黑洞醒了！现在可以攻击它，但它也会还手。", "The black hole is awake! You can attack it now, but it will strike back."), 3.5f);
            Fx.Ring(hole.Centre, hole.GravityRadius, HoleWakeColour, .8f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossInhale, hole.Centre);
        }

        /// <summary>The fed hole collapses at once into the hidden gate the timed check would open (rules.holeCheckAfterFeed off, since
        /// 2026-11-24); the gate stays open until the player goes through. Analytics record it as the hole's check passed.</summary>
        void OpenFedHole(BlackHole hole)
        {
            if (!hole || hole.Phase != HolePhase.Active) return;
            hole.campaignLabel = "";
            hole.OpenGate(gm && gm.waves ? gm.waves.WaveIndex + 1 : 0);
            CampaignEvents.RaiseMilestone(CampaignEvents.MilestoneHoleCheck, CampaignEvents.HoleCheckPassed, 1f, Day);
            if (ui) ui.Flash(ArenaText.Pair("黑洞吞够了，变成了一座传送门。", "The black hole has had its fill and has become a portal."), 3f);
            Fx.Ring(hole.Centre, hole.GravityRadius, HoleWakeColour, .8f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.PortalOpen, hole.Centre);
        }

        /// <summary>The side quest's line while the hole is fed: the count, and how to do it.</summary>
        string FeedQuestLine() => rules && rules.holeCheckAfterFeed
            ? ArenaText.Pair($"把怪物引进黑洞（{holeFed}/{HoleFeedGoal}）：走进它的引力圈，追着你的怪物会跟进来，被它吞掉。",
                $"Lure monsters into the black hole ({holeFed}/{HoleFeedGoal}): step into its pull, and those chasing you follow and get swallowed.")
            : ArenaText.Pair($"把 {HoleFeedGoal} 个怪物引进黑洞，就能打开传送门（{holeFed}/{HoleFeedGoal}）：走进它的引力圈，追着你的怪物会跟进来，被它吞掉。",
                $"Lure {HoleFeedGoal} monsters into the black hole to open a portal ({holeFed}/{HoleFeedGoal}): step into its pull, and those chasing you follow and get swallowed.");

        /// <summary>The hole's world label while it is fed. Written again only when the count changes.</summary>
        string FeedLabel()
        {
            if (feedLabelFor == holeFed) return feedLabel;
            feedLabelFor = holeFed;
            return feedLabel = ArenaText.Pair($"已吞噬 {holeFed}/{HoleFeedGoal}", $"FED {holeFed}/{HoleFeedGoal}");
        }

        /// <summary>The feeding, each frame of the hole's side quest (TickHoleQuest): the hole's pull, and in the afternoon its packs.
        /// holeOut: the hole is out by day or in the afternoon and still a hole.</summary>
        void TickHoleFeeding(WorldMap map, BlackHole hole, PlayerController p, bool holeOut)
        {
            ApplyCampaignHolePull(hole);
            if (HoleAwake || !holeOut) { DismissLured(); return; }
            if (!holeSeen || !p || !p.IsAlive || map.HoleIntroPlaying) return;
            // By day the morning's own monsters are there to lure: the packs are the afternoon's.
            if (Stage != CampaignStage.Dusk) { DismissLured(); return; }
            float away = Query.Flat(p.Position - hole.Centre).magnitude;
            if (away > rules.holeFightRadius + LureLeash) { DismissLured(); lureStarted = false; return; }
            if (away > rules.holeFightRadius || !InPlay) return;
            if (!AfternoonCombat) AfternoonCombat = true; // weapons fire and monsters act while the hole is fed
            if (!lureStarted) { lureStarted = true; lureAt = Time.time + LureFirstDelay; }
            PruneLured();
            if (Time.time < lureAt) return;
            int count = CampaignRules.HoleLureCount(rules.holeLurePack, rules.holeLureAlive, lured.Count);
            if (count <= 0) return;
            lureAt = Time.time + Mathf.Max(.5f, rules.holeLureInterval);
            SpawnLurePack(hole, p, count);
        }

        void SpawnLurePack(BlackHole hole, PlayerController p, int count)
        {
            if (!gm.waves) return;
            Vector3 toPlayer = Query.Flat(p.Position - hole.Centre);
            float playerBearing = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            var map = WorldMap.Active;
            for (int i = 0; i < count; i++)
            {
                // Behind the player, a little to either side: they come for the player, and into the hole's pull only if led there.
                float reach = Mathf.Max(toPlayer.magnitude, hole.GravityRadius) + Random.Range(LureNearest, LureFarthest);
                Vector3 at = LurePoint(hole, playerBearing + Random.Range(-LureSpreadDegrees, LureSpreadDegrees), reach);
                // The hole stands in a corner of the map, so behind a player on its far side lies ground past the map's side. A pack
                // born there stands outside the map's wall for good and, counted alive, keeps the next packs from coming. Such a
                // point turns round the hole to the nearest bearing, to either side, whose point lies on the map.
                for (int step = 1; step <= LureSweepSteps && map && !map.Contains(at, LureMapMargin); step++)
                {
                    float turn = (LureSpreadDegrees + LureSweepDegrees * step) * (Random.value < .5f ? 1f : -1f);
                    Vector3 one = LurePoint(hole, playerBearing + turn, reach), other = LurePoint(hole, playerBearing - turn, reach);
                    if (map.Contains(one, LureMapMargin)) at = one;
                    else if (map.Contains(other, LureMapMargin)) at = other;
                }
                if (map && !map.Contains(at, LureMapMargin)) at = map.ClampPoint(at, LureMapMargin);
                at = FindOpen(at, .6f, 8f);
                if (hole.InGravity(at) || map && !map.Contains(at, .6f)) continue;
                var body = gm.waves.SpawnCampaign(EnemyKind.Grunt, at);
                if (!body) continue;
                body.SuppressRewards = true;
                body.Raider = false;
                lured.Add(new Lured { body = body, version = body.SpawnVersion });
                Fx.Pop(at + Vector3.up * .6f, HoleWakeColour, .5f);
            }
        }

        /// <summary>The point reach cells from the hole on a bearing (degrees, 0 toward +z, turning toward +x).</summary>
        static Vector3 LurePoint(BlackHole hole, float bearingDegrees, float reach)
        {
            float bearing = bearingDegrees * Mathf.Deg2Rad;
            return hole.Centre + new Vector3(Mathf.Sin(bearing), 0f, Mathf.Cos(bearing)) * reach;
        }

        void PruneLured()
        {
            for (int i = lured.Count - 1; i >= 0; i--)
                if (!lured[i].body || !lured[i].body.IsAlive || lured[i].body.SpawnVersion != lured[i].version) lured.RemoveAt(i);
        }

        /// <summary>The hole's packs leave without a kill or a drop: the hole woke, the player went away, or the side quest ended.</summary>
        void DismissLured()
        {
            if (lured.Count == 0) return;
            PruneLured();
            foreach (var one in lured)
            {
                one.body.SuppressRewards = true;
                raidSpots.Remove(one.body);
                one.body.DiscardWithoutReward();
            }
            lured.Clear();
        }

        /// <summary>For automated checks: the afternoon of this day begins at once, the player at the base door.</summary>
        public void DebugBeginAfternoon(int day)
        {
            if (!Running) return;
            ClearNight(false);
            Day = day;
            BeginDusk();
            PlacePlayer(DoorPoint);
        }

        /// <summary>For automated checks: the hole swallows this many more at once.</summary>
        public void DebugFeedHole(int count)
        {
            var hole = questMap ? questMap.CampaignHole : null;
            if (!Running || !hole || HoleAwake) return;
            holeFed = Mathf.Min(HoleFeedGoal, holeFed + Mathf.Max(0, count));
            if (holeFed >= HoleFeedGoal) WakeHole(hole);
        }
    }
}
