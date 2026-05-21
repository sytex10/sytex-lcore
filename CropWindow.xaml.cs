using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.InteropServices;
using WpfRect = System.Windows.Rect;

namespace SytexLCore;

public partial class CropWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private Point _startPoint;
    private bool _isSelecting = false;
    private readonly uint _exitVirtualKey;

    public WpfRect SelectedRegion { get; private set; } = WpfRect.Empty;

    public CropWindow(uint exitVirtualKey = 0x78)
    {
        InitializeComponent();
        _exitVirtualKey = exitVirtualKey;
        PreviewKeyDown += CropWindow_PreviewKeyDown;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetForegroundWindow(hwnd);
        }
        catch { }

        this.Activate();
        this.Focus();
    }

    private void CropWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        int pressedVk = KeyInterop.VirtualKeyFromKey(e.Key);
        
        if (pressedVk == _exitVirtualKey)
        {
            SelectedRegion = WpfRect.Empty;
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // ESC tuşunu tamamen yut ve hiçbir işlem yapma!
            e.Handled = true;
        }
    }

    private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _startPoint = e.GetPosition(RootGrid);
            _isSelecting = true;
            RootGrid.CaptureMouse();

            SelectionBox.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionBox, _startPoint.X);
            Canvas.SetTop(SelectionBox, _startPoint.Y);
            SelectionBox.Width = 0;
            SelectionBox.Height = 0;
        }
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isSelecting)
        {
            var currentPoint = e.GetPosition(RootGrid);

            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(_startPoint.X - currentPoint.X);
            double height = Math.Abs(_startPoint.Y - currentPoint.Y);

            Canvas.SetLeft(SelectionBox, x);
            Canvas.SetTop(SelectionBox, y);
            SelectionBox.Width = width;
            SelectionBox.Height = height;
        }
    }

    private void RootGrid_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && _isSelecting)
        {
            _isSelecting = false;
            RootGrid.ReleaseMouseCapture();

            double x = Canvas.GetLeft(SelectionBox);
            double y = Canvas.GetTop(SelectionBox);
            double w = SelectionBox.Width;
            double h = SelectionBox.Height;

            if (w > 10 && h > 10)
            {
                // WPF logical coordinates. On Windows OCR, this will be scaled appropriately.
                SelectedRegion = new WpfRect(x, y, w, h);
            }

            Close();
        }
    }
}
