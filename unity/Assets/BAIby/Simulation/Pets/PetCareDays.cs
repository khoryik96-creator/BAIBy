using System;

namespace BAIby.Simulation.Pets
{
    // What a care verb counts toward in a care day.
    public enum PetCareKind { None, Meal, Together, Bedtime }

    // Household-wide care days for one species, the pet side of the care rhythm in
    // docs/care-and-growth.md: a day counts with 2 meals, a Together action and a bedtime (a hamster's day: its one
    // dinner and a Together action; it naps by day on its own, so no bedtime is asked). A missed
    // day halves the rhythm instead of resetting it. Full care days and the best rhythm feed the
    // species' earned-breed goals (PetUnlocks). Days are local calendar day numbers; 0 means none yet.
    public sealed class PetCareDays : IEquatable<PetCareDays>
    {
        public const int MealsPerDay = 2;

        public static readonly PetCareDays None = new PetCareDays(0, 0, false, false, 0, 0, 0, 0);

        // The local day the counters below belong to.
        public int Day { get; }
        public int Meals { get; }
        public bool Together { get; }
        public bool Bedtime { get; }
        // The forgiving rhythm, its best value and the last day that counted.
        public int Rhythm { get; }
        public int BestRhythm { get; }
        public int LastFullDay { get; }
        // Every day that counted, ever.
        public int FullDays { get; }

        public bool IsDayComplete => IsComplete(MealsPerDay, true);

        // Whether the day is complete for a species that asks `meals` meals and, when `bedtimeWanted`, a bedtime.
        public bool IsComplete(int meals, bool bedtimeWanted) => Meals >= meals && Together && (Bedtime || !bedtimeWanted);

        // What a species' care day asks: meals and whether a bedtime counts. A species with a care profile follows its
        // routine (a cat 2 meals and a nap, a hamster its one dinner and no bedtime); others keep 2 meals and a bedtime.
        public static (int Meals, bool Bedtime) RuleFor(Species species)
        {
            var profile = PetCareProfile.For(species);
            return profile == null ? (MealsPerDay, true) : (Math.Min(MealsPerDay, profile.MealsPerDay), !profile.NapsByDay);
        }

        public PetCareDays(int day, int meals, bool together, bool bedtime, int rhythm, int bestRhythm,
            int lastFullDay, int fullDays)
        {
            if (day < 0)
                throw new ArgumentOutOfRangeException(nameof(day));
            if (meals < 0)
                throw new ArgumentOutOfRangeException(nameof(meals));
            if (rhythm < 0 || bestRhythm < rhythm)
                throw new ArgumentOutOfRangeException(nameof(rhythm));
            if (lastFullDay < 0 || lastFullDay > day)
                throw new ArgumentOutOfRangeException(nameof(lastFullDay));
            if (fullDays < 0 || fullDays < bestRhythm)
                throw new ArgumentOutOfRangeException(nameof(fullDays));

            Day = day;
            Meals = meals;
            Together = together;
            Bedtime = bedtime;
            Rhythm = rhythm;
            BestRhythm = bestRhythm;
            LastFullDay = lastFullDay;
            FullDays = fullDays;
        }

        // The local calendar day of an instant, as a day number (1 = 0001-01-01).
        public static int DayNumber(DateTimeOffset localNow) => (int)(localNow.DateTime.Date.Ticks / TimeSpan.TicksPerDay) + 1;

        // Meals raise Fullness or Hay, bedtime is Nap or Rest, and any other verb that builds Bond is
        // Together time. A verb can only be one of them.
        public static PetCareKind KindOf(SpeciesDefinition species, PetVerb verb)
        {
            if (species == null)
                throw new ArgumentNullException(nameof(species));

            var definition = species.FindVerb(verb);
            if (definition == null)
                return PetCareKind.None;
            if (verb == PetVerb.Nap || verb == PetVerb.Rest)
                return PetCareKind.Bedtime;

            foreach (var effect in definition.Effects)
            {
                if ((effect.Meter == PetMeter.Fullness || effect.Meter == PetMeter.Hay) && effect.Delta > 0)
                    return PetCareKind.Meal;
            }

            return definition.Bond > 0 ? PetCareKind.Together : PetCareKind.None;
        }

        // Records one care action on the given local day. Completed is true when this action finished
        // the day's care; each day counts once. A day earlier than the tracked one (a clock set back)
        // is counted on the tracked day instead.
        public (PetCareDays Days, bool Completed) Record(PetCareKind kind, int day) => Record(kind, day, MealsPerDay, true);

        // The same for a species whose day asks `mealsWanted` meals and, when `bedtimeWanted`, a bedtime (RuleFor).
        public (PetCareDays Days, bool Completed) Record(PetCareKind kind, int day, int mealsWanted, bool bedtimeWanted)
        {
            if (day < 1)
                throw new ArgumentOutOfRangeException(nameof(day));
            if (mealsWanted < 1 || mealsWanted > MealsPerDay)
                throw new ArgumentOutOfRangeException(nameof(mealsWanted));

            var current = day > Day ? new PetCareDays(day, 0, false, false, Rhythm, BestRhythm, LastFullDay, FullDays) : this;
            if (kind == PetCareKind.None)
                return (current, false);

            var wasComplete = current.IsComplete(mealsWanted, bedtimeWanted);
            var meals = kind == PetCareKind.Meal ? Math.Min(current.Meals + 1, MealsPerDay) : current.Meals;
            var together = current.Together || kind == PetCareKind.Together;
            var bedtime = current.Bedtime || kind == PetCareKind.Bedtime;
            var updated = new PetCareDays(current.Day, meals, together, bedtime, current.Rhythm, current.BestRhythm,
                current.LastFullDay, current.FullDays);
            if (wasComplete || !updated.IsComplete(mealsWanted, bedtimeWanted))
                return (updated, false);

            var rhythm = current.Rhythm;
            if (current.LastFullDay > 0)
            {
                // Each missed day halves the rhythm, rounding down; it never resets outright.
                for (var missed = current.Day - current.LastFullDay - 1; missed > 0 && rhythm > 0; missed--)
                    rhythm /= 2;
            }

            rhythm++;
            return (new PetCareDays(current.Day, meals, together, bedtime, rhythm, Math.Max(rhythm, current.BestRhythm),
                current.Day, current.FullDays + 1), true);
        }

        public bool Equals(PetCareDays other) =>
            other != null && Day == other.Day && Meals == other.Meals && Together == other.Together &&
            Bedtime == other.Bedtime && Rhythm == other.Rhythm && BestRhythm == other.BestRhythm &&
            LastFullDay == other.LastFullDay && FullDays == other.FullDays;

        public override bool Equals(object obj) => Equals(obj as PetCareDays);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Day;
                hash = hash * 31 + Meals;
                hash = hash * 31 + (Together ? 1 : 0);
                hash = hash * 31 + (Bedtime ? 1 : 0);
                hash = hash * 31 + Rhythm;
                hash = hash * 31 + BestRhythm;
                hash = hash * 31 + LastFullDay;
                return hash * 31 + FullDays;
            }
        }
    }
}
