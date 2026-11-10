using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>An enemy's HP and shield shares as the V2 day campaign budgets them (Enemy.CampaignBudget).</summary>
    public readonly struct EnemyBudget
    {
        public readonly float maxHp, startShieldShare, phaseShieldShare;
        public EnemyBudget(float maxHp, float startShieldShare, float phaseShieldShare)
        {
            this.maxHp = maxHp; this.startShieldShare = startShieldShare; this.phaseShieldShare = phaseShieldShare;
        }
    }

    // The V2 day campaign's own HP for every body it meets (since 2026-11-10): its reference DPS by day and its two bosses' fixed HP and
    // shields (CampaignRules.Days.cs), so the open mode's EncounterBalance.asset and Tyrant.asset stay as they are.
    public partial class Enemy
    {
        /// <summary>V2 day campaign (since 2026-11-10): the HP and shield shares of a body being configured (its definition, elite, affix,
        /// mini Splitter); a null hook or a null result keeps the encounter balance's, as in the classic run.</summary>
        public static Func<EnemyDefinition, bool, ElementKind, bool, EnemyBudget?> CampaignBudget;

        // The campaign's shield shares for this body; below 0 when no campaign budgets it (the definition and the encounter balance rule).
        float budgetStartShield = -1f, budgetPhaseShield = -1f;

        /// <summary>Configure, after the encounter balance's HP: the campaign's budget for this body, when a campaign gives one.</summary>
        void ApplyCampaignBudget(EnemyDefinition data, bool elite, bool mini)
        {
            budgetStartShield = budgetPhaseShield = -1f;
            if (CampaignBudget?.Invoke(data, elite, Affix, mini) is not { } budget) return;
            maxHp = Mathf.Max(1f, budget.maxHp);
            budgetStartShield = Mathf.Max(0f, budget.startShieldShare);
            budgetPhaseShield = Mathf.Max(0f, budget.phaseShieldShare);
        }

        /// <summary>The shield a body is born with, as a share of its max HP: the campaign's, or an Earth affix's half, or its definition's.</summary>
        float StartShieldShare(EnemyDefinition data) => budgetStartShield >= 0f ? budgetStartShield : Affix == ElementKind.Earth ? .5f : data.startShieldShare;
    }
}
