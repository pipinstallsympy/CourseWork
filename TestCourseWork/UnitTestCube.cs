namespace TestCourseWork;
using CourseWorkZherbin;
public class UnitTestCube
{
    [Fact]
    public void TestDiagonalLength()
    {
        Cube c1 = new Cube(new Point(1, 1, 1), 5);
        Cube c2 = new Cube(new Point(1, 1, 1), 1);
        Cube c3 = new Cube(new Point(1, 1, 1), 0.1);
        
        Assert.Equal(5 * Math.Sqrt(2), c1.GetDiagonalLength());
        Assert.Equal(Math.Sqrt(2), c2.GetDiagonalLength());
        Assert.Equal(0.1 * Math.Sqrt(2), c3.GetDiagonalLength());
    }

    [Fact]
    public void TestDiagonalLength_Exception()
    {
        Assert.Throws<ArgumentException>(() => new Cube(new Point(1, 1, 1), 0));
        Assert.Throws<ArgumentException>(() => new Cube(new Point(1, 1, 1), -1));
    }
}