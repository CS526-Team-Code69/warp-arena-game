using UnityEngine;

namespace AnomalyArena
{
    public class V2UiSlot : MonoBehaviour
    {
        public UnityEngine.UI.Text title, detail;
        public UnityEngine.UI.Image surface, icon, cooldown;
        public UnityEngine.UI.Image weaponIcon;
        public UnityEngine.UI.Image contentIcon;

        public void Render(ArenaUiSlot slot, V2ArtSet art)
        {
            if (slot == null) return;
            if (title.text != slot.title) title.text = slot.title;
            if (detail.text != slot.detail) detail.text = slot.detail;
            surface.color = surface.sprite
                ? slot.active ? Color.white : new Color(.65f, .75f, .85f, 1f)
                : slot.active ? new Color(.10f, .31f, .39f, .96f) : new Color(.04f, .08f, .13f, .94f);
            title.color = ArenaRarity.ColorFor(slot.rarity);
            cooldown.fillAmount = Mathf.Clamp01(slot.cooldown01);
            V2Hud.SetElementIcon(icon, art, slot.elementIndex);
            V2Hud.SetWeaponIcon(weaponIcon, art, slot.formIndex);
            V2Hud.SetContentIcon(contentIcon, art, slot.skillIndex, -1, -1);
            if (contentIcon && !slot.available) contentIcon.color = new Color(.48f, .55f, .63f);
        }
    }
}
