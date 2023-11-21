using System.ComponentModel;
using NeoWatch.Geometries;

namespace NeoWatch.Drawing
{
    public class Drawable : IDrawable
    {
        public Drawable(string description)
        {
            Description = description;
        }

        public string Parse { get; set; }
        public string Description { get; set; }
        public IColor Color { get; set; }
        public int Thickness { get; set; }
        public string Dash { get; set; }
        public IGeometry TransformedGeometry { get; set; }
        public IGeometry TransformedCapGeometry { get; set; }
        public IBox Box { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void TransformGeometry(IDrawableVisitor visitor)
        {
        }
    }
}
