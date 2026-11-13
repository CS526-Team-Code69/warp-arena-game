using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    public sealed partial class V5Title
    {
        // ───── The collection: weapons, skills and monsters, from what ArenaCodex lists and ArenaCodexProgress keeps. One not met yet is
        // ？？？ and cannot be opened; a card met opens its details below the grid. ─────

        enum Shelf { Weapons, Skills, Monsters }

        sealed class CodexEntry
        {
            public Shelf shelf;
            public int id;
            public bool met;
            public string name;
            public Sprite picture;
            public Color? block;
            public SkillDefinition skill;
        }

        sealed class CardView
        {
            public V5Plate plate;
            public Image picture;
            public Text name, unknown;
        }

        const int CardCount = 18;
        const float ShelfTabW = 84f, CardGap = 6f, ShelfGridTop = 43.5f, ShelfGridH = 219f, DetailPad = 13.5f, DetailPicW = 129f;
        // The tags beside a detail's name: the gap after the name and between tags, and the least share of the line the name keeps.
        const float TagLead = 7.5f, TagGap = 4.5f, TitleShare = .55f;
        static readonly string[] ShelfNames = { "Weapons", "Skills", "Monsters" };
        static readonly Color TagInk = V5TitleKit.Rgb(11, 26, 42), WarmInk = V5TitleKit.Rgb(42, 18, 6);

        readonly V5Plate[] shelfTabs = new V5Plate[3];
        readonly Text[] shelfLabels = new Text[3];
        readonly CardView[] cards = new CardView[CardCount];
        readonly (Image back, Text text)[] tags = new (Image, Text)[2];
        readonly int[] shelfChosen = { -1, -1, -1 };
        readonly List<CodexEntry> entries = new();
        V5Block shelfRow, grid, detail;
        Text found, detailTitle, detailBody;
        Image detailPlate, detailPicture;
        Outline detailEdge;
        Shelf shelf;

        void BuildCodex()
        {
            var page = pageRoots[CollectionTab];
            shelfRow = Block(page, "Shelves", 0f, 0f, InnerW, RowH);
            for (int s = 0; s < 3; s++)
            {
                int index = s;
                var plate = V5Plate.Make(shelfRow.transform, ShelfNames[s], V5Plate.Look.Plate, 9f, () => OnShelf(index));
                plate.Place(s * (ShelfTabW + 6f), 0f, ShelfTabW, RowH);
                shelfLabels[s] = V5TitleKit.Label(plate.transform, "Label", 13, V5TitleKit.Ink2, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(shelfLabels[s].rectTransform, 4f);
                shelfTabs[s] = plate;
            }
            found = V5TitleKit.Label(shelfRow.transform, "Found", 11, V5TitleKit.Ink2, TextAnchor.MiddleRight);
            V5TitleKit.Place(found.rectTransform, 3f * (ShelfTabW + 6f), 0f, InnerW - 3f * (ShelfTabW + 6f), RowH);
            grid = Block(page, "Grid", 0f, ShelfGridTop, InnerW, ShelfGridH);
            for (int c = 0; c < CardCount; c++)
            {
                int index = c;
                var card = new CardView { plate = V5Plate.Make(grid.transform, "Card" + (c + 1).ToString("00"), V5Plate.Look.Plate, 8f, () => OnCard(index)) };
                card.plate.HoverPitch = 1.1f - c % 6 * .03f;
                card.picture = V5TitleKit.Picture(card.plate.transform, "Picture", null, Color.white);
                card.picture.preserveAspect = true;
                card.name = V5TitleKit.Label(card.plate.transform, "Name", 10, V5TitleKit.Ink2, TextAnchor.MiddleCenter);
                card.unknown = V5TitleKit.Label(card.plate.transform, "Unknown", 14, V5TitleKit.Rgb(233, 223, 201, .75f), TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(card.unknown.rectTransform);
                cards[c] = card;
            }
            float detailTop = ShelfGridTop + ShelfGridH + 10.5f;
            detail = Block(page, "Detail", 0f, detailTop, InnerW, BodyNoFootH - detailTop);
            detailPlate = V5TitleKit.Picture(detail.transform, "Plate", V5TitleKit.Piece("plate"), Color.white, V5TitleKit.PlateDensity(12f));
            V5TitleKit.Stretch(detailPlate.rectTransform);
            detailPicture = V5TitleKit.Picture(detail.transform, "Picture", null, Color.white);
            detailPicture.preserveAspect = true;
            detailTitle = V5TitleKit.Label(detail.transform, "Name", 18, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            for (int t = 0; t < tags.Length; t++)
            {
                var back = V5TitleKit.Picture(detail.transform, "Tag" + (t + 1), null, V5TitleKit.Steel);
                var text = V5TitleKit.Label(back.transform, "Words", 9, TagInk, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(text.rectTransform);
                tags[t] = (back, text);
            }
            detailBody = V5TitleKit.Label(detail.transform, "Text", 11, V5TitleKit.Ink2, TextAnchor.UpperLeft);
            V5TitleKit.Wrap(detailBody);
            detailBody.lineSpacing = 1.1f;
        }

        void ListEntries()
        {
            entries.Clear();
            var set = hud ? hud.art : null;
            bool on = Art.Enabled;
            switch (shelf)
            {
                case Shelf.Weapons:
                    foreach (var form in ArenaCodex.ListedWeapons())
                    {
                        int id = (int)form;
                        entries.Add(new CodexEntry
                        {
                            shelf = shelf, id = id, met = ArenaCodexProgress.WeaponDiscovered(form), name = ArenaTerms.WeaponForm(form),
                            picture = on ? At(set ? set.weaponSprites : null, id) : WhiteboxIcons.Weapon(id),
                        });
                    }
                    break;
                case Shelf.Skills:
                    foreach (var (id, data) in ArenaCodex.ListedSkills())
                        entries.Add(new CodexEntry
                        {
                            shelf = shelf, id = (int)id, met = ArenaCodexProgress.SkillDiscovered(id), name = ArenaTerms.Skill(id), skill = data,
                            picture = on ? At(set ? set.skillIcons : null, (int)id) : WhiteboxIcons.Skill((int)id),
                        });
                    break;
                default:
                    foreach (var kind in ArenaCodex.ListedMonsters())
                        entries.Add(new CodexEntry
                        {
                            shelf = shelf, id = (int)kind, met = ArenaCodexProgress.EnemyDiscovered(kind), name = ArenaText.Text("enemy." + kind),
                            picture = on ? ArenaCodex.EnemySprite(set, kind) : null, block = on ? null : ArenaCodex.MonsterColour(kind),
                        });
                    break;
            }
        }

        static Sprite At(Sprite[] sprites, int index) => sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;

        void RenderCodex(bool stagger)
        {
            ListEntries();
            int met = entries.FindAll(entry => entry.met).Count;
            string[] words = { ArenaText.Pair("武器", "Weapons"), ArenaText.Pair("技能", "Skills"), ArenaText.Pair("怪物", "Monsters") };
            for (int s = 0; s < 3; s++)
            {
                bool chosen = s == (int)shelf;
                shelfTabs[s].Chosen = chosen;
                shelfLabels[s].text = words[s];
                shelfLabels[s].color = chosen ? V5TitleKit.Chosen : V5TitleKit.Ink2;
                V5TitleKit.FitWidth(shelfLabels[s], 13, 9);
            }
            found.text = ArenaText.Pair($"已发现 <size=15><color=#ECE7DC>{met}</color></size> / <size=15><color=#ECE7DC>{entries.Count}</color></size>",
                $"Found <size=15><color=#ECE7DC>{met}</color></size> / <size=15><color=#ECE7DC>{entries.Count}</color></size>");
            int columns = shelf == Shelf.Skills ? 6 : 5, rowCount = Mathf.Max(1, (entries.Count + columns - 1) / columns);
            float cardW = (InnerW - (columns - 1) * CardGap) / columns, cardH = Mathf.Min(92f * Css, (ShelfGridH - (rowCount - 1) * CardGap) / rowCount);
            int chosenCard = shelfChosen[(int)shelf];
            if (chosenCard < 0 || chosenCard >= entries.Count || !entries[chosenCard].met) chosenCard = shelfChosen[(int)shelf] = entries.FindIndex(entry => entry.met);
            for (int c = 0; c < CardCount; c++)
            {
                var card = cards[c];
                bool shown = c < entries.Count && c < columns * Mathf.Max(1, Mathf.FloorToInt((ShelfGridH + CardGap) / (cardH + CardGap)));
                Show(card.plate, shown);
                if (!shown) continue;
                card.plate.Place(c % columns * (cardW + CardGap), c / columns * (cardH + CardGap), cardW, cardH);
                RenderCard(card, entries[c], c == chosenCard, cardW, cardH);
            }
            RenderDetail(chosenCard);
            ShowFoot(FootUse.None);
            if (!stagger) return;
            for (int c = 0; c < CardCount && c < entries.Count; c++)
            {
                var plate = cards[c].plate;
                Animate(plate, .24f, c * .018f, EaseIn, p =>
                {
                    plate.Offset = new Vector2(0f, -8f * Css * (1f - p));
                    plate.Alpha = p;
                });
            }
            Animate(detail, .24f, .12f, null, p => detail.Alpha = p);
        }

        void RenderCard(CardView card, CodexEntry entry, bool chosen, float width, float height)
        {
            card.plate.Chosen = chosen;
            card.plate.Dim = !entry.met;
            Show(card.unknown, !entry.met);
            if (!entry.met)
            {
                Show(card.picture, false);
                Show(card.name, false);
                card.unknown.text = ArenaText.Pair("？？？", "???");
                return;
            }
            Vector2 size = entry.shelf switch
            {
                Shelf.Weapons => new Vector2(88f, 42f) * Css,
                Shelf.Skills => new Vector2(50f, 50f) * Css,
                _ => new Vector2(58f, 58f) * Css,
            };
            float scale = Mathf.Min(1f, (height - 22f) / size.y, (width - 8f) / size.x);
            size *= scale;
            float top = (height - (size.y + 5f + 10f)) * .5f;
            bool picture = entry.picture || entry.block.HasValue;
            Show(card.picture, picture);
            card.picture.sprite = entry.picture;
            card.picture.color = entry.block ?? Color.white;
            V5TitleKit.Place(card.picture.rectTransform, (width - size.x) * .5f, top, size.x, size.y);
            Show(card.name, true);
            card.name.text = entry.name;
            V5TitleKit.Place(card.name.rectTransform, 2f, top + size.y + 5f, width - 4f, 10f);
            V5TitleKit.FitWidth(card.name, 10, 7);
        }

        void RenderDetail(int index)
        {
            bool on = Art.Enabled;
            V5TitleKit.Dress(detailPlate, on ? V5TitleKit.Piece("plate") : null, on ? Color.white : WhiteboxSkin.PlateFill, V5TitleKit.PlateDensity(12f));
            if (!on && !detailEdge)
            {
                detailEdge = detailPlate.gameObject.AddComponent<Outline>();
                detailEdge.useGraphicAlpha = false;
                detailEdge.effectColor = WhiteboxSkin.Edge;
                detailEdge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            if (detailEdge) detailEdge.enabled = !on;
            bool any = index >= 0 && index < entries.Count && entries[index].met;
            Show(detailPicture, any);
            Show(detailTitle, any);
            Show(detailBody, any);
            foreach (var (back, _) in tags) Show(back, false);
            if (!any) return;
            var entry = entries[index];
            var area = detail.Rect.sizeDelta;
            Vector2 size = entry.shelf switch
            {
                Shelf.Weapons => new Vector2(172f, 86f) * Css,
                Shelf.Skills => new Vector2(92f, 92f) * Css,
                _ => new Vector2(140f, 140f) * Css,
            };
            size *= Mathf.Min(1f, (area.y - 2f * DetailPad) / size.y);
            detailPicture.sprite = entry.picture;
            detailPicture.color = entry.block ?? Color.white;
            Show(detailPicture, entry.picture || entry.block.HasValue);
            V5TitleKit.Place(detailPicture.rectTransform, DetailPad + (DetailPicW - size.x) * .5f, (area.y - size.y) * .5f, size.x, size.y);
            float textLeft = DetailPad + DetailPicW + DetailPad, textW = InnerW - 15f - textLeft;
            // The tags are measured first and the name is fitted into the room they leave; they then follow its last letter, so a long
            // name (THE TYRANT KING) never runs under them. A tag that would leave the name less than TitleShare of the line is left out.
            var tagWords = Tags(entry);
            var widths = new List<float>();
            float tagsW = 0f;
            for (int t = 0; t < tags.Length && t < tagWords.Count; t++)
            {
                var (back, text) = tags[t];
                text.text = tagWords[t].words;
                text.color = tagWords[t].ink;
                back.color = tagWords[t].back;
                float w = text.preferredWidth + 10f, more = (widths.Count == 0 ? TagLead : TagGap) + w;
                if (tagsW + more > textW * (1f - TitleShare)) break;
                widths.Add(w);
                tagsW += more;
            }
            detailTitle.text = entry.name;
            V5TitleKit.Place(detailTitle.rectTransform, textLeft, DetailPad, textW - tagsW, 22f);
            V5TitleKit.FitWidth(detailTitle, 18, 12);
            float x = textLeft + detailTitle.preferredWidth + TagLead;
            for (int t = 0; t < widths.Count; t++)
            {
                if (x + widths[t] > textLeft + textW) break;
                Show(tags[t].back, true);
                V5TitleKit.Place(tags[t].back.rectTransform, x, DetailPad + 4.5f, widths[t], 13f);
                x += widths[t] + TagGap;
            }
            detailBody.text = Details(entry);
            V5TitleKit.Place(detailBody.rectTransform, textLeft, DetailPad + 22f + 9f, textW, area.y - DetailPad - 31f - 12f);
            ArenaTextFit.Refit(detailBody, 11, 8);
        }

        static List<(string words, Color back, Color ink)> Tags(CodexEntry entry)
        {
            var list = new List<(string, Color, Color)>();
            switch (entry.shelf)
            {
                case Shelf.Weapons:
                    var form = (WeaponForm)entry.id;
                    list.Add(RunWeapon.IsAutoForm(form) ? (ArenaText.Pair("自动", "Auto"), V5TitleKit.Rgb(201, 206, 216), TagInk) : (ArenaText.Pair("手持", "Hand"), V5TitleKit.Steel, TagInk));
                    var element = RunWeapons.BuiltInElement(form);
                    if (element != ElementKind.None)
                        list.Add((ArenaTerms.Element(element), element switch
                        {
                            ElementKind.Fire => V5TitleKit.Fire,
                            ElementKind.Frost => V5TitleKit.Frost,
                            ElementKind.Lightning => V5TitleKit.Bolt,
                            ElementKind.Poison => V5TitleKit.Venom,
                            _ => V5TitleKit.Steel,
                        }, element == ElementKind.Fire ? WarmInk : TagInk));
                    break;
                case Shelf.Skills:
                    list.Add((ArenaText.Pair("技能", "Skill"), V5TitleKit.Steel, TagInk));
                    break;
                default:
                    var kind = (EnemyKind)entry.id;
                    list.Add((ArenaCodex.Role(kind), EnemyRoles.IsBoss(kind) ? V5TitleKit.Crimson : EnemyRoles.IsSupport(kind) ? V5TitleKit.Teal
                        : EnemyRoles.IsSpecial(kind) ? V5TitleKit.OrangeHi : EnemyRoles.IsMelee(kind) ? V5TitleKit.Paint : V5TitleKit.Steel,
                        EnemyRoles.IsBoss(kind) ? Color.white : EnemyRoles.IsSpecial(kind) ? WarmInk : TagInk));
                    break;
            }
            return list;
        }

        static string Details(CodexEntry entry)
        {
            switch (entry.shelf)
            {
                case Shelf.Weapons:
                    var form = (WeaponForm)entry.id;
                    if (!RunWeapon.IsAnomalousForm(form)) return $"<b>{ArenaTerms.WeaponAnomaly(form, 0)}</b> {WeaponGuide.How(form, 0)}";
                    var revealed = ArenaCodex.RevealedAnomalies(form, out int missing);
                    var lines = new List<string>();
                    foreach (var (name, how) in revealed) lines.Add($"<b>{name}</b> {how}");
                    if (missing > 0) lines.Add($"<color=#7D8397>{ArenaCodex.MissingAnomalies(missing)}</color>");
                    return string.Join("\n", lines);
                case Shelf.Skills:
                    return entry.skill ? entry.skill.Description(1) : "";
                default:
                    return ArenaCodex.MonsterNote((EnemyKind)entry.id);
            }
        }

        void OnShelf(int index)
        {
            if (index == (int)shelf) return;
            shelf = (Shelf)index;
            V5UiSound.Play(V5UiSound.Ui.Tab);
            RenderCodex(true);
        }

        void OnCard(int index)
        {
            if (index >= entries.Count) return;
            var entry = entries[index];
            if (!entry.met)
            {
                V5UiSound.Play(V5UiSound.Ui.Denied);
                Shake(cards[index].plate);
                return;
            }
            if (shelfChosen[(int)shelf] == index) return;
            shelfChosen[(int)shelf] = index;
            V5UiSound.Play(V5UiSound.Ui.Press, 1f, .8f);
            for (int c = 0; c < CardCount; c++) cards[c].plate.Chosen = c == index;
            RenderDetail(index);
            Animate(detail, .22f, 0f, V5TitleKit.EaseOut, p =>
            {
                detail.Alpha = Mathf.Lerp(.2f, 1f, p);
                detail.Offset = new Vector2(10f * Css * (1f - p), 0f);
            });
        }
    }
}
