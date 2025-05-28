namespace CourseWorkZherbin;

public class CubeLine
{
    public List<Cube> Line;

    public CubeLine(CubeGrid grid)
    {
        Line = new List<Cube>();
        int len = grid.Grid.Count;
        for (int i = 0; i < len; i++)
        {
            for (int j = 0; j < len; j++)
            {
                for (int k = 0; k < len; k++)
                {
                    Line[i * len + j * len + k * len] = grid.Grid[i][j][k];
                }
            }
        }
    }
}