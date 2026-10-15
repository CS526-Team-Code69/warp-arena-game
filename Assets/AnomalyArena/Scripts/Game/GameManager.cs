using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AnomalyArena
{
    public enum GameState { Title, Playing, Won, Lost }

    [DefaultExecutionOrder(-100)]
    public partial class GameManager : MonoBehaviour
    {
        [Serializable]
        public class Rules
        {
            [Tooltip("Damage for hitting a wall after being knocked back / thrown (ignores protection)")] public float wallDamage = 5f;
            [Tooltip("Speed toward the wall at impact must exceed this to count as a hit (to be tuned)")] public float wallHitSpeed = 3f;
            [Tooltip("Knockback damping: initial speed = distance × damping")] public float knockDamping = 6f;
        }

        [Serializable]
        public class Feedback
        {
            [Tooltip("Short feedback for rapid bullets; capped by a real-time interval to avoid continuous freezing.")] public float bulletHitStop=.025f;
            public float bulletHitStopInterval=.12f;
            [Min(0f), Tooltip("V2: maximum real-time pause for a direct hit on the player. Enemy hits never pause the world.")]
            public float playerHitStopLimit = .04f;
            [Min(.1f), Tooltip("V2: minimum real time between global pauses; repeated hits never extend a pause.")]
            public float globalHitStopInterval = .35f;
            [Tooltip("Seconds the white flash lasts")] public float flashTime = 0.1f;
            [Tooltip("How much the body is squashed on hit (0 = no deformation)")] public float squash = 0.35f;
            [Tooltip("Hit stop for a normal hit, in seconds (real time)")] public float hitStop = 0.035f;
            [Tooltip("Hit stop for a heavy hit (damage reaches heavyDamage, wall impact, explosion, player hit), in seconds")] public float heavyHitStop = 0.08f;
            [Tooltip("Damage at which a hit counts as heavy")] public float heavyDamage = 10f;
            [Tooltip("Time scale during hit stop")] public float hitStopTimeScale = 0.05f;
            [Tooltip("Screen shake amplitude when the player is hit (units)")] public float playerHitShake = 0.5f;
            [Tooltip("Screen shake amplitude when an enemy takes a heavy hit (units)")] public float heavyShake = 0.25f;
            [Tooltip("How many units of shake decay per second")] public float shakeDecay = 4f;
            [Header("V4 local impact (no global pause)")]
            [Min(0f), Tooltip("Seconds the struck enemy alone holds its pose and animation on a normal hit.")] public float enemyHitPause = .045f;
            [Min(0f), Tooltip("Local pose hold on a heavy hit (damage ≥ heavyDamage × wave scale, slam or blast).")] public float enemyHeavyHitPause = .085f;
            [Min(0f), Tooltip("Local pose hold on a critical hit.")] public float critHitPause = .11f;
            [Min(0f), Tooltip("Small camera kick when the player lands a heavy hit.")] public float enemyHeavyShake = .1f;
            [Min(0f), Tooltip("Camera kick when the player lands a critical hit.")] public float critShake = .16f;
            [Min(0f), Tooltip("Minimum real seconds between hit kicks so crowds do not blur the screen.")] public float hitKickInterval = .12f;
        }

        public static GameManager Instance { get; private set; }
        public static int WallLayer { get; private set; }
        public static int CharacterLayer { get; private set; }
        public static int WallMask { get; private set; }
        public static int CharacterMask { get; private set; }

        public Rules rules = new Rules();
        public Feedback feedback = new Feedback();

        [Header("Refs")]
        public PlayerController player;
        public WaveManager waves;
        public WeaponSpawner weapons;
        public Hud hud;
        public Camera cam;
        [Header("V2.0 run")] public bool useV2;

        [Header("Materials")]
        [Tooltip("Base material for whitebox objects created at runtime (URP Lit)")] public Material litMaterial;
        [Tooltip("Material for translucent hints (URP Unlit transparent)")] public Material fxMaterial;
        public Material spawnMarkerMaterial;

        [Header("Art")]
        [Tooltip("Illustrated-mode textures; whitebox / illustrated is switched on the title screen")] public ArtSet art;

        public GameState State { get; private set; } = GameState.Title;
        public DeathCause LoseCause { get; private set; }
        public float Elapsed { get; private set; }

        readonly List<MonoBehaviour> transients = new List<MonoBehaviour>();
        readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
        bool finalWaveCleared;

        static bool skipTitle;
        float hitStopLeft;
        float nextGlobalHitStopAt;
        float nextBulletHitStopAt;
        public void TryBulletHitStop()
        {
            if(Time.unscaledTime<nextBulletHitStopAt)return;
            nextBulletHitStopAt=Time.unscaledTime+feedback.bulletHitStopInterval;
            HitStop(feedback.bulletHitStop);
            Shake(feedback.heavyShake*.25f);
        }
        bool hitStopStartedThisFrame;
        float shake;
        Vector3 camBase;

        void Awake()
        {
            Instance = this;
            WallLayer = LayerMask.NameToLayer("Wall");
            CharacterLayer = LayerMask.NameToLayer("Character");
            WallMask = 1 << WallLayer;
            CharacterMask = 1 << CharacterLayer;
            if (!cam) cam = Camera.main;
            if (cam) camBase = cam.transform.position;

            Time.timeScale = 1f;
        }

        public void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            if (useV2)
            {
                if (!IsCombatActive || hitStopLeft > 0f || Time.unscaledTime < nextGlobalHitStopAt) return;
                seconds = Mathf.Min(seconds, feedback.playerHitStopLimit);
                if (seconds <= 0f) return;
                nextGlobalHitStopAt = Time.unscaledTime + Mathf.Max(seconds, feedback.globalHitStopInterval);
            }
            hitStopLeft = Mathf.Max(hitStopLeft, seconds);

            Time.timeScale = useV2 ? Mathf.Max(.25f, feedback.hitStopTimeScale) : feedback.hitStopTimeScale;
            hitStopStartedThisFrame = true;
        }

        public void Shake(float amount){if(useV2&&cam&&cam.TryGetComponent<CameraRig>(out var rig))rig.Shake(amount);else shake=Mathf.Max(shake,amount);}

        void TickHitStop()
        {
            if (hitStopLeft <= 0f) return;
            if (useV2)
            {
                hitStopLeft = Mathf.Max(0f, hitStopLeft - Time.unscaledDeltaTime);
                Time.timeScale = !CanPlayerMove ? 0f : hitStopLeft > 0f ? Mathf.Max(.25f, feedback.hitStopTimeScale) : 1f;
                return;
            }

            if (hitStopStartedThisFrame) hitStopStartedThisFrame = false;
            else hitStopLeft -= Time.unscaledDeltaTime;
            Time.timeScale = hitStopLeft > 0f ? feedback.hitStopTimeScale : 1f;
        }

        void TickShake()
        {
            if(useV2)return;
            if (!cam) return;

            shake = Mathf.MoveTowards(shake, 0f, feedback.shakeDecay * Time.unscaledDeltaTime);
            Vector2 o = shake > 0f ? UnityEngine.Random.insideUnitCircle * shake : Vector2.zero;
            cam.transform.position = camBase + new Vector3(o.x, 0f, o.y);
        }

        void Start()
        {
            if(useV2){V2Start();return;}
            if (Art.On) Art.DecorateArena();
            weapons.SpawnInitial();
            if (!skipTitle) return;
            skipTitle = false;
            BeginPlaying();
        }

        public void BeginPlaying()
        {
            if(useV2){ExecuteUiCommand(ArenaUiCommand.Start);return;}
            if (State != GameState.Title) return;
            State = GameState.Playing;
            waves.StartWaves();
        }

        public void ToggleArt()
        {
            if(useV2){Art.Enabled=!Art.Enabled;return;}
            if (State != GameState.Title) return;
            Art.Enabled = !Art.Enabled;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Update()
        {
            if(useV2){V2Update();return;}
            TickHitStop();
            var kb = Keyboard.current;
            Cursor.visible = State != GameState.Playing;

            switch (State)
            {
                case GameState.Title:

                    if (kb != null && kb.spaceKey.wasPressedThisFrame) BeginPlaying();
                    else if (kb != null && kb.tKey.wasPressedThisFrame) ToggleArt();
                    break;
                case GameState.Playing:
                    Elapsed += Time.deltaTime;
                    break;
                default:
                    if (kb != null && kb.rKey.wasPressedThisFrame)
                    {
                        skipTitle = true;
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    }
                    break;
            }
        }

        public void NotifyFinalWaveCleared() => finalWaveCleared = true;

        void LateUpdate()
        {
            if(useV2){V2LateUpdate();return;}
            TickShake();
            if (State != GameState.Playing) return;
            if (finalWaveCleared)
            {
                State = GameState.Won;
                ClearTransients();
            }
            else if (!player.IsAlive)
            {
                State = GameState.Lost;
                LoseCause = player.DeathCause;
                ClearTransients();
            }
        }

        public void Register(MonoBehaviour t)
        {
            if (t && !transients.Contains(t)) transients.Add(t);
        }

        public void Unregister(MonoBehaviour t) => transients.Remove(t);

        public void ClearTransients()
        {
            if(useV2&&runWeapons)runWeapons.ClearWaveEffects();
            if(useV2)WorldMap.Active?.ClearTemporaryObjects();
            var copy = transients.ToArray();
            transients.Clear();
            foreach (var t in copy)
            {
                if (!t) continue;
                if (t is WeaponRuntime r) r.Cancel();
                else Destroy(t.gameObject);
            }
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

        public Material FxMat(Color c)
        {
            var m = new Material(fxMaterial);
            m.color = c;
            return m;
        }
    }
}
