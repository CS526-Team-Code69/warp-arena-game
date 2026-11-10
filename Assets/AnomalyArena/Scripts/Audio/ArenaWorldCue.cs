namespace AnomalyArena
{
    public enum ArenaWorldCue
    {
        NpcSwing, NpcWindup, NpcHit, NpcDeath, NpcSpit, NpcChargeWarning, NpcChargeRush,
        NpcShield, NpcSplit, NpcPulse, NpcShadeSpawn, NpcBloaterBlast,
        BossEnter, BossPhase, BossCleave, BossSlam, BossCharge, BossInhale, BossDeath,
        PlayerStepMetal, PlayerStepStone, PlayerSprint, PlayerHit, PlayerDeath,
        PortalOpen, PortalEnter, PortalClose, LevelUp, CardReveal, CardReroll, WaveEnd, Victory, Defeat,
        AmbientWreck, AmbientDust, AmbientRift, CombatMusic,
        // V5 (2026-10-21 document alignment): Chanter, Spire laser, Bloater radiation, Rammer stun, scatter jam, focus burst, knife return, Void Walk, Mirror echo, energy orb, trial return.
        NpcChant, NpcLaserLock, NpcLaserFire, NpcRadiation, PlayerStunned, WeaponJam, WeaponRepair, WeaponFocusCharge, WeaponFocusRelease, WeaponKnifeReturn,
        SkillVoidWalk, SkillMirrorEcho, PickupEnergy, TimeHeld
    }
}
