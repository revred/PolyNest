using ClipperLib;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PolyNester;

using Ngon = List<IntPoint>;

struct Pose2D
{
    public Pose2D()
    {
    }

    public double best_t = 0;
    public int best = 0;
    public long best_area = long.MaxValue;
    public bool flip_best = false;

    public Task BestPose2D(Ngon hull)
    {
        int n = hull.Count;
        for (int i = 0; i < n; i++)
        {
            double t = GeomUtility.AlignToEdgeRotation(hull, i);

            Mat3x3 rot = Mat3x3.RotateCounterClockwise(t);

            Ngon clone = hull.Clone(rot);

            IntRect bounds = GeomUtility.GetBounds(clone);
            long area = bounds.Area();
            double aspect = bounds.Aspect();

            if (area < best_area)
            {
                best_area = area;
                best = i;
                best_t = t;
                flip_best = aspect > 1.0;
            }
        }
        return Task.CompletedTask;
    }
}
