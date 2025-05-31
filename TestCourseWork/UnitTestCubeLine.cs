using CourseWorkZherbin;

namespace TestCourseWork;

public class UnitTestCubeLine
{
    public static IEnumerable<object[]> Test_Data()
    {
        yield return [new CubeGrid(10)];
        yield return [new CubeGrid(new Point(-1, -1, -1), new Point(1, 1, 1), 10)];
        yield return [new CubeGrid(new Cube(new Point(5, 5, 5), 20), 5)];
    }
    [Theory]
    [MemberData(nameof(Test_Data))]
    public void TestConstructor(CubeGrid grid)
    {
        CubeLine line = new CubeLine(grid);
        
        int len = grid.Grid.Count;
        for (int i = 0; i < len; i++)
        {
            for (int j = 0; j < len; j++)
            {
                for (int k = 0; k < len; k++)
                {
                    Assert.Equal(line.Line[i * len * len + j * len + k], grid.Grid[i][j][k]);
                }
            }
        }
    }
    
    public static IEnumerable<object[]> Test_Data2()
    {
        yield return [new CubeLine(new CubeGrid(10)), 5];
        yield return [new CubeLine(new CubeGrid(new Point(-1, -1, -1), new Point(1, 1, 1), 10)), 20];
        yield return [new CubeLine(new CubeGrid(new Cube(new Point(5, 5, 5), 20), 5)), 3];
    }

    [Theory]
    [MemberData(nameof(Test_Data2))]
    public void TestGeneratePoresByCount(CubeLine line, int poreAmount)
    {
        int poreCount = 0;
        int len = line.Line.Count;
        line.GeneratePoresByCount(poreAmount);

        for (int i = 0; i < len; i++)
        {
            if (line.Line[i].IsEmpty) poreCount++;
        }
        
        Assert.Equal(poreCount, poreAmount);
    }

    public static IEnumerable<object[]> Test_Data3()
    {
        yield return [new CubeLine(new CubeGrid(10)), -1];
        yield return [new CubeLine(new CubeGrid(new Point(-1, -1, -1), new Point(1, 1, 1), 10)), 1000];
        yield return [new CubeLine(new CubeGrid(new Cube(new Point(5, 5, 5), 20), 5)), 126];
    }
    
    [Theory]
    [MemberData(nameof(Test_Data3))]
    public void TestGeneratePoresByCount_Exception(CubeLine line, int poreAmount)
    {
        Assert.Throws<ArgumentException>(() => line.GeneratePoresByCount(poreAmount));
    }
}