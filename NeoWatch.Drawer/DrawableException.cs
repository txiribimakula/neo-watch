using System;

namespace NeoWatch.Drawing
{
    public class DrawableException : Exception
    {
        public DrawableException(IDrawable drawable)
        {
            Drawable = drawable;
        }

        public virtual IDrawable Drawable { get; }
    }
}
