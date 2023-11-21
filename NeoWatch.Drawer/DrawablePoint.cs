using System;
using System.ComponentModel;
using NeoWatch.Geometries;

namespace NeoWatch.Drawing
{
    public class DrawablePoint : Point, IDrawable
    {
        public DrawablePoint(float x, float y) : base(x, y) {
            Color = Colors.Black;
            Box = new Box(x - 1, x + 1, y - 1, y + 1);
        }

        public string Parse { get; set; }
        public string Description { get; set; }
        public IColor Color { get; set; }
        private int thickness { get; set; } = 1;
        public int Thickness
        {
            get { return thickness; }
            set { thickness = value; OnPropertyChanged(nameof(Thickness)); }
        }
        // TODO: point should change thickness instead of dash when selected.
        private string dash = "1 0";
        public string Dash
        {
            get { return dash; }
            set { dash = value; OnPropertyChanged(nameof(Dash)); }
        }
        private IGeometry transformedGeometry;
        public IGeometry TransformedGeometry {
            get { return transformedGeometry; }
            set { transformedGeometry = value; OnPropertyChanged(nameof(TransformedGeometry)); }
        }
        public IGeometry TransformedCapGeometry { get; set; }

        public IBox Box { get; set; }

        public void TransformGeometry(IDrawableVisitor visitor) {
            var transformedPoint = visitor.GetTransformedPoint(this);
            TransformedGeometry = visitor.GetTransformedPoint(this);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
