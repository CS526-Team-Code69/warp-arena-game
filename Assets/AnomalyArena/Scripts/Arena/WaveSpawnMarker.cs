using UnityEngine;

namespace AnomalyArena
{

    public sealed class WaveSpawnMarker : MonoBehaviour
    {
        public Renderer visual;
        public EnemyDefinition definition;
        public bool elite;
        public ElementKind affix;
        public float timer;

        public bool bossSummon;
        public void Begin(Vector3 position, EnemyDefinition enemy, bool isElite, ElementKind element, bool summon = false)
        {
            definition = enemy;
            elite = isElite;
            affix = element;
            bossSummon = summon;
            timer = enemy.kind == EnemyKind.Shade ? 1.5f : 1f;
            transform.position = new Vector3(position.x, 0.04f, position.z);
            transform.localScale = Vector3.one * Mathf.Max(1.2f, enemy.radius * 2.4f);
            if (visual) Art.Tint(visual, enemy.kind == EnemyKind.Shade ? new Color(0.65f, 0.45f, 1f) : new Color(1f, 0.2f, 0.25f));
            gameObject.SetActive(true);
        }
    }
}
