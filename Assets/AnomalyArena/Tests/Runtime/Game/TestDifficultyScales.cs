using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>
    /// The scene's difficulty scales (DifficultyScales) are tuning on top of the numbers the tests check, so for the whole run they read 1.
    /// A test of the scales sets DifficultyScales.Neutral to false itself and back to true in its TearDown.
    /// </summary>
    [SetUpFixture]
    public class TestDifficultyScales
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => DifficultyScales.Neutral = true;

        [OneTimeTearDown]
        public void OneTimeTearDown() => DifficultyScales.Neutral = false;
    }
}
