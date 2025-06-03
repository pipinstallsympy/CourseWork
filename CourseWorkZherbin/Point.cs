namespace CourseWorkZherbin;
public class Point
{
    public double X;
    public double Y;
    public double Z;

    public Point(double x = 0, double y = 0, double z = 0) { X = x; Y = y; Z = z; }
    
    public static Point operator +(Point a, Point b) =>
        new Point(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        
    public static Point operator -(Point a, Point b) =>
        new Point(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static bool operator ==(Point a, Point b)
    {
        if(ReferenceEquals(a, b)) return true;
        return (Math.Abs(a.X - b.X) < 1e-10) && (Math.Abs(a.Y - b.Y) < 1e-10) && (Math.Abs(a.Z - b.Z) < 1e-10);
    }

    public static bool operator !=(Point a, Point b) => !(a == b);

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }
        Point p = (Point)obj;
        return this == p;
    }
    
    
    public double DistanceBetweenPoint(Point p) => Math.Sqrt(Math.Pow(p.X - X, 2) + Math.Pow(p.Y - Y, 2) + Math.Pow(p.Z - Z, 2));
    
}