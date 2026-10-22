namespace AnomalyArena
{

    public static class ArenaTerms
    {

        static readonly string[] FormZh = { "Rifle", "Knife", "Rocket Launcher", "Grav Gun", "Scatter Cannon", "Phase Dagger", "Plasma Launcher", "Flamethrower", "Cryo Cannon", "Arc Coil", "Venom Launcher", "Railgun", "Minigun", "Swarm Launcher", "Orbit Blades", "Sentry Drone", "Shield Ring" };
        static readonly string[] FormEn = { "Rifle", "Knife", "Rocket Launcher", "Grav Gun", "Scatter Cannon", "Phase Dagger", "Plasma Launcher", "Flamethrower", "Cryo Cannon", "Arc Coil", "Venom Launcher", "Railgun", "Minigun", "Swarm Launcher", "Orbit Blades", "Sentry Drone", "Shield Ring" };
        static readonly string[] AnomalyZh =new string[34];
        static readonly string[] SkillZh = { "Flame Wake", "Contagion", "Frost Nova", "Storm Call", "Bulwark", "Radiance", "Devour", "Blink", "Shockwave", "Gravity Well", "Time Stop", "Mirror Image", "Overcharge", "Pocket Void", "Void Walk", "Singularity" };
        static readonly string[] SkillEn = { "Flame Wake", "Contagion", "Frost Nova", "Storm Call", "Bulwark", "Radiance", "Devour", "Blink", "Shockwave", "Gravity Well", "Time Stop", "Mirror Image", "Overcharge", "Pocket Void", "Void Walk", "Singularity" };
        static readonly string[] StatZh = { "Max HP", "Regeneration", "Move Speed", "Sprint Duration", "Damage", "Attack Speed", "Knockback", "Impact", "Pickup Range", "Protection", "Luck", "Max Energy", "Energy Siphon", "Element Penetration", "Crit" };
        static readonly string[] StatEn = { "Max HP", "Regeneration", "Move Speed", "Sprint Duration", "Damage", "Attack Speed", "Knockback", "Impact", "Pickup Range", "Protection", "Luck", "Max Energy", "Energy Siphon", "Element Penetration", "Crit" };

        public static string WeaponForm(AnomalyArena.WeaponForm form) => ArenaText.Pair(FormEn[(int)form],FormEn[(int)form]);
        public static string WeaponAnomaly(AnomalyArena.WeaponForm form, int variant) => ArenaText.Pair(WeaponRoster.AnomalyName(form, variant),WeaponRoster.AnomalyName(form, variant));
        public static string Skill(SkillId id) => ArenaText.Pair(SkillEn[(int)id],SkillEn[(int)id]);
        public static string Stat(StatId id) => (int)id < StatZh.Length ? ArenaText.Pair(StatEn[(int)id],StatEn[(int)id]) : id.ToString();
        static readonly string[] ExpandedTwistZh = {
            "Push enemies out by 3 when the pull ends; no extra damage.", "Repel only in the aimed 180-degree half circle.", "Push side targets toward the aim line; push centred targets forward.", "Focused pellets pierce the first target; later targets take 60%.",
            "Pierce the first body and swap with the second valid target; hit each once.", "Blast at departure too; overlapping blasts hit each enemy once.", "Click again after 0.3 s attached to detonate early.", "Stick to the wall after the third bounce, keeping the original fuse.",
            "Reflect the flame once at the first wall; hit each body once per shot.", "Push out by 3 when a fire patch ends; no extra damage.", "Frozen bodies can chain-shatter: at most 3 bursts, each body once.", "On release, the frost mist lingers ahead for 2 s and keeps freezing.",
            "Later jumps prefer the most Lightning stacks, then the nearest target.", "Knockback and thrown travel also charge: 1 per unit, cap 30.", "Cloud drifts 1 unit/s along its launch direction, blocked by walls and bounds.", "Needles bounce once off a wall; no body piercing.",
            "Pierce one interior wall, never bounds; body damage still decays.", "On a kill, fire one half-damage follow-up at the nearest target within 4; it cannot execute or chain another follow-up.", "Keep spin-up for 1 s after release; release still restores movement.", "Enemy blast becomes a forward 90-degree cone; self-damage and lockout remain.",
            "Wall hit leaves a 1 s mine; blast if an enemy enters 1.5, without making new mines.", "Reverse the six blasts from far to near; other values stay the same."
        };
        static readonly string[] ExpandedTwistEn = {
            "Push enemies out by 3 when the pull ends; no extra damage.", "Repel only in the aimed 180-degree half circle.", "Push side targets toward the aim line; push centred targets forward.", "Focused pellets pierce the first target; later targets take 60%.",
            "Pierce the first body and swap with the second valid target; hit each once.", "Blast at departure too; overlapping blasts hit each enemy once.", "Click again after 0.3 s attached to detonate early.", "Stick to the wall after the third bounce, keeping the original fuse.",
            "Reflect the flame once at the first wall; hit each body once per shot.", "Push out by 3 when a fire patch ends; no extra damage.", "Frozen bodies can chain-shatter: at most 3 bursts, each body once.", "On release, the frost mist lingers ahead for 2 s and keeps freezing.",
            "Later jumps prefer the most Lightning stacks, then the nearest target.", "Knockback and thrown travel also charge: 1 per unit, cap 30.", "Cloud drifts 1 unit/s along its launch direction, blocked by walls and bounds.", "Needles bounce once off a wall; no body piercing.",
            "Pierce one interior wall, never bounds; body damage still decays.", "On a kill, fire one half-damage follow-up at the nearest target within 4; it cannot execute or chain another follow-up.", "Keep spin-up for 1 s after release; release still restores movement.", "Enemy blast becomes a forward 90-degree cone; self-damage and lockout remain.",
            "Wall hit leaves a 1 s mine; blast if an enemy enters 1.5, without making new mines.", "Reverse the six blasts from far to near; other values stay the same."
        };
        static readonly string[] ExpandedTameZh = {
            "Your attraction field no longer pulls you.", "Remove your 2-unit recoil; orbs are still repelled.", "Reloading never jams.", "The release recoil becomes a push that never causes wall damage.",
            "Gain 0.5 s hit protection after a swap; no status clear or hole immunity.", "Recall the unused planted dagger after 6 s, without teleporting or exploding.", "Attached carriers no longer speed up; the blast can still hurt you.", "The blast spares your HP but still displaces you.",
            "The fuel-tank burst no longer sets you alight; the ring still burns enemies.", "Your own fire patches no longer burn you; other fire still can.", "Broken shards leave no ice patches.", "Spraying no longer slows you.",
            "Returning current no longer adds Lightning to you; it still shocks enemies around you.", "After 4 s full, drain 1 charge/s instead of self-blasting.", "Spore pods never burst under you, and their bursts spare you.", "Firing no longer costs you HP.",
            "Still move back by 4, using a push without wall-impact damage.", "Executions no longer lower your max HP.", "Full spin no longer limits turning or slows you.", "Overheat spares your HP; the 3 s lockout remains.",
            "Rockets never hurt you.", "Bombard blasts spare your HP but still displace you."
        };
        static readonly string[] ExpandedTameEn = {
            "Your attraction field no longer pulls you.", "Remove your 2-unit recoil; orbs are still repelled.", "Reloading never jams.", "The release recoil becomes a push that never causes wall damage.",
            "Gain 0.5 s hit protection after a swap; no status clear or hole immunity.", "Recall the unused planted dagger after 6 s, without teleporting or exploding.", "Attached carriers no longer speed up; the blast can still hurt you.", "The blast spares your HP but still displaces you.",
            "The fuel-tank burst no longer sets you alight; the ring still burns enemies.", "Your own fire patches no longer burn you; other fire still can.", "Broken shards leave no ice patches.", "Spraying no longer slows you.",
            "Returning current no longer adds Lightning to you; it still shocks enemies around you.", "After 4 s full, drain 1 charge/s instead of self-blasting.", "Spore pods never burst under you, and their bursts spare you.", "Firing no longer costs you HP.",
            "Still move back by 4, using a push without wall-impact damage.", "Executions no longer lower your max HP.", "Full spin no longer limits turning or slows you.", "Overheat spares your HP; the 3 s lockout remains.",
            "Rockets never hurt you.", "Bombard blasts spare your HP but still displace you."
        };
        public static string ExpandedUpgradeDescription(AnomalyArena.WeaponForm form,int variant,UpgradeAxis axis)
        {
            int index=((int)form-3)*2+(variant==0?0:1);
            if(index<0||index>=ExpandedTwistZh.Length)return "";
            return axis==UpgradeAxis.Twist?ArenaText.Pair(ExpandedTwistEn[index],ExpandedTwistEn[index]):axis==UpgradeAxis.Tame?ArenaText.Pair(ExpandedTameEn[index],ExpandedTameEn[index]):"";
        }
        public static string Upgrade(UpgradeAxis axis) => axis switch
        {
            UpgradeAxis.Infusion => ArenaText.Pair("Infusion","Infusion"),
            UpgradeAxis.Power => ArenaText.Pair("Power","Power"),
            UpgradeAxis.Tempo => ArenaText.Pair("Tempo","Tempo"),
            UpgradeAxis.Reach => ArenaText.Pair("Reach","Reach"),
            UpgradeAxis.Twist => ArenaText.Pair("Twist","Twist"),
            UpgradeAxis.Tame => ArenaText.Pair("Tame","Tame"),
            _ => axis.ToString()
        };
        public static string Element(ElementKind element) => element switch
        {
            ElementKind.None => ArenaText.Pair("None","None"),
            ElementKind.Fire => ArenaText.Pair("Fire","Fire"),
            ElementKind.Poison => ArenaText.Pair("Poison","Poison"),
            ElementKind.Frost => ArenaText.Pair("Frost","Frost"),
            ElementKind.Lightning => ArenaText.Pair("Lightning","Lightning"),
            ElementKind.Earth => ArenaText.Pair("Earth","Earth"),
            ElementKind.Light => ArenaText.Pair("Light","Light"),
            ElementKind.Void => ArenaText.Pair("Void","Void"),
            _ => element.ToString()
        };
        public static string CardKind(DraftCardKind kind) => kind switch
        {
            DraftCardKind.Stat => ArenaText.Pair("Stat","Stat"),
            DraftCardKind.Weapon => ArenaText.Pair("Weapon","Weapon"),
            _ => ArenaText.Pair("Skill","Skill")
        };
        public static string Rarity(CardRarity rarity) => rarity switch
        {
            CardRarity.Common => ArenaText.Pair("Common","Common"),
            CardRarity.Rare => ArenaText.Pair("Rare","Rare"),
            CardRarity.Exceptional => ArenaText.Pair("Exceptional","Exceptional"),
            CardRarity.Epic => ArenaText.Pair("Epic","Epic"),
            CardRarity.Legendary => ArenaText.Pair("Legendary","Legendary"),
            CardRarity.Void => ArenaText.Pair("Void","Void"),
            _ => ArenaText.Pair("Common","Common")
        };
    }
}
