namespace NeoWatch.Drawing
{
    public class Color : IColor
    {
        public Color(int red, int green, int blue) {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public int Red { get; set; }
        public int Green { get; set; }
        public int Blue { get; set; }
        public int Alpha { get; set; }

        public string Hex => string.Format("#{0:X2}{1:X2}{2:X2}", Red, Green, Alpha);
    }

    public static class Colors
    {
        public static IColor Black => new Color(0, 0, 0);
    }
}
