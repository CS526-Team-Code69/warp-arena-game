using System;

namespace AnomalyArena
{
    [Serializable]
    public class ArenaUiCard
    {
        public string title = "";
        public string body = "";
        public string kind = "";
        public string badge = "";
        public CardRarity rarity;
        public bool available = true;
        public int elementIndex = -1;
        public int formIndex = -1;
        public int skillIndex = -1;
        public int statIndex = -1;

        public int axisIndex = -1;
    }
}
