using System.Collections.Generic;
using System.Threading.Tasks;
using ClipperLib;

namespace PolyNester;

using Ngon = List<IntPoint>;
using Ngons = List<List<IntPoint>>;

public class MinkowskiCore
{
    Clipper clipps_;
    public Clipper Clipps => clipps_;

    public MinkowskiCore(Clipper clipps)
    {
        this.clipps_ = clipps;
    }
    public Ngons SumBoundary(Ngon pattern, Ngons path, bool flip_pattern)
    {
        Ngons full = new Ngons();
        Ngons res = new Ngons();

        for (int i = 0; i < path.Count; i++)
        {
            clipps_.Clear();

            Ngons seg = SumBoundary(pattern, path[i], flip_pattern);
            clipps_.AddPaths(full, PolyType.ptSubject, true);
            clipps_.AddPaths(seg, PolyType.ptSubject, true);
            clipps_.Execute(ClipType.ctUnion, res, PolyFillType.pftNonZero);
            full = res;
            res.Clear();           
        }
        clipps_.Clear();

        return full;
    }

    public Ngons SumBoundary(Ngon pattern, Ngon path, bool flip_pattern)
    {   
        Ngons full = new Ngons();
        for (int i = 0; i < path.Count; i++)
        {
            clipps_.Clear();
            IntPoint p1 = path[i];
            IntPoint p2 = path[(i + 1) % path.Count];

            Ngons seg = SumSegment(pattern, p1, p2, flip_pattern);
            clipps_.AddPaths(full, PolyType.ptSubject, true);
            clipps_.AddPaths(seg, PolyType.ptSubject, true);

            Ngons res = new Ngons();
            clipps_.Execute(ClipType.ctUnion, res, PolyFillType.pftNonZero);
            full = res;
        }

        clipps_.Clear();
        return full;
    }

    public Ngons SumSegment(Ngon pattern, IntPoint p1, IntPoint p2, bool flip_pattern)
    {
        Ngon p1_c = pattern.Clone(p1.X, p1.Y, flip_pattern);
        if (p1 == p2) return new Ngons() { p1_c };
        Ngon p2_c = pattern.Clone(p2.X, p2.Y, flip_pattern);

        Ngons full = new Ngons();

        clipps_.Clear();

        clipps_.AddPath(p1_c, PolyType.ptSubject, true);
        clipps_.AddPath(p2_c, PolyType.ptSubject, true);

        var ng12 = new Ngon() { p1, p2 };
        var aPath = Clipper.MinkowskiSum(pattern.Clone(0, 0, flip_pattern), ng12, false);
        clipps_.AddPaths(aPath, PolyType.ptSubject, true);
        clipps_.Execute(ClipType.ctUnion, full, PolyFillType.pftNonZero);

        clipps_.Clear();
        return full;
    }
}
