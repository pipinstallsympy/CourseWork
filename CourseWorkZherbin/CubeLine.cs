using System.Buffers;

namespace CourseWorkZherbin;

public class CubeLine : IDisposable
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

    public CubeLine(CubeLine other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Line = new List<Cube>(other.Line.Count);
        foreach (Cube c in other.Line)
        {
            Point p = c.CentralPoint;
            Cube copy = new Cube(new Point(p.X, p.Y, p.Z), c.SideLength)
            {
                IsEmpty = c.IsEmpty
            };
            Line.Add(copy);
        }
    }
    public CubeGrid GenerateGridFromLine()
    {
        CubeGrid grid = new CubeGrid();
        int len = (int)Math.Round(Math.Cbrt(Count()));
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

        GeneratePoresHybrid(poreAmount);
    }


    public void GeneratePoresByPercent(double percent)
    {
        if (percent is < 0 or >= 100)
        {
            throw new ArgumentException("Кол-во процентов принадлежит отрезку [0, 100)");
        }

        int len = Line.Count;
        int k = (int)Math.Ceiling(percent / 100.0 * len);
        GeneratePoresHybrid(k);
    }

    void GeneratePoresHybrid(int k)
    {
        int len = Line.Count;
        if (k <= 0) return;


        int[] indices = ArrayPool<int>.Shared.Rent(len);
        try
        {
            InitIndices(indices, len);

            if (k <= len / 2)
            {
                PartialFisherShuffle(indices, len, k);
                for (int i = 0; i < k; i++)
                    Line[indices[i]].IsEmpty = true;
            }
            else
            {
                int m = len - k;
                PartialFisherShuffle(indices, len, m);
                for (int i = 0; i < m; i++)
                    Line[indices[i]].IsEmpty = false;
            }
        }
        finally
        {
            Console.WriteLine($"Generated Pores: {k}");
            ArrayPool<int>.Shared.Return(indices);
        }
    }

    static void InitIndices(int[] indices, int len)
    {
        for (int i = 0; i < len; i++) indices[i] = i;
    }

    static void PartialFisherShuffle(int[] indices, int len, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            int j = Random.Shared.Next(i, len);
            (indices[i], indices[j]) = (indices[j], indices[i]);
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


    public List<Cube> GetMaterial()
    {
        List<Cube> material = new List<Cube>();

        foreach (Cube c in Line)
        {
            if (!c.IsEmpty) material.Add(c);
        }
        
        return material;
    }

    public Cube this[int index]
    {
        get => Line[index];
        set => Line[index] = value;
    }
    
    public int Count() => Line.Count;

    public void Dispose()
    {
        Line.Clear();
        GC.SuppressFinalize(this);
    }
}