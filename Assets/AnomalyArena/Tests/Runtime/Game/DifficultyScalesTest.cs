using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>The difficulty scales read their value in play and 1 while the tests keep them neutral; a negative value reads 0.</summary>
    [TestFixture]
    public class DifficultyScalesTest
    {
        [TearDown]
        public void Restore() => DifficultyScales.Neutral = true;

        [TestCase(.8f, false, .8f)]
        [TestCase(1.25f, false, 1.25f)]
        [TestCase(-1f, false, 0f)]
        [TestCase(.8f, true, 1f)]
        public void Read_ValueAndNeutral_IsTheValueOrOne(float value, bool neutral, float expected)
        {
            DifficultyScales.Neutral = neutral;

            float actual = DifficultyScales.Read(value);

            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
