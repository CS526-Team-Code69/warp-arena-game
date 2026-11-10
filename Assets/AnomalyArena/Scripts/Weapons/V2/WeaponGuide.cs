using System.Collections.Generic;

namespace AnomalyArena
{
    /// <summary>Player-facing text for the weapon card shown the moment a weapon is taken: how each anomaly fires and what its backfire does.</summary>
    public static class WeaponGuide
    {
        // Retired forms (Grav Gun, Phase Dagger, Plasma Launcher) have no entry: the armory never offers them.
        static Dictionary<(WeaponForm, int), (string how, string backfire)> Table() => new()
        {
            { (WeaponForm.Rifle, 0), (ArenaText.Pair("按住蓄力，松开射出一发贯穿一整条直线的子弹；蓄得越满越痛、推得越远。", "Hold to charge, release to fire a round that pierces a whole straight line; the fuller the charge, the harder it hits and the farther it pushes."), ArenaText.Pair($"蓄力时走得很慢，并且受到敌人攻击的伤害 +{Percent(Tuning.chargeDamageTaken - 1f)}%。", $"You walk very slowly while charging, and enemy attacks hit you for {Percent(Tuning.chargeDamageTaken - 1f)}% more.")) },
            { (WeaponForm.Rifle, 1), (ArenaText.Pair("按住连射，射击子弹，子弹碰到墙会弹一次；一匣打完要换弹。", "Hold to fire a stream of rounds; a round bounces off a wall once. Reload when the clip runs out."), ArenaText.Pair("子弹从背后飞出。", "Rounds fly out from your back.")) },
            { (WeaponForm.Knife, 0), (ArenaText.Pair("飞刃在附近的几个敌人之间来回弹，然后飞回出手那一刻玩家站的位置。", "The blade bounces back and forth between a few nearby enemies, then flies back to where you stood when you threw it."), ArenaText.Pair("飞刃折返时无差别砍伤路径上的敌人和玩家。", "On its way back the blade cuts everything on its path, enemies and you alike.")) },
            { (WeaponForm.Knife, 1), (ArenaText.Pair("链刃钩住敌人拉到身前当盾牌，能挡下飞行道具；再点一次把它甩出去，撞伤其他敌人、撞上地形，或甩进黑洞。", "The chain blade hooks an enemy and pulls it in front of you as a shield that stops projectiles; click again to fling it into other enemies, into walls or into a black hole."), ArenaText.Pair("把敌人拉向自己的同时，玩家也会被反向拽向敌人，两人在中间会合。", "As it pulls the enemy in, it drags you toward the enemy too: you meet halfway.")) },
            { (WeaponForm.RocketLauncher, 0), (ArenaText.Pair("标记光标所在的地面，稍等片刻一枚火箭从天而降，炸开一片。", "Marks the ground under the cursor; a moment later a rocket falls from the sky and blasts the area."), ArenaText.Pair("延迟爆炸，并会同时伤害玩家和敌人。", "The blast is delayed, and it hurts you and enemies alike.")) },
            { (WeaponForm.RocketLauncher, 1), (ArenaText.Pair("玩家自己变成火箭冲出去，撞伤路上的每一个敌人。", "You become the rocket and charge out, hitting every enemy on the way."), ArenaText.Pair("撞到地形上的墙体会扣血；路线上有黑洞就会被吞掉直接死亡。", "Hitting a wall costs you HP; a black hole on the path swallows you and kills you outright.")) },
            { (WeaponForm.ScatterCannon, 0), (ArenaText.Pair("短扇形两发高伤轰击，靠的越近伤害越高，两发一换。", "Two heavy blasts in a short cone; the closer the target, the higher the damage. Two shots per reload."), ArenaText.Pair("换弹时向脚下两侧抛射两枚滚烫弹壳，短暂延迟后小范围爆燃，烧伤踩在上面的玩家和敌人。", "Each reload throws two hot shells to the ground at your sides; a moment later they flare up into small fires that burn whoever steps on them, you included.")) },
            { (WeaponForm.ScatterCannon, 1), (ArenaText.Pair("按住蓄力将一发霰弹枪的所有弹丸集中至某一点，然后射出高连发子弹，可以远射。", "Hold to gather all the pellets of one shell into a single point, then fire them as a rapid burst that reaches far."), ArenaText.Pair("后坐力特别高，会一次性后退很多距离；背后有墙也不会撞伤。", "The recoil is huge: it shoves you a long way back in one go; a wall behind you does not hurt.")) },
            { (WeaponForm.Flamethrower, 0), (ArenaText.Pair("按住向前喷火，点燃扇形里的敌人。", "Hold to spray fire forward and ignite the enemies in the cone."), ArenaText.Pair("喷的时候被打中，背上的油箱会爆开，烧到周围的敌人，也瞬间点燃玩家。", "If you are hit while spraying, the tank on your back bursts, burning the enemies around you and setting you alight at once.")) },
            { (WeaponForm.Flamethrower, 1), (ArenaText.Pair("抛出火球，在落点留下一片燃烧的地面。", "Lobs a fireball that leaves a patch of burning ground where it lands."), ArenaText.Pair("站进自己的火里玩家也会被烧。", "Standing in your own fire burns you too.")) },
            { (WeaponForm.CryoCannon, 0), (ArenaText.Pair("扇形射出三发会飞的冰锥，每次射击同一个敌人最多中一根；打中已经冻住的敌人伤害翻倍。", "Fires three flying ice shards in a fan, at most one per enemy each shot; a frozen target takes double damage."), ArenaText.Pair("冰锥停下的地方会留下冰面，冰面上移动的玩家和敌人会打滑：起步、停下和转向都要一段时间（Boss、尖塔不受影响）。", "Where a shard stops it leaves an ice patch; you and enemies slip on it, starting, stopping and turning only gradually (not bosses or Spires).")) },
            { (WeaponForm.CryoCannon, 1), (ArenaText.Pair("按住向前喷雪，很快冻住敌人，一直喷就能让它们一直冻着。", "Hold to spray snow forward; enemies freeze quickly, and keeping the spray on keeps them frozen."), ArenaText.Pair("暴雪的时候玩家脚下会留下积雪，在积雪中待着会累计冰冻层数（不会冻住）。", "While you spray, snow builds up at your feet; standing in it stacks Frost on you (never enough to freeze).")) },
            { (WeaponForm.ArcCoil, 0), (ArenaText.Pair("电弧打向准星前方扇形里最近的敌人，再在附近的敌人之间跳好几次，不会隔墙跳；前方没有敌人就不放电（自带雷电属性）。", "An arc hits the nearest enemy in a cone ahead of your aim, then jumps several times between nearby enemies, never through a wall; with no enemy ahead it does not discharge (built-in Lightning element)."), ArenaText.Pair("附近敌人不够、跳不出去的电会回到玩家身上，叠多了会把玩家麻痹。", "When there are not enough enemies nearby, the jumps that find no target come back into you, and enough of them paralyse you.")) },
            { (WeaponForm.ArcCoil, 1), (ArenaText.Pair("拿在手上走路时充电，身边有敌人时充得快；点击把电全部放出去，电击玩家周围的一切，充满时放出去更痛。", "Charges as you walk with it in your hand, faster with enemies close by; click to release all of it and shock everything around you. A full charge hits harder."), ArenaText.Pair("充满电之后带着太久不放，它会在玩家身上自己放电。", "Hold a full charge too long without releasing it and it discharges into you by itself.")) },
            { (WeaponForm.SporeLauncher, 0), (ArenaText.Pair("抛出孢囊，炸开一团久久不散的毒云（自带毒素属性）。", "Lobs a pod that bursts into a long-lingering poison cloud (built-in Poison element)."), ArenaText.Pair("毒云散去时会留下孢子囊，谁踩到都会被炸一下：敌人会中毒，玩家自己踩到也会掉血（不中毒）。", "When the cloud fades it leaves spore pods that burst under whoever steps on them: enemies are poisoned, and you lose HP too (without the poison).")) },
            { (WeaponForm.SporeLauncher, 1), (ArenaText.Pair("按住连射毒针，每一根都给敌人叠毒；一匣打完要换弹。", "Hold to fire poison needles; each one stacks Poison on the enemy. Reload when the clip runs out."), ArenaText.Pair("每次换弹拆下旧毒囊时，会在玩家原地爆开一小团毒雾：敌人会中毒，玩家待在里面会掉血（不中毒）。", "Each reload, the spent venom sac bursts into a small poison mist where you stand: enemies in it are poisoned, and you lose HP while you stay in it (without the poison).")) },
            { (WeaponForm.Railgun, 0), (ArenaText.Pair("蓄一下势之后射出一发贯穿直线的轨道射线，无视护盾的减伤，后面的目标伤害依次递减。", "After a short wind-up it fires a rail beam that pierces a straight line and ignores the Shield's damage cut; each target behind the first takes less."), ArenaText.Pair("后坐力会把玩家向后推出很远，可能推进怪堆或者黑洞（背后有墙也不会撞伤）；蓄势时移速减半。", "The recoil shoves you far back, maybe into a crowd or a black hole (a wall behind you does not hurt). You move at half speed during the wind-up.")) },
            { (WeaponForm.Railgun, 1), (ArenaText.Pair("单发极高伤害，残血的敌人直接处决；击杀敌人可立即返还部分冷却，并提升下一发伤害。", "A single shot of very high damage that executes low-HP enemies outright; a kill gives part of the cooldown back at once and makes the next shot hit harder."), ArenaText.Pair("这一炮没有杀死任何敌人时（打中没死的 Boss 不算），过剩的电能会回流电击玩家自身，并进入长冷却。", "If a shot kills no enemy (a boss it hits but does not kill does not count), the surplus charge shocks you and the gun goes into a long cooldown.")) },
            { (WeaponForm.Minigun, 0), (ArenaText.Pair("按住射击，射速越转越快。", "Hold to fire; the fire rate climbs as it spins."), ArenaText.Pair($"转得越快，瞄准转向就越慢，并且会缓慢后退（转满时每秒约 {Tuning.spinUpRetreatSpeed:0.#} 格，背后有墙也不会撞伤）。", $"The faster it spins, the slower you can turn your aim, and it slowly pushes you back (about {Tuning.spinUpRetreatSpeed:0.#} cells a second at full spin; a wall behind you does not hurt).")) },
            { (WeaponForm.Minigun, 1), (ArenaText.Pair("一开火就是最高射速，但连续射击会不断积热。", "Full fire rate from the first shot, but sustained fire keeps building heat."), ArenaText.Pair("过热时会在玩家身边爆炸，然后停火一段时间。", "When it overheats it explodes around you, then stops firing for a while.")) },
            { (WeaponForm.SwarmLauncher, 0), (ArenaText.Pair("射出一群微型导弹，自动追踪附近的敌人。", "Fires a swarm of micro-missiles that home in on nearby enemies."), ArenaText.Pair($"每枚导弹锁定 {BarrageRange:0.#} 格内一个看得见的敌人；敌人不够每枚一个时，最多 {Tuning.barrageSelfLockMax} 枚会锁定玩家、折返轰炸玩家，其余没锁到的直线飞出去。", $"Each missile locks a different visible enemy within {BarrageRange:0.#} cells; when there are too few, up to {Tuning.barrageSelfLockMax} spare missiles lock onto you and turn back to blast you, and the rest fly straight on.")) },
            { (WeaponForm.SwarmLauncher, 1), (ArenaText.Pair("一串爆炸从玩家面前沿直线一路炸出去。", "A string of blasts walks away from you in a straight line."), ArenaText.Pair("第一次爆炸就在玩家面前，往前走就会走进去。", "The first blast is right in front of you; walk forward and you walk into it.")) },
            { (WeaponForm.OrbitBlades, 0), (ArenaText.Pair("刀刃绕着玩家转，碰到的敌人会受到伤害并被往外推。", "Blades circle you; enemies they touch take damage and are pushed outward."), ArenaText.Pair("会把可拾取物（经验球、血球、能量球）以及尸体拍飞。", "They also knock pickups (XP, health and energy orbs) and corpses away.")) },
            { (WeaponForm.OrbitBlades, 1), (ArenaText.Pair("刀刃绕着玩家转，碰到的敌人会受到伤害并被向内拉。", "Blades circle you; enemies they touch take damage and are pulled inward."), ArenaText.Pair("敌人会被拽到玩家脸上。", "Enemies get dragged right into your face.")) },
            // Document 5.5: the auto weapon texts read their numbers from the live tuning.
            { (WeaponForm.SentryDrone, 0), (ArenaText.Pair($"无人机自动高速连射附近的敌人，一匣 {Tuning.gunnerClip} 发。", $"The drone fires rapidly at nearby enemies on its own, {Tuning.gunnerClip} rounds to a magazine."), ArenaText.Pair($"打空一匣后无人机会掉在地上散热，玩家必须走过去踩一下才能让它重飞，不踩要等 {N(Tuning.gunnerAutoReturn)} 秒。", $"When a magazine runs dry the drone drops to the ground to cool down; step on it to send it back up, or wait {N(Tuning.gunnerAutoReturn)} s.")) },
            { (WeaponForm.SentryDrone, 1), (ArenaText.Pair($"无人机发射牵引光束，把 {N(Tuning.whirlMinRange)}–{N(WhirlRange)} 格远处落单的敌人拖拽过来并抛投砸落，它和落点附近的敌人都会受到伤害。", $"The drone's tractor beam drags a lone enemy {N(Tuning.whirlMinRange)}–{N(WhirlRange)} cells away, throws it and slams it down; it and the enemies where it lands take damage."), ArenaText.Pair($"无人机总是把怪砸在玩家 {N(Tuning.whirlAfterimageSeconds)} 秒前站的地方，那里会显示玩家的残影。", $"It always slams the enemy where you stood {N(Tuning.whirlAfterimageSeconds)} s ago; your afterimage marks the spot.")) },
            { (WeaponForm.ShieldRing, 0), (ArenaText.Pair($"开启环状力场，近身的敌人减速 {P(1f - Tuning.deflectSlow)}%；射来的飞弹有 {P(Tuning.deflectChance)}% 的概率被偏折而失效。力场里的敌人每 {N(Ring.cooldownA)} 秒受 {N(Ring.damageA)} 点轻伤，灌注的属性靠它叠上去。", $"Opens a ring-shaped force field: enemies close to you move {P(1f - Tuning.deflectSlow)}% slower, and incoming projectiles have a {P(Tuning.deflectChance)}% chance to be deflected and fizzle. Enemies in the field take a light {N(Ring.damageA)} damage every {N(Ring.cooldownA)} s, which is how an infused element gets onto them."), ArenaText.Pair($"玩家自己从圈内往外发射的子弹和抛物，穿过能量环边缘时也有 {P(Tuning.deflectChance)}% 的概率被偏折散射。", $"Your own bullets and lobbed shots leaving the ring also have a {P(Tuning.deflectChance)}% chance to be knocked off course at its edge.")) },
            { (WeaponForm.ShieldRing, 1), (ArenaText.Pair($"一段时间没受到敌人攻击后，生成一层可抵挡 1 次伤害的能量盾；每挡下一次给玩家 {N(Tuning.drainBlockEnergy)} 点能量。灌注后，能量盾每挡下一次攻击，就对 {N(Tuning.drainInfuseRadius)} 格内的敌人放出一次带该属性的冲击。", $"After a while without an enemy hit it raises an Energy Shield that blocks one hit; each block gives you {N(Tuning.drainBlockEnergy)} Energy. Once infused, each block also releases a burst of its element on the enemies within {N(Tuning.drainInfuseRadius)} cells."), ArenaText.Pair($"能量盾破碎后 {N(Tuning.drainOverloadSeconds)} 秒内再受到攻击，会失去 {N(Tuning.drainOverloadEnergy)} 点能量（每次破碎只扣一次）。", $"Take another hit within {N(Tuning.drainOverloadSeconds)} s after the shield breaks and you lose {N(Tuning.drainOverloadEnergy)} Energy (once per break).")) },
            // The V2 campaign's starting Pistol (since 2026-11-06): plain shots and no trait, so its trait line says so instead of staying empty.
            { (WeaponForm.Pistol, 0), (PistolHow, PistolTrait) },
            { (WeaponForm.Pistol, 1), (PistolHow, PistolTrait) },
        };

