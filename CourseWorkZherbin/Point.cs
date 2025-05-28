namespace CourseWorkZherbin;


public class Point
{
    public double X;
    public double Y;
    public double Z;

    public Point(double x = 0, double y = 0, double z = 0)
    {
        this.X = x;
        this.Y = y;
        this.Z = z;
    }
    
    public static Point operator +(Point a, Point b) =>
        new Point(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        
    public static Point operator -(Point a, Point b) =>
        new Point(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    
    public double DistanceBetweenPoint( Point p ) => Math.Sqrt(Math.Pow(p.X - this.X, 2) + Math.Pow(p.Y - this.Y, 2) + Math.Pow(p.Z - this.Z, 2));
}