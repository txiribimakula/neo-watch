using System;

namespace NeoWatch.Drawing
{
    public interface IBox : ICloneable
    {
        float MinX { get; set; }
        float MinY { get; set; }
        float MaxX { get; set; }
        float MaxY { get; set; }
        float HorizontalLength { get; }
        float VerticalLength { get; }
        bool IsValid { get; }

        void Expand(IBox box);
    }
}
