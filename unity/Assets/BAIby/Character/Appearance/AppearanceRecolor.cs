using System;

namespace BAIby.Character
{
    // How the rows of an RGBA buffer are stored.
    // TopDown: row 0 is the top of the image (PIL, PNG files, layout.json coordinates).
    // BottomUp: row 0 is the bottom of the image (Unity's Texture2D.GetPixels32, SetPixelData and
    // GetPixelData after ImageConversion.LoadImage).
    // Within a row, pixels always run left to right. A canvas and every overlay composited into it must
    // share one row order; only the overlay's vertical offset depends on it.
    public enum PixelRowOrder
    {
        TopDown = 0,
        BottomUp = 1,
    }

    // Pure pixel math for the baby's look, shared by the Unity runtime and checked by
    // tests/Appearance. Buffers are tightly packed 8-bit RGBA, 4 bytes per pixel.
    //
    // Region maps use the same layout: R = skin weight, G = hair weight, B = outfit-main weight,
    // A = outfit-accent weight (each 0-255, sum <= 255). A region map's A channel is a weight, not an
    // opacity: coverage always comes from the matching color image's alpha.
    //
    // Rounding everywhere is round-half-to-even on doubles (C# Math.Round default, Python round() and
    // numpy.rint), then clamped to 0-255.
    public static class AppearanceRecolor
    {
        public const int Channels = 4;

        private static readonly double[] Weights = BuildWeights();

        // Converts a top-measured overlay offset (layout.json "y", PIL convention) to the index of the
        // canvas row that holds the overlay's buffer row 0.
        // TopDown: buffer row 0 is the overlay's top row, which sits at yFromTop.
        // BottomUp: buffer row 0 is the overlay's bottom row; the overlay's top is canvasHeight - yFromTop
        // rows above the canvas bottom, so its bottom row is canvasHeight - yFromTop - overlayHeight.
        public static int FirstCanvasRow(int canvasHeight, int overlayHeight, int yFromTop, PixelRowOrder order)
        {
            switch (order)
            {
                case PixelRowOrder.TopDown: return yFromTop;
                case PixelRowOrder.BottomUp: return canvasHeight - yFromTop - overlayHeight;
                default: throw new ArgumentOutOfRangeException(nameof(order));
            }
        }

        // Composites an overlay image and its region map over a canvas image and region map in place,
        // with the overlay's top-left corner at (x, yFromTop) in canvas pixels measured from the top-left.
        // Colors use straight-alpha "over"; region weights blend with the same coefficients as color,
        // so overlay weights replace canvas weights where the overlay is opaque. Pixels falling outside
        // the canvas are clipped. Canvas buffers may be longer than canvasWidth * canvasHeight pixels
        // (reused scratch space); only the leading part is touched.
        public static void CompositeOver(
            byte[] canvasPixels, byte[] canvasRegions, int canvasWidth, int canvasHeight,
            byte[] overlayPixels, byte[] overlayRegions, int overlayWidth, int overlayHeight,
            int x, int yFromTop, PixelRowOrder order)
        {
            RequireBuffer(canvasPixels, canvasWidth, canvasHeight, nameof(canvasPixels));
            RequireBuffer(canvasRegions, canvasWidth, canvasHeight, nameof(canvasRegions));
            RequireBuffer(overlayPixels, overlayWidth, overlayHeight, nameof(overlayPixels));
            RequireBuffer(overlayRegions, overlayWidth, overlayHeight, nameof(overlayRegions));

            int firstRow = FirstCanvasRow(canvasHeight, overlayHeight, yFromTop, order);
            int firstColumn = Math.Max(0, -x);
            int lastColumn = Math.Min(overlayWidth, canvasWidth - x);
            for (int row = 0; row < overlayHeight; row++)
            {
                int canvasRow = firstRow + row;
                if (canvasRow < 0 || canvasRow >= canvasHeight)
                    continue;

                for (int column = firstColumn; column < lastColumn; column++)
                {
                    int source = (row * overlayWidth + column) * Channels;
                    int target = (canvasRow * canvasWidth + x + column) * Channels;
                    BlendOver(canvasPixels, canvasRegions, target, overlayPixels, overlayRegions, source);
                }
            }
        }

