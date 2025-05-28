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

    public void GeneratePoresByCount(int poreAmount)
    {
        int poreCount = 0;
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
        
        int poreCount = 0;
        double porePercent = 0;
        int randIndex;
        int len = Line.Count;
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
}