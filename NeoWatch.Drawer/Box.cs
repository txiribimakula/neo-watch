namespace NeoWatch.Drawing
{
    public class Box : IBox
    {
        private float minX;
        public float MinX {
            get => minX;
            set {
                if(value < minX) {
                    minX = value;
                }
            } 
        }
        private float minY;
        public float MinY { 
            get => minY;
            set {
                if(value < minY) {
                    minY = value;
                }
            }
        }
        private float maxX;
        public float MaxX {
            get => maxX;
            set {
                if (value > maxX) {
                    maxX = value;
                }
            }
        }
        private float maxY;
        public float MaxY {
            get => maxY;
            set {
                if (value > maxY) {
                    maxY = value;
                }
            }
        }
        public float HorizontalLength { get { return MaxX - MinX; } }
        public float VerticalLength { get { return MaxY - MinY; } }
        public bool IsValid {
            get {
                return HorizontalLength != 0 || VerticalLength != 0;
            }
        }

        public Box(float minX, float maxX, float minY, float maxY) {
            if(maxX - minX <= 0)
            {
                maxX = minX + 1;
            }
            if (maxY - minY <= 0)
            {
                maxY = minY + 1;
            }

            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
        }

        public void Expand(IBox box) {
            if (!IsValid) {
                MinX = box.MinX;
                MaxX = box.MaxX;
                MinY = box.MinY;
                MaxY = box.MaxY;
            } else {
                if (box.MinX < MinX) {
                    MinX = box.MinX;
                }
                if (box.MaxX > MaxX) {
                    MaxX = box.MaxX;
                }
                if (box.MinY < MinY) {
                    MinY = box.MinY;
                }
                if (box.MaxY > MaxY) {
                    MaxY = box.MaxY;
                }
            }
        }

        public object Clone() {
            return MemberwiseClone();
        }
    }
}
