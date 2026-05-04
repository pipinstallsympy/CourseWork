namespace CourseWorkZherbin;

public class Cube: Figure
{
    public Point CentralPoint;
    public double SideLength;

    public Cube(Point centralPoint, double sideLength)
    {
        if(sideLength <= 0) throw new ArgumentException("Сторона куба должна быть неотрицательной");
        CentralPoint = centralPoint;
        SideLength = sideLength;
        IsEmpty = false;
    }
    
    public double X  =>  CentralPoint.X;
    public double Y  =>  CentralPoint.Y;
    public double Z  =>  CentralPoint.Z;
}