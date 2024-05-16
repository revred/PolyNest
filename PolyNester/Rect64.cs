using System;
using System.Collections.Generic;
using ClipperLib;

namespace PolyNester
{
    public struct Rect64
    {
        public double left;
        public double right;
        public double top;
        public double bottom;

        public Rect64(double l, double t, double r, double b)
        {
            left = l;
            right = r;
            top = t;
            bottom = b;
        }

        public double Width()
        {
            return Math.Abs(left - right);
        }

        public double Height()
        {
            return Math.Abs(top - bottom);
        }

        public double Area()
        {
            return Width() * Height();
        }

        public double Aspect()
        {
            return Width() / Height();
        }
    }
}
