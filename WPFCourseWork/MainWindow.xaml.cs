using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CourseWorkZherbin;
using Point = CourseWorkZherbin.Point;
using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;

namespace WPFCourseWork;

public partial class MainWindow : Window
{
    private static GridLinesVisual3D gridLines = new GridLinesVisual3D()
    {
        Width = 1000,     
        Length = 1000,    
        MinorDistance = 1,  
        MajorDistance = 1,  
        Thickness = 0.01,   
    };
    private static CoordinateSystemVisual3D coordinateSystem = new CoordinateSystemVisual3D();
    private static List<TreeNode<Cube>> coherenceTreeList  = new List<TreeNode<Cube>>();

    private CubeLine? _currentLine;
    private Dictionary<Cube, Color>? _cubeColorMap;

    public MainWindow()
    {
        InitializeComponent();
        MethodsPanel.SelectionChanged += OnSelectionChanged;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch (MethodsPanel.SelectedIndex)
        {
            case 0:
                MethodsValue2.Visibility = Visibility.Collapsed;
                MethodsValue3.Visibility = Visibility.Collapsed;
                break;
            case 1:
                MethodsValue2.Visibility = Visibility.Visible;
                MethodsValue3.Visibility = Visibility.Collapsed;
                break;
            case 2:
                MethodsValue2.Visibility = Visibility.Collapsed;
                MethodsValue3.Visibility = Visibility.Visible;
                break;
        }
    }

    private void OnMethodSelected(object sender, RoutedEventArgs e)
    {
        var selectedPores = PorePanel.Children
            .OfType<RadioButton>()
            .FirstOrDefault(r => r.IsChecked == true)?.Content.ToString();

        if (!int.TryParse(PartitionAmount.Text, out int partition))
        {
            MessageBox.Show("Кол-во разбиений задано неверно");
            return;
        }
        if (!double.TryParse(PoresValue.Text, out double poresValue))
        {
            MessageBox.Show("Значение пор задано неверно");
            return;
        }

        switch (MethodsPanel.SelectedIndex)
        {
            case 0:
                RunTimedCalculation(() => CalculationsSingular(partition, selectedPores, poresValue));
                break;
            case 1:
                RunTimedCalculation(() => CalculationsCube(partition, selectedPores, poresValue));
                break;
            case 2:
                RunTimedCalculation(() => CalculationsPoints(partition, selectedPores, poresValue));
                break;
            default:
                MessageBox.Show("Что-то тут не так....");
                break;
        }
    }

    private void CalculationsSingular(int partition, string? poreChoice, double poresValue)
    {
        using (CubeGrid griddy = new CubeGrid(partition))
        {
            CubeLine? liney = griddy.GenerateLineFromGrid();
            CreatePores(liney, poreChoice, poresValue);
            UpdateNodeStats(liney);
            StoreGeneratedLine(liney);
            RedrawCubes();
        }        
    }
    
