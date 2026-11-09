using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

sealed class RoadMesh {
    struct Cell {
        internal float Left, Bottom, Right, Top, Height;
        internal ushort Region;
        internal byte Flags;
        internal int FirstEdge, EdgeCount;
        internal float X { get { return (Left + Right) / 2; } }
        internal float Y { get { return (Bottom + Top) / 2; } }
        internal Position Center() { return V2Rules.Center(X, Y, Height); }
    }

    struct Entry {
        internal int Node;
        internal double Cost;
    }

    readonly Cell[] cells;
    readonly int[] edges;
    readonly Dictionary<ushort, int[]> regions = new Dictionary<ushort, int[]>();
    readonly Dictionary<int, List<TransitStep>> transfers = new Dictionary<int, List<TransitStep>>();
    readonly Dictionary<int, int> gateCells = new Dictionary<int, int>();

    internal RoadMesh(TransitData network) {
        using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("road-mesh.bin.gz"))
        using (var input = new BinaryReader(new GZipStream(resource, CompressionMode.Decompress))) {
            if (input.ReadInt32() != 0x31304452) throw new InvalidDataException("Yol verisi biçimi geçersiz.");
            int regionCount = input.ReadInt32(), cellCount = input.ReadInt32();
            if (regionCount < 1 || regionCount > 10000 || cellCount < 1 || cellCount > 3000000)
                throw new InvalidDataException("Yol verisi boyutu geçersiz.");
            cells = new Cell[cellCount];
            var links = new List<int>();
            int index = 0;
            for (int regionIndex = 0; regionIndex < regionCount; regionIndex++) {
                ushort region = input.ReadUInt16();
                int length = input.ReadInt32();
                if (length < 0 || length > cells.Length - index) throw new InvalidDataException("Yol bölgesi geçersiz.");
                regions.Add(region, new[] { index, length });
                float originX = ((region & 255) - 135) * 192;
                float originY = ((region >> 8) - 92) * 192;
                for (int j = 0; j < length; j++, index++) {
                    cells[index] = new Cell {
                        Region = region, Left = originX + input.ReadSingle() / 10,
                        Bottom = originY + input.ReadSingle() / 10, Right = originX + input.ReadSingle() / 10,
                        Top = originY + input.ReadSingle() / 10, Height = input.ReadSingle(), Flags = input.ReadByte(),
                        FirstEdge = links.Count, EdgeCount = input.ReadUInt16()
                    };
                    for (int e = 0; e < cells[index].EdgeCount; e++) {
                        int destination = input.ReadInt32();
                        if (destination < 0 || destination >= cells.Length) throw new InvalidDataException("Yol bağlantısı geçersiz.");
                        links.Add(destination);
                    }
                }
            }
            if (index != cells.Length) throw new InvalidDataException("Yol hücreleri eksik.");
            edges = links.ToArray();
        }

