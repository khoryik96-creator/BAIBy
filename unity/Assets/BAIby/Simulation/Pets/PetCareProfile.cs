using System;
using System.Collections.Generic;

namespace BAIby.Simulation.Pets
{
    // What a species brings to the shared care systems (docs/care-and-growth.md, Core model and Pets and household): its
    // care verbs and the care tags they carry, its routine, its three milestones at their nominal ages, its night
    // rates, and which of its meters plays each of the four need roles the shared rules know (Fullness, Comfort, Energy
    // and Cleanliness). The cat and the hamster have one; other species keep PetSimulation's simple rules until their
    // slice.
    //
    // The cat: meals Morning and Evening, a play window in the afternoon and the evening nap (RoutineCalendar), a daily
    // brush in the wash window, and a scratched rug for mess. Milestones Pounce (day 2), Climb (day 3.5) and High jump
    // (day 5), each moved to between 0.8x and 1.3x by care and guaranteed by 1.4x, so the last one always arrives by
    // day 7, when the kitten grows up (Grown stays age-based, as before).
    //
    // The hamster (P2): nocturnal and kind about it. It naps through school hours by itself (two nap blocks that ask
    // nothing), eats once, at dinner, and has its wheel time in the evening; the daily bedding tidy is its wash, and messy
    // bedding is its mess. Exercise plays the Comfort role (a hamster that has not run is fidgety; a gentle hold lets it
    // scramble hand to hand) and Bedding the Cleanliness role. Its quiet hours are the day nap: needs fall slowly and
    // Energy comes back to full. Milestones Wheel (day 2), Tunnel (day 4) and Hand-tame (day 6) follow Milestone timing
    // on the same readiness chain as the cat's; the hamster grows up on day 5, so it may become hand-tame as a grown-up.
    public sealed class PetCareProfile
    {
        public static readonly PetCareProfile Cat = new PetCareProfile(Species.Cat, CareRoutine.Cat,
            new CareProfile("cat", new Dictionary<string, CareTags>
            {
                ["feed"] = CareTags.Meal,
                ["pet"] = CareTags.Together | CareTags.Comfort,
                ["wand"] = CareTags.Together | CareTags.Play,
                ["brush"] = CareTags.Hygiene,
                ["nap"] = CareTags.Bedtime,
                ["medicine"] = CareTags.Medicine,
            }),
            new Dictionary<PetVerb, string>
            {
                [PetVerb.Feed] = "feed",
                [PetVerb.Pet] = "pet",
                [PetVerb.Wand] = "wand",
                [PetVerb.Brush] = "brush",
                [PetVerb.Nap] = "nap",
            },
            new[] { MotorSkill.Pounce, MotorSkill.Climb, MotorSkill.HighJump },
            new[] { TimeSpan.FromDays(2), TimeSpan.FromDays(3.5), TimeSpan.FromDays(5) },
            nightFullness: 1.5, nightComfort: 0.5, nightEnergy: 9, nightCleanliness: 0.5);

        public static readonly PetCareProfile Hamster = new PetCareProfile(Species.Hamster, CareRoutine.Hamster,
            new CareProfile("hamster", new Dictionary<string, CareTags>
            {
                ["bowl"] = CareTags.Meal,
                ["wheel"] = CareTags.Together | CareTags.Play,
                ["fluff-bedding"] = CareTags.Hygiene,
                ["gentle-hold"] = CareTags.Together | CareTags.Comfort,
                ["nap"] = CareTags.Bedtime,
                ["medicine"] = CareTags.Medicine,
            }),
            new Dictionary<PetVerb, string>
            {
                [PetVerb.Bowl] = "bowl",
                [PetVerb.Wheel] = "wheel",
                [PetVerb.FluffBedding] = "fluff-bedding",
                [PetVerb.GentleHold] = "gentle-hold",
                [PetVerb.Nap] = "nap",
            },
            new[] { MotorSkill.Wheel, MotorSkill.Tunnel, MotorSkill.HandTame },
            new[] { TimeSpan.FromDays(2), TimeSpan.FromDays(4), TimeSpan.FromDays(6) },
            nightFullness: 0.8, nightComfort: 0.5, nightEnergy: 9, nightCleanliness: 0.8,
            comfort: PetMeter.Exercise, cleanliness: PetMeter.Bedding, restsAlone: true);

        private readonly Dictionary<PetVerb, string> verbs;
        private readonly MotorSkill[] skills;
        private readonly TimeSpan[] skillAges;

