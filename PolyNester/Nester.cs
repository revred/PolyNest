using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ClipperLib;

namespace PolyNester
{

    using Ngon = List<IntPoint>;
    using Ngons = List<List<IntPoint>>;

    public enum NestQuality { Simple, Convex, ConcaveLight, ConcaveMedium, ConcaveHigh, ConcaveFull, Full }

    public static class ConverterUtility
    {
        public static IntPoint ToIntPoint(this Vector64 vec) => new IntPoint(vec.X, vec.Y);
        public static Vector64 ToVector64(this IntPoint vec) => new Vector64(vec.X, vec.Y);
        public static IntRect ToIntRect(this Rect64 rec) 
            => new IntRect((long)rec.left, (long)rec.top, (long)rec.right, (long)rec.bottom);
        public static Rect64 ToRect64(this IntRect rec) 
            => new Rect64(rec.left, rec.top, rec.right, rec.bottom);
    }

    public class Nester
    {
        private class PolyForm
        {
            public Ngons npoly;
            public Mat3x3 tform;

            public IntPoint TransformPolyPoint(int idPoly, int index) => tform * npoly[idPoly][index];

            public Ngons TransformPoly()
            {
                Ngons nMain = new Ngons(npoly.Count);
                for (int i = 0; i < npoly.Count; i++)
                {
                    Ngon ni = new Ngon(npoly[i].Count);
                    for (int j = 0; j < npoly[i].Count; j++)
                        ni.Add(TransformPolyPoint(i, j));
                    nMain.Add(ni);
                }
                return nMain;
            }
        }

        private class PolyCmd
        {
            public Action<object[]> Call;
            public object[] param;
        }

        const long unit_scale = 10000000;
        List<PolyForm> libPolys_;   // list of saved polygons for reference by handle, stores raw poly positions and transforms
        Queue<PolyCmd> cmdBuffr_;   // buffers list of commands which will append transforms to elements of poly_lib on execute
        BackgroundWorker bkWorker_; // used to execute command buffer in background

        public int PolySpace => libPolys_.Count;

        public Nester()
        {
            libPolys_ = new List<PolyForm>();
            cmdBuffr_ = new Queue<PolyCmd>();
        }

        public void ExecuteCmdBuffer(Action<ProgressChangedEventArgs> callback_progress, Action<AsyncCompletedEventArgs> callback_completed)
        {
            bkWorker_ = new BackgroundWorker();
            bkWorker_.WorkerSupportsCancellation = true;
            bkWorker_.WorkerReportsProgress = true;

            if (callback_progress != null)
                bkWorker_.ProgressChanged += (sender, e) => callback_progress.Invoke(e);
            if (callback_completed != null)
                bkWorker_.RunWorkerCompleted += (sender, e) => callback_completed.Invoke(e);

            bkWorker_.DoWork += BkWorkerDoWork;
            bkWorker_.RunWorkerCompleted += BkRunCompleted;

            bkWorker_.RunWorkerAsync();
        }

        public bool CancelExecute()
        {
            if (!IsBusy()) return false;

            bkWorker_.CancelAsync();
            return true;
        }

        private void BkRunCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled || e.Error != null)
            {
                ResetTransformLib();
                cmdBuffr_.Clear();
            }

