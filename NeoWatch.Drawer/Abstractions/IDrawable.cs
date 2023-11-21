using NeoWatch.Geometries;
using System.ComponentModel;

namespace NeoWatch.Drawing
{
    public interface IDrawable : INotifyPropertyChanged, IBoxable
    {
        string Description { get; set; }
        IColor Color { set; get; }
        // TODO: define default thickness for all drawables (force getter implementation for DefaultThickness).
        int Thickness { set; get; }
        // TODO: define default dash for all drawables (force getter implementation for DefaultThickness).
        string Dash { get; set; }
        IGeometry TransformedGeometry { set; get; }
        IGeometry TransformedCapGeometry { set; get; }
        void TransformGeometry(IDrawableVisitor visitor);
    }
}
