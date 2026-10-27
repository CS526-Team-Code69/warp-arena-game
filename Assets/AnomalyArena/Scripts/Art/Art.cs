using UnityEngine;

namespace AnomalyArena
{

    public static class Art
    {

        public const string IllustratedOnlyScene = "ArenaV2_CN";

        public const string EnemyLabScene = "EnemyLab";
        public static bool IsIllustratedOnlyScene(string sceneName) => sceneName is IllustratedOnlyScene or EnemyLabScene;

        public static bool Enabled;

        public static bool IllustratedOnly { get; private set; }
        static bool v2ChoiceInitialized;

        static bool? toggleChoice;
        static MaterialPropertyBlock mpb;

        public static void EnsureV2Default(bool illustratedOnly = false)
        {
            IllustratedOnly = illustratedOnly;
            if (illustratedOnly)
            {
                toggleChoice ??= Enabled;
                Enabled = true;
                return;
            }
            if (toggleChoice.HasValue) { Enabled = toggleChoice.Value; toggleChoice = null; }
            if (v2ChoiceInitialized) return;
            Enabled = false; v2ChoiceInitialized = true;
        }

        public static void Toggle()
        {
            if (!IllustratedOnly) Enabled = !Enabled;
        }

        public static void Tint(Renderer r, Color c)
        {
            if (!r) return;
            mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
        }
    }
}
