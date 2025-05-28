namespace CourseWorkZherbin;

public class CubeGrid
{
    public List<List<List<Cube>>> Grid;

    public CubeGrid(int partition)
    {
        Grid = new List<List<List<Cube>>>();
        double step = 1.0 / partition;
        double halfStep = step * 0.5;
        
        for (int i = 0; i < partition; i++)
        {
            for (int j = 0; j < partition; j++)
            {
                for (int k = 0; k < partition; k++)
                {
                    Grid[i][j][k] = new Cube(new Point(halfStep + i * step,halfStep + j * step,halfStep + k * step), step);
                }
            }
        }
    }

    public CubeGrid(Cube startCube, int partition)
    {
        Grid = new List<List<List<Cube>>>();
        
        double step = startCube.SideLength / partition;
        int end = partition / 2;
        int start = end * -1;
        
        
        double halfStep;
        
        if (partition % 2 == 0)
        {
            halfStep = step * 0.5;
        }
        else
        {
            halfStep = 0;
        }
        

        Point p;
        double startX = startCube.CentralPoint.X;
        double startY = startCube.CentralPoint.Y;
        double startZ = startCube.CentralPoint.Z;
        
        
        for (int i = 0; i < partition / 2; i++)
        {
            for (int j = 0; j < partition; j++)
            {
                for (int k = 0; k < partition; k++)
                {
                    p = new Point(startX + i * step + halfStep * Sgn(i), startY + j * step + halfStep * Sgn(j), startZ + k * step + halfStep * Sgn(k));
                    Grid[i][j][k] = new Cube(p, step);
                }
            }
        }
    }

    public CubeGrid(Point startPoint, Point endPoint, int partition)
    {
        if (startPoint.X == endPoint.X || startPoint.Y == endPoint.Y || startPoint.Z == endPoint.Z)
        {
            throw new ArgumentException();
        }

        double xDist = Math.Abs(startPoint.X - endPoint.X);
        double yDist = Math.Abs(startPoint.X - endPoint.X);
        double zDist = Math.Abs(startPoint.X - endPoint.X);

        if (xDist != yDist || xDist != zDist)
        {
            throw new ArgumentException();
        }
        
        Grid = new List<List<List<Cube>>>();
        
        
        Point pZero = new Point();
        if (pZero.DistanceBetweenPoint(startPoint) > pZero.DistanceBetweenPoint(endPoint))
        {
            (startPoint, endPoint) = (endPoint, startPoint);
        }
        
        double step = 1.0 / partition;
        double halfStep = step * 0.5;
        double startX = startPoint.X;
        double startY = startPoint.Y;
        double startZ = startPoint.Z;
        Point p;

        for (int i = 0; i < partition; i++)
        {
            for (int j = 0; j < partition; j++)
            {
                for (int k = 0; k < partition; k++)
                {
                    p = new Point(startX + halfStep + i * step, startY + halfStep + j * step,
                        startZ + halfStep + k * step);
                    Grid[i][j][k] = new Cube(p, step);
                }
            }
        }
    }


    public CubeGrid(CubeLine line)
    {
        Grid = new List<List<List<Cube>>>();

        int len = (int)Math.Pow(line.Line.Count, 1/3);

        for (int i = 0; i < len; i++)
        {
            for (int j = 0; j < len; j++)
            {
                for (int k = 0; k < len; k++)
                {
                    Grid[i][j][k] = line.Line[i * len + j * len + k * len];
                }
            }
        }
    }

    int Sgn(int value)
    {
        return (value > 0)? 1 : (value == 0)? -1 : 0;
    }
}