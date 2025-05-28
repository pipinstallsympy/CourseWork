using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("TestCourseWork")]
namespace CourseWorkZherbin;


public class Point
{
    protected double x;
    protected double y;
    protected double z;

    public Point(double x = 0, double y = 0, double z = 0)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    
    public static Point operator +(Point a, Point b) =>
        new Point(a.x + b.x, a.y + b.y, a.z + b.z);
        
    public static Point operator -(Point a, Point b) =>
        new Point(a.x - b.x, a.y - b.y, a.z - b.z);
    
    public double DistanceBetweenPoint( Point p ) => Math.Sqrt(Math.Pow(p.x - this.x, 2) + Math.Pow(p.y - this.y, 2) + Math.Pow(p.z - this.z, 2));
}