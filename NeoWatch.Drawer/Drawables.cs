﻿using System.Collections.Generic;

namespace NeoWatch.Drawing
{
    public class Drawables : List<IDrawable>
    {
        public string Type { get; set; }

        public string Error { get; set; }
    }
}
