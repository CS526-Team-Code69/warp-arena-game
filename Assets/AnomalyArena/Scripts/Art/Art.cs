using UnityEngine;

namespace AnomalyArena
{

    public static class Art
    {

        public const string ChineseEditionScene = "ArenaV2_CN";

        public const string EnemyLabScene = "EnemyLab";

        public enum Edition { None, English, Chinese, EnemyLab }

        public static Edition EditionOf(string sceneName) => sceneName switch
        {
            ChineseEditionScene => Edition.Chinese,
            EnemyLabScene => Edition.EnemyLab,
            _ => Edition.English,
        };

        public readonly struct SceneArt
        {
            public readonly Edition edition;
            public SceneArt(Edition edition) => this.edition = edition;
            public bool IllustratedOnly => edition == Edition.EnemyLab;
            public static implicit operator bool(SceneArt art) => art.IllustratedOnly;
        }

        public static SceneArt IsIllustratedOnlyScene(string sceneName) => new SceneArt(EditionOf(sceneName));

        public static bool Enabled;

        public static bool IllustratedOnly { get; private set; }

        public static Edition RunningEdition { get; private set; }

        static bool v2ChoiceInitialized, chineseInitialized;
        static bool? toggleChoice, chineseChoice;
        static MaterialPropertyBlock mpb;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            Enabled = false;
            IllustratedOnly = false;
            RunningEdition = Edition.None;
            v2ChoiceInitialized = chineseInitialized = false;
            toggleChoice = chineseChoice = null;
        }

        public static void EnsureV2Default(SceneArt scene) => Begin(scene.edition);

        public static void EnsureV2Default(bool illustratedOnly = false) => Begin(illustratedOnly ? Edition.EnemyLab : Edition.English);

        static void Begin(Edition next)
        {
            if (next == Edition.None) next = Edition.English;
            if (RunningEdition != next)
            {

                if (RunningEdition == Edition.English) toggleChoice = Enabled;
                else if (RunningEdition == Edition.Chinese) chineseChoice = Enabled;
            }
            RunningEdition = next;
            IllustratedOnly = next == Edition.EnemyLab;
            switch (next)
            {
                case Edition.EnemyLab:
                    Enabled = true;
                    break;
                case Edition.Chinese:
                    if (chineseChoice.HasValue) { Enabled = chineseChoice.Value; chineseChoice = null; }
                    if (!chineseInitialized) { Enabled = true; chineseInitialized = true; }
                    break;
                default:
                    if (toggleChoice.HasValue) { Enabled = toggleChoice.Value; toggleChoice = null; }
                    if (!v2ChoiceInitialized) { Enabled = false; v2ChoiceInitialized = true; }
                    break;
            }
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
