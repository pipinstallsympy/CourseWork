using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CourseWorkZherbin;
using Point = CourseWorkZherbin.Point;
using HelixToolkit.Wpf.SharpDX;
using WPFCourseWork.Controls;
using WPFCourseWork.Rendering;

namespace WPFCourseWork;

public partial class MainWindow : Window
{
    private enum ExportColorTarget
    {
        Material,
        Pore
    }

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
    private Dictionary<Cube, MeshGeometryModel3D>? _poreMeshByCube;
    private LineGeometryModel3D? _percolationWireframe;
    private LineGeometryModel3D? _sampleBoundaryWireframe;
    private readonly List<Element3D> _percolationConnectionLines = new();
    private int _percolationSelectionUpdateVersion;

    private bool IsPercolationViewActive =>
        _percolationTabAvailable
        && MainSideTabControl.SelectedItem == PercolationTabItem;

    private enum CalculationJobType
    {
        CoherencyCheck,
        Percolation,
        ConnectComponents
    }

    private const int CoherencyPartitionWarningThreshold = 48;
    private CancellationTokenSource? _pipelineCts;
    private readonly Queue<CalculationJobType> _jobQueue = new();
    private CalculationJobType? _runningJob;
    private bool _pipelineProcessing;
    private bool _connectPreserveMaterial;
    private int _objectGeneration;
    private ExportColorTarget _activeExportColorTarget = ExportColorTarget.Material;

    public MainWindow()
    {
        InitializeComponent();
        Console.SetOut(new ConsoleLogWriter(AppendLogLine));
        Viewport.EffectsManager = _effectsManager;
        ConfigureViewportChrome();
        ConfigureDefaultCamera();
        _staticSceneItems = BuildStaticSceneItems();
        InitializeStaticScene();
        BringOriginAxesToFront();
        MethodsPanel.SelectionChanged += OnSelectionChanged;
        Viewport.PreviewMouseLeftButtonDown += OnViewport3DClick;
        Viewport.Loaded += (_, _) => ConfigureDefaultCamera();
        UpdateExportColorVisibility();
        ExportModeHintText.Text = ExportWithColorCheckBox.IsChecked == true
            ? "Сохраняются только воксели материала с выбранным цветом (файл MTL создаётся рядом)."
            : "Сохраняются только воксели материала (без пор).";
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
    /// Isometric view of the default unit-cube region before the first generation.
    /// </summary>
    private void ConfigureDefaultCamera()
    {
        Viewport.Camera = HelixSceneBuilder.CreateIsometricCameraForBounds((0, 0, 0, 1, 1, 1));
    }

    private void FrameCameraToLine(CubeLine? line)
    {
        if (line == null || line.Count() == 0)
        {
            ConfigureDefaultCamera();
            return;
        }

        Viewport.Camera = HelixSceneBuilder.CreateIsometricCamera(line);
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

    private void AppendLogLine(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => AppendLogLine(text));
            return;
        }

        LogsTextBox.AppendText(text);
        LogsTextBox.ScrollToEnd();
    }

