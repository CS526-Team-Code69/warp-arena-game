using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AnomalyArena
{

    [TestFixture]
    public class ArenaEmphasisTest
    {
        [Test]
        public void Apply_NumbersKeysAndCosts_TurnBoldInTheirColours()
        {
            var actual = ArenaEmphasis.Apply("每次命中 6.8 点，伤害 +30%。按 R 重抽，1 / 2 / 3 选择；原来的强化会丢失。");

            Assert.That(actual, Does.Contain("<b><color=#FFD54A>6.8 点</color></b>"), "a number with its unit turns gold");
            Assert.That(actual, Does.Contain("<b><color=#FFD54A>+30%</color></b>"), "and so does a percentage");
            Assert.That(actual, Does.Contain("<b><color=#7FD8FF>R</color></b>").And.Contain("<b><color=#7FD8FF>1 / 2 / 3</color></b>"),
                "keys turn light blue");
            Assert.That(actual, Does.Contain("<b><color=#FF6B5B>会丢失</color></b>"), "a cost turns red");
        }

        [Test]
        public void Apply_ElementWord_TakesItsElementColour()
        {
            var actual = ArenaEmphasis.Apply("属性：火焰");

            Assert.That(actual, Does.Contain($"<b><color=#{UnityEngine.ColorUtility.ToHtmlStringRGB(V2Hud.ElementColors[0])}>火焰</color></b>"));
        }

        [Test]
        public void Apply_TextWithTags_KeepsTheTagsAndReadsTheSame()
        {
            const string source = "<color=#9ad8ff>手持武器</color>\n\n<b><color=#ffd36e>伤害</color></b>\n每次命中 12 点";

            var actual = ArenaEmphasis.Apply(source);

            Assert.That(actual, Does.StartWith("<color=#9ad8ff>手持武器</color>"), "the digits inside a colour tag are left alone");
            Assert.That(actual, Does.Contain("<b><color=#FFD54A>12 点</color></b>"));
            Assert.That(Regex.Replace(actual, "<[^<>]+>", ""), Is.EqualTo(Regex.Replace(source, "<[^<>]+>", "")), "only tags are added");
        }

        [TestCase("火箭筒")]
        [TestCase("Element: None")]
        public void Apply_WordsThatOnlyContainAKeyOrElementCharacter_StayPlain(string text)
        {
            var actual = ArenaEmphasis.Apply(text);

            Assert.That(actual, Is.EqualTo(text));
        }
    }
}
