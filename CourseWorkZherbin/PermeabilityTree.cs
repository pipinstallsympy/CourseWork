using System.Runtime.CompilerServices;

namespace CourseWorkZherbin;

public class PermeabilityTreeList
{
    public List<PermeabilityTree> TreeList = new List<PermeabilityTree>();

    public PermeabilityTreeList(CubeGrid grid, double fullSideLength)
    {
        Coherency coherency = new Coherency();
        List<TreeNode<Cube>> nodes = Coherency.CreateCt(grid, false);

        int n = grid.Count();
        Cube c000 = grid[0][0][0];
        Cube cnnn = grid[n - 1][n - 1][n - 1];
        double halfSide = c000.SideLength * 0.5;
        double minX = c000.X - halfSide;
        double minY = c000.Y - halfSide;
        double minZ = c000.Z - halfSide;
        double maxX = cnnn.X + halfSide;
        double maxY = cnnn.Y + halfSide;
        double maxZ = cnnn.Z + halfSide;

        foreach (TreeNode<Cube> node in nodes)
        {
            PermeabilityTree tree = new PermeabilityTree(
                node, 
                minX, minY, minZ,
                maxX, maxY, maxZ);
            if (tree.edgeNodes.Count > 1 && (tree.PartialPermeability.Count > 0 || tree.EndToEndPermeability.Count > 0))
                TreeList.Add(tree);

        }
    }
}

public class PermeabilityDictionary
{
    public Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> EndToEndPermeability = new();
    public Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> PartialPermeability = new();

    public PermeabilityDictionary(PermeabilityTreeList permeabilityTreeList)
    {
        foreach (PermeabilityTree elem in permeabilityTreeList.TreeList)
        {
            Merge(EndToEndPermeability, elem.EndToEndPermeability);
            Merge(PartialPermeability, elem.PartialPermeability);
        }
    }

    public PermeabilityDictionary(PermeabilityTree tree)
    {
        Merge(EndToEndPermeability, tree.EndToEndPermeability);
        Merge(PartialPermeability, tree.PartialPermeability);
    }

    private static void Merge(
        Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> target,
        Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> source)
    {
        foreach (var kvp in source)
        {
            if (!target.TryGetValue(kvp.Key, out var list))
            {
                target[kvp.Key] = new List<TreeNode<Cube>>(kvp.Value);
                continue;
            }
            foreach (var node in kvp.Value)
            {
                if (!list.Contains(node)) list.Add(node);
            }
        }
    }
}

public class PermeabilityTree
{
    public TreeNode<Cube> startNode;
    public List<TreeNode<Cube>> edgeNodes = new();
    public Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> EndToEndPermeability = new();
    public Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> PartialPermeability = new();

    private readonly double _minX;
    private readonly double _minY;
    private readonly double _minZ;
    private readonly double _maxX;
    private readonly double _maxY;
    private readonly double _maxZ;

    public PermeabilityTree(
        TreeNode<Cube> startNode,
        double minX, double minY, double minZ,
        double maxX, double maxY, double maxZ)
    {
        this.startNode = startNode;
        _minX = minX; _minY = minY; _minZ = minZ;
        _maxX = maxX; _maxY = maxY; _maxZ = maxZ;

        if (IsOnGridBoundary(startNode.Value)) edgeNodes.Add(startNode);
        CreateEdgeNodes(startNode);
        CreateDictionaries();
    }

    private enum SampleFace { XMin, XMax, YMin, YMax, ZMin, ZMax }

    public int BoundaryPoreCount => edgeNodes.Count;