        public Species Species { get; }
        public CareRoutine Routine { get; }
        // The species' care verbs (string IDs, as for the baby) and their care tags.
        public CareProfile Profile { get; }
        public IReadOnlyList<MotorSkill> Skills => skills;
        public IReadOnlyList<TimeSpan> SkillAges => skillAges;
        // Needs fall this much per hour during quiet hours (awake rates come from the catalog), and Energy comes back
        // this fast after the nap.
        public double NightFullness { get; }
        public double NightComfort { get; }
        public double NightEnergy { get; }
        public double NightCleanliness { get; }
        // The meters in the shared need roles: Fullness, Comfort (a hamster's Exercise), Energy and Cleanliness (a
        // hamster's Bedding).
        public PetMeter FullnessMeter => PetMeter.Fullness;
        public PetMeter ComfortMeter { get; }
        public PetMeter EnergyMeter => PetMeter.Energy;
        public PetMeter CleanlinessMeter { get; }
        // The species rests on its own (a day nap): quiet hours always restore Energy, with no bedtime asked.
        public bool RestsAlone { get; }
        // How many meals a full care day asks for (two for a cat, one for a hamster's dinner), and whether it asks for
        // a bedtime (a hamster's day nap is its own).
        public int MealsPerDay => RoutineCalendar.MealWindows(Routine);
        public bool NapsByDay => RoutineCalendar.NapsByDay(Routine);

        private PetCareProfile(Species species, CareRoutine routine, CareProfile profile, Dictionary<PetVerb, string> verbs,
            MotorSkill[] skills, TimeSpan[] skillAges, double nightFullness, double nightComfort, double nightEnergy,
            double nightCleanliness, PetMeter comfort = PetMeter.Comfort, PetMeter cleanliness = PetMeter.Cleanliness,
            bool restsAlone = false)
        {
            ComfortMeter = comfort;
            CleanlinessMeter = cleanliness;
            RestsAlone = restsAlone;
            Species = species;
            Routine = routine;
            Profile = profile;
            this.verbs = verbs;
            this.skills = skills;
            this.skillAges = skillAges;
            NightFullness = nightFullness;
            NightComfort = nightComfort;
            NightEnergy = nightEnergy;
            NightCleanliness = nightCleanliness;
        }

        // The species' care profile, or null when the species keeps PetSimulation's simple rules.
        public static PetCareProfile For(Species species) =>
            species == Species.Cat ? Cat : species == Species.Hamster ? Hamster : null;

        // The need role a meter plays, as the shared rules name it, or null when the meter has none.
        public PetMeter? RoleOf(PetMeter meter)
        {
            if (meter == FullnessMeter)
                return PetMeter.Fullness;
            if (meter == ComfortMeter)
                return PetMeter.Comfort;
            if (meter == EnergyMeter)
                return PetMeter.Energy;
            if (meter == CleanlinessMeter)
                return PetMeter.Cleanliness;
            return null;
        }

        // The species' meter for a need role (Fullness, Comfort, Energy or Cleanliness).
        public PetMeter MeterFor(PetMeter role)
        {
            switch (role)
            {
                case PetMeter.Fullness: return FullnessMeter;
                case PetMeter.Comfort: return ComfortMeter;
                case PetMeter.Energy: return EnergyMeter;
                case PetMeter.Cleanliness: return CleanlinessMeter;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        public bool Offers(PetVerb verb) => verbs.ContainsKey(verb);

        // The verb's stable care ID.
        public string VerbId(PetVerb verb) =>
            verbs.TryGetValue(verb, out var id)
                ? id
                : throw new ArgumentOutOfRangeException(nameof(verb), $"A {Species} has no {verb} care.");

        public CareTags TagsFor(PetVerb verb) => Profile.TagsFor(VerbId(verb));

        // What the verb counts as for engagement (docs/care-and-growth.md, slice 4: play feeds Motor, petting is
        // soothing for Social, brushing is grooming; an on-time meal counts as a meal).
        public EngagementKind? KindOf(PetVerb verb, bool onTimeMeal)
        {
            var tags = TagsFor(verb);
            if ((tags & CareTags.Meal) != 0)
                return onTimeMeal ? EngagementKind.Meal : (EngagementKind?)null;
            if ((tags & CareTags.Play) != 0)
                return EngagementKind.PlayWand;
            if ((tags & CareTags.Comfort) != 0)
                return EngagementKind.Soothe;
            if ((tags & CareTags.Hygiene) != 0)
                return EngagementKind.Hygiene;
            return null;
        }

        // The kind of care for the day's variety.
        public CareCategories CategoryOf(PetVerb verb)
        {
            var tags = TagsFor(verb);
            if ((tags & CareTags.Meal) != 0)
                return CareCategories.Meal;
            if ((tags & CareTags.Play) != 0)
                return CareCategories.Play;
            if ((tags & CareTags.Comfort) != 0)
                return CareCategories.Comfort;
            if ((tags & CareTags.Hygiene) != 0)
                return CareCategories.Hygiene;
            return (tags & CareTags.Bedtime) != 0 ? CareCategories.Rest : CareCategories.None;
        }

        // How many milestones an age has passed at their nominal ages (a pet saved before milestones existed).
        public int ReachedBy(TimeSpan age)
        {
            var reached = 0;
            while (reached < skillAges.Length && age >= skillAges[reached])
                reached++;
            return reached;
        }

        // The milestone's place in the species' order (0 for the first), or -1.
        public int IndexOf(MotorSkill skill) => Array.IndexOf(skills, skill);
    }
}
