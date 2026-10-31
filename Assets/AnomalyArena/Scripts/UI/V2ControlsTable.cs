using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    // The controls table on the pause screen: one row per GDD control, a key cap and then what it does.
    public sealed class V2ControlsTable : MonoBehaviour
    {
        const string PillSprite = "V5/UI/ui-pill";
        const float KeyGap = 14f, ColumnGap = 36f;
        static readonly Color KeyFill = new(.06f, .13f, .22f, .92f), KeyText = new(.50f, .86f, 1f), ActionText = new(.86f, .90f, .96f);
        const string NoteColour = "#8E9DB4";
        static Sprite pill;

        readonly List<Text> labels = new();
        public readonly List<Text> keys = new(), actions = new();

        public static V2ControlsTable Create(Transform parent, int columns, float rowHeight, float keyWidth, int fontSize)
        {
            var go = new GameObject("ControlsTable", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var table = go.AddComponent<V2ControlsTable>();
            table.Build(columns, rowHeight, keyWidth, fontSize);
            return table;
        }

        public RectTransform Rect => (RectTransform)transform;

        public void SetFont(Font font)
        {
            if (!font) return;
            foreach (var label in labels) if (label.font != font) label.font = font;
        }

        void Build(int columns, float rowHeight, float keyWidth, int fontSize)
        {
            pill ??= LoadSprite(PillSprite, 16f);
            var area = Rect;
            int rows = Mathf.CeilToInt((float)V2Hud.Controls.Length / columns);
            for (int i = 0; i < V2Hud.Controls.Length; i++)
            {
                int column = i / rows, row = i % rows;
                var line = new GameObject("Row" + (i + 1), typeof(RectTransform)).GetComponent<RectTransform>();
                line.SetParent(area, false);
                // Columns share the width; rows run down each column in the document's order.
                float from = (float)column / columns, to = (float)(column + 1) / columns;
                line.anchorMin = new Vector2(from, 1f);
                line.anchorMax = new Vector2(to, 1f);
                line.pivot = new Vector2(0f, 1f);
                line.offsetMin = new Vector2(column > 0 ? ColumnGap * .5f : 0f, -(row + 1) * rowHeight);
                line.offsetMax = new Vector2(column < columns - 1 ? -ColumnGap * .5f : 0f, -row * rowHeight);

                var (key, note) = Split(V2Hud.Controls[i].input);
                var cap = new GameObject("KeyCap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                cap.transform.SetParent(line, false);
                cap.raycastTarget = false;
                cap.sprite = pill;
                cap.type = Image.Type.Sliced;
                cap.pixelsPerUnitMultiplier = 2f;
                cap.color = pill ? Color.white : KeyFill;
                var capRect = cap.rectTransform;
                capRect.anchorMin = new Vector2(0f, .5f);
                capRect.anchorMax = new Vector2(0f, .5f);
                capRect.pivot = new Vector2(0f, .5f);
                capRect.sizeDelta = new Vector2(keyWidth, Mathf.Min(rowHeight - 6f, 30f));
                capRect.anchoredPosition = Vector2.zero;
                var keyLabel = NewText(capRect, "Key", fontSize, TextAnchor.MiddleCenter, FontStyle.Bold);
                keyLabel.color = KeyText;
                keyLabel.text = key;
                keyLabel.rectTransform.anchorMin = Vector2.zero;
                keyLabel.rectTransform.anchorMax = Vector2.one;
                keyLabel.rectTransform.offsetMin = new Vector2(6f, 0f);
                keyLabel.rectTransform.offsetMax = new Vector2(-6f, 0f);
                keys.Add(keyLabel);

                var action = NewText(line, "Action", fontSize - 2, TextAnchor.MiddleLeft, FontStyle.Normal);
                // One size for every row: a long one wraps onto a second line instead of shrinking.
                action.resizeTextForBestFit = false;
                action.lineSpacing = .92f;
                action.color = ActionText;
                action.text = note == "" ? V2Hud.Controls[i].action : $"{V2Hud.Controls[i].action}  <color={NoteColour}>{note}</color>";
                var actionRect = action.rectTransform;
                actionRect.anchorMin = Vector2.zero;
                actionRect.anchorMax = Vector2.one;
                actionRect.offsetMin = new Vector2(keyWidth + KeyGap, 0f);
                actionRect.offsetMax = Vector2.zero;
                actions.Add(action);
            }
        }

        // "Left mouse button (hold to charge…)" puts "Left mouse button" on the cap and the rest after the action, in grey.
        public static (string key, string note) Split(string input)
        {
            int cut = input.IndexOf(" (", System.StringComparison.Ordinal);
            if (cut > 0) return (input.Substring(0, cut), input.Substring(cut + 1));
            cut = input.IndexOf(" while ", System.StringComparison.Ordinal);
            if (cut > 0) return (input.Substring(0, cut), input.Substring(cut + 1));
            return (input, "");
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
            // Long rows shrink rather than spill out of their cell.
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
