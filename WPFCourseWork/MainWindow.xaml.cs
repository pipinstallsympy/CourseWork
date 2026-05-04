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
            InitializeCube(ref liney);
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
                InitializeCube(ref liney);
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
                InitializeCube(ref liney);
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
            CreateCoherenceList(liney.GenerateGridFromLine());
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
        }
    }
    
    private void CreateCoherenceList(CubeGrid g)
    {
        Coherency coherency = new Coherency();
        coherenceTreeList = coherency.CreateCT(g);

        MessageBox.Show($"Cohenerency count: {coherenceTreeList.Count}");
    }


    private void InitializeCube(ref CubeLine? liney)
    {
        Viewport.Children.Clear();
        GC.Collect(2);
        Viewport.Children.Add(gridLines);
        Viewport.Children.Add(coordinateSystem);
        Viewport.Children.Add(new DefaultLights());

        int len = liney.Count();

        for (int i = 0; i < len; i++)
        {
            if(liney[i].IsEmpty) continue;
            var cube = new BoxVisual3D()
            {
                Center = new Point3D()
                {
                    X = liney[i].CentralPoint.X,
                    Y = liney[i].CentralPoint.Y,
                    Z = liney[i].CentralPoint.Z
                },
                Width = liney[i].SideLength,
                Height = liney[i].SideLength,
                Length = liney[i].SideLength,
                Material = MaterialHelper.CreateMaterial(Colors.Red)
            };
            Viewport.Children.Add(cube);
        }
    }
}