    private void ClearLogs()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ClearLogs);
            return;
        }

        LogsTextBox.Clear();
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

        ClearLogs();

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
        switch (poreChoice)
        {
            case "По количеству":
                int threshold = partition * partition * partition / 2;
                break;
            case "По процентному соотношению":
                break;
            default:
                MessageBox.Show("При выборе метода созданию пор пошло что-то не так");
                break;
        }
        
        bool flag = CreateFlag(partition, poreChoice, poresValue);
        using (CubeGrid griddy = new CubeGrid(partition, flag))
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
            bool flag = CreateFlag(partition, poreChoice, poresValue);
            using (griddy = new CubeGrid(startCube, partition, flag))
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
            bool flag = CreateFlag(partition, poreChoice, poresValue);
            using (griddy = new CubeGrid(p1, p2, partition, flag))
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


    private bool CreateFlag(int partition, string? poreChoice, double poresValue)
    {
        int cellCount = partition * partition * partition;
        switch (poreChoice)
        {
            case "По количеству":
                return poresValue > cellCount / 2;

            case "По процентному соотношению":
                int targetPores = (int)Math.Ceiling(poresValue / 100.0 * cellCount);
                return targetPores > cellCount / 2;
        }

        return false;
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
        _objectGeneration++;
        CancelPipeline();
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
        FrameCameraToLine(liney);

        if (liney != null)
            StartPipelineWithCoherencyCheck();
    }

    private static int GetPartitionFromLine(CubeLine line) =>
        (int)Math.Round(Math.Cbrt(line.Count()));

    private bool ConfirmLargePartitionConnectOperation(int partition)
    {
        if (partition < CoherencyPartitionWarningThreshold) return true;
        var result = MessageBox.Show(
            $"При разбиении {partition} объединение матрицы материала может занять несколько минут. Продолжить?",
            "Соединение компонентов",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    private void CancelPipeline()
    {
        if (_pipelineCts != null)
        {
            _pipelineCts.Cancel();
            _pipelineCts.Dispose();
            _pipelineCts = null;
        }

        _jobQueue.Clear();
        _runningJob = null;
        _pipelineProcessing = false;
        SetConnectBusy(false);
        SetPercolationBusy(false);
    }

    private void EnsurePipelineStarted()
    {
        if (_pipelineCts == null || _pipelineCts.IsCancellationRequested)
        {
            _pipelineCts?.Dispose();
            _pipelineCts = new CancellationTokenSource();
        }
    }

    private void StartPipelineWithCoherencyCheck()
    {
        EnsurePipelineStarted();
        _jobQueue.Enqueue(CalculationJobType.CoherencyCheck);
        ProcessPipelineAsync();
    }

    private void EnqueueJob(CalculationJobType job)
    {
        if (_runningJob == job || _jobQueue.Contains(job)) return;

        EnsurePipelineStarted();
        _jobQueue.Enqueue(job);
        ProcessPipelineAsync();
    }

    private void SetCoherencyCheckInProgress(bool inProgress)
    {
        if (inProgress)
            StatsCoherencyCount.Text = "Связных компонент: идёт расчёт…";
    }

    private void SetConnectBusy(bool busy)
    {
        ConnectComponentsButton.IsEnabled = !busy;
        if (busy)
            StatsCoherencyCount.Text = "Связных компонент: идёт соединение…";
    }

    private void SetPercolationBusy(bool busy)
    {
        CheckPercolationButton.IsEnabled = !busy;
        if (busy)
            StatsPercolationCount.Text = "Деревьев перколяции: идёт расчёт…";
    }

    private async void ProcessPipelineAsync()
    {
        if (_pipelineProcessing) return;

        _pipelineProcessing = true;
        var generation = _objectGeneration;

        try
        {
            while (_jobQueue.Count > 0
                   && _pipelineCts?.IsCancellationRequested != true
                   && generation == _objectGeneration)
            {
                var job = _jobQueue.Dequeue();
                _runningJob = job;

                try
                {
                    await RunPipelineJobAsync(job, generation);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                finally
                {
                    _runningJob = null;
                }
            }
        }
        finally
        {
            _pipelineProcessing = false;

            if (_jobQueue.Count > 0
                && generation == _objectGeneration
                && _pipelineCts?.IsCancellationRequested != true)
            {
                ProcessPipelineAsync();
            }
        }
    }

    private static (List<TreeNode<Cube>> Trees, Dictionary<Cube, Color> ColorMap, long ElapsedMs) RunCoherencyCheckWork(
        CubeLine line,
        CancellationToken token)
    {
        using var grid = line.GenerateGridFromLine();
        var sw = Stopwatch.StartNew();
        var trees = Coherency.CreateCt(grid, cancellationToken: token);
        var map = BuildColorMap(trees);
        sw.Stop();
        return (trees, map, sw.ElapsedMilliseconds);
    }

    private async Task RunPipelineJobAsync(CalculationJobType job, int generation)
    {
        if (_currentLine == null || _pipelineCts == null) return;

        var token = _pipelineCts.Token;
        var line = _currentLine;

        switch (job)
        {
            case CalculationJobType.CoherencyCheck:
                SetCoherencyCheckInProgress(true);
                try
                {
                    var (trees, colorMap, elapsedMs) = await Task.Run(
                        () => RunCoherencyCheckWork(line, token),
                        token);

                    if (generation != _objectGeneration) return;

                    coherenceTreeList = trees;
                    _cubeColorMap = colorMap;
                    StatsLastCalcTime.Text = $"Время последнего расчёта: {elapsedMs} мс";
                    StatsCoherencyCount.Text = $"Связных компонент: {coherenceTreeList.Count}";
                    RedrawCubes();
                }
                catch (Exception ex) when (ex is not OperationCanceledException && generation == _objectGeneration)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    SetCoherencyCheckInProgress(false);
                }

                break;

            case CalculationJobType.Percolation:
                SetPercolationBusy(true);
                try
                {
                    var (list, elapsedMs) = await Task.Run(
                        () => RunPercolationWork(line, token),
                        token);

                    if (generation != _objectGeneration) return;

                    _permeabilityTreeList = list;
                    ApplyPercolationResults();
                    StatsLastCalcTime.Text = $"Время последнего расчёта: {elapsedMs} мс";
                    if (IsPercolationViewActive) RedrawCubes();
                }
                catch (Exception ex) when (ex is not OperationCanceledException && generation == _objectGeneration)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    if (generation == _objectGeneration)
                        SetPercolationBusy(false);
                }

                break;

            case CalculationJobType.ConnectComponents:
                SetConnectBusy(true);
                try
                {
                    var workResult = await Task.Run(
                        () => RunConnectComponentsWork(line, _connectPreserveMaterial, token),
                        token);

                    if (generation != _objectGeneration) return;

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

                    if (_permeabilityTreeList != null)
                    {
                        SetPercolationBusy(true);
                        try
                        {
                            var (list, percolationMs) = await Task.Run(
                                () => RunPercolationWork(line, token),
                                token);

                            if (generation != _objectGeneration) return;

                            _permeabilityTreeList = list;
                            ApplyPercolationResults();
                            StatsLastCalcTime.Text =
                                $"Время последнего расчёта: {workResult.ElapsedMs} мс (связность), {percolationMs} мс (перколяция)";
                            if (IsPercolationViewActive) RedrawCubes();
                        }
                        finally
                        {
                            if (generation == _objectGeneration)
                                SetPercolationBusy(false);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException && generation == _objectGeneration)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    if (generation == _objectGeneration)
                        SetConnectBusy(false);
                }

                break;
        }
    }

    private void OnColorByComponentChanged(object sender, RoutedEventArgs e)
    {
        if (_currentLine == null) return;
        RedrawCubes();
    }

    private static (PermeabilityTreeList List, long ElapsedMs) RunPercolationWork(
        CubeLine line,
        CancellationToken token)
    {
        using var grid = line.GenerateGridFromLine();
        if (grid.Count() == 0)
            throw new InvalidOperationException("Сетка пуста");

        double fullSideLength = grid[0][0][0].SideLength * grid.Count();
        var sw = Stopwatch.StartNew();
        var list = new PermeabilityTreeList(grid, fullSideLength, token);
        sw.Stop();
        return (list, sw.ElapsedMilliseconds);
    }

    private void ApplyPercolationResults()
    {
        StatsPercolationCount.Text = $"Деревьев перколяции: {_permeabilityTreeList!.TreeList.Count}";
        PopulatePercolationTreeSelector();
        ApplyPercolationView();
        SetPercolationTabAvailable(_permeabilityTreeList.TreeList.Count > 0);
    }

    private void OnCheckPercolation(object sender, RoutedEventArgs e)
    {
        if (_currentLine == null)
        {
            MessageBox.Show("Сначала сгенерируйте объект");
            return;
        }

        if (_runningJob == CalculationJobType.Percolation) return;

        EnqueueJob(CalculationJobType.Percolation);
    }

    private void OnExportModeChanged(object sender, RoutedEventArgs e)
    {
        if (ExportModeHintText == null) return;

        UpdateExportColorVisibility();

        if (ExportCombinedRadio.IsChecked == true)
        {
            ExportModeHintText.Text =
                "Сохраняются материал и поры в одном OBJ с разными цветами (файл MTL создаётся рядом).";
        }
        else if (ExportPoresOnlyRadio.IsChecked == true)
        {
            ExportModeHintText.Text = ExportWithColorCheckBox.IsChecked == true
                ? "Сохраняются только воксели пор с выбранным цветом (файл MTL создаётся рядом)."
                : "Сохраняются только воксели пор (без материала).";
        }
        else
        {
            ExportModeHintText.Text = ExportWithColorCheckBox.IsChecked == true
                ? "Сохраняются только воксели материала с выбранным цветом (файл MTL создаётся рядом)."
                : "Сохраняются только воксели материала (без пор).";
        }
    }

    private void OnExportColorOptionChanged(object sender, RoutedEventArgs e)
    {
        UpdateExportColorVisibility();
        OnExportModeChanged(sender, e);
    }

    private void UpdateExportColorVisibility()
    {
        if (ExportWithColorCheckBox == null || ExportMaterialColorRow == null || ExportPoreColorRow == null)
        {
            return;
        }

        var mode = GetSelectedExportMode();
        bool includeColor = ExportWithColorCheckBox.IsChecked == true;

        ExportWithColorCheckBox.Visibility = mode == ObjExportMode.Combined
            ? Visibility.Collapsed
            : Visibility.Visible;

        ExportMaterialColorRow.Visibility = mode switch
        {
            ObjExportMode.Combined => Visibility.Visible,
            ObjExportMode.MaterialOnly when includeColor => Visibility.Visible,
            _ => Visibility.Collapsed
        };

        ExportPoreColorRow.Visibility = mode switch
        {
            ObjExportMode.Combined => Visibility.Visible,
            ObjExportMode.PoresOnly when includeColor => Visibility.Visible,
            _ => Visibility.Collapsed
        };
    }

    private void OnExportMaterialColorSwatchClick(object sender, MouseButtonEventArgs e)
    {
        _activeExportColorTarget = ExportColorTarget.Material;
        if (ExportColorPickerPopup.IsOpen &&
            ReferenceEquals(ExportColorPickerPopup.PlacementTarget, ExportMaterialColorSwatch))
        {
            ExportColorPickerPopup.IsOpen = false;
            return;
        }

        ExportColorPickerPopup.ShowFor(ExportMaterialColorSwatch, GetExportMaterialColor());
    }

    private void OnExportPoreColorSwatchClick(object sender, MouseButtonEventArgs e)
    {
        _activeExportColorTarget = ExportColorTarget.Pore;
        if (ExportColorPickerPopup.IsOpen &&
            ReferenceEquals(ExportColorPickerPopup.PlacementTarget, ExportPoreColorSwatch))
        {
            ExportColorPickerPopup.IsOpen = false;
            return;
        }

        ExportColorPickerPopup.ShowFor(ExportPoreColorSwatch, GetExportPoreColor());
    }

    private void OnExportColorPickerColorChanged(object sender, Color color)
    {
        ApplyExportColor(_activeExportColorTarget, color, updateHex: true);
    }

    private void OnExportMaterialColorHexLostFocus(object sender, RoutedEventArgs e)
    {
        TryApplyExportColorFromHex(ExportColorTarget.Material, ExportMaterialColorHex.Text);
    }

    private void OnExportPoreColorHexLostFocus(object sender, RoutedEventArgs e)
    {
        TryApplyExportColorFromHex(ExportColorTarget.Pore, ExportPoreColorHex.Text);
    }

    private void OnExportColorHexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
        {
            return;
        }

        var target = ReferenceEquals(textBox, ExportMaterialColorHex)
            ? ExportColorTarget.Material
            : ExportColorTarget.Pore;
        TryApplyExportColorFromHex(target, textBox.Text);
        e.Handled = true;
    }

    private Color GetExportMaterialColor()
    {
        return ColorUtils.TryParseHexColor(ExportMaterialColorHex.Text, out Color color)
            ? color
            : Color.FromRgb(0xCC, 0x22, 0x22);
    }

    private Color GetExportPoreColor()
    {
        return ColorUtils.TryParseHexColor(ExportPoreColorHex.Text, out Color color)
            ? color
            : Color.FromRgb(0x8F, 0xBC, 0x8F);
    }

    private void ApplyExportColor(ExportColorTarget target, Color color, bool updateHex)
    {
        if (target == ExportColorTarget.Material)
        {
            ExportMaterialColorSwatch.Background = new SolidColorBrush(color);
            if (updateHex)
            {
                ExportMaterialColorHex.Text = ColorUtils.ColorToHex(color);
            }

            SetHexBoxValid(ExportMaterialColorHex, isValid: true);
            return;
        }

        ExportPoreColorSwatch.Background = new SolidColorBrush(color);
        if (updateHex)
        {
            ExportPoreColorHex.Text = ColorUtils.ColorToHex(color);
        }

        SetHexBoxValid(ExportPoreColorHex, isValid: true);
    }

    private void TryApplyExportColorFromHex(ExportColorTarget target, string hex)
    {
        if (!ColorUtils.TryParseHexColor(hex, out Color color))
        {
            var textBox = target == ExportColorTarget.Material
                ? ExportMaterialColorHex
                : ExportPoreColorHex;
            SetHexBoxValid(textBox, isValid: false);
            return;
        }

        ApplyExportColor(target, color, updateHex: true);
    }

    private static void SetHexBoxValid(TextBox textBox, bool isValid)
    {
        textBox.BorderBrush = isValid
            ? SystemColors.ControlDarkBrush
            : Brushes.IndianRed;
    }

    private bool TryBuildExportSettings(out ObjExportSettings settings)
    {
        settings = default!;
        var mode = GetSelectedExportMode();
        bool includeColor = ExportWithColorCheckBox.IsChecked == true;

        if (!ObjColor.TryParseHex(ExportMaterialColorHex.Text, out ObjColor materialColor))
        {
            if (mode is ObjExportMode.MaterialOnly or ObjExportMode.Combined &&
                (mode == ObjExportMode.Combined || includeColor))
            {
                MessageBox.Show("Некорректный hex-цвет материала.", "Сохранение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                SetHexBoxValid(ExportMaterialColorHex, isValid: false);
                return false;
            }

            materialColor = ObjColor.DefaultMaterial;
        }

        if (!ObjColor.TryParseHex(ExportPoreColorHex.Text, out ObjColor poreColor))
        {
            if (mode is ObjExportMode.PoresOnly or ObjExportMode.Combined &&
                (mode == ObjExportMode.Combined || includeColor))
            {
                MessageBox.Show("Некорректный hex-цвет пор.", "Сохранение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                SetHexBoxValid(ExportPoreColorHex, isValid: false);
                return false;
            }

            poreColor = ObjColor.DefaultPore;
        }

        settings = new ObjExportSettings(
            mode,
            materialColor,
            poreColor,
            IncludeMaterialColor: mode == ObjExportMode.Combined ||
                                  (mode == ObjExportMode.MaterialOnly && includeColor),
            IncludePoreColor: mode == ObjExportMode.Combined ||
                              (mode == ObjExportMode.PoresOnly && includeColor));

        return true;
    }

    private ObjExportMode GetSelectedExportMode()
    {
        if (ExportPoresOnlyRadio.IsChecked == true)
        {
            return ObjExportMode.PoresOnly;
        }

        if (ExportCombinedRadio.IsChecked == true)
        {
            return ObjExportMode.Combined;
        }

        return ObjExportMode.MaterialOnly;
    }

    private void OnSaveObj(object sender, RoutedEventArgs e)
    {
        if (_currentLine == null)
        {
            MessageBox.Show("Сначала сгенерируйте объект", "Сохранение",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var mode = GetSelectedExportMode();
        var (dialogTitle, defaultFileName) = mode switch
        {
            ObjExportMode.PoresOnly => ("Сохранить поры как OBJ", "pores.obj"),
            ObjExportMode.Combined => ("Сохранить материал и поры как OBJ", "combined.obj"),
            _ => ("Сохранить материал как OBJ", "material.obj")
        };

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = dialogTitle,
            Filter = "Wavefront OBJ (*.obj)|*.obj",
            DefaultExt = "obj",
            FileName = defaultFileName,
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true) return;

        if (!TryBuildExportSettings(out ObjExportSettings settings))
        {
            return;
        }

        try
        {
            CourseWorkZherbin.ObjExporter.Export(_currentLine, dialog.FileName, settings);

            string successMessage = settings.WritesMtlFile
                ? $"Файлы сохранены:\n{dialog.FileName}\n{Path.ChangeExtension(dialog.FileName, ".mtl")}\n\n" +
                  "Импорт в Blender:\n" +
                  "• File → Import → Wavefront (.obj)\n" +
                  "• Выберите только файл .obj (не .mtl)\n" +
                  "• Файл .mtl должен лежать в той же папке\n" +
                  "• Name Collision: Replace (или новая сцена)"
                : $"Файл сохранён:\n{dialog.FileName}";

            MessageBox.Show(successMessage, "Сохранение",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка сохранения",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
                    SchedulePercolationSelectionUpdate();
                }
                e.Handled = true;
                return;
            }
        }

        if (_selectedBoundaryPore != null)
        {
            _selectedBoundaryPore = null;
            SchedulePercolationSelectionUpdate();
        }
    }

    private void SchedulePercolationSelectionUpdate()
    {
        int version = ++_percolationSelectionUpdateVersion;
        Dispatcher.BeginInvoke(() =>
        {
            if (version != _percolationSelectionUpdateVersion) return;
            if (!IsPercolationViewActive || _poreMeshByCube == null) return;
            UpdatePercolationSelection();
        }, DispatcherPriority.Render);
    }

    private void ClearPercolationConnectionLines()
    {
        foreach (var line in _percolationConnectionLines)
        {
            Viewport.Items.Remove(line);
            _dynamicSceneItems.Remove(line);
        }
        _percolationConnectionLines.Clear();
    }

    private void ClearPercolationScene()
    {
        if (_percolationWireframe != null)
        {
            Viewport.Items.Remove(_percolationWireframe);
            _dynamicSceneItems.Remove(_percolationWireframe);
            _percolationWireframe = null;
        }

        if (_sampleBoundaryWireframe != null)
        {
            Viewport.Items.Remove(_sampleBoundaryWireframe);
            _dynamicSceneItems.Remove(_sampleBoundaryWireframe);
            _sampleBoundaryWireframe = null;
        }

        if (_poreMeshByCube != null)
        {
            foreach (var mesh in _poreMeshByCube.Values)
            {
                Viewport.Items.Remove(mesh);
                _dynamicSceneItems.Remove(mesh);
            }
            _poreMeshByCube = null;
        }

        ClearPercolationConnectionLines();
        _percolationSelectionUpdateVersion++;
    }

    private void RebuildPercolationScene()
    {
        if (_currentLine == null) return;

        var visiblePores = GetVisiblePercolationPores();
        if (visiblePores == null) return;

        ClearPercolationScene();

        _sampleBoundaryWireframe = HelixSceneBuilder.BuildSampleBoundaryWireframe(_currentLine);
        if (_sampleBoundaryWireframe != null)
            AddDynamicSceneItem(_sampleBoundaryWireframe);

      

        _poreMeshByCube = new Dictionary<Cube, MeshGeometryModel3D>();
        foreach (var cube in visiblePores)
        {
            (Color color, double opacity) = ResolveBoundaryPoreAppearance(cube);
            var mesh = HelixSceneBuilder.BuildPoreMesh(cube, color, opacity);
            _poreMeshByCube[cube] = mesh;
            AddDynamicSceneItem(mesh);
        }

        AddPercolationConnectionLines();
        BringOriginAxesToFront();
    }

    private void UpdatePercolationSelection()
    {
        if (_poreMeshByCube == null) return;

        var visiblePores = GetVisiblePercolationPores();
        if (visiblePores == null) return;

        foreach (var cube in visiblePores)
        {
            if (!_poreMeshByCube.TryGetValue(cube, out var mesh)) continue;
            (Color color, double opacity) = ResolveBoundaryPoreAppearance(cube);
            HelixSceneBuilder.ApplyPoreAppearance(mesh, color, opacity);
        }

        ClearPercolationConnectionLines();
        AddPercolationConnectionLines();
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

    private void OnConnectComponents(object sender, RoutedEventArgs e)
    {
        if (_currentLine == null)
        {
            MessageBox.Show("Сначала сгенерируйте объект");
            return;
        }

        if (_runningJob == CalculationJobType.ConnectComponents) return;

        int partition = GetPartitionFromLine(_currentLine);
        if (!ConfirmLargePartitionConnectOperation(partition)) return;

        _connectPreserveMaterial = PreserveMaterialCheckBox?.IsChecked == true;
        EnqueueJob(CalculationJobType.ConnectComponents);
    }

    private static ConnectComponentsWorkResult RunConnectComponentsWork(
        CubeLine line,
        bool preserveMaterial,
        CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        using var grid = line.GenerateGridFromLine();
        var components = Coherency.CreateCt(grid, cancellationToken: token);

        if (components.Count <= 1)
        {
            sw.Stop();
            return new ConnectComponentsWorkResult
            {
                AlreadyConnected = true,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }

        var bridges = new CoherencyConnector().ConnectAll(grid, components, token);

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
            int removed = RemoveLeavesCascade(components, forbidden, bridgeCubesCount, token);
            leavesShortage = bridgeCubesCount - removed;
        }

        foreach (var cube in distinctBridgeCubes)
        {
            if (cube.IsEmpty) cube.IsEmpty = false;
        }

        using var gridAfter = line.GenerateGridFromLine();
        var trees = Coherency.CreateCt(gridAfter, cancellationToken: token);
        var colorMap = BuildColorMap(trees);
        sw.Stop();

        return new ConnectComponentsWorkResult
        {
            Trees = trees,
            ColorMap = colorMap,
            ElapsedMs = sw.ElapsedMilliseconds,
            LeavesShortage = leavesShortage
        };
    }

    private static int RemoveLeavesCascade(
        List<TreeNode<Cube>> components,
        HashSet<Cube> forbidden,
        int target,
        CancellationToken cancellationToken = default)
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
            cancellationToken.ThrowIfCancellationRequested();
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
        if (IsPercolationViewActive && GetVisiblePercolationPores() != null)
        {
            ClearDynamicSceneItems();
            RebuildPercolationScene();
            return;
        }

        ClearPercolationScene();
        ClearDynamicSceneItems();

        if (_currentLine == null) return;

        bool useComponentColors = ColorComponentsCheckBox.IsChecked == true
                                  && _cubeColorMap != null
                                  && _cubeColorMap.Count > 0;

        foreach (var mesh in HelixSceneBuilder.BuildBatchedMaterialMeshes(
                     _currentLine, useComponentColors, _cubeColorMap))
        {
            AddDynamicSceneItem(mesh);
        }

        BringOriginAxesToFront();
    }

    private void AddPercolationConnectionLines()
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
            AddPercolationConnectionLine(endToEndLines);

        var partialLines = HelixSceneBuilder.BuildConnectionLines(partialPairs, Colors.Gold);
        if (partialLines != null)
            AddPercolationConnectionLine(partialLines);
    }

    private void AddPercolationConnectionLine(Element3D line)
    {
        AddDynamicSceneItem(line);
        _percolationConnectionLines.Add(line);
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
