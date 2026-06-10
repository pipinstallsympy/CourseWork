using CourseWorkZherbin;

namespace TestCourseWork;

public class UnitTestCoherencyCancellation
{
    [Fact]
    public void CreateCt_ThrowsWhenCancelled()
    {
        using var grid = new CubeGrid(10);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            Coherency.CreateCt(grid, cancellationToken: cts.Token));
    }

    [Fact]
    public void ConnectAll_ThrowsWhenCancelled()
    {
        using var grid = new CubeGrid(10);
        var components = Coherency.CreateCt(grid);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new CoherencyConnector().ConnectAll(grid, components, cts.Token));
    }
}
