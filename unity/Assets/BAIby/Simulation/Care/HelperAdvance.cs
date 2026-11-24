using System;

namespace BAIby.Simulation
{
    // One gap with helpers, shared by every companion with a care record (BabySimulation and PetCare). The needs come from
    // BabyNeeds with a HelperRun; the ledger is CareRules' own, advanced again with the feeder's serves when there were
    // any, so the meal windows they served in are Helped. Bond, temperament and the rhythm read the ledger's events,
    // which are the same with or without helpers (a helped meal is still a missed meal for them).
    internal static class HelperAdvance
    {
        public readonly struct Gap
        {
            public readonly CareRecord Care;
            public readonly BabyNeeds.Values Needs;
            public readonly HelperRun Run;

            public Gap(CareRecord care, BabyNeeds.Values needs, HelperRun run)
            {
                Care = care;
                Needs = needs;
                Run = run;
            }
        }

        // `plain` is CareRules.Advance(from, until) without serves. With no helper that may act this is exactly the gap
        // without helpers.
        public static Gap Real(CareRecord from, CareRecord plain, DateTimeOffset until, BabyNeeds.Values start, TimeSpan cap,
            CareTunables care, BabyNeeds.Effects effects, BabyNeeds.NeedRates rates, HelperSettings settings,
            HelperRules rules)
        {
            var run = HelperRun.Begin(from, settings, rules, care.DayStart);
            var needs = BabyNeeds.Real(start, from, plain, cap, care.AwayHoldLevel, care.DayStart, effects, rates, run);
            if (run == null)
                return new Gap(plain, needs, null);

            var record = run.Served.Count > 0 ? CareRules.Advance(from, until, care, null, run.Served) : plain;
            return new Gap(record.WithHelpers(run.Record), needs, run);
        }

        // True when helpers may act on this record (switched on and the ledger anchored).
        public static bool Active(CareRecord care, HelperSettings settings) =>
            care != null && settings != null && settings.Any && !care.AwaitingAnchor;

        // Fills the feeder (the Sitter's bottles) to `portions`, at most the tunables' stock. Free and hands-on, but not
        // care: it gives no credit and moves nothing else.
        public static CareRecord Stock(CareRecord care, int portions, HelperRules rules)
        {
            if (care == null)
                throw new ArgumentNullException(nameof(care));
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            if (portions < 0)
                throw new ArgumentOutOfRangeException(nameof(portions));

            return care.WithHelpers(care.Helpers.WithStock(Math.Min(rules.MaxStock, portions)));
        }
    }
}
