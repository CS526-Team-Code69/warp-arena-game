using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public sealed class TemporaryWall : MonoBehaviour
    {
        [Tooltip("Minimum seconds between two contact knockbacks on the same enemy.")] [Min(0f)]
        public float contactInterval = 0.5f;

        float life, push, age;
        readonly Dictionary<Combatant, float> nextPush = new();
        public bool BattlefieldFrozen { get; set; }

        public void Deploy(Vector3 at, float width, float duration, float contactKnockback, Combatant user)
        {
            push = contactKnockback;
            life = duration;
            age = 0f;
            nextPush.Clear();
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
            float dt = GameManager.Instance.WorldDeltaTime;
            age += dt;
            life -= dt;
            if (life > 0f) return;
            gameObject.SetActive(false);
            WorldMap.Active.HoleChanged();
        }

        void OnCollisionEnter(Collision collision) => KnockAway(collision);
        void OnCollisionStay(Collision collision) => KnockAway(collision);

        void KnockAway(Collision collision)
        {
            if (push <= 0f || BattlefieldFrozen) return;
            var game = GameManager.Instance;
            if (!game || !game.IsCombatActive) return;
            var enemy = Combatant.From(collision.collider);
            if (!enemy || !enemy.IsAlive || enemy.Team != Team.Enemy || enemy.State != CharacterState.Normal) return;
            if (nextPush.TryGetValue(enemy, out float allowedAt) && age < allowedAt) return;
            nextPush[enemy] = age + contactInterval;
            Vector3 normal = Query.Flat(transform.forward).normalized;
            float side = Vector3.Dot(Query.Flat(enemy.Position - transform.position), normal);
            enemy.Knockback(side < 0f ? -normal : normal, push, KnockKind.Hit);
        }
    }
}