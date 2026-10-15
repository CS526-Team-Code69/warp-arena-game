using UnityEngine;

namespace AnomalyArena
{
    public enum OrbKind { Xp, Hp }

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

        const float TravelSampleDistance = 0.25f;

        void Awake()
        {
            if (!visual) visual = GetComponent<Renderer>();
            appearance = new MaterialPropertyBlock();
        }

        public void Begin(Vector3 at, OrbKind kind, float amount, Material material, Texture2D texture)
        {
            Kind = kind; Value = Mathf.Max(0f, amount); InUse = true; repelVelocity = Vector3.zero; xpAttracted = false; magnetSuppressedFor = 0f;
            transform.position = new Vector3(at.x, 0.3f, at.z);
            gameObject.SetActive(true);
            if (!visual) return;
            if (material) visual.sharedMaterial = material;
            appearance ??= new MaterialPropertyBlock();
            appearance.Clear();
            Color colour = kind == OrbKind.Xp ? new Color(0.3f, 0.88f, 1f) : new Color(0.36f, 1f, 0.55f);
            appearance.SetColor(BaseColor, colour); appearance.SetColor(ColorProperty, colour);
            if (texture) appearance.SetTexture(BaseMap, texture);
            visual.SetPropertyBlock(appearance);
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
            transform.position = new Vector3(after.x, 0.3f, after.z);
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

            Release();
            if (kind == OrbKind.Xp) progression?.GainXp(amount);
            else if (player && player.IsAlive) player.Heal(amount);
            V2Audio.Instance?.PlayWorld(kind == OrbKind.Xp ? ArenaSoundCue.PickupXp : ArenaSoundCue.PickupHp, at);
        }
        public void Release() { InUse = false; Value = 0f; repelVelocity = Vector3.zero; xpAttracted = false; magnetSuppressedFor = 0f; gameObject.SetActive(false); }
    }
}
