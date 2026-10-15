using System;

namespace AnomalyArena
{
    [Serializable]
    public class ArenaUiSlot
    {
        public string title = "Empty";
        public string detail = "";
        public CardRarity rarity;
        public bool active;
        public bool available = true;
        public float cooldown01;
        public int elementIndex = -1;
        public int formIndex = -1;
        public int skillIndex = -1;
    }
}
