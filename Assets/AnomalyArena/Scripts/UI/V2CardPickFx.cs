using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{

    public class V2CardPickFx : MonoBehaviour
    {
        [Tooltip("Full-screen wash in the rarity colour; shown for Epic and rarer cards.")] public Image screenTint;
        [Tooltip("Rotating light rays behind the card; shown for Epic and rarer cards.")] public Image rays;
        [Tooltip("Shock ring expanding from the card; shown for Rare and rarer cards.")] public Image ring;
        [Tooltip("Sliced glow outline laid over the picked card.")] public Image frame;
        [Tooltip("Reused spark images; the rarity decides how many fly.")] public Image[] sparks;
        [Tooltip("Inactive parent that receives the card copy while its buttons are removed.")] public RectTransform ghostHolder;
        [Tooltip("Seconds the whole flourish lasts.")] public float duration = .75f;
        [Tooltip("How far the frame sprite's bright edge sits inside its border, in UI units.")] public float frameInset = 40f;

        RectTransform ghost;
        CanvasGroup ghostGroup;
        float age = float.MaxValue, power;
        Color tint;
        Vector2 center, size;
        int sparkCount;
        Vector2[] sparkPosition, sparkVelocity;
        float[] sparkSpin, sparkSize;
        readonly System.Random random = new(526);

        public static float Power(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare => .45f,
            CardRarity.Exceptional => .6f,
            CardRarity.Epic => .8f,
            CardRarity.Legendary or CardRarity.Void => 1f,
            _ => .3f
        };

        public static int SparkCount(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare => 10,
            CardRarity.Exceptional => 14,
            CardRarity.Epic => 18,
            CardRarity.Legendary or CardRarity.Void => 24,
            _ => 6
        };

        public bool Playing => age < duration;

        public int Plays { get; private set; }

        void Awake() => Hide();

        void OnDisable()
        {
            age = float.MaxValue;
            Hide();
        }

        public void Play(RectTransform card, CardRarity rarity)
        {
            if (!card) return;
            var root = (RectTransform)transform;
            Vector3 world = card.TransformPoint(card.rect.center);
            center = (Vector2)root.InverseTransformPoint(world) - root.rect.center;
            float scale = root.lossyScale.x > 0f ? card.lossyScale.x / root.lossyScale.x : 1f;
            size = card.rect.size * scale;
            power = Power(rarity);
            tint = ArenaRarity.ColorFor(rarity);
            age = 0f;
            Plays++;
            MakeGhost(card);
            int count = sparks == null ? 0 : Mathf.Min(sparks.Length, SparkCount(rarity));
            if (sparkPosition == null || sparkPosition.Length < count)
            {
                sparkPosition = new Vector2[count];
                sparkVelocity = new Vector2[count];
                sparkSpin = new float[count];
                sparkSize = new float[count];
            }
            sparkCount = count;
            for (int i = 0; i < count; i++)
            {

                Vector2 half = size * .5f;
                float along = Next(-1f, 1f);
                Vector2 start = (i % 4) switch
                {
                    0 => new Vector2(along * half.x, half.y),
                    1 => new Vector2(half.x, along * half.y),
                    2 => new Vector2(along * half.x, -half.y),
                    _ => new Vector2(-half.x, along * half.y)
                };
                Vector2 direction = (start.normalized + new Vector2(Next(-.35f, .35f), Next(-.35f, .35f))).normalized;
                sparkPosition[i] = center + start;
                sparkVelocity[i] = direction * Next(260f, 620f) * (.7f + .5f * power);
                sparkSpin[i] = Next(-540f, 540f);
                sparkSize[i] = Next(14f, 26f) * (.8f + .4f * power);
                var spark = sparks[i];
                if (!spark) continue;
                spark.color = Color.Lerp(tint, Color.white, Next(0f, .6f));
                spark.enabled = true;
            }
            for (int i = count; i < (sparks?.Length ?? 0); i++)
                if (sparks[i]) sparks[i].enabled = false;
            Show(frame, true);
            Show(ring, power >= .5f);
            Show(rays, power >= .75f);
            Show(screenTint, power >= .75f);
            Tick(0f);
        }

        void LateUpdate()
        {
            if (age >= duration) return;
            Tick(Time.unscaledDeltaTime);
        }

        void Tick(float dt)
        {
            age += dt;
            float t = Mathf.Clamp01(age / Mathf.Max(.01f, duration));
            float fade = 1f - t;
            if (ghost)
            {
                float pop = age < .08f ? age / .08f : Mathf.Max(0f, 1f - (age - .08f) / .2f);
                float lift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.18f, duration, age));
                ghost.localScale = Vector3.one * (1f + (.05f + .07f * power) * pop + .06f * lift);
                ghost.anchoredPosition = center + Vector2.up * 24f * lift;
                ghostGroup.alpha = 1f - lift;
            }
            if (frame && frame.enabled)
            {
                Place(frame.rectTransform, center, size + Vector2.one * (frameInset * 2f + 36f * Mathf.Sqrt(t)));
                frame.color = WithAlpha(Color.Lerp(Color.white, tint, .6f), Mathf.Pow(fade, 1.4f) * (.75f + .25f * power));
            }
            float span = Mathf.Max(size.x, size.y);
            if (ring && ring.enabled)
            {
                float grow = 1f - (1f - t) * (1f - t);
                Place(ring.rectTransform, center, Vector2.one * span * Mathf.Lerp(.7f, 1.5f + .6f * power, grow));
                ring.color = WithAlpha(tint, fade * fade);
            }
            if (rays && rays.enabled)
            {
                Place(rays.rectTransform, center, Vector2.one * span * (1.8f + .6f * Mathf.Sqrt(t)) * (.8f + .3f * power));
                rays.rectTransform.localEulerAngles = new Vector3(0f, 0f, -35f * age);
                rays.color = WithAlpha(Color.Lerp(tint, Color.white, .25f), Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.15f)) * .75f);
            }
            if (screenTint && screenTint.enabled)
                screenTint.color = WithAlpha(tint, .16f * power * Mathf.Min(1f, age / .05f) * (1f - Mathf.InverseLerp(.05f, .45f, age)));
            float drag = Mathf.Exp(-3.5f * dt);
            for (int i = 0; i < sparkCount; i++)
            {
                var spark = sparks[i];
                if (!spark) continue;
                sparkPosition[i] += sparkVelocity[i] * dt;
                sparkVelocity[i] *= drag;
                Place(spark.rectTransform, sparkPosition[i], Vector2.one * sparkSize[i] * (1f - .5f * t));
                spark.rectTransform.localEulerAngles = new Vector3(0f, 0f, sparkSpin[i] * age);
                var c = spark.color;
                c.a = fade;
                spark.color = c;
            }
            if (age >= duration)
            {
                Hide();
                age = float.MaxValue;
            }
        }

        void MakeGhost(RectTransform card)
        {
            DropGhost();
            if (!ghostHolder) return;
            var copy = Instantiate(card.gameObject, ghostHolder, false);
            copy.name = "Picked Card";

            var parts = copy.GetComponentsInChildren<Component>(true);
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                var part = parts[i];
                if (part is Graphic graphic) { graphic.raycastTarget = false; continue; }
                if (part is Transform or CanvasRenderer or BaseMeshEffect or Mask or RectMask2D or CanvasGroup) continue;
                DestroyImmediate(part);
            }
            ghostGroup = copy.GetComponent<CanvasGroup>();
            if (!ghostGroup) ghostGroup = copy.AddComponent<CanvasGroup>();
            ghostGroup.blocksRaycasts = false;
            ghostGroup.interactable = false;
            ghostGroup.alpha = 1f;
            ghost = (RectTransform)copy.transform;
            ghost.SetParent(transform, false);
            ghost.SetSiblingIndex(rays ? rays.transform.GetSiblingIndex() + 1 : 0);
            ghost.anchorMin = ghost.anchorMax = ghost.pivot = new Vector2(.5f, .5f);
            ghost.sizeDelta = size;
            ghost.localRotation = Quaternion.identity;
            ghost.localScale = Vector3.one;
            ghost.anchoredPosition = center;
            if (!copy.activeSelf) copy.SetActive(true);
        }

        void DropGhost()
        {
            if (ghost) Destroy(ghost.gameObject);
            ghost = null;
            ghostGroup = null;
        }

        void Hide()
        {
            DropGhost();
            Show(screenTint, false);
            Show(rays, false);
            Show(ring, false);
            Show(frame, false);
            if (sparks != null) foreach (var spark in sparks) if (spark) spark.enabled = false;
        }

        float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);

        static void Show(Graphic graphic, bool on)
        {
            if (graphic && graphic.enabled != on) graphic.enabled = on;
        }

        static void Place(RectTransform rect, Vector2 at, Vector2 dimensions)
        {
            rect.anchoredPosition = at;
            rect.sizeDelta = dimensions;
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
