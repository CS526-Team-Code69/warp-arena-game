using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>The key map (ArenaKeys), no scene: the default keys are the ones the game always had; an action takes a new key; a key
    /// another action holds swaps the two, so no two actions ever share a key; the menus' fixed keys and the keys a browser takes are
    /// refused; the keys are saved and read back; a saved text that makes no sense leaves the defaults. Keys are given by their names
    /// in the keyboard's own list, because the tests do not reference the Input System. The choices live in memory for the whole test
    /// run (TestSavedSettings), so nothing here reaches PlayerPrefs.</summary>
    [TestFixture]
    public class ArenaKeysTest
    {
        static readonly GameAction[] Actions = (GameAction[])Enum.GetValues(typeof(GameAction));

        [SetUp]
        public void SetUp()
        {
            Assume.That(ArenaPrefs.MemoryForTests, Is.True, "the test run keeps the saved choices in memory (TestSavedSettings)");
            ArenaKeys.ResetToDefaults();
        }

        [TearDown]
        public void TearDown() => ArenaKeys.ResetToDefaults();

        // ───── Defaults ─────

        [TestCase(GameAction.MoveUp, "W")]
        [TestCase(GameAction.MoveLeft, "A")]
        [TestCase(GameAction.MoveDown, "S")]
        [TestCase(GameAction.MoveRight, "D")]
        [TestCase(GameAction.Sprint, "LeftShift")]
        [TestCase(GameAction.Interact, "E")]
        [TestCase(GameAction.Skill1, "Digit1")]
        [TestCase(GameAction.Skill2, "Digit2")]
        [TestCase(GameAction.Skill3, "Digit3")]
        [TestCase(GameAction.Lamp, "F")]
        [TestCase(GameAction.CardPick, "T")]
        public void Defaults_EachAction_IsOnTheKeyTheGameAlwaysHad(GameAction action, string key)
        {
            Assert.That(ArenaKeys.KeyNameOf(action), Is.EqualTo(key));
            Assert.That(ArenaKeys.DefaultKeyNameOf(action), Is.EqualTo(key));
        }

        [Test]
        public void Defaults_TheNamesTheTextsShow_AreTheOnesTheyAlwaysShowed()
        {
            Assert.That(ArenaKeys.Count, Is.EqualTo(Actions.Length), "one key for each action");
            Assert.That(ArenaKeys.MoveKeys, Is.EqualTo("WASD"));
            Assert.That(ArenaKeys.SprintKey, Is.EqualTo("Shift"));
            Assert.That(ArenaKeys.SkillKeys, Is.EqualTo("1 / 2 / 3"));
            Assert.That(ArenaKeys.SkillKeysWith(", "), Is.EqualTo("1, 2, 3"));
            Assert.That(ArenaKeys.InteractKey, Is.EqualTo("E"));
            Assert.That(ArenaKeys.LampKey, Is.EqualTo("F"));
            Assert.That(ArenaKeys.CardPickKey, Is.EqualTo("T"));
        }

        // ───── Rebinding ─────

        [Test]
        public void Bind_AFreeKey_TheActionTakesIt_AndTheViewsAreTold()
        {
            int revision = ArenaKeys.Revision, told = 0;
            Action count = () => told++;
            ArenaKeys.Changed += count;
            try
            {
                var result = ArenaKeys.BindByName(GameAction.Interact, "G", out var swappedWith);

                Assert.That(result, Is.EqualTo(KeyBindResult.Bound));
                Assert.That(swappedWith, Is.EqualTo(GameAction.Interact), "nothing was swapped");
                Assert.That(ArenaKeys.KeyNameOf(GameAction.Interact), Is.EqualTo("G"));
                Assert.That(ArenaKeys.InteractKey, Is.EqualTo("G"), "the texts name the new key");
                Assert.That(ArenaKeys.Revision, Is.GreaterThan(revision), "a view that wrote the old name can tell it must write again");
                Assert.That(told, Is.EqualTo(1), "and whoever listens is told once");
                Assert.That(Actions.Where(action => action != GameAction.Interact).Select(ArenaKeys.KeyNameOf),
                    Is.EqualTo(Actions.Where(action => action != GameAction.Interact).Select(ArenaKeys.DefaultKeyNameOf)), "the other actions keep their keys");
            }
            finally { ArenaKeys.Changed -= count; }
        }

        [Test]
        public void Bind_TheKeyItAlreadyHas_ChangesNothing()
        {
            int revision = ArenaKeys.Revision;

            var result = ArenaKeys.BindByName(GameAction.Lamp, "F", out _);

            Assert.That(result, Is.EqualTo(KeyBindResult.Unchanged));
            Assert.That(ArenaKeys.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void Bind_ArrowKeys_TheMoveKeysAreNamedByTheirArrows()
        {
            ArenaKeys.BindByName(GameAction.MoveUp, "UpArrow", out _);
            ArenaKeys.BindByName(GameAction.MoveLeft, "LeftArrow", out _);
            ArenaKeys.BindByName(GameAction.MoveDown, "DownArrow", out _);
            ArenaKeys.BindByName(GameAction.MoveRight, "RightArrow", out _);

            Assert.That(ArenaKeys.MoveKeys, Is.EqualTo("↑ ← ↓ →"), "up, left, down, right, apart: they are no longer four letters of one word");
        }

        // ───── A key another action holds ─────

        [Test]
        public void Bind_AKeyAnotherActionHolds_SwapsTheTwo_SoNoKeyIsSharedAndNoActionIsLeftWithoutOne()
        {
            var result = ArenaKeys.BindByName(GameAction.Lamp, "E", out var swappedWith);

            Assert.That(result, Is.EqualTo(KeyBindResult.Swapped));
            Assert.That(swappedWith, Is.EqualTo(GameAction.Interact), "the action that held the key");
            Assert.That(ArenaKeys.KeyNameOf(GameAction.Lamp), Is.EqualTo("E"));
            Assert.That(ArenaKeys.KeyNameOf(GameAction.Interact), Is.EqualTo("F"), "it takes the key the other one gave up");
            AssertEveryActionHasItsOwnKey();
        }

        [Test]
        public void Bind_EitherShift_IsTheOneShift()
        {
            var result = ArenaKeys.BindByName(GameAction.Lamp, "RightShift", out var swappedWith);

            Assert.That(result, Is.EqualTo(KeyBindResult.Swapped), "sprinting is on Shift, and the right Shift is the same key to the game");
            Assert.That(swappedWith, Is.EqualTo(GameAction.Sprint));
            Assert.That(ArenaKeys.KeyNameOf(GameAction.Lamp), Is.EqualTo("LeftShift"));
            Assert.That(ArenaKeys.LampKey, Is.EqualTo("Shift"));
            AssertEveryActionHasItsOwnKey();
        }

        // ───── Keys that cannot be used ─────

        // The menus' own keys, the keys a browser or the system takes, and what is no key at all.
        [TestCase("Escape"), TestCase("Tab"), TestCase("R"), TestCase("Space"), TestCase("Enter"), TestCase("NumpadEnter")]
        [TestCase("LeftCtrl"), TestCase("RightAlt"), TestCase("LeftMeta"), TestCase("F5"), TestCase("CapsLock")]
        [TestCase("F13"), TestCase("None"), TestCase("NoSuchKey"), TestCase(""), TestCase("5"), TestCase((string)null)]
        public void Bind_AFixedOrUnsafeKeyOrNoKey_IsRefused_AndNothingChanges(string key)
        {
            string before = ArenaKeys.SavedText;
            int revision = ArenaKeys.Revision;

            var result = ArenaKeys.BindByName(GameAction.Sprint, key, out _);

            Assert.That(result, Is.EqualTo(KeyBindResult.Refused));
            Assert.That(ArenaKeys.CanBindByName(key), Is.False);
            Assert.That(ArenaKeys.SavedText, Is.EqualTo(before));
            Assert.That(ArenaKeys.Revision, Is.EqualTo(revision));
        }

        [TestCase("G"), TestCase("Digit7"), TestCase("UpArrow"), TestCase("Numpad1"), TestCase("LeftShift"), TestCase("Backspace"), TestCase("Semicolon")]
        public void CanBind_AnOrdinaryKey_MayBeUsed(string key) => Assert.That(ArenaKeys.CanBindByName(key), Is.True);

        // ───── Saved and read back ─────

        [Test]
        public void Bind_IsSavedAtOnce_AndReadBackAtTheNextStart()
        {
            ArenaKeys.BindByName(GameAction.Interact, "G", out _);
            ArenaKeys.BindByName(GameAction.Lamp, "E", out _);
            string saved = ArenaPrefs.GetString(ArenaKeys.SavedKey, "");

            ArenaKeys.Reload(); // forgets the keys in memory, as a new play does

            Assert.That(saved, Does.Contain("Interact=G").And.Contain("Lamp=E"), "the keys are saved by the actions' and the keys' names");
            Assert.That(saved, Is.EqualTo(ArenaKeys.SavedText));
            Assert.That(ArenaKeys.KeyNameOf(GameAction.Interact), Is.EqualTo("G"), "read back");
            Assert.That(ArenaKeys.KeyNameOf(GameAction.Lamp), Is.EqualTo("E"));
            Assert.That(ArenaKeys.KeyNameOf(GameAction.Sprint), Is.EqualTo("LeftShift"), "an action never changed is still on its default");
        }

        [Test]
        public void ResetToDefaults_EveryActionIsBackOnItsKey_AndThatIsSaved()
        {
            ArenaKeys.BindByName(GameAction.Interact, "G", out _);
            ArenaKeys.BindByName(GameAction.Skill1, "Q", out _);

            ArenaKeys.ResetToDefaults();
            ArenaKeys.Reload();

            Assert.That(Actions.Select(ArenaKeys.KeyNameOf), Is.EqualTo(Actions.Select(ArenaKeys.DefaultKeyNameOf)));
        }

        [Test]
        public void SavedText_WithEntriesThatMakeNoSense_KeepsTheGoodOnesAndSkipsTheRest()
        {
            // An action that does not exist, a fixed key, an entry without a key, one without an action, and a good one.
            ArenaPrefs.SetString(ArenaKeys.SavedKey, "Jump=Q;Lamp=Escape;Sprint=;=K;Interact=G;nonsense");

            ArenaKeys.Reload();

            Assert.That(ArenaKeys.KeyNameOf(GameAction.Interact), Is.EqualTo("G"));
            Assert.That(ArenaKeys.KeyNameOf(GameAction.Lamp), Is.EqualTo("F"), "a fixed key is not taken from a saved text either");
            Assert.That(ArenaKeys.KeyNameOf(GameAction.Sprint), Is.EqualTo("LeftShift"));
            AssertEveryActionHasItsOwnKey();
        }

        [Test]
        public void SavedText_ThatPutsTwoActionsOnOneKey_IsDroppedWhole()
        {
            // Interact on F, which the lamp holds by default: nothing says which of the two should keep it.
            ArenaPrefs.SetString(ArenaKeys.SavedKey, "Skill1=Q;Interact=F");

            ArenaKeys.Reload();

            Assert.That(Actions.Select(ArenaKeys.KeyNameOf), Is.EqualTo(Actions.Select(ArenaKeys.DefaultKeyNameOf)), "the defaults stand");
        }

        static void AssertEveryActionHasItsOwnKey()
        {
            var keys = new List<string>();
            foreach (var action in Actions) keys.Add(ArenaKeys.KeyNameOf(action));
            Assert.That(keys, Is.Unique, "no two actions share a key");
            Assert.That(keys, Has.None.Empty.And.None.EqualTo("None"), "and none is without one");
        }
    }
}
