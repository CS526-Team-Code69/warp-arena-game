namespace AnomalyArena
{
                 
                                                                                                                                          
                                                                                                                                         
                                                                                                                                         
                                                                                                                            
                  
    public static class ArenaTextFit
    {
                                                                                                                
        public static void Show(UnityEngine.UI.Text label, string text, int largest, int smallest)
        {
            if (!label) return;
            text ??= "";
            if (label.text == text && !label.resizeTextForBestFit) return;
            label.text = text;
            Refit(label, largest, smallest);
        }

                                                                              
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
