using UnityEngine;

namespace AnomalyArena
{
    public sealed class EnemyBolt : MonoBehaviour
    {
        Vector3 direction;
        Enemy owner;
        Combatant dealer;
        Team targetTeam = Team.Player;
        float life;
        public void Launch(Enemy enemy, Vector3 at, Vector3 dir)
        {
            owner = enemy;
            dealer = enemy;
            targetTeam = Team.Player;
            transform.position = Query.AtCastHeight(at);
            direction = Query.Flat(dir).normalized;
            life = 6f;
            gameObject.SetActive(true);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSpit,at);
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
            if (Query.WallBetween(from, to, out _) || (WorldMap.Active && WorldMap.Active.IsHole(to))) { gameObject.SetActive(false); return; }
            foreach (var character in Query.CharactersAlong(from, to, 0.2f))
            {
                if (character.Team != targetTeam || !character.IsAlive) continue;
                float beforeHp = character.Hp;
                var board = ElementStatus.For(character);
                float beforeShield = board ? board.Shield : 0f;
                var hit = DamageInfo.Attack(owner ? owner.attackDamage : 6f, dealer, direction, 3f, HitKind.Bullet);
                if (targetTeam == Team.Player && owner) hit.element = owner.Affix;
                if (character.ReceiveDamage(hit) && targetTeam == Team.Player && owner && owner.Affix != ElementKind.None)
                    ElementCombat.ApplyAffix(owner, character, owner.Affix, Mathf.Max(0f, beforeHp - character.Hp) + Mathf.Max(0f, beforeShield - (board ? board.Shield : 0f)));
                gameObject.SetActive(false);
                return;
            }
            transform.position = to;
            life -= dt;
            if (life <= 0f) gameObject.SetActive(false);
        }
    }
}
