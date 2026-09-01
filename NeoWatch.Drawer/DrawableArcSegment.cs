using System;
using System.Collections.Generic;
using System.ComponentModel;
using NeoWatch.Geometries;

namespace NeoWatch.Drawing
{
    public class DrawableArcSegment : ArcSegment, IDrawable {
        public DrawableArcSegment(Point centerPoint, float initialAngle, float sweepAngle, float radius)
            : base(centerPoint, initialAngle, sweepAngle, radius) {
            Color = Colors.Black;
            SetBox();
        }

        public string Description { get; set; }
        public IColor Color { get; set; }
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

        private void SetBox() {
            var bounds = Scene.ScenePrimitive.Copy(this).Bounds;
            Box = new Box((float)bounds.MinX, (float)bounds.MaxX, (float)bounds.MinY, (float)bounds.MaxY);
        }

        public void TransformGeometry(IDrawableVisitor visitor) {
            TransformedGeometry = visitor.GetTransformedArc(this);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
