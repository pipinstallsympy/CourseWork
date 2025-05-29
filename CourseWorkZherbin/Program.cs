namespace CourseWorkZherbin;

class Program
{
    static void Main(string[] args)
    {
        CubeGrid grid = new CubeGrid(2);
        CubeLine g2 = grid.GenerateLineFromGrid();
        g2.GeneratePoresByCount(5);
        grid = g2.GenerateGridFromLine();
        foreach (var elem1 in grid.Grid)
        {
            foreach (var elem2 in elem1)
            {
                foreach (var elem3 in elem2)
                {
                    Console.WriteLine(elem3.IsEmpty);
                }
            }
        }
    }
}