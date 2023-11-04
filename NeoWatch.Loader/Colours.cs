using System.Drawing;

namespace NeoWatch.Loading
{
    public static class Colours
    {
        private static int currentColorIndex = -1;

        private static KnownColor[] colors = {
            KnownColor.Black,
            KnownColor.DarkRed,
            KnownColor.DarkGreen,
            KnownColor.Yellow,
            KnownColor.DarkOrange,
            KnownColor.DarkBlue,
            KnownColor.DeepPink,
            KnownColor.HotPink,
            KnownColor.Brown,
        };

        public static Color NextColor()
        {
            currentColorIndex++;
            if (currentColorIndex == colors.Length)
            {
                currentColorIndex = 0;
            }
            return Color.FromKnownColor(colors[currentColorIndex]);
        }

        public static string AsHex(this Color color)
        {
            return "#" + (color.ToArgb() & 0x00FFFFFF).ToString("X6");
        }
    }
}
