using CourseWorkZherbin;

namespace TestCourseWork;

public class UnitTestCubeGrid
{
    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public void TestConstructorPartition(int partition)
    {
        CubeGrid g = new CubeGrid(partition);
        double step = 1.0 / partition;
        double halfStep = step * 0.5;
        
        for (int i = 0; i < partition; i++)
        {
            for (int j = 0; j < partition; j++)
            {
                for (int k = 0; k < partition; k++)
                {
                    Assert.Equal(g.Grid[i][j][k].CentralPoint, new Point(halfStep + i * step, halfStep + j * step, halfStep + k * step));
                    Assert.Equal(g.Grid[i][j][k].SideLength, step);
                }
            }
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void TestConstructorPartition_Exception(int partition)
    {
        Assert.Throws<ArgumentException>(() => new CubeGrid(partition));
    }

    public static IEnumerable<object[]> TestData_1()
    {
        yield return [new Cube(new Point(), 10), 10];
        yield return [new Cube(new Point(1, 2, 3), 5), 25];
        yield return [new Cube(new Point(), 1), 1];
    }
    
    [Theory]
    [MemberData(nameof(TestData_1))]
    public void TestConstructorStartCubePartition(Cube startCube, int partition)
    {
        CubeGrid g = new CubeGrid(startCube, partition);
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
        
        double startX = startCube.CentralPoint.X;
        double startY = startCube.CentralPoint.Y;
        double startZ = startCube.CentralPoint.Z;
        int end = partition / 2;
        int start = end * -1;

        for (int i = 0; i < partition; i++)
        {
            for (int j = 0; j < partition; j++)
            {
                for (int k = 0; k < partition; k++)
                {
                    Assert.Equal(g.Grid[i][j][k].CentralPoint, new Point(startX + (i + start) * step + halfStep * Sgn(i), startY + (j + start) * step + halfStep * Sgn(j), startZ + (k + start) * step + halfStep * Sgn(k)));
                    Assert.Equal(g.Grid[i][j][k].SideLength, step);
                }
            }
        }
    }
    
    public static IEnumerable<object[]> TestData_2()
    {
        yield return [new Cube(new Point(1, 2, 3), 5), 0];
        yield return [null, 0];

    }

    [Theory]
    [MemberData(nameof(TestData_2))]
    public void TestConstructorStartCubePartition_Exception1(Cube startCube, int partition)
    {
        Assert.Throws<ArgumentException>(() => new CubeGrid(startCube, partition));
    }


    public static IEnumerable<object[]> TestData_3()
    {
        yield return [null, 5];
    }
    [Theory]
    [MemberData(nameof(TestData_3))]
    public void TestConstructorStartCubePartition_Exception2(Cube startCube, int partition)
    {
        Assert.Throws<ArgumentNullException>(() => new CubeGrid(startCube, partition));
    }

    public static IEnumerable<object[]> TestData_4()
    {
        yield return [new Point(), new Point(1, 1, 1), 5];
        yield return [new Point(-1, -1, -1), new Point(1, 1, 1), 10];
        yield return [new Point(2, 2, 2), new Point(-2, -2, -2), 5];
    }

    [Theory]
    [MemberData(nameof(TestData_4))]
    public void TestConstructorStartPointEndPointPartition(Point startPoint, Point endPoint, int partition)
    {
        double xDist = Math.Abs(startPoint.X - endPoint.X);
        double yDist = Math.Abs(startPoint.X - endPoint.X);
        double zDist = Math.Abs(startPoint.X - endPoint.X);

        CubeGrid g = new CubeGrid(startPoint, endPoint, partition);
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

        for (int i = 0; i < partition; i++)
        {
            for (int j = 0; j < partition; j++)
            {
                for (int k = 0; k < partition; k++)
                {
                    Assert.Equal(g.Grid[i][j][k].CentralPoint, new Point(startX + halfStep + (i - newSideLengthHalf) * step, startY + halfStep + (j - newSideLengthHalf) * step,
                        startZ + halfStep + (k - newSideLengthHalf) * step));
                    Assert.Equal(g.Grid[i][j][k].SideLength, step);
                }
            }
        }
    }
    
    public static IEnumerable<object[]> TestData_5()
    {
        yield return [new Point(), new Point(1, 1, 1), 0];
        yield return [new Point(-1, -1, -1), new Point(-1, 1, 1), 10];
        yield return [new Point(2, 2, 2), new Point(-2, 2, -2), 5];
        yield return [new Point(2, 2, 2), new Point(-2, -2, 2), 5];
        yield return [new Point(1, 1, 1), new Point(1, 2, 3), 20];
    }

    [Theory]
    [MemberData(nameof(TestData_5))]
    public void TestConstructorStartPointEndPointPartition_Exception(Point startPoint, Point endPoint, int partition)
    {
        Assert.Throws<ArgumentException>(() => new CubeGrid(startPoint, endPoint, partition));
    }

    public static IEnumerable<object[]> TestData_6()
    {
        yield return [new CubeGrid(new Point(), new Point(1, 1, 1),2)];
    }
    
    [Theory]
    [MemberData(nameof(TestData_6))]
    public void TestGenerateLineFromGrid(CubeGrid grid)
    {
        CubeLine testLine = grid.GenerateLineFromGrid();

        int len = grid.Grid.Count;

        for (int i = 0; i < len; i++)
        {
            for (int j = 0; j < len; j++)
            {
                for (int k = 0; k < len; k++)
                {
                    Assert.Equal(grid.Grid[i][j][k], testLine.Line[i * len * len + j * len + k]);
                }
            }
        }
    }

    
    
    int Sgn(int value)
    {
        return (value > 0)? 1 : (value == 0)? -1 : 0;
    }
}