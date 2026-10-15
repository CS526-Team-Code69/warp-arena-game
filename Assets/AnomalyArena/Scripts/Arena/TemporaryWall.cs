using UnityEngine;

namespace AnomalyArena
{
    public sealed class TemporaryWall : MonoBehaviour
    {
        float life, push;
        Combatant owner;
        public bool BattlefieldFrozen { get; set; }
        public void Deploy(Vector3 at, float width, float duration, float endKnockback, Combatant user)
        {
            owner = user;
            push = endKnockback;
            life = duration;
            transform.position = at;
            transform.rotation = Quaternion.LookRotation(Query.Flat(user.AimDirection));
            transform.localScale = new Vector3(width, 2f, 1f);
            gameObject.SetActive(true);
            foreach (var combatant in Query.Characters(at, width * 0.5f + 1f))
            {
                Vector3 direction = Query.Flat(combatant.Position - at);
                combatant.Push(direction, 1.5f);
            }
            WorldMap.Active.HoleChanged();
        }
        void Update()
        {
            if (BattlefieldFrozen || !GameManager.Instance.IsCombatActive) return;
            life -= GameManager.Instance.WorldDeltaTime;
            if (life > 0f) return;
            if (push > 0f)
                foreach (var combatant in Query.Characters(transform.position, transform.localScale.x * 0.5f + 1f))
                    combatant.ReceiveDamage(DamageInfo.Attack(0, owner, combatant.Position - transform.position, push));
            gameObject.SetActive(false);
            WorldMap.Active.HoleChanged();
        }
    }
}
