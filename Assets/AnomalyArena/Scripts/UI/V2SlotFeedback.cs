using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{

    [DisallowMultipleComponent]
    public sealed class V2SlotFeedback : MonoBehaviour
    {
        public enum Kind { Hand, Auto, Skill }

        const int Glow = 12, CutCorner = 16, TagCut = 5;
        const float FrameInset = 1.5f, EdgeHalfWidth = 1.25f, GlowStrength = .6f;
        const float AttackSeconds = .24f, CastSeconds = .42f, SpinSeconds = .46f, SpinFadeSeconds = .2f, SwapSeconds = .3f;

        const float RapidGap = .2f, RapidPeak = .7f;
        const float AttackFill = .42f, CastFill = .55f, SwapFill = .3f, OtherHandAlpha = .6f, IconPop = .16f, ClockFollow = 12f;

        const float PictureScale = .88f;
        static readonly Color InUse = new(.45f, .9f, 1f), Attack = new(1f, .92f, .68f), Cast = new(.86f, .78f, 1f), Clock = new(.62f, .52f, 1f, .3f);
        static readonly Color TagText = new(.02f, .07f, .12f);
        static readonly Dictionary<(Vector2Int size, int cut), (Sprite fill, Sprite edge)> Shapes = new();

        Kind kind;
        int index;
        V2UiSlot slot;
        V2SlotFeedback partner;
        ArenaUiSlot model;
        Text keyLabel;
        int shownKey = -1, seenPulses;
        float flash, flashSeconds = AttackSeconds, fillPeak, lastPulse = -1f, shownClock, spinAge = -1f;
        Color flashColour;
        bool wasInUse;
        Vector2 builtFor;
        Image clockImage, flashImage, edgeImage;
        CanvasGroup group;
        GameObject tag;
        Text tagLabel;
        float appliedEdgeAlpha = -1f, appliedFlashAlpha = -1f, appliedClock = -1f, appliedPop = -1f;
        Color appliedEdgeColour;

        public bool IsInUse => kind == Kind.Hand && model != null && model.active && model.formIndex >= 0;

        public bool IsOtherHand => kind == Kind.Hand && !IsInUse && partner && partner.IsInUse;
        public bool TagShown => tag && tag.activeSelf;
        public float Flash => flash;
        public float ClockShown => shownClock;

        public float Spin => spinAge < 0f ? -1f : Mathf.Clamp01(spinAge / SpinSeconds);
        public float EdgeAlpha => appliedEdgeAlpha;

        public static void Add(V2UiSlot[] slots, Kind kind)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i]) continue;
                var feedback = slots[i].gameObject.AddComponent<V2SlotFeedback>();
                feedback.kind = kind;
                feedback.index = i;
                feedback.slot = slots[i];
                slots[i].feedback = feedback;
            }
            if (kind == Kind.Hand && slots.Length == 2 && slots[0] && slots[1])
            {
                slots[0].feedback.partner = slots[1].feedback;
                slots[1].feedback.partner = slots[0].feedback;
            }
        }

        public void Bind(ArenaUiSlot value)
        {
            if (model == value) return;
            model = value;
            seenPulses = value.pulses;
        }

        void OnEnable() => ArenaText.Changed += Relabel;
        void OnDisable() => ArenaText.Changed -= Relabel;

        void Relabel()
        {
            shownKey = -1;
            if (tagLabel) tagLabel.text = "";
        }

        void Update()
        {
            if (model == null) return;
            var size = ((RectTransform)transform).rect.size;
            if (size != builtFor) Build(size);
            if (!flashImage) return;
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;

            if (model.pulses != seenPulses)
            {
                if (model.pulses > seenPulses) Pulse(now);
                seenPulses = model.pulses;
            }
            bool inUse = IsInUse;

            if (inUse && !wasInUse) Light(1f, SwapSeconds, SwapFill, InUse);
            wasInUse = inUse;
            flash = Mathf.Max(0f, flash - dt / flashSeconds);

            if (kind == Kind.Hand) RenderHand(inUse, dt);
            if (kind == Kind.Skill) RenderSkill(dt);
            float flashAlpha = fillPeak * flash * flash;
            if (!Mathf.Approximately(flashAlpha, appliedFlashAlpha))
            {
                flashImage.color = new Color(flashColour.r, flashColour.g, flashColour.b, flashAlpha);
                flashImage.enabled = flashAlpha > 0f;
                appliedFlashAlpha = flashAlpha;
            }
            RenderEdge(inUse);
            float pop = 1f + IconPop * flash * flash;
            if (!Mathf.Approximately(pop, appliedPop))
            {
                if (slot.weaponIcon) slot.weaponIcon.rectTransform.localScale = Vector3.one * pop;
                if (slot.contentIcon) slot.contentIcon.rectTransform.localScale = Vector3.one * pop;
                appliedPop = pop;
            }
        }

        void Pulse(float now)
        {
            if (kind == Kind.Skill)
            {
                Light(1f, CastSeconds, CastFill, Cast);
                spinAge = 0f;
                shownClock = 1f;
            }
            else Light(now - lastPulse < RapidGap ? RapidPeak : 1f, AttackSeconds, AttackFill, Attack);
            lastPulse = now;
        }

        void Light(float strength, float seconds, float peak, Color colour)
        {
            flash = Mathf.Max(flash, strength);
            flashSeconds = seconds;
            fillPeak = peak;
            flashColour = colour;
        }

        void RenderHand(bool inUse, float dt)
        {
            if (tag.activeSelf != inUse) tag.SetActive(inUse);
            if (inUse)
            {
                if (tagLabel.text.Length == 0) tagLabel.text = ArenaText.Pair("IN USE","IN USE");
                if (tagLabel.font != slot.title.font) tagLabel.font = slot.title.font;
            }
            float alpha = Mathf.MoveTowards(group.alpha, IsOtherHand ? OtherHandAlpha : 1f, dt * 6f);
            if (!Mathf.Approximately(alpha, group.alpha)) group.alpha = alpha;

            int keyState = model.active ? 0 : model.formIndex >= 0 ? 1 : 2;
            if (keyState == shownKey || !keyLabel || !ArenaText.Ready) return;
            shownKey = keyState;
            int number = index + 1;
            keyLabel.text = keyState == 0 ? ArenaText.Pair($"HAND {number} [LMB]",$"HAND {number} [LMB]")
                : keyState == 1 ? ArenaText.Pair($"HAND {number} [RMB SWITCH]",$"HAND {number} [RMB SWITCH]")
                : ArenaText.Pair($"HAND {number}",$"HAND {number}");
        }

        void RenderSkill(float dt)
        {
            float target = Mathf.Clamp01(model.cooldown01);
            shownClock = target >= shownClock ? target : Mathf.Lerp(shownClock, target, 1f - Mathf.Exp(-dt * ClockFollow));
            if (target <= 0f && shownClock < .004f) shownClock = 0f;
            if (!Mathf.Approximately(shownClock, appliedClock))
            {
                clockImage.fillAmount = shownClock;
                clockImage.enabled = shownClock > 0f;
                appliedClock = shownClock;
            }
            if (spinAge >= 0f)
            {
                spinAge += dt;
                if (spinAge > SpinSeconds + SpinFadeSeconds) spinAge = -1f;
            }
        }

        void RenderEdge(bool inUse)
        {
            Color colour;
            float alpha;
            if (kind == Kind.Skill)
            {
                colour = Cast;
                alpha = spinAge < 0f ? 0f : 1f - Mathf.Clamp01((spinAge - SpinSeconds) / SpinFadeSeconds);
                float around = Spin;
                if (around >= 0f && !Mathf.Approximately(edgeImage.fillAmount, around)) edgeImage.fillAmount = around;
            }
            else
            {
                float lit = flash * flash;
                colour = Color.Lerp(inUse ? InUse : flashColour, flashColour, lit);
                alpha = inUse ? 1f : lit;
            }
            if (Mathf.Approximately(alpha, appliedEdgeAlpha) && colour == appliedEdgeColour) return;
            edgeImage.color = new Color(colour.r, colour.g, colour.b, alpha);
            edgeImage.enabled = alpha > 0f;
            appliedEdgeAlpha = alpha;
            appliedEdgeColour = colour;
        }

        void Build(Vector2 size)
        {
            builtFor = size;
            var whole = new Vector2Int(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            if (whole.x <= 0 || whole.y <= 0) return;
            var (fill, edge) = ShapesFor(whole, CutCorner);
            if (!flashImage)
            {

                clockImage = Overlay("CooldownClock", 0);
                flashImage = Overlay("Flash", 1);
                edgeImage = Overlay("LitEdge", 2);
                clockImage.type = Image.Type.Filled;
                clockImage.fillMethod = Image.FillMethod.Radial360;
                clockImage.fillOrigin = (int)Image.Origin360.Top;

                clockImage.fillClockwise = false;
                clockImage.color = Clock;
                if (kind == Kind.Skill)
                {
                    edgeImage.type = Image.Type.Filled;
                    edgeImage.fillMethod = Image.FillMethod.Radial360;
                    edgeImage.fillOrigin = (int)Image.Origin360.Top;
                    edgeImage.fillClockwise = true;
                }
                if (kind == Kind.Hand) BuildHandParts();
                ShrinkPicture(slot.weaponIcon);
                ShrinkPicture(slot.contentIcon);
                ShrinkPicture(slot.icon);
            }
            clockImage.sprite = fill;
            flashImage.sprite = fill;
            edgeImage.sprite = edge;
        }

        static void ShrinkPicture(Image picture)
        {
            if (!picture) return;
            var rect = picture.rectTransform;
            if (rect.anchorMin != rect.anchorMax) return;
            var size = rect.rect.size;
            rect.anchoredPosition += new Vector2((.5f - rect.pivot.x) * size.x, (.5f - rect.pivot.y) * size.y);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size * PictureScale;
        }

        void BuildHandParts()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var key = transform.Find("SlotKey");
            keyLabel = key ? key.GetComponent<Text>() : null;

            if (key && key.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>() is { } localizer) Destroy(localizer);

            const int tagWidth = 62, tagHeight = 20;
            tag = new GameObject("InUseTag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tag.transform.SetParent(transform, false);
            var back = tag.GetComponent<Image>();
            back.raycastTarget = false;
            back.sprite = ShapesFor(new Vector2Int(tagWidth, tagHeight), TagCut).fill;
            back.color = InUse;
            var rect = back.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(tagWidth + Glow * 2f, tagHeight + Glow * 2f);
            rect.anchoredPosition = Vector2.zero;

            tagLabel = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            tagLabel.transform.SetParent(tag.transform, false);
            tagLabel.raycastTarget = false;
            tagLabel.font = slot.title.font;
            tagLabel.fontSize = 13;
            tagLabel.fontStyle = FontStyle.Bold;
            tagLabel.color = TagText;
            tagLabel.alignment = TextAnchor.MiddleCenter;
            tagLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            tagLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var label = tagLabel.rectTransform;
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            tag.SetActive(false);
        }

        Image Overlay(string name, int order)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(transform, false);
            image.transform.SetSiblingIndex(order);
            image.raycastTarget = false;
            image.enabled = false;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * -Glow;
            rect.offsetMax = Vector2.one * Glow;
            return image;
        }

        static (Sprite fill, Sprite edge) ShapesFor(Vector2Int size, int cut)
        {
            if (Shapes.TryGetValue((size, cut), out var known) && known.fill) return known;
            int w = size.x + Glow * 2, h = size.y + Glow * 2;
            var fill = new Color32[w * h];
            var edge = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = DistanceToFrame(x + .5f - Glow, y + .5f - Glow, size, cut);
                    fill[x + y * w] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(.5f - d)));
                    float line = Mathf.Clamp01(EdgeHalfWidth + .5f - Mathf.Abs(d));
                    float glow = d > 0f ? Mathf.Pow(Mathf.Clamp01(1f - d / (Glow - FrameInset)), 2f) * GlowStrength : 0f;
                    edge[x + y * w] = new Color32(255, 255, 255, (byte)(255f * Mathf.Max(line, glow)));
                }
            var shapes = (Sprite(fill, w, h), Sprite(edge, w, h));
            Shapes[(size, cut)] = shapes;
            return shapes;
        }

        static float DistanceToFrame(float x, float y, Vector2Int size, int cut)
        {
            float left = FrameInset - x, right = x - (size.x - FrameInset), bottom = FrameInset - y, top = y - (size.y - FrameInset);
            float topLeft = (left + top + cut) * .7071f, bottomRight = (right + bottom + cut) * .7071f;
            return Mathf.Max(Mathf.Max(left, right), Mathf.Max(bottom, top), Mathf.Max(topLeft, bottomRight));
        }

        static Sprite Sprite(Color32[] pixels, int w, int h)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
