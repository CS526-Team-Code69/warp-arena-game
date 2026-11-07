using System;

namespace AnomalyArena
{

    public static class CampaignEvents
    {

        public static event Action<int, bool> DayStarted;

        public static event Action<string, int> PhaseChanged;

        public static event Action<float, float, CampaignRoom> BaseDamaged;

        public static event Action<CampaignCost, int> NightDeath;

        public static event Action<CampaignCost> Spent;

        public static event Action<string, string, CampaignRoom, CampaignCost, int> Purchased;

        public static event Action<int, CampaignCost, CampaignCost> Dawn;

        public static event Action<string, int> Ended;

        internal static void RaiseDayStarted(int day, bool overtime) => DayStarted?.Invoke(day, overtime);
        internal static void RaisePhase(string phase, int day) => PhaseChanged?.Invoke(phase, day);
        internal static void RaiseBaseDamaged(float amount, float toHp, CampaignRoom room) => BaseDamaged?.Invoke(amount, toHp, room);
        internal static void RaiseNightDeath(CampaignCost lost, int day) => NightDeath?.Invoke(lost, day);
        internal static void RaiseSpent(CampaignCost cost) => Spent?.Invoke(cost);
        internal static void RaisePurchased(string item, string detail, CampaignRoom room, CampaignCost cost, int day) => Purchased?.Invoke(item, detail, room, cost, day);
        internal static void RaiseDawn(int day, CampaignCost wallet, CampaignCost earned) => Dawn?.Invoke(day, wallet, earned);
        internal static void RaiseEnded(string cause, int day) => Ended?.Invoke(cause, day);
    }
}
