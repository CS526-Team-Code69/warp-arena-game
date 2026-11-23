namespace AnomalyArena
{
    public partial class Enemy
    {
        const float DevourHintSeconds = 6f;

        // Whether this fight has said what an Astral Guard does; ConfigureTyrant clears it for each fight.
        bool devourTold;

        /// <summary>Tests: this fight has queued the devour hint.</summary>
        public bool DevourTold => devourTold;

        /// <summary>The Tyrant devours its crowd only while the player stands far from it (TyrantMoveChoice), and nothing else in the
        /// fight says so: a player who keeps away can watch its HP come back again and again. The first Astral Guard of a fight that
        /// devours anything says what happened and how to stop it. It goes through the notices' queue (V2WorldLabels.Notice) ahead of
        /// the notices waiting, so the first-sight notices of the Tyrant's crowd neither replace it nor are replaced by it.</summary>
        void TellDevourOnce()
        {
            if (devourTold) return;
            devourTold = true;
            V2WorldLabels.Notice(DevourHint, DevourHintSeconds, true);
        }

        /// <summary>The devour hint: a title and a line.</summary>
        public static string DevourHint() =>
            ArenaText.Pair("暴虐君王吞噬了身边的怪物", "THE TYRANT KING DEVOURS ITS MINIONS") + "\n" +
            ArenaText.Pair("你离它太远时，它会吞掉身边的怪物，回复生命和护盾。靠近它打。",
                "While you stay far away it swallows the monsters around it to restore HP and Shield. Fight it up close.");
    }
}
