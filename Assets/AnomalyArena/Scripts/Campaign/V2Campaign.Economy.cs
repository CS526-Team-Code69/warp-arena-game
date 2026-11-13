using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The three V2 currencies (V2 doc section 3; decision D4-A: star matter is what alien energy drops as, ore is alien alloy, dark matter
    /// is mined only at night). Kill drops: chance x amount by enemy tier, growing a little each day (the doc: each day raises the drop
    /// chance, and stronger monsters drop more). Since 2026-11-06 kills drop alien energy only: alloy and dark matter are mined, never
    /// dropped, so the map's limited ore sets each day's alloy. A fractional amount is rounded up with the fraction as its chance, so the
    /// average stays exactly the table's. Drops fly to a player within pickupMagnet cells and stay on the ground until dark.
    /// </summary>
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

        /// <summary>A drop on the ground; solid: its view is the whitebox table's look (stands on the ground and turns) rather than a picture
        /// facing the camera.</summary>
        sealed class Pickup { public PickupKind kind; public int amount; public Vector3 position; public Transform view; public float age; public bool solid; }
        const float SolidPickupTurn = 90f;   // degrees a second a whitebox drop turns
        const float SolidPickupGrowth = .08f; // a whitebox drop's size grows by this share per unit it holds, up to six
        // Since 2026-11-10 a whitebox drop floats this high (a dead enemy's body used to hide it on the ground) at this size.
        const float SolidPickupHeight = .8f, SolidPickupSize = 1.3f;
        readonly List<Pickup> pickups = new();
        const int MaxPickups = 80;

        void ResetEconomy()
        {
            alloy = energy = dark = 0;
            EarnedAlloy = EarnedEnergy = EarnedDark = 0;
            haul = default;
            ClearPickups();
            ResetVoid(); // the void currencies and what they bought (V2Campaign.Void.cs)
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

        // Tonight's haul: what was mined or picked up after nightfall, or since the last base visit that night (going into the base stores
        // it). A death at night loses it (the rule since 2026-11-06); the base's stock, the rooms and the ship stay. Dusk does not count:
        // the day's drops picked up at dusk and the dusk ore are kept (counting them wiped a whole day's drops in the first check run).
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
            bool tutorial = Stage == CampaignStage.Tutorial;
            // Since 2026-11-10 a morning's kills share out the day's kill energy (CampaignRules.dayKillEnergy, V2Campaign.Days.cs), so
            // killing every monster of the day pays exactly the table; the chance and amount by tier below are only read without it.
            if (!tutorial && rules.dayKillEnergy is { Length: > 0 })
            {
                int share = KillEnergyFor(enemy);
                if (share > 0) SpawnPickup(PickupKind.Energy, share, enemy.Position);
                return;
            }
            var tier = TierOf(enemy);
            int day = Mathf.Max(1, Day);
            // Tutorial: every grunt drops one star matter (the V2 doc's "kill it, it drops star matter"). A chance of 0 drops nothing.
            float chance = rules.energyChance.For(tier);
            if (tutorial || (chance > 0f && Random.value < chance))
            {
                int amount = tutorial ? 1 : Roll(rules.energyAmount.For(tier) * (1f + rules.energyGrowthPerDay * (day - 1)));
                if (amount > 0) SpawnPickup(PickupKind.Energy, amount, enemy.Position);
            }
            // No alloy shard since 2026-11-06 (rules.alloyChance and alloyAmount are no longer read): alloy comes from the map's ore.
        }

        void SpawnPickup(PickupKind kind, int amount, Vector3 at)
        {
            at = Query.Flat(at);
            if (pickups.Count >= MaxPickups)
            {
                // A full pool merges the drop into the nearest of its kind instead of making another object.
                Pickup nearest = null; float best = float.MaxValue;
                foreach (var p in pickups) if (p.kind == kind) { float d = Query.Flat(p.position - at).sqrMagnitude; if (d < best) { best = d; nearest = p; } }
                if (nearest != null) { nearest.amount += amount; return; }
            }
            var pickup = new Pickup { kind = kind, amount = amount, position = at };
            if (kind == PickupKind.Energy && !Art.Enabled)
            {
                // The whitebox (2026-11-10): the whitebox table's turning energy crystal.
                pickup.view = Whitebox.Build("energy", worldRoot, "EnergyWhitebox");
                pickup.view.localScale = Vector3.one * (SolidPickupSize + SolidPickupGrowth * Mathf.Min(6, amount));
                pickup.solid = true;
                pickups.Add(pickup);
                return;
            }
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
            // URP Unlit as alpha-blended transparent (the icons are dark-backed; additive keeps them glowing).
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
                    ui.ShowGain(pickup.kind == PickupKind.Energy ? 0 : 1, pickup.amount, pickup.position);
                    if (pickup.view) Destroy(pickup.view.gameObject);
                    pickups.RemoveAt(i);
                    OnTutorialPickup(pickup.kind == PickupKind.Energy);
                    continue;
                }
                if (pickup.view && pickup.solid)
                {
                    // Floating over the ground (and over a corpse lying there), bobbing a little, turning.
                    pickup.view.position = pickup.position + Vector3.up * (SolidPickupHeight + .15f * Mathf.Sin(pickup.age * 4f));
                    pickup.view.rotation = Quaternion.Euler(0f, pickup.age * SolidPickupTurn, 0f);
                }
                else if (pickup.view)
                {
                    pickup.view.position = pickup.position + Vector3.up * (.7f + .15f * Mathf.Sin(pickup.age * 4f));
                    if (cam) pickup.view.rotation = cam.transform.rotation;
                }
            }
        }

        /// <summary>For automated checks: every shard on the ground moves under the player (collected on the next frame).</summary>
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

        // ───── The dark matter core (the Tyrant's drop) ─────

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
            prompt = d < 2.2f ? ArenaText.Pair($"按 {ArenaKeys.InteractKey} 拾取暗物质核心", $"Press {ArenaKeys.InteractKey} to take the dark matter core") : ArenaText.Pair("拾取暗物质核心", "Take the dark matter core");
            if (d < 2.2f && EPressed)
            {
                if (core.view) Destroy(core.view.gameObject);
                nodes.Remove(core); core = null; prompt = "";
                StartEnding();
            }
        }
    }
}
