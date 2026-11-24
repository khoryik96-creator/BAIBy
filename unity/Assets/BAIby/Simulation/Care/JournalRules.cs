using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // Pure journal rules (docs/care-and-growth.md, Return loops): what goes in, when, and the photo limit. Entries are
    // dated by the instant something happened and the little one's day then, so one long advance and many short ones
    // write the same journal.
    public static class JournalRules
    {
        public const int DaysPerWeek = 7;

        // The little one's day at an age: 1 on the day they arrive.
        public static int DayOf(TimeSpan age)
        {
            var days = age.Ticks <= 0 ? 0 : age.Ticks / TimeSpan.TicksPerDay;
            return (int)Math.Min(int.MaxValue - 1, days) + 1;
        }

        // A journal for a little one saved before journals existed: the arrival, and each milestone already reached on
        // its nominal day (the real day is unknown), never after the checkpoint. A poorly spell that is under way begins
        // at its start. Weeks already lived get no page.
        public static JournalRecord Seed(DateTimeOffset checkpoint, TimeSpan age, bool sitting, bool crawling, bool walking,
            TimeSpan sittingAge, TimeSpan crawlingAge, TimeSpan walkingAge, CareRecord care) =>
            Seed(checkpoint, age, walking ? 3 : crawling ? 2 : sitting ? 1 : 0,
                new[] { MotorSkill.Sitting, MotorSkill.Crawling, MotorSkill.Walking },
                new[] { sittingAge, crawlingAge, walkingAge }, care);

        // The same for any species: the first `reached` of its milestones, at their nominal ages.
        public static JournalRecord Seed(DateTimeOffset checkpoint, TimeSpan age, int reached, IReadOnlyList<MotorSkill> skills,
            IReadOnlyList<TimeSpan> ages, CareRecord care)
        {
            if (care == null)
                throw new ArgumentNullException(nameof(care));
            if (skills == null || ages == null || skills.Count != ages.Count)
                throw new ArgumentException("Each milestone needs its nominal age.", nameof(skills));

            var born = Before(checkpoint, age);
            var entries = new List<JournalEntry> { JournalEntry.Arrival(born) };
            for (var stage = 0; stage < skills.Count && stage < reached; stage++)
            {
                var skill = skills[stage];
                var nominal = ages[stage];
                var at = nominal >= age ? checkpoint : Before(checkpoint, age - nominal);
                if (at < entries[entries.Count - 1].At)
                    at = entries[entries.Count - 1].At;
                entries.Add(JournalEntry.Milestone(at, DayOf(nominal < age ? nominal : age), skill));
            }

            var unwell = care.Condition != CareCondition.Well;
            var spellSince = unwell ? (care.ConditionSince < checkpoint ? care.ConditionSince : checkpoint) : DateTimeOffset.MinValue;
            var spellDay = unwell ? DayOf(age - (checkpoint - spellSince)) : 0;
            return new JournalRecord(entries, spellSince, spellDay, (int)Math.Min(int.MaxValue, age.Ticks / TimeSpan.TicksPerDay / DaysPerWeek), 0);
        }

        // What the helpers did, one entry a day of the little one's life: each event adds to its day's counts, so one
        // long advance and many short ones write the same entries. `startAge` is the age at the start of the advance (or
        // the preview day) the events came from.
        public static JournalRecord Helpers(JournalRecord journal, IReadOnlyList<HelperEvent> events, TimeSpan startAge)
        {
            if (journal == null)
                throw new ArgumentNullException(nameof(journal));
            if (events == null || events.Count == 0)
                return journal;

            foreach (var helped in events)
            {
                var day = DayOf(startAge + helped.Elapsed);
                JournalEntry old = null;
                var entries = journal.Entries;
                for (var i = entries.Count - 1; i >= 0; i--)
                {
                    if (entries[i].Kind == JournalKind.Helper && entries[i].Day == day)
                    {
                        old = entries[i];
                        break;
                    }
                }

                var feeds = (old?.Feeds ?? 0) + (helped.Kind == HelperKind.Feeder ? 1 : 0);
                var plays = (old?.Plays ?? 0) + (helped.Kind == HelperKind.Play ? 1 : 0);
                var tidies = (old?.Tidies ?? 0) + (helped.Kind == HelperKind.Tidy ? 1 : 0);
                var at = old != null && old.At > helped.At ? old.At : helped.At;
                var entry = JournalEntry.Helper(at, day, Math.Min(HelperRecord.CountLimit, feeds),
                    Math.Min(HelperRecord.CountLimit, plays), Math.Min(HelperRecord.CountLimit, tidies));
                journal = old == null ? journal.With(entry) : journal.Replace(old, entry);
            }

            return journal;
        }

        // A new little one's journal: the arrival.
        public static JournalRecord Start(DateTimeOffset at) =>
            new JournalRecord(new[] { JournalEntry.Arrival(at) }, DateTimeOffset.MinValue, 0, 0, 0);

        public static int PhotosLeft(JournalRecord journal, TimeSpan age) =>
            journal == null ? JournalRecord.PhotosPerDay : Math.Max(0, JournalRecord.PhotosPerDay - journal.PhotosOn(DayOf(age)));

        // A photo of the moment, if fewer than three were taken today. Returns the journal unchanged otherwise.
        public static JournalRecord Photo(JournalRecord journal, DateTimeOffset at, TimeSpan age, PhotoMoment moment,
            MotorSkill stage, BabyAppearance look)
        {
            if (journal == null)
                throw new ArgumentNullException(nameof(journal));
            if (PhotosLeft(journal, age) <= 0)
                return journal;

            return journal.With(JournalEntry.Photo(at, DayOf(age), moment, stage, look ?? BabyAppearance.Default));
        }

        // Follows the health axis: a spell begins when the little one stops being well, and when they are well again
        // "a hard day we got through" is written.
        public static JournalRecord AfterCondition(JournalRecord journal, CareCondition before, CareCondition after,
            DateTimeOffset at, TimeSpan age)
        {
            if (journal == null)
                throw new ArgumentNullException(nameof(journal));
            if (before == CareCondition.Well && after != CareCondition.Well && !journal.InSpell)
                return journal.With(Array.Empty<JournalEntry>(), at, DayOf(age), journal.Weeks, journal.WeekStartCareDays);
            if (before == CareCondition.Well || after != CareCondition.Well)
                return journal;

            var since = journal.InSpell ? journal.SpellSince : at;
            var sinceDay = journal.InSpell ? Math.Min(journal.SpellDay, DayOf(age)) : DayOf(age);
            var entry = JournalEntry.HardDay(at, DayOf(age), HardSpell.Poorly, since <= at ? since : at, sinceDay);
            return journal.With(new[] { entry }, DateTimeOffset.MinValue, 0, journal.Weeks, journal.WeekStartCareDays);
        }

        // A shy skill is back after a good day.
        public static JournalRecord SkillBack(JournalRecord journal, DateTimeOffset at, TimeSpan age, MotorSkill skill,
            DateTimeOffset since)
        {
            if (journal == null)
                throw new ArgumentNullException(nameof(journal));

            var start = since <= at ? since : at;
            var sinceDay = DayOf(age - (at - start));
            return journal.With(JournalEntry.HardDay(at, DayOf(age), HardSpell.Shy, start, Math.Min(sinceDay, DayOf(age)), skill));
        }

        // Week pages for every week the age passed: the care days counted in it (from the instants at which care days
        // closed), at the instant the week ended. `from` is the gap's start instant and `real` whether time passed
        // (a preview day ages the little one at its checkpoint, after its own care day is counted). At most the last
        // two weeks of a long absence get a page; the care days of the weeks before fold into the first of them.
        public static JournalRecord Weeks(JournalRecord journal, TimeSpan ageBefore, TimeSpan ageAfter, DateTimeOffset from,
            bool real, int totalBefore, IReadOnlyList<long> countedDays)
        {
            if (journal == null)
                throw new ArgumentNullException(nameof(journal));

            var weekTicks = TimeSpan.TicksPerDay * DaysPerWeek;
            var reached = (int)Math.Min(int.MaxValue - 1, ageAfter.Ticks / weekTicks);
            if (reached <= journal.Weeks)
                return journal;

            var startCareDays = journal.WeekStartCareDays;
            var pages = new List<JournalEntry>();
            for (var week = Math.Max(journal.Weeks + 1, reached - 1); week <= reached; week++)
            {
                var endAge = week * weekTicks;
                var at = from;
                if (real)
                {
                    var ahead = Math.Max(0, endAge - ageBefore.Ticks);
                    at = new DateTimeOffset(Math.Min(DateTimeOffset.MaxValue.UtcTicks, from.UtcTicks + Math.Min(ahead,
                        DateTimeOffset.MaxValue.UtcTicks - from.UtcTicks)), TimeSpan.Zero);
                }

                var counted = totalBefore;
                if (countedDays != null)
                {
                    foreach (var instant in countedDays)
                    {
                        if (!real || instant <= at.UtcTicks)
                            counted++;
                    }
                }

                pages.Add(JournalEntry.Week(at, DayOf(TimeSpan.FromTicks(endAge - 1)), week, Math.Max(0, counted - startCareDays)));
                startCareDays = counted;
            }

            return journal.With(pages, journal.SpellSince, journal.SpellDay, reached, startCareDays);
        }

        // Journal entries for one step of any companion: milestones reached (on the day and at the instant they were
        // reached, from `from` and the age before the step), rhythm stickers, a hard spell that ended, and week pages.
        // `bond` is null when no care day closed; `real` is false for a preview day, which writes at `from`.
        public static JournalRecord Chronicle(JournalRecord journal, DateTimeOffset from, TimeSpan ageBefore,
            CareCondition conditionBefore, DateTimeOffset at, TimeSpan ageAfter, CareCondition conditionAfter,
            IReadOnlyList<(MotorSkill Skill, long Age)> milestones, BondStep bond, bool real, int totalCareDays)
        {
            if (journal == null)
                throw new ArgumentNullException(nameof(journal));

            var entries = new List<JournalEntry>();
            if (milestones != null)
            {
                foreach (var (skill, unlockAge) in milestones)
                {
                    var age = TimeSpan.FromTicks(unlockAge);
                    entries.Add(JournalEntry.Milestone(real ? InstantAt(from, age - ageBefore) : from, DayOf(age), skill));
                }
            }

            if (bond != null)
            {
                foreach (var (stuck, days) in bond.Stickers)
                {
                    var instant = new DateTimeOffset(stuck, TimeSpan.Zero);
                    entries.Add(JournalEntry.Sticker(instant, DayOf(real ? ageBefore + (instant - from) : ageBefore), days));
                }
            }

            if (entries.Count > 0)
                journal = journal.With(entries, journal.SpellSince, journal.SpellDay, journal.Weeks, journal.WeekStartCareDays);
            journal = AfterCondition(journal, conditionBefore, conditionAfter, at, ageAfter);
            return Weeks(journal, ageBefore, ageAfter, from, real, totalCareDays, bond?.CountedDays);
        }

        private static DateTimeOffset InstantAt(DateTimeOffset from, TimeSpan after)
        {
            var ticks = from.UtcTicks + Math.Max(0, Math.Min(after.Ticks, DateTimeOffset.MaxValue.UtcTicks - from.UtcTicks));
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        private static DateTimeOffset Before(DateTimeOffset at, TimeSpan span)
        {
            var ticks = at.UtcTicks - Math.Max(0, span.Ticks);
            return new DateTimeOffset(Math.Max(0, ticks), TimeSpan.Zero);
        }
    }
}
