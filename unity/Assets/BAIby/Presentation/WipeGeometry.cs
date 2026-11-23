using System;

namespace BAIby.Presentation
{
    internal static class WipeGeometry
    {
        // Measure from the closest point on the whole swipe, including both ends.
        public static bool TouchesSpot(double startX, double startY, double endX, double endY,
            double targetX, double targetY, double radius)
        {
            var swipeX = endX - startX;
            var swipeY = endY - startY;
            var lengthSquared = swipeX * swipeX + swipeY * swipeY;
            var along = lengthSquared == 0
                ? 0
                : ((targetX - startX) * swipeX + (targetY - startY) * swipeY) / lengthSquared;
            along = Math.Max(0, Math.Min(1, along));

            var offsetX = startX + along * swipeX - targetX;
            var offsetY = startY + along * swipeY - targetY;
            return offsetX * offsetX + offsetY * offsetY <= radius * radius;
        }
    }
}
