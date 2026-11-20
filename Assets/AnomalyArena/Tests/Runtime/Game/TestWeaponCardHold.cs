using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>
    /// A weapon card whose trait the player has never seen holds for GameManager.NewTraitCardSeconds before it can be closed. The tests
    /// walk through many weapon cards on a machine where every trait may still be new, so for the whole run every card keeps the usual
    /// short lock. A test of the hold itself turns GameManager.ShortWeaponCardHold off for its own run and back on afterwards.
    /// </summary>
    [SetUpFixture]
    public class TestWeaponCardHold
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => GameManager.ShortWeaponCardHold = true;

        [OneTimeTearDown]
        public void OneTimeTearDown() => GameManager.ShortWeaponCardHold = false;
    }
}
