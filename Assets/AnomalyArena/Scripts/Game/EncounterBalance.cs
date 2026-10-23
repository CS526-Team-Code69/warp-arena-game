using UnityEngine;

namespace AnomalyArena
{

    [CreateAssetMenu(menuName = "Anomaly Arena/Waves/TTK Balance")]
    public sealed class EncounterBalance : ScriptableObject
    {
        [Tooltip(
            "Estimated effective single-target DPS of an ordinary build per wave (movement, accuracy, skills and auto weapons included). Not measured. 14 ordinary waves; index 14 is the final boss wave 15.")]
        public float[] referenceDps = { 7.5f, 8.5f, 10f, 12f, 14f, 16f, 18f, 20f, 23f, 26f, 28f, 31f, 34f, 37f, 40f };

        [Tooltip(
            "Continuous-fire budget for melee swarm enemies; discrete one/two-hit thresholds are checked separately.")]
        public float fodderSeconds = .65f;

        [Tooltip(
            "Seconds of reference DPS a boss takes to kill, shields included (system doc §10.3: one to two minutes).")]
        public float bossSeconds = 90f;

        [Min(1), Tooltip(
            "The Nash Prophet's HP is the same whenever the trial is entered: budgeted against this wave's reference DPS, so an early trial is a hard fight and a late one an easier one (wave 8: about 2 min at wave 5, 1 min at wave 12).")]
        public int prophetBudgetWave = 8;

        public StatusRules statusRules;
        public float ShieldDirectFactor => statusRules ? Mathf.Max(.01f, statusRules.shieldDirectFactor) : .7f;

        [Range(0f, 1f),
         Tooltip(
             "Tyrant shield at each phase change as a share of max HP; twice per fight, counted in the boss budget.")]
        public float tyrantPhaseShieldShare = .1f;

        [Tooltip(
            "EnemyKind order (14 kinds, Chanter last). Seconds to kill an ordinary non-swarm enemy: fragile ranged versus durable heavy.")]
        public float[] functionSeconds =
            { .65f, 2.6f, .65f, 1.8f, 2.4f, 1.6f, 2.3f, 2f, 3f, 2.8f, 1.4f, 60f, 60f, 2.3f };

        [Tooltip("EnemyKind order (14 kinds, Chanter last). Seconds to kill an elite (an affixed special).")]
        public float[] eliteSeconds = { 7.5f, 8f, 7.5f, 6f, 7.5f, 6.5f, 7.5f, 7f, 8f, 8f, 6f, 60f, 60f, 7.5f };

        [Range(0f, 1f), Tooltip("Max HP of a mini Splitter as a share of a full Splitter of the same wave.")]
        public float splitterMiniHpShare = .4f;

        public float DpsAt(int wave) => referenceDps == null || referenceDps.Length == 0
            ? 7.5f
            : Mathf.Max(1f, referenceDps[Mathf.Clamp(wave - 1, 0, referenceDps.Length - 1)]);

        public float TargetSeconds(EnemyDefinition enemy, bool elite)
        {
            if (EnemyRoles.IsBoss(enemy.kind)) return Mathf.Max(1f, bossSeconds);
            if (!elite && EnemyRoles.IsMelee(enemy.kind))
                return Mathf.Max(.1f, fodderSeconds);                                               
            var values = elite ? eliteSeconds : functionSeconds;
            return values != null && (int)enemy.kind < values.Length ? Mathf.Max(.1f, values[(int)enemy.kind]) :
                elite ? 7.5f : 2f;
        }

        public static float InitialShieldShare(EnemyDefinition enemy, ElementKind affix) =>
            enemy.canAffix && affix == ElementKind.Earth ? .5f : enemy.startShieldShare;

        public float ShieldBudgetShare(EnemyDefinition enemy, ElementKind affix) => InitialShieldShare(enemy, affix)
            + (enemy.kind == EnemyKind.Tyrant ? 2f * tyrantPhaseShieldShare : 0f);

        public float MiniShare(bool mini) => mini ? Mathf.Clamp01(splitterMiniHpShare) : 1f;

        public int BudgetWave(EnemyDefinition enemy, int wave) => enemy.kind switch
        {
            EnemyKind.Tyrant when referenceDps is { Length: > 0 } => referenceDps.Length,
            EnemyKind.Hollow => Mathf.Max(1, prophetBudgetWave),
            _ => wave
        };

        public float MaxHp(EnemyDefinition enemy, int wave, bool elite, ElementKind affix = ElementKind.None,
            bool mini = false) =>
            Mathf.Max(1f,
                Mathf.Round(DpsAt(BudgetWave(enemy, wave)) * TargetSeconds(enemy, elite) * MiniShare(mini) /
                            (1f + ShieldBudgetShare(enemy, affix) / ShieldDirectFactor)));

        public float EffectiveHp(EnemyDefinition enemy, int wave, bool elite, ElementKind affix = ElementKind.None,
            bool mini = false) =>
            MaxHp(enemy, wave, elite, affix, mini) * (1f + ShieldBudgetShare(enemy, affix) / ShieldDirectFactor);
    }
}