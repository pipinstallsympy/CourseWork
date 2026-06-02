using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CourseWorkZherbin;
using Point = CourseWorkZherbin.Point;
using HelixToolkit.Wpf.SharpDX;
using WPFCourseWork.Rendering;

namespace WPFCourseWork;

public partial class MainWindow : Window
{
    private static List<TreeNode<Cube>> coherenceTreeList = new List<TreeNode<Cube>>();

    private readonly IEffectsManager _effectsManager = new DefaultEffectsManager();
    private readonly IReadOnlyList<Element3D> _sceneLights = HelixSceneBuilder.CreateDefaultSceneLights();
    private readonly IReadOnlyList<Element3D> _staticSceneItems;
    private readonly IReadOnlyList<MeshGeometryModel3D> _originAxes = HelixSceneBuilder.CreateOriginUnitAxes();
    private readonly List<Element3D> _dynamicSceneItems = new();

    private CubeLine? _currentLine;
    private Dictionary<Cube, Color>? _cubeColorMap;
    private PermeabilityTreeList? _permeabilityTreeList;
    private HashSet<Cube>? _percolationAllTreePores;
    private HashSet<Cube>? _percolationEdgePores;
    private Cube? _selectedBoundaryPore;
    private Dictionary<Cube, HashSet<Cube>>? _partialPeersByCube;
    private Dictionary<Cube, HashSet<Cube>>? _endToEndPeersByCube;
    private int _selectedPercolationTreeIndex = -1;
    private bool _updatingPercolationSelector;
    private bool _percolationTabAvailable;

    private bool IsPercolationViewActive =>
        _percolationTabAvailable
        && MainSideTabControl.SelectedItem == PercolationTabItem;

    private const int CoherencyPartitionWarningThreshold = 48;
    private const int MaxPercolationLinesToDraw = 500;
    private bool _coherencyCalculationInProgress;
    private bool _updatingPercolationLinesCheckBox;

    public MainWindow()
    {
        InitializeComponent();
        Viewport.EffectsManager = _effectsManager;
        ConfigureViewportChrome();
        ConfigureTopDownCamera();
        _staticSceneItems = BuildStaticSceneItems();
        InitializeStaticScene();
        BringOriginAxesToFront();
        MethodsPanel.SelectionChanged += OnSelectionChanged;
        Viewport.PreviewMouseLeftButtonDown += OnViewport3DClick;
        Viewport.Loaded += (_, _) => ConfigureTopDownCamera();
    }

    private void ConfigureViewportChrome()
    {
        Viewport.ShowViewCube = false;
        Viewport.ShowCoordinateSystem = true;
        Viewport.CoordinateSystemHorizontalPosition = 0.82;
        Viewport.CoordinateSystemVerticalPosition = -0.82;
        Viewport.CoordinateSystemSize = 120;
    }

