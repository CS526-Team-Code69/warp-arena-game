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

        // Counts each attack of this slot's weapon or cast of its skill. Written the moment it happens, not at the snapshot's 10 Hz: the view flashes when it moves.
        public int pulses;

        // Counts each time this slot's weapon cost the player HP (its own hit, burn or a wall slam its knock started), written the moment
        // it happens: the dock's slot flashes red when it moves.
        public int selfHits;
    }
}
