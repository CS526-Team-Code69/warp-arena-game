namespace AnomalyArena
{

    public static class ArenaTerms
    {

        static readonly string[] FormZh = { "Rifle", "Knife", "Rocket Launcher", "Grav Gun", "Scatter Cannon", "Phase Dagger", "Plasma Launcher", "Flamethrower", "Cryo Cannon", "Arc Cannon", "Venom Launcher", "Railgun", "Gatling", "Swarm Launcher", "Orbit Blades", "Sentry Drone", "Shield Ring" };
        static readonly string[] FormEn = { "Rifle", "Knife", "Rocket Launcher", "Grav Gun", "Scatter Cannon", "Phase Dagger", "Plasma Launcher", "Flamethrower", "Cryo Cannon", "Arc Cannon", "Venom Launcher", "Railgun", "Gatling", "Swarm Launcher", "Orbit Blades", "Sentry Drone", "Shield Ring" };
        static readonly string[] AnomalyZh =new string[34];
        static readonly string[] SkillZh = { "Flame Wake", "Contagion", "Frost Nova", "Storm Call", "Bulwark", "Radiance", "Devour", "Blink", "Shockwave", "Gravity Well", "Time Stop", "Mirror Image", "Overcharge", "Pocket Void", "Void Walk", "Singularity" };
        static readonly string[] SkillEn = { "Flame Wake", "Contagion", "Frost Nova", "Storm Call", "Bulwark", "Radiance", "Devour", "Blink", "Shockwave", "Gravity Well", "Time Stop", "Mirror Image", "Overcharge", "Pocket Void", "Void Walk", "Singularity" };
        static readonly string[] StatZh = { "Max HP", "Regeneration", "Move Speed", "Sprint Duration", "Damage", "Attack Speed", "Knockback", "Impact", "Pickup Range", "Protection", "Luck", "Max Energy", "Energy Siphon", "Damage Penetration", "Crit" };
        static readonly string[] StatEn = { "Max HP", "Regeneration", "Move Speed", "Sprint Duration", "Damage", "Attack Speed", "Knockback", "Impact", "Pickup Range", "Protection", "Luck", "Max Energy", "Energy Siphon", "Damage Penetration", "Crit" };

        public static string WeaponForm(AnomalyArena.WeaponForm form) => ArenaText.Pair(FormEn[(int)form],FormEn[(int)form]);
        public static string WeaponAnomaly(AnomalyArena.WeaponForm form, int variant) => ArenaText.Pair(WeaponRoster.AnomalyName(form, variant),WeaponRoster.AnomalyName(form, variant));
        public static string Skill(SkillId id) => ArenaText.Pair(SkillEn[(int)id],SkillEn[(int)id]);
        public static string Stat(StatId id) => (int)id < StatZh.Length ? ArenaText.Pair(StatEn[(int)id],StatEn[(int)id]) : id.ToString();
        static readonly string[] ExpandedTwistZh = {
            "Push enemies out by 3 when the pull ends; no extra damage.", "Repel only in the aimed 180-degree half circle.", "The second round of each clip pulls in enemies within {vacuumRange} cells ahead, then blasts them for {vacuumShare}% damage.", "Each pellet passes through the first enemy; the second takes {focusPierce}% damage.",
            "Pierce the first body and swap with the second valid target; hit each once.", "Blast at departure too; overlapping blasts hit each enemy once.", "Click again after 0.3 s attached to detonate early.", "Stick to the wall after the third bounce, keeping the original fuse.",
            "While spraying, the flame burns enemy projectiles in its cone (spikes, boss shots); no extra damage.", "Two fire patches within {fenceRange} with no wall between form a fence: an enemy touching it gains {fenceFire} Burning per second, you {fenceSelfFire}.", "A shard knocks a frozen enemy {throwDist} away (not bosses or Spires); when it stops it shatters, taking the shard with its frozen bonus, and others within {burstR} take {splash}% of a shard.", "Release after holding {minHold} s or more: frozen enemies in the cone thaw at once, taking {shatterPerSec} per second frozen under the spray, {shatterCap} at most (scaled like the weapon).",
            "The first target is unchanged; each later jump goes to the enemy nearest your aim point within 4 of the last target, never through walls.", "A discharge also strikes the path you walked in the last {trailSec} s: enemies within {trailWidth} of it take the same damage, once each.", "While you stand in your landed cloud, it moves toward you at {cloudFollow} cells/s, blocked by walls and bounds.", "An enemy hit by {needleCount} needles within {needleWindow} s bursts: other enemies within {needleR} take {needleShare}x needle damage; it does not.",
            "The slug's path stays for {lanceSec} s; where the next slug crosses it, a blast: radius {lanceR}, {lanceShare}% base damage, ignores Shield.", "For {markSec} s, any damage that drops an enemy you hit under the execute line executes it (not bosses; up to {markMax}).", "Switch at full spin ({turretSpin} s) with fire held: the Gatling stays and fires the same way for {turretSec} s; switching back recalls it.", "Each enemy your bullets kill removes {vent}% heat; an elite kill clears it (not bosses).",
            "Rockets stick to enemies and merge into one blast after {hiveFuse} s: damage adds up, +{hiveR} radius per extra rocket.", "Each of the {bombardBlasts} blasts lands along your aim at that moment; sweep the mouse to bend the line into an arc."
        };
        static readonly string[] ExpandedTwistEn = {
            "Push enemies out by 3 when the pull ends; no extra damage.", "Repel only in the aimed 180-degree half circle.", "The second round of each clip pulls in enemies within {vacuumRange} cells ahead, then blasts them for {vacuumShare}% damage.", "Each pellet passes through the first enemy; the second takes {focusPierce}% damage.",
            "Pierce the first body and swap with the second valid target; hit each once.", "Blast at departure too; overlapping blasts hit each enemy once.", "Click again after 0.3 s attached to detonate early.", "Stick to the wall after the third bounce, keeping the original fuse.",
            "While spraying, the flame burns enemy projectiles in its cone (spikes, boss shots); no extra damage.", "Two fire patches within {fenceRange} with no wall between form a fence: an enemy touching it gains {fenceFire} Burning per second, you {fenceSelfFire}.", "A shard knocks a frozen enemy {throwDist} away (not bosses or Spires); when it stops it shatters, taking the shard with its frozen bonus, and others within {burstR} take {splash}% of a shard.", "Release after holding {minHold} s or more: frozen enemies in the cone thaw at once, taking {shatterPerSec} per second frozen under the spray, {shatterCap} at most (scaled like the weapon).",
            "The first target is unchanged; each later jump goes to the enemy nearest your aim point within 4 of the last target, never through walls.", "A discharge also strikes the path you walked in the last {trailSec} s: enemies within {trailWidth} of it take the same damage, once each.", "While you stand in your landed cloud, it moves toward you at {cloudFollow} cells/s, blocked by walls and bounds.", "An enemy hit by {needleCount} needles within {needleWindow} s bursts: other enemies within {needleR} take {needleShare}x needle damage; it does not.",
            "The slug's path stays for {lanceSec} s; where the next slug crosses it, a blast: radius {lanceR}, {lanceShare}% base damage, ignores Shield.", "For {markSec} s, any damage that drops an enemy you hit under the execute line executes it (not bosses; up to {markMax}).", "Switch at full spin ({turretSpin} s) with fire held: the Gatling stays and fires the same way for {turretSec} s; switching back recalls it.", "Each enemy your bullets kill removes {vent}% heat; an elite kill clears it (not bosses).",
            "Rockets stick to enemies and merge into one blast after {hiveFuse} s: damage adds up, +{hiveR} radius per extra rocket.", "Each of the {bombardBlasts} blasts lands along your aim at that moment; sweep the mouse to bend the line into an arc."
        };
        static readonly string[] ExpandedTameZh = {
            "Your attraction field no longer pulls you.", "Remove your 2-unit recoil; orbs are still repelled.", "Reloads never jam; press fire at {quickStart}–{quickEnd}% of a reload to finish it and fire at once.", "Recoil causes no wall damage and makes you immune to attacks for {guardMin}–{guardMax} s (by charge).",
            "Gain 0.5 s hit protection after a swap; no status clear or hole immunity.", "Recall the unused planted dagger after 6 s, without teleporting or exploding.", "Attached carriers no longer speed up; the blast can still hurt you.", "The blast spares your HP but still displaces you.",
            "The fuel-tank burst no longer sets you alight and knocks enemies within {tankR} back by {tankPush} (a real knockback; bosses are not knocked back).", "Your own patches and fences no longer burn you (ones already down too); while your centre is in one, you move x{wildfireSpeed}.", "Ice patches no longer chill you; they add {iceFrost} Frost per second to enemies on them instead; {iceCap} per weapon at most.", "Spraying no longer slows you; enemies within {auraR} of you move at x{auraSlow} instead.",
            "Unused jumps no longer return through you; they are stored for the next discharge, up to +{chainStore}.", "Held full in your hand for 4 s, it discharges by itself at full strength and spares you.", "Stepping on a spore pod kicks it {kick} away, where it bursts; pod mists never poison you.", "Firing no longer costs HP; every {needlerEvery} needles give you {needlerShield} Shield instead.",
            "Wall within {braceDist} behind you: no recoil, +{braceBonus}% damage; no wall: slide back 4 without losing control.", "Executions add {tameHp} max HP and heal {tameHp} instead of lowering it (up to +{tameHpCap} from this gun).", "Turning is free; after {anchorSec} s of firing, knockback on you drops to {anchorKnock}% and black holes cannot drag you.", "Meltdown no longer hurts you; your attack speed rises {meltHaste}% for {meltHasteSec} s instead. Still a 3 s lockout.",
            "Rockets never hurt you; backward rockets fly at the nearest visible enemy within {rearRange} behind you.", "Your own blasts no longer hurt or push you, and touching one cuts that shot's cooldown by {bombardCut}%."
        };
        static readonly string[] ExpandedTameEn = {
            "Your attraction field no longer pulls you.", "Remove your 2-unit recoil; orbs are still repelled.", "Reloads never jam; press fire at {quickStart}–{quickEnd}% of a reload to finish it and fire at once.", "Recoil causes no wall damage and makes you immune to attacks for {guardMin}–{guardMax} s (by charge).",
            "Gain 0.5 s hit protection after a swap; no status clear or hole immunity.", "Recall the unused planted dagger after 6 s, without teleporting or exploding.", "Attached carriers no longer speed up; the blast can still hurt you.", "The blast spares your HP but still displaces you.",
            "The fuel-tank burst no longer sets you alight and knocks enemies within {tankR} back by {tankPush} (a real knockback; bosses are not knocked back).", "Your own patches and fences no longer burn you (ones already down too); while your centre is in one, you move x{wildfireSpeed}.", "Ice patches no longer chill you; they add {iceFrost} Frost per second to enemies on them instead; {iceCap} per weapon at most.", "Spraying no longer slows you; enemies within {auraR} of you move at x{auraSlow} instead.",
            "Unused jumps no longer return through you; they are stored for the next discharge, up to +{chainStore}.", "Held full in your hand for 4 s, it discharges by itself at full strength and spares you.", "Stepping on a spore pod kicks it {kick} away, where it bursts; pod mists never poison you.", "Firing no longer costs HP; every {needlerEvery} needles give you {needlerShield} Shield instead.",
            "Wall within {braceDist} behind you: no recoil, +{braceBonus}% damage; no wall: slide back 4 without losing control.", "Executions add {tameHp} max HP and heal {tameHp} instead of lowering it (up to +{tameHpCap} from this gun).", "Turning is free; after {anchorSec} s of firing, knockback on you drops to {anchorKnock}% and black holes cannot drag you.", "Meltdown no longer hurts you; your attack speed rises {meltHaste}% for {meltHasteSec} s instead. Still a 3 s lockout.",
            "Rockets never hurt you; backward rockets fly at the nearest visible enemy within {rearRange} behind you.", "Your own blasts no longer hurt or push you, and touching one cuts that shot's cooldown by {bombardCut}%."
        };
        public static string ExpandedUpgradeDescription(AnomalyArena.WeaponForm form,int variant,UpgradeAxis axis)
        {
            int index=((int)form-3)*2+(variant==0?0:1);
            if(index<0||index>=ExpandedTwistZh.Length)return "";
            return Fill(axis==UpgradeAxis.Twist?ArenaText.Pair(ExpandedTwistEn[index],ExpandedTwistEn[index]):axis==UpgradeAxis.Tame?ArenaText.Pair(ExpandedTameEn[index],ExpandedTameEn[index]):"");
        }

        static string Fill(string text)
        {
            if(string.IsNullOrEmpty(text)||text.IndexOf('{')<0)return text;
            var weapons=GameManager.Instance?GameManager.Instance.runWeapons:null;
            var u=weapons?weapons.upgrades:new WeaponUpgradeTuning();
            string P(float share)=>(share*100f).ToString("0.#");
            string N(float value)=>value.ToString("0.##");
            var tokens=new (string key,string value)[]
            {

                ("{vacuumRange}",N(u.scatterVacuumRange)),("{vacuumShare}",P(u.scatterVacuumShare)),("{focusPierce}",P(u.focusPierceRetention)),
                ("{quickStart}",P(u.scatterQuickStart)),("{quickEnd}",P(u.scatterQuickEnd)),("{guardMin}",N(u.focusGuardMin)),("{guardMax}",N(u.focusGuardMax)),

                ("{fenceRange}",N(u.wildfireFenceRange)),("{fenceFire}",N(u.wildfireFenceFirePerSecond)),("{fenceSelfFire}",N(u.wildfireFenceSelfFirePerSecond)),
                ("{tankR}",N(u.fuelTankRadius)),("{tankPush}",N(u.fuelTankTamePush)),("{wildfireSpeed}",N(u.wildfireTameSpeed)),
                ("{throwDist}",N(u.shatterThrowDistance)),("{burstR}",N(u.shatterBurstRadius)),("{splash}",P(u.shatterSplashShare)),
                ("{iceFrost}",N(u.iceFrostPerSecond)),("{iceCap}",u.icePatchTamedCap.ToString()),
                ("{minHold}",N(u.blizzardShatterMinHold)),("{shatterPerSec}",N(u.blizzardShatterPerSecond)),("{shatterCap}",N(u.blizzardShatterCap)),
                ("{auraR}",N(u.blizzardAuraRadius)),("{auraSlow}",N(u.blizzardSelfSlow)),("{chainStore}",u.chainStoreMax.ToString()),
                ("{trailSec}",N(u.capacitorTrailSeconds)),("{trailWidth}",N(u.capacitorTrailWidth)),("{cloudFollow}",N(u.cloudFollowSpeed)),("{kick}",N(u.sporeKickDistance)),
                ("{needleCount}",u.needleBurstCount.ToString()),("{needleWindow}",N(u.needleBurstWindow)),("{needleR}",N(u.needleBurstRadius)),("{needleShare}",N(u.needleBurstShare)),
                ("{needlerEvery}",u.needlerHpPerNeedles.ToString()),("{needlerShield}",N(u.needlerTameShield)),

                ("{lanceSec}",N(u.lanceTrailSeconds)),("{lanceR}",N(u.lanceCrossRadius)),("{lanceShare}",P(u.lanceCrossShare)),
                ("{braceDist}",N(u.lanceBraceDistance)),("{braceBonus}",P(u.lanceBraceDamage-1f)),
                ("{markSec}",N(u.executeMarkSeconds)),("{markMax}",u.executeMarkMax.ToString()),("{tameHp}",N(u.executeTameHpGain)),("{tameHpCap}",N(u.executeTameHpCap)),
                ("{turretSpin}",N(u.turretSpinSeconds)),("{turretSec}",N(u.turretSeconds)),("{anchorSec}",N(u.spinAnchorSpinSeconds)),("{anchorKnock}",P(u.spinAnchorKnockFactor)),
                ("{vent}",P(u.meltdownKillVent/4f)),("{meltHaste}",P(u.meltdownTameHaste)),("{meltHasteSec}",N(u.meltdownTameHasteSeconds)),
                ("{hiveFuse}",N(u.swarmHiveFuse)),("{hiveR}",N(u.swarmHivePerRocketRadius)),("{rearRange}",N(u.barrageRearRange)),
                ("{bombardBlasts}",System.Math.Max(1,u.bombardBlasts).ToString()),("{bombardCut}",P(u.bombardTameCooldownCut)),
            };
            foreach(var (key,value) in tokens)if(text.Contains(key))text=text.Replace(key,value);
            return text;
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
