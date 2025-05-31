using System.Windows;
using System.Windows.Controls;
using CourseWorkZherbin;
using Point = CourseWorkZherbin.Point;

namespace WPFCourseWork;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
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
        
        //TODO: Избавится от этого ужаса и сделать также, как и с выбором метода
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
                CalculationsSingular(partition, selectedPores, poresValue);
                break;
            case 1:
                
                CalculationsCube(partition, selectedPores, poresValue); 
                break;
            case 2:
                CalculationsPoints(partition, selectedPores, poresValue);
                break;
            default:
                MessageBox.Show("Что-то тут не так....");
                break;
        }
        
    }

    private void CalculationsSingular(int partition, string poreChoice, double poresValue)
    {
        CubeGrid griddy = new CubeGrid(partition);
        CubeLine liney = griddy.GenerateLineFromGrid();
        CreatePores(liney, poreChoice, poresValue);
        
        List<string> output = new List<string>();

        for (int i = 0; i < partition; i++)
        {
            for (int j = 0; j < partition; j++)
            {
                for (int k = 0; k < partition; k++)
                {
                    output.Add(griddy.Grid[i][j][k].IsEmpty.ToString());
                }
            }
        }
        
        CalculationsResults.ItemsSource = output;
        
    }
    
    private void CalculationsCube(int partition, string poreChoice, double poresValue)
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
        CubeLine liney;
        try
        {
            startCube = new Cube(new Point(centerX, centerY, centerZ), sideLength);
            griddy = new CubeGrid(startCube, partition);
            liney = griddy.GenerateLineFromGrid();
        }
        catch(Exception e)
        {
            MessageBox.Show(e.Message);
            return;
        }
        CreatePores(liney, poreChoice, poresValue);
        
        List<string> output = new List<string>();

        for (int i = 0; i < partition; i++)
        {
            for (int j = 0; j < partition; j++)
            {
                for (int k = 0; k < partition; k++)
                {
                    output.Add(griddy.Grid[i][j][k].IsEmpty.ToString());
                }
            }
        }
        
        CalculationsResults.ItemsSource = output;
    }
    
    private void CalculationsPoints(int partition, string poreChoice, double poresValue)
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
        CubeLine liney;
        try
        {
            griddy = new CubeGrid(p1, p2, partition);
            liney = griddy.GenerateLineFromGrid();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message);
            return;
        }
        
        CreatePores(liney, poreChoice, poresValue);
        
        List<string> output = new List<string>();

        for (int i = 0; i < partition; i++)
        {
            for (int j = 0; j < partition; j++)
            {
                for (int k = 0; k < partition; k++)
                {
                    output.Add(griddy.Grid[i][j][k].IsEmpty.ToString());
                }
            }
        }
        
        CalculationsResults.ItemsSource = output;
    }

    private void CreatePores(CubeLine liney, string poreChoice, double poresValue)
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

}