using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture, Category("TtkBalance")]
    public class EncounterBalanceTest
    {
        const string Scene="Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        [TearDown] public void TearDown()=>Time.timeScale=1f;

        [TestCase(WeaponForm.Rifle,0),TestCase(WeaponForm.Rifle,1),TestCase(WeaponForm.Knife,0)]
        [TestCase(WeaponForm.RocketLauncher,0),TestCase(WeaponForm.RocketLauncher,1)]
        [LoadScene(Scene),Timeout(40000)]
        public async Task Starter_AttackInputToFodderDeath_RecordsRealTimeline(WeaponForm form,int variant)
        {
            var sandbox=await V3CombatSmokeTest.StartSandbox();var game=sandbox.game;var target=sandbox.targets[0];
            foreach(var other in sandbox.targets.Skip(1))other.DiscardWithoutReward();
            target.Configure(game.waves.roster.First(e=>e.kind==EnemyKind.Grunt),1f,false);
            target.enabled=false;target.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;
            V3CombatSmokeTest.Teleport(target,game.player.Position+Vector3.forward*6f);
            game.runWeapons.ClearWaveEffects();game.runWeapons.Auto=new RunWeapon[3];
            var weapon=new RunWeapon{form=form,variant=variant,revealed=true,rounds=4};
            game.runWeapons.Hand[game.runWeapons.ActiveIndex]=weapon;
            Physics.SyncTransforms();await Awaitable.FixedUpdateAsync();
            float start=Time.time,deadline=Time.realtimeSinceStartup+6f;
            while(target.IsAlive)
            {
                Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"starter must finish the target");
                Vector3 aim=Query.Flat(target.Position-game.player.Position).normalized;
                if(variant==1&&form==WeaponForm.Rifle) game.player.DebugSetAim(-aim);
                else game.player.DebugSetAimPoint(target.Position);
                if(form==WeaponForm.Rifle&&variant==0&&weapon.charging)
                    game.runWeapons.HandleInput(false,weapon.charge<.99f,weapon.charge>=.99f);
                else game.runWeapons.HandleInput(true,true,false);
                await Awaitable.NextFrameAsync();
            }
            float seconds=Time.time-start;
            Directory.CreateDirectory("work/authoring-receipts");
            File.AppendAllText("work/authoring-receipts/ttk-runtime.csv",FormattableString.Invariant($"{form},{variant},{target.maxHp},{seconds}\n"));
            Assert.That(target.maxHp,Is.EqualTo(5f));

            Assert.That(seconds,Is.InRange(.4f,.95f),"input-to-death with one saved target six units away");
        }

        [Test,LoadScene(Scene),Timeout(40000)]
        public async Task AuthoredWaveBudgets_UseRealHpAndShield_WithoutReadingLiveBuild()
        {
            var sandbox=await V3CombatSmokeTest.StartSandbox();var game=sandbox.game;
            var target=sandbox.targets[0];var balance=game.waves.waveSet.encounterBalance;
            var waveField=typeof(WaveManager).GetField("<WaveIndex>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(int wave in new[]{1,6,10,15})
            {
                waveField.SetValue(game.waves,wave-1);
                foreach(var data in game.waves.roster)
                {
                    bool elite=data.canElite&&data.kind is not (EnemyKind.Grunt or EnemyKind.Skitter);
                    if(elite&&wave<6)continue;
                    target.Configure(data,game.waves.HpMultiplier,elite);target.enabled=false;
                    float actualShield=ElementStatus.For(target).Shield;
                    float phases=data.kind==EnemyKind.Tyrant?target.maxHp*balance.tyrantPhaseShieldShare*2f:0f;
                    float modeled=(target.maxHp+(actualShield+phases)/balance.ShieldDirectFactor)/balance.DpsAt(wave);
                    if(target.IsBoss)Assert.That(modeled,Is.InRange(45f,75f),data.kind+" boss budget");
                    else if(elite)Assert.That(modeled,Is.InRange(5f,10f),data.kind+" elite budget");
                    else if(data.kind is EnemyKind.Grunt or EnemyKind.Skitter)Assert.That(modeled,Is.InRange(.5f,.8f));
                    float hp=target.maxHp;game.progression.Stats.damageBonus+=1f;
                    target.Configure(data,game.waves.HpMultiplier,elite);target.enabled=false;
                    Assert.That(target.maxHp,Is.EqualTo(hp),"a player's upgrade must not inflate enemies");
                }
                float scale=game.waves.CalculateExperienceRewardScale();
                Assert.That(float.IsFinite(scale)&&scale>0f,Is.True,"finite positive XP reward correction");
            }
        }

        [Test,LoadScene(Scene),Timeout(40000)]
        public async Task TimeSlipCleanup_AndImmovableBossPull_RespectGameplayState()
        {
            var sandbox=await V3CombatSmokeTest.StartSandbox();var game=sandbox.game;
            game.skills.Learn(SkillId.TimeSlip);Assert.That(game.skills.TryCast(0),Is.True);
            Assert.That(game.WorldTimeScale,Is.LessThan(1f));game.skills.ClearWaveEffects();
            Assert.That(game.WorldTimeScale,Is.EqualTo(1f),"slow time must not leak into the next wave");
            var boss=sandbox.targets[0];boss.Configure(game.waves.roster.First(e=>e.kind==EnemyKind.Hollow),1f,false);boss.enabled=false;
            Vector3 before=boss.Position;SkillWorldAPI.Pull(boss,before+Vector3.forward*10f,5f);
            await Awaitable.FixedUpdateAsync();
            Assert.That(Query.Flat(boss.Position-before).magnitude,Is.LessThan(.01f),"boss is immovable");
        }
    }
}
