using UnityEngine;

namespace AnomalyArena
{

    public static class Art
    {
        public static bool Enabled;
        static bool v2ChoiceInitialized;
        static MaterialPropertyBlock mpb;

        public static void EnsureV2Default()
        {
            if (v2ChoiceInitialized) return;
            Enabled = false; v2ChoiceInitialized = true;
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
