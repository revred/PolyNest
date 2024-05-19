using ClipperLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PolyNester;


using Ngon = List<IntPoint>;
using Ngons = List<List<IntPoint>>;

public struct ClipperBuilder
{
    IntPoint[] pts_;
    int[] tris_;
    double miter_distance_;
    int[] clust_ids_;
    Ngons[] all_;

    public int[] Cids => clust_ids_;
    public Ngons[] All => all_;

    public ClipperBuilder(IntPoint[] pts, int[] tris, double miter_distance = 0.0)
    {
        pts_ = pts;
        tris_ = tris;
        miter_distance_ = miter_distance;
        clust_ids_ = new int[pts.Length];
        all_ = null;
    }

    public Task BuildClusters()
    {
        var clusters = ComputeClusters();
        TransformClusters(clusters);            
        return Task.CompletedTask;
    }


    HashSet<int>[] SetupHashes()
    {
        HashSet<int>[] graph = new HashSet<int>[pts_.Length];
        for (int i = 0; i < graph.Length; i++)
            graph[i] = new HashSet<int>();

        for (int i = 0; i < tris_.Length; i += 3)
        {
            int t1 = tris_[i];
            int t2 = tris_[i + 1];
            int t3 = tris_[i + 2];

            graph[t1].Add(t2);
            graph[t1].Add(t3);
            graph[t2].Add(t1);
            graph[t2].Add(t3);
            graph[t3].Add(t1);
            graph[t3].Add(t2);
        }

        if (graph.Any(p => p.Count == 0))
            throw new Exception("No singular vertices should exist on mesh");

        return graph;
    }

    Ngons[] ComputeClusters()
    {
        var graph = SetupHashes();
        HashSet<int> unmarked = new HashSet<int>(Enumerable.Range(0, pts_.Length));
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
                clust_ids_[c] = clust_cnt;
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
        for (int i = 0; i < tris_.Length; i += 3)
        {
            int clust = clust_ids_[tris_[i]];

            IntPoint p1 = pts_[tris_[i]];
            IntPoint p2 = pts_[tris_[i + 1]];
            IntPoint p3 = pts_[tris_[i + 2]];

            var addit = new Ngon() { p1, p2, p3 };
            if (clusters[clust] == null) clusters[clust] = new Ngons() { addit };
            else clusters[clust].Add(addit);
        }
        return clusters;
    }

    void TransformClusters(Ngons[] clusters)
    {
        all_ = new Ngons[clusters.Count()];
        Task[] tasks = new Task[clusters.Count()];
        for (int i = 0; i < clusters.Count(); i++)
        {
            tasks[i] = FillAll(i, clusters[i], all_);
        }
        Task.WhenAll(tasks).GetAwaiter().GetResult();        
    }

    async Task FillAll(int index, Ngons inCluster, Ngons[] all)
    {
        all[index] = await Transform(inCluster);
    }

    Task<Ngons> Transform(Ngons cluster)
    {
        Clipper clipLoop = new Clipper();

        Ngons cl = cluster;
        Ngons full = new Ngons();
        foreach (Ngon n in cl) clipLoop.AddPath(n, PolyType.ptSubject, true);
        clipLoop.Execute(ClipType.ctUnion, full, PolyFillType.pftNonZero);
        Ngons fill = Clipper.SimplifyPolygons(full, PolyFillType.pftNonZero);

        if (miter_distance_ < 0.00001) return Task.FromResult(fill);

        Ngons fill_miter = new Ngons();
        ClipperOffset co = new ClipperOffset();
        co.AddPaths(fill, JoinType.jtMiter, EndType.etClosedPolygon);
        co.Execute(ref fill_miter, miter_distance_);
        return Task.FromResult(Clipper.SimplifyPolygons(fill_miter, PolyFillType.pftNonZero));
    }
}
