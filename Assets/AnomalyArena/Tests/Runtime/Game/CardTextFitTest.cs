using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{

    [TestFixture]
    public class CardTextFitTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task DraftCards_EverySkillAndWeaponText_FitsTheBodyBoxInBothLanguages()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var screen = UnityEngine.Object.FindObjectsByType<V2UiScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(s => s.state == ArenaUiState.Draft && s.choices != null && s.choices.Length > 0);
            Assume.That(screen, Is.Not.Null, "the saved scene has the draft screen");
            var choice = screen.choices[0];
            bool screenWasActive = screen.gameObject.activeSelf, choiceWasActive = choice.gameObject.activeSelf;
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                screen.gameObject.SetActive(true);
                choice.gameObject.SetActive(true);
                choice.FitBody();
                int savedSize = choice.body.resizeTextMaxSize;
                foreach (var code in new[] { "zh-Hans", "en" })
                {
                    locale.localeCode = code;
                    locale.SelectLocale();
                    bool chinese = code.StartsWith("zh");
                    await Until(() => ArenaText.Ready && ArenaText.IsChinese == chinese, 12f, "locale " + code);
                    if (chinese)
                    {

                        choice.ShowBody("拿到之前不知道是哪种攻击方式和特性；选中的那一刻揭晓。");
                        Assert.That(choice.body.fontSize, Is.EqualTo(savedSize), "a short Chinese text keeps the largest size and wraps");
                    }
                    var overflow = new List<string>();
                    foreach (var (name, text) in CardTexts())
                    {
                        choice.ShowBody(text);
                        Canvas.ForceUpdateCanvases();
                        var settings = choice.body.GetGenerationSettings(choice.body.rectTransform.rect.size);
                        var generator = new TextGenerator();
                        generator.PopulateWithErrors(text, settings, choice.gameObject);
                        int visible = generator.characterCountVisible;
                        int needed = text.Count(c => !char.IsWhiteSpace(c));
                        int shown = text.Take(visible).Count(c => !char.IsWhiteSpace(c));
                        if (shown < needed) overflow.Add($"{code} {name}: {shown}/{needed} chars at size {choice.body.fontSize}");
                    }
                    Assert.That(overflow, Is.Empty, "card texts cut off in " + code);
                }
                Assert.That(choice.body.resizeTextMaxSize, Is.EqualTo(savedSize), "the saved size stays the largest");
            }
            finally
            {
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                UnityEngine.Object.Destroy(locale.gameObject);
                choice.gameObject.SetActive(choiceWasActive);
                screen.gameObject.SetActive(screenWasActive);
            }
        }

        static IEnumerable<(string, string)> CardTexts()
        {
            var catalog = GameManager.Instance.skills.Catalog;
            foreach (var skill in catalog.skills.Where(s => s))
            for (int level = 1; level <= SkillDefinition.MaxLevel; level++)
                yield return ($"{skill.id} L{level}",
                    skill.Description(level) + ArenaText.Pair($"\n{skill.cost:0} Energy",$"\n{skill.cost:0} Energy"));
            for (int form = (int)WeaponForm.GravGun; form <= (int)WeaponForm.SwarmLauncher; form++)
            for (int variant = 0; variant < 2; variant++)
            foreach (var axis in new[] { UpgradeAxis.Twist, UpgradeAxis.Tame })
                yield return ($"{(WeaponForm)form} {variant} {axis}", ArenaTerms.ExpandedUpgradeDescription((WeaponForm)form, variant, axis));
            foreach (var card in StartingFormCards()) yield return card;
        }

        static List<(string, string)> StartingFormCards()
        {
            var weapons = GameManager.Instance.runWeapons;
            var hand = weapons.Hand;
            var auto = weapons.Auto;
            var cards = new List<(string, string)>();
            try
            {
                for (int form = (int)WeaponForm.Rifle; form <= (int)WeaponForm.RocketLauncher; form++)
                for (int variant = 0; variant < 2; variant++)
                foreach (var axis in new[] { UpgradeAxis.Twist, UpgradeAxis.Tame })
                {
                    var open = UpgradeAxis.Infusion | (axis == UpgradeAxis.Twist ? UpgradeAxis.Tame : UpgradeAxis.Twist);
                    weapons.Hand = new[] { new RunWeapon { form = (WeaponForm)form, variant = variant, revealed = true, axes = open }, null };
                    weapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
                    var card = weapons.RollUpgradeCard();
                    Assert.That(card, Is.Not.Null, $"{(WeaponForm)form} {variant} {axis}: a card");
                    cards.Add(($"{(WeaponForm)form} {variant} {axis}", card.Description));
                }
            }
            finally { weapons.Hand = hand; weapons.Auto = auto; }
            return cards;
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
