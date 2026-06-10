using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WPFCourseWork.Controls;

public partial class ColorPickerPopup : Popup
{
    private const int SvWidth = 180;
    private const int SvHeight = 140;
    private const int HueWidth = 20;
    private const int HueHeight = 140;

    private double _hue;
    private double _saturation;
    private double _value = 1;
    private bool _draggingSv;
    private bool _draggingHue;

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorPickerPopup),
            new FrameworkPropertyMetadata(Colors.Red, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    public event EventHandler<Color>? ColorChanged;

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public ColorPickerPopup()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _draggingSv = false;
            _draggingHue = false;
            ReleasePickerMouseCapture();
        };
    }

    public void ShowFor(UIElement placementTarget, Color initialColor)
    {
        PlacementTarget = placementTarget;
        SetColor(initialColor, raiseEvent: false);
        IsOpen = true;
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerPopup popup && e.NewValue is Color color)
        {
            popup.SetColor(color, raiseEvent: false);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildHueImage();
        UpdateSaturationValueImage();
        UpdateMarkers();
        UpdatePreview();
    }

    private void SetColor(Color color, bool raiseEvent)
    {
        (_hue, _saturation, _value) = ColorUtils.RgbToHsv(color.R, color.G, color.B);
        if (_value < 0.00001)
        {
            _value = 0.00001;
        }

        UpdateSaturationValueImage();
        UpdateMarkers();
        UpdatePreview();

        if (raiseEvent)
        {
            SelectedColor = color;
            ColorChanged?.Invoke(this, color);
        }
    }

    private void BuildHueImage()
    {
        var bitmap = new WriteableBitmap(HueWidth, HueHeight, 96, 96, PixelFormats.Bgra32, null);
        bitmap.Lock();
        try
        {
            unsafe
            {
                byte* pixels = (byte*)bitmap.BackBuffer;
                for (int y = 0; y < HueHeight; y++)
                {
                    double hue = 360.0 * y / (HueHeight - 1);
                    Color color = ColorUtils.HsvToRgb(hue, 1, 1);
                    for (int x = 0; x < HueWidth; x++)
                    {
                        int index = (y * HueWidth + x) * 4;
                        pixels[index] = color.B;
                        pixels[index + 1] = color.G;
                        pixels[index + 2] = color.R;
                        pixels[index + 3] = 255;
                    }
                }
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, HueWidth, HueHeight));
        }
        finally
        {
            bitmap.Unlock();
        }

        HueImage.Source = bitmap;
    }

    private void UpdateSaturationValueImage()
    {
        var bitmap = new WriteableBitmap(SvWidth, SvHeight, 96, 96, PixelFormats.Bgra32, null);
        Color baseColor = ColorUtils.HsvToRgb(_hue, 1, 1);

        bitmap.Lock();
        try
        {
            unsafe
            {
                byte* pixels = (byte*)bitmap.BackBuffer;
                for (int y = 0; y < SvHeight; y++)
                {
                    double value = 1.0 - (double)y / (SvHeight - 1);
                    for (int x = 0; x < SvWidth; x++)
                    {
                        double saturation = (double)x / (SvWidth - 1);
                        Color color = ColorUtils.HsvToRgb(_hue, saturation, value);
                        int index = (y * SvWidth + x) * 4;
                        pixels[index] = color.B;
                        pixels[index + 1] = color.G;
                        pixels[index + 2] = color.R;
                        pixels[index + 3] = 255;
                    }
                }
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, SvWidth, SvHeight));
        }
        finally
        {
            bitmap.Unlock();
        }

        SaturationValueImage.Source = bitmap;
        SaturationValueCanvas.Background = new SolidColorBrush(baseColor);
    }

    private void UpdateMarkers()
    {
        Canvas.SetLeft(SaturationValueMarker, _saturation * (SvWidth - 1) - 5);
        Canvas.SetTop(SaturationValueMarker, (1 - _value) * (SvHeight - 1) - 5);
        Canvas.SetTop(HueMarker, _hue / 360.0 * (HueHeight - 1) - 1.5);
    }

    private void UpdatePreview()
    {
        Color color = ColorUtils.HsvToRgb(_hue, _saturation, _value);
        PreviewSwatch.Background = new SolidColorBrush(color);
        PreviewHexText.Text = ColorUtils.ColorToHex(color);
        SelectedColor = color;
    }

    private void ApplyCurrentColor(bool raiseEvent)
    {
        Color color = ColorUtils.HsvToRgb(_hue, _saturation, _value);
        UpdatePreview();
        if (raiseEvent)
        {
            ColorChanged?.Invoke(this, color);
        }
    }

    private void OnSaturationValueMouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingSv = true;
        SaturationValueCanvas.CaptureMouse();
        UpdateFromSaturationValue(e.GetPosition(SaturationValueCanvas));
    }

    private void OnSaturationValueMouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingSv)
        {
            return;
        }

        UpdateFromSaturationValue(e.GetPosition(SaturationValueCanvas));
    }

    private void OnSaturationValueMouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingSv = false;
        SaturationValueCanvas.ReleaseMouseCapture();
    }

    private void UpdateFromSaturationValue(Point position)
    {
        _saturation = Math.Clamp(position.X / (SvWidth - 1), 0, 1);
        _value = Math.Clamp(1 - position.Y / (SvHeight - 1), 0, 1);
        UpdateMarkers();
        ApplyCurrentColor(raiseEvent: true);
    }

    private void OnHueMouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = true;
        HueCanvas.CaptureMouse();
        UpdateFromHue(e.GetPosition(HueCanvas));
    }

    private void OnHueMouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingHue)
        {
            return;
        }

        UpdateFromHue(e.GetPosition(HueCanvas));
    }

    private void OnHueMouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingHue = false;
        HueCanvas.ReleaseMouseCapture();
    }

    private void UpdateFromHue(Point position)
    {
        _hue = Math.Clamp(position.Y / (HueHeight - 1), 0, 1) * 360;
        UpdateSaturationValueImage();
        UpdateMarkers();
        ApplyCurrentColor(raiseEvent: true);
    }

    private void ReleasePickerMouseCapture()
    {
        if (SaturationValueCanvas.IsMouseCaptured)
        {
            SaturationValueCanvas.ReleaseMouseCapture();
        }

        if (HueCanvas.IsMouseCaptured)
        {
            HueCanvas.ReleaseMouseCapture();
        }
    }
}
