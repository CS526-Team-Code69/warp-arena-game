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
        public Vector3 Direction => direction;

        public bool IsPoisonSpike => owner && owner.definition && owner.definition.spikePoisonStacks > 0f;

        public void Launch(Enemy enemy, Vector3 at, Vector3 dir)
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
            gameObject.SetActive(true);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSpit, at);
        }

        public void Reflect(Combatant newOwner)
        {
            dealer = newOwner;
            targetTeam = Team.Enemy;
            direction = -direction;
            life = 6f;
        }

        void FixedUpdate()
        {
            if (!GameManager.Instance.IsCombatActive) return;
            float dt = GameManager.Instance.WorldFixedDeltaTime;
            Vector3 from = transform.position;
            Vector3 to = from + direction * (7f * dt);
            if (Query.WallBetween(from, to, out _) || (WorldMap.Active && WorldMap.Active.IsHole(to)))
            {
                gameObject.SetActive(false);
                return;
            }

            foreach (var character in Query.CharactersAlong(from, to, 0.2f))
            {
                if (character.Team != targetTeam || !character.IsAlive) continue;
                float beforeHp = character.Hp;
                var board = ElementStatus.For(character);
                float beforeShield = board ? board.Shield : 0f;
                bool atPlayer = targetTeam == Team.Player;

                var hit = DamageInfo.Attack((owner ? owner.attackDamage : 6f) * (atPlayer ? damageFactor : 1f), dealer,
                    direction,
                    atPlayer ? (owner ? owner.attackKnockback : 0f) : 3f, HitKind.Bullet);
                if (atPlayer && owner) hit.element = owner.Affix;
                if (character.ReceiveDamage(hit) && atPlayer && owner)
                {
                    if (owner.definition && owner.definition.spikePoisonStacks > 0f)
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