using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Waves/TTK Balance")]
    public sealed class EncounterBalance : ScriptableObject
    {
        [Tooltip("Estimated effective single-target DPS for an ordinary same-stage build, including movement, accuracy, skills and automatic weapons. Not measured telemetry.")]
        public float[] referenceDps = { 7.5f,8.5f,10f,12f,14f,16f,18f,20f,23f,26f,28f,31f,34f,37f,40f };
        [Tooltip("Continuous-DPS fodder budget; discrete attack tests cover one-hit and two-hit thresholds.")]
        public float fodderSeconds = .65f;
        public float bossSeconds = 60f;
        public StatusRules statusRules;
        public float ShieldDirectFactor => statusRules ? Mathf.Max(.01f,statusRules.shieldDirectFactor) : .7f;
        [Range(0f,1f), Tooltip("Shield share of max HP for each Tyrant phase; two grants are included in the boss budget.")]
        public float tyrantPhaseShieldShare = .1f;
        [Tooltip("EnemyKind order. Function-enemy target seconds preserve fragile ranged and durable heavy roles.")]
        public float[] functionSeconds = { .65f,2.6f,.65f,1.8f,2.4f,1.6f,2.3f,2f,3f,2.8f,1.4f,60f,60f };
        public float[] eliteSeconds = { 7.5f,8f,7.5f,6f,7.5f,6.5f,7.5f,7f,8f,8f,6f,60f,60f };

        public float DpsAt(int wave) => referenceDps == null || referenceDps.Length == 0 ? 7.5f
            : Mathf.Max(1f, referenceDps[Mathf.Clamp(wave-1,0,referenceDps.Length-1)]);
        public float TargetSeconds(EnemyDefinition enemy, bool elite)
        {
            if (enemy.kind is EnemyKind.Hollow or EnemyKind.Tyrant) return Mathf.Max(1f,bossSeconds);
            if (!elite && enemy.kind is EnemyKind.Grunt or EnemyKind.Skitter) return Mathf.Max(.1f,fodderSeconds);
            var values = elite ? eliteSeconds : functionSeconds;
            return values != null && (int)enemy.kind < values.Length ? Mathf.Max(.1f,values[(int)enemy.kind]) : elite ? 7.5f : 2f;
        }
        public static float InitialShieldShare(EnemyDefinition enemy, ElementKind affix) =>
            enemy.canAffix && affix == ElementKind.Earth ? .5f : enemy.startShieldShare;
        public float ShieldBudgetShare(EnemyDefinition enemy, ElementKind affix) => InitialShieldShare(enemy,affix)
            + (enemy.kind == EnemyKind.Tyrant ? 2f*tyrantPhaseShieldShare : 0f);
        public float MaxHp(EnemyDefinition enemy, int wave, bool elite, ElementKind affix = ElementKind.None) =>
            Mathf.Max(1f, Mathf.Round(DpsAt(wave)*TargetSeconds(enemy,elite)/(1f+ShieldBudgetShare(enemy,affix)/ShieldDirectFactor)));
        public float EffectiveHp(EnemyDefinition enemy, int wave, bool elite, ElementKind affix = ElementKind.None) =>
            MaxHp(enemy,wave,elite,affix)*(1f+ShieldBudgetShare(enemy,affix)/ShieldDirectFactor);
    }
}
