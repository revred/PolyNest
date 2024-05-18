using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClipperLib;

namespace PolyNester;

using Ngon = List<IntPoint>;
using Ngons = List<List<IntPoint>>;

public static class GeomUtility
{
    private class PolarComparer : IComparer
    {
        public static int CompareIntPoint(IntPoint A, IntPoint B)
        {
            long det = A.Y * B.X - A.X * B.Y;

            if (det == 0)
            {
                long dot = A.X * B.X + A.Y * B.Y;
                if (dot >= 0)
                    return 0;
            }

            if (A.Y == 0 && A.X > 0)
                return -1;
            if (B.Y == 0 && B.X > 0)
                return 1;
            if (A.Y > 0 && B.Y < 0)
                return -1;
            if (A.Y < 0 && B.Y > 0)
                return 1;
            return det > 0 ? 1 : -1;
        }

        int IComparer.Compare(object a, object b)
        {
            IntPoint A = (IntPoint)a;
            IntPoint B = (IntPoint)b;

            return CompareIntPoint(A, B);
        }
    }

    public static long Width(this IntRect rect)
    {
        return Math.Abs(rect.left - rect.right);
    }

    public static long Height(this IntRect rect)
    {
        return Math.Abs(rect.top - rect.bottom);
    }

    public static long Area(this IntRect rect)
    {
        return rect.Width() * rect.Height();
    }

    public static double Aspect(this IntRect rect)
    {
        return ((double)rect.Width()) / rect.Height();
    }

    public static Ngon Clone(this Ngon poly)
    {
        return new Ngon(poly);
    }

    public static Ngon Clone(this Ngon poly, long shift_x, long shift_y, bool flip_first = false)
    {
        long scale = flip_first ? -1 : 1;

        Ngon clone = new Ngon(poly.Count);
        for (int i = 0; i < poly.Count; i++)
            clone.Add(new IntPoint(scale * poly[i].X + shift_x, scale * poly[i].Y + shift_y));
        return clone;
    }

    public static Ngon Clone(this Ngon poly, Mat3x3 T)
    {
        Ngon clone = new Ngon(poly.Count);
        for (int i = 0; i < poly.Count; i++)
            clone.Add(T * poly[i]);
        return clone;
    }

    public static Ngons Clone(this Ngons polys)
    {
        Ngons clone = new Ngons(polys.Count);
        for (int i = 0; i < polys.Count; i++)
            clone.Add(polys[i].Clone());
        return clone;
    }

    public static Ngons Clone(this Ngons polys, long shift_x, long shift_y, bool flip_first = false)
    {
        Ngons clone = new Ngons(polys.Count);
        for (int i = 0; i < polys.Count; i++)
            clone.Add(polys[i].Clone(shift_x, shift_y, flip_first));
        return clone;
    }

    public static Ngons Clone(this Ngons polys, Mat3x3 T)
    {
        Ngons clone = new Ngons(polys.Count);
        for (int i = 0; i < polys.Count; i++)
            clone.Add(polys[i].Clone(T));
        return clone;
    }

    public static IntRect GetBounds(IEnumerable<IntPoint> points)
    {
        long width_min = long.MaxValue;
        long width_max = long.MinValue;
        long height_min = long.MaxValue;
        long height_max = long.MinValue;

        foreach (IntPoint p in points)
        {
            width_min = Math.Min(width_min, p.X);
            height_min = Math.Min(height_min, p.Y);
            width_max = Math.Max(width_max, p.X);
            height_max = Math.Max(height_max, p.Y);
        }

        return new IntRect(width_min, height_max, width_max, height_min);
    }

    public static Rect64 GetBounds(IEnumerable<Vector64> points)
    {
        double width_min = double.MaxValue;
        double width_max = double.MinValue;
        double height_min = double.MaxValue;
        double height_max = double.MinValue;

        foreach (Vector64 p in points)
        {
            width_min = Math.Min(width_min, p.X);
            height_min = Math.Min(height_min, p.Y);
            width_max = Math.Max(width_max, p.X);
            height_max = Math.Max(height_max, p.Y);
        }

        return new Rect64(width_min, height_max, width_max, height_min);
    }

