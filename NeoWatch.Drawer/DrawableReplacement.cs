namespace NeoWatch.Drawing
{
    /// <summary>One drawable that has to take the place of another, at the same index.</summary>
    public class DrawableReplacement
    {
        public DrawableReplacement(int index, IDrawable drawable)
        {
            Index = index;
            Drawable = drawable;
        }

        public int Index { get; private set; }

        public IDrawable Drawable { get; private set; }
    }
}
