namespace CourseWorkZherbin;

public class CubeLine
{
    public List<Cube> Line;

    public CubeLine()
    {
        Line = new List<Cube>();
    }
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

    public CubeGrid GenerateGridFromLine()
    {
        CubeGrid grid = new CubeGrid();
        int len = (int)Math.Pow(this.Line.Count, 1/3);

        for (int i = 0; i < len; i++)
        {
            for (int j = 0; j < len; j++)
            {
                for (int k = 0; k < len; k++)
                {
                    grid.Grid[i][j][k] = this.Line[i * len + j * len + k * len];
                }
            }
        }

        return grid;
    }

    public void GeneratePoresByCount(int poreAmount)
    {
        int poreCount = this.PoreAmount();
        int randIndex;
        int len = Line.Count;
        Random rand = new Random();

        if (poreAmount < 0 || poreAmount >= len)
        {
            throw new ArgumentException();
        }
        while(poreAmount != poreCount)
        {
            randIndex = rand.Next(len);
            if (this.Line[randIndex].IsEmpty == false)
            {
                this.Line[randIndex].IsEmpty = true;
                poreCount++;
            }
        }
    }

    public void GeneratePoresByPercent(double percent)
    {
        if (percent < 0 || percent >= 100)
        {
            throw new ArgumentException();
        }
        
        int randIndex;
        int len = Line.Count;
        int poreCount = this.PoreAmount();
        double porePercent = (double)poreCount / len;
        Random rand = new Random();


        while(porePercent < percent)
        {
            randIndex = rand.Next(len);
            if (this.Line[randIndex].IsEmpty == false)
            {
                this.Line[randIndex].IsEmpty = true;
                poreCount++;
                porePercent = (double)poreCount / len * 100;
            }
        }
    }

    public int PoreAmount()
    {
        int count = 0;
        foreach (Cube c in Line)
        {
            if (c.IsEmpty == true) count++;
        }
        return count;
    }
}