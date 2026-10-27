namespace AnomalyArena
{

    public static class ArenaTerms
    {

        static readonly string[] FormZh = { "Rifle", "Knife", "Rocket Launcher", "Grav Gun", "Scatter Cannon", "Phase Dagger", "Plasma Launcher", "Flamethrower", "Cryo Cannon", "Arc Cannon", "Venom Launcher", "Railgun", "Gatling", "Swarm Launcher", "Orbit Blades", "Sentry Drone", "Energy Ring" };
        static readonly string[] FormEn = { "Rifle", "Knife", "Rocket Launcher", "Grav Gun", "Scatter Cannon", "Phase Dagger", "Plasma Launcher", "Flamethrower", "Cryo Cannon", "Arc Cannon", "Venom Launcher", "Railgun", "Gatling", "Swarm Launcher", "Orbit Blades", "Sentry Drone", "Energy Ring" };
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
            "While spraying, the flame burns enemy projectiles in its cone (spikes, boss shots); no extra damage.", "Two fire patches within {fenceRange} with no wall between form a fence: an enemy touching it burns for {fenceBurn} per second (more in later waves); you gain {fenceSelfFire} Burning per second.", "A shard knocks a frozen enemy {throwDist} away (not bosses or Spires); when it stops it shatters, taking the shard with its frozen bonus, and others within {burstR} take {splash}% of a shard.", "Release after holding {minHold} s or more: frozen enemies in the cone thaw at once, taking {shatterPerSec} per second frozen under the spray, {shatterCap} at most (scaled like the weapon).",
            "The first target is unchanged; each later jump goes to the enemy nearest your aim point within 4 of the last target, never through walls.", "A discharge also strikes the path you walked in the last {trailSec} s: enemies within {trailWidth} of it take the same damage, once each.", "A spore pod bursting inside your cloud detonates it: enemies within {cloudBlastR}x its radius take {cloudBlastTicks} ticks of its damage and fill up on Poison (you are spared); the cloud's time resets to {cloudSec} s.","An enemy hit by {needleCount} needles within {needleWindow} s bursts: other enemies within {needleR} take {needleShare}x needle damage; it does not.",
            "The slug's path stays for {lanceSec} s; where the next slug crosses it, a blast: radius {lanceR}, {lanceShare}% base damage, ignores the Shield's damage cut.", "For {markSec} s, any damage that drops an enemy you hit under the execute line executes it (not bosses; up to {markMax}).", "Switch at full spin ({turretSpin} s) with fire held: the Gatling stays and fires the same way for {turretSec} s; switching back recalls it.", "Each enemy your bullets kill removes {vent}% heat; an elite kill clears it (not bosses).",
            "Rockets stick to enemies and merge into one blast after {hiveFuse} s: damage adds up, +{hiveR} radius per extra rocket.", "Each of the {bombardBlasts} blasts lands along your aim at that moment; sweep the mouse to bend the line into an arc."
        };
        static readonly string[] ExpandedTwistEn = {
            "Push enemies out by 3 when the pull ends; no extra damage.", "Repel only in the aimed 180-degree half circle.", "The second round of each clip pulls in enemies within {vacuumRange} cells ahead, then blasts them for {vacuumShare}% damage.", "Each pellet passes through the first enemy; the second takes {focusPierce}% damage.",
            "Pierce the first body and swap with the second valid target; hit each once.", "Blast at departure too; overlapping blasts hit each enemy once.", "Click again after 0.3 s attached to detonate early.", "Stick to the wall after the third bounce, keeping the original fuse.",
            "While spraying, the flame burns enemy projectiles in its cone (spikes, boss shots); no extra damage.", "Two fire patches within {fenceRange} with no wall between form a fence: an enemy touching it burns for {fenceBurn} per second (more in later waves); you gain {fenceSelfFire} Burning per second.", "A shard knocks a frozen enemy {throwDist} away (not bosses or Spires); when it stops it shatters, taking the shard with its frozen bonus, and others within {burstR} take {splash}% of a shard.", "Release after holding {minHold} s or more: frozen enemies in the cone thaw at once, taking {shatterPerSec} per second frozen under the spray, {shatterCap} at most (scaled like the weapon).",
            "The first target is unchanged; each later jump goes to the enemy nearest your aim point within 4 of the last target, never through walls.", "A discharge also strikes the path you walked in the last {trailSec} s: enemies within {trailWidth} of it take the same damage, once each.", "A spore pod bursting inside your cloud detonates it: enemies within {cloudBlastR}x its radius take {cloudBlastTicks} ticks of its damage and fill up on Poison (you are spared); the cloud's time resets to {cloudSec} s.","An enemy hit by {needleCount} needles within {needleWindow} s bursts: other enemies within {needleR} take {needleShare}x needle damage; it does not.",
            "The slug's path stays for {lanceSec} s; where the next slug crosses it, a blast: radius {lanceR}, {lanceShare}% base damage, ignores the Shield's damage cut.", "For {markSec} s, any damage that drops an enemy you hit under the execute line executes it (not bosses; up to {markMax}).", "Switch at full spin ({turretSpin} s) with fire held: the Gatling stays and fires the same way for {turretSec} s; switching back recalls it.", "Each enemy your bullets kill removes {vent}% heat; an elite kill clears it (not bosses).",
            "Rockets stick to enemies and merge into one blast after {hiveFuse} s: damage adds up, +{hiveR} radius per extra rocket.", "Each of the {bombardBlasts} blasts lands along your aim at that moment; sweep the mouse to bend the line into an arc."
        };
        static readonly string[] ExpandedTameZh = {
            "Your attraction field no longer pulls you.", "Remove your 2-unit recoil; orbs are still repelled.", "Shell fires grow x{shellTameR} and no longer burn you; step on a burning shell while reloading to finish the reload at once and move x{shellSpeed} for {shellSpeedSec} s.", "The recoil pushes you only {focusTameRecoil}% as far and makes you immune to attacks for {guardMin}–{guardMax} s (by charge).",
            "Gain 0.5 s hit protection after a swap; no status clear or hole immunity.", "Recall the unused planted dagger after 6 s, without teleporting or exploding.", "Attached carriers no longer speed up; the blast can still hurt you.", "The blast spares your HP but still displaces you.",
            "The fuel-tank burst no longer sets you alight and knocks enemies within {tankR} back by {tankPush} (a real knockback; bosses are not knocked back).", "Your own patches and fences no longer burn you (ones already down too); while your centre is in one, you move x{wildfireSpeed}.", "You no longer slip on your own ice and move x{iceSpeed} on it; enemies on it still slip and gain {iceFrost} Frost per second until they freeze.", "Your snow no longer stacks Frost on you, and you take {snowCut}% less damage standing in it; enemies in it move at x{snowSlow} and gain {snowFrost} more Frost per second.",
            "Unused jumps no longer return through you; they are stored for the next discharge, up to +{chainStore}. While some are stored and it is in your hand, you move {chainSpeed}% faster.", "Held full in your hand for 4 s, it discharges by itself at full strength and spares you.", "Stepping on a spore pod kicks it {kick} away, where it bursts; pod mists never poison you.", "Your reload mist no longer hurts you and lasts {mistTameSec} s; while you touch it you reload {mistReload}x as fast.",
            "Wall within {braceDist} behind you: no recoil, +{braceBonus}% damage; no wall: the recoil pushes you only {lanceTameRecoil} (not {lanceRecoil}). The wind-up still slows you.", "A shot that kills nothing no longer shocks you or forces a long cooldown: an arc shield circles you for {arcShieldSec} s, shocking enemies within {arcShieldR} every {arcShieldTick} s for {arcShieldShare}% of the shot. It blocks no damage.", "Turning is free; after {anchorSec} s of firing, backing into a wall braces you: no more backing off, and meanwhile knockback on you drops to {anchorKnock}%, black holes cannot drag you and the fire rate rises {braceRate}%.", "The overheat blast no longer hurts you and knocks nearby enemies back {meltPush}; for {meltHasteSec} s after, your attack speed rises {meltHaste}% and your move speed {meltMove}%, whichever weapon you hold. Still a 3 s lockout.",
            "No missile locks onto you: spares take turns on the locked enemies, or on the nearest visible enemy anywhere if none is in range; with no enemy at all they circle you for {escortSec} s and strike the first one in range.", "Your own blasts no longer hurt or push you, and touching one cuts that shot's cooldown by {bombardCut}%."
        };
        static readonly string[] ExpandedTameEn = {
            "Your attraction field no longer pulls you.", "Remove your 2-unit recoil; orbs are still repelled.", "Shell fires grow x{shellTameR} and no longer burn you; step on a burning shell while reloading to finish the reload at once and move x{shellSpeed} for {shellSpeedSec} s.", "The recoil pushes you only {focusTameRecoil}% as far and makes you immune to attacks for {guardMin}–{guardMax} s (by charge).",
            "Gain 0.5 s hit protection after a swap; no status clear or hole immunity.", "Recall the unused planted dagger after 6 s, without teleporting or exploding.", "Attached carriers no longer speed up; the blast can still hurt you.", "The blast spares your HP but still displaces you.",
            "The fuel-tank burst no longer sets you alight and knocks enemies within {tankR} back by {tankPush} (a real knockback; bosses are not knocked back).", "Your own patches and fences no longer burn you (ones already down too); while your centre is in one, you move x{wildfireSpeed}.", "You no longer slip on your own ice and move x{iceSpeed} on it; enemies on it still slip and gain {iceFrost} Frost per second until they freeze.", "Your snow no longer stacks Frost on you, and you take {snowCut}% less damage standing in it; enemies in it move at x{snowSlow} and gain {snowFrost} more Frost per second.",
            "Unused jumps no longer return through you; they are stored for the next discharge, up to +{chainStore}. While some are stored and it is in your hand, you move {chainSpeed}% faster.", "Held full in your hand for 4 s, it discharges by itself at full strength and spares you.", "Stepping on a spore pod kicks it {kick} away, where it bursts; pod mists never poison you.", "Your reload mist no longer hurts you and lasts {mistTameSec} s; while you touch it you reload {mistReload}x as fast.",
            "Wall within {braceDist} behind you: no recoil, +{braceBonus}% damage; no wall: the recoil pushes you only {lanceTameRecoil} (not {lanceRecoil}). The wind-up still slows you.", "A shot that kills nothing no longer shocks you or forces a long cooldown: an arc shield circles you for {arcShieldSec} s, shocking enemies within {arcShieldR} every {arcShieldTick} s for {arcShieldShare}% of the shot. It blocks no damage.", "Turning is free; after {anchorSec} s of firing, backing into a wall braces you: no more backing off, and meanwhile knockback on you drops to {anchorKnock}%, black holes cannot drag you and the fire rate rises {braceRate}%.", "The overheat blast no longer hurts you and knocks nearby enemies back {meltPush}; for {meltHasteSec} s after, your attack speed rises {meltHaste}% and your move speed {meltMove}%, whichever weapon you hold. Still a 3 s lockout.",
            "No missile locks onto you: spares take turns on the locked enemies, or on the nearest visible enemy anywhere if none is in range; with no enemy at all they circle you for {escortSec} s and strike the first one in range.", "Your own blasts no longer hurt or push you, and touching one cuts that shot's cooldown by {bombardCut}%."
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
                ("{shellTameR}",N(u.scatterShellTameRadius)),("{shellSpeed}",N(u.scatterShellTameSpeed)),("{shellSpeedSec}",N(u.scatterShellTameSpeedSeconds)),("{guardMin}",N(u.focusGuardMin)),("{guardMax}",N(u.focusGuardMax)),
                ("{focusTameRecoil}",P(UnityEngine.Mathf.Clamp01(u.focusTameRecoilShare))),

                ("{fenceRange}",N(u.wildfireFenceRange)),("{fenceBurn}",N(u.wildfireFenceFirePerSecond*ElementCombat.WaveScale)),("{fenceSelfFire}",N(u.wildfireFenceSelfFirePerSecond)),
                ("{tankR}",N(u.fuelTankRadius)),("{tankPush}",N(u.fuelTankTamePush)),("{wildfireSpeed}",N(u.wildfireTameSpeed)),
                ("{throwDist}",N(u.shatterThrowDistance)),("{burstR}",N(u.shatterBurstRadius)),("{splash}",P(u.shatterSplashShare)),
                ("{iceFrost}",N(u.iceFrostPerSecond)),("{iceSpeed}",N(u.shatterTameSpeed)),
                ("{minHold}",N(u.blizzardShatterMinHold)),("{shatterPerSec}",N(u.blizzardShatterPerSecond)),("{shatterCap}",N(u.blizzardShatterCap)),
                ("{snowCut}",P(1f-UnityEngine.Mathf.Clamp01(u.snowTameDamageTaken))),("{snowSlow}",N(u.snowTameSlow)),("{snowFrost}",N(u.snowTameFrostPerSecond)),("{chainStore}",u.chainStoreMax.ToString()),
                ("{trailSec}",N(u.capacitorTrailSeconds)),("{trailWidth}",N(u.capacitorTrailWidth)),("{kick}",N(u.sporeKickDistance)),("{chainSpeed}",P(u.chainTameMoveSpeed-1f)),
                ("{cloudBlastR}",N(u.cloudBlastRadiusShare)),("{cloudBlastTicks}",N(u.cloudBlastTicks)),("{cloudSec}",N(u.cloudSeconds)),
                ("{needleCount}",u.needleBurstCount.ToString()),("{needleWindow}",N(u.needleBurstWindow)),("{needleR}",N(u.needleBurstRadius)),("{needleShare}",N(u.needleBurstShare)),
                ("{mistTameSec}",N(u.needlerMistSeconds*UnityEngine.Mathf.Max(1f,u.needlerMistTameLength))),("{mistReload}",N(UnityEngine.Mathf.Max(1f,u.needlerMistTameReload))),

                ("{lanceSec}",N(u.lanceTrailSeconds)),("{lanceR}",N(u.lanceCrossRadius)),("{lanceShare}",P(u.lanceCrossShare)),
                ("{braceDist}",N(u.lanceBraceDistance)),("{braceBonus}",P(u.lanceBraceDamage-1f)),
                ("{lanceRecoil}",N(UnityEngine.Mathf.Max(0f,u.lanceRecoil))),("{lanceTameRecoil}",N(UnityEngine.Mathf.Max(0f,u.lanceRecoil)*UnityEngine.Mathf.Clamp01(u.lanceTameRecoilShare))),
                ("{markSec}",N(u.executeMarkSeconds)),("{markMax}",u.executeMarkMax.ToString()),
                ("{arcShieldSec}",N(u.executeAuraSeconds)),("{arcShieldTick}",N(u.executeAuraInterval)),("{arcShieldR}",N(u.executeAuraRadius)),("{arcShieldShare}",P(u.executeAuraShare)),
                ("{turretSpin}",N(u.turretSpinSeconds)),("{turretSec}",N(u.turretSeconds)),("{anchorSec}",N(u.spinAnchorSpinSeconds)),("{anchorKnock}",P(u.spinAnchorKnockFactor)),("{braceRate}",P(u.spinBraceRateBonus)),
                ("{vent}",P(u.meltdownKillVent/4f)),("{meltHaste}",P(u.meltdownTameHaste)),("{meltHasteSec}",N(u.meltdownTameHasteSeconds)),("{meltPush}",N(u.meltdownTamePush)),("{meltMove}",P(u.meltdownTameMoveBonus)),
                ("{hiveFuse}",N(u.swarmHiveFuse)),("{hiveR}",N(u.swarmHivePerRocketRadius)),("{escortSec}",N(u.barrageEscortSeconds)),
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

        public static string ElementRule(ElementKind element)
        {
            var player = GameManager.Instance ? GameManager.Instance.player : null;
            var r = player ? ElementStatus.For(player)?.Rules : null;
            if (!r) return "";
            string P(float share) => (share * 100f).ToString("0.#");
            string N(float value) => value.ToString("0.##");
            string stacks = N(r.stacksPerHit), window = N(r.elementWindow), immunity = N(r.controlImmunity);
            bool everySecond = UnityEngine.Mathf.Approximately(r.twitchInterval, 1f);
            return element switch
            {
                ElementKind.Fire => ArenaText.Pair(                    $"Hits ignite the enemy: it burns {P(r.fireBurnShare)}% of the weapon's DPS per second for {window} s (the Flamethrower's built-in Fire has its own factor); each hit renews it.",                    $"Hits ignite the enemy: it burns {P(r.fireBurnShare)}% of the weapon's DPS per second for {window} s (the Flamethrower's built-in Fire has its own factor); each hit renews it."),
                ElementKind.Poison => ArenaText.Pair(                    $"Each hit adds {stacks} Poison (up to {N(r.poisonOnEnemyCap)}); each stack deals {N(r.poisonOnEnemyDamagePerStack * ElementCombat.WaveScale)} per second through Shields and fades {window} s after the last hit.",                    $"Each hit adds {stacks} Poison (up to {N(r.poisonOnEnemyCap)}); each stack deals {N(r.poisonOnEnemyDamagePerStack * ElementCombat.WaveScale)} per second through Shields and fades {window} s after the last hit."),
                ElementKind.Frost => ArenaText.Pair(                    $"Each hit adds {stacks} Frost, each slowing the enemy's moves and attacks by {P(r.frostSlowPerStack)}%; at {N(r.frostThreshold)} it freezes for {N(r.frostDuration)} s and takes +{P(r.frozenDamageTaken - 1f)}% damage, then cannot freeze again for {immunity} s.",                    $"Each hit adds {stacks} Frost, each slowing the enemy's moves and attacks by {P(r.frostSlowPerStack)}%; at {N(r.frostThreshold)} it freezes for {N(r.frostDuration)} s and takes +{P(r.frozenDamageTaken - 1f)}% damage, then cannot freeze again for {immunity} s."),
                ElementKind.Lightning => ArenaText.Pair(                    $"Each hit adds {stacks} Lightning: per stack +{P(r.lightningVulnerabilityPerStack)}% damage taken and a {P(r.lightningTwitchChancePerStack)}% chance {(everySecond ? "a second" : $"every {N(r.twitchInterval)} s")} to twitch; at {N(r.lightningThreshold)} the enemy is paralysed for {N(r.lightningDuration)} s{(r.lightningParalysedVulnerability > 0f ? $" (taking +{P(r.lightningParalysedVulnerability)}% damage)" : "")}, then cannot be again for {immunity} s. It jumps to nearby enemies.",                    $"Each hit adds {stacks} Lightning: per stack +{P(r.lightningVulnerabilityPerStack)}% damage taken and a {P(r.lightningTwitchChancePerStack)}% chance {(everySecond ? "a second" : $"every {N(r.twitchInterval)} s")} to twitch; at {N(r.lightningThreshold)} the enemy is paralysed for {N(r.lightningDuration)} s{(r.lightningParalysedVulnerability > 0f ? $" (taking +{P(r.lightningParalysedVulnerability)}% damage)" : "")}, then cannot be again for {immunity} s. It jumps to nearby enemies."),
                ElementKind.Earth => ArenaText.Pair($"Hits give you Shield equal to {P(r.earthShare)}% of their damage.",$"Hits give you Shield equal to {P(r.earthShare)}% of their damage."),
                ElementKind.Light => ArenaText.Pair(                    $"Hits heal you {P(r.lightShare)}% of their damage; ×{N(r.lightAgainstVoid)} damage against Void enemies.",                    $"Hits heal you {P(r.lightShare)}% of their damage; ×{N(r.lightAgainstVoid)} damage against Void enemies."),
                ElementKind.Void => ArenaText.Pair($"Hits restore Energy equal to {P(r.voidShare)}% of their damage.",$"Hits restore Energy equal to {P(r.voidShare)}% of their damage."),
                _ => ""
            };
        }
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
