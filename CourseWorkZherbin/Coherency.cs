namespace CourseWorkZherbin;

public class Coherency
{
    static List<Cube> freeNodes = new List<Cube>(); 
    static List<Cube> usedNodes = new List<Cube>();
    static CubeGrid grid;
    private static int count;
    public List<TreeNode<Cube>> CreateCT(CubeGrid g, bool isMaterial = true)
    {
        freeNodes = isMaterial ? g.GetMaterial() : g.GetPores();
        usedNodes.Clear();
        grid = g;
        count = grid.Count();
        
        List<TreeNode<Cube>> nodes = new List<TreeNode<Cube>>();
        while (freeNodes.Count > 0)
        {
            usedNodes.Add(freeNodes[0]);
            TreeNode<Cube> node = new TreeNode<Cube>(freeNodes[0]);
            freeNodes.RemoveAt(0);
            nodes.Add(node);
            RecursionCT(node, isMaterial);
        }

        return nodes;
    }

    private void RecursionCT(TreeNode<Cube> node, bool isMaterial)
    {
        List<int>? currIndex = grid.IndexOf(node.Value);
        if (currIndex == null) throw new NullReferenceException("Recursion CT");
        int x = currIndex[0];
        int y = currIndex[1];
        int z = currIndex[2];
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                for (int k = -1; k <= 1; k++)
                {
                    if (x + i < 0 || y + j < 0 || z + k < 0) continue;
                    if (x + i >= count || y + j >= count || z + k >= count) continue;
                    if (Math.Abs(i) + Math.Abs(j) + Math.Abs(k) != 1) continue;

                    Cube c = grid[x + i][y + j][z + k];
                    if(!(c.IsEmpty ^ isMaterial)) continue;
                    if(usedNodes.Contains(c)) continue;
                    freeNodes.Remove(c);
                    usedNodes.Add(c);

                    TreeNode<Cube> newChild = new TreeNode<Cube>(c);
                    newChild.Parent = node;
                    node.Children.Add(newChild);
                    RecursionCT(newChild, isMaterial);
                }
            }
        }
    }
}