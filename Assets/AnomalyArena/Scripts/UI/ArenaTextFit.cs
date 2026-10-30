namespace AnomalyArena
{

    public static class ArenaTextFit
    {

        public static void Show(UnityEngine.UI.Text label, string text, int largest, int smallest)
        {
            if (!label) return;
            text ??= "";
            if (label.text == text && !label.resizeTextForBestFit) return;
            label.resizeTextForBestFit = false;
            label.text = text;
            float height = label.rectTransform.rect.height;
            int size = largest;
            for (; size > smallest; size--)
            {
                label.fontSize = size;
                if (label.preferredHeight <= height) break;
            }
            label.fontSize = size;
        }
    }
}
