using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
                 
                                                                                                                                            
                                                                                                                                        
                                                                                                                                       
                                                                                                                                          
                                                                                                                             
                  
    public sealed partial class V2Campaign
    {
        int alloy, energy, dark;
        public int Alloy => alloy;
        public int Energy => energy;
        public int Dark => dark;
        public int EarnedAlloy { get; private set; }
        public int EarnedEnergy { get; private set; }
        public int EarnedDark { get; private set; }

        enum PickupKind { Energy, Alloy }

        sealed class Pickup { public PickupKind kind; public int amount; public Vector3 position; public Transform view; public float age; }
        readonly List<Pickup> pickups = new();
        const int MaxPickups = 80;

        void ResetEconomy()
        {
            alloy = energy = dark = 0;
            EarnedAlloy = EarnedEnergy = EarnedDark = 0;
            haul = default;
            ClearPickups();
        }

        public bool CanAfford(CampaignCost cost) => alloy >= cost.alloy && energy >= cost.energy && dark >= cost.dark;

        public bool TrySpend(CampaignCost cost)
        {
            if (!CanAfford(cost)) { V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel); return false; }
            alloy -= cost.alloy; energy -= cost.energy; dark -= cost.dark;
            V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);
            if (!cost.IsFree) CampaignEvents.RaiseSpent(cost);
            return true;
        }

        public void Gain(CampaignCost amount)
        {
            alloy += amount.alloy; energy += amount.energy; dark += amount.dark;
            EarnedAlloy += amount.alloy; EarnedEnergy += amount.energy; EarnedDark += amount.dark;
            if (Stage == CampaignStage.Night)
            {
                haul.alloy += amount.alloy; haul.energy += amount.energy; haul.dark += amount.dark;
            }
        }

                                                                                                                                           
                                                                                                                                          
                                                                                                                                        
        CampaignCost haul;
        public CampaignCost TonightsHaul => haul;
        void BankHaul() => haul = default;

        CampaignCost LoseHaul(float share)
        {
            var lost = new CampaignCost(Mathf.Min(alloy, Mathf.CeilToInt(haul.alloy * share)), Mathf.Min(energy, Mathf.CeilToInt(haul.energy * share)), Mathf.Min(dark, Mathf.CeilToInt(haul.dark * share)));
            alloy -= lost.alloy; energy -= lost.energy; dark -= lost.dark;
            haul = default;
            return lost;
        }

        static CampaignDropTier TierOf(Enemy enemy) => enemy.Elite ? CampaignDropTier.Elite
            : enemy.Mini || EnemyRoles.IsMelee(enemy.definition.kind) ? CampaignDropTier.Fodder : CampaignDropTier.Special;

        static int Roll(float expected)
        {
            int whole = Mathf.FloorToInt(expected);
            return whole + (Random.value < expected - whole ? 1 : 0);
        }

        void DropCurrencies(Enemy enemy)
        {
            if (!enemy.definition) return;
            var tier = TierOf(enemy);
            int day = Mathf.Max(1, Day);
            bool tutorial = Stage == CampaignStage.Tutorial;
                                                                                                                                       
            float chance = rules.energyChance.For(tier);
            if (tutorial || (chance > 0f && Random.value < chance))
            {
                int amount = tutorial ? 1 : Roll(rules.energyAmount.For(tier) * (1f + rules.energyGrowthPerDay * (day - 1)));
                if (amount > 0) SpawnPickup(PickupKind.Energy, amount, enemy.Position);
            }
                                                                                                                                      
        }

        void SpawnPickup(PickupKind kind, int amount, Vector3 at)
        {
            at = Query.Flat(at);
            if (pickups.Count >= MaxPickups)
            {
                                                                                                             
                Pickup nearest = null; float best = float.MaxValue;
                foreach (var p in pickups) if (p.kind == kind) { float d = Query.Flat(p.position - at).sqrMagnitude; if (d < best) { best = d; nearest = p; } }
                if (nearest != null) { nearest.amount += amount; return; }
            }
            var pickup = new Pickup { kind = kind, amount = amount, position = at };
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = kind + "Shard";
            go.transform.SetParent(worldRoot, false);
            go.transform.localScale = Vector3.one * (.55f + .08f * Mathf.Min(6, amount));
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.sharedMaterial = PickupMaterial(kind);
            pickup.view = go.transform;
            pickups.Add(pickup);
        }

        Material energyShardMaterial, alloyShardMaterial;
        Material PickupMaterial(PickupKind kind)
        {
            ref Material cached = ref kind == PickupKind.Energy ? ref energyShardMaterial : ref alloyShardMaterial;
            if (cached) return cached;
            var sprite = art ? kind == PickupKind.Energy ? art.energy : art.alloy : null;
            if (art && art.shardMaterial) cached = new Material(art.shardMaterial) { name = kind + " shard" };
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                cached = shader ? new Material(shader) { name = kind + " shard" } : new Material(gm.litMaterial) { name = kind + " shard" };
            }
            Color tint = kind == PickupKind.Energy ? new Color(.78f, .45f, 1f) : new Color(.65f, .8f, 1f);
            if (sprite)
            {
                cached.mainTexture = sprite.texture;
                if (cached.HasProperty("_BaseMap")) cached.SetTexture("_BaseMap", sprite.texture);
                tint = Color.white;
            }
            if (cached.HasProperty("_BaseColor")) cached.SetColor("_BaseColor", tint);
            cached.color = tint;
                                                                                                               
            if (cached.HasProperty("_Surface"))
            {
                cached.SetFloat("_Surface", 1f); cached.SetFloat("_Blend", 2f);
                cached.SetOverrideTag("RenderType", "Transparent");
                cached.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                cached.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                cached.SetInt("_ZWrite", 0);
                cached.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                cached.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return cached;
        }

        void TickPickups(float dt)
        {
            var p = gm.player;
            if (!p) return;
            var cam = gm.cam;
            Vector3 pp = Query.Flat(p.Position);
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                var pickup = pickups[i];
                pickup.age += dt;
                Vector3 to = pp - pickup.position;
                float distance = to.magnitude;
                if (distance < rules.pickupMagnet) pickup.position += to.normalized * Mathf.Min(distance, 14f * dt);
                if (distance < 1f)
                {
                    Gain(pickup.kind == PickupKind.Energy ? new CampaignCost(0, pickup.amount) : new CampaignCost(pickup.amount));
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.PickupEnergy, pickup.position, pickup.kind == PickupKind.Energy ? 1.25f : .95f, .6f);
                    if (pickup.view) Destroy(pickup.view.gameObject);
                    pickups.RemoveAt(i);
                    OnTutorialPickup(pickup.kind == PickupKind.Energy);
                    continue;
                }
                if (pickup.view)
                {
                    pickup.view.position = pickup.position + Vector3.up * (.7f + .15f * Mathf.Sin(pickup.age * 4f));
                    if (cam) pickup.view.rotation = cam.transform.rotation;
                }
            }
        }

                                                                                                                                    
        public void DebugCollectPickups()
        {
            if (!gm.player) return;
            foreach (var p in pickups) p.position = Query.Flat(gm.player.Position);
        }
        public int PickupCount => pickups.Count;

        void ClearPickups()
        {
            foreach (var p in pickups) if (p.view) Destroy(p.view.gameObject);
            pickups.Clear();
        }

                                                               

        Node core;

        void SpawnCore(Vector3 at)
        {
            at = FindOpen(at, .8f, 6f);
            core = AddNode(NodeKind.Core, at, 1);
            Fx.Ring(at, 3f, new Color(.75f, .3f, 1f), .8f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.PortalOpen, at);
        }

        void TickCore(float dt)
        {
            TickPickups(dt);
            var p = gm.player;
            if (core == null || !p) return;
            if (core.view) core.view.Rotate(0f, 90f * dt, 0f, Space.World);
            if (Random.value < dt * 10f) Fx.Pop(core.position + Vector3.up * Random.Range(.5f, 1.6f), new Color(.75f, .3f, 1f), .6f);
            float d = Query.Flat(core.position - p.Position).magnitude;
            prompt = d < 2.2f ? ArenaText.Pair("E: take the dark matter core","E: take the dark matter core") : ArenaText.Pair("Take the dark matter core","Take the dark matter core");
            if (d < 2.2f && EPressed)
            {
                if (core.view) Destroy(core.view.gameObject);
                nodes.Remove(core); core = null; prompt = "";
                V3WorldAudio.Instance?.Play(ArenaWorldCue.LevelUp, p.Position);
                StartEnding();
            }
        }
    }
}
