using System;
using System.Collections.Generic;
using System.Text;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Application
{
    // How a cosmetic is reached: free, earned through care (never sold), or part of a product (a look pack).
    public enum CosmeticAccess { Free, Earned, Product }

    // One premium cosmetics product: its name, what is inside (content IDs) and a short line for Grown-ups.
    public sealed class CosmeticPack
    {
        public CosmeticPack(string productId, string title, string contents, params string[] contentIds)
        {
            if (!ProductIds.IsKnownFamily(productId))
                throw new ArgumentException("A cosmetics pack needs a catalogue product ID.", nameof(productId));

            ProductId = productId;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Contents = contents ?? throw new ArgumentNullException(nameof(contents));
            ContentIds = Array.AsReadOnly(contentIds ?? Array.Empty<string>());
        }

        public string ProductId { get; }
        public string Title { get; }
        // What is inside, in words, for the Looks & rooms panel.
        public string Contents { get; }
        public IReadOnlyList<string> ContentIds { get; }
    }

    // Premium cosmetics (docs/monetization.md, M2b "Rooms and wear" and the baby look packs). A cosmetic is a content ID
    // already stored in a look or a room (an Outfit, a Headwear, a NurseryTheme, a pet accessory or its colour), so
    // nothing about purchases is saved in a record: what is unlocked is computed from entitlements at runtime. A save
    // that holds a locked ID keeps it and shows the free default instead; owning the pack again shows the look again.
    // Cosmetics never change care, growth, Bond or anything else. Looks earned through care are never sold.
    public static class CosmeticCatalog
    {
        public const string CozyKnits = "look.cozy-knits";
        public const string GardenParty = "look.garden-party";
        public const string CloudNursery = "room.cloud-nursery";
        public const string Starlight = "room.starlight";
        public const string CatBandanasBows = "petwear.cat.bandanas-bows";
        public const string Founder = "supporter.founder";

        // The household card's Founder badge (supporter.founder).
        public const string FounderBadge = "badge.founder";

        // Pet accessory colours the free game offers; the pet pack adds more (PackAccents).
        public static readonly IReadOnlyList<Outfit> FreeAccents =
            Array.AsReadOnly(new[] { Outfit.Sage, Outfit.Sky, Outfit.Blush, Outfit.Midnight });

        public static readonly IReadOnlyList<Outfit> PackAccents =
            Array.AsReadOnly(new[] { Outfit.Butter, Outfit.Lilac, Outfit.Terracotta });

        private static readonly CosmeticPack[] AllPacks =
        {
            new CosmeticPack(CozyKnits, "Cozy knits",
                "Four knit outfits (Oatmeal, Berry, Moss and Fog) and a pom-pom beanie.",
                OutfitId(Outfit.OatmealKnit), OutfitId(Outfit.BerryKnit), OutfitId(Outfit.MossKnit), OutfitId(Outfit.FogKnit),
                HeadwearId(Headwear.PomBeanie)),
            new CosmeticPack(GardenParty, "Garden party",
                "Four garden outfits (Rosebud, Marigold, Garden mint and Bluebell) and a straw sun hat.",
                OutfitId(Outfit.Rosebud), OutfitId(Outfit.Marigold), OutfitId(Outfit.GardenMint), OutfitId(Outfit.Bluebell),
                HeadwearId(Headwear.SunHat)),
            new CosmeticPack(CloudNursery, "Cloud nursery",
                "A sky-blue room with soft painted clouds and a cloud-white rug, for the nursery and pet rooms.",
                RoomId(NurseryTheme.CloudNursery)),
            new CosmeticPack(Starlight, "Starlight",
                "A twilight room with a sprinkle of golden stars and a lavender rug, for the nursery and pet rooms.",
                RoomId(NurseryTheme.Starlight)),
            new CosmeticPack(CatBandanasBows, "Cat bandanas & bows",
                "A bandana and a bow for every cat, and three more colours (Butter, Lilac and Terracotta) for collars too.",
                PetWearId(Species.Cat, PetAccessory.Bandana), PetWearId(Species.Cat, PetAccessory.Bow),
                PetAccentId(Outfit.Butter), PetAccentId(Outfit.Lilac), PetAccentId(Outfit.Terracotta)),
            new CosmeticPack(Founder, "Founder pack",
                "For grown-ups who want to support the game: the Fern & gold outfit, the Golden hour room and a " +
                "Founder badge on the household card.",
                OutfitId(Outfit.FernGold), RoomId(NurseryTheme.GoldenHour), FounderBadge),
        };

        private static readonly Dictionary<string, CosmeticPack> PackOf = BuildIndex();

        // Every cosmetics product, in the order Grown-ups lists them.
        public static IReadOnlyList<CosmeticPack> Packs => Array.AsReadOnly(AllPacks);

        public static IReadOnlyList<string> Products
        {
            get
            {
                var products = new List<string>();
                foreach (var pack in AllPacks)
                    products.Add(pack.ProductId);
                return products.AsReadOnly();
            }
        }

        public static string OutfitId(Outfit outfit) => Id("outfit", Require(outfit));
        public static string HeadwearId(Headwear headwear) => Id("headwear", Require(headwear));
        public static string RoomId(NurseryTheme theme) => Id("room-theme", Require(theme));
        public static string PetWearId(Species species, PetAccessory accessory) =>
            "wear." + PetIds.Key(species) + "." + Kebab(Require(accessory).ToString());

        // A pet accessory's colour.
        public static string PetAccentId(Outfit accent) => Id("wear-color", Require(accent));

        // A pet accessory's colour is open when its own ID is (the pet pack's colours) and, for a colour that is a
        // baby look-pack outfit, when that outfit is.
        public static bool IsPetAccentUnlocked(IEntitlements entitlements, Outfit accent) =>
            IsCosmeticUnlocked(entitlements, PetAccentId(accent)) && IsUnlocked(entitlements, accent);

        private static bool IsPetAccentShown(IEntitlements entitlements, Outfit accent) =>
            IsPetAccentUnlocked(entitlements, accent) || (entitlements != null && entitlements.OwnershipUnknown);

        public static CosmeticAccess AccessOf(string contentId)
        {
            if (contentId != null && PackOf.ContainsKey(contentId))
                return CosmeticAccess.Product;
            foreach (Headwear headwear in Enum.GetValues(typeof(Headwear)))
            {
                if (EarnedLooks.RhythmFor(headwear) > 0 && HeadwearId(headwear) == contentId)
                    return CosmeticAccess.Earned;
            }

            foreach (Outfit outfit in Enum.GetValues(typeof(Outfit)))
            {
                if (EarnedLooks.RhythmFor(outfit) > 0 && OutfitId(outfit) == contentId)
                    return CosmeticAccess.Earned;
            }

            return CosmeticAccess.Free;
        }

        // The pack a cosmetic belongs to; null for a free or earned look.
        public static CosmeticPack PackFor(string contentId) =>
            contentId != null && PackOf.TryGetValue(contentId, out var pack) ? pack : null;

        public static CosmeticPack PackForProduct(string productId)
        {
            foreach (var pack in AllPacks)
            {
                if (pack.ProductId == productId)
                    return pack;
            }

            return null;
        }

        // Free and earned looks are never locked here (the care rhythm gates earned looks: EarnedLooks); a product's
        // look is unlocked while its pack is owned.
        public static bool IsCosmeticUnlocked(IEntitlements entitlements, string contentId)
        {
            var pack = PackFor(contentId);
            return pack == null || (entitlements != null && entitlements.Owns(pack.ProductId));
        }

        // Whether a saved look may be shown: unlocked, or ownership can't be known yet (a damaged cache never takes a
        // look away before the store answers).
        public static bool IsShown(IEntitlements entitlements, string contentId) =>
            IsCosmeticUnlocked(entitlements, contentId) || (entitlements != null && entitlements.OwnershipUnknown);

        public static bool IsUnlocked(IEntitlements entitlements, Outfit outfit) => IsCosmeticUnlocked(entitlements, OutfitId(outfit));
        public static bool IsUnlocked(IEntitlements entitlements, Headwear headwear) => IsCosmeticUnlocked(entitlements, HeadwearId(headwear));
        public static bool IsUnlocked(IEntitlements entitlements, NurseryTheme theme) => IsCosmeticUnlocked(entitlements, RoomId(theme));

        // The look to draw: a locked outfit shows Sage and a locked hat none. The saved look is left as it is.
        public static BabyAppearance Displayed(BabyAppearance look, IEntitlements entitlements)
        {
            if (look == null)
                throw new ArgumentNullException(nameof(look));

            var shown = look;
            if (!IsShown(entitlements, OutfitId(look.Outfit)))
                shown = shown.WithOutfit(Outfit.Sage);
            if (!IsShown(entitlements, HeadwearId(look.Headwear)))
                shown = shown.WithHeadwear(Headwear.None);
            return shown;
        }

        // The room to draw: a locked theme shows Sage.
        public static NurseryTheme Displayed(NurseryTheme theme, IEntitlements entitlements) =>
            IsShown(entitlements, RoomId(theme)) ? theme : NurseryTheme.Sage;

        // A pet's look to draw: a locked accessory shows the free collar, and a locked colour Sage.
        public static PetAppearance Displayed(PetAppearance look, IEntitlements entitlements)
        {
            if (look == null)
                throw new ArgumentNullException(nameof(look));

            var shown = look;
            if (look.Accessory != PetAccessory.None && !IsShown(entitlements, PetWearId(look.Species, look.Accessory)))
                shown = shown.WithAccessory(PetAccessory.Collar);
            if (!IsPetAccentShown(entitlements, look.AccessoryAccent))
                shown = shown.WithAccessoryAccent(Outfit.Sage);
            return shown;
        }

        // Whether a new look may be saved: every part that changes is unlocked. A part already worn may stay, so a
        // look saved before a refund keeps its ID while another part changes (as earned looks do: EarnedLooks.CanWear).
        public static bool CanWear(BabyAppearance current, BabyAppearance next, IEntitlements entitlements)
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));

            current = current ?? BabyAppearance.Default;
            return (next.Outfit == current.Outfit || IsUnlocked(entitlements, next.Outfit)) &&
                (next.Headwear == current.Headwear || IsUnlocked(entitlements, next.Headwear));
        }

        public static bool CanWear(PetAppearance current, PetAppearance next, IEntitlements entitlements)
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));

            var accessoryKept = current != null && next.Accessory == current.Accessory && next.Species == current.Species;
            var accentKept = current != null && next.AccessoryAccent == current.AccessoryAccent;
            return (accessoryKept || next.Accessory == PetAccessory.None ||
                    IsCosmeticUnlocked(entitlements, PetWearId(next.Species, next.Accessory))) &&
                (accentKept || IsPetAccentUnlocked(entitlements, next.AccessoryAccent));
        }

        // The first locked part of a look (for the calm "Part of the … pack" line), or null when every part is open.
        public static CosmeticPack LockedPack(BabyAppearance look, IEntitlements entitlements)
        {
            if (look == null)
                return null;
            if (!IsUnlocked(entitlements, look.Outfit))
                return PackFor(OutfitId(look.Outfit));
            return IsUnlocked(entitlements, look.Headwear) ? null : PackFor(HeadwearId(look.Headwear));
        }

        public static CosmeticPack LockedPack(PetAppearance look, IEntitlements entitlements)
        {
            if (look == null)
                return null;
            if (look.Accessory != PetAccessory.None && !IsCosmeticUnlocked(entitlements, PetWearId(look.Species, look.Accessory)))
                return PackFor(PetWearId(look.Species, look.Accessory));
            if (IsPetAccentUnlocked(entitlements, look.AccessoryAccent))
                return null;
            return PackFor(PetAccentId(look.AccessoryAccent)) ?? PackFor(OutfitId(look.AccessoryAccent));
        }

        // The calm line a locked item shows in a picker. No price, no shop link: the pack lives in Grown-ups.
        public static string LockedLine(CosmeticPack pack) =>
            pack == null ? string.Empty : $"Part of the {pack.Title} pack — a grown-up can get it in Grown-ups.";

        // "OatmealKnit" -> "oatmeal-knit". Enum member names are content IDs already (tests/content-ids), so they never change.
        public static string Kebab(string name)
        {
            var text = new StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++)
            {
                var character = name[i];
                if (char.IsUpper(character) && i > 0)
                    text.Append('-');
                text.Append(char.ToLowerInvariant(character));
            }

            return text.ToString();
        }

        private static string Id<T>(string kind, T value) where T : Enum => kind + "." + Kebab(value.ToString());

        private static T Require<T>(T value) where T : Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            return value;
        }

        private static Dictionary<string, CosmeticPack> BuildIndex()
        {
            var index = new Dictionary<string, CosmeticPack>(StringComparer.Ordinal);
            foreach (var pack in AllPacks)
            {
                foreach (var id in pack.ContentIds)
                {
                    if (index.ContainsKey(id))
                        throw new InvalidOperationException($"{id} is in two cosmetics packs.");
                    index.Add(id, pack);
                }
            }

            return index;
        }
    }

    // IEntitlements.IsCosmeticUnlocked(contentId), as docs/monetization.md names it.
    public static class CosmeticEntitlements
    {
        public static bool IsCosmeticUnlocked(this IEntitlements entitlements, string contentId) =>
            CosmeticCatalog.IsCosmeticUnlocked(entitlements, contentId);
    }
}
