using UnityEngine;

namespace AnomalyArena
{
    public enum OrbKind { Xp, Hp, Energy }

    /// <summary>An editor-authored pickup that can be reused for XP, HP or Energy. Movement is ticked by OrbPool. With the art off
    /// (2026-11-10) the saved sphere is hidden and the orb shows its kind's look from the whitebox table: a small cyan ball for XP, a
    /// purple one for Energy, a green cross for HP.</summary>
    public sealed class ExperienceOrb : MonoBehaviour
    {
        [field: SerializeField] public Renderer visual { get; set; }
        public OrbKind Kind { get; private set; }
        public float Value { get; private set; }
        public bool InUse { get; private set; }
        public Vector3 Position => transform.position;
        Vector3 repelVelocity;
        bool xpAttracted;
        float magnetSuppressedFor;
        MaterialPropertyBlock appearance;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorProperty = Shader.PropertyToID("_Color");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        // Spatial samples use the same circular hole query as combatants and keep fast orbs from skipping the core.
        const float TravelSampleDistance = 0.25f;
        // The height the orb floats at (its centre).
        const float Height = 0.3f;
        // The whitebox looks of the three kinds, made on first use; the kind and mode shown.
        WhiteboxLook xpLook, energyLook, hpLook;
        bool lookShown;
        OrbKind lookKind;

        void Awake()
        {
            if (!visual) visual = GetComponent<Renderer>();
            appearance = new MaterialPropertyBlock();
        }

        public void Begin(Vector3 at, OrbKind kind, float amount, Material material, Texture2D texture)
        {
            Kind = kind; Value = Mathf.Max(0f, amount); InUse = true; repelVelocity = Vector3.zero; xpAttracted = false; magnetSuppressedFor = 0f;
            transform.position = new Vector3(at.x, Height, at.z);
            gameObject.SetActive(true);
            ShowWhitebox();
            if (!visual) return;
            if (material) visual.sharedMaterial = material;
            appearance ??= new MaterialPropertyBlock();
            appearance.Clear();
            Color colour = kind switch
            {
                OrbKind.Xp => new Color(0.3f, 0.88f, 1f),
                OrbKind.Energy => new Color(.62f, .46f, 1f),
                _ => new Color(0.36f, 1f, 0.55f)
            };
            appearance.SetColor(BaseColor, colour); appearance.SetColor(ColorProperty, colour);
            if (texture) appearance.SetTexture(BaseMap, texture);
            visual.SetPropertyBlock(appearance);
            if (V5Vfx.On) V5Vfx.I.Orb(this, colour);
        }

        public void Merge(float amount) => Value += Mathf.Max(0f, amount);
        public void Repel(Vector3 center, float distance)
        {
            xpAttracted = false;
            magnetSuppressedFor = .35f;
            Vector3 away = Query.Flat(Position - center);
            if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
            repelVelocity += away.normalized * (Mathf.Max(0f, distance) * 6f);
        }

        public void Tick(float dt, Combatant player, RunProgression progression, float pickupRange, float magnetSpeed)
        {
            if (!InUse || !player || !player.IsAlive || dt <= 0f) return;
            Vector3 position = Position;
            if (SkillWorldAPI.IsHole?.Invoke(position) ?? false) { Release(); return; }
            Vector3 toward = Query.Flat(player.Position - position);
            float touch = OrbPool.Instance ? Mathf.Max(.05f, OrbPool.Instance.collectionRadius) : 1f;
            if (toward.sqrMagnitude <= touch * touch) { Collect(player, progression); return; }
            Vector3 drift = SkillWorldAPI.GetPull?.Invoke(position) ?? Vector3.zero;
            bool canAttract = magnetSuppressedFor <= 0f;
            magnetSuppressedFor = Mathf.Max(0f, magnetSuppressedFor-dt);
            bool near = canAttract && toward.sqrMagnitude <= pickupRange * pickupRange;
            if (Kind == OrbKind.Xp && near) xpAttracted = true;
            if (near || xpAttracted) drift += toward.normalized * Mathf.Max(0f, magnetSpeed);
            drift += repelVelocity;
            repelVelocity *= Mathf.Exp(-6f * dt);
            Vector3 after = position + Query.Flat(drift) * dt;
            Vector3 step = Query.Flat(after - position);
            float collectAt = PickupEntry(position, step, player.Position);
            Vector3 end = collectAt >= 0f ? position + step * collectAt : after;
            Vector3 travel = Query.Flat(end - position);
            float distance = travel.magnitude;
            for (float walked = TravelSampleDistance; walked < distance; walked += TravelSampleDistance)
                if (SkillWorldAPI.IsHole?.Invoke(position + travel / distance * walked) ?? false) { Release(); return; }
            if (SkillWorldAPI.IsHole?.Invoke(end) ?? false) { Release(); return; }
            if (collectAt >= 0f) { Collect(player, progression); return; }
            transform.position = new Vector3(after.x, Height, after.z);
        }

