﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClipperLib;

namespace PolyNester;

using Ngon = List<IntPoint>;
using Ngons = List<List<IntPoint>>;

public class SigmaMinima
{
    private NestQuality quality;
    private Ngon pattern;
    private Ngons subject;
    private bool flip_pattern;
    private Clipper clipps_;

    private SigmaMinima(NestQuality quality, Ngon pattern, Ngons subject, bool flip_pattern)
    {
        this.quality = quality;
        this.pattern = pattern;
        this.subject = subject;
        this.flip_pattern = flip_pattern;
        clipps_ = new Clipper();
    }

    public static Ngons MakeOne(Ngon pattern, Ngons subject, NestQuality quality, bool flip_pattern)
    {
        SigmaMinima sm = new SigmaMinima(quality, pattern, subject, flip_pattern);
        return sm.Extract().GetAwaiter().GetResult();
    }

    internal async Task<Ngons> Extract()
    {        
        var result = quality switch
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
        IntRect pB = GeomUtility.GetBounds(pattern);
        IntRect sB = GeomUtility.GetBounds(subject[0]);

        if (flip_pattern)
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
        Ngon h_p = GeomUtility.ConvexHull(pattern.Clone(0, 0, flip_pattern));
        Ngon h_s = GeomUtility.ConvexHull(subject[0].Clone());

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
        Ngon subj = subject[0];
        Ngon patt = pattern.Clone(0, 0, flip_pattern);

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
        Ngons full = new Ngons();

        long scale = flip_pattern ? -1 : 1;
        clipps_.Clear();
        ///======================Clipper Sequential Usage: 01 =================================!
        for (int i = 0; i < pattern.Count; i++)
        {
            var scaled = subject.Clone(scale * pattern[i].X, scale * pattern[i].Y);
            clipps_.AddPaths(scaled, PolyType.ptSubject, true);
        }
        clipps_.Execute(ClipType.ctUnion, full, PolyFillType.pftNonZero);
        clipps_.Clear();
        ///======================Clipper Sequential Usage: 02 =================================!
        var mks = core.SumBoundary(pattern, subject, flip_pattern);
        clipps_.Clear();
        ///======================Clipper Sequential Usage: 03 =================================!
        clipps_.AddPaths(full, PolyType.ptSubject, true);
        clipps_.AddPaths(mks, PolyType.ptSubject, true);
        Ngons res = new Ngons();
        clipps_.Execute(ClipType.ctUnion, res, PolyFillType.pftNonZero);
        ///======================Clipper Sequential Usage: end ================================!
        clipps_.Clear();
        return res;
    }
}
