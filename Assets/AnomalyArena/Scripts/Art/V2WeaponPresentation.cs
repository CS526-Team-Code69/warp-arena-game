using UnityEngine;

namespace AnomalyArena
{
    public class V2WeaponPresentation : MonoBehaviour
    {
        public RunWeapons weapons;
        public SpriteRenderer illustration;
        public Renderer whitebox;
        [Tooltip("WeaponForm enum order; one image for both variants.")]
        public Sprite[] formSprites = new Sprite[17];
        public float length = 2.2f;
        int lastForm = -1;
        void LateUpdate()
        {
            var active = weapons ? weapons.Active : null;
            int form = active != null ? (int)active.form : -1;
            bool valid = form >= 0 && form < formSprites.Length;
            bool daggerAway = active != null && active.form == WeaponForm.PhaseDagger && active.variant == 1
                && (active.inFlight || active.recallPlanted);
            if (illustration) illustration.enabled = valid && !daggerAway && Art.Enabled;
            if (whitebox) whitebox.enabled = valid && !daggerAway && !Art.Enabled;
            if (!valid || form == lastForm || !illustration) return;
            lastForm = form; illustration.sprite = formSprites[form];
            if (illustration.sprite)
                illustration.transform.localScale = Vector3.one * (length / Mathf.Max(.01f, illustration.sprite.bounds.size.x));
        }
    }
}