        // The art can be switched while an orb lies on the ground.
        void LateUpdate()
        {
            if (lookShown != (InUse && !Art.Enabled) || lookShown && lookKind != Kind) ShowWhitebox();
        }

        /// <summary>In whitebox: the saved sphere hidden, the kind's table look shown with its feet on the ground under the orb.</summary>
        void ShowWhitebox()
        {
            bool on = InUse && !Art.Enabled;
            lookShown = on;
            lookKind = Kind;
            if (visual) visual.forceRenderingOff = on;
            Show(ref xpLook, "orbs.xp", on && Kind == OrbKind.Xp);
            Show(ref energyLook, "orbs.energy", on && Kind == OrbKind.Energy);
            Show(ref hpLook, "orbs.hp", on && Kind == OrbKind.Hp);
        }

        void Show(ref WhiteboxLook look, string id, bool on)
        {
            if (!on)
            {
                if (look && look.gameObject.activeSelf) look.gameObject.SetActive(false);
                return;
            }
            if (!look)
            {
                look = Whitebox.Build(id, transform, "Whitebox " + id).GetComponent<WhiteboxLook>();
                // The orb object is scaled (its sphere is 0.3 across) and floats at Height: the look keeps the table's metres and its feet
                // on the ground.
                var scale = transform.lossyScale;
                look.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, scale.x), 1f / Mathf.Max(1e-4f, scale.y), 1f / Mathf.Max(1e-4f, scale.z));
                look.transform.localPosition = new Vector3(0f, -Height / Mathf.Max(1e-4f, scale.y), 0f);
            }
            if (!look.gameObject.activeSelf) look.gameObject.SetActive(true);
        }

        static float PickupEntry(Vector3 position, Vector3 step, Vector3 target)
        {
            float lengthSquared = step.sqrMagnitude;
            if (lengthSquared < 0.000001f) return -1f;
            Vector3 relative = Query.Flat(position - target);
            float dot = Vector3.Dot(relative, step);
            float radius = OrbPool.Instance ? Mathf.Max(.05f, OrbPool.Instance.collectionRadius) : 1f;
            float discriminant = dot * dot - lengthSquared * (relative.sqrMagnitude - radius * radius);
            if (discriminant < 0f) return -1f;
            float at = (-dot - Mathf.Sqrt(discriminant)) / lengthSquared;
            return at >= 0f && at <= 1f ? at : -1f;
        }

        public void Collect(Combatant player, RunProgression progression)
        {
            if (!InUse) return;
            var kind = Kind; float amount = Value; Vector3 at = Position;
            // Release before callbacks: a level-up callback cannot collect the same value twice.
            Release();
            switch (kind)
            {
                case OrbKind.Xp: progression?.GainXp(amount); break;
                case OrbKind.Energy: progression?.GainEnergy(amount); break;
                default: if (player && player.IsAlive) player.Heal(amount); break;
            }
            // Energy reuses the HP pickup sound.
            if (kind == OrbKind.Energy) V3WorldAudio.Instance?.Play(ArenaWorldCue.PickupEnergy, at);
            else V2Audio.Instance?.PlayWorld(kind == OrbKind.Xp ? ArenaSoundCue.PickupXp : ArenaSoundCue.PickupHp, at);
        }
        public void Release() { InUse = false; Value = 0f; repelVelocity = Vector3.zero; xpAttracted = false; magnetSuppressedFor = 0f; gameObject.SetActive(false); }
    }
}
