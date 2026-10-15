using UnityEngine;

namespace AnomalyArena
{
    [DefaultExecutionOrder(1200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class V2EnemyThreatFeedback : MonoBehaviour
    {
        public Enemy enemy;
        [Tooltip("Saved 3D model and affix renderers; no visual objects are created at runtime.")]
        public Renderer[] surfaces;
        public SpriteRenderer legacyIllustration;
        [Min(.1f)] public float flashesPerSecond = 6f;
        public Color warningEmission = new Color(1.2f, .015f, .01f);
        public Color warningBodyColor = new Color(1f, .025f, .02f);
        MaterialPropertyBlock block;
        Color[] previousBase, previousEmission;
        bool[] activeMaterial, emissionMaterial;
        bool wasWarning;
        float warningClock;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            if (!enemy) enemy = GetComponent<Enemy>();
            block = new MaterialPropertyBlock();
            int count = surfaces?.Length ?? 0;
            previousBase = new Color[count]; previousEmission = new Color[count];
            activeMaterial = new bool[count]; emissionMaterial = new bool[count];
            for (int i=0; i<count; i++)
            {
                var surface = surfaces[i]; var material = surface ? surface.sharedMaterial : null;
                activeMaterial[i] = material;
                emissionMaterial[i] = material && material.IsKeywordEnabled("_EMISSION");
            }
        }
        void OnEnable() { warningClock = 0f; }
        void OnDisable() { RestoreWhitebox(); wasWarning = false; warningClock = 0f; }

        void LateUpdate()
        {
            bool warning = enemy && enemy.BloaterWarningActive;
            if (!warning)
            {
                if (wasWarning) RestoreWhitebox();
                wasWarning = false; warningClock = 0f; return;
            }
            var gm = GameManager.Instance;
            warningClock += gm ? gm.WorldDeltaTime : Time.deltaTime;
            float wave = Mathf.Sin(warningClock * flashesPerSecond * Mathf.PI);
            float pulse = .3f + .7f * wave * wave;
            for (int i=0; i<(surfaces?.Length ?? 0); i++)
            {
                var surface = surfaces[i];
                if (!surface || !activeMaterial[i]) continue;
                surface.GetPropertyBlock(block);
                if (emissionMaterial[i])
                {
                    block.SetColor(Emission, block.GetColor(Emission) + warningEmission * pulse);
                }
                else
                {
                    if (!wasWarning)
                    {
                        previousBase[i] = block.GetColor(BaseColor);
                        if (previousBase[i] == default) previousBase[i] = surface.sharedMaterial.color;
                        previousEmission[i] = block.GetColor(Emission);
                    }
                    block.SetColor(BaseColor, Color.Lerp(previousBase[i], warningBodyColor, pulse));
                }
                surface.SetPropertyBlock(block);
            }
            if (legacyIllustration && legacyIllustration.enabled)
                legacyIllustration.color = Color.Lerp(legacyIllustration.color, warningBodyColor, pulse);
            wasWarning = true;
        }
        void RestoreWhitebox()
        {
            if (!wasWarning || block == null || surfaces == null) return;
            for (int i=0; i<surfaces.Length; i++)
            {
                if (!surfaces[i] || emissionMaterial[i]) continue;
                surfaces[i].GetPropertyBlock(block); block.SetColor(BaseColor, previousBase[i]); block.SetColor(Emission, previousEmission[i]);
                surfaces[i].SetPropertyBlock(block);
            }
        }
    }
}
