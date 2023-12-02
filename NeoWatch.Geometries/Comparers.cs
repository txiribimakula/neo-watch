using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoWatch.Geometries
{
    public static class Comparers
    {
        public static float Tolerance => 0.0001f;

        public static bool EqualsWithTolerance(this float value, float otherValue)
        {
            if(Math.Abs(value - otherValue) > Tolerance) return false;

            return true;
        }
    }
}
