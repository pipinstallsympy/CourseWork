using System.Collections.Generic;
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
        
        for (int i = 0; i < partition; i++)
        {
            Grid.Add(new List<List<Cube>>(partition));
            for (int j = 0; j < partition; j++)
            {
                Grid[i].Add(new List<Cube>(partition));
                for (int k = 0; k < partition; k++)
                {
                    Grid[i][j].Add(new Cube(new Point(halfStep + i * step,halfStep + j * step,halfStep + k * step), step));
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
        
        double step = startCube.SideLength / partition;
        double halfStep;

        if (partition % 2 == 0)
        {
            halfStep = step * 0.5;
        }
        else
        {
            halfStep = 0;
        }
        
        Grid = new List<List<List<Cube>>>();
        int end = partition / 2;
        int start = end * -1;
        
        Point p;
        double startX = startCube.CentralPoint.X;
        double startY = startCube.CentralPoint.Y;
        double startZ = startCube.CentralPoint.Z;
        
        
        for (int i = 0; i < partition; i++)
        {
            Grid.Add(new List<List<Cube>>(partition));
            for (int j = 0; j < partition; j++)
            {
                Grid[i].Add(new List<Cube>(partition));
                for (int k = 0; k < partition; k++)
                {
                    p = new Point(startX + (i + start) * step + halfStep * Sgn(i), startY + (j + start) * step + halfStep * Sgn(j), startZ + (k + start) * step + halfStep * Sgn(k));
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
        
        if (startPoint.X == endPoint.X || startPoint.Y == endPoint.Y || startPoint.Z == endPoint.Z)
        {
            throw new ArgumentException("По заданным точкам видео, что это не куб");
        }

        double xDist = Math.Abs(startPoint.X - endPoint.X);
        double yDist = Math.Abs(startPoint.X - endPoint.X);
        double zDist = Math.Abs(startPoint.X - endPoint.X);

        if (xDist != yDist || xDist != zDist)
        {
            throw new ArgumentException("По заданным точкам видео, что это не куб");
        }

        Grid = new List<List<List<Cube>>>();
        
        
        Point pZero = new Point();
        if (pZero.DistanceBetweenPoint(startPoint) > pZero.DistanceBetweenPoint(endPoint))
        {
            (startPoint, endPoint) = (endPoint, startPoint);
        }
        
        double newSideLengthHalf = xDist / (2 * Math.Sqrt(2));
        double step = newSideLengthHalf * 2 / partition;
        double halfStep = step * 0.5;
        double startX = startPoint.X;
        double startY = startPoint.Y;
        double startZ = startPoint.Z;
        Point p;

        for (int i = 0; i < partition; i++)
        {
            Grid.Add(new List<List<Cube>>(partition));
            for (int j = 0; j < partition; j++)
            {
                Grid[i].Add(new List<Cube>(partition));
                for (int k = 0; k < partition; k++)
                {
                    p = new Point(startX + halfStep + (i - newSideLengthHalf) * step, startY + halfStep + (j - newSideLengthHalf) * step,
                        startZ + halfStep + (k - newSideLengthHalf) * step);
                    Grid[i][j].Add( new Cube(p, step));
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

    int Sgn(int value)
    {
        return (value > 0)? 1 : (value == 0)? -1 : 0;
    }
}