        static string PistolHow => ArenaText.Pair("普通射击：按住连续射击，每发子弹沿直线飞出，打中第一个敌人就停下。没有蓄力，不用换弹，也没有任何异常效果。",
            "Plain shots: hold to keep firing; each round flies in a straight line and stops at the first enemy it hits. No charge, no reload and no anomaly of any kind.");
        static string PistolTrait => ArenaText.Pair("无。小手枪是普通武器，没有异常特性，也不能升级。", "None. The Pistol is an ordinary weapon: no anomaly trait, and it takes no upgrades.");

        // Package g4-gatling-swarm: the Spin-up and Barrage cards read their numbers from the live tuning and roster.
        static WeaponUpgradeTuning Tuning => GameManager.Instance && GameManager.Instance.runWeapons ? GameManager.Instance.runWeapons.upgrades : new WeaponUpgradeTuning();
        // The Deflect card states its light hit from the live roster (damageA every cooldownA).
        static WeaponRoster.Definition Ring
        {
            get
            {
                var weapons = GameManager.Instance ? GameManager.Instance.runWeapons : null;
                var definition = weapons && weapons.roster ? weapons.roster.Get(WeaponForm.ShieldRing) : null;
                return definition ?? new WeaponRoster.Definition { form = WeaponForm.ShieldRing, damageA = 1f, cooldownA = 1f };
            }
        }
        static float BarrageRange
        {
            get
            {
                var weapons = GameManager.Instance ? GameManager.Instance.runWeapons : null;
                var definition = weapons && weapons.roster ? weapons.roster.Get(WeaponForm.SwarmLauncher) : null;
                return definition != null ? definition.rangeA : 9f;
            }
        }