        var gates = network.Gates.Where(ExpeditionRules.Infrastructure).ToDictionary(g => g.Id);
        foreach (var gate in gates.Values) gateCells[gate.Id] = Locate(gate.Point(), null, 12);
        foreach (var link in network.Links) {
            int from, to;
            if (!gateCells.TryGetValue(link.From, out from) || !gateCells.TryGetValue(link.To, out to) || from < 0 || to < 0 || from == to) continue;
            List<TransitStep> options;
            if (!transfers.TryGetValue(from, out options)) transfers.Add(from, options = new List<TransitStep>());
            options.Add(new TransitStep { From = gates[link.From], To = gates[link.To] });
        }
    }

    int Locate(Position point, HashSet<int> excluded, double limit) {
        int best = -1;
        double nearest = double.PositiveInfinity;
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
            int[] range;
            if (!regions.TryGetValue((ushort)(point.Region + dx + 256 * dy), out range)) continue;
            for (int i = range[0]; i < range[0] + range[1]; i++) {
                var cell = cells[i];
                if ((cell.Flags & 1) != 0 || (excluded != null && excluded.Contains(i))) continue;
                double x = Math.Max(cell.Left - point.X, Math.Max(0, point.X - cell.Right));
                double y = Math.Max(cell.Bottom - point.Y, Math.Max(0, point.Y - cell.Top));
                double distance = x * x + y * y;
                if (distance <= limit * limit && distance < nearest) { best = i; nearest = distance; }
            }
        }
        return best;
    }

    static void Push(List<Entry> heap, Entry entry) {
        int child = heap.Count;
        heap.Add(entry);
        while (child > 0) {
            int parent = (child - 1) / 2;
            if (heap[parent].Cost <= entry.Cost) break;
            heap[child] = heap[parent];
            child = parent;
        }
        heap[child] = entry;
    }

    static Entry Pop(List<Entry> heap) {
        Entry result = heap[0], tail = heap[heap.Count - 1];
        heap.RemoveAt(heap.Count - 1);
        if (heap.Count == 0) return result;
        int parent = 0;
        while (parent * 2 + 1 < heap.Count) {
            int child = parent * 2 + 1;
            if (child + 1 < heap.Count && heap[child + 1].Cost < heap[child].Cost) child++;
            if (heap[child].Cost >= tail.Cost) break;
            heap[parent] = heap[child];
            parent = child;
        }
        heap[parent] = tail;
        return result;
    }

    // Entrances can sit on a small walkable patch that the exported mesh does not link to the road network.
    // Find the closest pair of cells between the reachable network and that patch so the live walker only has to cross the short gap.
    bool FindBridge(double[] distance, int destination, out int from, out int to, out double gap) {
        from = to = -1; gap = double.PositiveInfinity;
        var island = new HashSet<int> { destination };
        var pending = new Queue<int>();
        pending.Enqueue(destination);
        while (pending.Count > 0 && island.Count < 20000) {
            var c = cells[pending.Dequeue()];
            for (int i = c.FirstEdge; i < c.FirstEdge + c.EdgeCount; i++) if (island.Add(edges[i])) pending.Enqueue(edges[i]);
        }
        double best = double.PositiveInfinity;
        foreach (int b in island) {
            var cb = cells[b];
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
                int[] range;
                if (!regions.TryGetValue((ushort)(cb.Region + dx + 256 * dy), out range)) continue;
                for (int a = range[0]; a < range[0] + range[1]; a++) {
                    if (double.IsPositiveInfinity(distance[a]) || (cells[a].Flags & 1) != 0) continue;
                    double ddx = cells[a].X - cb.X, ddy = cells[a].Y - cb.Y, d = ddx * ddx + ddy * ddy;
                    if (d < best) { best = d; from = a; to = b; }
                }
            }
        }
        if (from < 0) return false;
        gap = Math.Sqrt(best);
        return true;
    }

    internal List<TransitStep> Plan(Position start, Position goal, HashSet<int> excluded = null) {
        int origin = Locate(start, null, 12), destination = Locate(goal, excluded, 12);
        if (origin < 0 || destination < 0) throw new InvalidOperationException("Başlangıç veya portal yakınında yürünebilir harita hücresi bulunamadı.");
        var distance = new double[cells.Length];
        var parent = new int[cells.Length];
        var transfersUsed = new Dictionary<int, TransitStep>();
        var queue = new List<Entry>();
        int bridgeFrom = -1, bridgeTo = -1;
        double bridgeCost = 0;
        int nearestNode = origin;
        double nearestGap = start.Distance(goal);
        Action<int, int, double, TransitStep> visit = (from, to, cost, hop) => {
            if (excluded != null && excluded.Contains(to)) return;
            double candidate = distance[from] + cost;
            if (candidate >= distance[to]) return;
            distance[to] = candidate;
            parent[to] = from;
            if (hop == null) transfersUsed.Remove(to); else transfersUsed[to] = hop;
            Push(queue, new Entry { Node = to, Cost = candidate });
        };
        for (int attempt = 0; attempt < 2; attempt++) {
            for (int i = 0; i < distance.Length; i++) { distance[i] = double.PositiveInfinity; parent[i] = -1; }
            transfersUsed.Clear();
            queue.Clear();
            nearestNode = origin;
            nearestGap = start.Distance(goal);
            distance[origin] = 0;
            Push(queue, new Entry { Node = origin });
            while (queue.Count > 0) {
                var current = Pop(queue);
                int node = current.Node;
                if (current.Cost != distance[node]) continue;
                if (node == destination) break;
                var cell = cells[node];
                double gapX = cell.X - goal.X, gapY = cell.Y - goal.Y;
                double gap = Math.Sqrt(gapX * gapX + gapY * gapY);
                if (gap < nearestGap) { nearestGap = gap; nearestNode = node; }
                for (int i = cell.FirstEdge; i < cell.FirstEdge + cell.EdgeCount; i++) {
                    int next = edges[i];
                    var target = cells[next];
                    double dx = cell.X - target.X, dy = cell.Y - target.Y;
                    double cost = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
                    visit(node, next, cost, null);
                }
                if (node == bridgeFrom) visit(node, bridgeTo, bridgeCost, null);
                List<TransitStep> options;
                if (transfers.TryGetValue(node, out options))
                    foreach (var hop in options) visit(node, gateCells[hop.To.Id], 180, hop);
            }
            if (!double.IsPositiveInfinity(distance[destination]) || attempt == 1) break;
            // Permit a bounded live approach when an entrance is on a separate local mesh.
            var near = nearestNode == origin ? start : cells[nearestNode].Center();
            double limit = ExpeditionRules.Area(near) == ExpeditionRules.Area(goal) ? 192 : 64;
            if (nearestGap <= limit) break;
            double gapToIsland;
            if (!FindBridge(distance, destination, out bridgeFrom, out bridgeTo, out gapToIsland) || gapToIsland > limit) break;
            bridgeCost = Math.Max(1, gapToIsland);
        }
        bool needsLiveApproach = double.IsPositiveInfinity(distance[destination]);
        if (needsLiveApproach) {
            // The live walker validates the final short leg around portal objects.
            var approach = nearestNode == origin ? start : cells[nearestNode].Center();
            double approachLimit = ExpeditionRules.Area(approach) == ExpeditionRules.Area(goal) ? 192 : 64;
            if (nearestGap > approachLimit) throw new InvalidOperationException("Harita verisinde portala bağlı yol bulunamadı.");
            destination = nearestNode;
        }
        var path = new List<int>();
        for (int node = destination; node != origin; node = parent[node]) path.Add(node);
        path.Reverse();
        var result = new List<TransitStep>();
        Position previous = start;
        foreach (int node in path) {
            TransitStep hop;
            if (transfersUsed.TryGetValue(node, out hop)) {
                result.Add(hop);
                previous = hop.To.Point();
                continue;
            }
            Position point = cells[node].Center();
            bool bridgeStep = node == bridgeTo || node == bridgeFrom;
            if (!bridgeStep && previous.Distance(point) < 32 && !(needsLiveApproach && node == destination)) continue;
            result.Add(new TransitStep { CellId = node, To = new TransitGate { Code = "Harita yolu", X = point.X, Y = point.Y, Height = point.Height } });
            previous = point;
        }
        return result;
    }}