        // recolor_r(s)_c = clamp(round(s_c * target_r_c / reference_r_c), 0, 255).
        // A zero reference channel cannot be scaled, so the source value is kept.
        public static byte RecolorChannel(int source, double target, double reference)
        {
            if (source < 0 || source > 255)
                throw new ArgumentOutOfRangeException(nameof(source));
            if (!(reference > 0))
                return (byte)source;
            return RoundToByte(source * target / reference);
        }

        // Recolors pixelCount pixels in place:
        // out_c = round(s_c * (1 - sum(w)) + sum_r(w_r * recolor_r(s)_c)), w_r = map_r / 255; alpha unchanged.
        // Evaluation order (kept identical in the Python preview): sum(w) adds skin, hair, main, accent in
        // that order; the weighted recolors are added in the same order and then added to s_c * (1 - sum(w)).
        // Pixels whose weights are all zero are left untouched. A malformed map whose weights sum past 255
        // is normalized (w_r = map_r / sum, nothing of the source kept) instead of extrapolating.
        public static void Recolor(byte[] pixels, byte[] regions, int pixelCount, AppearanceColors reference, AppearanceColors target)
        {
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));
            if (regions == null)
                throw new ArgumentNullException(nameof(regions));
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (pixelCount < 0 || (long)pixelCount * Channels > pixels.Length || (long)pixelCount * Channels > regions.Length)
                throw new ArgumentOutOfRangeException(nameof(pixelCount));

