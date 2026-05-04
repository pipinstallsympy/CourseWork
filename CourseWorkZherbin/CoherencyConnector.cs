namespace CourseWorkZherbin;

public sealed record ConnectionPath(
    TreeNode<Cube> From,
    TreeNode<Cube> To,
    List<Cube> EmptyCubesToFill,
    int Distance
    );


public class CoherencyConnector
{
    public ConnectionPath? ShortestConnection(CubeGrid grid, TreeNode<Cube> componentA, TreeNode<Cube> componentB)
    {
        var coordinates = BuildCoordIndex(grid);
        var aSet = CollectCubes(componentA);
        var bSet = CollectCubes(componentB);
        if (aSet.Count == 0 || bSet.Count == 0) return null;

        int n = grid.Count();
        var distance = new Dictionary<Cube, int>();
        var previous = new Dictionary<Cube, Cube?>();
        var dequeued = new LinkedList<Cube>();

        foreach (var c in aSet)
        {
            distance[c] = 0;
            previous[c] = null;
            dequeued.AddFirst(c);
        }

        int[] dx = [1, -1, 0, 0, 0, 0];
        int[] dy = [0, 0, 1, -1, 0, 0];
        int[] dz = [0, 0, 0, 0, 1, -1];

        Cube? hit = null;
        while (dequeued.Count > 0)
        {
            Cube c = dequeued.First!.Value;
            dequeued.RemoveFirst();

            if (bSet.Contains(c))
            {
                hit = c;
                break;
            }

            var (x, y, z) = coordinates[c];
            int dc = distance[c];

            for (int i = 0; i < 6; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                int nz = z + dz[i];
                if (nx < 0 || ny < 0 || nz < 0) continue;
                if (nx >= n || ny >= n || nz >= n) continue;
                
                Cube v = grid[nx][ny][nz];
                int nd = dc + (v.IsEmpty ? 1 : 0);
                
                if(distance.TryGetValue(v,  out var cur) &&  nd  >= cur) continue;
                distance[v] = nd;
                previous[v] = c;
                if (!v.IsEmpty)
                {
                    dequeued.AddFirst(v);
                }
                else
                {
                    dequeued.AddLast(v);
                }
            }
        }
        
        if(hit == null) return null;

        List<Cube> fill = new List<Cube>();
        Cube? p = hit;
        while (p != null)
        {
            if(p.IsEmpty) fill.Add(p);
            previous.TryGetValue(p, out p);
        }
        fill.Reverse();
        
        return new ConnectionPath(componentA, componentB, fill, distance[hit]);
    }


    public List<ConnectionPath> ConnectAll(CubeGrid grid, List<TreeNode<Cube>> components)
    {
        int k = components.Count;
        if (k < 2) return new List<ConnectionPath>();
        
        List<ConnectionPath> edges = new List<ConnectionPath>();

        for (int i = 0; i < k; i++)
        {
            for (int j = i + 1; j < k; j++)
            {
                ConnectionPath? p  = ShortestConnection(grid, components[i], components[j]);
                if (p !=  null) edges.Add(p);
            }
        }
        
        edges.Sort((a, b) =>  a.Distance.CompareTo(b.Distance));

        var parent = Enumerable.Range(0, k).ToArray();
        int Find(int x) => parent[x] == x ? x : (parent[x] = Find(parent[x]));
        
        
        var indexOfComponent = new Dictionary<TreeNode<Cube>, int>();
        for (int i = 0; i < k; i++) indexOfComponent[components[i]] = i;

        var mst = new List<ConnectionPath>();
        foreach (var e in edges)
        {
            int a = indexOfComponent[e.From];
            int b = indexOfComponent[e.To];
            int ra = Find(a);
            int rb = Find(b);

            if (ra != rb)
            {
                parent[ra] = rb;
                mst.Add(e);
                if (mst.Count == k - 1) break;
            }
        }

        return mst;
    }

    private static HashSet<Cube> CollectCubes(TreeNode<Cube> root)
    {
        HashSet<Cube> set = new HashSet<Cube>();
        Stack<TreeNode<Cube>> stack = new Stack<TreeNode<Cube>>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            set.Add(node.Value);
            foreach(var ch in node.Children)  stack.Push(ch);
        }

        return set;
    }

    private static Dictionary<Cube, (int x, int y, int z)> BuildCoordIndex(CubeGrid grid)
    {
        int n  =  grid.Count();
        var dict = new Dictionary<Cube, (int, int, int)>(n * n * n);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                for (int k = 0; k < n; k++)
                {
                    dict[grid[i][j][k]] = (i, j, k);
                }
            }
        }
        return dict;
    }
}    
    
    