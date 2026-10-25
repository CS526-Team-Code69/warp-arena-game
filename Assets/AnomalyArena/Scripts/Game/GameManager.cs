using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    public enum GameState
    {
        Title,
        Playing,
        Won,
        Lost
    }

    [DefaultExecutionOrder(-100)]
    public partial class GameManager : MonoBehaviour
    {
        [Serializable]
        public class Rules
        {
            [Tooltip("Damage for hitting a wall after being knocked back / thrown (ignores protection)")]
            public float wallDamage = 5f;

            [Tooltip("Speed toward the wall at impact must exceed this to count as a hit (to be tuned)")]
            public float wallHitSpeed = 3f;

            [Tooltip("Knockback damping: initial speed = distance × damping")]
            public float knockDamping = 6f;

            [Tooltip("Document 5.3: how far each knockback tier pushes, and how an enemy's body size resists a push.")]
            public KnockbackRules knockback = new();
        }

        [Serializable]
        public class Feedback
        {
            [Tooltip("Seconds the white flash lasts")]
            public float flashTime = 0.1f;

            [Tooltip("How much the body is squashed on hit (0 = no deformation)")]
            public float squash = 0.35f;

            [Tooltip("Damage at which a hit counts as heavy")]
            public float heavyDamage = 10f;

            [Header("Player hit: feedback only (no stun, no push); bosses still knock back")]
            [Min(0f), Tooltip("Camera push away from a basic enemy's hit.")]
            public float basicHitKick = .18f;

            [Min(0f), Tooltip("Camera push away from an elite's hit.")]
            public float eliteHitKick = .3f;

            [Min(0f), Tooltip("Camera push from a boss hit.")]
            public float bossHitKick = .4f;

            [Min(0f), Tooltip("How far the player model flinches along the hit (units).")]
            public float playerFlinch = .18f;

            [Header("Hits the player lands")]
            [Min(0f),
             Tooltip("Real seconds before the same enemy can hold its pose again, so rapid fire never freezes it.")]
            public float victimHoldCooldown = .25f;

            [Min(0f), Tooltip("Real seconds between camera pushes from landed hits, so crowds never blur the screen.")]
            public float hitKickInterval = .12f;

            [Tooltip("Knife, hook, body slam, orbit blades, shield ring: the target bites back.")]
            public HitFeelProfile melee = new()
            {
                victimHold = .07f, victimHeavyHold = .1f, victimJolt = .18f, attackerHold = .04f, hitKick = .06f,
                heavyHitKick = .12f, splash = 1.4f, fireRecoil = -.12f
            };

            [Tooltip("Rifle, railgun, shatter shards: a heavy round with a short hold on heavy hits.")]
            public HitFeelProfile heavyShot = new()
            {
                victimHold = .03f, victimHeavyHold = .07f, victimJolt = .16f, heavyHitKick = .1f, splash = 1f,
                fireRecoil = .14f, fireKick = .12f, muzzleFlash = 1.3f
            };

            [Tooltip("Minigun, needler, scatter, sentry: many small rounds, no hold, only a jolt.")]
            public HitFeelProfile lightShot = new()
            {
                victimJolt = .07f, heavyHitKick = .05f, splash = .7f, fireRecoil = .05f, fireKick = .03f,
                muzzleFlash = .75f
            };

            [Tooltip("Flames, frost, lightning, spores, explosions: flash only, never a hold.")]
            public HitFeelProfile area = new()
                { victimJolt = .04f, heavyHitKick = .1f, splash = .6f, fireRecoil = .03f };

            public HitFeelProfile For(HitFeel feel) => feel switch
            {
                HitFeel.Melee => melee,
                HitFeel.HeavyShot => heavyShot,
                HitFeel.LightShot => lightShot,
                _ => area,
            };
        }

        public static GameManager Instance { get; private set; }
        public static int WallLayer { get; private set; }
        public static int CharacterLayer { get; private set; }
        public static int WallMask { get; private set; }
        public static int CharacterMask { get; private set; }

        public Rules rules = new Rules();
        public Feedback feedback = new Feedback();

        [Header("Refs")] public PlayerController player;
        public WaveManager waves;
        public Hud hud;
        public Camera cam;

        [Header("Materials")] [Tooltip("Base material for whitebox objects created at runtime (URP Lit)")]
        public Material litMaterial;

        public GameState State { get; private set; } = GameState.Title;
        public DeathCause LoseCause { get; private set; }
        public float Elapsed { get; private set; }

        readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
        bool finalWaveCleared;
        float nextHitKickAt;

        void Awake()
        {
            Instance = this;
            WallLayer = LayerMask.NameToLayer("Wall");
            CharacterLayer = LayerMask.NameToLayer("Character");
            WallMask = 1 << WallLayer;
            CharacterMask = 1 << CharacterLayer;
            if (!cam) cam = Camera.main;

            Time.timeScale = 1f;
        }

        public void Shake(float amount)
        {
            if (cam && cam.TryGetComponent<CameraRig>(out var rig)) rig.Shake(amount);
        }

        public void Kick(Vector3 dir, float amount)
        {
            if (amount > 0f && cam && cam.TryGetComponent<CameraRig>(out var rig)) rig.Kick(dir, amount);
        }

        public void HitKick(Vector3 dir, float amount)
        {
            if (amount <= 0f || Time.unscaledTime < nextHitKickAt) return;
            nextHitKickAt = Time.unscaledTime + feedback.hitKickInterval;
            Kick(dir, amount);
        }

        void Start() => V2Start();

        public void BeginPlaying() => ExecuteUiCommand(ArenaUiCommand.Start);

        public void ToggleArt() => Art.Enabled = !Art.Enabled;

        void Update() => V2Update();

        void LateUpdate() => V2LateUpdate();

        public void ClearTransients()
        {
            if (runWeapons) runWeapons.ClearWaveEffects();
            WorldMap.Active?.ClearTemporaryObjects();
        }

        public Material Mat(Color c)
        {
            c.a = 1f;
            if (matCache.TryGetValue(c, out var m)) return m;
            m = new Material(litMaterial);
            m.color = c;
            matCache[c] = m;
            return m;
        }
    }
}