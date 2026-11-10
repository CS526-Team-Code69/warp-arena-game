using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The interface's frames and plates in whitebox (2026-11-10, second round: the skinned frames, plates and buttons become plain too,
    /// and come back as they were with the art on). An entry remembers an image's illustrated skin (its sprite, how it is drawn, its colour,
    /// whether it shows) when it is added, and swaps between that and a flat look as Art.Enabled changes:
    /// - panels, cards, plates and buttons: a plain rectangle in a flat colour with a thin edge (an Outline);
    /// - a frame whose colour its owner keeps setting (a HUD slot): the plain rectangle and the edge, the colour left to the owner;
    /// - the darkened screen behind a panel: a flat dark wash;
    /// - bars: a plain square sprite, so they still fill, in their own colour;
    /// - decorations (glows, clouds, rules, the panel's nebula): not drawn.
    /// The flat colours are the campaign panels' own (V2CampaignUi), in the colour family of the whitebox backdrop (WhiteboxBackdrop), so
    /// text on them reads as on the base panel. A Set follows the switch for all its entries; its owner calls Follow every frame.
    /// </summary>
    public static class WhiteboxSkin
    {
        public enum Look { Panel, Card, Plate, Button, PrimaryButton, Dim, Frame, Bar, Hide }

        public static readonly Color PanelFill = new(.04f, .07f, .12f, .96f), CardFill = new(.07f, .11f, .18f, 1f), PlateFill = new(.05f, .08f, .13f, 1f),
            ButtonFill = new(.1f, .15f, .22f, 1f), PrimaryFill = new(.12f, .32f, .5f, 1f), DimFill = new(0f, .01f, .03f, .6f), Edge = new(.3f, .55f, .85f, .8f);
        /// <summary>How far the thin edge reaches outside the rectangle, in canvas units.</summary>
        public const float EdgeWidth = 1.5f;

        static Sprite square;

        /// <summary>A plain white square (no border), for images that must keep a sprite: a filled bar fills only with one.</summary>
        public static Sprite Square
        {
            get
            {
                if (square) return square;
                var texture = Texture2D.whiteTexture;
                square = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                square.name = "Whitebox square";
                square.hideFlags = HideFlags.DontSave;
                return square;
            }
        }

        /// <summary>The flat fill of a look (clear for the looks that keep their owner's colour).</summary>
        public static Color FillOf(Look look) => look switch
        {
            Look.Panel => PanelFill,
            Look.Card => CardFill,
            Look.Plate => PlateFill,
            Look.Button => ButtonFill,
            Look.PrimaryButton => PrimaryFill,
            Look.Dim => DimFill,
            _ => Color.clear
        };

        /// <summary>The looks whose colour the flat look sets (and gives back with the art on).</summary>
        public static bool OwnsColour(Look look) => look is Look.Panel or Look.Card or Look.Plate or Look.Button or Look.PrimaryButton or Look.Dim;

        /// <summary>The looks drawn with a thin edge.</summary>
        public static bool HasEdge(Look look) => look is Look.Panel or Look.Card or Look.Plate or Look.Button or Look.PrimaryButton or Look.Frame;

        /// <summary>One image and its two looks.</summary>
        public sealed class Entry
        {
            public readonly Image image;
            public readonly Look look;
            readonly Sprite sprite;
            readonly Image.Type type;
            readonly float density;
            readonly Color colour;
            readonly bool shown;
            Color edge = Edge;
            Outline outline;
            bool? illustrated;

            /// <summary>Remembers the image's look as it is now as its illustrated look.</summary>
            public Entry(Image image, Look look)
            {
                this.image = image;
                this.look = look;
                if (!image) return;
                sprite = image.sprite;
                type = image.type;
                density = image.pixelsPerUnitMultiplier;
                colour = image.color;
                shown = image.enabled;
            }

            /// <summary>Whether the illustrated look is showing (null before the first Follow).</summary>
            public bool? Illustrated => illustrated;

            /// <summary>The thin edge's colour in whitebox (a card's rarity).</summary>
            public void SetEdge(Color colour)
            {
                edge = colour;
                if (outline) outline.effectColor = colour;
            }

            /// <summary>Shows the look that goes with the art switch, if it is not showing yet.</summary>
            public void Follow()
            {
                if (illustrated != Art.Enabled) Show(Art.Enabled);
            }

            public void Show(bool art)
            {
                illustrated = art;
                if (!image) return;
                if (art)
                {
                    if (look == Look.Hide) { image.enabled = shown; return; }
                    image.sprite = sprite;
                    image.type = type;
                    image.pixelsPerUnitMultiplier = density;
                    if (OwnsColour(look)) image.color = colour;
                    if (outline) outline.enabled = false;
                    return;
                }
                switch (look)
                {
                    case Look.Hide:
                        image.enabled = false;
                        return;
                    case Look.Bar:
                        image.sprite = Square; // keeps its fill and its colour
                        return;
                }
                image.sprite = null;
                image.type = Image.Type.Simple;
                if (OwnsColour(look)) image.color = FillOf(look);
                if (!HasEdge(look)) return;
                if (!outline)
                {
                    outline = image.gameObject.AddComponent<Outline>();
                    outline.useGraphicAlpha = false;
                    outline.effectDistance = new Vector2(EdgeWidth, -EdgeWidth);
                }
                outline.effectColor = edge;
                outline.enabled = true;
            }
        }

        /// <summary>A group of entries that follows the switch together.</summary>
        public sealed class Set
        {
            readonly List<Entry> entries = new();
            bool? illustrated;

            public int Count => entries.Count;

            /// <summary>Adds an image with its look as it is now as the illustrated one; it takes the current look at once.</summary>
            public Entry Add(Image image, Look look)
            {
                var entry = new Entry(image, look);
                if (!image) return entry;
                entries.Add(entry);
                if (illustrated.HasValue) entry.Show(illustrated.Value);
                return entry;
            }

            /// <summary>Called every frame: when the switch has moved, every entry takes its new look.</summary>
            public void Follow()
            {
                if (illustrated == Art.Enabled) return;
                illustrated = Art.Enabled;
                foreach (var entry in entries) entry.Show(illustrated.Value);
            }
        }
    }
}
