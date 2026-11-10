namespace AnomalyArena
{
    public enum SkillId
    {
        FlameWake, Contagion, FrostNova, StormCall, Bulwark, Radiance, Devour,
        Blink, Shockwave, GravityWell, TimeSlip, Decoy, Overcharge,
        PocketVoid, VoidWalk, Singularity
    }

    /// <summary>Draft band of a normal skill (system doc §11.2 card-pool column). Greater Void skills never enter the draft.</summary>
    public enum SkillCategory { BasicElement, BasicMechanic, AdvancedElement, AdvancedMechanic }
}
