using System;
using System.ComponentModel;
using NeoWatch.Geometries;

namespace NeoWatch.Drawing
{
    public class DrawableLineSegment : LineSegment, IDrawable
    {
        public DrawableLineSegment(Point initialPoint, Point finalPoint) : base(initialPoint, finalPoint) {
            Color = Colors.Black;
            SetBox();
        }

        public string Description { get; set; }
        public string Error { get; set; }
        public IColor Color { set; get; }
        private int thickness { get; set; } = 1;
        public int Thickness
        {
            get { return thickness; }
            set { thickness = value; OnPropertyChanged(nameof(Thickness)); }
        }
        private string dash = "1 0";
        public string Dash
        {
            get { return dash; }
            set { dash = value; OnPropertyChanged(nameof(Dash)); }
        }
        public IBox Box { get; set; }
        private IGeometry transformedGeometry;
        public IGeometry TransformedGeometry {
            get { return transformedGeometry; }
            set { transformedGeometry = value; OnPropertyChanged(nameof(TransformedGeometry)); }
        }

        public IGeometry TransformedCapGeometry { get; set; }

        public void TransformGeometry(IDrawableVisitor visitor) {
            var transformedSegment = visitor.GetTransformedSegment(this);
            TransformedGeometry = transformedSegment;

            var transformedSegmentLength = Math.Sqrt(Math.Pow(transformedSegment.FinalPoint.X - transformedSegment.InitialPoint.X, 2) + Math.Pow(transformedSegment.FinalPoint.Y - transformedSegment.InitialPoint.Y, 2));
            var capRatio = 2 / transformedSegmentLength;
            var newCapSegmentPoint = new Point(
                (float)((1 - capRatio) * transformedSegment.FinalPoint.X + capRatio * transformedSegment.InitialPoint.X),
                (float)((1 - capRatio) * transformedSegment.FinalPoint.Y + capRatio * transformedSegment.InitialPoint.Y)
            );
            TransformedCapGeometry = new LineSegment(newCapSegmentPoint, transformedSegment.FinalPoint);
            OnPropertyChanged(nameof(TransformedCapGeometry));
        }

        private void SetBox() {
            float minX, maxX, minY, maxY;
            minX = InitialPoint.X < FinalPoint.X ? InitialPoint.X : FinalPoint.X;
            maxX = InitialPoint.X > FinalPoint.X ? InitialPoint.X : FinalPoint.X;
            minY = InitialPoint.Y < FinalPoint.Y ? InitialPoint.Y : FinalPoint.Y;
            maxY = InitialPoint.Y > FinalPoint.Y ? InitialPoint.Y : FinalPoint.Y;

            Box = new Box(minX, maxX, minY, maxY);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
