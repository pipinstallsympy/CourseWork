namespace CourseWorkZherbin;

public class Cube: Figure
{
    public Point CentralPoint;
    public double SideLength;
    public double HalfDiagLength;

    public Cube(Point centralPoint, double sideLength)
    {
        if(sideLength <= 0) throw new ArgumentException();
        CentralPoint = centralPoint;
        SideLength = sideLength;
        HalfDiagLength = sideLength * Math.Sqrt(2) / 2;
        this.IsEmpty = false;
    }
    

    public double GetDiagonalLength() => HalfDiagLength * 2;
}