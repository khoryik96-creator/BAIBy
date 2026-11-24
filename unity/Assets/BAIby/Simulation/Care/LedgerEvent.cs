using System;

namespace BAIby.Simulation
{
    // What the ledger resolved during one CareRules.Advance, in time order. Not saved. Bond losses, the care rhythm
    // and temperament (BondRules) read these, so a long advance and many short ones see the same days.
    public enum LedgerEventKind { MealMissed, DayClosed, HelperDays }

    // How one care day went, as its ledger closed it.
    public readonly struct CareDaySummary
    {
        // Meals given on time or late, and meals missed.
        public int MealsDone { get; }
        public int MealsMissed { get; }
        public bool Together { get; }
        // Put to bed on time or late.
        public bool Bedtime { get; }
        // A window was covered by a helper (past the absence ceiling), the day overlapped Away mode, or a window was
        // excused (the first day, a travel day, a migration).
        public bool HelperCovered { get; }
        public bool Away { get; }
        public bool Excused { get; }

        public CareDaySummary(int mealsDone, int mealsMissed, bool together, bool bedtime, bool helperCovered, bool away,
            bool excused)
        {
            MealsDone = mealsDone;
            MealsMissed = mealsMissed;
            Together = together;
            Bedtime = bedtime;
            HelperCovered = helperCovered;
            Away = away;
            Excused = excused;
        }
    }

    public readonly struct LedgerEvent
    {
        public LedgerEventKind Kind { get; }
        // UTC ticks: when a missed window closed, when a day ended, or when the first of several helper days ended.
        public long At { get; }
        // The care day the event belongs to (the first one for HelperDays).
        public long CareDay { get; }
        // HelperDays: whole care days a helper covered, ending a day apart from At.
        public int Count { get; }
        public CareDaySummary Day { get; }

        public LedgerEvent(LedgerEventKind kind, long at, long careDay, int count = 0, CareDaySummary day = default)
        {
            if (!Enum.IsDefined(typeof(LedgerEventKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));

            Kind = kind;
            At = at;
            CareDay = careDay;
            Count = count;
            Day = day;
        }
    }
}
