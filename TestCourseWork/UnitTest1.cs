using System.Diagnostics;

namespace TestCourseWork;
using CourseWorkZherbin;

public class UnitTest1
{
    [Fact]
    public void TestDistanceBetweenPoint()
    {
        Point p1 = new Point();
        Point p2 = new Point(1, 1, 1);
        Point p3 = new Point(-1, -1, -1);
        
        Assert.Equal(Math.Sqrt(3), p1.DistanceBetweenPoint(p2));
        Assert.Equal(0.0, p1.DistanceBetweenPoint(p1));
        Assert.Equal(0.0, p2.DistanceBetweenPoint(p2));
        Assert.Equal(2 * Math.Sqrt(3), p3.DistanceBetweenPoint(p2));
    }

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