            bkWorker_.Dispose();
        }

        private void BkWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            while (cmdBuffr_.Count > 0)
            {
                PolyCmd cmd = cmdBuffr_.Dequeue();
                cmd.Call(cmd.param);

                if (bkWorker_.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }
            }
        }

        public void ClearCommandBuffer() => cmdBuffr_.Clear();
        public bool IsBusy() => bkWorker_?.IsBusy ?? false;

        private HashSet<int> Preprocess(IEnumerable<int> handles)
        {
            if (handles == null) handles = Enumerable.Range(0, libPolys_.Count);
            HashSet<int> unique = new HashSet<int>();
            foreach (int i in handles) unique.Add(i);
            return unique;
        }

        public void OddCmdScale(int handle, double scale_x, double scale_y)
        {
            cmdBuffr_.Enqueue(new PolyCmd() { Call = CmdScale, param = new object[] { handle, scale_x, scale_y } });
        }

        private void CmdScale(params object[] param)
        {
            int handle = (int)param[0];
            double scale_x = (double)param[1];
            double scale_y = (double)param[2];
            libPolys_[handle].tform = Mat3x3.Scale(scale_x, scale_y) * libPolys_[handle].tform;
        }

        public void OddCmdRotate(int handle, double theta)
        {
            cmdBuffr_.Enqueue(new PolyCmd() { Call = CmdRotate, param = new object[] { handle, theta } });
        }

        private void CmdRotate(params object[] param)
        {
            int handle = (int)param[0];
            double theta = (double)param[1];
            libPolys_[handle].tform = Mat3x3.RotateCounterClockwise(theta) * libPolys_[handle].tform;
        }

        public void OddCmdTranslate(int handle, double translate_x, double translate_y)
        {
            cmdBuffr_.Enqueue(new PolyCmd() { Call = CmdTranslate, param = new object[] { handle, translate_x, translate_y } });
        }

        private void CmdTranslate(params object[] param)
        {
            int handle = (int)param[0];
            double translate_x = (double)param[1];
            double translate_y = (double)param[2];
            libPolys_[handle].tform = Mat3x3.Translate(translate_x, translate_y) * libPolys_[handle].tform;
        }

        public void OddCmdTranslateOriginToZero(IEnumerable<int> handles)
        {
            cmdBuffr_.Enqueue(new PolyCmd() { Call = cmd_translate_origin_to_zero, param = new object[] { handles } });
        }

        private void cmd_translate_origin_to_zero(params object[] param)
        {
            HashSet<int> unique = Preprocess(param[0] as IEnumerable<int>);

            foreach (int i in unique)
            {
                IntPoint o = libPolys_[i].TransformPolyPoint(0, 0);
                CmdTranslate(i, (double)-o.X, (double)-o.Y);
            }
        }

        public void OddCmdRefit(Rect64 target, bool stretch, IEnumerable<int> handles)
        {
            cmdBuffr_.Enqueue(new PolyCmd() { Call = cmd_refit, param = new object[] { target, stretch, handles } });
        }

        private void cmd_refit(params object[] param)
        {
            Rect64 target = (Rect64)param[0];
            bool stretch = (bool)param[1];
            HashSet<int> unique = Preprocess(param[2] as IEnumerable<int>);

            HashSet<Vector64> points = new HashSet<Vector64>();
            foreach (int i in unique)
                points.UnionWith(libPolys_[i].npoly[0].Select(p => libPolys_[i].tform * new Vector64(p.X, p.Y)));

            Vector64 scale, trans;
            GeomUtility.GetRefitTransform(points, target, stretch, out scale, out trans);

            foreach (int i in unique)
            {
                CmdScale(i, scale.X, scale.Y);
                CmdTranslate(i, trans.X, trans.Y);
            }
        }

        /// <summary>
        /// Get the optimal quality for tradeoff between speed and precision of Nesting
        /// </summary>
        /// <param name="subj_handle"></param>
        /// <param name="pattern_handle"></param>
        /// <returns></returns>
        private NestQuality GetNestQuality(int subj_handle, int pattern_handle, double max_area_bounds)
        {
            Ngon S = libPolys_[subj_handle].TransformPoly()[0];
            Ngon P = libPolys_[pattern_handle].TransformPoly()[0];

            if (GeomUtility.AlmostRectangle(S) && GeomUtility.AlmostRectangle(P))
                return NestQuality.Simple;

            double s_A = GeomUtility.GetBounds(S).Area();
            double p_A = GeomUtility.GetBounds(P).Area();

            if (p_A / s_A > 1000) return NestQuality.Simple;
            if (s_A / max_area_bounds < 0.05) return NestQuality.Simple;

            double os_A = 1.0 / s_A;
            if (p_A * os_A > 100) return NestQuality.Convex;
            if (p_A * os_A > 50) return NestQuality.ConcaveLight;
            if (p_A * os_A > 10) return NestQuality.ConcaveMedium;
            if (p_A * os_A > 2) return NestQuality.ConcaveHigh;
            if (p_A * os_A > 0.25) return NestQuality.ConcaveFull;
            return NestQuality.Full;
        }

        /// <summary>
        /// Parallel kernel for generating NFP of pattern on handle, return the index in the library of this NFP
        /// Decides the optimal quality for this NFP
        /// </summary>
        /// <param name="subj_handle"></param>
        /// <param name="pattern_handle"></param>
        /// <param name="lib_set_at"></param>
        /// <returns></returns>
        private int NestKernel(int subj_handle, int pattern_handle, double max_area_bounds, int lib_set_at, NestQuality max_quality = NestQuality.Full)
        {
            NestQuality quality = GetNestQuality(subj_handle, pattern_handle, max_area_bounds);
            quality = (NestQuality)Math.Min((int)quality, (int)max_quality);
            return AddMinkowskiSum(subj_handle, pattern_handle, quality, true, lib_set_at);
        }

        /// <summary>
        /// Regular for loop in the syntax of a parallel for used for debugging
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="body"></param>
        private void For(int i, int j, Action<int> body)
        {
            for (int k = i; k < j; k++)
                body(k);
        }

        public void OddCmdNest(IEnumerable<int> handles, NestQuality max_quality = NestQuality.Full)
        {
            cmdBuffr_.Enqueue(new PolyCmd() { Call = CmdNest, param = new object[] { handles, max_quality } });
        }

        /// <summary>
        /// Nest the collection of handles with minimal enclosing square from origin
        /// </summary>
        /// <param name="handles"></param>
        private void CmdNest(params object[] param)
        {
            HashSet<int> unique = Preprocess(param[0] as IEnumerable<int>);
            NestQuality max_quality = (NestQuality)param[1];

            cmd_translate_origin_to_zero(unique);

            int n = unique.Count;

            Dictionary<int, IntRect> bounds = new Dictionary<int, IntRect>();
            foreach (int handle in unique)
                bounds.Add(handle, GeomUtility.GetBounds(libPolys_[handle].TransformPoly()[0]));

            int[] ordered_handles = unique.OrderByDescending(p => Math.Max(bounds[p].Height(), bounds[p].Width())).ToArray();
            double max_bound_area = bounds[ordered_handles[0]].Area();

            int start_cnt = libPolys_.Count;

            int[] canvas_regions = AddCanvasFitPolygon(ordered_handles);

            int base_cnt = libPolys_.Count;
            for (int i = 0; i < n * n - n; i++)
                libPolys_.Add(new PolyForm());

            int update_breaks = 10;
            int nfp_chunk_sz = n * n / update_breaks * update_breaks == n * n ? n * n / update_breaks : n * n / update_breaks + 1;

            // the row corresponds to pattern and col to nfp for this pattern on col subj
            int[,] nfps = new int[n, n];
            for (int k = 0; k < update_breaks; k++)
            {
                int start = k * nfp_chunk_sz;
                int end = Math.Min((k + 1) * nfp_chunk_sz, n * n);

                if (start >= end) break;

                // Very Complext Statement
                Parallel.For(start, end, i => nfps[i / n, i % n] = i / n == i % n ? -1 : NestKernel(ordered_handles[i % n], ordered_handles[i / n], max_bound_area, base_cnt + i - (i % n > i / n ? 1 : 0) - i / n, max_quality));

                double progress = Math.Min(((double)(k + 1)) / (update_breaks + 1) * 50.0, 50.0);
                bkWorker_.ReportProgress((int)progress);

                if (bkWorker_.CancellationPending)
                    break;
            }

            int place_chunk_sz = Math.Max(n / update_breaks, 1);

            bool[] placed = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (i % 10 == 0 && bkWorker_.CancellationPending)
                    break;

                Clipper c = new Clipper();
                c.AddPath(libPolys_[canvas_regions[i]].npoly[0], PolyType.ptSubject, true);
                for (int j = 0; j < i; j++)
                {
                    if (!placed[j])
                        continue;

                    c.AddPaths(libPolys_[nfps[i, j]].TransformPoly(), PolyType.ptClip, true);
                }
                Ngons fit_region = new Ngons();
                c.Execute(ClipType.ctDifference, fit_region, PolyFillType.pftNonZero);


                IntPoint o = libPolys_[ordered_handles[i]].TransformPolyPoint(0, 0);
                IntRect bds = bounds[ordered_handles[i]];
                long ext_x = bds.right - o.X;
                long ext_y = bds.top - o.Y;
                IntPoint place = new IntPoint(0, 0);
                long pl_score = long.MaxValue;
                for (int k = 0; k < fit_region.Count; k++)
                    for (int l = 0; l < fit_region[k].Count; l++)
                    {
                        IntPoint cand = fit_region[k][l];
                        long cd_score = Math.Max(cand.X + ext_x, cand.Y + ext_y);
                        if (cd_score < pl_score)
                        {
                            pl_score = cd_score;
                            place = cand;
                            placed[i] = true;
                        }
                    }

                if (!placed[i])
                    continue;

                CmdTranslate(ordered_handles[i], (double)(place.X - o.X), (double)(place.Y - o.Y));
                for (int k = i + 1; k < n; k++)
                    CmdTranslate(nfps[k, i], (double)(place.X - o.X), (double)(place.Y - o.Y));

                if (i % place_chunk_sz == 0)
                {
                    double progress = Math.Min(60.0 + ((double)(i / place_chunk_sz)) / (update_breaks + 1) * 40.0, 100.0);
                    bkWorker_.ReportProgress((int)progress);
                }
            }

            // remove temporary added values
            libPolys_.RemoveRange(start_cnt, libPolys_.Count - start_cnt);
        }

        public void CMD_OptimalRotation(IEnumerable<int> handles)
        {
            cmdBuffr_.Enqueue(new PolyCmd() { Call = cmd_optimal_rotation, param = new object[] { handles } });
        }

        private void cmd_optimal_rotation(int handle)
        {
            Ngon hull = libPolys_[handle].TransformPoly()[0];
            int n = hull.Count;

            double best_t = 0;
            int best = 0;
            long best_area = long.MaxValue;
            bool flip_best = false;

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

            double flip = flip_best ? Math.PI * 0.5 : 0;
            IntPoint around = hull[best];

            CmdTranslate(handle, (double)-around.X, (double)-around.Y);
            CmdRotate(handle, best_t + flip);
            CmdTranslate(handle, (double)around.X, (double)around.Y);
        }

        private void cmd_optimal_rotation(params object[] param)
        {
            HashSet<int> unique = Preprocess(param[0] as IEnumerable<int>);

            foreach (int i in unique)
                cmd_optimal_rotation(i);
        }

        /// <summary>
        /// Append a set triangulated polygons to the nester and get handles for each point to the correp. polygon island
        /// </summary>
        /// <param name="points"></param>
        /// <param name="tris"></param>
        /// <returns></returns>
        public int[] AddPolygons(IntPoint[] points, int[] tris, double miter_distance = 0.0)
        {
            // from points to clusters of tris
            int[] poly_map = new int[points.Length];
            for (int i = 0; i < poly_map.Length; i++)
                poly_map[i] = -1;

            HashSet<int>[] graph = new HashSet<int>[points.Length];
            for (int i = 0; i < graph.Length; i++)
                graph[i] = new HashSet<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                int t1 = tris[i];
                int t2 = tris[i + 1];
                int t3 = tris[i + 2];

                graph[t1].Add(t2);
                graph[t1].Add(t3);
                graph[t2].Add(t1);
                graph[t2].Add(t3);
                graph[t3].Add(t1);
                graph[t3].Add(t2);
            }

            if (graph.Any(p => p.Count == 0))
                throw new Exception("No singular vertices should exist on mesh");

            int[] clust_ids = new int[points.Length];

            HashSet<int> unmarked = new HashSet<int>(Enumerable.Range(0, points.Length));
            int clust_cnt = 0;
            while (unmarked.Count > 0)
            {
                Queue<int> open = new Queue<int>();
                int first = unmarked.First();
                unmarked.Remove(first);
                open.Enqueue(first);
                while (open.Count > 0)
                {
                    int c = open.Dequeue();
                    clust_ids[c] = clust_cnt;
                    foreach (int n in graph[c])
                    {
                        if (unmarked.Contains(n))
                        {
                            unmarked.Remove(n);
                            open.Enqueue(n);
                        }
                    }
                }

                clust_cnt++;
            }

            Ngons[] clusters = new Ngons[clust_cnt];
            for (int i = 0; i < tris.Length; i += 3)
            {
                int clust = clust_ids[tris[i]];
                if (clusters[clust] == null)
                    clusters[clust] = new Ngons();

                IntPoint p1 = points[tris[i]];
                IntPoint p2 = points[tris[i + 1]];
                IntPoint p3 = points[tris[i + 2]];

                clusters[clust].Add(new Ngon() { p1, p2, p3 });
            }

            List<Ngons> fulls = new List<Ngons>();

            for (int i = 0; i < clust_cnt; i++)
            {
                Ngons cl = clusters[i];

                Clipper c = new Clipper();
                foreach (Ngon n in cl)
                    c.AddPath(n, PolyType.ptSubject, true);

                Ngons full = new Ngons();
                c.Execute(ClipType.ctUnion, full, PolyFillType.pftNonZero);
                full = Clipper.SimplifyPolygons(full, PolyFillType.pftNonZero);

                if (miter_distance > 0.00001)
                {
                    Ngons full_miter = new Ngons();
                    ClipperOffset co = new ClipperOffset();
                    co.AddPaths(full, JoinType.jtMiter, EndType.etClosedPolygon);
                    co.Execute(ref full_miter, miter_distance);
                    full_miter = Clipper.SimplifyPolygons(full_miter, PolyFillType.pftNonZero);
                    fulls.Add(full_miter);
                }
                else
                    fulls.Add(full);
            }

            for (int i = 0; i < clust_ids.Length; i++)
                clust_ids[i] += libPolys_.Count;

            for (int i = 0; i < fulls.Count; i++)
                libPolys_.Add(new PolyForm() { npoly = fulls[i], tform = Mat3x3.Eye() });

            return clust_ids;
        }

        /// <summary>
        /// Append a set of triangulated polygons to the nester and get handles for points to polygons, coordinates are assumed
        /// to be in UV [0,1] space
        /// </summary>
        /// <param name="points"></param>
        /// <param name="tris"></param>
        /// <returns></returns>
        public int[] AddUVPolygons(Vector64[] points, int[] tris, double miter_distance = 0.0)
        {
            IntPoint[] new_pts = new IntPoint[points.Length];
            for (int i = 0; i < points.Length; i++)
                new_pts[i] = new IntPoint(points[i].X * unit_scale, points[i].Y * unit_scale);

            int start_index = libPolys_.Count;

            int[] map = AddPolygons(new_pts, tris, miter_distance * unit_scale);

            return map;
        }

        public int AddMinkowskiSum(int subj_handle, int pattern_handle, NestQuality quality, bool flip_pattern, int set_at = -1)
        {
            Ngons A = libPolys_[subj_handle].TransformPoly();
            Ngons B = libPolys_[pattern_handle].TransformPoly();

            Ngons C = GeomUtility.MinkowskiSum(B[0], A, quality, flip_pattern);
            PolyForm pref = new PolyForm() { npoly = C, tform = Mat3x3.Eye() };

            if (set_at < 0)
                libPolys_.Add(pref);
            else
                libPolys_[set_at] = pref;

            return set_at < 0 ? libPolys_.Count - 1 : set_at;
        }

        public int AddCanvasFitPolygon(IntRect canvas, int pattern_handle)
        {
            Ngon B = libPolys_[pattern_handle].TransformPoly()[0];

            Ngon C = GeomUtility.CanFitInsidePolygon(canvas, B);
            libPolys_.Add(new PolyForm() { npoly = new Ngons() { C }, tform = Mat3x3.Eye() });
            return libPolys_.Count - 1;
        }

        public int AddCanvasFitPolygon(Rect64 canvas, int pattern_handle)
        {
            IntRect c = new IntRect((long)canvas.left, (long)canvas.top, (long)canvas.right, (long)canvas.bottom);
            return AddCanvasFitPolygon(c, pattern_handle);
        }

        public int[] AddCanvasFitPolygon(IEnumerable<int> handles)
        {
            HashSet<int> unique = Preprocess(handles);

            long w = 0;
            long h = 0;

            foreach (int i in unique)
            {
                IntRect bds = GeomUtility.GetBounds(libPolys_[i].TransformPoly()[0]);
                w += bds.Width();
                h += bds.Height();
            }

            w += 1000;
            h += 1000;

            IntRect canvas = new IntRect(0, h, w, 0);

            return handles.Select(p => AddCanvasFitPolygon(canvas, p)).ToArray();
        }

        public Ngons GetTransformedPoly(int handle)
        {
            return libPolys_[handle].TransformPoly();
        }

        public void ResetTransformLib()
        {
            for (int i = 0; i < libPolys_.Count; i++)
                libPolys_[i].tform = Mat3x3.Eye();
        }

        public void ApplyTransformLibUVSpace(Vector64[] points, int[] handles)
        {
            for (int i = 0; i < points.Length; i++)
                points[i] = libPolys_[handles[i]].tform * (unit_scale * points[i]);
        }

        public void RevertTransformLibUVSpace(Vector64[] points, int[] handles)
        {
            for (int i = 0; i < points.Length; i++)
                points[i] = (1.0 / unit_scale) * (libPolys_[handles[i]].tform.Inverse() * points[i]);
        }
    }
}