    public int CountPoresInSubtree()
    {
        int count = 0;
        var stack = new Stack<TreeNode<Cube>>();
        stack.Push(startNode);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Value.IsEmpty) count++;
            foreach (var child in node.Children)
                stack.Push(child);
        }
        return count;
    }

    public void CollectAllPoresInSubtree(HashSet<Cube> target)
    {
        var stack = new Stack<TreeNode<Cube>>();
        stack.Push(startNode);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Value.IsEmpty) target.Add(node.Value);
            foreach (var child in node.Children)
                stack.Push(child);
        }
    }

    public void CollectBoundaryPores(HashSet<Cube> target)
    {
        foreach (var node in edgeNodes)
            target.Add(node.Value);
    }

    public int TreeDepth => ComputeTreeDepth(startNode);

    public bool HasEndToEndPercolation => EndToEndPermeability.Count > 0;

    public int EndToEndPairCount => CountUniquePairs(EndToEndPermeability);

    public int PartialPairCount => CountUniquePairs(PartialPermeability);

    public string FormatSelectorLabel(int index)
    {
        string through = HasEndToEndPercolation ? "сквозная ✓" : "без сквозной";
        return $"Дерево {index + 1}: {CountPoresInSubtree()} пор, {BoundaryPoreCount} гран., {through}";
    }

    public static int CountUniquePairs(Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> dict)
    {
        int count = 0;
        foreach (var _ in EnumerateUniquePairs(dict))
            count++;
        return count;
    }

    public static IEnumerable<(Cube A, Cube B)> EnumerateUniquePairs(
        Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> dict)
    {
        var seen = new HashSet<(long, long, long, long, long, long)>();
        foreach (var kvp in dict)
        {
            Cube a = kvp.Key.Value;
            foreach (var node in kvp.Value)
            {
                Cube b = node.Value;
                if (!IsCanonicalPair(a, b)) continue;
                var key = PairKey(a, b);
                if (seen.Add(key))
                    yield return (a, b);
            }
        }
    }

    private static bool IsCanonicalPair(Cube a, Cube b)
    {
        const double eps = 1e-9;
        if (Math.Abs(a.X - b.X) > eps) return a.X < b.X;
        if (Math.Abs(a.Y - b.Y) > eps) return a.Y < b.Y;
        if (Math.Abs(a.Z - b.Z) > eps) return a.Z < b.Z;
        return RuntimeHelpers.GetHashCode(a) < RuntimeHelpers.GetHashCode(b);
    }

    private static (long, long, long, long, long, long) PairKey(Cube a, Cube b)
    {
        static long Q(double v) => (long)Math.Round(v * 1_000_000);
        if (IsCanonicalPair(a, b))
            return (Q(a.X), Q(a.Y), Q(a.Z), Q(b.X), Q(b.Y), Q(b.Z));
        return (Q(b.X), Q(b.Y), Q(b.Z), Q(a.X), Q(a.Y), Q(a.Z));
    }

    private static int ComputeTreeDepth(TreeNode<Cube> node)
    {
        if (node.Children.Count == 0) return 1;
        int maxChildDepth = 0;
        foreach (var child in node.Children)
            maxChildDepth = Math.Max(maxChildDepth, ComputeTreeDepth(child));
        return maxChildDepth + 1;
    }

    private void CreateEdgeNodes(TreeNode<Cube> n)
    {
        var stack = new Stack<TreeNode<Cube>>();
        for (int c = n.Children.Count - 1; c >= 0; c--)
            stack.Push(n.Children[c]);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Value.IsEmpty && IsOnGridBoundary(node.Value))
                edgeNodes.Add(node);
            for (int c = node.Children.Count - 1; c >= 0; c--)
                stack.Push(node.Children[c]);
        }
    }

    private bool IsOnGridBoundary(Cube c) => GetTouchingFaces(c).Count > 0;

    private HashSet<SampleFace> GetTouchingFaces(Cube c)
    {
        double half = c.SideLength * 0.5;
        const double eps = 1e-9;
        var faces = new HashSet<SampleFace>();
        if (Math.Abs(c.X - (_minX + half)) < eps) faces.Add(SampleFace.XMin);
        if (Math.Abs(c.X - (_maxX - half)) < eps) faces.Add(SampleFace.XMax);
        if (Math.Abs(c.Y - (_minY + half)) < eps) faces.Add(SampleFace.YMin);
        if (Math.Abs(c.Y - (_maxY - half)) < eps) faces.Add(SampleFace.YMax);
        if (Math.Abs(c.Z - (_minZ + half)) < eps) faces.Add(SampleFace.ZMin);
        if (Math.Abs(c.Z - (_maxZ - half)) < eps) faces.Add(SampleFace.ZMax);
        return faces;
    }

    private static bool AreOpposite(SampleFace a, SampleFace b) =>
        (a == SampleFace.XMin && b == SampleFace.XMax) || (a == SampleFace.XMax && b == SampleFace.XMin)
        || (a == SampleFace.YMin && b == SampleFace.YMax) || (a == SampleFace.YMax && b == SampleFace.YMin)
        || (a == SampleFace.ZMin && b == SampleFace.ZMax) || (a == SampleFace.ZMax && b == SampleFace.ZMin);

    private static bool AreAdjacent(SampleFace a, SampleFace b)
    {
        if (a == b) return false;
        return a switch
        {
            SampleFace.XMin or SampleFace.XMax => b is SampleFace.YMin or SampleFace.YMax or SampleFace.ZMin or SampleFace.ZMax,
            SampleFace.YMin or SampleFace.YMax => b is SampleFace.XMin or SampleFace.XMax or SampleFace.ZMin or SampleFace.ZMax,
            SampleFace.ZMin or SampleFace.ZMax => b is SampleFace.XMin or SampleFace.XMax or SampleFace.YMin or SampleFace.YMax,
            _ => false
        };
    }

    private static bool HasOppositeFacePair(HashSet<SampleFace> f1, HashSet<SampleFace> f2)
    {
        foreach (var face1 in f1)
        foreach (var face2 in f2)
            if (AreOpposite(face1, face2)) return true;
        return false;
    }

    private static bool HasAdjacentFacePair(HashSet<SampleFace> f1, HashSet<SampleFace> f2)
    {
        foreach (var face1 in f1)
        foreach (var face2 in f2)
            if (AreAdjacent(face1, face2)) return true;
        return false;
    }

    private void CreateDictionaries()
    {
        int count = edgeNodes.Count;
        for (int i = 0; i < count; i++)
        {
            var faces1 = GetTouchingFaces(edgeNodes[i].Value);
            for (int j = i + 1; j < count; j++)
            {
                var faces2 = GetTouchingFaces(edgeNodes[j].Value);

                if (HasOppositeFacePair(faces1, faces2))
                {
                    AddPair(EndToEndPermeability, edgeNodes[i], edgeNodes[j]);
                    continue;
                }

                if (HasAdjacentFacePair(faces1, faces2))
                    AddPair(PartialPermeability, edgeNodes[i], edgeNodes[j]);
            }
        }
    }
    
    private static void AddPair(
        Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> dict,
        TreeNode<Cube> a,
        TreeNode<Cube> b)
    {
        if (!dict.TryGetValue(a, out var listA))
        {
            listA = new List<TreeNode<Cube>>();
            dict[a] = listA;
        }
        if (!listA.Contains(b)) listA.Add(b);
        if (!dict.TryGetValue(b, out var listB))
        {
            listB = new List<TreeNode<Cube>>();
            dict[b] = listB;
        }
        if (!listB.Contains(a)) listB.Add(a);
    }
}