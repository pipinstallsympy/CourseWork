using System.Threading;

namespace CourseWorkZherbin;

internal sealed class ActionProgress : IProgress<int>
{
    private readonly Action<int> _handler;

    public ActionProgress(Action<int> handler) => _handler = handler;

    public void Report(int value) => _handler(value);
}

public sealed class ClusterProgressScope
{
    private readonly IProgress<(int current, int total)>? _progress;
    private int _k;
    private int _total;

    public ClusterProgressScope(IProgress<(int current, int total)>? progress) => _progress = progress;

    public IProgress<int> Phase1ClusterDiscovered => new ActionProgress(ReportPhase1Cluster);

    public IProgress<int> Phase2ClusterProcessed => new ActionProgress(ReportPhase2Cluster);

    public IProgress<int> Phase3ClusterDiscovered => new ActionProgress(ReportPhase3Cluster);

    private void ReportPhase1Cluster(int count)
    {
        _k = count;
        _total = 2 * _k + 1;
        Report(count);
    }

    private void ReportPhase2Cluster(int processed) => Report(_k + processed);

    private void ReportPhase3Cluster(int count)
    {
        _total = 2 * _k + count;
        Report(2 * _k + count);
    }

    private void Report(int current) => _progress?.Report((current, Math.Max(current, _total)));
}

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
    
    public static List<TreeNode<Cube>> CreateCt(
        CubeGrid g,
        bool isMaterial = true,
        CancellationToken cancellationToken = default,
        IProgress<int>? clusterDiscovered = null,
        IProgress<int>? poreVisited = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int n = g.Count();
        bool[,,] visited = new bool[n, n, n];
        List<TreeNode<Cube>> nodes = new List<TreeNode<Cube>>();
        int poresVisited = 0;

        for (int i = 0; i < n; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int j = 0; j < n; j++)
            {
                for (int k = 0; k < n; k++)
                {
                    Cube cube = g[i][j][k];
                    if (!(cube.IsEmpty ^ isMaterial)) continue;
                    if (visited[i, j, k]) continue;

                    visited[i, j, k] = true;
                    if (!isMaterial)
                        poreVisited?.Report(++poresVisited);

                    TreeNode<Cube> node = new TreeNode<Cube>(cube);
                    nodes.Add(node);
                    if (isMaterial)
                        clusterDiscovered?.Report(nodes.Count);

                    IterativeCt(
                        g,
                        visited,
                        isMaterial,
                        n,
                        node,
                        i,
                        j,
                        k,
                        cancellationToken,
                        poreVisited,
                        ref poresVisited);
                }
            }
        }

        Console.WriteLine($"Created {(isMaterial ? "material" : "pore")} coherency forest. count: {nodes.Count}");
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
        int rootZ,
        CancellationToken cancellationToken,
        IProgress<int>? poreVisited,
        ref int poresVisited)
    {
        var stack = new Stack<(TreeNode<Cube> node, int x, int y, int z)>();
        var toPush = new List<(TreeNode<Cube> node, int x, int y, int z)>();
        stack.Push((root, rootX, rootY, rootZ));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

                if (!isMaterial)
                    poreVisited?.Report(++poresVisited);

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
