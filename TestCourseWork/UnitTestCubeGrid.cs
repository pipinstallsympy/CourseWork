using CourseWorkZherbin;

namespace TestCourseWork;

public class UnitTestCubeGrid
{
    [Theory]
    [InlineData(2)]
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
}