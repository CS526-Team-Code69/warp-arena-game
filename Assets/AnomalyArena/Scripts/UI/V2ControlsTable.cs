using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{

    public sealed class V2ControlsTable : MonoBehaviour
    {
        const string PillSprite = "V5/UI/ui-pill";
        const float KeyGap = 14f;

        static readonly Color KeyTint = new(.50f, .82f, 1f), KeyFill = new(.06f, .13f, .22f, .92f), KeyText = new(.50f, .86f, 1f), ActionText = new(.86f, .90f, .96f);
        const string NoteColour = "#8E9DB4";
        static Sprite pill;

        readonly List<Text> labels = new();
        readonly List<RectTransform> lines = new();
        public readonly List<Text> keys = new(), actions = new();
        float keyWidth;
        bool filled;
        Font filledWith;

        public static V2ControlsTable Create(Transform parent, float rowHeight, float keyWidth, int fontSize)
        {
            var go = new GameObject("ControlsTable", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var table = go.AddComponent<V2ControlsTable>();
            table.Build(rowHeight, keyWidth, fontSize);
            return table;
        }

        public RectTransform Rect => (RectTransform)transform;

        public void Fill(Font font)
        {
            if (filled && font == filledWith) return;
            filled = true;
            filledWith = font;
            var rows = V2Hud.ControlRows();
            for (int i = 0; i < rows.Length; i++)
            {
                keys[i].text = rows[i].input;
                actions[i].text = rows[i].note == "" ? rows[i].action : $"{rows[i].action}  <color={NoteColour}>{rows[i].note}</color>";
            }
            if (font) foreach (var label in labels) if (label.font != font) label.font = font;

            float widest = 0f;
            foreach (var action in actions) widest = Mathf.Max(widest, action.preferredWidth);
            float left = Mathf.Max(0f, (Rect.rect.width - keyWidth - KeyGap - widest) * .5f);
            foreach (var line in lines) line.offsetMin = new Vector2(left, line.offsetMin.y);
        }

        void Build(float rowHeight, float keyWidth, int fontSize)
        {
            this.keyWidth = keyWidth;
            pill ??= LoadSprite(PillSprite, 16f);
            var area = Rect;
            int rows = V2Hud.ControlRows().Length;
            for (int i = 0; i < rows; i++)
            {
                var line = new GameObject("Row" + (i + 1), typeof(RectTransform)).GetComponent<RectTransform>();
                line.SetParent(area, false);
                line.anchorMin = new Vector2(0f, 1f);
                line.anchorMax = new Vector2(1f, 1f);
                line.pivot = new Vector2(0f, 1f);
                line.offsetMin = new Vector2(0f, -(i + 1) * rowHeight);
                line.offsetMax = new Vector2(0f, -i * rowHeight);
                lines.Add(line);

                var cap = new GameObject("KeyCap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                cap.transform.SetParent(line, false);
                cap.raycastTarget = false;
                cap.sprite = pill;
                cap.type = Image.Type.Sliced;
                cap.pixelsPerUnitMultiplier = 2f;
                cap.color = pill ? KeyTint : KeyFill;
                var capRect = cap.rectTransform;
                capRect.anchorMin = new Vector2(0f, .5f);
                capRect.anchorMax = new Vector2(0f, .5f);
                capRect.pivot = new Vector2(0f, .5f);
                capRect.sizeDelta = new Vector2(keyWidth, Mathf.Min(rowHeight - 6f, 30f));
                capRect.anchoredPosition = Vector2.zero;
                var keyLabel = NewText(capRect, "Key", fontSize, TextAnchor.MiddleCenter, FontStyle.Bold);
                keyLabel.color = KeyText;
                keyLabel.rectTransform.anchorMin = Vector2.zero;
                keyLabel.rectTransform.anchorMax = Vector2.one;
                keyLabel.rectTransform.offsetMin = new Vector2(6f, 0f);
                keyLabel.rectTransform.offsetMax = new Vector2(-6f, 0f);
                keys.Add(keyLabel);

                var action = NewText(line, "Action", fontSize - 2, TextAnchor.MiddleLeft, FontStyle.Normal);

                action.resizeTextForBestFit = false;
                action.lineSpacing = .92f;
                action.color = ActionText;
                var actionRect = action.rectTransform;
                actionRect.anchorMin = Vector2.zero;
                actionRect.anchorMax = Vector2.one;
                actionRect.offsetMin = new Vector2(keyWidth + KeyGap, 0f);
                actionRect.offsetMax = Vector2.zero;
                actions.Add(action);
            }
        }

        Text NewText(Transform parent, string name, int size, TextAnchor anchor, FontStyle style)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.raycastTarget = false;
            label.supportRichText = true;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(9, size - 5);
            label.resizeTextMaxSize = size;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .55f);
            shadow.effectDistance = new Vector2(1f, -1f);
            labels.Add(label);
            return label;
        }

        static Sprite LoadSprite(string path, float border)
        {
            var texture = Resources.Load<Texture2D>(path);
            if (!texture) return null;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * border);
        }
    }
}
