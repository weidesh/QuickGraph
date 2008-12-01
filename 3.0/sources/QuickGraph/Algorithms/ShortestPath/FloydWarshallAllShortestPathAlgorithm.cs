﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuickGraph.Algorithms.Services;
using System.Diagnostics.Contracts;
using QuickGraph.Collections;
using System.Diagnostics;
using System.IO;

namespace QuickGraph.Algorithms.ShortestPath
{
    /// <summary>
    /// Floyd-Warshall all shortest path algorith,
    /// </summary>
    /// <typeparam name="TVertex"></typeparam>
    /// <typeparam name="TEdge"></typeparam>
    public class FloydWarshallAllShortestPathAlgorithm<TVertex, TEdge> 
        : AlgorithmBase<IVertexAndEdgeListGraph<TVertex, TEdge>>
        where TEdge : IEdge<TVertex>
    {
        private readonly Func<TEdge, double> weights;
        private readonly IDistanceRelaxer distanceRelaxer;
        private readonly Dictionary<VertexPair<TVertex>, VertexData> data;

        struct VertexData
        {
            public readonly double Cost;
            readonly TVertex _predecessor;
            readonly TEdge _edge;
            readonly bool edgeStored;

            public bool TryGetPredecessor(out TVertex predecessor)
            {
                predecessor = this._predecessor;
                return !this.edgeStored;
            }

            public bool TryGetEdge(out TEdge edge)
            {
                edge = this._edge;
                return this.edgeStored;
            }

            public VertexData(double cost, TEdge edge)
            {
                Contract.Requires(edge != null);

                this.Cost = cost;
                this._predecessor = default(TVertex);
                this._edge = edge;
                this.edgeStored = true;
            }

            public VertexData(double cost, TVertex predecessor)
            {
                Contract.Requires(predecessor != null);

                this.Cost = cost;
                this._predecessor = predecessor;
                this._edge = default(TEdge);
                this.edgeStored = false;
            }

            public override string ToString()
            {
                if (this.edgeStored)
                    return String.Format("e:{0}-{1}", this.Cost, this._edge);
                else
                    return String.Format("p:{0}-{1}", this.Cost, this._predecessor);
            }
        }

        public FloydWarshallAllShortestPathAlgorithm(
            IAlgorithmComponent host,
            IVertexAndEdgeListGraph<TVertex, TEdge> visitedGraph,
            Func<TEdge, double> weights,
            IDistanceRelaxer distanceRelaxer
            )
            : base(host, visitedGraph)
        {
            Contract.Requires(weights != null);
            Contract.Requires(distanceRelaxer != null);

            this.weights = weights;
            this.distanceRelaxer = distanceRelaxer;
            this.data = new Dictionary<VertexPair<TVertex>, VertexData>();
        }

        public FloydWarshallAllShortestPathAlgorithm(
            IVertexAndEdgeListGraph<TVertex, TEdge> visitedGraph,
            Func<TEdge, double> weights,
            IDistanceRelaxer distanceRelaxer)
            : base(visitedGraph)
        {
            Contract.Requires(weights != null);
            Contract.Requires(distanceRelaxer != null);

            this.weights =weights;
            this.distanceRelaxer = distanceRelaxer;
            this.data = new Dictionary<VertexPair<TVertex>, VertexData>();
        }

        public FloydWarshallAllShortestPathAlgorithm(
            IVertexAndEdgeListGraph<TVertex, TEdge> visitedGraph,
            Func<TEdge, double> weights)
            : this(visitedGraph, weights, ShortestDistanceRelaxer.Instance)
        {
        }

        public bool TryGetPath(
            TVertex source,
            TVertex target,
            out IEnumerable<TEdge> path)
        {
            Contract.Requires(source != null);
            Contract.Requires(target != null);

            if (source.Equals(target))
            {
                path = new TEdge[0];
                return true;
            }

#if DEBUG
            var set = new HashSet<TVertex>();
            set.Add(source); set.Add(target);
#endif

            var edges = new EdgeList<TVertex, TEdge>();
            var todo = new Stack<VertexPair<TVertex>>();
            todo.Push(new VertexPair<TVertex>(source, target));
            while (todo.Count > 0)
            {
                var current = todo.Pop();
                Contract.Assert(!current.Source.Equals(current.Target));
                VertexData data;
                if (this.data.TryGetValue(current, out data))
                {
                    TEdge edge;
                    if (data.TryGetEdge(out edge))
                        edges.Add(edge);
                    else
                    {
                        TVertex intermediate;
                        if (data.TryGetPredecessor(out intermediate))
                        {
#if DEBUG
                            if (!set.Add(intermediate))
                                throw new Exception(intermediate.ToString() + " already in path");
#endif
                            todo.Push(new VertexPair<TVertex>(intermediate, current.Target));
                            todo.Push(new VertexPair<TVertex>(current.Source, intermediate));
                        }
                        else
                        {
                            Contract.Assert(false);
                            path = null;
                            return false;
                        }
                    }
                }
                else
                {
                    // no path found
                    path = null;
                    return false;
                }
            }

            Contract.Assert(todo.Count == 0);
            Contract.Assert(edges.Count > 0);
            path = edges.ToArray();
            return true;
        }

        protected override void InternalCompute()
        {
            var cancelManager = this.Services.CancelManager;
            // matrix i,j -> path
            this.data.Clear();

            var vertices = this.VisitedGraph.Vertices;
            var edges = this.VisitedGraph.Edges;

            // prepare the matrix with initial costs
            // walk each edge and add entry in cost dictionary
            foreach (var edge in edges)
            {
                var ij = VertexPair<TVertex>.FromEdge<TEdge>(edge);
                var cost = this.weights(edge);
                VertexData value;
                if (!data.TryGetValue(ij, out value))
                    data[ij] = new VertexData(cost, edge);
                else if (cost < value.Cost)
                    data[ij] = new VertexData(cost, edge);
            }
            if (cancelManager.IsCancelling) return;

            // walk each vertices and make sure cost self-cost 0
            foreach (var v in vertices)
                data[new VertexPair<TVertex>(v, v)] = new VertexData(0, default(TEdge));

            if (cancelManager.IsCancelling) return;

            // iterate k, i, j
            foreach (var vk in vertices)
            {
                if (cancelManager.IsCancelling) return;
                foreach (var vi in vertices)
                {
                    var ik = new VertexPair<TVertex>(vi, vk);
                    VertexData pathik;
                    if(data.TryGetValue(ik, out pathik))
                        foreach (var vj in vertices)
                        {
                            var kj = new VertexPair<TVertex>(vk, vj);

                            VertexData pathkj;
                            if (data.TryGetValue(kj, out pathkj))
                            {
                                double combined = this.distanceRelaxer.Combine(pathik.Cost, pathkj.Cost);
                                var ij = new VertexPair<TVertex>(vi, vj);
                                VertexData pathij;
                                if (data.TryGetValue(ij, out pathij))
                                {
                                    if (this.distanceRelaxer.Compare(combined, pathij.Cost))
                                        data[ij] = new VertexData(combined, vk);
                                }
                                else
                                    data[ij] = new VertexData(combined, vk);
                            }
                        }
                }
            }

            // check negative cycles
            foreach (var vi in vertices)
            {
                var ii = new VertexPair<TVertex>(vi, vi);
                VertexData value;
                if (data.TryGetValue(ii, out value) &&
                    value.Cost < 0)
                    throw new NegativeCycleGraphException();
            }
        }

        [Conditional("DEBUG")]
        public void Dump(TextWriter writer)
        {
            writer.WriteLine("data:");
            foreach (var kv in this.data)
                writer.WriteLine("{0}->{1}: {2}", 
                    kv.Key.Source, 
                    kv.Key.Target, 
                    kv.Value.ToString());
        }
    }
}
