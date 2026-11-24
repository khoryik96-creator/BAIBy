using System;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Character
{
    // Target colors for a pet's look, in region order: main coat (R), secondary coat or markings (G),
    // iris (B) and accessory accent (A). Breed coats come from the layout palette; a breed coat the
    // palette does not list (a premium breed before its tuning, say) uses the generic coat color below
    // with a deeper secondary, so it still renders calmly. The accent reuses the baby outfit accent
    // palette, so a collar can match a baby's outfit; Sage keeps the drawn collar color.
    public static class PetPalette
    {
        public static AppearanceColors Targets(PetLayout layout, PetBreed breed, PetCoat coat, Outfit accent) =>
            Targets(layout, breed, coat, accent, false);

        // Targets for a "dark" overlay (Calico's dark patches): G takes the coat's dark color.
        public static AppearanceColors DarkTargets(PetLayout layout, PetBreed breed, PetCoat coat, Outfit accent) =>
            Targets(layout, breed, coat, accent, true);

        public static PetCoatColors CoatColors(PetLayout layout, PetBreed breed, PetCoat coat)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (layout.TryGetCoat(breed, coat, out var colors))
                return colors;

            var main = FallbackCoat(coat);
            var secondary = Deeper(main);
            return new PetCoatColors(main, secondary, secondary, layout.Reference.Region(2));
        }

        // A generic color per coat, used where the layout palette has no entry for the breed.
        public static AppearanceColor FallbackCoat(PetCoat coat)
        {
            switch (coat)
            {
                case PetCoat.Cream: return new AppearanceColor(246, 226, 196);
                case PetCoat.Ginger: return new AppearanceColor(240, 172, 110);
                case PetCoat.Gray: return new AppearanceColor(178, 180, 190);
                case PetCoat.Black: return new AppearanceColor(84, 82, 98);
                case PetCoat.White: return new AppearanceColor(250, 247, 242);
                case PetCoat.Brown: return new AppearanceColor(206, 162, 118);
                case PetCoat.Golden: return new AppearanceColor(230, 186, 112);
                case PetCoat.Silver: return new AppearanceColor(214, 216, 222);
                case PetCoat.Blue: return new AppearanceColor(160, 168, 186);
                case PetCoat.Seal: return new AppearanceColor(246, 232, 210);
                case PetCoat.Lilac: return new AppearanceColor(222, 208, 214);
                case PetCoat.Red: return new AppearanceColor(220, 128, 84);
                case PetCoat.Sable: return new AppearanceColor(176, 126, 86);
                case PetCoat.Tan: return new AppearanceColor(216, 178, 132);
                case PetCoat.Fawn: return new AppearanceColor(228, 198, 160);
                case PetCoat.Orange: return new AppearanceColor(240, 152, 74);
                case PetCoat.Yellow: return new AppearanceColor(244, 214, 112);
                case PetCoat.Purple: return new AppearanceColor(172, 142, 200);
                case PetCoat.Chocolate: return new AppearanceColor(132, 88, 64);
                case PetCoat.Tricolor: return new AppearanceColor(250, 247, 242);
                default: throw new ArgumentOutOfRangeException(nameof(coat));
            }
        }

        private static AppearanceColors Targets(PetLayout layout, PetBreed breed, PetCoat coat, Outfit accent, bool dark)
        {
            var colors = CoatColors(layout, breed, coat);
            return new AppearanceColors(
                colors.Main,
                dark ? colors.Dark : colors.Secondary,
                colors.Eyes,
                AppearancePalette.OutfitAccent(accent, layout.Reference.Region(3)));
        }

        private static AppearanceColor Deeper(AppearanceColor color) =>
            new AppearanceColor(Math.Round(color.R * 0.62), Math.Round(color.G * 0.62), Math.Round(color.B * 0.62));
    }
}
