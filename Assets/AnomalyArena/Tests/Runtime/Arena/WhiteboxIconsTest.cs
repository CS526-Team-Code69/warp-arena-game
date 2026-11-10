using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The interface's whitebox pictures (2026-11-10): every weapon in use draws an icon of its own; with the art off the icon
    /// setters give the whitebox's shapes and never the art set's pictures, with it on the pictures again; the study window's tiles
    /// find their kind from their ids; the currencies read on the HUD's dark panel; and a skinned frame turns plain and back.</summary>
    [TestFixture]
    public class WhiteboxIconsTest
    {
        static IEnumerable<WeaponForm> FormsInUse => ((WeaponForm[])Enum.GetValues(typeof(WeaponForm)))
            .Where(f => f is not (WeaponForm.GravGun or WeaponForm.PhaseDagger or WeaponForm.PlasmaLauncher));

        [Test]
        public void WeaponPixels_EveryFormInUse_DrawsSomething_AndNoTwoAlike()
        {
            var drawn = new Dictionary<WeaponForm, Color32[]>();
            foreach (var form in FormsInUse)
            {
                var pixels = WhiteboxIcons.WeaponPixels((int)form, out int width, out int height);
                Assert.That(pixels.Length, Is.EqualTo(width * height), $"{form}: one colour per pixel");
                Assert.That(pixels.Count(p => p.a > 128), Is.GreaterThan(width * height / 50), $"{form} fills part of its icon");
                foreach (var (other, seen) in drawn)
                    Assert.That(ShareDifferent(pixels, seen), Is.GreaterThan(.02f), $"{form} and {other} look different");
                drawn[form] = pixels;
            }
            Assert.That(drawn.Count, Is.EqualTo(15), "twelve hand weapons and three auto weapons");
        }

        /// <summary>The share of pixels whose colour or cover differs clearly between two pictures of the same size.</summary>
        static float ShareDifferent(Color32[] a, Color32[] b)
        {
            int different = 0;
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) + Math.Abs(a[i].a - b[i].a) > 60) different++;
            return different / (float)a.Length;
        }

        [Test, CreateScene]
        public void IconSetters_ArtOff_GiveTheWhiteboxShapes_ArtOn_GiveTheArtSetsPictures()
        {
            bool was = Art.Enabled;
            var set = ScriptableObject.CreateInstance<V2ArtSet>();
            var picture = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(.5f, .5f));
            foreach (var sprites in new[] { set.weaponSprites, set.elementIcons, set.skillIcons, set.statIcons, set.axisIcons })
                for (int i = 0; i < sprites.Length; i++) sprites[i] = picture;
            var image = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            try
            {
                Art.Enabled = false;
                V2Hud.SetWeaponIcon(image, set, (int)WeaponForm.Rifle);
                Assert.That(image.sprite, Is.SameAs(WhiteboxIcons.Weapon((int)WeaponForm.Rifle)), "a weapon");
                V2Hud.SetElementIcon(image, set, 0);
                Assert.That(image.sprite, Is.SameAs(WhiteboxIcons.Element(0)), "an element");
                V2Hud.SetContentIcon(image, set, (int)SkillId.Blink, -1, -1);
                Assert.That(image.sprite, Is.SameAs(WhiteboxIcons.Skill((int)SkillId.Blink)), "a skill");
                V2Hud.SetContentIcon(image, set, -1, (int)StatId.Damage, -1);
                Assert.That(image.sprite, Is.SameAs(WhiteboxIcons.Stat((int)StatId.Damage)), "a stat");
                V2Hud.SetContentIcon(image, set, -1, -1, 2);
                Assert.That(image.sprite, Is.SameAs(WhiteboxIcons.Axis(2)), "an upgrade direction");
                Assert.That(image.sprite, Is.Not.SameAs(picture), "no picture of the art set in whitebox");

                Art.Enabled = true;
                V2Hud.SetWeaponIcon(image, set, (int)WeaponForm.Rifle);
                Assert.That(image.sprite, Is.SameAs(picture), "the art set's picture with the art on");
            }
            finally
            {
                Art.Enabled = was;
                Object.Destroy(image.gameObject);
                Object.Destroy(set);
                Object.Destroy(picture);
            }
        }

        [TestCase("mk", WhiteboxIcons.Kind.Axis, 1)]
        [TestCase("armor", WhiteboxIcons.Kind.Armor, 0)]
        [TestCase("weapon.Railgun", WhiteboxIcons.Kind.Weapon, (int)WeaponForm.Railgun)]
        [TestCase("twist.Knife", WhiteboxIcons.Kind.Weapon, (int)WeaponForm.Knife)]
        [TestCase("tier.Body", WhiteboxIcons.Kind.Room, (int)CampaignRoom.Energy)]
        [TestCase("stat.MaxHp.Rare", WhiteboxIcons.Kind.Stat, (int)StatId.MaxHp)]
        [TestCase("skill.FrostNova", WhiteboxIcons.Kind.Skill, (int)SkillId.FrostNova)]
        [TestCase("infuse.Knife.Lightning", WhiteboxIcons.Kind.Element, (int)ElementKind.Lightning - 1)]
        [TestCase("overload.shield.alloy", WhiteboxIcons.Kind.Room, (int)CampaignRoom.Control)]
        [TestCase("void.skill.Singularity", WhiteboxIcons.Kind.Skill, (int)SkillId.Singularity)]
        [TestCase("void.enchant.0", WhiteboxIcons.Kind.Element, (int)ElementKind.Void - 1)]
        public void StudyKind_ReadsTheTilesId(string id, WhiteboxIcons.Kind kind, int index)
        {
            Assert.That(WhiteboxIcons.StudyKind(id, out var foundKind, out int foundIndex), Is.True, id);
            Assert.That(foundKind, Is.EqualTo(kind), id);
            Assert.That(foundIndex, Is.EqualTo(index), id);
        }

        [TestCase("")]
        [TestCase("lamp")]
        [TestCase("stat.NoSuchStat.Rare")]
        public void StudyKind_UnknownId_FindsNothing(string id) => Assert.That(WhiteboxIcons.StudyKind(id, out _, out _), Is.False);

        /// <summary>Second round (2026-11-10): the dark matter's icon was nearly lost on the HUD's near-black panel. Laid on that panel's
        /// colour, every currency's drawn pixels are bright on average.</summary>
        [TestCase(WhiteboxIcons.Currency.Energy)]
        [TestCase(WhiteboxIcons.Currency.Alloy)]
        [TestCase(WhiteboxIcons.Currency.Dark)]
        public void CurrencyPixels_OnTheHudsDarkPanel_AreBright(WhiteboxIcons.Currency currency)
        {
            var panel = new Color(.04f, .07f, .12f);
            var pixels = WhiteboxIcons.CurrencyPixels(currency);
            float sum = 0f;
            int drawn = 0;
            foreach (Color pixel in pixels)
            {
                if (pixel.a < .5f) continue;
                sum += WhiteboxIcons.Luminance(Color.Lerp(panel, pixel, pixel.a));
                drawn++;
            }
            Assert.That(drawn, Is.GreaterThan(pixels.Length / 10), $"{currency} fills part of its icon");
            Assert.That(sum / drawn, Is.GreaterThan(.4f), $"{currency} reads on the dark panel");
        }

        [Test, CreateScene]
        public void SkinSet_ArtOff_PlainRectangleWithAThinEdge_ArtOn_TheSkinBack()
        {
            bool was = Art.Enabled;
            var skin = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(.5f, .5f));
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            panel.sprite = skin;
            panel.type = Image.Type.Sliced;
            panel.color = Color.white;
            var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var set = new WhiteboxSkin.Set();
            try
            {
                set.Add(panel, WhiteboxSkin.Look.Panel);
                set.Add(glow, WhiteboxSkin.Look.Hide);

                Art.Enabled = false;
                set.Follow();
                Assert.That(panel.sprite, Is.Null, "no drawn frame in whitebox");
                Assert.That(panel.color, Is.EqualTo(WhiteboxSkin.PanelFill), "a flat colour");
                Assert.That(panel.GetComponent<Outline>() is { enabled: true }, Is.True, "a thin edge");
                Assert.That(glow.enabled, Is.False, "no decoration");

                Art.Enabled = true;
                set.Follow();
                Assert.That(panel.sprite, Is.SameAs(skin), "the drawn frame is back");
                Assert.That(panel.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(panel.color, Is.EqualTo(Color.white));
                Assert.That(panel.GetComponent<Outline>().enabled, Is.False, "no edge over the drawn frame");
                Assert.That(glow.enabled, Is.True, "the decoration is back");
            }
            finally
            {
                Art.Enabled = was;
                Object.Destroy(panel.gameObject);
                Object.Destroy(glow.gameObject);
                Object.Destroy(skin);
            }
        }
    }
}
