namespace CourseWorkZherbin;

public class Coherency
{
    static List<Cube> material = new List<Cube>(); 
    static List<Cube> usedMaterial = new List<Cube>();
    static CubeGrid grid;
    private static int count;
    public List<TreeNode<Cube>> CreateCT(CubeGrid g)
    {
        material = g.GetMaterial();
        usedMaterial.Clear();
        grid = g;
        count = grid.Count();
        
        List<TreeNode<Cube>> nodes = new List<TreeNode<Cube>>();
        while (material.Count > 0)
        {
            usedMaterial.Add(material[0]);
            TreeNode<Cube> node = new TreeNode<Cube>(material[0]);
            material.RemoveAt(0);
            nodes.Add(node);
            RecursionCT(node);
        }

        return nodes;
    }

    private void RecursionCT(TreeNode<Cube> node)
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
                    if(c.IsEmpty) continue;
                    if(usedMaterial.Contains(c)) continue;
                    material.Remove(c);
                    usedMaterial.Add(c);

                    TreeNode<Cube> newChild = new TreeNode<Cube>(c);
                    newChild.Parent = node;
                    node.Children.Add(newChild);
                    RecursionCT(newChild);
                }
            }
        }
    }
}