            var table = BuildTable(reference, target);
            for (int pixel = 0; pixel < pixelCount; pixel++)
            {
                int offset = pixel * Channels;
                int skin = regions[offset];
                int hair = regions[offset + 1];
                int main = regions[offset + 2];
                int accent = regions[offset + 3];
                int total = skin + hair + main + accent;
                if (total == 0)
                    continue;

                double wSkin, wHair, wMain, wAccent, keep;
                if (total <= 255)
                {
                    wSkin = Weights[skin];
                    wHair = Weights[hair];
                    wMain = Weights[main];
                    wAccent = Weights[accent];
                    keep = 1 - (wSkin + wHair + wMain + wAccent);
                }
                else
                {
                    wSkin = skin / (double)total;
                    wHair = hair / (double)total;
                    wMain = main / (double)total;
                    wAccent = accent / (double)total;
                    keep = 0;
                }

                for (int channel = 0; channel < 3; channel++)
                {
                    int s = pixels[offset + channel];
                    int lookup = channel * 256 + s;
                    double recolored = wSkin * table[lookup] + wHair * table[768 + lookup] +
                        wMain * table[1536 + lookup] + wAccent * table[2304 + lookup];
                    pixels[offset + channel] = RoundToByte(s * keep + recolored);
                }
            }
        }

        // Gives each fully transparent pixel that touches visible pixels the alpha-weighted average color
        // of its visible 8-neighbors, like Unity's import-time "Alpha Is Transparency" edge fill, so
        // bilinear filtering does not pull the silhouette's edge toward black. Alpha stays 0 and visible
        // pixels are untouched (only alpha > 0 pixels are read and only alpha = 0 pixels are written, so
        // the in-place pass is order independent). Row order does not matter.
        public static void BleedTransparentEdges(byte[] pixels, int width, int height)
        {
            RequireBuffer(pixels, width, height, nameof(pixels));
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = (y * width + x) * Channels;
                    if (pixels[offset + 3] != 0)
                        continue;

                    int red = 0, green = 0, blue = 0, coverage = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int row = y + dy;
                        if (row < 0 || row >= height)
                            continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int column = x + dx;
                            if (column < 0 || column >= width)
                                continue;
                            int neighbor = (row * width + column) * Channels;
                            int alpha = pixels[neighbor + 3];
                            if (alpha == 0)
                                continue;
                            red += pixels[neighbor] * alpha;
                            green += pixels[neighbor + 1] * alpha;
                            blue += pixels[neighbor + 2] * alpha;
                            coverage += alpha;
                        }
                    }

                    if (coverage == 0)
                        continue;
                    pixels[offset] = (byte)((red + coverage / 2) / coverage);
                    pixels[offset + 1] = (byte)((green + coverage / 2) / coverage);
                    pixels[offset + 2] = (byte)((blue + coverage / 2) / coverage);
                }
            }
        }

        // Round half to even, then clamp to 0-255. NaN becomes 0.
        public static byte RoundToByte(double value)
        {
            if (!(value > 0))
                return 0;
            if (value >= 255)
                return 255;
            return (byte)Math.Round(value, MidpointRounding.ToEven);
        }

        private static void BlendOver(byte[] canvasPixels, byte[] canvasRegions, int target,
            byte[] overlayPixels, byte[] overlayRegions, int source)
        {
            int sourceAlpha = overlayPixels[source + 3];
            if (sourceAlpha == 0)
                return;

            if (sourceAlpha == 255)
            {
                for (int channel = 0; channel < Channels; channel++)
                {
                    canvasPixels[target + channel] = overlayPixels[source + channel];
                    canvasRegions[target + channel] = overlayRegions[source + channel];
                }
                return;
            }

            // a_out = a_s + a_d (1 - a_s); c_out = (c_s a_s + c_d a_d (1 - a_s)) / a_out.
            double a = sourceAlpha / 255.0;
            double b = canvasPixels[target + 3] / 255.0;
            double alpha = a + b * (1 - a);
            double overlayShare = a / alpha;
            double canvasShare = b * (1 - a) / alpha;
            for (int channel = 0; channel < 3; channel++)
            {
                canvasPixels[target + channel] = RoundToByte(
                    overlayPixels[source + channel] * overlayShare + canvasPixels[target + channel] * canvasShare);
            }
            canvasPixels[target + 3] = RoundToByte(alpha * 255);

            int total = 0;
            for (int channel = 0; channel < Channels; channel++)
            {
                byte weight = RoundToByte(
                    overlayRegions[source + channel] * overlayShare + canvasRegions[target + channel] * canvasShare);
                canvasRegions[target + channel] = weight;
                total += weight;
            }

            // Independent rounding can push a valid pair of maps one or two steps past 255; take the
            // excess from the largest weight (first in R, G, B, A order on ties) to keep sum <= 255.
            while (total > 255)
            {
                int largest = target;
                for (int channel = 1; channel < Channels; channel++)
                    if (canvasRegions[target + channel] > canvasRegions[largest])
                        largest = target + channel;
                canvasRegions[largest]--;
                total--;
            }
        }

        private static double[] BuildTable(AppearanceColors reference, AppearanceColors target)
        {
            // [region][channel][source] flattened: region * 768 + channel * 256 + source.
            var table = new double[AppearanceColors.RegionCount * 3 * 256];
            for (int region = 0; region < AppearanceColors.RegionCount; region++)
            {
                var from = reference.Region(region);
                var to = target.Region(region);
                for (int channel = 0; channel < 3; channel++)
                    for (int source = 0; source < 256; source++)
                        table[region * 768 + channel * 256 + source] = RecolorChannel(source, to[channel], from[channel]);
            }
            return table;
        }

        private static double[] BuildWeights()
        {
            var weights = new double[256];
            for (int value = 0; value < weights.Length; value++)
                weights[value] = value / 255.0;
            return weights;
        }

        private static void RequireBuffer(byte[] buffer, int width, int height, string name)
        {
            if (buffer == null)
                throw new ArgumentNullException(name);
            if (width < 0 || height < 0 || (long)width * height * Channels > buffer.Length)
                throw new ArgumentException("The RGBA buffer is smaller than its stated size.", name);
        }
    }
}