    private void CalculationsCube(int partition, string? poreChoice, double poresValue)
    {
        if (!double.TryParse(CenterX.Text, out double centerX))
        {
            MessageBox.Show("Координаты заданы неверно");
            return;
        }
        if (!double.TryParse(CenterY.Text, out double centerY))
        {
            MessageBox.Show("Координаты заданы неверно");
            return;
        }
        if (!double.TryParse(CenterZ.Text, out double centerZ))
        {
            MessageBox.Show("Координаты заданы неверно");
            return;
        }
        if (!double.TryParse(CubeSideLength.Text, out double sideLength))
        {
            MessageBox.Show("Сторона куба задана наверно");
            return;
        }
        
        Cube startCube;
        CubeGrid griddy;
        CubeLine? liney;
        try
        {
            startCube = new Cube(new Point(centerX, centerY, centerZ), sideLength);
            using (griddy = new CubeGrid(startCube, partition))
            {
                liney = griddy.GenerateLineFromGrid(); 
                CreatePores(liney, poreChoice, poresValue);
                UpdateNodeStats(liney);
                StoreGeneratedLine(liney);
                RedrawCubes();
            }
        }
        catch(Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }
    
    private void CalculationsPoints(int partition, string? poreChoice, double poresValue)
    {
        if (!double.TryParse(P1X.Text, out double p1X))
        {
            MessageBox.Show("Координаты заданы неверно");
            return;
        }
        if (!double.TryParse(P1Y.Text, out double p1Y))
        {
            MessageBox.Show("Координаты заданы неверно");
            return;
        }
        if (!double.TryParse(P1Z.Text, out double p1Z))
        {
            MessageBox.Show("Координаты заданы неверно");
            return;
        }
        if (!double.TryParse(P2X.Text, out double p2X))
        {
            MessageBox.Show("Координаты заданы неверно");
            return;
        }
        if (!double.TryParse(P2Y.Text, out double p2Y))
        {
            MessageBox.Show("Координаты заданы неверно");
            return;
        }
        if (!double.TryParse(P2Z.Text, out double p2Z))
        {
            MessageBox.Show("Координаты заданы неверно");
            return;
        }
        
        Point p1 = new Point(p1X, p1Y, p1Z);
        Point p2 = new Point(p2X, p2Y, p2Z);
        CubeGrid griddy;
        CubeLine? liney;
        
        try
        {
            using (griddy = new CubeGrid(p1, p2, partition))
            {
                liney = griddy.GenerateLineFromGrid();
                CreatePores(liney, poreChoice, poresValue);
                UpdateNodeStats(liney);
                StoreGeneratedLine(liney);
                RedrawCubes();
            }
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
            
        }
    }

    

    private void RunTimedCalculation(Action calculation)
    {
        var sw = Stopwatch.StartNew();
        calculation();
        sw.Stop();
        StatsLastCalcTime.Text = $"Время последнего расчёта: {sw.ElapsedMilliseconds} мс";
    }

    private void UpdateNodeStats(CubeLine? liney)
    {
        if (liney == null) return;
        int total = liney.Count();
        int pores = liney.PoreAmount();
        StatsMaterialCount.Text = $"Узлов материала: {total - pores}";
        StatsPoreCount.Text     = $"Узлов пор: {pores}";
    }

    private void CreatePores(CubeLine? liney, string? poreChoice, double poresValue)
    {
        try
        {
            switch (poreChoice)
            {
                case "По количеству":
                    liney.GeneratePoresByCount(Convert.ToInt32(poresValue));
                    break;
                case "По процентному соотношению":
                    liney.GeneratePoresByPercent(poresValue);
                    break;
                default:
                    MessageBox.Show("При выборе метода созданию пор пошло что-то не так");
                    break;
            }
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }

    private void StoreGeneratedLine(CubeLine? liney)
    {
        _currentLine = liney;
        coherenceTreeList = new List<TreeNode<Cube>>();
        _cubeColorMap = null;
        StatsCoherencyCount.Text = "Связных компонент: -";
    }

    private void OnCheckCoherency(object sender, RoutedEventArgs e)
    {
        if (_currentLine == null)
        {
            MessageBox.Show("Сначала сгенерируйте объект");
            return;
        }

        try
        {
            var coherency = new Coherency();
            using var grid = _currentLine.GenerateGridFromLine();
            var sw = Stopwatch.StartNew();
            coherenceTreeList = coherency.CreateCT(grid);
            sw.Stop();
            _cubeColorMap = BuildColorMap(coherenceTreeList);
            StatsLastCalcTime.Text = $"Время последнего расчёта: {sw.ElapsedMilliseconds} мс";
            StatsCoherencyCount.Text = $"Связных компонент: {coherenceTreeList.Count}";
            RedrawCubes();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private void OnColorByComponentChanged(object sender, RoutedEventArgs e)
    {
        if (_currentLine == null) return;
        RedrawCubes();
    }

    private void OnConnectComponents(object sender, RoutedEventArgs e)
    {
        if (_currentLine == null)
        {
            MessageBox.Show("Сначала сгенерируйте объект");
            return;
        }

        try
        {
            var sw = Stopwatch.StartNew();

            using var grid = _currentLine.GenerateGridFromLine();
            var components = new Coherency().CreateCT(grid);

            if (components.Count <= 1)
            {
                sw.Stop();
                MessageBox.Show("Все узлы материала уже связаны.");
                return;
            }

            var bridges = new CoherencyConnector().ConnectAll(grid, components);

            foreach (var bridge in bridges)
            {
                foreach (var cube in bridge.EmptyCubesToFill)
                {
                    if (cube.IsEmpty) cube.IsEmpty = false;
                }
            }

            using var gridAfter = _currentLine.GenerateGridFromLine();
            coherenceTreeList = new Coherency().CreateCT(gridAfter);
            _cubeColorMap = BuildColorMap(coherenceTreeList);

            sw.Stop();

            UpdateNodeStats(_currentLine);
            StatsCoherencyCount.Text = $"Связных компонент: {coherenceTreeList.Count}";
            StatsLastCalcTime.Text = $"Время последнего расчёта: {sw.ElapsedMilliseconds} мс";

            RedrawCubes();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private static Dictionary<Cube, Color> BuildColorMap(List<TreeNode<Cube>> components)
    {
        var map = new Dictionary<Cube, Color>();
        int n = components.Count;
        if (n == 0) return map;

        for (int i = 0; i < n; i++)
        {
            double hue = i * 360.0 / n;
            Color color = HsvToRgb(hue, 0.75, 0.95);

            var stack = new Stack<TreeNode<Cube>>();
            stack.Push(components[i]);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                map[node.Value] = color;
                foreach (var child in node.Children)
                {
                    stack.Push(child);
                }
            }
        }

        return map;
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;

        double r, g, b;
        if (h < 60)        { r = c; g = x; b = 0; }
        else if (h < 120)  { r = x; g = c; b = 0; }
        else if (h < 180)  { r = 0; g = c; b = x; }
        else if (h < 240)  { r = 0; g = x; b = c; }
        else if (h < 300)  { r = x; g = 0; b = c; }
        else               { r = c; g = 0; b = x; }

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    private void RedrawCubes()
    {
        Viewport.Children.Clear();
        GC.Collect(2);
        Viewport.Children.Add(gridLines);
        Viewport.Children.Add(coordinateSystem);
        Viewport.Children.Add(new DefaultLights());

        if (_currentLine == null) return;

        bool useComponentColors = ColorComponentsCheckBox.IsChecked == true
                                  && _cubeColorMap != null
                                  && _cubeColorMap.Count > 0;

        int len = _currentLine.Count();
        for (int i = 0; i < len; i++)
        {
            var current = _currentLine[i];
            if (current.IsEmpty) continue;

            Color color = Colors.Red;
            if (useComponentColors && _cubeColorMap!.TryGetValue(current, out var mapped))
            {
                color = mapped;
            }

            var cube = new BoxVisual3D()
            {
                Center = new Point3D()
                {
                    X = current.CentralPoint.X,
                    Y = current.CentralPoint.Y,
                    Z = current.CentralPoint.Z
                },
                Width = current.SideLength,
                Height = current.SideLength,
                Length = current.SideLength,
                Material = MaterialHelper.CreateMaterial(color)
            };
            Viewport.Children.Add(cube);
        }
    }
}
