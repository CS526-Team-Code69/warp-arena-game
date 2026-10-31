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
        bool lastV5;
        const float V5HandShare = .62f;
        void LateUpdate()
        {
            var active = weapons ? weapons.Active : null;
            int form = active != null ? (int)active.form : -1;
            bool valid = form >= 0 && form < formSprites.Length;
            bool daggerAway = active != null && active.form == WeaponForm.PhaseDagger && active.variant == 1
                && (active.inFlight || active.recallPlanted);
            if (illustration) illustration.enabled = valid && !daggerAway && Art.Enabled;
            if (whitebox) whitebox.enabled = valid && !daggerAway && !Art.Enabled;
            bool v5 = V5Vfx.On;
            if (!valid || !illustration || form == lastForm && v5 == lastV5) return;
            lastForm = form; lastV5 = v5; illustration.sprite = formSprites[form];

            if (illustration.sprite)
                illustration.transform.localScale = Vector3.one * ((v5 ? length * V5HandShare : length) / Mathf.Max(.01f, illustration.sprite.bounds.size.x));
        }
    }
}
