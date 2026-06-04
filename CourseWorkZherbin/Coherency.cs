namespace CourseWorkZherbin;

public class Coherency
{
    private static readonly (int dx, int dy, int dz)[] directions =
    {
        (-1, 0, 0),
        (1, 0, 0),
        (0, -1, 0),
        (0, 1, 0),
        (0, 0, -1),
        (0, 0, 1),
    };
    
    public static List<TreeNode<Cube>> CreateCt(CubeGrid g, bool isMaterial = true)
    {
        int n = g.Count();
        bool[,,] visited = new bool[n, n, n];
        List<TreeNode<Cube>> nodes = new List<TreeNode<Cube>>();

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                for (int k = 0; k < n; k++)
                {
                    Cube cube = g[i][j][k];
                    if (!(cube.IsEmpty ^ isMaterial)) continue;
                    if (visited[i, j, k]) continue;

                    visited[i, j, k] = true;
                    TreeNode<Cube> node = new TreeNode<Cube>(cube);
                    nodes.Add(node);
                    IterativeCt(g, visited, isMaterial, n, node, i, j, k);
                }
            }
        }

        return nodes;
    }

    private static void IterativeCt(
        CubeGrid grid,
        bool[,,] visited,
        bool isMaterial,
        int count,
        TreeNode<Cube> root,
        int rootX,
        int rootY,
        int rootZ)
    {
        var stack = new Stack<(TreeNode<Cube> node, int x, int y, int z)>();
        var toPush = new List<(TreeNode<Cube> node, int x, int y, int z)>();
        stack.Push((root, rootX, rootY, rootZ));

        while (stack.Count > 0)
        {
            var (node, x, y, z) = stack.Pop();
            toPush.Clear();

            for (int i = 0; i < 6; i++)
            {
                int nx = x + directions[i].dx;
                int ny = y + directions[i].dy;
                int nz = z + directions[i].dz;
                if (nx < 0 || ny < 0 || nz < 0) continue; 
                if (nx >= count || ny >= count || nz >= count) continue;

                Cube c = grid[nx][ny][nz];
                if (!(c.IsEmpty ^ isMaterial)) continue;
                if (visited[nx, ny, nz]) continue;
                visited[nx, ny, nz] = true;

                TreeNode<Cube> newChild = new TreeNode<Cube>(c);
                newChild.Parent = node;
                node.Children.Add(newChild);
                toPush.Add((newChild, nx, ny, nz));
            }

            for (int t = toPush.Count - 1; t >= 0; t--)
                stack.Push(toPush[t]);
        }
    }
}
