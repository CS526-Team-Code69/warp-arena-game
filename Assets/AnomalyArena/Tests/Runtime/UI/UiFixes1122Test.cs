using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// The rules behind the 11-22 interface fixes. Chinese line breaking (ChineseLineBreak): a line fills before it breaks, closing
    /// punctuation never begins a line and opening punctuation never ends one, a number keeps its unit, a comma that just misses hangs at
    /// the line's end; and a wrapping Chinese text on the screen is drawn by these rules. The base tour's plate keeps off the page's parts
    /// beside the highlighted one (TourPlateRoom). A screen-edge marker that would lie on the story's goal plate stands where its line meets
    /// the plate's edge (V2EdgeRoom). A fall into the player's own Pocket Void names that hole on the result screen, whose pull is slower
    /// than a walk.
    /// </summary>
    [TestFixture]
    public class UiFixes1122Test
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        // ───── Chinese line breaking: the rules, with a font where a Chinese character is 10 wide and anything else 5 ─────

        static float Advance(char c, FontStyle style) => c >= '\u2E80' ? 10f : 5f;

        static string[] Lines(string text, float width = 100f)
        {
            string wrapped = ChineseLineBreak.Wrap(text, true, Advance, FontStyle.Normal, width, out int lines);
            Assert.That(wrapped, Is.Not.Null, "the text can be laid out");
            var result = wrapped.Split('\n');
            Assert.That(result.Length, Is.EqualTo(lines), "the line count given matches the lines made");
            return result;
        }

        [Test]
        public void LineBreak_AFullLineThenAComma_TheCommaHangsAtTheLinesEnd()
        {
            Assert.That(Lines("一二三四五六七八九十，一二三"), Is.EqualTo(new[] { "一二三四五六七八九十，", "一二三" }));
        }

        [Test]
        public void LineBreak_AClosingBracketPastTheEdge_TakesItsCharactersDown_AndNoLineEndsOnTheOpeningOne()
        {
            var lines = Lines("一二三四五六七八九（十）一二");
            Assert.That(lines, Is.EqualTo(new[] { "一二三四五六七八九", "（十）一二" }));
        }

        [Test]
        public void LineBreak_ThePercentAndTheUnit_StayWithTheirNumber()
        {
            var percent = Lines("一二三四五六七八 10% 的伤害");
            Assert.That(percent, Is.EqualTo(new[] { "一二三四五六七八 10%", "的伤害" }), "10% is not split and % does not begin a line");
            var seconds = Lines("一二三四五六七八九 8 秒内结束");
            Assert.That(seconds, Is.EqualTo(new[] { "一二三四五六七八九", "8 秒内结束" }), "8 stays with 秒");
            var point = Lines("一二三四五六七八九 1.26 倍");
            Assert.That(point, Is.EqualTo(new[] { "一二三四五六七八九", "1.26 倍" }), "the point of 1.26 does not hang at the line's end");
        }

        [Test]
        public void LineBreak_ALineWithAPlainSpace_FillsBeforeItBreaks()
        {
            // Unity's own layout breaks at the space after 之后 and leaves the line two characters long.
            var lines = Lines("之后 8 秒内伤害提高很多然后结束");
            Assert.That(lines[0], Is.EqualTo("之后 8 秒内伤害提高"), "the first line is filled (95 of 100)");
            Assert.That(lines.Length, Is.EqualTo(2));
        }

        [Test]
        public void LineBreak_TheLongDashAndTheEllipsis_NeverBeginALine()
        {
            var lines = Lines("一二三四五六七八九十……一二");
            Assert.That(lines.Skip(1).Select(l => l[0]), Has.None.EqualTo('…'), string.Join("|", lines));
            Assert.That(lines[1], Does.StartWith("十……"), "the character before the ellipsis goes down with it");
        }

        [Test]
        public void LineBreak_RichTextTags_TakeNoRoom_AndASizeTagLeavesUnitysLayout()
        {
            Assert.That(Lines("<color=#ffcc00>一二三四五</color>六七八九十，一"), Is.EqualTo(new[] { "<color=#ffcc00>一二三四五</color>六七八九十，", "一" }));
            Assert.That(ChineseLineBreak.Wrap("<size=20>一二三</size>", true, Advance, FontStyle.Normal, 100f, out _), Is.Null);
        }

        [Test]
        public void LineBreak_IsForChineseTextsOnly()
        {
            Assert.That(ChineseLineBreak.HasChinese("Press Space to dash toward the cursor"), Is.False);
            Assert.That(ChineseLineBreak.HasChinese("按 E 进入"), Is.True);
            Assert.That(ChineseLineBreak.HasChinese("（Shift）"), Is.True, "full-width brackets count");
        }

        // ───── A wrapping Chinese text on the screen ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task LineBreak_AWrappingChineseTextOnTheScreen_IsDrawnByTheRules_AndNotLongerThanUnitysLayout()
        {
            await Ready();
            var canvas = new GameObject("LineBreakCheck", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var label = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                label.rectTransform.SetParent(canvas.transform, false);
                label.rectTransform.sizeDelta = new Vector2(330f, 400f);
                label.font = ArenaText.Font ? ArenaText.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 20;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.alignment = TextAnchor.UpperLeft;
                label.text = "离你 5–12 格远的怪物会被拉过来，之后 8 秒内它们受到的伤害提高 10%，被拉进黑洞的怪物不会掉落任何东西（经验除外）。夜里要靠矿灯才能采暗物质：回基地，在火箭页造矿灯，每盏 3 合金（现有 2 盏）。";
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                Assert.That(label.TryGetComponent<ChineseLineBreak>(out var effect), Is.True, "the text got the rules as it was first drawn");
                Assert.That(effect.Drawn, Is.Not.Null, "it is drawn from a string laid out by the rules");
                var lines = effect.Drawn.Split('\n');
                Debug.Log("[UIFIX1122] drawn lines: " + string.Join(" | ", lines) + $" ({lines.Length}, Unity's {label.cachedTextGenerator.lineCount})");
                Assert.That(lines.Length, Is.LessThanOrEqualTo(label.cachedTextGenerator.lineCount), "no more lines than Unity's own layout");
                Assert.That(lines.Skip(1).Where(l => "，。、）%：；".IndexOf(l[0]) >= 0), Is.Empty, "no line begins with closing punctuation");
                Assert.That(lines.Take(lines.Length - 1).Select(l => l[l.Length - 1]), Has.None.EqualTo('（'), "no line ends with an opening bracket");
                Assert.That(effect.Drawn.Replace("\n", ""), Does.Contain("10%").And.Contain("8 秒").And.Contain("2 盏").And.Contain("3 合金"),
                    "numbers keep their units");
                // Each line fits the box as the font measures it.
                float room = label.rectTransform.rect.width * label.pixelsPerUnit;
                int size = label.cachedTextGenerator.fontSizeUsedForBestFit;
                foreach (var line in lines)
                {
                    float width = 0f;
                    foreach (char c in line.TrimEnd(' ', '，', '。'))
                        if (label.font.GetCharacterInfo(c, out var glyph, size, label.fontStyle)) width += glyph.advance;
                    Assert.That(width, Is.LessThanOrEqualTo(room + .5f), $"\"{line}\" fits its line");
                }

                ChineseLineBreak.SetEnabled(false);
                await Awaitable.NextFrameAsync();
                Assert.That(effect.Drawn, Is.Null, "turned off, the text is drawn as Unity lays it out");
            }
            finally
            {
                ChineseLineBreak.SetEnabled(true);
                Object.Destroy(canvas.gameObject);
            }
        }

        // ───── The base tour's plate ─────

        // The workshop on the 1600 x 900 reference screen, Day 0: the skill cards at the right; the weapon Mk, the armor row and the two
        // weapon cards in the left column; the keys at the bottom left.
        static readonly Vector2 Area = new(1600f, 900f);
        static readonly Rect SkillCards = Rect.MinMaxRect(800f, 240f, 1585f, 655f);
        static readonly Rect[] LeftColumn =
        {
            Rect.MinMaxRect(30f, 470f, 785f, 690f), // the weapon Mk's card
            Rect.MinMaxRect(30f, 360f, 785f, 455f), // the armor row
            Rect.MinMaxRect(30f, 165f, 395f, 340f), // the main hand's card
            Rect.MinMaxRect(415f, 165f, 785f, 340f), // the other hand's card
            Rect.MinMaxRect(30f, 46f, 135f, 96f),   // the keys
        };

        [Test]
        public void TourPlate_BesideTheSkillCards_KeepsOffTheArmorRowAndTheOtherParts()
        {
            Func<float, float> height = _ => 65f;
            var usual = BaseTour.PlatePlace(BaseTour.PlateSide(Area, SkillCards, 300f, 18f, 16f), Area, SkillCards, new Vector2(560f, 65f), 18f, 16f);
            Assume.That(usual.Overlaps(LeftColumn[1]), Is.True, "the usual place lies over the armor row");
            var place = TourPlateRoom.Place(Area, SkillCards, height, 400f, LeftColumn, Array.Empty<Rect>(), 300f, 560f, 18f, 16f);
            Assert.That(TourPlateRoom.Cost(place, LeftColumn, null), Is.Zero, $"the plate at {place} covers none of the page's parts");
            Assert.That(place.Overlaps(SkillCards), Is.False, "nor the skill cards");
            Assert.That(place.xMin, Is.GreaterThanOrEqualTo(16f).And.LessThanOrEqualTo(Area.x - 16f - place.width), "inside the screen");
        }

        [Test]
        public void TourPlate_WithNothingInTheWay_StaysAtItsUsualPlace()
        {
            var usual = BaseTour.PlatePlace(BaseTour.PlateSide(Area, SkillCards, 300f, 18f, 16f), Area, SkillCards, new Vector2(560f, 65f), 18f, 16f);
            var place = TourPlateRoom.Place(Area, SkillCards, _ => 65f, 400f, Array.Empty<Rect>(), Array.Empty<Rect>(), 300f, 560f, 18f, 16f);
            Assert.That(place, Is.EqualTo(usual));
        }

        // ───── The screen-edge markers and the goal plate ─────

        [Test]
        public void EdgeMarker_LyingOnTheGoalPlate_StandsWhereItsLineMeetsThePlatesEdge()
        {
            var middle = new Vector2(960f, 540f);
            var plate = Rect.MinMaxRect(0f, 600f, 460f, 1080f);
            var steep = V2EdgeRoom.ClearOf(middle, new Vector2(300f, 1000f), plate);
            Assert.That(steep.x, Is.EqualTo(460f).Within(.01f), "up and to the left: just right of the plate");
            Assert.That(steep.y, Is.EqualTo(540f + 460f * (500f / 660f)).Within(.05f), "on its own line from the middle");
            var flat = V2EdgeRoom.ClearOf(middle, new Vector2(100f, 700f), plate);
            Assert.That(flat.x, Is.EqualTo(460f).Within(.01f));
            Assert.That(flat.y, Is.EqualTo(540f + 160f * (500f / 860f)).Within(.05f));
            var below = V2EdgeRoom.ClearOf(middle, new Vector2(200f, 560f), plate);
            Assert.That(below, Is.EqualTo(new Vector2(200f, 560f)), "a marker whose line passes under the plate stays where it is");
            var right = V2EdgeRoom.ClearOf(middle, new Vector2(1500f, 1000f), plate);
            Assert.That(right, Is.EqualTo(new Vector2(1500f, 1000f)), "a marker toward the top right stays at the top edge");
        }

        // ───── The player's own Pocket Void ─────

        [Test]
        public void OwnPocketVoid_TheResultLine_SaysItsPullIsSlowerThanAWalk()
        {
            string own = GameManager.ResultCauseLine("black_hole", null, DamageSource.Hit, null, 0, false, false, true);
            string map = GameManager.ResultCauseLine("black_hole", null, DamageSource.Hit, null, 0, false, false);
            Assert.That(own, Does.Contain(ArenaText.Pair("口袋虚空", "Pocket Void")).And.Contain(ArenaText.Pair("比走路慢", "slower than your walk")));
            Assert.That(own, Does.Not.Contain(ArenaText.Pair("比走路快", "faster than your walk")));
            Assert.That(map, Does.Contain(ArenaText.Pair("比走路快", "faster than your walk")), "the map's black hole keeps its line");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task OwnPocketVoid_UnderThePlayer_EndsTheRun_AndTheResultNamesIt()
        {
            var game = await Ready();
            await StartRun(game);
            var player = game.player;
            WorldMap.Active.CreateVoidHole(player.Position, 3f, 1f, 8f, player, false);
            await Until(() => game.State == GameState.Lost, 10f, "the player's own Pocket Void ends the run");
            Assert.That(WorldMap.Active.PlayerInOwnVoid, Is.True, "the map knows whose hole took the player");
            Assert.That(game.ResultCauseLine(), Does.Contain(ArenaText.Pair("口袋虚空", "Pocket Void")).And.Not.Contain(ArenaText.Pair("比走路快", "faster")));
        }

        // ───── Helpers ─────

        static async Task<GameManager> Ready()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 15f, "scene ready");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        static async Task StartRun(GameManager game)
        {
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The wave never started; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Cutscene) WorldMap.Active.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1, float timeout = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