    /// <summary>
    /// Top-down view along -Y onto the X–Z grid and origin axes at (0, 0, 0).
    /// </summary>
    private void ConfigureTopDownCamera()
    {
        const double distance = 3.5;
        Viewport.Camera = new PerspectiveCamera
        {
            Position = new System.Windows.Media.Media3D.Point3D(0, distance, 0),
            LookDirection = new System.Windows.Media.Media3D.Vector3D(0, -distance, 0),
            UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 0, -1),
            FieldOfView = 50
        };
    }

    private IReadOnlyList<Element3D> BuildStaticSceneItems()
    {
        var items = new List<Element3D>();
        items.AddRange(_sceneLights);
        items.Add(HelixSceneBuilder.CreateZxPlaneGrid());
        return items;
    }

    private void BringOriginAxesToFront()
    {
        foreach (var axis in _originAxes)
        {
            if (Viewport.Items.Contains(axis))
            {
                Viewport.Items.Remove(axis);
            }
            Viewport.Items.Add(axis);
        }
    }

    private void InitializeStaticScene()
    {
        foreach (var item in _staticSceneItems)
        {
            Viewport.Items.Add(item);
        }
    }

    private void ClearDynamicSceneItems()
    {
        foreach (var item in _dynamicSceneItems)
        {
            Viewport.Items.Remove(item);
        }
        _dynamicSceneItems.Clear();
    }

    private void AddDynamicSceneItem(Element3D item)
    {
        Viewport.Items.Add(item);
        _dynamicSceneItems.Add(item);
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
        catch (Exception e)
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
        StatsPoreCount.Text = $"Узлов пор: {pores}";
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
        _percolationAllTreePores = null;
        _percolationEdgePores = null;
        _partialPeersByCube = null;
        _endToEndPeersByCube = null;
        _selectedBoundaryPore = null;
        _selectedPercolationTreeIndex = -1;
        StatsPercolationCount.Text = "Деревьев перколяции: -";
        SetPercolationTabAvailable(false);
    }

    private static int GetPartitionFromLine(CubeLine line) =>
        (int)Math.Round(Math.Cbrt(line.Count()));

    private bool ConfirmLargePartitionOperation(int partition, bool connectComponents)
    {
        if (partition < CoherencyPartitionWarningThreshold) return true;
        string message = connectComponents
            ? $"При разбиении {partition} объединение матрицы материала может занять несколько минут. Продолжить?"
            : $"При разбиении {partition} расчёт связности может занять несколько минут. Продолжить?";
        string title = connectComponents ? "Соединение компонентов" : "Проверка связности";
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    private void SetCoherencyBusy(bool busy)
    {
        _coherencyCalculationInProgress = busy;
        CheckCoherencyButton.IsEnabled = !busy;
        ConnectComponentsButton.IsEnabled = !busy;
        if (busy)
            StatsCoherencyCount.Text = "Связных компонент: идёт расчёт…";
    }

    private async void OnCheckCoherency(object sender, RoutedEventArgs e)
    {
        if (_currentLine == null)
        {
            MessageBox.Show("Сначала сгенерируйте объект");
            return;
        }

        if (_coherencyCalculationInProgress) return;

        int partition = GetPartitionFromLine(_currentLine);
        if (!ConfirmLargePartitionOperation(partition, connectComponents: false)) return;

        var line = _currentLine;
        SetCoherencyBusy(true);
        try
        {
            var (trees, colorMap, elapsedMs) = await Task.Run(() =>
            {
                using var grid = line.GenerateGridFromLine();
                var sw = Stopwatch.StartNew();
                var resultTrees = new Coherency().CreateCT(grid);
                var map = BuildColorMap(resultTrees);
                sw.Stop();
                return (resultTrees, map, sw.ElapsedMilliseconds);
            });

            coherenceTreeList = trees;
            _cubeColorMap = colorMap;
            StatsLastCalcTime.Text = $"Время последнего расчёта: {elapsedMs} мс";
            StatsCoherencyCount.Text = $"Связных компонент: {coherenceTreeList.Count}";
            RedrawCubes();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
        finally
        {
            SetCoherencyBusy(false);
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
            sw.Stop();

            StatsPercolationCount.Text = $"Деревьев перколяции: {_permeabilityTreeList.TreeList.Count}";
            StatsLastCalcTime.Text = $"Время последнего расчёта: {sw.ElapsedMilliseconds} мс";

            PopulatePercolationTreeSelector();
            ApplyPercolationView();
            SetPercolationTabAvailable(_permeabilityTreeList.TreeList.Count > 0);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private void SetPercolationTabAvailable(bool available)
    {
        _percolationTabAvailable = available;
        PercolationTabItem.IsEnabled = available;

        if (available) return;

        MainSideTabControl.SelectedItem = ObjectTabItem;
        _updatingPercolationSelector = true;
        try
        {
            PercolationTreeSelector.Items.Clear();
        }
        finally
        {
            _updatingPercolationSelector = false;
        }

        _selectedPercolationTreeIndex = -1;
        _percolationAllTreePores = null;
        _percolationEdgePores = null;
        _partialPeersByCube = null;
        _endToEndPeersByCube = null;
        _selectedBoundaryPore = null;
        _updatingPercolationLinesCheckBox = true;
        try
        {
            ShowEndToEndConnectionsCheckBox.IsChecked = false;
            ShowPartialConnectionsCheckBox.IsChecked = false;
        }
        finally
        {
            _updatingPercolationLinesCheckBox = false;
        }
        UpdatePercolationTreeStats();

        if (_currentLine != null)
            RedrawCubes();
    }

    private void OnMainSideTabChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedBoundaryPore = null;
        if (_currentLine == null) return;
        RedrawCubes();
    }

    private void PopulatePercolationTreeSelector()
    {
        _updatingPercolationSelector = true;
        try
        {
            PercolationTreeSelector.Items.Clear();
            if (_permeabilityTreeList == null || _permeabilityTreeList.TreeList.Count == 0)
            {
                _selectedPercolationTreeIndex = -1;
                UpdatePercolationTreeStats();
                return;
            }

            for (int i = 0; i < _permeabilityTreeList.TreeList.Count; i++)
                PercolationTreeSelector.Items.Add(_permeabilityTreeList.TreeList[i].FormatSelectorLabel(i));

            PercolationTreeSelector.SelectedIndex = 0;
            _selectedPercolationTreeIndex = 0;
            UpdatePercolationTreeStats();
        }
        finally
        {
            _updatingPercolationSelector = false;
        }
    }

    private void UpdatePercolationTreeStats()
    {
        if (_permeabilityTreeList == null
            || _selectedPercolationTreeIndex < 0
            || _selectedPercolationTreeIndex >= _permeabilityTreeList.TreeList.Count)
        {
            PercolationStatsPoreCount.Text = "Кол-во пор: -";
            PercolationStatsBoundaryPores.Text = "Кол-во пограничных пор: -";
            PercolationStatsEndToEnd.Text = "Пар сквозной перколяции: -";
            PercolationStatsPartial.Text = "Пар частичной перколяции: -";
            PercolationStatsHasThrough.Text = "Сквозная перколяция: -";
            PercolationStatsTreeDepth.Text = "Глубина октодерева: -";
            return;
        }

        var tree = _permeabilityTreeList.TreeList[_selectedPercolationTreeIndex];
        PercolationStatsPoreCount.Text = $"Кол-во пор: {tree.CountPoresInSubtree()}";
        PercolationStatsBoundaryPores.Text = $"Кол-во пограничных пор: {tree.BoundaryPoreCount}";
        PercolationStatsEndToEnd.Text = $"Пар сквозной перколяции: {tree.EndToEndPairCount}";
        PercolationStatsPartial.Text = $"Пар частичной перколяции: {tree.PartialPairCount}";
        PercolationStatsHasThrough.Text = tree.HasEndToEndPercolation
            ? "Сквозная перколяция: да"
            : "Сквозная перколяция: нет";
        PercolationStatsTreeDepth.Text = $"Глубина октодерева: {tree.TreeDepth}";
    }

    private void ApplyPercolationView()
    {
        if (_permeabilityTreeList == null
            || _selectedPercolationTreeIndex < 0
            || _selectedPercolationTreeIndex >= _permeabilityTreeList.TreeList.Count)
        {
            return;
        }

        var tree = _permeabilityTreeList.TreeList[_selectedPercolationTreeIndex];

        _percolationAllTreePores = new HashSet<Cube>();
        _percolationEdgePores = new HashSet<Cube>();
        tree.CollectAllPoresInSubtree(_percolationAllTreePores);
        tree.CollectBoundaryPores(_percolationEdgePores);

        var dict = new PermeabilityDictionary(tree);
        (_partialPeersByCube, _endToEndPeersByCube) = BuildPercolationIndex(dict);
        _selectedBoundaryPore = null;

        if (IsPercolationViewActive && _currentLine != null)
            RedrawCubes();
    }

    private void OnPercolationDisplayModeChanged(object sender, RoutedEventArgs e)
    {
        if (_percolationAllTreePores == null || _percolationEdgePores == null) return;
        _selectedBoundaryPore = null;
        if (_currentLine != null)
            RedrawCubes();
    }

    private void OnPercolationConnectionLinesChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingPercolationLinesCheckBox) return;
        if (_percolationAllTreePores == null) return;

        if (sender is CheckBox { IsChecked: true } cb
            && _permeabilityTreeList != null
            && _selectedPercolationTreeIndex >= 0
            && _selectedPercolationTreeIndex < _permeabilityTreeList.TreeList.Count)
        {
            var tree = _permeabilityTreeList.TreeList[_selectedPercolationTreeIndex];
            int linesToAdd = cb == ShowEndToEndConnectionsCheckBox
                ? tree.EndToEndPairCount
                : tree.PartialPairCount;
            if (linesToAdd > MaxPercolationLinesToDraw)
            {
                _updatingPercolationLinesCheckBox = true;
                try
                {
                    cb.IsChecked = false;
                }
                finally
                {
                    _updatingPercolationLinesCheckBox = false;
                }

                MessageBox.Show(
                    $"Слишком много связей ({linesToAdd}). Используйте клик по поре или уменьшите сетку.",
                    "Перколяция",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        if (_currentLine != null)
            RedrawCubes();
    }

    private HashSet<Cube>? GetVisiblePercolationPores()
    {
        if (ShowFullPercolationTreeRadio.IsChecked == true)
            return _percolationAllTreePores;
        return _percolationEdgePores;
    }

    private void OnPercolationTreeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPercolationSelector) return;
        if (PercolationTreeSelector.Items.Count == 0) return;
        if (PercolationTreeSelector.SelectedIndex < 0) return;

        _selectedPercolationTreeIndex = PercolationTreeSelector.SelectedIndex;
        UpdatePercolationTreeStats();
        ApplyPercolationView();
    }

    private void OnViewport3DClick(object sender, MouseButtonEventArgs e)
    {
        if (!IsPercolationViewActive) return;
        var visiblePores = GetVisiblePercolationPores();
        if (visiblePores == null) return;

        var pt = e.GetPosition(Viewport);
        var hits = Viewport.FindHits(pt);
        foreach (var hit in hits)
        {
            if (hit.ModelHit is MeshGeometryModel3D { Tag: Cube cube })
            {
                if (!visiblePores.Contains(cube)) continue;
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

    private static (Dictionary<Cube, HashSet<Cube>> partialPeers,
                    Dictionary<Cube, HashSet<Cube>> endToEndPeers)
        BuildPercolationIndex(PermeabilityDictionary dict)
    {
        var partial = new Dictionary<Cube, HashSet<Cube>>();
        var endToEnd = new Dictionary<Cube, HashSet<Cube>>();

        AccumulatePeers(dict.PartialPermeability, partial);
        AccumulatePeers(dict.EndToEndPermeability, endToEnd);

        return (partial, endToEnd);
    }

    private static void AccumulatePeers(
        Dictionary<TreeNode<Cube>, List<TreeNode<Cube>>> source,
        Dictionary<Cube, HashSet<Cube>> peers)
    {
        foreach (var kvp in source)
        {
            Cube a = kvp.Key.Value;
            if (!peers.TryGetValue(a, out var listA))
            {
                listA = new HashSet<Cube>();
                peers[a] = listA;
            }
            foreach (var node in kvp.Value)
            {
                Cube b = node.Value;
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

    private sealed class ConnectComponentsWorkResult
    {
        public bool AlreadyConnected { get; init; }
        public List<TreeNode<Cube>> Trees { get; init; } = new();
        public Dictionary<Cube, Color> ColorMap { get; init; } = new();
        public long ElapsedMs { get; init; }
        public int LeavesShortage { get; init; }
    }

    private async void OnConnectComponents(object sender, RoutedEventArgs e)
    {
        if (_currentLine == null)
        {
            MessageBox.Show("Сначала сгенерируйте объект");
            return;
        }

        if (_coherencyCalculationInProgress) return;

        int partition = GetPartitionFromLine(_currentLine);
        if (!ConfirmLargePartitionOperation(partition, connectComponents: true)) return;

        var line = _currentLine;
        bool preserveMaterial = PreserveMaterialCheckBox?.IsChecked == true;
        SetCoherencyBusy(true);
        try
        {
            var workResult = await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                using var grid = line.GenerateGridFromLine();
                var components = new Coherency().CreateCT(grid);

                if (components.Count <= 1)
                {
                    sw.Stop();
                    return new ConnectComponentsWorkResult
                    {
                        AlreadyConnected = true,
                        ElapsedMs = sw.ElapsedMilliseconds
                    };
                }

                var bridges = new CoherencyConnector().ConnectAll(grid, components);

                var distinctBridgeCubes = new HashSet<Cube>();
                foreach (var b in bridges)
                {
                    foreach (var c in b.EmptyCubesToFill)
                        distinctBridgeCubes.Add(c);
                }

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

                using var gridAfter = line.GenerateGridFromLine();
                var trees = new Coherency().CreateCT(gridAfter);
                var colorMap = BuildColorMap(trees);
                sw.Stop();

                return new ConnectComponentsWorkResult
                {
                    Trees = trees,
                    ColorMap = colorMap,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    LeavesShortage = leavesShortage
                };
            });

            if (workResult.AlreadyConnected)
            {
                MessageBox.Show("Все узлы материала уже связаны.");
                StatsLastCalcTime.Text = $"Время последнего расчёта: {workResult.ElapsedMs} мс";
                StatsCoherencyCount.Text = coherenceTreeList.Count > 0
                    ? $"Связных компонент: {coherenceTreeList.Count}"
                    : "Связных компонент: -";
                return;
            }

            coherenceTreeList = workResult.Trees;
            _cubeColorMap = workResult.ColorMap;
            UpdateNodeStats(line);
            StatsCoherencyCount.Text = $"Связных компонент: {coherenceTreeList.Count}";
            StatsLastCalcTime.Text = $"Время последнего расчёта: {workResult.ElapsedMs} мс";
            RedrawCubes();

            if (workResult.LeavesShortage > 0)
            {
                MessageBox.Show(
                    $"Не хватило листовых узлов материала для полного сохранения количества: " +
                    $"{workResult.LeavesShortage} соединяющих узлов добавлены без компенсации.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
        finally
        {
            SetCoherencyBusy(false);
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
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    private void RedrawCubes()
    {
        ClearDynamicSceneItems();

        if (_currentLine == null) return;

        bool useComponentColors = ColorComponentsCheckBox.IsChecked == true
                                  && _cubeColorMap != null
                                  && _cubeColorMap.Count > 0;

        var visiblePercolationPores = GetVisiblePercolationPores();
        bool showPercolation = IsPercolationViewActive
                               && visiblePercolationPores != null;

        int len = _currentLine.Count();
        int side = (int)Math.Round(Math.Cbrt(len));
        HashSet<(int, int, int, int, int, int)>? uniqueEdges =
            showPercolation ? new HashSet<(int, int, int, int, int, int)>() : null;

        var deferredTransparent = showPercolation
            ? new List<(Cube cube, Color color, double opacity)>()
            : null;
        var deferredOpaque = showPercolation
            ? new List<(Cube cube, Color color, double opacity)>()
            : null;

        for (int idx = 0; idx < len; idx++)
        {
            var current = _currentLine[idx];

            if (current.IsEmpty)
            {
                if (!showPercolation) continue;
                if (!visiblePercolationPores!.Contains(current)) continue;

                (Color color, double opacity) = ResolveBoundaryPoreAppearance(current);
                if (opacity >= 1.0)
                {
                    deferredOpaque!.Add((current, color, opacity));
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
        }

        if (!showPercolation)
        {
            foreach (var mesh in HelixSceneBuilder.BuildBatchedMaterialMeshes(
                         _currentLine, useComponentColors, _cubeColorMap))
            {
                AddDynamicSceneItem(mesh);
            }
        }
        else if (uniqueEdges!.Count > 0)
        {
            var wire = HelixSceneBuilder.BuildMaterialWireframe(uniqueEdges, _currentLine[0]);
            if (wire != null)
            {
                AddDynamicSceneItem(wire);
            }
        }

        if (showPercolation)
            AddPercolationConnectionLines();

        if (deferredTransparent != null)
        {
            foreach (var (cube, color, opacity) in deferredTransparent)
            {
                AddDynamicSceneItem(HelixSceneBuilder.BuildPoreMesh(cube, color, opacity));
            }
        }

        if (deferredOpaque != null)
        {
            foreach (var (cube, color, opacity) in deferredOpaque)
            {
                AddDynamicSceneItem(HelixSceneBuilder.BuildPoreMesh(cube, color, opacity));
            }
        }

        BringOriginAxesToFront();
    }

    private void AddPercolationConnectionLines()
    {
        if (_permeabilityTreeList == null
            || _selectedPercolationTreeIndex < 0
            || _selectedPercolationTreeIndex >= _permeabilityTreeList.TreeList.Count)
        {
            return;
        }

        var tree = _permeabilityTreeList.TreeList[_selectedPercolationTreeIndex];
        bool showAllEndToEnd = ShowEndToEndConnectionsCheckBox.IsChecked == true;
        bool showAllPartial = ShowPartialConnectionsCheckBox.IsChecked == true;

        if (showAllEndToEnd)
        {
            var endToEndLines = HelixSceneBuilder.BuildConnectionLines(
                PermeabilityTree.EnumerateUniquePairs(tree.EndToEndPermeability),
                Colors.DodgerBlue);
            if (endToEndLines != null)
                AddDynamicSceneItem(endToEndLines);
        }

        if (showAllPartial)
        {
            var partialLines = HelixSceneBuilder.BuildConnectionLines(
                PermeabilityTree.EnumerateUniquePairs(tree.PartialPermeability),
                Colors.Gold);
            if (partialLines != null)
                AddDynamicSceneItem(partialLines);
        }

        if (!showAllEndToEnd && !showAllPartial && _selectedBoundaryPore != null)
            AddSelectedPoreConnectionLines();
    }

    private void AddSelectedPoreConnectionLines()
    {
        if (_selectedBoundaryPore == null) return;

        var endToEndPairs = new List<(Cube from, Cube to)>();
        var partialPairs = new List<(Cube from, Cube to)>();

        if (_endToEndPeersByCube != null
            && _endToEndPeersByCube.TryGetValue(_selectedBoundaryPore, out var endToEndPeers))
        {
            foreach (var peer in endToEndPeers)
                endToEndPairs.Add((_selectedBoundaryPore, peer));
        }

        if (_partialPeersByCube != null
            && _partialPeersByCube.TryGetValue(_selectedBoundaryPore, out var partialPeers))
        {
            foreach (var peer in partialPeers)
                partialPairs.Add((_selectedBoundaryPore, peer));
        }

        var endToEndLines = HelixSceneBuilder.BuildConnectionLines(endToEndPairs, Colors.DodgerBlue);
        if (endToEndLines != null)
            AddDynamicSceneItem(endToEndLines);

        var partialLines = HelixSceneBuilder.BuildConnectionLines(partialPairs, Colors.Gold);
        if (partialLines != null)
            AddDynamicSceneItem(partialLines);
    }

    private (Color color, double opacity) ResolveBoundaryPoreAppearance(Cube cube)
    {
        if (_selectedBoundaryPore == null)
        {
            bool isBoundary = _percolationEdgePores != null && _percolationEdgePores.Contains(cube);
            return isBoundary ? (Colors.LimeGreen, 1.0) : (Colors.DarkSeaGreen, 1.0);
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
}
