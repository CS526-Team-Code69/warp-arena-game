namespace AnomalyArena
{
    /// <summary>
    /// Card texts show as large as their box allows. Unity's own best fit counts a run of Chinese without spaces as one
    /// word that has to fit on a single line, so a short Chinese card text fell to the smallest size on one line (probe on 2026-10-28: a
    /// 27-character sentence came out at size 10 on one line, where size 19 fits the same box in two lines). Here best fit stays off and
    /// the size is the largest, from largest down to smallest, at which the whole text fits the box with ordinary wrapping.
    /// </summary>
    public static class ArenaTextFit
    {
        /// <summary>Sets the text and picks its size; an unchanged text keeps the size picked for it.</summary>
        public static void Show(UnityEngine.UI.Text label, string text, int largest, int smallest)
        {
            if (!label) return;
            text ??= "";
            if (label.text == text && !label.resizeTextForBestFit) return;
            label.text = text;
            Refit(label, largest, smallest);
        }

        /// <summary>Picks the size for a text someone else has set.</summary>
        public static void Refit(UnityEngine.UI.Text label, int largest, int smallest)
        {
            if (!label) return;
            label.resizeTextForBestFit = false;
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
