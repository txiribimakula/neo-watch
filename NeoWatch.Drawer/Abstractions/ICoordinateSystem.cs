using NeoWatch.Geometries;

namespace NeoWatch.Drawing
{
    public interface ICoordinateSystem : IBoxable
    {
        float Scale { get; set; }
        Point Offset { get; set; }

        float LocalWidth { get; set; }
        float LocalHeight { get; set; }
        float WorldWidth { get; set; }
        float WorldHeight { get; set; }
        float LocalMinX { get; set; }
        float LocalMaxX { get; set; }
        float LocalMinY { get; set; }
        float LocalMaxY { get; set; }

        Point ConvertPointToWorld(Point point);
        Point ConvertPointToLocal(Point point);
        float ConvertXToWorld(float x);
        float ConvertYToWorld(float y);
        float ConvertLengthToWorld(float length);
        float ConvertXToLocal(float x);
        float ConvertYToLocal(float y);
        float ConvertLengthToLocal(float length);

        void ReCalculate(float worldWidth, float worldHeight);
    }
}
