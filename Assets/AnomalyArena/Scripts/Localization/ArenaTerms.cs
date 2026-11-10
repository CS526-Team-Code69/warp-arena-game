namespace AnomalyArena
{
    /// <summary>Enum identities stay stable; only player-facing names follow the official selected locale.</summary>
    public static class ArenaTerms
    {
        // Names follow the system design document §6.5 (renames 2026-10-25: Arc Cannon / Arc Jump, Gatling; 2026-10-27, document 5.5: Flying Blade / Chain Blade); retired forms keep their old names for saved data.
        // The Pistol (since 2026-11-06, last) is the V2 campaign's plain starting weapon: both of its "anomaly" names are just its plain shot.
        static readonly string[] FormZh = { "步枪", "小刀", "火箭筒", "重力枪", "霰射炮", "相位匕首", "等离子发射器", "喷火器", "冷凝炮", "电弧炮", "毒液发射器", "轨道炮", "加特林", "蜂群发射器", "环绕刃", "哨戒无人机", "能量环", "小手枪" };
        static readonly string[] FormEn = { "Rifle", "Knife", "Rocket Launcher", "Grav Gun", "Scatter Cannon", "Phase Dagger", "Plasma Launcher", "Flamethrower", "Cryo Cannon", "Arc Cannon", "Venom Launcher", "Railgun", "Gatling", "Swarm Launcher", "Orbit Blades", "Sentry Drone", "Energy Ring", "Pistol" };
        static readonly string[] AnomalyZh = { "蓄力射击", "反向射击", "飞刃", "链刃", "火力打击", "人体导弹", "吸引", "排斥", "轰击", "集中", "位置交换", "回返", "黏附", "弹跳", "喷流", "野火", "碎冰", "暴雪", "跳电", "蓄电", "毒云", "毒针", "穿甲", "处决", "预热", "熔毁", "齐射", "轰炸", "闭刃", "开刃", "射手", "回旋", "偏折", "汲能", "普通射击", "普通射击" };
        static readonly string[] SkillZh = { "烈焰尾迹", "传染", "冰霜新星", "风暴召唤", "壁垒", "辉光", "吞噬", "闪现", "冲击波", "引力井", "时停", "镜像", "过载", "口袋虚空", "虚空行走", "虚空奇点" };
        static readonly string[] SkillEn = { "Flame Wake", "Contagion", "Frost Nova", "Storm Call", "Bulwark", "Radiance", "Devour", "Blink", "Shockwave", "Gravity Well", "Time Stop", "Mirror Image", "Overcharge", "Pocket Void", "Void Walk", "Singularity" };
        static readonly string[] StatZh = { "最大生命", "生命回复", "移动速度", "冲刺时长", "伤害", "攻击速度", "击退", "冲击", "拾取范围", "防护", "幸运", "能量上限", "能量汲取", "伤害穿透", "暴击" };
        static readonly string[] StatEn = { "Max HP", "Regeneration", "Move Speed", "Sprint Duration", "Damage", "Attack Speed", "Knockback", "Impact", "Pickup Range", "Protection", "Luck", "Max Energy", "Energy Siphon", "Damage Penetration", "Crit" };

        public static string WeaponForm(AnomalyArena.WeaponForm form) => ArenaText.Pair(FormZh[(int)form], FormEn[(int)form]);
        public static string WeaponAnomaly(AnomalyArena.WeaponForm form, int variant) => ArenaText.Pair(AnomalyZh[(int)form * 2 + (variant == 0 ? 0 : 1)], WeaponRoster.AnomalyName(form, variant));
        public static string Skill(SkillId id) => ArenaText.Pair(SkillZh[(int)id], SkillEn[(int)id]);
        public static string Stat(StatId id) => (int)id < StatZh.Length ? ArenaText.Pair(StatZh[(int)id], StatEn[(int)id]) : id.ToString();
        static readonly string[] ExpandedTwistZh = {
            "牵引结束向外击退3单位，不加伤害。", "排斥只影响准星前方180°。", "每匣第 2 发先吸来前方 {vacuumRange} 格内敌人，再轰出 {vacuumShare}% 伤害。", "每颗弹丸穿过第 1 个敌人，第 2 个敌人受 {focusPierce}% 伤害。",
            "匕首穿过第一人，与第二个合法目标交换；每人仅一次命中。", "离开处也爆炸；两圈重叠的同一敌人只受一次伤害。", "黏附0.3秒后再次点击可提前引爆。", "第三次弹墙后贴墙，保留原引信。",
            "喷射时烧掉扇形里的敌方飞行物（毒刺、Boss 弹幕），不加伤害。", "两片火区相距 {fenceRange} 格内且不隔墙时连成火墙：碰到火墙的敌人被点燃，每秒烧 {fenceBurn} 点（随波次成长）；你碰到每秒叠 {fenceSelfFire} 层燃烧。", "冰锥把冻住的敌人打飞 {throwDist} 格（Boss、尖塔除外），停下时碎开：它吃下这一锥，{burstR} 格内其他敌人受 {splash}% 冰锥伤害。", "按住 {minHold} 秒以上再松手，扇形里冻住的敌人立即解冻，每被喷冻 1 秒受 {shatterPerSec} 点伤害，最多 {shatterCap} 点（随武器伤害成长）；解冻后的敌人一段时间内冻不住。",
            "第一跳不变；之后每跳在上一个目标 4 格内、不隔墙的敌人里选离准星最近的。", "放电时你最近 {trailSec} 秒走过的路线也落雷：离路线 {trailWidth} 格内的敌人受同样伤害，每人一次。", "孢子囊在毒云里炸开时引爆整团毒云：毒云半径 {cloudBlastR} 倍内的敌人受 {cloudBlastTicks} 跳毒云伤害、毒层叠满，不伤你；毒云时间刷新为 {cloudSec} 秒。", "{needleWindow} 秒内被毒针打中 {needleCount} 次的敌人炸开：{needleR} 格内其他敌人受 {needleShare} 倍毒针伤害，它自己不受。",
            "弹道留 {lanceSec} 秒，下一发与它交叉处爆炸：半径 {lanceR}、基础伤害 {lanceShare}%，无视护盾的减伤。", "被打中的敌人（Boss 除外）{markSec} 秒内被任何伤害打到处决线下就处决，最多 {markMax} 个。", "按住 {turretSpin} 秒满转速后不松手切枪：加特林留在原地朝原方向再射 {turretSec} 秒，切回即收回。", "子弹每打死 1 个敌人热量降 {vent}%，打死精英清空热量（Boss 不算）。",
            "火箭粘住敌人，{hiveFuse} 秒后合并爆炸：伤害相加，每多 1 枚半径 +{hiveR}。", "{bombardBlasts} 次爆炸各落在当时的准星方向，拖动鼠标可炸成弧线。"
        };
        static readonly string[] ExpandedTwistEn = {
            "Push enemies out by 3 when the pull ends; no extra damage.", "Repel only in the aimed 180-degree half circle.", "The second round of each clip pulls in enemies within {vacuumRange} cells ahead, then blasts them for {vacuumShare}% damage.", "Each pellet passes through the first enemy; the second takes {focusPierce}% damage.",
            "Pierce the first body and swap with the second valid target; hit each once.", "Blast at departure too; overlapping blasts hit each enemy once.", "Click again after 0.3 s attached to detonate early.", "Stick to the wall after the third bounce, keeping the original fuse.",
            "While spraying, the flame burns enemy projectiles in its cone (spikes, boss shots); no extra damage.", "Two fire patches within {fenceRange} with no wall between form a fence: an enemy touching it burns for {fenceBurn} per second (more in later waves); you gain {fenceSelfFire} Burning per second.", "A shard knocks a frozen enemy {throwDist} away (not bosses or Spires); when it stops it shatters, taking the shard with its frozen bonus, and others within {burstR} take {splash}% of a shard.", "Release after holding {minHold} s or more: frozen enemies in the cone thaw at once, taking {shatterPerSec} per second frozen under the spray, {shatterCap} at most (scaled like the weapon); a thawed enemy cannot be frozen again for a while.",
            "The first target is unchanged; each later jump goes to the enemy nearest your aim point within 4 of the last target, never through walls.", "A discharge also strikes the path you walked in the last {trailSec} s: enemies within {trailWidth} of it take the same damage, once each.", "A spore pod bursting inside your cloud detonates it: enemies within {cloudBlastR}x its radius take {cloudBlastTicks} ticks of its damage and fill up on Poison (you are spared); the cloud's time resets to {cloudSec} s.","An enemy hit by {needleCount} needles within {needleWindow} s bursts: other enemies within {needleR} take {needleShare}x needle damage; it does not.",
            "The slug's path stays for {lanceSec} s; where the next slug crosses it, a blast: radius {lanceR}, {lanceShare}% base damage, ignores the Shield's damage cut.", "For {markSec} s, any damage that drops an enemy you hit under the execute line executes it (not bosses; up to {markMax}).", "Switch at full spin ({turretSpin} s) with fire held: the Gatling stays and fires the same way for {turretSec} s; switching back recalls it.", "Each enemy your bullets kill removes {vent}% heat; an elite kill clears it (not bosses).",
            "Rockets stick to enemies and merge into one blast after {hiveFuse} s: damage adds up, +{hiveR} radius per extra rocket.", "Each of the {bombardBlasts} blasts lands along your aim at that moment; sweep the mouse to bend the line into an arc."
        };
        static readonly string[] ExpandedTameZh = {
            "自己的牵引场不再拉自己。", "取消自身2单位后坐，经验球仍会被推散。", "弹壳爆燃范围 ×{shellTameR}，不再烧你；换弹时踩上烧起来的弹壳，立刻装满，移速 ×{shellSpeed} 持续 {shellSpeedSec} 秒。", "后坐照样把你推开，推开的这一下免受攻击 {guardMin}–{guardMax} 秒（随蓄力）。",
            "成功交换后获得0.5秒受击保护，不清状态、不防掉洞。", "留置6秒未回返便自动收刀，不传送、不爆炸。", "携带者不再加速，爆炸仍能伤自己。", "爆炸不扣本人生命，仍推移本人。",
            "油箱爆开不再给你叠火，还把 {tankR} 格内的敌人击退 {tankPush} 格（真击退，Boss 免疫击退）。", "自己的火区和火墙不再烧你（已放下的也一样）；身体中心在自己火区里时移速 ×{wildfireSpeed}。", "你在自己的冰面上不再打滑，移速 ×{iceSpeed}；踩在冰面上的敌人照样打滑，每秒叠 {iceFrost} 层冰冻，能被冻住。", "积雪不再给你叠冰冻，你站在积雪里受到的伤害减少 {snowCut}%；走进积雪的敌人移速 ×{snowSlow}，每秒多叠 {snowFrost} 层冰冻。",
            "跳不出去的电不再回流到你，存到下一发，最多多跳 {chainStore} 次；存着电且拿在手上时，你的移速 +{chainSpeed}%。", "拿在手上满电 4 秒没放时自动放一次满电放电，不伤你。", "你踩到孢子囊时把它踢出 {kick} 格，落地再炸开；孢子囊的毒雾都不毒你。", "换弹毒雾不再伤你，持续时间延长到 {mistTameSec} 秒；你碰到它时换弹速度 ×{mistReload}。",
            "背后 {braceDist} 格内有墙：不后退，伤害 +{braceBonus}%；没墙时照样后退 {lanceRecoil} 格；蓄势时仍会减速。", "没杀死敌人时不再电你、不进长冷却，改为 {arcShieldSec} 秒电弧护盾：每 {arcShieldTick} 秒电一次 {arcShieldR} 格内的敌人，伤害为这一炮的 {arcShieldShare}%，不挡伤害。", "转身不再受限；按住开火 {anchorSec} 秒转满后，后退撞到墙体会稳固架枪：不再后退，期间击退只剩 {anchorKnock}%、不被黑洞拉动、射速 +{braceRate}%。", "过热爆炸不再伤你，还把身边敌人震退 {meltPush} 格；之后 {meltHasteSec} 秒内你的攻速 +{meltHaste}%、移速 +{meltMove}%，切到另一把武器也有效；仍停火 3 秒。",
            "导弹不再锁定你：多出来的导弹轮流集火已锁定的敌人，射程内没有敌人就锁场上最近的可见敌人；场上没有敌人时绕着你护航 {escortSec} 秒，有敌人进入射程就飞去打。", "碰到自己的爆炸不受伤不被推，这一发冷却减 {bombardCut}%。"
        };
        static readonly string[] ExpandedTameEn = {
            "Your attraction field no longer pulls you.", "Remove your 2-unit recoil; orbs are still repelled.", "Shell fires grow x{shellTameR} and no longer burn you; step on a burning shell while reloading to finish the reload at once and move x{shellSpeed} for {shellSpeedSec} s.", "The recoil still pushes you as far, and while it does you are immune to attacks for {guardMin}–{guardMax} s (by charge).",
            "Gain 0.5 s hit protection after a swap; no status clear or hole immunity.", "Recall the unused planted dagger after 6 s, without teleporting or exploding.", "Attached carriers no longer speed up; the blast can still hurt you.", "The blast spares your HP but still displaces you.",
            "The fuel-tank burst no longer sets you alight and knocks enemies within {tankR} back by {tankPush} (a real knockback; bosses are not knocked back).", "Your own patches and fences no longer burn you (ones already down too); while your centre is in one, you move x{wildfireSpeed}.", "You no longer slip on your own ice and move x{iceSpeed} on it; enemies on it still slip and gain {iceFrost} Frost per second until they freeze.", "Your snow no longer stacks Frost on you, and you take {snowCut}% less damage standing in it; enemies in it move at x{snowSlow} and gain {snowFrost} more Frost per second.",
            "Unused jumps no longer return through you; they are stored for the next discharge, up to +{chainStore}. While some are stored and it is in your hand, you move {chainSpeed}% faster.", "Held full in your hand for 4 s, it discharges by itself at full strength and spares you.", "Stepping on a spore pod kicks it {kick} away, where it bursts; pod mists never poison you.", "Your reload mist no longer hurts you and lasts {mistTameSec} s; while you touch it you reload {mistReload}x as fast.",
            "Wall within {braceDist} behind you: no recoil, +{braceBonus}% damage; no wall: the recoil still pushes you back {lanceRecoil}. The wind-up still slows you.", "A shot that kills nothing no longer shocks you or forces a long cooldown: an arc shield circles you for {arcShieldSec} s, shocking enemies within {arcShieldR} every {arcShieldTick} s for {arcShieldShare}% of the shot. It blocks no damage.", "Turning is free; after {anchorSec} s of firing, backing into a wall braces you: no more backing off, and meanwhile knockback on you drops to {anchorKnock}%, black holes cannot drag you and the fire rate rises {braceRate}%.", "The overheat blast no longer hurts you and knocks nearby enemies back {meltPush}; for {meltHasteSec} s after, your attack speed rises {meltHaste}% and your move speed {meltMove}%, whichever weapon you hold. Still a 3 s lockout.",
            "No missile locks onto you: spares take turns on the locked enemies, or on the nearest visible enemy anywhere if none is in range; with no enemy at all they circle you for {escortSec} s and strike the first one in range.", "Your own blasts no longer hurt or push you, and touching one cuts that shot's cooldown by {bombardCut}%."
        };
        public static string ExpandedUpgradeDescription(AnomalyArena.WeaponForm form,int variant,UpgradeAxis axis)
        {
            int index=((int)form-3)*2+(variant==0?0:1);
            if(index<0||index>=ExpandedTwistZh.Length)return "";
            return Fill(axis==UpgradeAxis.Twist?ArenaText.Pair(ExpandedTwistZh[index],ExpandedTwistEn[index]):axis==UpgradeAxis.Tame?ArenaText.Pair(ExpandedTameZh[index],ExpandedTameEn[index]):"");
        }
        /// <summary>Puts the live tuning numbers into card text, so a retuned value never leaves a stale card.</summary>
        static string Fill(string text)
        {
            if(string.IsNullOrEmpty(text)||text.IndexOf('{')<0)return text;
            var weapons=GameManager.Instance?GameManager.Instance.runWeapons:null;
            var u=weapons?weapons.upgrades:new WeaponUpgradeTuning();
            string P(float share)=>(share*100f).ToString("0.#");
            string N(float value)=>value.ToString("0.##");
            var tokens=new (string key,string value)[]
            {
                // Scatter Cannon
                ("{vacuumRange}",N(u.scatterVacuumRange)),("{vacuumShare}",P(u.scatterVacuumShare)),("{focusPierce}",P(u.focusPierceRetention)),
                ("{shellTameR}",N(u.scatterShellTameRadius)),("{shellSpeed}",N(u.scatterShellTameSpeed)),("{shellSpeedSec}",N(u.scatterShellTameSpeedSeconds)),("{guardMin}",N(u.focusGuardMin)),("{guardMax}",N(u.focusGuardMax)),
                // Flamethrower, Cryo Cannon, Arc Cannon, Venom Launcher
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
                // Railgun, Gatling, Swarm Launcher
                ("{lanceSec}",N(u.lanceTrailSeconds)),("{lanceR}",N(u.lanceCrossRadius)),("{lanceShare}",P(u.lanceCrossShare)),
                ("{braceDist}",N(u.lanceBraceDistance)),("{braceBonus}",P(u.lanceBraceDamage-1f)),
                ("{lanceRecoil}",N(UnityEngine.Mathf.Max(0f,u.lanceRecoil))),
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
            UpgradeAxis.Infusion => ArenaText.Pair("灌注", "Infusion"),
            UpgradeAxis.Power => ArenaText.Pair("威力", "Power"),
            UpgradeAxis.Tempo => ArenaText.Pair("节奏", "Tempo"),
            UpgradeAxis.Reach => ArenaText.Pair("范围", "Reach"),
            UpgradeAxis.Twist => ArenaText.Pair("变招", "Twist"),
            UpgradeAxis.Tame => ArenaText.Pair("驯化", "Tame"),
            _ => axis.ToString()
        };
        public static string Element(ElementKind element) => element switch
        {
            ElementKind.None => ArenaText.Pair("无属性", "None"),
            ElementKind.Fire => ArenaText.Pair("火焰", "Fire"),
            ElementKind.Poison => ArenaText.Pair("毒素", "Poison"),
            ElementKind.Frost => ArenaText.Pair("冰冻", "Frost"),
            ElementKind.Lightning => ArenaText.Pair("雷电", "Lightning"),
            ElementKind.Earth => ArenaText.Pair("大地", "Earth"),
            ElementKind.Light => ArenaText.Pair("光明", "Light"),
            ElementKind.Void => ArenaText.Pair("虚空", "Void"),
            _ => element.ToString()
        };
        /// <summary>What an infused element does, for the Infusion card, with the live StatusRules numbers so a retuned
        /// value never leaves a stale card. Empty without a player to read the rules from.</summary>
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
                ElementKind.Fire => ArenaText.Pair($"命中点燃敌人：{window} 秒内每秒烧这把武器秒伤的 {P(r.fireBurnShare)}%（喷火器自带的火焰另有倍率），再命中就续上。",
                    $"Hits ignite the enemy: it burns {P(r.fireBurnShare)}% of the weapon's DPS per second for {window} s (the Flamethrower's built-in Fire has its own factor); each hit renews it."),
                ElementKind.Poison => ArenaText.Pair($"每次命中叠 {stacks} 层毒（最多 {N(r.poisonOnEnemyCap)} 层），每层每秒 {N(r.poisonOnEnemyDamagePerStack * ElementCombat.WaveScale)} 点伤害、无视护盾；{window} 秒不再命中就散去。",
                    $"Each hit adds {stacks} Poison (up to {N(r.poisonOnEnemyCap)}); each stack deals {N(r.poisonOnEnemyDamagePerStack * ElementCombat.WaveScale)} per second through Shields and fades {window} s after the last hit."),
                ElementKind.Frost => ArenaText.Pair($"每次命中叠 {stacks} 层冰冻，每层让敌人移动和出手慢 {P(r.frostSlowPerStack)}%；叠满 {N(r.frostThreshold)} 层冻结 {N(r.frostDuration)} 秒、受到伤害 +{P(r.frozenDamageTaken - 1f)}%，之后 {immunity} 秒不会再冻结。",
                    $"Each hit adds {stacks} Frost, each slowing the enemy's moves and attacks by {P(r.frostSlowPerStack)}%; at {N(r.frostThreshold)} it freezes for {N(r.frostDuration)} s and takes +{P(r.frozenDamageTaken - 1f)}% damage, then cannot freeze again for {immunity} s."),
                ElementKind.Lightning => ArenaText.Pair($"每次命中叠 {stacks} 层雷电，每层让敌人受到伤害 +{P(r.lightningVulnerabilityPerStack)}%、{(everySecond ? "每秒" : $"每 {N(r.twitchInterval)} 秒")}有 {P(r.lightningTwitchChancePerStack)}% 机会抽搐；叠满 {N(r.lightningThreshold)} 层麻痹 {N(r.lightningDuration)} 秒{(r.lightningParalysedVulnerability > 0f ? $"（期间受到伤害 +{P(r.lightningParalysedVulnerability)}%）" : "")}，之后 {immunity} 秒不会再麻痹；雷电会传给附近的敌人。",
                    $"Each hit adds {stacks} Lightning: per stack +{P(r.lightningVulnerabilityPerStack)}% damage taken and a {P(r.lightningTwitchChancePerStack)}% chance {(everySecond ? "a second" : $"every {N(r.twitchInterval)} s")} to twitch; at {N(r.lightningThreshold)} the enemy is paralysed for {N(r.lightningDuration)} s{(r.lightningParalysedVulnerability > 0f ? $" (taking +{P(r.lightningParalysedVulnerability)}% damage)" : "")}, then cannot be again for {immunity} s. It jumps to nearby enemies."),
                ElementKind.Earth => ArenaText.Pair($"命中时把伤害的 {P(r.earthShare)}% 变成你的护盾。", $"Hits give you Shield equal to {P(r.earthShare)}% of their damage."),
                ElementKind.Light => ArenaText.Pair($"命中时按伤害的 {P(r.lightShare)}% 给你回血；打虚空敌人伤害 ×{N(r.lightAgainstVoid)}。",
                    $"Hits heal you {P(r.lightShare)}% of their damage; ×{N(r.lightAgainstVoid)} damage against Void enemies."),
                ElementKind.Void => ArenaText.Pair($"命中时按伤害的 {P(r.voidShare)}% 给你回能量。", $"Hits restore Energy equal to {P(r.voidShare)}% of their damage."),
                _ => ""
            };
        }
        public static string CardKind(DraftCardKind kind) => kind switch
        {
            DraftCardKind.Stat => ArenaText.Pair("属性", "Stat"),
            DraftCardKind.Weapon => ArenaText.Pair("武器", "Weapon"),
            _ => ArenaText.Pair("技能", "Skill")
        };
        public static string Rarity(CardRarity rarity) => rarity switch
        {
            CardRarity.Common => ArenaText.Pair("普通", "Common"),
            CardRarity.Rare => ArenaText.Pair("稀有", "Rare"),
            CardRarity.Exceptional => ArenaText.Pair("罕见", "Exceptional"),
            CardRarity.Epic => ArenaText.Pair("史诗", "Epic"),
            CardRarity.Legendary => ArenaText.Pair("传说", "Legendary"),
            CardRarity.Void => ArenaText.Pair("虚空", "Void"),
            _ => ArenaText.Pair("普通", "Common")
        };
    }
}