    public static void GetRefitTransform(IEnumerable<Vector64> points, Rect64 target, bool stretch, out Vector64 scale, out Vector64 shift)
    {
        Rect64 bds = GetBounds(points);

        scale = new Vector64(target.Width() / bds.Width(), target.Height() / bds.Height());

        if (!stretch)
        {
            double s = Math.Min(scale.X, scale.Y);
            scale = new Vector64(s, s);
        }

        shift = new Vector64(-bds.left, -bds.bottom) * scale
            + new Vector64(Math.Min(target.left, target.right), Math.Min(target.bottom, target.top));
    }

    public static Ngon ConvexHull(Ngon subject, double rigidness = 0)
    {
        if (subject.Count == 0) return new Ngon();
        if (rigidness >= 1) return subject.Clone();

        subject = subject.Clone();

        if (Clipper.Area(subject) < 0) Clipper.ReversePaths(new Ngons() { subject });

        Ngon last_hull = new Ngon();
        Ngon hull = subject;

        double subj_area = Clipper.Area(hull);

        int last_vert = 0;
        for (int i = 1; i < subject.Count; i++)
            if (hull[last_vert].Y > hull[i].Y)
                last_vert = i;

        while (last_hull.Count != hull.Count)
        {
            last_hull = hull;
            hull = new Ngon();
            hull.Add(last_hull[last_vert]);

            int steps_since_insert = 0;
            int max_steps = rigidness <= 0 ? int.MaxValue : (int)Math.Round(10 - (10 * rigidness));

            int n = last_hull.Count;

            int start = last_vert;
            for (int i = 1; i < n; i++)
            {
                IntPoint a = last_hull[last_vert];
                IntPoint b = last_hull[(start + i) % n];
                IntPoint c = last_hull[(start + i + 1) % n];

                IntPoint ab = new IntPoint(b.X - a.X, b.Y - a.Y);
                IntPoint ac = new IntPoint(c.X - a.X, c.Y - a.Y);

                if (ab.Y * ac.X < ab.X * ac.Y || steps_since_insert >= max_steps)
                {
                    hull.Add(b);
                    last_vert = (start + i) % n;
                    steps_since_insert = -1;
                }
                steps_since_insert++;
            }

            last_vert = 0;

            double hull_area = Clipper.Area(hull);

            if (subj_area / hull_area < Math.Sqrt(rigidness))
            {
                hull = Clipper.SimplifyPolygon(hull, PolyFillType.pftNonZero)[0];
                break;
            }
        }

        return hull;
    }

    public static Ngon CanFitInsidePolygon(IntRect canvas, Ngon pattern)
    {
        IntRect bds = GetBounds(pattern);

        long l = canvas.left - bds.left;
        long r = canvas.right - bds.right;
        long t = canvas.top - bds.top;
        long b = canvas.bottom - bds.bottom;

        if (l > r || b > t)
            return null;

        return new Ngon() { new IntPoint(l, b), new IntPoint(r, b), new IntPoint(r, t), new IntPoint(l, t) };
    }

    public static double AlignToEdgeRotation(Ngon target, int edge_start)
    {
        edge_start %= target.Count;
        int next_pt = (edge_start + 1) % target.Count;
        IntPoint best_edge = new IntPoint(target[next_pt].X - target[edge_start].X, target[next_pt].Y - target[edge_start].Y);
        return -Math.Atan2(best_edge.Y, best_edge.X);
    }

    public static bool AlmostRectangle(Ngon target, double percent_diff = 0.05)
    {
        IntRect bounds = GetBounds(target);
        double area = Math.Abs(Clipper.Area(target));

        return 1.0 - area / bounds.Area() < percent_diff;
    }
}