        static (string how, string backfire) Entry(WeaponForm form, int variant) =>
            Table().TryGetValue((form, variant == 0 ? 0 : 1), out var entry) ? entry : ("", "");

        /// <summary>How this anomaly fires, in one or two sentences.</summary>
        public static string How(WeaponForm form, int variant) => Entry(form, variant).how;

        /// <summary>What its backfire (the card's trait, 特性) does to the player.</summary>
        public static string Backfire(WeaponForm form, int variant) => Entry(form, variant).backfire + Stowed(form, variant);

        /// <summary>Document 5.4: a switch never clears what a trait has built up; stowed, it wears off. Kept beside the
        /// table so the sentence stays with the rule (RunWeapons.DrainStowedCapacitor).</summary>
        static string Stowed(WeaponForm form, int variant) => form == WeaponForm.ArcCoil && variant == 1
            ? ArenaText.Pair("收起来时电量会慢慢掉光，满电计时也会往回退，收着不会自己放电。", " Stowed, it slowly loses its charge and its full-charge timer runs back; it never discharges while stowed.")
            : "";

        static string Percent(float share) => (share * 100f).ToString("0.#");

        /// <summary>The Whirl's farthest pull: the Sentry Drone's rangeB in the live roster.</summary>
        static float WhirlRange
        {
            get
            {
                var roster = GameManager.Instance && GameManager.Instance.runWeapons ? GameManager.Instance.runWeapons.roster : null;
                var definition = roster ? roster.Get(WeaponForm.SentryDrone) : null;
                return definition != null ? definition.rangeB : 12f;
            }
        }
        static string N(float value) => value.ToString("0.##");
        static string P(float share) => (share * 100f).ToString("0.#");
    }
}
