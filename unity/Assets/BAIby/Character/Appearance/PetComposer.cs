using System;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Character
{
    // Supplies one decoded art image as tightly packed RGBA bytes in the composer's row order. Throws
    // when the image is missing or has another size.
    public delegate byte[] PetArtSource(string path, int width, int height);

    // Pure compositing for a pet pose, shared by PetVisuals and checked by tests/Appearance. The order
    // follows docs/pet-mode.md and tools/pets: base, breed markings, a "dark" markings overlay recolored
    // on its own first (Calico), the accessory, then one recolor pass and the transparent-edge fill.
    // A breed without marking art shows the plain base; an accessory without art is left off. A breed the layout lists
    // in breedBases (the Corgi) is drawn on its own base pose (breed + pose), with its markings and the accessory
    // drawn for that pose.
    public sealed class PetComposer
    {
        private readonly PetLayout layout;
        private byte[] pixels = Array.Empty<byte>();
        private byte[] regions = Array.Empty<byte>();
        private byte[] overlayPixels = Array.Empty<byte>();
        private byte[] overlayRegions = Array.Empty<byte>();

        public PetComposer(PetLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public PetLayout Layout => layout;

        // Composites and recolors one pose. Returns the composer's scratch buffer: its first
        // width * height * 4 bytes hold the pose until the next call.
        public byte[] Compose(PetBreed breed, PetCoat coat, PetAccessory accessory, Outfit accent, string pose,
            PetArtSource source, PixelRowOrder order, out int width, out int height)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var shown = layout.ShownPose(breed, pose);
            if (!layout.TryGetPoseSize(shown, out width, out height))
                throw new InvalidOperationException("The pet layout has no " + shown + " pose.");

            int length = width * height * AppearanceRecolor.Channels;
            Ensure(ref pixels, length);
            Ensure(ref regions, length);
            var basePath = PetLayout.BasePath(shown);
            Buffer.BlockCopy(Checked(source(basePath, width, height), length, basePath), 0, pixels, 0, length);
            var baseRegions = PetLayout.RegionsPath(basePath);
            Buffer.BlockCopy(Checked(source(baseRegions, width, height), length, baseRegions), 0, regions, 0, length);

            CompositeIfPresent(PetLayout.MarkingsPath(breed, pose), shown, width, height, source, order, null);
            var dark = PetLayout.DarkMarkingsPath(breed, pose);
            if (layout.IsDarkOverlay(dark))
                CompositeIfPresent(dark, shown, width, height, source, order, PetPalette.DarkTargets(layout, breed, coat, accent));
            CompositeIfPresent(PetLayout.AccessoryPath(accessory, shown), shown, width, height, source, order, null);

            AppearanceRecolor.Recolor(pixels, regions, width * height, layout.Reference,
                PetPalette.Targets(layout, breed, coat, accent));
            AppearanceRecolor.BleedTransparentEdges(pixels, width, height);
            return pixels;
        }

        // Frees the scratch buffers; the next Compose allocates them again.
        public void Release()
        {
            pixels = Array.Empty<byte>();
            regions = Array.Empty<byte>();
            overlayPixels = Array.Empty<byte>();
            overlayRegions = Array.Empty<byte>();
        }

        // preRecolor set: the overlay is recolored with these targets on its own and then composited with
        // zero region weights, so the final pass keeps its color.
        private void CompositeIfPresent(string path, string pose, int width, int height, PetArtSource source,
            PixelRowOrder order, AppearanceColors preRecolor)
        {
            if (path == null || !layout.TryGetOverlay(path, out var overlay) || overlay.Pose != pose)
                return;

            int length = overlay.Width * overlay.Height * AppearanceRecolor.Channels;
            Ensure(ref overlayPixels, length);
            Ensure(ref overlayRegions, length);
            Buffer.BlockCopy(Checked(source(path, overlay.Width, overlay.Height), length, path), 0, overlayPixels, 0, length);
            var regionsPath = PetLayout.RegionsPath(path);
            Buffer.BlockCopy(Checked(source(regionsPath, overlay.Width, overlay.Height), length, regionsPath),
                0, overlayRegions, 0, length);

            if (preRecolor != null)
            {
                AppearanceRecolor.Recolor(overlayPixels, overlayRegions, overlay.Width * overlay.Height, layout.Reference, preRecolor);
                Array.Clear(overlayRegions, 0, length);
            }

            AppearanceRecolor.CompositeOver(pixels, regions, width, height, overlayPixels, overlayRegions,
                overlay.Width, overlay.Height, overlay.X, overlay.YFromTop, order);
        }

        private static byte[] Checked(byte[] image, int length, string path)
        {
            if (image == null || image.Length < length)
                throw new InvalidOperationException("The pet art " + path + " is missing or smaller than the layout says.");
            return image;
        }

        private static void Ensure(ref byte[] buffer, int length)
        {
            if (buffer.Length < length)
                buffer = new byte[length];
        }
    }
}
