using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The void trial's ground beyond the saved platform (system doc 12.5): small broken fragments floating round it and
    /// black-hole portals on it, built at runtime from the saved platform's material and the saved trial hole, so the scene gains nothing. A
    /// portal is taken like the map's own portal: standing on it, free to act, the player presses E (TryEnterPortal), and walking over one
    /// does nothing. A portal on the platform sends the player to the fragment on the far side, so they come out in another direction; the
    /// portal in the middle of each fragment brings them back out of the portal they took, and after every jump the player stands still a
    /// moment, so a key still held from the way in does not walk them off. Off the platform and off every fragment is the void, and a body
    /// there falls at once. Walking enemies never step off; the Prophet's gravity pull draws in the player alone.
    /// </summary>
    public sealed partial class WorldMap
    {
        readonly List<Transform> trialFragments = new();
        readonly List<BlackHole> trialPortals = new(), fragmentPortals = new();
        int trialTerrainCount;
        // After any jump every portal waits a moment before E takes the player again, and the player stands still a little longer
        // (PortalArrivalHold). The portal they came out of opens to E like any other once that moment is over.
        float portalRearm, arrivalHold;

        /// <summary>The player came out of a trial portal a moment ago (nashPortalArrivalHold) and stands still: a move key held from the way
        /// in does not walk them off the small fragment or the platform before they see where they are, and no pull draws them.
        /// PlayerController reads it; shooting and skills work as ever.</summary>
        public bool PortalArrivalHold => TrialActive && arrivalHold > 0f;

        /// <summary>Fragments in the running trial; the platform has one portal for each (none when the scene has no trial hole to copy).</summary>
        public int TrialFragmentCount => TrialActive ? trialTerrainCount : 0;
        public int TrialPortalCount => TrialActive ? Mathf.Min(trialTerrainCount, trialPortals.Count) : 0;

        /// <summary>Middle of fragment i: round the platform at nashFragmentDistance, evenly spaced from nashFragmentAngle.</summary>
        public Vector3 TrialFragmentCentre(int i) => TrialRing(BossNumbers.nashFragmentAngle, BossNumbers.nashFragmentDistance, i);

        /// <summary>Middle of platform portal i: round the middle at nashPortalRadius, evenly spaced from nashPortalAngle.</summary>
        public Vector3 TrialPortalCentre(int i) => TrialRing(BossNumbers.nashPortalAngle, BossNumbers.nashPortalRadius, i);

        /// <summary>The fragment platform portal i leads to: the one on the far side (for four, two places round), so each portal has its
        /// own fragment and the player comes out in another direction than the one they went in.</summary>
        public int TrialPortalFragment(int portal)
        {
            int count = Mathf.Max(1, trialTerrainCount);
            return (portal + (count + 1) / 2) % count;
        }

        Vector3 TrialRing(float firstAngle, float distance, int i) => Query.Flat(trialPlatform.position) +
            Quaternion.Euler(0f, firstAngle + 360f * i / Mathf.Max(1, trialTerrainCount), 0f) * Vector3.forward * distance;

        /// <summary>Whether a point is on ground in the trial: the platform (it keeps its size in every phase) or a fragment, margin cells
        /// inside their edge. Everything else is the void.</summary>
        public bool OnTrialGround(Vector3 point, float margin = 0f)
        {
            if (!trialPlatform) return false;
            if (Query.Flat(point - trialPlatform.position).magnitude <= trialRadius - margin) return true;
            float reach = BossNumbers.nashFragmentRadius - margin;
            for (int i = 0; i < trialTerrainCount; i++)
                if (Query.Flat(point - TrialFragmentCentre(i)).magnitude <= reach) return true;
            return false;
        }

        /// <summary>How far from the platform's middle the trial's ground reaches: the fragments' outer edge, or the platform's without them.
        /// Boss shots fly a little past it (EnemyBolt), so standing on a fragment is no shelter from the rings.</summary>
        public float TrialGroundReach => trialTerrainCount > 0
            ? Mathf.Max(trialRadius, BossNumbers.nashFragmentDistance + BossNumbers.nashFragmentRadius)
            : trialRadius;

        /// <summary>Enemy walking in the trial (Enemy.KeepOffEdges): a step past the platform edge (less the body) is cut back onto it, so the
        /// body slides along the edge; a body already nearer the edge than that keeps its distance and may only step back in. A step into
        /// a hole of the player's own stops.</summary>
        public Vector3 KeepOnTrialGround(Vector3 position, Vector3 v, float dt, float bodyRadius)
        {
            if (!trialPlatform || dt <= 0f) return v;
            Vector3 centre = Query.Flat(trialPlatform.position), from = Query.Flat(position) - centre, next = from + Query.Flat(v) * dt;
            float allowed = Mathf.Max(trialRadius - bodyRadius, from.magnitude);
            if (next.magnitude > allowed) next = next.normalized * Mathf.Max(0f, allowed);
            return IsTemporaryHole(centre + next) ? Vector3.zero : (next - from) / dt;
        }

        /// <summary>BeginTrial: lays out the fragments and the portals for this trial (made on the first trial, reused after).</summary>
        void BuildTrialTerrain()
        {
            var numbers = BossNumbers;
            trialTerrainCount = Mathf.Max(0, numbers.nashFragmentCount);
            portalRearm = arrivalHold = 0f;
            var look = trialPlatform.GetComponent<Renderer>();
            while (trialFragments.Count < trialTerrainCount) trialFragments.Add(MakeTrialFragment(trialFragments.Count + 1, look));
            if (trialTemporaryHole)
                while (trialPortals.Count < trialTerrainCount)
                {
                    trialPortals.Add(MakeTrialPortal("TrialPortal (" + (trialPortals.Count + 1) + ")"));
                    fragmentPortals.Add(MakeTrialPortal("FragmentPortal (" + (fragmentPortals.Count + 1) + ")"));
                }

            float width = numbers.nashFragmentRadius * 2f;
            for (int i = 0; i < trialFragments.Count; i++)
            {
                var fragment = trialFragments[i];
                bool used = i < trialTerrainCount;
                fragment.gameObject.SetActive(used);
                if (!used) continue;
                Vector3 at = TrialFragmentCentre(i);
                fragment.position = new Vector3(at.x, trialPlatform.position.y, at.z);
                fragment.localScale = new Vector3(width, trialPlatform.localScale.y, width);
            }

            for (int i = 0; i < trialPortals.Count; i++)
            {
                if (i >= trialTerrainCount)
                {
                    trialPortals[i].gameObject.SetActive(false);
                    fragmentPortals[i].gameObject.SetActive(false);
                    continue;
                }

                trialPortals[i].ShowAsTrialPortal(TrialPortalCentre(i));
                fragmentPortals[i].ShowAsTrialPortal(TrialFragmentCentre(i));
            }

            Physics.SyncTransforms();
        }

        /// <summary>A disc like the saved platform: a cylinder with the platform's material, standing on a box (Awake gives the platform the
        /// same box: a flattened cylinder's capsule would be a sphere as wide as the disc).</summary>
        Transform MakeTrialFragment(int number, Renderer look)
        {
            var fragment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fragment.name = "TrialFragment (" + number + ")";
            fragment.layer = trialPlatform.gameObject.layer;
            fragment.transform.SetParent(trialPlatform.parent, false);
            if (fragment.TryGetComponent(out CapsuleCollider capsule))
            {
                var box = fragment.AddComponent<BoxCollider>();
                box.center = capsule.center;
                box.size = new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f);
                Destroy(capsule);
            }

            if (look && fragment.TryGetComponent(out MeshRenderer body))
            {
                body.sharedMaterial = look.sharedMaterial;
                body.shadowCastingMode = look.shadowCastingMode;
                body.receiveShadows = look.receiveShadows;
            }

            return fragment.transform;
        }

        /// <summary>A copy of the trial hole shown as an open portal (BlackHole.ShowAsTrialPortal), outside every hole list, so it never
        /// swallows, pulls or counts.</summary>
        BlackHole MakeTrialPortal(string name)
        {
            var portal = Instantiate(trialTemporaryHole, trialTemporaryHole.transform.parent);
            portal.name = name;
            portal.gameObject.SetActive(false);
            return portal;
        }

        /// <summary>CompleteTrial: every fragment and portal goes away with the trial.</summary>
        void HideTrialTerrain()
        {
            foreach (var fragment in trialFragments) if (fragment) fragment.gameObject.SetActive(false);
            foreach (var portal in trialPortals) if (portal) portal.gameObject.SetActive(false);
            foreach (var portal in fragmentPortals) if (portal) portal.gameObject.SetActive(false);
            trialTerrainCount = 0;
            arrivalHold = 0f;
        }

        /// <summary>FixedUpdate in the trial, for each body: off the ground it falls; a body other than the Prophet in a hole of the player's
        /// own is swallowed, and the bosses aside, everyone is pulled by those holes; the Prophet's gravity pull draws in the player alone,
        /// and the portal clocks run with the player (only the player takes the portals, with E: TryTakeTrialPortal). A summon that falls
        /// costs the Prophet nothing.</summary>
        void TickTrialBody(Combatant combatant)
        {
            if (!OnTrialGround(combatant.Position))
            {
                combatant.OnEnterFallZone(Vector3.zero);
                return;
            }

            bool boss = combatant is Enemy { IsBoss: true };
            // Launch Yourself Tame: the trial's holes neither swallow nor drag the flying missile (the void still does).
            bool holeProof = MissileHoleProof(combatant);
            if (!boss && !holeProof && IsTemporaryHole(combatant.Position, combatant))
            {
                SwallowIntoVoid(combatant);
                return;
            }

            bool player = combatant == GM.player;
            // Just out of a portal the player stands still (PortalArrivalHold): no hole and no gravity pull draws them meanwhile.
            if (!boss && !holeProof && !(player && PortalArrivalHold)) ApplyHolePull(combatant);
            if (!player) return;
            // Firing at full spin (Spin-up Tame) holds the player against it as against a black hole; Time Slip slows it with the world.
            // The Singularity's invincibility is not drawn either: the pull is a boss move, unlike the holes above.
            if (hollow && hollow.GravityPulling && !PortalArrivalHold && !(GM.runWeapons && GM.runWeapons.Anchored) && !(GM.skills && GM.skills.SingularityActive))
                ApplyPull(combatant, Query.Flat(hollow.Position - combatant.Position).normalized * (hollow.GravityPullSpeed * GM.WorldTimeScale));
            // The moment after a portal jump (TakeTrialPortal) runs out.
            portalRearm = Mathf.Max(0f, portalRearm - Time.fixedDeltaTime);
            arrivalHold = Mathf.Max(0f, arrivalHold - Time.fixedDeltaTime);
        }

        /// <summary>Whether E takes the player through a trial portal right now, and which one; the portal's label offers E only then
        /// (V2WorldLabels).</summary>
        public bool CanTakeTrialPortal(PlayerController player, out BlackHole portal)
        {
            int index = TakeableTrialPortal(player), count = TrialPortalCount;
            portal = index < 0 ? null : index < count ? trialPortals[index] : fragmentPortals[index - count];
            return index >= 0;
        }

        /// <summary>The trial portal the player stands on (their centre within nashPortalTrigger of its middle) while E would take it: the
        /// fight is on, the portals are awake again after the last jump (nashPortalRearm) and the player is in the normal state and free to
        /// act, as CanEnterPortal asks on the map (not knocked, stunned, frozen or twitching). Platform portal i is i, the way-back portal on
        /// fragment f is the portal count + f; -1 when E does nothing.</summary>
        int TakeableTrialPortal(PlayerController player)
        {
            if (!TrialActive || !trialPlatform || !player || !player.IsAlive || portalRearm > 0f || !GM.IsCombatActive) return -1;
            if (player.State != CharacterState.Normal || player.ActionsBlocked) return -1;
            Vector3 at = Query.Flat(player.Position);
            float trigger = BossNumbers.nashPortalTrigger;
            int count = TrialPortalCount;
            for (int i = 0; i < count; i++)
                if (Query.Flat(at - trialPortals[i].Centre).magnitude <= trigger) return i;
            for (int f = 0; f < count; f++)
                if (Query.Flat(at - fragmentPortals[f].Centre).magnitude <= trigger) return count + f;
            return -1;
        }

        /// <summary>E in the trial (TryEnterPortal): a platform portal sends the player to its fragment, onto the outer side clear of the
        /// way-back portal; a fragment's portal sends them back onto the platform portal that leads there. Either way they come out at rest
        /// and stand still for nashPortalArrivalHold. Off every portal, or not free to act, nothing happens.</summary>
        bool TryTakeTrialPortal(PlayerController player)
        {
            int index = TakeableTrialPortal(player), count = TrialPortalCount;
            if (index < 0) return false;
            Vector3 at = Query.Flat(player.Position);
            if (index < count)
            {
                Vector3 centre = TrialFragmentCentre(TrialPortalFragment(index)), outward = Query.Flat(centre - trialPlatform.position).normalized;
                TakeTrialPortal(player, at, centre + outward * BossNumbers.nashFragmentArrival);
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                if (TrialPortalFragment(i) != index - count) continue;
                TakeTrialPortal(player, at, Query.Flat(trialPortals[i].Centre));
                return true;
            }

            return false;
        }

        void TakeTrialPortal(PlayerController player, Vector3 from, Vector3 to)
        {
            portalRearm = BossNumbers.nashPortalRearm;
            arrivalHold = BossNumbers.nashPortalArrivalHold;
            Color tint = ElementCombat.ColorFor(ElementKind.Void);
            Fx.Ring(from, BossNumbers.nashFragmentRadius, tint);
            player.CancelDisplacement(); // no slide or recoil push carries over to the other side
            MovePlayer(player, to);
            Fx.Ring(to, BossNumbers.nashFragmentRadius, tint);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.PortalEnter, to);
        }
    }
}
