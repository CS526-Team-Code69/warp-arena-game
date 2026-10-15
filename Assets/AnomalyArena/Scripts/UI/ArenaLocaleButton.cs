using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.EventSystems;

namespace AnomalyArena
{
    public sealed class ArenaLocaleButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public string localeCode = "zh-Hans";
        UnityEngine.UI.Button button;
        void Awake() => button = GetComponent<UnityEngine.UI.Button>();
        public void OnPointerEnter(PointerEventData eventData) { if (button && button.IsInteractable()) V2Audio.Instance?.PlayUI(ArenaUiSound.Hover); }
        public void OnSelect(BaseEventData eventData) { if (button && button.IsInteractable()) V2Audio.Instance?.PlayUI(ArenaUiSound.Hover); }
        public void SelectLocale()
        {
            if (!ArenaText.Ready) return;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(localeCode));
            if (locale && locale != LocalizationSettings.SelectedLocale)
            {
                LocalizationSettings.SelectedLocale = locale;
                V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);
            }
        }
    }
}
