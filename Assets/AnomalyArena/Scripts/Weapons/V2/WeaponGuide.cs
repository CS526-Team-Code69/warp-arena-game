using System.Collections.Generic;

namespace AnomalyArena
{
    /// <summary>Player-facing text for the weapon card shown the moment a weapon is taken: how each anomaly fires and what its backfire does.</summary>
    public static class WeaponGuide
    {
        // Retired forms (Grav Gun, Phase Dagger, Plasma Launcher) have no entry: the armory never offers them.
        static Dictionary<(WeaponForm, int), (string how, string backfire)> Table() => new()
        {
            { (WeaponForm.Rifle, 0), (ArenaText.Pair("按住蓄力，松开时射出一发子弹，贯穿直线上的所有敌人；蓄得越满，伤害越高，把敌人推得越远。", "Hold to charge and release to fire a round that pierces every enemy in a straight line; the fuller the charge, the more damage it deals and the farther it pushes them."), ArenaText.Pair($"蓄力时你的移速减半，被敌人攻击时多受 {Percent(Tuning.chargeDamageTaken - 1f)}% 的伤害；身边有敌人时，先拉开距离再蓄力。", $"While you charge you move at half speed and enemy attacks hit you for {Percent(Tuning.chargeDamageTaken - 1f)}% more, so open some distance before you charge.")) },
            { (WeaponForm.Rifle, 1), (ArenaText.Pair("按住连续射击，子弹撞到墙会反弹一次；一匣打完要换弹。", "Hold to fire a stream of rounds; a round bounces off a wall once. Reload when the clip runs out."), ArenaText.Pair("子弹从你背后、朝准星的反方向飞出，所以要背对敌人开枪；背靠墙开枪时，子弹撞墙后会弹回你的前方。", "Rounds leave from your back, opposite your aim: turn your back on an enemy to hit it, or put your back to a wall so they bounce off it and fly forward.")) },
            { (WeaponForm.Knife, 0), (ArenaText.Pair("朝准星掷出飞刃，它会在附近的几个敌人之间依次弹跳，然后飞回你出手那一刻站的位置。", "Throws the blade toward your aim; it bounces between a few nearby enemies in turn, then flies back to the spot where you stood when you threw it."), ArenaText.Pair("飞刃往回飞时会砍伤路上的所有敌人，也会砍伤你；出手后离开原地，它就砍不到你。", "On its way back it cuts every enemy in its path, and you too; walk away from where you threw it and it misses you.")) },
            { (WeaponForm.Knife, 1), (ArenaText.Pair("甩出链刃钩住第一个碰到的敌人，把它拉到身前当盾牌，挡下敌人射来的飞弹；再点一次，把它朝准星甩出去，撞伤别的敌人、撞上墙，或者扔进黑洞。Boss 和尖塔钩不动。", "The chain blade hooks the first enemy it reaches and pulls it in front of you as a shield that stops enemy projectiles; click again to fling it along your aim into other enemies, into a wall or into a black hole. Bosses and Spires cannot be hooked."), ArenaText.Pair("链刃把敌人拉向你的同时，也会把你反向拽向敌人，你和它在中间会合；去钩怪堆里的敌人，你就会被拖向怪堆。", "As it reels the enemy in, it drags you toward it as well, and the two of you meet halfway; hook an enemy inside a crowd and you are dragged toward the crowd.")) },
            { (WeaponForm.RocketLauncher, 0), (ArenaText.Pair("在光标所在的地面做标记，片刻后一枚火箭从天而降，炸伤范围内的所有敌人，墙后的敌人也躲不开。", "Marks the ground under the cursor; a moment later a rocket falls from the sky and blasts every enemy in the area, even behind walls."), ArenaText.Pair("爆炸不分敌我：火箭落下时你如果站在标记范围里，也会被炸伤并被炸退。被炸退时撞上墙，还会再伤一次。", "The blast hurts you as well as enemies: if you are inside the marked area when the rocket lands, it hits you and knocks you back. If it throws you into a wall, the wall hurts you again.")) },
            { (WeaponForm.RocketLauncher, 1), (ArenaText.Pair("你自己化作火箭，朝准星方向冲出去，撞伤沿途的每一个敌人。", "You become the rocket and fly toward your aim, hitting every enemy along the way."), ArenaText.Pair("撞上墙会受伤并被弹回来；飞行时会被黑洞拉偏，飞进黑洞会被直接吞掉、当场死亡。出发前先看清前方。", "Hitting a wall hurts you and bounces you back; black holes drag your flight off course, and flying into one swallows you and kills you outright. Check the way ahead before you launch.")) },
            { (WeaponForm.ScatterCannon, 0), (ArenaText.Pair("朝前方短扇形轰出一炮，离得越近伤害越高，并把敌人击退；每打两发要换一次弹。", "Blasts a short cone ahead; the closer the enemy, the more damage it takes, and every enemy hit is knocked back. It reloads after every two shots."), ArenaText.Pair("每次换弹都会在你左右两侧各抛下一枚滚烫的弹壳，片刻后各烧成一小片火，烧伤碰到火的敌人，你踩上去也会被烧；换弹时别往两边走。", "Each reload drops two hot shells, one on each side of you; a moment later each flares into a small fire that burns enemies touching it, and you if you step on it, so do not sidestep while you reload.")) },
            { (WeaponForm.ScatterCannon, 1), (ArenaText.Pair("按住蓄力，把一发霰弹的所有弹丸聚成一束，松开后沿准星方向快速连射出去，射程很远；蓄得越满伤害越高。", "Hold to gather all the pellets of one shell into a single line, then release to fire them in a rapid burst along your aim; it reaches far, and a fuller charge deals more damage."), ArenaText.Pair("后坐力很大，松手的瞬间会把你往后推出很远，蓄得越满推得越远；背后有墙只会停下，不会撞伤。开火前先看看身后。", "The recoil is huge: the moment you release, it shoves you a long way back, farther with a fuller charge. A wall behind you just stops you and does not hurt; check what is behind you before you fire.")) },
            { (WeaponForm.Flamethrower, 0), (ArenaText.Pair("按住向前喷火，点燃扇形里的敌人。", "Hold to spray fire forward and ignite the enemies in the cone."), ArenaText.Pair("喷火时一旦被敌人打中，背上的油箱就会爆开，烧伤身边的敌人，也让你立刻着火；攻击要打过来时先松手。", "If an enemy hits you while you spray, the tank on your back bursts, burning the enemies around you and setting you on fire at once; let go of the trigger when an attack is coming.")) },
            { (WeaponForm.Flamethrower, 1), (ArenaText.Pair("朝准星抛出火球，在落点留下一片燃烧的地面，持续烧伤站在上面的敌人。", "Lobs a fireball at your aim; where it lands it leaves a patch of burning ground that keeps burning the enemies standing on it."), ArenaText.Pair("你站进自己的火里也会被烧，别把火球扔在自己脚下。", "Your own fire burns you too if you stand in it, so do not throw it at your feet.")) },
            { (WeaponForm.CryoCannon, 0), (ArenaText.Pair("扇形射出三根冰锥，每次射击同一个敌人最多中一根；打中已经冻住的敌人时伤害翻倍。", "Fires three flying ice shards in a fan, at most one per enemy each shot; a frozen target takes double damage."), ArenaText.Pair("冰锥停下的地方会留下一片冰面，你和敌人走上去都会打滑，起步、停下和转向都要一段时间（Boss 和尖塔不受影响）；能绕开冰面就绕开。", "Wherever a shard stops it leaves an ice patch; you and enemies slip on it, so starting, stopping and turning all take time (bosses and Spires never slip). Steer around the ice when you can.")) },
            { (WeaponForm.CryoCannon, 1), (ArenaText.Pair("按住向前喷雪，很快冻住敌人，一直喷就能让它们一直冻着。", "Hold to spray snow forward; enemies freeze quickly, and keeping the spray on keeps them frozen."), ArenaText.Pair("喷雪时脚下会不断积雪，站在积雪里会给你叠冰冻，让你的移动和武器都变慢，但不会把你冻住；停手后尽快走出积雪。", "While you spray, snow piles up at your feet; standing in it stacks Frost on you, which slows your movement and your weapons but never freezes you. Step out of the snow once you stop spraying.")) },
            { (WeaponForm.ArcCoil, 0), (ArenaText.Pair("电弧打向准星前方扇形里最近的敌人，再在附近的敌人之间跳好几次，不会隔墙跳；前方没有敌人就不放电。它自带雷电属性。", "An arc hits the nearest enemy in a cone ahead of your aim, then jumps several times between nearby enemies, never through a wall; with no enemy ahead it does not discharge. It carries the Lightning element."), ArenaText.Pair("附近的敌人不够时，跳不出去的电会回流到你身上，叠多了会让你麻痹；对着成群的敌人放电最安全。", "When there are too few enemies nearby, the jumps that find no target come back into you, and enough of them paralyse you; it is safest to fire into a group.")) },
            { (WeaponForm.ArcCoil, 1), (ArenaText.Pair("拿在手上走路就会充电，身边有敌人时充得快；点击把电全部放出去，电击你周围的所有敌人，充满时放出去更痛。", "Charges as you walk with it in your hand, faster with enemies close by; click to release all of it and shock every enemy around you. A full charge hits harder."), ArenaText.Pair("满电后拿在手上 4 秒还不放，它会在你身上自动放电，电伤你和周围的敌人；充满了就尽快放出去。", "Keep a full charge in your hand for 4 s without releasing it and it discharges into you by itself, shocking you and the enemies around you; release a full charge soon.")) },
            { (WeaponForm.SporeLauncher, 0), (ArenaText.Pair("朝准星抛出孢囊，落地炸开一团毒云，在原地停留一阵，持续毒伤里面的敌人。它自带毒素属性。", "Lobs a pod at your aim that bursts into a poison cloud; the cloud lingers for a while and keeps poisoning the enemies inside it. It carries the Poison element."), ArenaText.Pair("毒云散去后会留下几颗孢子囊，谁踩上去，它就在谁脚下炸成一小团毒雾：敌人会中毒，你待在里面会掉血（不会中毒）。走路时绕开它们，或者把敌人引过去踩。", "When the cloud fades it leaves spore pods behind, and each bursts into a small poison mist under whoever steps on it: enemies are poisoned, and you lose HP while you stay in it (without being poisoned). Walk around them, or lead enemies over them.")) },
            { (WeaponForm.SporeLauncher, 1), (ArenaText.Pair("按住连射毒针，每一根都给敌人叠毒；一匣打完要换弹。", "Hold to fire poison needles; each one stacks Poison on the enemy. Reload when the clip runs out."), ArenaText.Pair("每次换弹时，拆下的旧毒囊会在你脚下炸成一小团毒雾：敌人碰到会中毒，你待在里面会掉血（不会中毒）；换弹时走出毒雾就不会受伤。", "Each reload, the spent venom sac bursts into a small poison mist at your feet: enemies in it are poisoned, and you lose HP while you stay in it (without being poisoned). Step out of the mist while you reload.")) },
            { (WeaponForm.Railgun, 0), (ArenaText.Pair("点一下开始蓄势，片刻后自动射出一道贯穿直线的轨道射线，无视护盾的减伤；越靠后的目标受到的伤害越低。", "Click to start a short wind-up, after which it fires by itself: a rail beam that pierces a straight line and ignores the Shield's damage cut. Each target further along takes less damage."), ArenaText.Pair("蓄势时你的移速减半；开火的后坐力会把你向后推出很远，可能把你推进怪堆或黑洞（背后有墙只会停下，不会撞伤）。开火前先看看身后。", "You move at half speed during the wind-up, and the recoil shoves you far back, possibly into a crowd or a black hole (a wall behind you just stops you and does not hurt). Check behind you before you fire.")) },
            { (WeaponForm.Railgun, 1), (ArenaText.Pair($"单发伤害极高，打中后生命低于 {P(Tuning.executeThreshold)}% 的非 Boss 敌人会被直接处决；这一发杀死敌人时，会立即返还部分冷却，并提升下一发伤害。", $"A single shot of very high damage; a non-boss enemy it leaves below {P(Tuning.executeThreshold)}% HP is executed outright. A shot that kills gives part of the cooldown back at once and makes the next shot hit harder."), ArenaText.Pair("这一发没有杀死任何敌人时（打空也算，打中 Boss 但没打死不算），多余的电能会回流电击你自己，并让枪进入长冷却。看准能打死的目标再开火。", "If a shot kills no enemy (a miss counts, but a boss it hits and does not kill does not), the surplus charge shocks you and the gun goes into a long cooldown. Fire at targets you can finish.")) },
            { (WeaponForm.Minigun, 0), (ArenaText.Pair("按住射击，枪管越转越快：刚开火时射速较慢，按住约 3 秒后达到最高射速。", "Hold to fire; the barrels spin faster and faster, from a slow start to full fire rate after about 3 s of holding."), ArenaText.Pair($"转得越快，准星转动就越慢，同时你会缓慢后退（转满时每秒约 {Tuning.spinUpRetreatSpeed:0.#} 格，背后有墙只会停下，不会撞伤）。需要大幅转向时，先松手再瞄准。", $"The faster it spins, the slower your aim can turn, and it slowly pushes you back (about {Tuning.spinUpRetreatSpeed:0.#} cells a second at full spin; a wall behind you just stops you and does not hurt). Let go before you need to swing your aim far.")) },
            { (WeaponForm.Minigun, 1), (ArenaText.Pair("一开火就是最高射速，但连续射击会不断积热；停火时热量会慢慢降下来。", "Full fire rate from the first shot, but sustained fire keeps building heat; the heat drains slowly while you are not firing."), ArenaText.Pair("热量积满时会在你身边爆炸，伤到你和周围的敌人，然后停火 3 秒；听到过热警告就先松手散热。", "When the heat maxes out it explodes around you, hurting you and the enemies nearby, then stops firing for 3 s; at the overheat warning, let go and let it cool.")) },
            { (WeaponForm.SwarmLauncher, 0), (ArenaText.Pair("射出一群微型导弹，自动追踪附近的敌人。", "Fires a swarm of micro-missiles that home in on nearby enemies."), ArenaText.Pair($"每枚导弹各锁定 {BarrageRange:0.#} 格内一个看得见的敌人；敌人比导弹少时，多出来的导弹里最多 {Tuning.barrageSelfLockMax} 枚会锁定你，飞出去后折返炸你，其余的直线飞走。射程内的敌人足够多，就不会有导弹折返。", $"Each missile locks a different visible enemy within {BarrageRange:0.#} cells; when there are fewer enemies than missiles, up to {Tuning.barrageSelfLockMax} of the spares lock onto you, fly out and turn back to blast you, and the rest fly straight on. With enough enemies in range, none come back.")) },
            { (WeaponForm.SwarmLauncher, 1), (ArenaText.Pair("从你面前开始，沿准星方向一路炸出一串爆炸，每个落点都会先出现标记。", "Sets off a string of blasts along your aim, starting just in front of you; each landing spot is marked first."), ArenaText.Pair("第一次爆炸就在你面前不远处，这些爆炸也会炸伤你并把你炸退；开火后站着别动或往旁边走，别往前冲。被炸退时撞上墙，还会再伤一次。", "The first blast lands just in front of you, and the blasts hurt you and knock you back too; after firing, stand still or step aside rather than walking forward. If one throws you into a wall, the wall hurts you again.")) },
            { (WeaponForm.OrbitBlades, 0), (ArenaText.Pair("两片刀刃绕着你旋转，碰到的敌人会受到伤害并被往外推开。", "Two blades circle you; enemies they touch take damage and are pushed outward."), ArenaText.Pair("刀刃还会把可拾取物（经验球、血球、能量球）和尸体往外拍飞，想捡的东西得追过去捡。", "They also knock pickups (XP, health and energy orbs) and corpses away from you, so you have to chase down whatever you want to collect.")) },
            { (WeaponForm.OrbitBlades, 1), (ArenaText.Pair("两片刀刃绕着你旋转，碰到的敌人会受到伤害并被往里拉。", "Two blades circle you; enemies they touch take damage and are pulled inward."), ArenaText.Pair("敌人会被一路拉到你身边，贴身的敌人更容易打中你。", "Enemies get dragged right up to you, where they can hit you more easily.")) },
            // Document 5.5: the auto weapon texts read their numbers from the live tuning.
            { (WeaponForm.SentryDrone, 0), (ArenaText.Pair($"无人机自动高速连射附近的敌人，一匣 {Tuning.gunnerClip} 发。", $"The drone fires rapidly at nearby enemies on its own, {Tuning.gunnerClip} rounds to a magazine."), ArenaText.Pair($"打空一匣后，无人机会掉在地上散热；走过去踩一下，它就会装满弹匣重新起飞，不踩的话要等 {N(Tuning.gunnerAutoReturn)} 秒。", $"When a magazine runs dry the drone drops to the ground to cool down; step on it to send it back up with a full magazine, otherwise it takes {N(Tuning.gunnerAutoReturn)} s to return.")) },
            { (WeaponForm.SentryDrone, 1), (ArenaText.Pair($"无人机用牵引光束抓住离你 {N(Tuning.whirlMinRange)}–{N(WhirlRange)} 格远、落单的敌人，把它拖过来砸在地上，它和落点附近的敌人都会受伤。", $"The drone's tractor beam drags a lone enemy {N(Tuning.whirlMinRange)}–{N(WhirlRange)} cells away, throws it and slams it down; it and the enemies where it lands take damage."), ArenaText.Pair($"敌人总会被砸在你 {N(Tuning.whirlAfterimageSeconds)} 秒前站的位置，那里会显示你的残影；站着不动，敌人就会落在你身边，所以要保持移动。", $"It always slams the enemy where you stood {N(Tuning.whirlAfterimageSeconds)} s ago, marked by your afterimage; stand still and the enemy lands right beside you, so keep moving.")) },
            { (WeaponForm.ShieldRing, 0), (ArenaText.Pair($"你身边始终有一道环状力场：圈内的敌人移速降低 {P(1f - Tuning.deflectSlow)}%，飞进圈内的敌方飞弹有 {P(Tuning.deflectChance)}% 的概率被偏折失效。圈内的敌人每 {N(Ring.cooldownA)} 秒受到 {N(Ring.damageA)} 点轻伤，给能量环附魔的属性就靠这一下叠到敌人身上。", $"A force field always surrounds you: enemies inside it move {P(1f - Tuning.deflectSlow)}% slower, and enemy projectiles that enter it have a {P(Tuning.deflectChance)}% chance to be deflected and fizzle. Enemies in the field take a light {N(Ring.damageA)} damage every {N(Ring.cooldownA)} s, which is how an infused element gets onto them."), ArenaText.Pair($"你从圈内射出的子弹和抛出的投掷物，穿过力场边缘时也有 {P(Tuning.deflectChance)}% 的概率被偏折、飞歪方向；轨道炮和小刀不受影响。", $"Your own bullets and lobbed shots leaving the field also have a {P(Tuning.deflectChance)}% chance to be knocked off course at its edge; the Railgun and the Knife are not affected.")) },
            { (WeaponForm.ShieldRing, 1), (ArenaText.Pair($"一段时间没有受到敌人攻击，你身上就会生成一层能量盾，能完整挡下一次攻击；每挡下一次，你获得 {N(Tuning.drainBlockEnergy)} 点能量。附魔后，能量盾每挡下一次攻击，还会对 {N(Tuning.drainInfuseRadius)} 格内的敌人放出一次带该属性的冲击。", $"After a while without an enemy hit, an Energy Shield forms on you and blocks one attack completely; each block gives you {N(Tuning.drainBlockEnergy)} Energy. Once infused, each block also releases a burst of its element on the enemies within {N(Tuning.drainInfuseRadius)} cells."), ArenaText.Pair($"能量盾被打碎后的 {N(Tuning.drainOverloadSeconds)} 秒内，如果再受到一次攻击，你会失去 {N(Tuning.drainOverloadEnergy)} 点能量（每次破碎只扣一次）；盾碎后先避开敌人。", $"If you take another hit within {N(Tuning.drainOverloadSeconds)} s after the shield breaks, you lose {N(Tuning.drainOverloadEnergy)} Energy (once per break); keep away from enemies right after it breaks.")) },
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

        /// <summary>How this anomaly attacks in a few words, its reach and then its manner ("远程 · 蓄力单发"), as the weapon crate's card shows
        /// it before the weapon is taken; empty for a form without one (the Pistol and the retired forms).</summary>
        public static string Style(WeaponForm form, int variant)
        {
            var (chinese, english) = StyleWords(form, variant);
            return chinese.Length == 0 ? "" : ArenaText.Pair(chinese, english);
        }

        /// <summary>A starting weapon's attack before it is taken, when which of its form's two anomalies comes is decided only then: both
        /// attacks, "A 或 B", the reach said once when the two share it ("远程 · 蓄力单发 或 按住连射").</summary>
        public static string EitherStyle(WeaponForm form)
        {
            var (c0, e0) = StyleWords(form, 0);
            var (c1, e1) = StyleWords(form, 1);
            if (c0.Length == 0 || c1.Length == 0) return Style(form, 0);
            if (c0 == c1) return ArenaText.Pair(c0, e0);
            return ArenaText.Pair(Either(c0, c1, " 或 "), Either(e0, e1, " or "));

            static string Either(string a, string b, string or)
            {
                const string Dot = " · ";
                int i = a.IndexOf(Dot, System.StringComparison.Ordinal), j = b.IndexOf(Dot, System.StringComparison.Ordinal);
                return i > 0 && j > 0 && a.Substring(0, i) == b.Substring(0, j) ? a + or + b.Substring(j + Dot.Length) : a + or + b;
            }
        }

        /// <summary>Style's words in both languages, read without the language settings.</summary>
        public static (string chinese, string english) StyleWords(WeaponForm form, int variant) =>
            Styles.TryGetValue((form, variant == 0 ? 0 : 1), out var words) ? words : ("", "");

        // A hand weapon's reach is its attack's reach in the roster (WeaponRoster.asset, rangeA and rangeB, with the Blizzard's spray cut at
        // RunWeaponsUpgrades.blizzardMaxRange): close up to 7 cells, mid range 8 to 15, long range from 20. The auto weapons say
        // "automatic"; the rocket launcher's two attacks say where they land instead of how far they reach, and the Capacitor that it
        // strikes all round. The manner is what the attack does in the code (RunWeapons.Fire).
        static readonly Dictionary<(WeaponForm, int), (string chinese, string english)> Styles = new()
        {
            { (WeaponForm.Rifle, 0), ("远程 · 蓄力单发", "Long range · charged shot") },
            { (WeaponForm.Rifle, 1), ("远程 · 按住连射", "Long range · hold to fire") },
            { (WeaponForm.Knife, 0), ("中距 · 掷出飞刃", "Mid range · thrown blade") },
            { (WeaponForm.Knife, 1), ("中距 · 钩住敌人", "Mid range · hook and swing") },
            { (WeaponForm.RocketLauncher, 0), ("定点 · 延时轰炸", "Targeted · delayed strike") },
            { (WeaponForm.RocketLauncher, 1), ("冲锋 · 撞伤沿途敌人", "Charge · rams enemies in the way") },
            { (WeaponForm.ScatterCannon, 0), ("近距 · 扇形散射", "Close · spread shot") },
            { (WeaponForm.ScatterCannon, 1), ("远程 · 蓄力连射", "Long range · charged burst") },
            { (WeaponForm.Flamethrower, 0), ("近距 · 按住喷火", "Close · hold to spray") },
            { (WeaponForm.Flamethrower, 1), ("中距 · 抛射火球", "Mid range · lobbed fireball") },
            { (WeaponForm.CryoCannon, 0), ("中距 · 三向冰锥", "Mid range · three-way shards") },
            { (WeaponForm.CryoCannon, 1), ("近距 · 按住喷雪", "Close · hold to spray") },
            { (WeaponForm.ArcCoil, 0), ("中距 · 连锁电击", "Mid range · chain lightning") },
            { (WeaponForm.ArcCoil, 1), ("周身 · 充能放电", "Around you · charged discharge") },
            { (WeaponForm.SporeLauncher, 0), ("中距 · 抛射毒云", "Mid range · lobbed cloud") },
            { (WeaponForm.SporeLauncher, 1), ("中距 · 按住连射", "Mid range · hold to fire") },
            { (WeaponForm.OrbitBlades, 0), ("自动 · 绕身刀刃", "Automatic · orbiting blades") },
            { (WeaponForm.OrbitBlades, 1), ("自动 · 绕身刀刃", "Automatic · orbiting blades") },
            { (WeaponForm.SentryDrone, 0), ("自动 · 无人机射击", "Automatic · drone fire") },
            { (WeaponForm.SentryDrone, 1), ("自动 · 无人机抓取", "Automatic · drone grab") },
            { (WeaponForm.ShieldRing, 0), ("自动 · 减速力场", "Automatic · slowing field") },
            { (WeaponForm.ShieldRing, 1), ("自动 · 挡下一击", "Automatic · blocks a hit") },
            { (WeaponForm.Minigun, 0), ("中距 · 按住扫射", "Mid range · hold to sweep") },
            { (WeaponForm.Minigun, 1), ("中距 · 按住扫射", "Mid range · hold to sweep") },
            { (WeaponForm.SwarmLauncher, 0), ("中距 · 追踪导弹", "Mid range · homing missiles") },
            { (WeaponForm.SwarmLauncher, 1), ("中距 · 连串爆炸", "Mid range · chain of blasts") },
            { (WeaponForm.Railgun, 0), ("远程 · 蓄力贯穿", "Long range · charged piercing beam") },
            { (WeaponForm.Railgun, 1), ("远程 · 重击单发", "Long range · heavy single shot") },
        };

        /// <summary>Document 5.4: a switch never clears what a trait has built up; stowed, it wears off. Kept beside the
        /// table so the sentence stays with the rule (RunWeapons.DrainStowedCapacitor).</summary>
        static string Stowed(WeaponForm form, int variant) => form == WeaponForm.ArcCoil && variant == 1
            ? ArenaText.Pair("收起来时电量会慢慢掉光，满电计时也会往回退，收着不会自己放电。", " Stowed, it slowly loses its charge and its full-charge timer runs back; it never discharges while stowed.")
            : "";

        static string Percent(float share) => (share * 100f).ToString("0.#");

        // ───── The moment the player's own weapon hurts them ─────

        /// <summary>One plain line for the moment the player's own weapon costs them HP (the hit's banner, the result screen's first line):
        /// what the trait did and how to keep clear of it, short enough for the banner's second line (about 34 Chinese or 95 English
        /// characters). wall: the HP went to a wall slam the weapon's blast or flight started. A weapon with no line of its own, and an
        /// unknown one (null form), gets the general line.</summary>
        public static string SelfHit(WeaponForm? form, int variant, bool wall) =>
            (form.HasValue ? SelfHitLine(form.Value, variant, wall) : null)
            ?? (wall ? ArenaText.Pair("这把武器的特性把你推去撞了墙，撞墙会受伤。", "The weapon's trait threw you into a wall, and walls hurt.")
                : ArenaText.Pair("这是这把武器的特性造成的。", "This comes from the weapon's trait."));

        /// <summary>The trait can cost the player HP (it has a line in the self-hit table): the weapon card's trait heading says so.</summary>
        public static bool HurtsYou(WeaponForm form, int variant) => SelfHitLine(form, variant, false) != null || SelfHitLine(form, variant, true) != null;

        /// <summary>The trait moves the player without hurting them: the Chain Blade's two-way pull, and the recoil of Focus, Lance and
        /// Spin-up. The weapon card's trait heading says so.</summary>
        public static bool MovesYou(WeaponForm form, int variant) => (form, variant == 0 ? 0 : 1) is (WeaponForm.Knife, 1) or (WeaponForm.ScatterCannon, 1)
            or (WeaponForm.Railgun, 0) or (WeaponForm.Minigun, 0);

        static string SelfHitLine(WeaponForm form, int variant, bool wall) => (form, variant == 0 ? 0 : 1, wall) switch
        {
            (WeaponForm.Knife, 0, _) => ArenaText.Pair("飞刃会飞回你出手时站的位置。掷出后走开，它就砍不到你。", "The blade flies back to where you stood when you threw it. Step away after you throw."),
            (WeaponForm.RocketLauncher, 0, false) => ArenaText.Pair("火箭落下时别站在标记圈里，爆炸不分敌我。", "Stay out of the marked circle when the rocket lands. The blast hits you too."),
            (WeaponForm.RocketLauncher, 0, true) => ArenaText.Pair("爆炸把你炸退撞上了墙。别在墙边朝自己脚下开火。", "The blast threw you back into a wall. Do not fire at your own feet near a wall."),
            (WeaponForm.RocketLauncher, 1, _) => ArenaText.Pair("化作火箭时撞上墙会受伤。出发前先看清前方有没有墙。", "Hitting a wall in flight hurts you. Check for walls ahead before you launch."),
            (WeaponForm.ScatterCannon, 0, _) => ArenaText.Pair("换弹时抛在两侧的弹壳会烧成火。换弹时别往两边走。", "The shells dropped at your sides on reload flare into fire. Do not sidestep while you reload."),
            (WeaponForm.Flamethrower, 0, _) => ArenaText.Pair("喷火时被敌人打中，背上的油箱炸开把你点着了。攻击要来时先松手。", "A hit while you sprayed burst your fuel tank and set you on fire. Let go when an attack comes."),
            (WeaponForm.Flamethrower, 1, _) => ArenaText.Pair("你自己的火也会烧你。别把火球扔在自己脚下。", "Your own fire burns you too. Do not throw the fireball at your feet."),
            (WeaponForm.ArcCoil, 0, _) => ArenaText.Pair("附近的敌人太少，跳不出去的电回到了你身上。对着成群的敌人放电。", "There were too few enemies nearby, so the unspent jumps came back into you. Fire into a group."),
            (WeaponForm.ArcCoil, 1, _) => ArenaText.Pair("满电后 4 秒不放，它就会在你身上放电。充满了就尽快放出去。", "A full charge held for 4 s discharges into you. Release it soon after it fills."),
            (WeaponForm.SporeLauncher, 0, _) => ArenaText.Pair("你踩爆了自己留下的孢子囊，待在毒雾里会掉血。走路时绕开它们。", "You burst one of your own spore pods. Its mist drains HP; walk around the pods."),
            (WeaponForm.SporeLauncher, 1, _) => ArenaText.Pair("换弹时脚下会炸开一团毒雾。换弹时走出去就不会受伤。", "Each reload bursts a poison mist at your feet. Step out of it while you reload."),
            (WeaponForm.Railgun, 1, _) => ArenaText.Pair("这一发没有杀死敌人，多余的电能电到了你。看准能打死的目标再开火。", "That shot killed nothing, so its surplus charge shocked you. Fire at targets you can finish."),
            (WeaponForm.Minigun, 1, _) => ArenaText.Pair("热量积满后会在你身边爆炸。听到过热警告就先松手散热。", "Full heat explodes around you. Let go at the overheat warning and let it cool."),
            (WeaponForm.SwarmLauncher, 0, _) => ArenaText.Pair("敌人比导弹少时，多出来的导弹会折返炸你。对着成群的敌人发射。", "With fewer enemies than missiles, the spare ones turn back on you. Fire at groups."),
            (WeaponForm.SwarmLauncher, 1, false) => ArenaText.Pair("这串爆炸也会炸到你。开火后站着别动或往旁边走，别往前冲。", "The blasts hurt you too. After firing, stand still or step aside; do not walk forward."),
            (WeaponForm.SwarmLauncher, 1, true) => ArenaText.Pair("爆炸把你往后炸退，撞上了身后的墙。别背靠着墙开火。", "The blast threw you back into the wall behind you. Do not fire with your back to a wall."),
            _ => null,
        };

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
