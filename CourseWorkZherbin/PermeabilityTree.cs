namespace CourseWorkZherbin;

public class PermeabilityTreeList
{
    public List<PermeabilityTree> TreeList = new List<PermeabilityTree>();

    public PermeabilityTreeList(CubeGrid grid, double fullSideLength)
    {
        Coherency coherency = new Coherency();
        List<TreeNode<Cube>> nodes = coherency.CreateCT(grid, false);

        foreach (TreeNode<Cube> node in nodes)
        {
            PermeabilityTree tree = new PermeabilityTree(node, fullSideLength);
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

    public PermeabilityTree(TreeNode<Cube> startNode, double fullSideLength)
    {
        this.startNode = startNode;
        edgeNodes.Add(startNode);
        CreateEdgeNodes(startNode);
        CreateDictionaries(fullSideLength);
    }

    private void CreateEdgeNodes(TreeNode<Cube> n)
    { 
        foreach (TreeNode<Cube> node in n.Children)
        {
            if(node.Value.IsEmpty) edgeNodes.Add(node);
             CreateEdgeNodes(node);
        }
    }

    private void CreateDictionaries(double fullSideLength)
    {
        
        int count = edgeNodes.Count;
        for (int i = 0; i < count; i++)
        {
            Cube c1 =  edgeNodes[i].Value;
            for (int j = i + 1; j < count; j++)
            {
                Cube c2 =  edgeNodes[j].Value;
                int dx = (Math.Abs(c1.X - c2.X) < 1e-9) ? 1 : 0;
                int dy = (Math.Abs(c1.Y - c2.Y) < 1e-9) ? 1 : 0;
                int dz = (Math.Abs(c1.Z - c2.Z) < 1e-9) ? 1 : 0;
                
                if ((dx + dy + dz) == 2) continue;

                double checkLength = fullSideLength - c1.SideLength;
                bool endToEnd =
                    Math.Abs(Math.Abs(c1.X - c2.X) - checkLength) < 1e-9 ||
                    Math.Abs(Math.Abs(c1.Y - c2.Y) - checkLength) < 1e-9 ||
                    Math.Abs(Math.Abs(c1.Z - c2.Z) - checkLength) < 1e-9;
                
                if (endToEnd)
                    AddPair(EndToEndPermeability, edgeNodes[i], edgeNodes[j]);
                else
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