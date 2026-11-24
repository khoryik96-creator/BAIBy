using System;
using System.Collections.Generic;
using BAIby.Simulation;

namespace BAIby.Application
{
    // Which helpers the household may use: owned through an entitlement (the set or the helper's own product) and not
    // switched off in the household's settings. Ownership is computed at runtime and never saved in a record: a refund
    // stops the helper, and what it already did stays.
    public static class HelperOwnership
    {
        public static string ProductFor(HelperKind kind)
        {
            switch (kind)
            {
                case HelperKind.Feeder: return ProductIds.HelperFeeder;
                case HelperKind.Play: return ProductIds.HelperPlay;
                case HelperKind.Tidy: return ProductIds.HelperTidy;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static bool Owns(IEntitlements entitlements, HelperKind kind) =>
            entitlements != null && (entitlements.Owns(ProductIds.HelperSet) || entitlements.Owns(ProductFor(kind)));

        public static bool OwnsAny(IEntitlements entitlements) =>
            Owns(entitlements, HelperKind.Feeder) || Owns(entitlements, HelperKind.Play) || Owns(entitlements, HelperKind.Tidy);

        // Owned helpers are on unless the household switched them off.
        public static HelperSettings Settings(IEntitlements entitlements, ICollection<HelperKind> switchedOff)
        {
            bool On(HelperKind kind) => Owns(entitlements, kind) && (switchedOff == null || !switchedOff.Contains(kind));
            return new HelperSettings(On(HelperKind.Feeder), On(HelperKind.Play), On(HelperKind.Tidy));
        }
    }
}
