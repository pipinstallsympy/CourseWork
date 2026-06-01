using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private PermeabilityTreeList? _permeabilityTreeList;
    private HashSet<Cube>? _percolationBoundaryPores;
    private Cube? _selectedBoundaryPore;
    private Dictionary<Cube, HashSet<Cube>>? _partialPeersByCube;
    private Dictionary<Cube, HashSet<Cube>>? _endToEndPeersByCube;
    private readonly Dictionary<Visual3D, Cube> _visualToCube = new();

    public MainWindow()
    {
        InitializeComponent();
        MethodsPanel.SelectionChanged += OnSelectionChanged;
        Viewport.PreviewMouseLeftButtonDown += OnViewport3DClick;
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

        _permeabilityTreeList = null;
        _percolationBoundaryPores = null;
        _partialPeersByCube = null;
        _endToEndPeersByCube = null;
        _selectedBoundaryPore = null;
        StatsPercolationCount.Text = "Деревьев перколяции: -";
        ShowPercolationCheckBox.IsChecked = false;
        ShowPercolationCheckBox.IsEnabled = false;
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

    private void OnCheckPercolation(object sender, RoutedEventArgs e)
    {
        if (_currentLine == null)
        {
            MessageBox.Show("Сначала сгенерируйте объект");
            return;
        }

        try
        {
            using var grid = _currentLine.GenerateGridFromLine();
            if (grid.Count() == 0)
            {
                MessageBox.Show("Сетка пуста");
                return;
            }

            double fullSideLength = grid[0][0][0].SideLength * grid.Count();

            var sw = Stopwatch.StartNew();
            _permeabilityTreeList = new PermeabilityTreeList(grid, fullSideLength);
            var dict = new PermeabilityDictionary(_permeabilityTreeList);
            (_percolationBoundaryPores, _partialPeersByCube, _endToEndPeersByCube)
                = BuildPercolationIndex(dict);
            _selectedBoundaryPore = null;
            sw.Stop();

            StatsPercolationCount.Text = $"Деревьев перколяции: {_permeabilityTreeList.TreeList.Count}";
            StatsLastCalcTime.Text = $"Время последнего расчёта: {sw.ElapsedMilliseconds} мс";

            ShowPercolationCheckBox.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private void OnShowPercolationChanged(object sender, RoutedEventArgs e)
    {
        if (ShowPercolationCheckBox.IsChecked != true)
        {
            _selectedBoundaryPore = null;
        }
        if (_currentLine == null) return;
        RedrawCubes();
    }

    private void OnViewport3DClick(object sender, MouseButtonEventArgs e)
    {
        if (ShowPercolationCheckBox.IsChecked != true) return;
        if (_percolationBoundaryPores == null) return;

        var pt = e.GetPosition(Viewport);
        var hits = Viewport3DHelper.FindHits(Viewport.Viewport, pt);
        foreach (var h in hits)
        {
            if (_visualToCube.TryGetValue(h.Visual, out var cube))
            {
                if (!ReferenceEquals(_selectedBoundaryPore, cube))
                {
                    _selectedBoundaryPore = cube;
                    RedrawCubes();
                }
                e.Handled = true;
                return;
            }
        }

        if (_selectedBoundaryPore != null)
        {
            _selectedBoundaryPore = null;
            RedrawCubes();
        }
    }

    private static (HashSet<Cube> boundary,
                    Dictionary<Cube, HashSet<Cube>> partialPeers,
                    Dictionary<Cube, HashSet<Cube>> endToEndPeers)
        BuildPercolationIndex(PermeabilityDictionary dict)
    {
        var boundary = new HashSet<Cube>();
        var partial = new Dictionary<Cube, HashSet<Cube>>();
        var endToEnd = new Dictionary<Cube, HashSet<Cube>>();

        AccumulatePeers(dict.PartialPermeability, partial, boundary);
        AccumulatePeers(dict.EndToEndPermeability, endToEnd, boundary);

        return (boundary, partial, endToEnd);
    }

    private static void AccumulatePeers(
        Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> source,
        Dictionary<Cube, HashSet<Cube>> peers,
        HashSet<Cube> boundary)
    {
        foreach (var kvp in source)
        {
            Cube a = kvp.Key.Value;
            boundary.Add(a);
            if (!peers.TryGetValue(a, out var listA))
            {
                listA = new HashSet<Cube>();
                peers[a] = listA;
            }
            foreach (var node in kvp.Value)
            {
                Cube b = node.Value;
                boundary.Add(b);
                listA.Add(b);
                if (!peers.TryGetValue(b, out var listB))
                {
                    listB = new HashSet<Cube>();
                    peers[b] = listB;
                }
                listB.Add(a);
            }
        }
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
            bool preserveMaterial = PreserveMaterialCheckBox?.IsChecked == true;
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

            var distinctBridgeCubes = new HashSet<Cube>();
            foreach (var b in bridges)
                foreach (var c in b.EmptyCubesToFill)
                    distinctBridgeCubes.Add(c);

            int bridgeCubesCount = distinctBridgeCubes.Count;
            int leavesShortage = 0;

            if (preserveMaterial && bridgeCubesCount > 0)
            {
                var forbidden = BuildForbiddenSet(grid, distinctBridgeCubes);
                int removed = RemoveLeavesCascade(components, forbidden, bridgeCubesCount);
                leavesShortage = bridgeCubesCount - removed;
            }

            foreach (var cube in distinctBridgeCubes)
            {
                if (cube.IsEmpty) cube.IsEmpty = false;
            }

            using var gridAfter = _currentLine.GenerateGridFromLine();
            coherenceTreeList = new Coherency().CreateCT(gridAfter);
            _cubeColorMap = BuildColorMap(coherenceTreeList);

            sw.Stop();

            UpdateNodeStats(_currentLine);
            StatsCoherencyCount.Text = $"Связных компонент: {coherenceTreeList.Count}";
            StatsLastCalcTime.Text = $"Время последнего расчёта: {sw.ElapsedMilliseconds} мс";

            RedrawCubes();

            if (leavesShortage > 0)
            {
                MessageBox.Show(
                    $"Не хватило листовых узлов материала для полного сохранения количества: " +
                    $"{leavesShortage} соединяющих узлов добавлены без компенсации.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private static int RemoveLeavesCascade(
        List<TreeNode<Cube>> components,
        HashSet<Cube> forbidden,
        int target)
    {
        var leaves = new Queue<TreeNode<Cube>>();
        foreach (var root in components)
        {
            var stack = new Stack<TreeNode<Cube>>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n.Children.Count == 0) leaves.Enqueue(n);
                foreach (var ch in n.Children) stack.Push(ch);
            }
        }

        int removed = 0;
        while (removed < target && leaves.Count > 0)
        {
            var leaf = leaves.Dequeue();
            if (leaf.Children.Count > 0) continue;
            if (forbidden.Contains(leaf.Value)) continue;

            leaf.Value.IsEmpty = true;
            var parent = leaf.Parent;
            if (parent != null)
            {
                parent.Children.Remove(leaf);
                if (parent.Children.Count == 0) leaves.Enqueue(parent);
            }
            removed++;
        }
        return removed;
    }

    private static HashSet<Cube> BuildForbiddenSet(CubeGrid grid, IEnumerable<Cube> bridgeCubes)
    {
        int n = grid.Count();
        var coords = new Dictionary<Cube, (int x, int y, int z)>(n * n * n);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                for (int k = 0; k < n; k++)
                {
                    coords[grid[i][j][k]] = (i, j, k);
                }
            }
        }

        int[] dx = [0, 1, -1, 0, 0, 0, 0];
        int[] dy = [0, 0, 0, 1, -1, 0, 0];
        int[] dz = [0, 0, 0, 0, 0, 1, -1];

        var forbidden = new HashSet<Cube>();
        foreach (var c in bridgeCubes)
        {
            if (!coords.TryGetValue(c, out var pos)) continue;
            for (int i = 0; i < 7; i++)
            {
                int nx = pos.x + dx[i];
                int ny = pos.y + dy[i];
                int nz = pos.z + dz[i];
                if (nx < 0 || ny < 0 || nz < 0) continue;
                if (nx >= n || ny >= n || nz >= n) continue;
                forbidden.Add(grid[nx][ny][nz]);
            }
        }
        return forbidden;
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
        _visualToCube.Clear();
        GC.Collect(2);
        Viewport.Children.Add(gridLines);
        Viewport.Children.Add(coordinateSystem);
        Viewport.Children.Add(new DefaultLights());

        if (_currentLine == null) return;

        bool useComponentColors = ColorComponentsCheckBox.IsChecked == true
                                  && _cubeColorMap != null
                                  && _cubeColorMap.Count > 0;

        bool showPercolation = ShowPercolationCheckBox.IsChecked == true
                               && _percolationBoundaryPores != null;

        int len = _currentLine.Count();
        int side = (int)Math.Round(Math.Cbrt(len));
        HashSet<(int, int, int, int, int, int)>? uniqueEdges =
            showPercolation ? new HashSet<(int, int, int, int, int, int)>() : null;

        

        var deferredTransparent = showPercolation
            ? new List<(Cube cube, Color color, double opacity)>()
            : null;

        

        for (int idx = 0; idx < len; idx++)
        {
            var current = _currentLine[idx];

            if (current.IsEmpty)
            {
                if (!showPercolation) continue;
                if (!_percolationBoundaryPores!.Contains(current)) continue;

                (Color color, double opacity) = ResolveBoundaryPoreAppearance(current);
                if (opacity >= 1.0)
                {
                    AddCubeVisual(current, color, opacity, current);
                    
                }
                else
                {
                    deferredTransparent!.Add((current, color, opacity));
                    
                }
                continue;
            }

            if (showPercolation)
            {
                int i = idx / (side * side);
                int j = (idx / side) % side;
                int k = idx % side;
                CollectOuterFaceEdges(uniqueEdges!, i, j, k, side);
                continue;
            }

            Color cubeColor = Colors.Red;
            if (useComponentColors && _cubeColorMap!.TryGetValue(current, out var mapped))
            {
                cubeColor = mapped;
            }

            AddCubeVisual(current, cubeColor, 1.0);
        }

        Point3DCollection? materialEdges = null;
        if (showPercolation && uniqueEdges!.Count > 0)
        {
            var c000 = _currentLine[0];
            double half = c000.SideLength * 0.5;
            double step = c000.SideLength;
            double baseX = c000.CentralPoint.X - half;
            double baseY = c000.CentralPoint.Y - half;
            double baseZ = c000.CentralPoint.Z - half;

            materialEdges = new Point3DCollection(uniqueEdges.Count * 2);
            foreach (var e in uniqueEdges)
            {
                materialEdges.Add(new Point3D(baseX + e.Item1 * step, baseY + e.Item2 * step, baseZ + e.Item3 * step));
                materialEdges.Add(new Point3D(baseX + e.Item4 * step, baseY + e.Item5 * step, baseZ + e.Item6 * step));
            }

            var wire = new LinesVisual3D
            {
                Points = materialEdges,
                Color = Colors.DimGray,
                Thickness = 1.0
            };
            Viewport.Children.Add(wire);
        }

        if (deferredTransparent != null)
        {
            foreach (var (cube, color, opacity) in deferredTransparent)
            {
                AddCubeVisual(cube, color, opacity, cube);
            }
        }

    }

    private (Color color, double opacity) ResolveBoundaryPoreAppearance(Cube cube)
    {
        if (_selectedBoundaryPore == null)
        {
            return (Colors.LimeGreen, 1.0);
        }

        if (ReferenceEquals(cube, _selectedBoundaryPore))
        {
            return (Colors.LimeGreen, 1.0);
        }

        if (_endToEndPeersByCube != null
            && _endToEndPeersByCube.TryGetValue(_selectedBoundaryPore, out var endToEndPeers)
            && endToEndPeers.Contains(cube))
        {
            return (Colors.DodgerBlue, 1.0);
        }

        if (_partialPeersByCube != null
            && _partialPeersByCube.TryGetValue(_selectedBoundaryPore, out var partialPeers)
            && partialPeers.Contains(cube))
        {
            return (Colors.Gold, 1.0);
        }

        return (Colors.LightGray, 1.0);
    }

    private void CollectOuterFaceEdges(
        HashSet<(int, int, int, int, int, int)> set,
        int i, int j, int k, int side)
    {
        bool nXm = IsMaterialNeighbor(i - 1, j, k, side);
        bool nXp = IsMaterialNeighbor(i + 1, j, k, side);
        bool nYm = IsMaterialNeighbor(i, j - 1, k, side);
        bool nYp = IsMaterialNeighbor(i, j + 1, k, side);
        bool nZm = IsMaterialNeighbor(i, j, k - 1, side);
        bool nZp = IsMaterialNeighbor(i, j, k + 1, side);

        

        int x0 = i, x1 = i + 1;
        int y0 = j, y1 = j + 1;
        int z0 = k, z1 = k + 1;

        if (!nXm)
        {
            AddEdge(set, x0, y0, z0, x0, y1, z0);
            AddEdge(set, x0, y1, z0, x0, y1, z1);
            AddEdge(set, x0, y1, z1, x0, y0, z1);
            AddEdge(set, x0, y0, z1, x0, y0, z0);
        }
        if (!nXp)
        {
            AddEdge(set, x1, y0, z0, x1, y1, z0);
            AddEdge(set, x1, y1, z0, x1, y1, z1);
            AddEdge(set, x1, y1, z1, x1, y0, z1);
            AddEdge(set, x1, y0, z1, x1, y0, z0);
        }
        if (!nYm)
        {
            AddEdge(set, x0, y0, z0, x1, y0, z0);
            AddEdge(set, x1, y0, z0, x1, y0, z1);
            AddEdge(set, x1, y0, z1, x0, y0, z1);
            AddEdge(set, x0, y0, z1, x0, y0, z0);
        }
        if (!nYp)
        {
            AddEdge(set, x0, y1, z0, x1, y1, z0);
            AddEdge(set, x1, y1, z0, x1, y1, z1);
            AddEdge(set, x1, y1, z1, x0, y1, z1);
            AddEdge(set, x0, y1, z1, x0, y1, z0);
        }
        if (!nZm)
        {
            AddEdge(set, x0, y0, z0, x1, y0, z0);
            AddEdge(set, x1, y0, z0, x1, y1, z0);
            AddEdge(set, x1, y1, z0, x0, y1, z0);
            AddEdge(set, x0, y1, z0, x0, y0, z0);
        }
        if (!nZp)
        {
            AddEdge(set, x0, y0, z1, x1, y0, z1);
            AddEdge(set, x1, y0, z1, x1, y1, z1);
            AddEdge(set, x1, y1, z1, x0, y1, z1);
            AddEdge(set, x0, y1, z1, x0, y0, z1);
        }
    }

    private bool IsMaterialNeighbor(int i, int j, int k, int side)
    {
        if (i < 0 || j < 0 || k < 0) return false;
        if (i >= side || j >= side || k >= side) return false;
        int idx = i * side * side + j * side + k;
        return !_currentLine![idx].IsEmpty;
    }

    private static void AddEdge(
        HashSet<(int, int, int, int, int, int)> set,
        int x1, int y1, int z1,
        int x2, int y2, int z2)
    {
        bool firstSmaller =
            x1 < x2 || (x1 == x2 && (y1 < y2 || (y1 == y2 && z1 <= z2)));
        if (firstSmaller)
        {
            set.Add((x1, y1, z1, x2, y2, z2));
        }
        else
        {
            set.Add((x2, y2, z2, x1, y1, z1));
        }
    }

    private void AddCubeVisual(Cube current, Color color, double opacity, Cube? mapTo = null)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        var box = new BoxVisual3D()
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
            Material = MaterialHelper.CreateMaterial(brush)
        };
        Viewport.Children.Add(box);
        if (mapTo != null)
        {
            _visualToCube[box] = mapTo;
        }
    }
}
