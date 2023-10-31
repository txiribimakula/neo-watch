namespace NeoWatch.Drawing
{
    public class GeometryDrawer
    {
        public DrawableVisitor DrawableVisitor { get; set; }

        public GeometryDrawer(DrawableVisitor visitor) {
            DrawableVisitor = visitor;
        }

        public void TransformGeometries(DrawableCollection drawables) {
            foreach (var drawable in drawables) {
                drawable.TransformGeometry(DrawableVisitor);
            }
        }

        public void TransformGeometries((IDrawable, IDrawable) drawables) {
            if(drawables.Item1 != null && drawables.Item2 != null) {
                drawables.Item1.TransformGeometry(DrawableVisitor);
                drawables.Item2.TransformGeometry(DrawableVisitor);
            }
        }

        public void TransformGeometry(IDrawable drawable) {
            drawable.TransformGeometry(DrawableVisitor);
        }
    }
}
