using System.ComponentModel;
using NeoWatch.Geometries;

namespace NeoWatch.Drawing
{
    public class Axis : IDrawable
    {
        public Axis(IBox box) {
            Color = Colors.Black;
            Box = box;
        }

        public string Description { get; set; }
        public string Error { get; set; }
        public IColor Color { get; set; }
        public IGeometry TransformedGeometry { get; set; }
        public IGeometry TransformedCapGeometry { get; set; }
        public IBox Box { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public bool IsHorizontal => Box.HorizontalLength > Box.VerticalLength;

        public int Thickness { get; set; }
        public string Dash { get; set; }

        public void TransformGeometry(IDrawableVisitor visitor) {
            var coordinatesOriginWorld = visitor.GetTransformedPoint(new Point(0, 0));
            if(IsHorizontal) {
                TransformedGeometry = new LineSegment(new Point(Box.MinX, coordinatesOriginWorld.Y), new Point(Box.MaxX, coordinatesOriginWorld.Y));
            } else {
                TransformedGeometry = new LineSegment(new Point(coordinatesOriginWorld.X, Box.MinY), new Point(coordinatesOriginWorld.X, Box.MaxY));
            }
        }
    }
}
