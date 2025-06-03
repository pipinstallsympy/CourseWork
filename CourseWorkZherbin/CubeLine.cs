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
        int len = grid.Count();
        for (int i = 0; i < len; i++)
        {
            for (int j = 0; j < len; j++)
            {
                for (int k = 0; k < len; k++)
                {
                    Line.Add(grid.Grid[i][j][k]);
                }
            }
        }
    }

    public CubeGrid GenerateGridFromLine()
    {
        CubeGrid grid = new CubeGrid();
        int len = (int)Math.Pow(Count(), 1.0/3);
        grid.Grid = new List<List<List<Cube>>>(len);
        

        for (int i = 0; i < len; i++)
        {
            grid.Grid.Add(new List<List<Cube>>(len));
            for (int j = 0; j < len; j++)
            {
                grid.Grid[i].Add(new List<Cube>(len));
                for (int k = 0; k < len; k++)
                {
                    grid.Grid[i][j].Add(Line[i * len * len + j * len + k]);
                }
            }
        }

        return grid;
    }

    public void GeneratePoresByCount(int poreAmount)
    {
        int len = Count();
        if (poreAmount < 0 || poreAmount >= len)
        {
            throw new ArgumentException("Кол-во пор не может быть меньше 0 и больше кол-во элементов в разбиении");
        }
        
        int poreCount = PoreAmount();
        int randIndex;
        Random rand = new Random();

        
        while(poreAmount != poreCount)
        {
            randIndex = rand.Next(len);
            if (Line[randIndex].IsEmpty == false)
            {
                Line[randIndex].IsEmpty = true;
                poreCount++;
            }
        }
    }

    public void GeneratePoresByPercent(double percent)
    {
        if (percent is < 0 or >= 100)
        {
            throw new ArgumentException("Кол-во процентов принадлежит отрезку [0, 100)");
        }
        
        int randIndex;
        int len = Line.Count;
        int poreCount = PoreAmount();
        double porePercent = (double)poreCount / len;
        Random rand = new Random();


        while(porePercent < percent)
        {
            randIndex = rand.Next(len);
            if (Line[randIndex].IsEmpty == false)
            {
                Line[randIndex].IsEmpty = true;
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
            if (c.IsEmpty) count++;
        }
        return count;
    }

    public Cube this[int index]
    {
        get => Line[index];
        set => Line[index] = value;
    }
    
    public int Count() => Line.Count; 
}