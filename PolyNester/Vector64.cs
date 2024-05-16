using System.Collections.Generic;
using ClipperLib;

namespace PolyNester
{
    public struct Vector64
    {
        public double X;
        public double Y;
        public Vector64(double X, double Y)
        {
            this.X = X; this.Y = Y;
        }

        public Vector64(Vector64 pt)
        {
            this.X = pt.X; this.Y = pt.Y;
        }

        public static Vector64 operator +(Vector64 a, Vector64 b)
        {
            return new Vector64(a.X + b.X, a.Y + b.Y);
        }

        public static Vector64 operator -(Vector64 a, Vector64 b)
        {
            return new Vector64(a.X - b.X, a.Y - b.Y);
        }

        public static Vector64 operator *(Vector64 a, Vector64 b)
        {
            return new Vector64(a.X * b.X, a.Y * b.Y);
        }

        public static Vector64 operator /(Vector64 a, Vector64 b)
        {
            return new Vector64(a.X / b.X, a.Y / b.Y);
        }

        public static Vector64 operator *(Vector64 a, double b)
        {
            return new Vector64(a.X * b, a.Y * b);
        }

        public static Vector64 operator *(double b, Vector64 a)
        {
            return new Vector64(a.X * b, a.Y * b);
        }

        public static bool operator ==(Vector64 a, Vector64 b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(Vector64 a, Vector64 b)
        {
            return a.X != b.X || a.Y != b.Y;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj is Vector64)
            {
                Vector64 a = (Vector64)obj;
                return (X == a.X) && (Y == a.Y);
            }
            else return false;
        }

        public override int GetHashCode()
        {
            return (X.GetHashCode() ^ Y.GetHashCode());
        }

    }
}
