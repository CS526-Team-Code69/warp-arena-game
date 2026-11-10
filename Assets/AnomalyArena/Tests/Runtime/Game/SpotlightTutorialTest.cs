using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Day 0's forced spotlight tutorial and the weapon crates (2026-11-10), the rules that need no scene: which inputs each
    /// step lets through, what is taught once a step is done, the step conditions that are plain arithmetic, where a crate's weapon goes,
    /// and the numbers the implementation spec gives.</summary>
    [TestFixture]
    public class SpotlightTutorialTest
    {
        [TestCase(TutorialStep.Move, PlayerAct.Move)]
        [TestCase(TutorialStep.Fire, PlayerAct.Fire)]
        [TestCase(TutorialStep.Mine, PlayerAct.Move)]
        [TestCase(TutorialStep.Crate, PlayerAct.Move)]
        [TestCase(TutorialStep.Sprint, PlayerAct.Move | PlayerAct.Sprint)]
        [TestCase(TutorialStep.Fight, PlayerAct.Move | PlayerAct.Sprint | PlayerAct.Fire)]
        [TestCase(TutorialStep.Collect, PlayerAct.Move)]
        [TestCase(TutorialStep.Lamp, PlayerAct.None)]
        [TestCase(TutorialStep.DarkMatter, PlayerAct.Move)]
        [TestCase(TutorialStep.Switch, PlayerAct.Switch)]
        public void Allowed_WhileAStepIsTaught_IsItsOwnInputAndWalkingWhereItMustReachSomething(TutorialStep step, PlayerAct expected)
        {
            Assert.That(SpotlightTutorial.Allowed(step), Is.EqualTo(expected));
        }

        [Test]
        public void Allowed_OnlyTheStepThatTeachesIt_LetsTheHandsBeSwitched()
        {
            foreach (TutorialStep step in System.Enum.GetValues(typeof(TutorialStep)))
            {
                if (step is TutorialStep.None or TutorialStep.SecondCrate or TutorialStep.Done) continue;
                // The right button works under a spotlight only in the step that teaches it (the second crate opened, since 2026-11-10).
                var expected = step == TutorialStep.Switch ? PlayerAct.Switch : PlayerAct.None;
                Assert.That(SpotlightTutorial.Allowed(step) & PlayerAct.Switch, Is.EqualTo(expected), step.ToString());
            }
        }

        [TestCase(TutorialStep.None, PlayerAct.None)]
        [TestCase(TutorialStep.Move, PlayerAct.Move)]
        [TestCase(TutorialStep.Fire, PlayerAct.Move | PlayerAct.Fire)]
        [TestCase(TutorialStep.Crate, PlayerAct.Move | PlayerAct.Fire)]
        [TestCase(TutorialStep.Sprint, PlayerAct.Move | PlayerAct.Fire | PlayerAct.Sprint)]
        [TestCase(TutorialStep.Fight, PlayerAct.All)]
        [TestCase(TutorialStep.Collect, PlayerAct.All)]
        [TestCase(TutorialStep.Done, PlayerAct.All)]
        [TestCase(TutorialStep.Switch, PlayerAct.All)]
        public void Taught_BetweenSteps_IsWhatTheStepsDoneSoFarTaught(TutorialStep lastDone, PlayerAct expected)
        {
            Assert.That(SpotlightTutorial.Taught(lastDone), Is.EqualTo(expected));
        }

        [Test]
        public void Lets_FollowsTheStepWhileForced_AndWhatIsTaughtBetweenSteps()
        {
            // Firing is taught (step 2 done), but the mining step's spotlight holds it back.
            Assert.That(SpotlightTutorial.Lets(PlayerAct.Fire, TutorialStep.Mine, TutorialStep.Fire), Is.False);
            Assert.That(SpotlightTutorial.Lets(PlayerAct.Move, TutorialStep.Mine, TutorialStep.Fire), Is.True);
            // Walking to the ore with no spotlight up: walking and firing work, sprinting is not taught yet.
            Assert.That(SpotlightTutorial.Lets(PlayerAct.Fire, TutorialStep.None, TutorialStep.Fire), Is.True);
            Assert.That(SpotlightTutorial.Lets(PlayerAct.Sprint, TutorialStep.None, TutorialStep.Fire), Is.False);
            // The firing step itself: the player stands still.
            Assert.That(SpotlightTutorial.Lets(PlayerAct.Move, TutorialStep.Fire, TutorialStep.Move), Is.False);
        }

        [Test]
        public void MovedToward_CountsOnlyTheWayTowardTheTarget_OnTheGround()
        {
            var start = new Vector3(0f, 0f, 0f);
            var target = new Vector3(10f, 0f, 0f);
            Assert.That(SpotlightTutorial.MovedToward(start, new Vector3(4f, 3f, 0f), target), Is.EqualTo(4f).Within(1e-4f), "height does not count");
            Assert.That(SpotlightTutorial.MovedToward(start, new Vector3(0f, 0f, 5f), target), Is.EqualTo(0f).Within(1e-4f), "sideways counts nothing");
            Assert.That(SpotlightTutorial.MovedToward(start, new Vector3(-2f, 0f, 0f), target), Is.EqualTo(-2f).Within(1e-4f), "away counts negative");
            Assert.That(SpotlightTutorial.MovedToward(start, new Vector3(3f, 0f, 4f), start), Is.EqualTo(5f).Within(1e-4f), "no direction: the distance");
        }

        [TestCase(0f, .3f, true)]
        [TestCase(.05f, .3f, true)]
        [TestCase(.3f, .28f, false)]
        [TestCase(0f, 0f, false)]
        public void Fired_IsAJumpOfTheCooldown(float before, float now, bool expected)
        {
            Assert.That(SpotlightTutorial.Fired(before, now), Is.EqualTo(expected));
        }

        [Test]
        public void Reach_IsTheFarthestPointAlongTheWay()
        {
            var trail = new[] { new Vector3(5f, 0f, 1f), new Vector3(20f, 2f, -3f), new Vector3(12f, 0f, 0f) };
            Assert.That(SpotlightTutorial.Reach(trail, Vector3.zero, new Vector3(2f, 0f, 0f)), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(SpotlightTutorial.Reach(new Vector3[0], Vector3.zero, Vector3.right), Is.EqualTo(0f));
            Assert.That(SpotlightTutorial.Reach(trail, Vector3.zero, Vector3.zero), Is.EqualTo(0f), "no way: no reach");
        }

        static RunWeapon Weapon(WeaponForm form) => new() { form = form };

        [Test]
        public void HandFor_PrefersAFreeHand_ThenThePistol_ThenTheHandNotInUse()
        {
            var pistol = Weapon(WeaponForm.Pistol);
            Assert.That(CrateSlots.HandFor(new[] { pistol, null }, 0), Is.EqualTo(1), "Day 0's first crate: the free hand");
            Assert.That(CrateSlots.HandFor(new[] { Weapon(WeaponForm.Rifle), pistol }, 0), Is.EqualTo(1), "the pistol's hand");
            Assert.That(CrateSlots.HandFor(new[] { pistol, Weapon(WeaponForm.Rifle) }, 1), Is.EqualTo(0), "the pistol's hand, even in use");
            Assert.That(CrateSlots.HandFor(new[] { Weapon(WeaponForm.Rifle), Weapon(WeaponForm.Knife) }, 0), Is.EqualTo(1), "the hand not in use");
            Assert.That(CrateSlots.HandFor(new[] { Weapon(WeaponForm.Rifle), Weapon(WeaponForm.Knife) }, 1), Is.EqualTo(0), "the hand not in use");
        }

        [Test]
        public void SlotFor_PutsAnAutoWeaponOnlyIntoAFreeAutoSlot()
        {
            var hand = new[] { Weapon(WeaponForm.Rifle), Weapon(WeaponForm.Knife) };
            var drone = Weapon(WeaponForm.SentryDrone);
            Assert.That(CrateSlots.SlotFor(drone, hand, new[] { Weapon(WeaponForm.OrbitBlades), null }, 0), Is.EqualTo(1));
            Assert.That(CrateSlots.SlotFor(drone, hand, new[] { Weapon(WeaponForm.OrbitBlades), Weapon(WeaponForm.ShieldRing) }, 0), Is.EqualTo(-1));
            Assert.That(CrateSlots.SlotFor(null, hand, new RunWeapon[2], 0), Is.EqualTo(-1));
        }

        [Test]
        public void Rules_StartAtTheSpecsNumbers()
        {
            var rules = ScriptableObject.CreateInstance<CampaignRules>();
            try
            {
                Assert.That(rules.spotlightDim, Is.EqualTo(.75f), "dim 0.75");
                Assert.That(rules.tutorialMoveMeters, Is.EqualTo(4f), "step 1: 4 cells");
                Assert.That(rules.tutorialShots, Is.EqualTo(3), "step 2: 3 shots");
                Assert.That(rules.tutorialFightGrunts, Is.EqualTo(4), "step 6: 4 monsters");
                Assert.That(rules.crateTrailShare, Is.EqualTo(.6f), "the crate at six tenths of the trail");
            }
            finally { Object.DestroyImmediate(rules); }
        }
    }
}
