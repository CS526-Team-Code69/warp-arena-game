using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The story's three resources each have a colour, a look and a name of their own: alloy copper, alien energy
    /// teal-green, dark matter violet; the whitebox ore's pillars are copper while the dark matter node and the energy crystal keep their
    /// looks; the dark matter node stands on a model of its own (the void crystal's) with a violet glow, the alloy ore glows a warm copper and
    /// the unminable void crystals of Day 0 are matte. The weapon crate names each weapon's attack in a few words before it is taken, in
    /// both languages, its reach following the roster's range.</summary>
    [TestFixture]
    public class ResourceLooksTest
    {
        // ───── Colours ─────

        [Test]
        public void ResourceColours_AreTheDesignColours_ByTheCurrencyIndex()
        {
            Assert.That(ColorUtility.ToHtmlStringRGB(ResourceColours.Alloy), Is.EqualTo("E39A55"), "alloy is copper");
            Assert.That(ColorUtility.ToHtmlStringRGB(ResourceColours.Energy), Is.EqualTo("3FE0B0"), "alien energy is teal-green");
            Assert.That(ColorUtility.ToHtmlStringRGB(ResourceColours.Dark), Is.EqualTo("B48CFF"), "dark matter is the light violet");
            Assert.That(ResourceColours.Of(0), Is.EqualTo(ResourceColours.Energy), "0 is alien energy (ShowGain's index)");
            Assert.That(ResourceColours.Of(1), Is.EqualTo(ResourceColours.Alloy), "1 is alloy");
            Assert.That(ResourceColours.Of(2), Is.EqualTo(ResourceColours.Dark), "2 is dark matter");
        }

        [Test]
        public void WhiteboxOre_HasCopperPillars_TheDarkMatterNodeAndTheEnergyCrystalKeepTheirColours()
        {
            var ore = Whitebox.PartsOf("ore");
            Assert.That(ore.Count, Is.EqualTo(3), "a rock and two pillars");
            for (int i = 1; i < 3; i++)
            {
                var c = ore[i].TableColour;
                Assert.That(Vector3.Distance(new Vector3(c.r, c.g, c.b), new Vector3(.89f, .6f, .33f)), Is.LessThan(.005f), $"pillar {i} is copper");
            }
            foreach (var part in Whitebox.PartsOf("dark").Skip(1))
                Assert.That(part.TableColour, Is.EqualTo(Whitebox.Colour("dark")), "the dark matter node's crystals keep their violet");
            Assert.That(Whitebox.PartsOf("energy")[0].TableColour, Is.EqualTo(Whitebox.Colour("energy")), "the energy crystal keeps its cyan");
            Assert.That(Whitebox.Colour("energy").g, Is.GreaterThan(Whitebox.Colour("energy").r), "a cool colour, not violet");
        }

        // ───── The node models and materials ─────

        const string ArtRoot = "Assets/AnomalyArena/Art/Campaign/";

        [Test]
        public void NodeArt_DarkMatterHasTheVoidCrystalsModel_WithAVioletGlow_OreGlowsCopper_VoidCrystalsAreMatte()
        {
#if UNITY_EDITOR
            GameObject Prefab(string name) => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/" + name + ".prefab");
            Material Mat(string name) => UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "Materials/" + name + ".mat");
            Mesh MeshOf(GameObject prefab) => prefab.GetComponentInChildren<MeshFilter>(true).sharedMesh;
            var ore = Prefab("OreAlloy");
            var dark = Prefab("DarkMatterNode");
            var crystal = Prefab("VoidCrystal");
            Assert.That(ore && dark && crystal, Is.True, "the three node prefabs");
            Assert.That(MeshOf(dark), Is.Not.EqualTo(MeshOf(ore)), "dark matter no longer shares the alloy ore's model");
            Assert.That(MeshOf(dark), Is.EqualTo(MeshOf(crystal)), "it stands on the void crystal's model");
            Assert.That(dark.GetComponentInChildren<Renderer>(true).sharedMaterial, Is.EqualTo(Mat("DarkMatterNode")), "with its own material");

            var darkMat = Mat("DarkMatterNode");
            Assert.That(darkMat.IsKeywordEnabled("_EMISSION"), Is.True, "dark matter glows");
            var darkGlow = darkMat.GetColor("_EmissionColor");
            Assert.That(darkGlow.b, Is.GreaterThan(darkGlow.r), "violet: more blue than red");
            var darkBody = darkMat.GetColor("_BaseColor");
            Assert.That(darkBody.maxColorComponent, Is.LessThanOrEqualTo(.6f), "its body is darkened toward black");

            var oreMat = Mat("OreAlloy");
            Assert.That(oreMat.IsKeywordEnabled("_EMISSION"), Is.True, "the alloy ore has a little warm glow");
            var oreGlow = oreMat.GetColor("_EmissionColor");
            Assert.That(oreGlow.r, Is.GreaterThan(oreGlow.g), "copper: more red than green");
            Assert.That(oreGlow.g, Is.GreaterThan(oreGlow.b), "and more green than blue");
            var oreTint = oreMat.GetColor("_BaseColor");
            Assert.That(oreTint.r, Is.GreaterThan(oreTint.b), "its colour map is tinted warm");

            var crystalMat = Mat("VoidCrystal");
            Assert.That(crystalMat.IsKeywordEnabled("_EMISSION"), Is.False, "the void crystals by Day 0's way home do not glow");
            Assert.That(crystalMat.GetColor("_EmissionColor").maxColorComponent, Is.EqualTo(0f), "no glow colour either");
            Assert.That(crystalMat.GetFloat("_Smoothness"), Is.LessThan(.2f), "they are matte");

            var shardGlow = Mat("EnergyShard").GetColor("_EmissionColor");
            Assert.That(shardGlow.g, Is.GreaterThan(shardGlow.r), "the energy shard glows teal-green, not violet");
#else
            Assert.Ignore("Reads the assets in the editor only.");
#endif
        }

        // ───── The weapon crate's attack words ─────

        static readonly WeaponForm[] Retired = { WeaponForm.GravGun, WeaponForm.PhaseDagger, WeaponForm.PlasmaLauncher };

        [Test]
        public void WeaponStyle_EveryAnomalyWeaponNamesBothItsAttacks_InBothLanguages()
        {
            foreach (WeaponForm form in Enum.GetValues(typeof(WeaponForm)))
            {
                if (form == WeaponForm.Pistol || Retired.Contains(form))
                {
                    Assert.That(WeaponGuide.StyleWords(form, 0), Is.EqualTo(("", "")), $"{form} is never in a crate and has no attack words");
                    continue;
                }
                for (int variant = 0; variant < 2; variant++)
                {
                    var (chinese, english) = WeaponGuide.StyleWords(form, variant);
                    Assert.That(chinese, Does.Contain(" · "), $"{form} {variant}: the reach or kind, then the manner, in Chinese");
                    Assert.That(english, Does.Contain(" · "), $"{form} {variant}: and in English");
                    Assert.That(chinese.Length, Is.LessThanOrEqualTo(12), $"{form} {variant}: a few words, not a sentence");
                }
            }
            Assert.That(WeaponGuide.StyleWords(WeaponForm.Rifle, 0), Is.EqualTo(("远程 · 蓄力单发", "Long range · charged shot")));
            Assert.That(WeaponGuide.StyleWords(WeaponForm.RocketLauncher, 0), Is.EqualTo(("定点 · 延时轰炸", "Targeted · delayed strike")));
            Assert.That(WeaponGuide.StyleWords(WeaponForm.OrbitBlades, 1), Is.EqualTo(("自动 · 绕身刀刃", "Automatic · orbiting blades")));
            Assert.That(WeaponGuide.StyleWords(WeaponForm.Knife, 7), Is.EqualTo(WeaponGuide.StyleWords(WeaponForm.Knife, 1)), "any variant but 0 is the second");
        }

        [Test]
        public void WeaponStyle_ReachFollowsTheRosterRange()
        {
#if UNITY_EDITOR
            var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponRoster>("Assets/AnomalyArena/V2/Data/WeaponRoster.asset");
            Assert.That(roster, Is.Not.Null, "the weapon roster");
            float blizzard = new WeaponUpgradeTuning().blizzardMaxRange;
            var checkedReaches = new List<string>();
            foreach (WeaponForm form in Enum.GetValues(typeof(WeaponForm)))
            {
                var definition = roster.Get(form);
                if (definition == null || definition.retired || form == WeaponForm.Pistol) continue;
                for (int variant = 0; variant < 2; variant++)
                {
                    string chinese = WeaponGuide.StyleWords(form, variant).chinese;
                    float reach = variant == 0 ? definition.rangeA : definition.rangeB;
                    // The Blizzard's spray stops short of its range (RunWeaponsUpgrades.BlizzardSpray).
                    if (form == WeaponForm.CryoCannon && variant == 1) reach = Mathf.Min(reach, blizzard);
                    if (chinese.StartsWith("近距")) Assert.That(reach, Is.LessThanOrEqualTo(7f), $"{form} {variant} is close: {reach} cells");
                    else if (chinese.StartsWith("中距")) Assert.That(reach, Is.InRange(8f, 15f), $"{form} {variant} is mid range: {reach} cells");
                    else if (chinese.StartsWith("远程")) Assert.That(reach, Is.GreaterThanOrEqualTo(20f), $"{form} {variant} is long range: {reach} cells");
                    else continue;
                    checkedReaches.Add($"{form} {variant}");
                }
            }
            Assert.That(checkedReaches, Has.Count.GreaterThanOrEqualTo(18), "every hand weapon's reach was checked");
#else
            Assert.Ignore("Reads the roster in the editor only.");
#endif
        }
    }
}
