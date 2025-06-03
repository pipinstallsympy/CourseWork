namespace CourseWorkZherbin;

public class CubeGrid
{
    public List<List<List<Cube>>> Grid;

    public CubeGrid()
    {
        Grid = new List<List<List<Cube>>>();
    }
    public CubeGrid(int partition)
    {
        if (partition <= 0)
        {
            throw new ArgumentException("Partition must be greater than zero");
        }
        
        Grid = new List<List<List<Cube>>>(partition);
        double step = 1.0 / partition;
        double halfStep = step * 0.5;

        Point p;
        
        for (int i = 0; i < partition; i++)
        {
            Grid.Add(new List<List<Cube>>(partition));
            for (int j = 0; j < partition; j++)
            {
                Grid[i].Add(new List<Cube>(partition));
                for (int k = 0; k < partition; k++)
                {
                    p = new Point(halfStep + i * step, halfStep + j * step, halfStep + k * step);
                    Grid[i][j].Add(new Cube(p, step));
                }
            }
        }
    }
    
    public CubeGrid(Cube startCube, int partition)
    {
        if (partition <= 0)
        {
            throw new ArgumentException("Кол-во разделений должно быть больше нуля");
        }

        if (startCube == null)
        {
            throw new ArgumentException("Куб обладает null значением");
        }
        
        Grid = new List<List<List<Cube>>>();
        double step = startCube.SideLength / partition;
        double halfStep = step * 0.5;
        
        Point p;
        double startX = startCube.CentralPoint.X - startCube.SideLength * 0.5 + halfStep;
        double startY = startCube.CentralPoint.Y - startCube.SideLength * 0.5 + halfStep;
        double startZ = startCube.CentralPoint.Z - startCube.SideLength * 0.5 + halfStep;
        
        
        for (int i = 0; i < partition; i++)
        {
            Grid.Add(new List<List<Cube>>(partition));
            for (int j = 0; j < partition; j++)
            {
                Grid[i].Add(new List<Cube>(partition));
                for (int k = 0; k < partition; k++)
                {
                    p = new Point(startX + i * step, startY + j * step, startZ + k * step);
                    Grid[i][j].Add(new Cube(p, step));
                }
            }
        }
    }

    public CubeGrid(Point startPoint, Point endPoint, int partition)
    {
        if (partition <= 0)
        {
            throw new ArgumentException("Кол-во разделений должно быть больше нуля");
        }
        
        if (Math.Abs(startPoint.X - endPoint.X) < 1E-10 || Math.Abs(startPoint.Y - endPoint.Y) < 1E-10 || Math.Abs(startPoint.Z - endPoint.Z) < 1E-10)
        {
            throw new ArgumentException("По заданным точкам видео, что это не куб");
        }

        double xDist = Math.Abs(startPoint.X - endPoint.X);
        double yDist = Math.Abs(startPoint.Y - endPoint.Y);
        double zDist = Math.Abs(startPoint.Z - endPoint.Z);

        if (Math.Abs(xDist - yDist) > 1E-10 || Math.Abs(xDist - zDist) > 1E-10)
        {
            throw new ArgumentException("По заданным точкам видео, что это не куб");
        }

        Grid = new List<List<List<Cube>>>();
        
        
        Point pZero = new Point();
        if (pZero.DistanceBetweenPoint(startPoint) > pZero.DistanceBetweenPoint(endPoint))
        {
            (startPoint, endPoint) = (endPoint, startPoint);
        }
        
        double newSideLengthHalf = startPoint.DistanceBetweenPoint(endPoint) / Math.Sqrt(3);
        double step = newSideLengthHalf / partition;
        double halfStep = step * 0.5;
        
        Point p;
        double startX = startPoint.X + halfStep;
        double startY = startPoint.Y + halfStep;
        double startZ = startPoint.Z + halfStep;

        for (int i = 0; i < partition; i++)
        {
            Grid.Add(new List<List<Cube>>(partition));
            for (int j = 0; j < partition; j++)
            {
                Grid[i].Add(new List<Cube>(partition));
                for (int k = 0; k < partition; k++)
                {
                    p = new Point(startX + i * step, startY + j * step, startZ + k * step);
                    Grid[i][j].Add(new Cube(p, step));
                }
            }
        }
    }
    

    public CubeLine GenerateLineFromGrid()
    {
        CubeLine line = new CubeLine();
        int len = Grid.Count;
        
        for (int i = 0; i < len; i++)
        {
            for (int j = 0; j < len; j++)
            {
                for (int k = 0; k < len; k++)
                {
                    line.Line.Add(Grid[i][j][k]);
                }
            }
        }
        
        return line;
    }

    public List<List<Cube>> this[int index]
    {
        get => Grid[index];
        set => Grid[index] = value;
    }

    public int Count() => Grid.Count;
}