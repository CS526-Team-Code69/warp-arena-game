using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>
    /// The settings panel's choices (the keys, the volume) are saved in PlayerPrefs, which the editor keeps per product: every copy of
    /// the project on the machine shares them. The tests must neither depend on keys or a volume a player saved (the texts that name
    /// keys would differ) nor change them. For the whole run the choices are kept in memory instead (ArenaPrefs), starting empty: the
    /// default keys and full volume. A test that changes a key or the volume puts it back in its TearDown; it never reaches PlayerPrefs.
    /// </summary>
    [SetUpFixture]
    public class TestSavedSettings
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => Use(true);

        [OneTimeTearDown]
        public void OneTimeTearDown() => Use(false);

        static void Use(bool memory)
        {
            ArenaPrefs.UseMemoryForTests(memory);
            ArenaKeys.Reload();
            ArenaVolume.Reload();
        }
    }
}
