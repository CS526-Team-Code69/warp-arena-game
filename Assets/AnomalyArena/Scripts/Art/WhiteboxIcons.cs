using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The interface's pictures in whitebox. Every icon here is drawn at run
    /// time into a small texture from flat shapes and colours (no image file), made on first use and kept. One shape per kind, so the kind
    /// reads at a glance, and the colour tells the members of a kind apart:
    /// - weapons: the side view of the weapon's own whitebox look (the table's parts and colours, Whitebox.PartsOf), muzzle to the right;
    ///   a hand weapon is drawn as if at least HandMinLength long, so the pistol stays short beside the rifle. The three auto weapons are
    ///   drawn as they move: three blades round a hub, the drone over its ring, eight blocks in a ring;
    /// - elements: a circle in the element's colour (ElementCombat), light hollow, void black inside a violet rim;
    /// - skills: a hexagon, in its element's colour for the element skills, hollow for the mechanic skills, black in a violet rim with a
    ///   small violet mark for the three greater void skills;
    /// - body stats and the armor: a square; upgrade directions and the Mk: an upward triangle; rooms: a house in the room's colour;
    /// - currencies: the side view of what is picked up for them (the energy shard, the alloy ore, the dark matter node), so the corner of
    ///   the screen shows the same thing as the ground. (Their table colours, energy and alloy, are too close to tell apart on one shape.)
    ///   Their dark parts are lifted and a pale ring runs round each, so all three read on the HUD's near-black panel (second round).
    /// Rarity stays on the frames round the icons. V2Hud's SetElementIcon / SetContentIcon / SetWeaponIcon take these while Art.Enabled
    /// is off, so every slot, choice and crate follows the switch at its next refresh; the study window maps its tiles with ForStudy.
    /// </summary>
    public static class WhiteboxIcons
    {
        /// <summary>The kinds of thing an interface icon can show.</summary>
        public enum Kind { Weapon, Element, Skill, Stat, Axis, Room, Armor, Currency }

        /// <summary>The flat shapes the kinds are drawn with.</summary>
        public enum Glyph { Circle, Ring, Hexagon, Square, Triangle, Diamond, House }

        /// <summary>The three currencies of the HUD's top-right corner, in its order.</summary>
        public enum Currency { Energy, Alloy, Dark }

        public const int WeaponWidth = 192, WeaponHeight = 96, GlyphSize = 96, LookSize = 96;
        /// <summary>A hand weapon is drawn as if it were at least this long (metres): the pistol keeps its short look beside the rifle.</summary>
        public const float HandMinLength = .9f;
        // Whitebox.Meshes keeps its own copy private: half the width of an obelisk's shaft where its cap starts, at 80 % of the height.
        const float ObeliskShaftTop = .28f;
        // Upgrade directions in the art set's axisIcons order.
        const int AxisPower = 1, AxisCount = 6;
        static readonly Color Hollow = new(.06f, .08f, .12f), VoidRim = new(.78f, .54f, 1f);

        static readonly Sprite[] weapons = new Sprite[32], elements = new Sprite[8], skills = new Sprite[32], stats = new Sprite[32],
            axes = new Sprite[AxisCount], rooms = new Sprite[4], currencies = new Sprite[3], marks = new Sprite[8];
        static Sprite armor, unknown;
        static readonly Dictionary<string, Sprite> looks = new();

        // ───── The rules (no textures) ─────

        /// <summary>The shape each kind is drawn with. Weapons and currencies are drawn from their whitebox looks instead.</summary>
        public static Glyph GlyphOf(Kind kind) => kind switch
        {
            Kind.Element => Glyph.Circle,
            Kind.Skill => Glyph.Hexagon,
            Kind.Stat or Kind.Armor => Glyph.Square,
            Kind.Axis => Glyph.Triangle,
            Kind.Room => Glyph.House,
            _ => Glyph.Diamond
        };

        /// <summary>An element's colour by the art set's elementIcons index (0 fire … 6 void).</summary>
        public static Color ElementColour(int index) => ElementCombat.ColorFor((ElementKind)(index + 1));

        /// <summary>A skill's colour: its element's for the seven element skills; its own for the mechanic and the greater void skills.</summary>
        public static Color SkillColour(SkillId id)
        {
            if (id <= SkillId.Devour) return ElementColour((int)id); // FlameWake … Devour are Fire … Void, in the same order
            return id switch
            {
                SkillId.Blink => Whitebox.Colour("energy"),
                SkillId.Shockwave => Whitebox.Colour("white"),
                SkillId.GravityWell => Whitebox.Colour("prophet"),
                SkillId.TimeSlip => Whitebox.Colour("hp"),
                SkillId.Decoy => Whitebox.Colour("bloater"),
                SkillId.Overcharge => Whitebox.Colour("amber"),
                _ => Whitebox.Colour("core")
            };
        }

        /// <summary>The mechanic skills are drawn hollow, the greater void skills black in a violet rim, the rest filled.</summary>
        public static bool SkillIsMechanic(SkillId id) => id is >= SkillId.Blink and <= SkillId.Overcharge;
        public static bool SkillIsGreaterVoid(SkillId id) => id >= SkillId.PocketVoid;

        /// <summary>A body stat's colour. The eight stats the study window sells (max HP, move speed, damage, attack speed, protection,
        /// max energy, penetration, crit) differ in hue.</summary>
        public static Color StatColour(StatId stat) => stat switch
        {
            StatId.MaxHp => Whitebox.Colour("hp"),
            StatId.Regeneration => new Color(.6f, .95f, .72f),
            StatId.MoveSpeed => Whitebox.Colour("skitter"),
            StatId.Stamina => Whitebox.Colour("rammer"),
            StatId.Damage => Whitebox.Colour("grunt"),
            StatId.AttackSpeed => Whitebox.Colour("brute"),
            StatId.Knockback => Whitebox.Colour("stomper"),
            StatId.Impact => Whitebox.Colour("olive"),
            StatId.PickupRange => Whitebox.Colour("warden"),
            StatId.Protection => Whitebox.Colour("alloy"),
            StatId.Luck => Whitebox.Colour("amber"),
            StatId.MaxEnergy => Whitebox.Colour("energy"),
            StatId.EnergySiphon => Whitebox.Colour("chanter"),
            StatId.ElementPenetration => Whitebox.Colour("shade"),
            _ => Whitebox.Colour("white")
        };

        /// <summary>An upgrade direction's colour by the art set's axisIcons index (Infusion, Power, Tempo, Reach, Twist, Tame).</summary>
        public static Color AxisColour(int index) => index switch
        {
            0 => Whitebox.Colour("shade"),
            AxisPower => Whitebox.Colour("grunt"),
            2 => Whitebox.Colour("skitter"),
            3 => Whitebox.Colour("energy"),
            4 => Whitebox.Colour("chanter"),
            _ => Whitebox.Colour("hp")
        };

        /// <summary>The whitebox look a currency's icon is the side view of.</summary>
        public static string CurrencyLook(Currency currency) => currency switch
        {
            Currency.Energy => "energy",
            Currency.Alloy => "ore",
            _ => "dark"
        };

        /// <summary>
        /// What a tile of the study window shows, from its id (V2Campaign.Study.cs and V2Campaign.Void.cs name them): "mk" and "void.mk"
        /// the Power direction; "armor" and "void.armor" the armor; "weapon.F", "tame.F" and "twist.F" the weapon form F; "tier.L" the
        /// Energy Lab; "stat.S.R" the stat S; "skill.K" and "void.skill.K" the skill K; "infuse.F.E" the element E; "void.enchant.N" the
        /// void element; "overload.shield.*" Central Control and "overload.damage.*" the Power direction. Index is the kind's own number
        /// (the art set's order for elements and directions, the enum's for the rest). False for an id it does not know.
        /// </summary>
        public static bool StudyKind(string id, out Kind kind, out int index)
        {
            kind = Kind.Stat; index = -1;
            if (string.IsNullOrEmpty(id)) return false;
            var parts = id.Split('.');
            string head = parts[0], second = parts.Length > 1 ? parts[1] : "", third = parts.Length > 2 ? parts[2] : "";
            if (head == "void")
            {
                switch (second)
                {
                    case "skill": return Parsed(third, Kind.Skill, out kind, out index, out SkillId _);
                    case "mk": kind = Kind.Axis; index = AxisPower; return true;
                    case "armor": kind = Kind.Armor; index = 0; return true;
                    case "enchant": kind = Kind.Element; index = (int)ElementKind.Void - 1; return true;
                    default: return false;
                }
            }
            switch (head)
            {
                case "mk": kind = Kind.Axis; index = AxisPower; return true;
                case "armor": kind = Kind.Armor; index = 0; return true;
                case "weapon": case "tame": case "twist": return Parsed(second, Kind.Weapon, out kind, out index, out WeaponForm _);
                case "tier": kind = Kind.Room; index = (int)CampaignRoom.Energy; return true;
                case "stat": return Parsed(second, Kind.Stat, out kind, out index, out StatId _);
                case "skill": return Parsed(second, Kind.Skill, out kind, out index, out SkillId _);
                case "infuse":
                    if (!Enum.TryParse(third, out ElementKind element) || element == ElementKind.None) return false;
                    kind = Kind.Element; index = (int)element - 1; return true;
                case "overload":
                    if (second == "shield") { kind = Kind.Room; index = (int)CampaignRoom.Control; }
                    else { kind = Kind.Axis; index = AxisPower; }
                    return true;
                default: return false;
            }
        }

        static bool Parsed<T>(string name, Kind as_, out Kind kind, out int index, out T value) where T : struct, Enum
        {
            kind = as_; index = -1;
            if (!Enum.TryParse(name, out value) || !Enum.IsDefined(typeof(T), value)) return false;
            index = Convert.ToInt32(value);
            return true;
        }

        // ───── Sprites ─────

        /// <summary>A weapon form's icon (WeaponForm order), 2:1, muzzle to the right; null for a number out of range.</summary>
        public static Sprite Weapon(int form)
        {
            if (form < 0 || form >= weapons.Length || !Enum.IsDefined(typeof(WeaponForm), form)) return null;
            if (weapons[form]) return weapons[form];
            var pixels = WeaponPixels(form, out int w, out int h);
            return weapons[form] = Make("Whitebox weapon " + (WeaponForm)form, w, h, pixels);
        }

        /// <summary>An element's icon by the art set's elementIcons index (0 fire … 6 void); null out of range.</summary>
        public static Sprite Element(int index)
        {
            if (index < 0 || index >= 7) return null;
            if (elements[index]) return elements[index];
            var shapes = new List<Shape>();
            var colour = ElementColour(index);
            if (index == (int)ElementKind.Void - 1)
            {
                shapes.Add(Shape.Ellipse(Vector2.zero, Vector2.one * .86f, VoidRim));
                shapes.Add(Shape.Ellipse(Vector2.zero, Vector2.one * .62f, Whitebox.Colour("black")));
            }
            else if (index == (int)ElementKind.Light - 1) shapes.Add(Shape.Ellipse(Vector2.zero, Vector2.one * .86f, colour, .5f));
            else shapes.Add(Shape.Ellipse(Vector2.zero, Vector2.one * .86f, colour));
            return elements[index] = Make("Whitebox element " + (index + 1), GlyphSize, GlyphSize, Paint(GlyphSize, GlyphSize, shapes, Unit, 0f));
        }

        /// <summary>A skill's icon (SkillId order); null out of range.</summary>
        public static Sprite Skill(int skill)
        {
            if (skill < 0 || skill >= skills.Length || !Enum.IsDefined(typeof(SkillId), skill)) return null;
            if (skills[skill]) return skills[skill];
            var id = (SkillId)skill;
            var shapes = new List<Shape>();
            var colour = SkillColour(id);
            if (SkillIsGreaterVoid(id))
            {
                shapes.Add(GlyphShape(Glyph.Hexagon, VoidRim));
                shapes.Add(GlyphShape(Glyph.Hexagon, Whitebox.Colour("black"), .74f));
                var mark = id switch { SkillId.PocketVoid => Glyph.Circle, SkillId.VoidWalk => Glyph.Triangle, _ => Glyph.Diamond };
                shapes.Add(GlyphShape(mark, colour, .38f));
            }
            else
            {
                shapes.Add(GlyphShape(Glyph.Hexagon, colour));
                if (SkillIsMechanic(id)) shapes.Add(GlyphShape(Glyph.Hexagon, Hollow, .5f));
            }
            return skills[skill] = Make("Whitebox skill " + id, GlyphSize, GlyphSize, Paint(GlyphSize, GlyphSize, shapes, Unit, 0f));
        }

        /// <summary>A body stat's icon (StatId order); null out of range.</summary>
        public static Sprite Stat(int stat)
        {
            if (stat < 0 || stat >= stats.Length || !Enum.IsDefined(typeof(StatId), stat)) return null;
            if (stats[stat]) return stats[stat];
            var shapes = new List<Shape> { GlyphShape(Glyph.Square, StatColour((StatId)stat)) };
            return stats[stat] = Make("Whitebox stat " + (StatId)stat, GlyphSize, GlyphSize, Paint(GlyphSize, GlyphSize, shapes, Unit, 0f));
        }

        /// <summary>An upgrade direction's icon by the art set's axisIcons index (Infusion, Power, Tempo, Reach, Twist, Tame); null out of range.</summary>
        public static Sprite Axis(int index)
        {
            if (index < 0 || index >= axes.Length) return null;
            if (axes[index]) return axes[index];
            var shapes = new List<Shape> { GlyphShape(Glyph.Triangle, AxisColour(index)) };
            return axes[index] = Make("Whitebox direction " + index, GlyphSize, GlyphSize, Paint(GlyphSize, GlyphSize, shapes, Unit, 0f));
        }

        /// <summary>A base room's icon: a house in the room's colour with a dark door.</summary>
        public static Sprite Room(CampaignRoom room)
        {
            int i = (int)room;
            if (i < 0 || i >= rooms.Length) return null;
            if (rooms[i]) return rooms[i];
            var shapes = new List<Shape>
            {
                GlyphShape(Glyph.House, V2Campaign.RoomColour(room)),
                Shape.Polygon(Corners(new Vector2(-.17f, -.82f), new Vector2(.17f, -.3f)), Hollow),
            };
            return rooms[i] = Make("Whitebox room " + room, GlyphSize, GlyphSize, Paint(GlyphSize, GlyphSize, shapes, Unit, 0f));
        }

        /// <summary>The player's armor: a square in the player's colour with a dark visor slit.</summary>
        public static Sprite Armor()
        {
            if (armor) return armor;
            var shapes = new List<Shape>
            {
                GlyphShape(Glyph.Square, Whitebox.Colour("player")),
                Shape.Polygon(Corners(new Vector2(-.52f, .1f), new Vector2(.52f, .32f)), Hollow),
            };
            return armor = Make("Whitebox armor", GlyphSize, GlyphSize, Paint(GlyphSize, GlyphSize, shapes, Unit, 0f));
        }

        /// <summary>A plain grey square, for a picture whose kind is not known here.</summary>
        public static Sprite Unknown()
        {
            if (unknown) return unknown;
            var shapes = new List<Shape> { GlyphShape(Glyph.Square, Whitebox.Colour("greyDark")) };
            return unknown = Make("Whitebox unknown", GlyphSize, GlyphSize, Paint(GlyphSize, GlyphSize, shapes, Unit, 0f));
        }

        /// <summary>A currency's icon: the side view of its whitebox look, lit up for the dark panels it sits on (CurrencyPixels).</summary>
        public static Sprite CurrencyIcon(Currency currency)
        {
            int i = (int)currency;
            if (i < 0 || i >= currencies.Length) return null;
            return currencies[i] ? currencies[i] : currencies[i] = Make("Whitebox currency " + currency, LookSize, LookSize, CurrencyPixels(currency));
        }

        // The currencies sit on the HUD's near-black panel (2026-11-10, second round: the dark matter's black rock and violet crystals were
        // hard to see there): every part is lifted to at least this brightness, and a pale ring runs round the whole shape.
        public const float CurrencyMinLuminance = .34f;
        const int CurrencyHaloWidth = 5;
        static readonly Color CurrencyHalo = new(.86f, .92f, 1f, .9f);

        /// <summary>The pixels of a currency's icon (LookSize square, bottom row first). Pure: no texture is made.</summary>
        public static Color32[] CurrencyPixels(Currency currency)
        {
            var shapes = SideView(Whitebox.PartsOf(CurrencyLook(currency)), null);
            for (int i = 0; i < shapes.Count; i++)
            {
                var s = shapes[i];
                float luminance = Luminance(s.fill);
                if (luminance >= CurrencyMinLuminance) continue;
                var lifted = Color.Lerp(s.fill, Color.white, (CurrencyMinLuminance - luminance) / Mathf.Max(1e-4f, 1f - luminance));
                lifted.a = s.fill.a;
                s.fill = lifted;
                s.edge = Rim(lifted);
                shapes[i] = s;
            }
            var pixels = Paint(LookSize, LookSize, shapes, null, 0f);
            Halo(pixels, LookSize, LookSize, CurrencyHalo, CurrencyHaloWidth);
            return pixels;
        }

        /// <summary>Relative luminance (Rec. 709 weights) of a colour.</summary>
        public static float Luminance(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;

        /// <summary>A ring of `colour` up to `width` pixels round everything drawn, under it (the shape's own pixels stay in front).</summary>
        static void Halo(Color32[] pixels, int width, int height, Color colour, int ring)
        {
            var alpha = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) alpha[i] = pixels[i].a;
            int reach = ring * ring;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (alpha[i] == 255) continue;
                    int near = 0;
                    for (int dy = -ring; dy <= ring && near < 255; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= height) continue;
                        for (int dx = -ring; dx <= ring; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= width || dx * dx + dy * dy > reach) continue;
                            near = Mathf.Max(near, alpha[yy * width + xx]);
                        }
                    }
                    if (near == 0) continue;
                    float under = colour.a * near / 255f, a = alpha[i] / 255f;
                    float outA = a + under * (1f - a);
                    if (outA <= 0f) continue;
                    Color top = pixels[i];
                    float keep = under * (1f - a);
                    pixels[i] = new Color((top.r * a + colour.r * keep) / outA, (top.g * a + colour.g * keep) / outA, (top.b * a + colour.b * keep) / outA, outA);
                }
        }

        /// <summary>The side view of a whitebox look (any id of the table), square, fitted to its parts. A crate leaves out its lock: the
        /// crates open straight away since 2026-11-10 (work/whitebox-1110/接入清单.md, section 3).</summary>
        public static Sprite Look(string lookId)
        {
            if (string.IsNullOrEmpty(lookId)) return null;
            if (looks.TryGetValue(lookId, out var made) && made) return made;
            var parts = Whitebox.PartsOf(lookId);
            if (parts.Count == 0) return null;
            var pixels = Paint(LookSize, LookSize, SideView(parts, "lock"), null, 0f);
            return looks[lookId] = Make("Whitebox look " + lookId, LookSize, LookSize, pixels);
        }

        /// <summary>A white shape for tinting (the tutorial's diagrams draw the whitebox's crystals and shards with these).</summary>
        public static Sprite Mark(Glyph glyph)
        {
            int i = (int)glyph;
            if (marks[i]) return marks[i];
            var shapes = new List<Shape> { glyph == Glyph.Ring ? Shape.Ellipse(Vector2.zero, Vector2.one * .86f, Color.white, .55f) : GlyphShape(glyph, Color.white) };
            return marks[i] = Make("Whitebox mark " + glyph, GlyphSize, GlyphSize, Paint(GlyphSize, GlyphSize, shapes, Unit, 0f));
        }

        /// <summary>The icon of one kind and number (as StudyKind gives them); null when there is none.</summary>
        public static Sprite Of(Kind kind, int index) => kind switch
        {
            Kind.Weapon => Weapon(index),
            Kind.Element => Element(index),
            Kind.Skill => Skill(index),
            Kind.Stat => Stat(index),
            Kind.Axis => Axis(index),
            Kind.Room => index >= 0 && index < rooms.Length ? Room((CampaignRoom)index) : null,
            Kind.Armor => Armor(),
            Kind.Currency => index >= 0 && index < currencies.Length ? CurrencyIcon((Currency)index) : null,
            _ => null
        };

        /// <summary>A study tile's whitebox icon from its id; null for an id StudyKind does not know.</summary>
        public static Sprite ForStudy(string id) => StudyKind(id, out var kind, out int index) ? Of(kind, index) : null;

        // ───── Weapons ─────

        /// <summary>The pixels of a weapon form's icon (WeaponWidth × WeaponHeight, bottom row first). Pure: no texture is made.</summary>
        public static Color32[] WeaponPixels(int form, out int width, out int height)
        {
            width = WeaponWidth; height = WeaponHeight;
            var f = (WeaponForm)form;
            switch (f)
            {
                case WeaponForm.OrbitBlades: return Paint(width, height, OrbitShapes(), Unit, 0f);
                case WeaponForm.ShieldRing: return Paint(width, height, RingShapes(), Unit, 0f);
            }
            string id = V2WeaponPresentation.LookIdFor(f);
            IReadOnlyList<Whitebox.Part> parts = id != null ? Whitebox.PartsOf(id) : null;
            if (parts == null || parts.Count == 0) parts = new[] { new Whitebox.Part(Whitebox.Prim.Box, .2f, .2f, 1f, 0f, 0f, .5f, "grey", 1f) };
            return Paint(width, height, SideView(parts, null), null, RunWeapon.IsAutoForm(f) ? 0f : HandMinLength);
        }

        /// <summary>The orbit blades from above: three ice blades round a white hub.</summary>
        static List<Shape> OrbitShapes()
        {
            var ice = Whitebox.Colour("ice");
            var shapes = new List<Shape>();
            for (int k = 0; k < 3; k++)
            {
                float a = (90f + k * 120f) * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var side = new Vector2(-along.y, along.x);
                shapes.Add(Shape.Polygon(new[] { along * .24f + side * .2f, along * .96f + side * .06f, along * .24f - side * .08f }, ice));
            }
            shapes.Add(Shape.Ellipse(Vector2.zero, Vector2.one * .22f, Whitebox.Colour("white")));
            return shapes;
        }

        /// <summary>The energy ring from above: eight energy blocks on a faint ring.</summary>
        static List<Shape> RingShapes()
        {
            var energy = Whitebox.Colour("energy");
            var faint = energy; faint.a = .35f;
            var shapes = new List<Shape> { Shape.Ellipse(Vector2.zero, Vector2.one * .76f, faint, .86f) };
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                var at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .72f;
                shapes.Add(Shape.Polygon(Corners(at - Vector2.one * .13f, at + Vector2.one * .13f), energy));
            }
            return shapes;
        }

        // ───── Side views of looks ─────

        /// <summary>The look's parts seen from its right side: forward (z) to the right, up (y) up, in table order (later parts in front).
        /// Parts in the colour `skip` are left out.</summary>
        static List<Shape> SideView(IReadOnlyList<Whitebox.Part> parts, string skip)
        {
            var shapes = new List<Shape>();
            foreach (var p in parts)
            {
                if (skip != null && p.colour == skip) continue;
                var colour = p.TableColour;
                Vector3 s = p.size, c = p.position;
                float z0 = c.z - s.z * .5f, z1 = c.z + s.z * .5f, y0 = c.y - s.y * .5f, y1 = c.y + s.y * .5f;
                var middle = new Vector2(c.z, c.y);
                var half = new Vector2(s.z * .5f, s.y * .5f);
                switch (p.prim)
                {
                    case Whitebox.Prim.Wedge: // high at the back, sloping down to the front
                        shapes.Add(Shape.Polygon(new[] { new Vector2(z0, y0), new Vector2(z1, y0), new Vector2(z0, y1) }, colour));
                        break;
                    case Whitebox.Prim.Pyramid:
                        shapes.Add(Shape.Polygon(new[] { new Vector2(z0, y0), new Vector2(z1, y0), new Vector2(c.z, y1) }, colour));
                        break;
                    case Whitebox.Prim.PyramidZ:
                        shapes.Add(Shape.Polygon(new[] { new Vector2(z0, y0), new Vector2(z1, c.y), new Vector2(z0, y1) }, colour));
                        break;
                    case Whitebox.Prim.Octa:
                        shapes.Add(Shape.Polygon(new[] { new Vector2(c.z, y0), new Vector2(z1, c.y), new Vector2(c.z, y1), new Vector2(z0, c.y) }, colour));
                        break;
                    case Whitebox.Prim.Icosphere:
                        shapes.Add(Shape.Ellipse(middle, half, colour));
                        break;
                    case Whitebox.Prim.Obelisk:
                        float neck = y0 + s.y * .8f;
                        shapes.Add(Shape.Polygon(new[] { new Vector2(z0, y0), new Vector2(z1, y0), new Vector2(c.z + ObeliskShaftTop * s.z, neck),
                            new Vector2(c.z, y1), new Vector2(c.z - ObeliskShaftTop * s.z, neck) }, colour));
                        break;
                    case Whitebox.Prim.Flare: // twice as wide at the front as at the back
                        shapes.Add(Shape.Polygon(new[] { new Vector2(z0, c.y - s.y * .25f), new Vector2(z1, y0), new Vector2(z1, y1), new Vector2(z0, c.y + s.y * .25f) }, colour));
                        break;
                    case Whitebox.Prim.Ring:
                        int thin = Thinnest(s);
                        if (thin == 0) shapes.Add(Shape.Ellipse(middle, half, colour, .62f));
                        else if (thin == 1) shapes.Add(Shape.Ellipse(middle, new Vector2(half.x, Mathf.Max(half.y, s.z * .16f)), colour, .62f)); // lying flat: a little from above
                        else shapes.Add(Shape.Polygon(Corners(new Vector2(z0, y0), new Vector2(z1, y1)), colour)); // seen edge on
                        break;
                    case Whitebox.Prim.Disc:
                    case Whitebox.Prim.Prism6:
                    case Whitebox.Prim.Prism8:
                        int odd = OddAxis(s);
                        int sides = p.prim == Whitebox.Prim.Prism6 ? 6 : p.prim == Whitebox.Prim.Prism8 ? 8 : 24;
                        if (odd == 0) shapes.Add(Shape.Polygon(Regular(sides, middle, half, 0f), colour)); // its end faces the side
                        else if (p.prim == Whitebox.Prim.Disc && odd == 1) shapes.Add(Shape.Ellipse(middle, new Vector2(half.x, Mathf.Max(half.y, s.z * .16f)), colour));
                        else
                        {
                            shapes.Add(Shape.Polygon(Corners(new Vector2(z0, y0), new Vector2(z1, y1)), colour));
                            // A lighter band down the middle face, so a prism reads as faceted.
                            var band = Shade(colour, 1.18f);
                            shapes.Add(odd == 2 ? Shape.Polygon(Corners(new Vector2(z0, c.y - s.y * .17f), new Vector2(z1, c.y + s.y * .17f)), band, band)
                                : Shape.Polygon(Corners(new Vector2(c.z - s.z * .17f, y0), new Vector2(c.z + s.z * .17f, y1)), band, band));
                        }
                        break;
                    default:
                        shapes.Add(Shape.Polygon(Corners(new Vector2(z0, y0), new Vector2(z1, y1)), colour));
                        break;
                }
            }
            return shapes;
        }

        /// <summary>The axis (0 x, 1 y, 2 z) whose size differs from the other two, which are equal: a prism's or a disc's axis.</summary>
        static int OddAxis(Vector3 s)
        {
            if (Mathf.Abs(s.x - s.z) < 1e-3f) return 1;
            if (Mathf.Abs(s.x - s.y) < 1e-3f) return 2;
            if (Mathf.Abs(s.y - s.z) < 1e-3f) return 0;
            return 1;
        }

        static int Thinnest(Vector3 s) => s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;

        // ───── Painting ─────

        static readonly Rect Unit = new(-1f, -1f, 2f, 2f);

        /// <summary>A convex polygon (any winding) or an ellipse, with an inner hole as a share of its radii (a ring), its fill and its rim.</summary>
        struct Shape
        {
            public Vector2[] points;
            public Vector2 centre, radii;
            public float hole;
            public Color fill, edge;

            public static Shape Polygon(Vector2[] points, Color fill) => Polygon(points, fill, Rim(fill));

            public static Shape Polygon(Vector2[] points, Color fill, Color edge)
            {
                float area = 0f;
                for (int i = 0; i < points.Length; i++) { var a = points[i]; var b = points[(i + 1) % points.Length]; area += a.x * b.y - b.x * a.y; }
                if (area < 0f) Array.Reverse(points); // counter-clockwise, so "inside" is the left of every edge
                return new Shape { points = points, fill = fill, edge = edge };
            }

            public static Shape Ellipse(Vector2 centre, Vector2 radii, Color fill, float hole = 0f) =>
                new() { centre = centre, radii = radii, hole = hole, fill = fill, edge = Rim(fill) };
        }

        static Color Rim(Color fill) => new(fill.r * .45f, fill.g * .45f, fill.b * .45f, fill.a);

        static Color Shade(Color c, float k) => new(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

        static Shape GlyphShape(Glyph glyph, Color colour, float scale = 1f)
        {
            if (glyph == Glyph.Circle) return Shape.Ellipse(Vector2.zero, Vector2.one * .86f * scale, colour);
            if (glyph == Glyph.Ring) return Shape.Ellipse(Vector2.zero, Vector2.one * .86f * scale, colour, .55f);
            var points = glyph switch
            {
                Glyph.Square => Corners(new Vector2(-.8f, -.8f), new Vector2(.8f, .8f)),
                Glyph.Triangle => new[] { new Vector2(-.9f, -.74f), new Vector2(.9f, -.74f), new Vector2(0f, .9f) },
                Glyph.Diamond => new[] { new Vector2(0f, -.94f), new Vector2(.7f, 0f), new Vector2(0f, .94f), new Vector2(-.7f, 0f) },
                Glyph.House => new[] { new Vector2(-.78f, -.82f), new Vector2(.78f, -.82f), new Vector2(.78f, .14f), new Vector2(0f, .88f), new Vector2(-.78f, .14f) },
                _ => Regular(6, Vector2.zero, Vector2.one * .92f, 0f)
            };
            for (int i = 0; i < points.Length; i++) points[i] *= scale;
            return Shape.Polygon(points, colour);
        }

        static Vector2[] Corners(Vector2 min, Vector2 max) => new[] { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) };

        static Vector2[] Regular(int sides, Vector2 centre, Vector2 radii, float startDegrees)
        {
            var points = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (startDegrees + i * 360f / sides) * Mathf.Deg2Rad;
                points[i] = centre + new Vector2(Mathf.Cos(a) * radii.x, Mathf.Sin(a) * radii.y);
            }
            return points;
        }

        /// <summary>
        /// Paints the shapes in order (later ones in front) into a width × height picture, bottom row first. The frame is the area of the
        /// shapes' own space that fills the picture (keeping its proportions); without one, the box round every shape, at least minSpanX
        /// wide, with a margin. Each shape has a darker rim and is a little lighter at the top than at the foot, so the flat shapes read as
        /// lit solids; four samples a pixel smooth the edges.
        /// </summary>
        static Color32[] Paint(int width, int height, List<Shape> shapes, Rect? frame, float minSpanX)
        {
            Rect box = frame ?? Bounds(shapes);
            float spanX = Mathf.Max(box.width, minSpanX, 1e-4f), spanY = Mathf.Max(box.height, 1e-4f);
            float margin = Mathf.Min(width, height) * (frame.HasValue ? .03f : .1f);
            float scale = Mathf.Min((width - 2f * margin) / spanX, (height - 2f * margin) / spanY);
            Vector2 from = box.center, to = new(width * .5f, height * .5f);
            float rimWidth = Mathf.Max(1.2f, Mathf.Min(width, height) / 44f);
            var colours = new Color[width * height];
            foreach (var source in shapes)
            {
                var s = Placed(source, from, scale, to);
                var r = Extent(s);
                float rim = Mathf.Min(rimWidth, Mathf.Min(r.width, r.height) * .18f);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(r.xMin)), x1 = Mathf.Min(width - 1, Mathf.CeilToInt(r.xMax));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(r.yMin)), y1 = Mathf.Min(height - 1, Mathf.CeilToInt(r.yMax));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int hits = 0;
                        for (int k = 0; k < 4; k++)
                            if (Inside(s, new Vector2(x + .25f + (k & 1) * .5f, y + .25f + (k >> 1) * .5f))) hits++;
                        if (hits == 0) continue;
                        var centre = new Vector2(x + .5f, y + .5f);
                        float up = r.height > 0f ? Mathf.Clamp01((centre.y - r.yMin) / r.height) : .5f;
                        Color colour = Depth(s, centre) < rim ? s.edge : Shade(s.fill, Mathf.Lerp(.8f, 1.12f, up));
                        float a = hits * .25f * s.fill.a;
                        int i = y * width + x;
                        var under = colours[i];
                        float outA = a + under.a * (1f - a);
                        if (outA <= 0f) continue;
                        float keep = under.a * (1f - a);
                        colours[i] = new Color((colour.r * a + under.r * keep) / outA, (colour.g * a + under.g * keep) / outA, (colour.b * a + under.b * keep) / outA, outA);
                    }
            }
            var pixels = new Color32[colours.Length];
            for (int i = 0; i < colours.Length; i++) pixels[i] = colours[i];
            return pixels;
        }

        static Shape Placed(Shape s, Vector2 from, float scale, Vector2 to)
        {
            var placed = s;
            if (s.points != null)
            {
                placed.points = new Vector2[s.points.Length];
                for (int i = 0; i < s.points.Length; i++) placed.points[i] = (s.points[i] - from) * scale + to;
            }
            else
            {
                placed.centre = (s.centre - from) * scale + to;
                placed.radii = s.radii * scale;
            }
            return placed;
        }

        static Rect Bounds(List<Shape> shapes)
        {
            bool any = false;
            Rect all = default;
            foreach (var s in shapes)
            {
                var r = Extent(s);
                all = any ? Rect.MinMaxRect(Mathf.Min(all.xMin, r.xMin), Mathf.Min(all.yMin, r.yMin), Mathf.Max(all.xMax, r.xMax), Mathf.Max(all.yMax, r.yMax)) : r;
                any = true;
            }
            return any ? all : Unit;
        }

        static Rect Extent(Shape s)
        {
            if (s.points == null) return Rect.MinMaxRect(s.centre.x - s.radii.x, s.centre.y - s.radii.y, s.centre.x + s.radii.x, s.centre.y + s.radii.y);
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (var p in s.points) { xMin = Mathf.Min(xMin, p.x); yMin = Mathf.Min(yMin, p.y); xMax = Mathf.Max(xMax, p.x); yMax = Mathf.Max(yMax, p.y); }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static bool Inside(in Shape s, Vector2 p)
        {
            if (s.points == null)
            {
                float q = Norm(s, p);
                return q <= 1f && q >= s.hole;
            }
            for (int i = 0; i < s.points.Length; i++)
            {
                var a = s.points[i]; var b = s.points[(i + 1) % s.points.Length];
                if ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) < 0f) return false;
            }
            return true;
        }

        /// <summary>How far in from the nearest edge a point is, in pixels (an ellipse's is near enough for a rim).</summary>
        static float Depth(in Shape s, Vector2 p)
        {
            if (s.points == null)
            {
                float q = Norm(s, p), r = Mathf.Min(s.radii.x, s.radii.y);
                float depth = (1f - q) * r;
                return s.hole > 0f ? Mathf.Min(depth, (q - s.hole) * r) : depth;
            }
            float nearest = float.MaxValue;
            for (int i = 0; i < s.points.Length; i++)
            {
                var a = s.points[i]; var b = s.points[(i + 1) % s.points.Length];
                float length = Vector2.Distance(a, b);
                if (length < 1e-4f) continue;
                nearest = Mathf.Min(nearest, ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x)) / length);
            }
            return nearest;
        }

        static float Norm(in Shape s, Vector2 p)
        {
            float dx = (p.x - s.centre.x) / Mathf.Max(1e-4f, s.radii.x), dy = (p.y - s.centre.y) / Mathf.Max(1e-4f, s.radii.y);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static Sprite Make(string name, int width, int height, Color32[] pixels)
        {
            // With smaller copies (mipmaps): the icons are drawn at 96 pixels and shown at about a third of that in the HUD, where thin
            // rims and rings would break up without them.
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            var sprite = Sprite.Create(texture, new UnityEngine.Rect(0f, 0f, width, height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
