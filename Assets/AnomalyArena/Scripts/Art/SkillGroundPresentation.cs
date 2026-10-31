using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    [DefaultExecutionOrder(1400)]
    public sealed class SkillGroundPresentation : MonoBehaviour
    {
        public SkillLoadout skills;
        [Header("Saved fire layers.")]
        public ParticleSystem flameGround, flamePlumes;
        public Renderer[] areaFields;
        public LineRenderer[] lightningBolts;
        [Tooltip("Offset above the ground surface; does not change the damage area.")]
        public float groundHeight = .08f;
        [Range(.25f, 2f)] public float flameHeight = .85f;
        [Range(.1f, 1f)] public float groundOpacity = .48f;
        [Range(.1f, 1f)] public float plumeOpacity = .85f;
        public int VisibleFlameCount { get; private set; }
        public int VisibleAreaCount { get; private set; }
        public float AnimationTime { get; private set; }

        const int Capacity = 1024;
        readonly List<SkillGroundVisual> samples = new(Capacity);
        readonly ParticleSystem.Particle[] ground = new ParticleSystem.Particle[Capacity];
        readonly ParticleSystem.Particle[] plumes = new ParticleSystem.Particle[Capacity];
        MaterialPropertyBlock properties;
        ParticleSystemRenderer groundRenderer, plumeRenderer;
        float[] boltLife;
        int nextBolt;
        Vector3 lastPlayerAt;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            groundRenderer = flameGround ? flameGround.GetComponent<ParticleSystemRenderer>() : null;
            plumeRenderer = flamePlumes ? flamePlumes.GetComponent<ParticleSystemRenderer>() : null;
            boltLife = new float[lightningBolts?.Length ?? 0];
            Hold(flameGround); Hold(flamePlumes);
        }

        static void Hold(ParticleSystem system)
        { if (system) { system.Play(false); system.Pause(false); } }

        void OnEnable()
        {
            if (!skills) return;
            skills.CastSucceeded += OnCast;
            skills.VisualRequested += OnImpact;
        }
        void OnDisable()
        {
            if (skills) { skills.CastSucceeded -= OnCast; skills.VisualRequested -= OnImpact; }
            if (flameGround) flameGround.Clear(false);
            if (flamePlumes) flamePlumes.Clear(false);
            if (areaFields != null) foreach (var field in areaFields) if (field) field.enabled = false;
            if (lightningBolts != null) foreach (var bolt in lightningBolts) if (bolt) bolt.enabled = false;
            if (boltLife != null) System.Array.Clear(boltLife, 0, boltLife.Length);
            VisibleFlameCount = VisibleAreaCount = 0;
        }

        void LateUpdate()
        {
            if (!skills) return;
            var game = GameManager.Instance;
            float dt = game && game.IsCombatActive ? game.WorldDeltaTime : 0f;
            AnimationTime += dt;

            if (game && game.player) lastPlayerAt = game.player.Position;
            skills.CopyGroundVisuals(samples);
            VisibleFlameCount = VisibleAreaCount = 0;
            bool v5 = V5Vfx.On;
            foreach (var sample in samples)
            {
                if (v5)
                {

                    if (sample.Id == SkillId.FlameWake) { VisibleFlameCount++; V5Vfx.I.WakeSample(sample.Position, sample.Radius); }
                    else
                    {
                        VisibleAreaCount++;
                        if (sample.Id == SkillId.GravityWell) V5Vfx.I.GravityWellTick(sample.Position, sample.Radius);
                        else V5Vfx.I.StormTick(sample.Position, sample.Radius);
                    }
                    continue;
                }
                if (sample.Id == SkillId.FlameWake)
                {
                    if (VisibleFlameCount >= Capacity) continue;
                    int i = VisibleFlameCount++;
                    var point = new Vector3(sample.Position.x, groundHeight, sample.Position.z);
                    float fade = Mathf.Clamp01(sample.Remaining / .25f);
                    ground[i] = Particle(point, sample.Radius * 2f, groundOpacity * fade, i);
                    ground[i].rotation = i * 137.5f;

                    float height = flameHeight * Mathf.Min(1.5f, sample.Radius / .75f);
                    plumes[i] = Particle(point + Vector3.up * height * .5f, height, plumeOpacity * fade, i);
                    plumes[i].startSize3D = new Vector3(sample.Radius * 1.15f, height, 1f);
                }
                else if (areaFields != null && VisibleAreaCount < areaFields.Length)
                {
                    var field = areaFields[VisibleAreaCount++];
                    if (!field) continue;
                    field.enabled = true;
                    field.transform.SetPositionAndRotation(new Vector3(sample.Position.x, groundHeight, sample.Position.z),
                        Quaternion.Euler(90f, 0f, 0f));
                    field.transform.localScale = Vector3.one * (sample.Radius * 2f);
                    properties.Clear();
                    properties.SetFloat("_Mode", sample.Id == SkillId.GravityWell ? 2f : 3f);
                    properties.SetFloat("_EffectTime", sample.Age);
                    properties.SetColor("_BaseColor", sample.Id == SkillId.GravityWell
                        ? new Color(.56f, .35f, 1f, .55f) : new Color(.45f, .8f, 1f, .5f));
                    field.SetPropertyBlock(properties);
                }
            }
            if (flameGround) flameGround.SetParticles(ground, v5 ? 0 : VisibleFlameCount);
            if (flamePlumes) flamePlumes.SetParticles(plumes, v5 ? 0 : VisibleFlameCount);
            SetFireTime(groundRenderer); SetFireTime(plumeRenderer);
            if (areaFields != null)
                for (int i = v5 ? 0 : VisibleAreaCount; i < areaFields.Length; i++) if (areaFields[i]) areaFields[i].enabled = false;
            for (int i = 0; i < boltLife.Length; i++)
            {
                if (!lightningBolts[i]) continue;
                boltLife[i] = Mathf.Max(0f, boltLife[i] - dt);
                if (game && !game.CanPlayerMove && game.RunPhase != ArenaUiState.Paused) boltLife[i] = 0f;
                lightningBolts[i].enabled = boltLife[i] > 0f;
                lightningBolts[i].widthMultiplier = .09f * Mathf.Clamp01(boltLife[i] / .12f);
            }
        }

        static ParticleSystem.Particle Particle(Vector3 point, float size, float alpha, int index) => new()
        {
            position = point, startSize = size, startColor = new Color(1f, 1f, 1f, alpha),
            startLifetime = 1000f, remainingLifetime = 1000f, randomSeed = (uint)(index + 1)
        };
        void SetFireTime(Renderer layer)
        {
            if (!layer) return;
            properties.Clear(); properties.SetFloat("_EffectTime", AnimationTime); layer.SetPropertyBlock(properties);
        }
        void OnCast(SkillId id, Vector3 point, float radius, float duration)
        {
            if (V5Vfx.On)
            {
                V5Vfx.I.SkillCast(id, point, radius, lastPlayerAt);
                return;
            }

            if (id is SkillId.FlameWake or SkillId.GravityWell or SkillId.StormCall
                or SkillId.PocketVoid or SkillId.VoidWalk or SkillId.Singularity) return;
            var definition = skills.Catalog ? skills.Catalog.Find(id) : null;
            Color color = definition ? ElementCombat.ColorFor(definition.element) : Color.white;
            var pool = FxPool.Active;
            if (pool) pool.Emit(point, color, Mathf.Max(.8f, radius * 2f), .35f, texture: pool.ring, flat: true);
        }
        void OnImpact(SkillId id, Vector3 point, float radius, float duration)
        {
            if (id == SkillId.StormCall && V5Vfx.On) { V5Vfx.I.LightningStrike(point, new Color(.72f, .85f, 1f)); return; }
            if (id != SkillId.StormCall || boltLife.Length == 0) return;
            int index = nextBolt++ % boltLife.Length;
            var bolt = lightningBolts[index]; if (!bolt) return;
            boltLife[index] = .18f; bolt.enabled = true; bolt.positionCount = 5;
            bolt.SetPosition(0, point + Vector3.up * 4f);
            bolt.SetPosition(1, point + new Vector3(.28f, 3f, .12f));
            bolt.SetPosition(2, point + new Vector3(-.24f, 1.9f, -.12f));
            bolt.SetPosition(3, point + new Vector3(.16f, .8f, .05f));
            bolt.SetPosition(4, point + Vector3.up * .12f);
            var pool = FxPool.Active;
            if (pool) pool.Emit(point, new Color(.7f, .9f, 1f), .8f, .18f);
        }
    }
}
