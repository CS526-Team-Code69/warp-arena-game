using UnityEngine;

namespace AnomalyArena
{
    public sealed class EnemyBolt : MonoBehaviour
    {
        Vector3 direction;
        Enemy owner;
        Combatant dealer;
        Team targetTeam = Team.Player;
        float life, damageFactor = 1f;
                                                                                                              
        float shotDamage = -1f, shotKnockback = -1f, shotSpeed = -1f;
                                                                                                                       
        float trackTurn, trackLeft;
        public Vector3 Direction => direction;
                                                                                                                                  
        float Speed => shotSpeed > 0f ? shotSpeed : owner && owner.definition ? Mathf.Max(.1f, owner.definition.projectileSpeed) : 7f;
                                                                                                                         
        public bool IsBossShot => shotSpeed > 0f;
                                                                                                         
        public bool Hostile => targetTeam == Team.Player;
                                                                            
        public const float HitRadius = .2f;
        static readonly Color BossShotColour = new(.93f, .82f, 1f);
        bool tinted;

                                                                                                                                 
        public bool IsPoisonSpike => !IsBossShot && owner && owner.definition && owner.definition.spikePoisonStacks > 0f;

        public void Launch(Enemy enemy, Vector3 at, Vector3 dir)
        {
            shotDamage = shotKnockback = shotSpeed = -1f;
            if (tinted && TryGetComponent(out Renderer body)) body.SetPropertyBlock(null);
            tinted = false;
            LaunchFrom(enemy, at, dir, true);
        }

                                                                                  
        public void Launch(Enemy enemy, Vector3 at, Vector3 dir, float damage, float knockback, float speed)
        {
            shotDamage = Mathf.Max(0f, damage);
            shotKnockback = Mathf.Max(0f, knockback);
            shotSpeed = Mathf.Max(.1f, speed);
            if (TryGetComponent(out Renderer body)) { Art.Tint(body, BossShotColour); tinted = true; }
            LaunchFrom(enemy, at, dir, false);                                                       
        }

                                                                                                                             
                                                                                                       
        public void Track(float turnDegreesPerSecond, float seconds)
        {
            trackTurn = Mathf.Max(0f, turnDegreesPerSecond);
            trackLeft = trackTurn > 0f ? Mathf.Max(0f, seconds) : 0f;
        }

        void LaunchFrom(Enemy enemy, Vector3 at, Vector3 dir, bool sound)
        {
            owner = enemy;
            dealer = enemy;
            targetTeam = Team.Player;
            damageFactor =
                enemy
                    ? enemy.OutgoingDamageFactor
                    : 1f;                                                                  
            transform.position = Query.AtCastHeight(at);
            direction = Query.Flat(dir).normalized;
            life = 6f;
            trackTurn = trackLeft = 0f;
            gameObject.SetActive(true);
            if (sound) V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSpit, at);
        }

                                                                                                              
        public void Burn() => gameObject.SetActive(false);

        public void Reflect(Combatant newOwner)
        {
            dealer = newOwner;
            targetTeam = Team.Enemy;
            direction = -direction;
            trackLeft = 0f;
            life = 6f;
        }

        void FixedUpdate()
        {
            if (!GameManager.Instance.IsCombatActive) return;
            float dt = GameManager.Instance.WorldFixedDeltaTime;
            Vector3 from = transform.position;
                                                                                                                                
            if (trackLeft > 0f)
            {
                trackLeft -= dt;
                var target = GameManager.Instance.player;
                if (Hostile && target && target.IsAlive)
                {
                    var body = target.GetComponent<Rigidbody>();
                    Vector3 meet = Enemy.VolleyLeadPoint(from, target.Position, body ? body.linearVelocity : Vector3.zero, Speed);
                    direction = Query.Steer(direction, meet - Query.Flat(from), trackTurn * dt);
                }
            }

            Vector3 to = from + direction * (Speed * dt);
            if (Query.WallBetween(from, to, out _) || (WorldMap.Active && WorldMap.Active.IsHole(to)))
            {
                gameObject.SetActive(false);
                return;
            }

                                                                                                                                
            var world = WorldMap.Active;
            if (world && world.TrialActive && world.trialPlatform &&
                Query.Flat(to - world.trialPlatform.position).magnitude > world.TrialGroundReach + 2f)
            {
                gameObject.SetActive(false);
                return;
            }

            foreach (var character in Query.CharactersAlong(from, to, HitRadius))
            {
                if (character.Team != targetTeam || !character.IsAlive) continue;
                                                                                                  
                if (targetTeam == Team.Player && world && world.PlayerHidden) continue;
                float beforeHp = character.Hp;
                var board = ElementStatus.For(character);
                float beforeShield = board ? board.Shield : 0f;
                bool atPlayer = targetTeam == Team.Player;
                                                                                                                                   
                float damage = shotDamage >= 0f ? shotDamage : owner ? owner.attackDamage : 6f;
                float push = shotKnockback >= 0f ? shotKnockback : owner ? owner.attackKnockback : 0f;
                var hit = DamageInfo.Attack(damage * (atPlayer ? damageFactor : 1f), dealer, direction, atPlayer ? push : 3f,
                    HitKind.Bullet);
                if (atPlayer && owner) hit.element = owner.Affix;
                if (character.ReceiveDamage(hit) && atPlayer && owner)
                {
                    if (!IsBossShot && owner.definition && owner.definition.spikePoisonStacks > 0f)
                        ElementCombat.ApplyStacks(character, ElementKind.Poison, owner.definition.spikePoisonStacks,
                            owner);
                    if (owner.Affix != ElementKind.None)
                        ElementCombat.ApplyAffix(owner, character, owner.Affix,
                            Mathf.Max(0f, beforeHp - character.Hp) +
                            Mathf.Max(0f, beforeShield - (board ? board.Shield : 0f)));
                }

                gameObject.SetActive(false);
                return;
            }

            transform.position = to;
            life -= dt;
            if (life <= 0f) gameObject.SetActive(false);
        }
    }
}