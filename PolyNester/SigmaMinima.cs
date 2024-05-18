using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClipperLib;

namespace PolyNester;

using Ngon = List<IntPoint>;
using Ngons = List<List<IntPoint>>;

public class SigmaMinima
{
    private NestQuality quality_;
    private Ngon pattern_;
    private Ngons subject_;
    private bool flip_;
    private Clipper clipps_;

    private SigmaMinima(NestQuality quality, Ngon pattern, Ngons subject, bool flip_pattern)
    {
        quality_ = quality;
        pattern_ = pattern;
        subject_ = subject;
        flip_ = flip_pattern;

        if (quality_ == NestQuality.ConcaveFull || quality_ == NestQuality.Full)
            clipps_ = new Clipper();
    }

    public static Ngons MakeOne(Ngon pattern, Ngons subject, NestQuality quality, bool flip_pattern)
    {
        SigmaMinima sm = new SigmaMinima(quality, pattern, subject, flip_pattern);
        return sm.Extract().GetAwaiter().GetResult();
    }

    internal async Task<Ngons> Extract()
    {        
        var result = quality_ switch
        {
            NestQuality.Simple => MSumSimple(),
            NestQuality.Convex => MSumConvex(),
            NestQuality.ConcaveLight => MSumConcave(0.25),
            NestQuality.ConcaveMedium => MSumConcave(0.55),
            NestQuality.ConcaveHigh => MSumConcave(0.85),
            NestQuality.ConcaveFull => MSumConcave(1.0),
            NestQuality.Full => MSumFull(),
            _ => null
        };
        await Task.Delay(0);
        return result;
    }

    private Ngons MSumSimple()
    {
        IntRect pB = GeomUtility.GetBounds(pattern_);
        IntRect sB = GeomUtility.GetBounds(subject_[0]);

        if (flip_)
        {
            pB = new IntRect(-pB.right, -pB.bottom, -pB.left, -pB.top);
        }

        long l = pB.left + sB.left;
        long r = pB.right + sB.right;
        long t = pB.top + sB.top;
        long b = pB.bottom + sB.bottom;

        Ngon p = new Ngon() { new IntPoint(l, b), new IntPoint(r, b), new IntPoint(r, t), new IntPoint(l, t) };
        return new Ngons() { p };
    }

    private Ngons MSumConvex()
    {
        Ngon h_p = GeomUtility.ConvexHull(pattern_.Clone(0, 0, flip_));
        Ngon h_s = GeomUtility.ConvexHull(subject_[0].Clone());

        int n_p = h_p.Count;
        int n_s = h_s.Count;

        int sp = 0;
        for (int k = 0; k < n_p; k++)
            if (h_p[k].Y < h_p[sp].Y)
                sp = k;

        int ss = 0;
        for (int k = 0; k < n_s; k++)
            if (h_s[k].Y < h_s[ss].Y)
                ss = k;

        Ngon poly = new Ngon(n_p + n_s);

        int i = 0;
        int j = 0;
        while (i < n_p || j < n_s)
        {
            int ip = (sp + i + 1) % n_p;
            int jp = (ss + j + 1) % n_s;
            int ii = (sp + i) % n_p;
            int jj = (ss + j) % n_s;

            IntPoint sum = new IntPoint(h_p[ii].X + h_s[jj].X, h_p[ii].Y + h_s[jj].Y);
            IntPoint v = new IntPoint(h_p[ip].X - h_p[ii].X, h_p[ip].Y - h_p[ii].Y);
            IntPoint w = new IntPoint(h_s[jp].X - h_s[jj].X, h_s[jp].Y - h_s[jj].Y);

            poly.Add(sum);

            if (i == n_p)
            {
                j++;
                continue;
            }

            if (j == n_s)
            {
                i++;
                continue;
            }

            long cross = v.Y * w.X - v.X * w.Y;

            if (cross < 0) i++;
            else if (cross > 0) j++;
            else
            {
                long dot = v.X * w.X + v.Y * w.Y;
                if (dot > 0)
                {
                    i++;
                    j++;
                }
                else
                {
                    throw new Exception();
                }
            }
        }

        return Clipper.SimplifyPolygon(poly);
    }

    private Ngons MSumConcave(double rigidness = 1.0)
    {
        Ngon subj = subject_[0];
        Ngon patt = pattern_.Clone(0, 0, flip_);

        if (rigidness < 1.0)
        {
            subj = GeomUtility.ConvexHull(subj, rigidness);
            patt = GeomUtility.ConvexHull(patt, rigidness);
        }

        var core = new MinkowskiCore(clipps_);
        Ngons sres = core.SumBoundary(patt, subj, false);
        return sres.Count == 0 ? sres : new Ngons() { sres[0] };
    }

    Ngons MSumFull()
    {
        var core = new MinkowskiCore(clipps_);
        var mks = core.SumBoundary(pattern_, subject_, flip_);
        
        var full = PatternFlipper();
        Clipper clipps = new Clipper();
        clipps.AddPaths(full, PolyType.ptSubject, true);
        clipps.AddPaths(mks, PolyType.ptSubject, true);
        Ngons res = new Ngons();
        clipps.Execute(ClipType.ctUnion, res, PolyFillType.pftNonZero);       
        
        return res;
    }

    Ngons PatternFlipper()
    {
        Ngons full = new Ngons();
        long scale = flip_ ? -1 : 1;
        Clipper clipps = new Clipper();

        for (int i = 0; i < pattern_.Count; i++)
        {
            var scaled = subject_.Clone(scale * pattern_[i].X, scale * pattern_[i].Y);
            clipps.AddPaths(scaled, PolyType.ptSubject, true);
        }
        clipps.Execute(ClipType.ctUnion, full, PolyFillType.pftNonZero);
        return full;
    }
}
