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

        public static string NextColor()
        {
            currentColorIndex++;
            if (currentColorIndex == colors.Length)
            {
                currentColorIndex = 0;
            }
            return "#" + (System.Drawing.Color.FromKnownColor(colors[currentColorIndex]).ToArgb() & 0x00FFFFFF).ToString("X6");
        }
    }
}
