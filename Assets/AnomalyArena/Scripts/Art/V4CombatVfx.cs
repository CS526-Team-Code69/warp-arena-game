using UnityEngine;

namespace AnomalyArena
{

    [DefaultExecutionOrder(1200)]
    public sealed class V4CombatVfx : MonoBehaviour
    {
        [Tooltip("Saved quads sharing one transparent material; colour and texture go through property blocks.")]
        public Renderer[] items;
        [Header("Element states on bodies")]
        public Texture[] fireFrames;
        public Texture[] poisonFrames;
        public Texture poisonMist, frostShell, frostRing, frostShard;
        public Texture[] lightningFrames;
        public Texture lightningJolt, slowRing;
        [Header("Player buffs")]
        public Texture auraOvercharge, auraRadiance, auraTimeSlip, auraShield, auraVoid;
        [Min(1f)] public float framesPerSecond = 12f;
        [Tooltip("Element layers stop beyond this distance from the player so crowds stay readable and cheap.")]
        [Min(1f)] public float maxDistance = 26f;

        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static readonly int TextureId = Shader.PropertyToID("_BaseMap");
        MaterialPropertyBlock block;
        int used;
        Transform cameraTransform;

        void Awake() { block = new MaterialPropertyBlock(); }
        void OnDisable() { if (items != null) foreach (var item in items) if (item) item.enabled = false; }

        void LateUpdate()
        {
            used = 0;
            var gm = GameManager.Instance;
            if (items == null || !gm || !gm.useV2 || !gm.cam || !Art.Enabled) { HideRest(); return; }
            cameraTransform = gm.cam.transform;
            float time = Time.unscaledTime;
            var player = gm.player;
            if (player && player.IsAlive) DrawPlayerBuffs(player, gm, time);
            Vector3 centre = player ? player.Position : cameraTransform.position;
            float maxSqr = maxDistance * maxDistance;
            foreach (var body in Combatant.All)
            {
                if (used >= items.Length) break;
                if (!body || body == (Combatant)player || !body.IsAlive || !body.gameObject.activeInHierarchy) continue;
                if (Query.Flat(body.Position - centre).sqrMagnitude > maxSqr) continue;
                var status = ElementStatus.For(body);
                if (status) DrawStatus(body, status, time);
            }
            HideRest();
        }

        void DrawStatus(Combatant body, ElementStatus status, float time)
        {
            float r = Mathf.Max(.35f, body.radius) * 1.3f;
            Vector3 feet = new Vector3(body.Position.x, .07f, body.Position.z);
            Vector3 chest = Query.AtCastHeight(body.Position) + Vector3.up * r * .35f;
            float phase = (body.GetHashCode() & 255) * .137f;
            if (status.Frozen)
                Billboard(frostShell, chest, r * 2.7f, new Color(.75f, .95f, 1f, .9f));
            else if (status.Frost > .05f)
                Flat(frostRing, feet, r * (2.2f + .6f * status.Chill01), new Color(.55f, .9f, 1f, .35f + .55f * status.Chill01), time * 20f + phase * 90f);
            if (status.Fire > .3f)
            {
                float size = r * Mathf.Lerp(1.7f, 2.6f, Mathf.Clamp01(status.Fire / 8f));
                Billboard(Frame(fireFrames, time + phase), chest + Vector3.up * size * .12f, size, new Color(1f, 1f, 1f, .92f));
            }
            if (status.Poison > .5f)
            {
                Billboard(poisonMist, chest, r * 2.6f, new Color(.7f, 1f, .5f, .35f + .25f * Mathf.Clamp01(status.Poison / 12f)));
                Billboard(Frame(poisonFrames, time * .7f + phase), chest + Vector3.up * r * .4f, r * 1.9f, new Color(1f, 1f, 1f, .95f));
            }
            if (status.Paralysed)
            {
                Flat(slowRing, feet, r * 2.5f, new Color(1f, .9f, .45f, .75f), -time * 40f);
                Billboard(Frame(lightningFrames, time * 1.4f + phase), chest, r * 2.4f, new Color(1f, .95f, .6f, .9f));
                if (status.Twitching) Billboard(lightningJolt, chest, r * 3.3f, new Color(1f, .95f, .55f, 1f));
            }
            else if (status.Lightning > .5f)
                Billboard(Frame(lightningFrames, time * 1.4f + phase), chest, r * 2f, new Color(1f, .9f, .5f, .3f + .1f * status.Lightning));
        }

        void DrawPlayerBuffs(Combatant player, GameManager gm, float time)
        {
            var skills = gm.skills;
            Vector3 feet = new Vector3(player.Position.x, .08f, player.Position.z);
            Vector3 chest = Query.AtCastHeight(player.Position) + Vector3.up * .3f;
            float pulse = .5f + .5f * Mathf.Sin(time * 6f);
            if (skills && skills.TimeSlipActive) Flat(auraTimeSlip, feet, 5.5f, new Color(.7f, .85f, 1f, .55f + .2f * pulse), -time * 25f);
            if (skills && skills.OverchargeActive) Flat(auraOvercharge, feet, 2.6f + .25f * pulse, new Color(1f, .9f, .45f, .95f), time * 160f);
            if (skills && skills.RadianceActive) Flat(auraRadiance, feet, 3.2f + .2f * pulse, new Color(1f, .96f, .7f, .85f), time * 30f);
            if (skills && skills.VoidWalkActive) Flat(auraVoid, feet, 3f, new Color(.85f, .6f, 1f, .9f), -time * 90f);
            var status = ElementStatus.For(player);
            if (status && status.Shield > 0f)
            {
                float share = Mathf.Clamp01(status.Shield / Mathf.Max(1f, player.maxHp * (status.Rules ? status.Rules.playerShieldCapShare : .5f)));
                Billboard(auraShield, chest, 2.6f, new Color(1f, .93f, .75f, .35f + .5f * share));
            }
        }

        Texture Frame(Texture[] frames, float time)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[Mathf.Abs((int)(time * framesPerSecond)) % frames.Length];
        }

        void Billboard(Texture texture, Vector3 at, float size, Color color)
        {
            if (!texture || used >= items.Length) return;

            Vector3 toCamera = (cameraTransform.position - at).normalized;
            Place(texture, at + toCamera * size * .35f, cameraTransform.rotation, size, color);
        }

        void Flat(Texture texture, Vector3 at, float size, Color color, float angle)
        {
            if (!texture || used >= items.Length) return;
            Place(texture, at, Quaternion.Euler(90f, angle, 0f), size, color);
        }

        void Place(Texture texture, Vector3 at, Quaternion rotation, float size, Color color)
        {
            var item = items[used++];
            if (!item) return;
            var t = item.transform;
            t.SetPositionAndRotation(at, rotation);
            t.localScale = new Vector3(size, size, 1f);
            block.Clear(); block.SetColor(ColorId, color); block.SetTexture(TextureId, texture);
            item.SetPropertyBlock(block);
            if (!item.enabled) item.enabled = true;
        }

        void HideRest()
        {
            if (items == null) return;
            for (int i = used; i < items.Length; i++) if (items[i] && items[i].enabled) items[i].enabled = false;
        }
    }
}
