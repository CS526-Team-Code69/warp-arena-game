using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The bottom row shows every weapon's and every skill's name whole in both languages. The slots' title boxes are measured
    /// for the Chinese names (V2HudLayout); a longer English name gets smaller instead of losing letters: "Shockwave" showed as
    /// "Shockwa", "Pocket Void" as "Pocket", and "Launch Yourself · Mk 4" lost its Mk. The fifth pass's slot (V5BattleSlot, every page
    /// but the English Alpha's) names a weapon by its form alone, its kind and tier in the row under it, on one line that gets smaller
    /// until it fits. A rarity's name colour reads on the dark slot, Void's too, whose own colour is black.</summary>
    [TestFixture]
    public class HudSlotNameFitTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task SlotTitles_EveryWeaponAndSkillName_IsShownWholeInBothLanguages()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var hud = UnityEngine.Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
            Assume.That(hud && hud.handSlots is { Length: > 0 } && hud.autoSlots is { Length: > 0 } && hud.skillSlots is { Length: > 0 }, Is.True,
                "the scene's HUD has its three groups of slots");
            V2UiSlot hand = hud.handSlots[0], auto = hud.autoSlots[0], skill = hud.skillSlots[0];
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                foreach (var code in new[] { "zh-Hans", "en" })
                {
                    locale.localeCode = code;
                    locale.SelectLocale();
                    bool chinese = code.StartsWith("zh");
                    await Until(() => ArenaText.Ready && ArenaText.IsChinese == chinese, 12f, "locale " + code);
                    var cut = new List<string>();
                    foreach (WeaponForm form in Enum.GetValues(typeof(WeaponForm)))
                    for (int variant = 0; variant < 2; variant++)
                    foreach (int mk in new[] { 1, 2, 10 })
                    {
                        var weapon = new RunWeapon { form = form, variant = variant, revealed = true, mk = mk };
                        Check(weapon.IsAuto ? auto : hand, new ArenaUiSlot { title = weapon.Label, formIndex = (int)form }, ArenaTerms.WeaponForm(form), hud.art, code, cut);
                    }
                    foreach (SkillId id in Enum.GetValues(typeof(SkillId)))
                        Check(skill, new ArenaUiSlot { title = ArenaTerms.Skill(id), skillIndex = (int)id }, ArenaTerms.Skill(id), hud.art, code, cut);
                    Assert.That(cut, Is.Empty, "names cut short in the bottom row in " + code);
                }
            }
            finally
            {
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                UnityEngine.Object.Destroy(locale.gameObject);
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task SlotTitle_OfAVoidSkill_IsWrittenInAColourThatReadsOnTheSlot()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var hud = UnityEngine.Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
            Assume.That(hud && hud.skillSlots is { Length: > 0 }, Is.True, "the scene's HUD has its skill slots");
            var slot = hud.skillSlots[0];

            slot.Render(new ArenaUiSlot { title = ArenaTerms.Skill(SkillId.PocketVoid), rarity = CardRarity.Void, skillIndex = (int)SkillId.PocketVoid }, hud.art);

            Assert.That(slot.title.color.grayscale, Is.GreaterThan(.6f), "a void name is light enough to read on the dark slot");
            Assert.That(slot.title.color, Is.Not.EqualTo(Color.white), "and it is not the white of a common name");
        }

        [TestCase(CardRarity.Common)]
        [TestCase(CardRarity.Rare)]
        [TestCase(CardRarity.Exceptional)]
        [TestCase(CardRarity.Epic)]
        [TestCase(CardRarity.Legendary)]
        [TestCase(CardRarity.Void)]
        public void InkFor_EveryRarity_ReadsOnADarkGround(CardRarity rarity) =>
            Assert.That(ArenaRarity.InkFor(rarity).grayscale, Is.GreaterThan(.5f), "a name in this colour reads on a dark slot");

        [Test]
        public void InkFor_EveryRarityButVoid_IsTheRaritysOwnColour()
        {
            foreach (CardRarity rarity in Enum.GetValues(typeof(CardRarity)))
                if (rarity != CardRarity.Void) Assert.That(ArenaRarity.InkFor(rarity), Is.EqualTo(ArenaRarity.ColorFor(rarity)), rarity.ToString());
        }

        /// <summary>Shows the name in the slot as the HUD does and counts the characters the title's box holds; on the fifth pass's slot,
        /// checks it shows the name expected (a weapon's form, a skill's name) no wider than its box.</summary>
        static void Check(V2UiSlot slot, ArenaUiSlot value, string expected, V2ArtSet art, string code, List<string> cut)
        {
            slot.Render(value, art);
            string name = value.title, shown = slot.title.text;
            if (slot.v5)
            {
                float width = slot.title.preferredWidth, room = slot.title.rectTransform.rect.width;
                if (shown != expected || width > room + .5f) cut.Add($"{code} {slot.name} \"{name}\": shows \"{shown}\" {width:0.#} wide in {room:0.#}");
                return;
            }
            var settings = slot.title.GetGenerationSettings(slot.title.rectTransform.rect.size);
            var generator = new TextGenerator();
            generator.PopulateWithErrors(shown, settings, slot.gameObject);
            int needed = shown.Count(c => !char.IsWhiteSpace(c));
            int held = shown.Take(generator.characterCountVisible).Count(c => !char.IsWhiteSpace(c));
            if (held < needed) cut.Add($"{code} {slot.name} \"{name}\": {held}/{needed} characters at scale {settings.scaleFactor:0.00}");
        }

        static async Task Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
