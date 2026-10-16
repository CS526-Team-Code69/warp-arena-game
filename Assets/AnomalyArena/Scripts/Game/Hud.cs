using UnityEngine;

namespace AnomalyArena
{

    public class Hud : MonoBehaviour
    {
        [Tooltip("The scene's retained Canvas HUD that draws all feedback.")]
        public V2Hud v2Hud;

        public void Toast(string text, Color c, float life = 2.2f)
        {
            if (v2Hud) v2Hud.ShowBanner(text, life);
        }

        public void Banner(string title, string sub)
        {
            if (v2Hud) v2Hud.ShowBanner($"{title}\n{sub}");
        }

        public void FlashDamage()
        {
            if (v2Hud) v2Hud.FlashDamage();
        }

        public void DamageNumber(Vector3 world, float amount, Color c, bool heavy)
        {
            if (amount <= 0f || !v2Hud) return;
            v2Hud.DamageNumber(world, Mathf.Max(1f, amount), c, heavy);
        }
    }
}
