using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
                 
                                                                                                                                           
                                                                                                                                             
                                                                                                                                         
                                                                                                                                         
                                                                                                                                             
                                                                                                   
                  
    public sealed partial class WorldMap
    {
        readonly List<Transform> trialFragments = new();
        readonly List<BlackHole> trialPortals = new(), fragmentPortals = new();
        int trialTerrainCount;
                                                                                                                                         
                                                                           
        int heldPortal = -1;
        float portalRearm, arrivalHold;

                                                                                                                                              
                                                                                                                                       
                                                                                  
        public bool PortalArrivalHold => TrialActive && arrivalHold > 0f;

                                                                                                                                                    
        public int TrialFragmentCount => TrialActive ? trialTerrainCount : 0;
        public int TrialPortalCount => TrialActive ? Mathf.Min(trialTerrainCount, trialPortals.Count) : 0;

                                                                                                                                      
        public Vector3 TrialFragmentCentre(int i) => TrialRing(BossNumbers.nashFragmentAngle, BossNumbers.nashFragmentDistance, i);

                                                                                                                                     
        public Vector3 TrialPortalCentre(int i) => TrialRing(BossNumbers.nashPortalAngle, BossNumbers.nashPortalRadius, i);

                                                                                                                                          
                                                                                                           
        public int TrialPortalFragment(int portal)
        {
            int count = Mathf.Max(1, trialTerrainCount);
            return (portal + (count + 1) / 2) % count;
        }

        Vector3 TrialRing(float firstAngle, float distance, int i) => Query.Flat(trialPlatform.position) +
            Quaternion.Euler(0f, firstAngle + 360f * i / Mathf.Max(1, trialTerrainCount), 0f) * Vector3.forward * distance;

                                                                                                                                         
                                                              
        public bool OnTrialGround(Vector3 point, float margin = 0f)
        {
            if (!trialPlatform) return false;
            if (Query.Flat(point - trialPlatform.position).magnitude <= trialRadius - margin) return true;
            float reach = BossNumbers.nashFragmentRadius - margin;
            for (int i = 0; i < trialTerrainCount; i++)
                if (Query.Flat(point - TrialFragmentCentre(i)).magnitude <= reach) return true;
            return false;
        }

                                                                                                                                              
                                                                                                                          
        public float TrialGroundReach => trialTerrainCount > 0
            ? Mathf.Max(trialRadius, BossNumbers.nashFragmentDistance + BossNumbers.nashFragmentRadius)
            : trialRadius;

                                                                                                                                               
                                                                                                                                              
                                                       
        public Vector3 KeepOnTrialGround(Vector3 position, Vector3 v, float dt, float bodyRadius)
        {
            if (!trialPlatform || dt <= 0f) return v;
            Vector3 centre = Query.Flat(trialPlatform.position), from = Query.Flat(position) - centre, next = from + Query.Flat(v) * dt;
            float allowed = Mathf.Max(trialRadius - bodyRadius, from.magnitude);
            if (next.magnitude > allowed) next = next.normalized * Mathf.Max(0f, allowed);
            return IsTemporaryHole(centre + next) ? Vector3.zero : (next - from) / dt;
        }

                                                                                                                                         
        void BuildTrialTerrain()
        {
            var numbers = BossNumbers;
            trialTerrainCount = Mathf.Max(0, numbers.nashFragmentCount);
            heldPortal = -1;
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

                                                                                                                                         
                                                
        BlackHole MakeTrialPortal(string name)
        {
            var portal = Instantiate(trialTemporaryHole, trialTemporaryHole.transform.parent);
            portal.name = name;
            portal.gameObject.SetActive(false);
            return portal;
        }

                                                                                                 
        void HideTrialTerrain()
        {
            foreach (var fragment in trialFragments) if (fragment) fragment.gameObject.SetActive(false);
            foreach (var portal in trialPortals) if (portal) portal.gameObject.SetActive(false);
            foreach (var portal in fragmentPortals) if (portal) portal.gameObject.SetActive(false);
            trialTerrainCount = 0;
            heldPortal = -1;
            arrivalHold = 0f;
        }

                                                                                                                                              
                                                                                                                                            
                                                                                                                              
        void TickTrialBody(Combatant combatant)
        {
            if (!OnTrialGround(combatant.Position))
            {
                combatant.OnEnterFallZone(Vector3.zero);
                return;
            }

            bool boss = combatant is Enemy { IsBoss: true };
            if (!boss && IsTemporaryHole(combatant.Position, combatant))
            {
                SwallowIntoVoid(combatant);
                return;
            }

            bool player = combatant == GM.player;
                                                                                                                                  
            if (!boss && !(player && PortalArrivalHold)) ApplyHolePull(combatant);
            if (!player) return;
                                                                                                                                         
            if (hollow && hollow.GravityPulling && !PortalArrivalHold && !(GM.runWeapons && GM.runWeapons.Anchored))
                ApplyPull(combatant, Query.Flat(hollow.Position - combatant.Position).normalized * (hollow.GravityPullSpeed * GM.WorldTimeScale));
            TickTrialPortals(GM.player);
        }

                                                                                                                                              
                                                                                                                                             
                                                                                                 
        void TickTrialPortals(PlayerController player)
        {
            var numbers = BossNumbers;
            portalRearm = Mathf.Max(0f, portalRearm - Time.fixedDeltaTime);
            arrivalHold = Mathf.Max(0f, arrivalHold - Time.fixedDeltaTime);
            Vector3 at = Query.Flat(player.Position);
            float trigger = numbers.nashPortalTrigger;
            if (heldPortal >= 0 && (heldPortal >= trialPortals.Count || Query.Flat(at - trialPortals[heldPortal].Centre).magnitude > trigger))
                heldPortal = -1;
            if (portalRearm > 0f || player.State is not (CharacterState.Normal or CharacterState.Knocked)) return;
            int count = TrialPortalCount;
            for (int i = 0; i < count; i++)
            {
                if (i == heldPortal || Query.Flat(at - trialPortals[i].Centre).magnitude > trigger) continue;
                int target = TrialPortalFragment(i);
                Vector3 centre = TrialFragmentCentre(target), outward = Query.Flat(centre - trialPlatform.position).normalized;
                TakeTrialPortal(player, at, centre + outward * numbers.nashFragmentArrival);
                return;
            }

            for (int f = 0; f < count; f++)
            {
                if (Query.Flat(at - fragmentPortals[f].Centre).magnitude > trigger) continue;
                for (int i = 0; i < count; i++)
                {
                    if (TrialPortalFragment(i) != f) continue;
                    heldPortal = i;
                    TakeTrialPortal(player, at, Query.Flat(trialPortals[i].Centre));
                    return;
                }
            }
        }

        void TakeTrialPortal(PlayerController player, Vector3 from, Vector3 to)
        {
            portalRearm = BossNumbers.nashPortalRearm;
            arrivalHold = BossNumbers.nashPortalArrivalHold;
            Color tint = ElementCombat.ColorFor(ElementKind.Void);
            Fx.Ring(from, BossNumbers.nashFragmentRadius, tint);
            player.CancelDisplacement();                                                          
            MovePlayer(player, to);
            Fx.Ring(to, BossNumbers.nashFragmentRadius, tint);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.PortalEnter, to);
        }
    }
}
