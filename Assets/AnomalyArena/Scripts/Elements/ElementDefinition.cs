using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Element Definition")]
    public sealed class ElementDefinition : ScriptableObject
    {
        public ElementKind element;
        public string displayName;
        public Color colour = Color.white;
        public Texture2D icon;
        public bool paysWielder;
    }
}
