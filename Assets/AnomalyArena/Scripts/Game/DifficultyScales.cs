namespace AnomalyArena
{
    /// <summary>
    /// The difficulty scales held by the scene (WaveManager.enemyDamageScale, RunWeapons.weaponDamageScale and selfDamageScale) are
    /// tuning on top of the game's own numbers. The tests are written for those own numbers, so while they run the scales read 1
    /// (Neutral, set by the tests' set-up fixture); a test of the scales themselves turns Neutral off for its own run.
    /// </summary>
    public static class DifficultyScales
    {
        /// <summary>While true every difficulty scale reads 1.</summary>
        public static bool Neutral;

        /// <summary>The scale as the game uses it: 1 while Neutral, otherwise the value (never below 0).</summary>
        public static float Read(float value) => Neutral ? 1f : value < 0f ? 0f : value;
    }
}
