using System;
using System.Collections.Generic;

namespace BAIby.Simulation.Pets
{
    // What a species brings to the shared care systems (docs/care-and-growth.md, Core model and Pets and household): its
    // care verbs and the care tags they carry, its routine, its three milestones at their nominal ages, and its night
    // rates. Only the cat has one so far; other species keep PetSimulation's simple rules until their slice.
    //
    // The cat: meals Morning and Evening, a play window in the afternoon and the evening nap (RoutineCalendar), a daily
    // brush in the wash window, and a scratched rug for mess. Milestones Pounce (day 2), Climb (day 3.5) and High jump
    // (day 5), each moved to between 0.8x and 1.3x by care and guaranteed by 1.4x, so the last one always arrives by
    // day 7, when the kitten grows up (Grown stays age-based, as before).
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
            nightFullness: 2.5, nightComfort: 1, nightEnergy: 9, nightCleanliness: 1.5);

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

        private PetCareProfile(Species species, CareRoutine routine, CareProfile profile, Dictionary<PetVerb, string> verbs,
            MotorSkill[] skills, TimeSpan[] skillAges, double nightFullness, double nightComfort, double nightEnergy,
            double nightCleanliness)
        {
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
        public static PetCareProfile For(Species species) => species == Species.Cat ? Cat : null;

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
