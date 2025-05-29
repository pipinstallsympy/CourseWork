namespace TestCourseWork;
using CourseWorkZherbin;

public class UnitTestPoint
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
    public void TestEquals()
    {
        Point p1 = new Point(1, 1, 1);
        Point p2 = new Point(1, 1, 1);
        Point p3 = new Point(0, 0, 0);
        Assert.Equal(p1, p2);
        Assert.Equal(p2, p1);
        
        Assert.NotEqual(p1, p3);
        Assert.NotEqual(p3, p1);
    }

    
}