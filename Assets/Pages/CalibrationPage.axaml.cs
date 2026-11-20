using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Globalization;
using System.Threading.Tasks;
using AvaloniaApplication2.Services;

namespace AvaloniaApplication2.Assets.Pages;

public partial class CalibrationPage : UserControl
{
    public event Action? BackRequested;

    private readonly CalibrationService _calibrationService = new CalibrationService();
    private readonly TextBox[] _timingBoxes = new TextBox[16];
    private readonly TextBlock[] _menuLabels = new TextBlock[8];

    private Button? _saveCalibrationButton;
    private Button? _backButton;
    private TextBox? _controllerIpBox;
    private Border? _machineStatusDot;
    private TextBlock? _machineStatusText;
    private Border? _machineStatusBadge;
    private TextBlock? _statusMessageText;
    private ProgressBar? _loadingProgressBar;
    private TextBlock? _activeFieldLabel;

    private int _activeIndex = 0;

    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.Parse("#00A844"));
    private static readonly IBrush BrightGreenBrush = new SolidColorBrush(Color.Parse("#00FF66"));
    private static readonly IBrush DefaultBorderBrush = new SolidColorBrush(Color.Parse("#005C25"));
    private static readonly IBrush ActiveBackgroundBrush = new SolidColorBrush(Color.Parse("#1A2D1F"));
    private static readonly IBrush DefaultBackgroundBrush = new SolidColorBrush(Color.Parse("#181818"));

    private static readonly IBrush AmberBrush = new SolidColorBrush(Color.Parse("#FFC107"));
    private static readonly IBrush RedBrush = new SolidColorBrush(Color.Parse("#FF5252"));

    public CalibrationPage()
    {
        InitializeComponent();

        ResolveNamedControls();
        SetupFieldListeners();

        AttachedToVisualTree += CalibrationPage_AttachedToVisualTree;
    }

    private void ResolveNamedControls()
    {
        _saveCalibrationButton = this.FindControl<Button>("SaveCalibrationButton");
        _backButton = this.FindControl<Button>("BackButton");
        _controllerIpBox = this.FindControl<TextBox>("ControllerIpBox");
        _machineStatusDot = this.FindControl<Border>("MachineStatusDot");
        _machineStatusText = this.FindControl<TextBlock>("MachineStatusText");
        _machineStatusBadge = this.FindControl<Border>("MachineStatusBadge");
        _statusMessageText = this.FindControl<TextBlock>("StatusMessageText");
        _loadingProgressBar = this.FindControl<ProgressBar>("LoadingProgressBar");
        _activeFieldLabel = this.FindControl<TextBlock>("ActiveFieldLabel");

        for (int i = 1; i <= 8; i++)
        {
            _timingBoxes[i - 1] = this.FindControl<TextBox>($"PowderBox_{i}")!;
            _timingBoxes[i + 7] = this.FindControl<TextBox>($"WaterBox_{i}")!;
            _menuLabels[i - 1] = this.FindControl<TextBlock>($"Menu{i}Label")!;
        }
    }

    private void SetupFieldListeners()
    {
        for (int i = 0; i < 16; i++)
        {
            int index = i;
            TextBox? box = _timingBoxes[i];
            if (box == null) continue;

            box.GotFocus += (_, _) => SelectActiveField(index);
            box.PointerPressed += (_, _) => SelectActiveField(index);
        }

        if (_controllerIpBox != null)
        {
            _controllerIpBox.Text = Drinks.ControllerIp;
            _controllerIpBox.TextChanged += (_, _) =>
            {
                string ip = _controllerIpBox.Text?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    Drinks.SetControllerIp(ip);
                }
            };
        }
    }

    private async void CalibrationPage_AttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        UpdateMenuLabels();
        LoadLocalTimings();
        SelectActiveField(0);

        await RefreshControllerDataAsync();
    }

    private void UpdateMenuLabels()
    {
        DrinkData[] drinks =
        {
            Drink.First, Drink.Second, Drink.Third, Drink.Fourth,
            Drink.Fifth, Drink.Sixth, Drink.Seventh, Drink.Eighth
        };

        for (int i = 0; i < 8; i++)
        {
            if (_menuLabels[i] == null) continue;
            string drinkName = string.IsNullOrWhiteSpace(drinks[i]?.Name) ? $"Drink {i + 1}" : drinks[i].Name;
            _menuLabels[i].Text = $"MENU {i + 1} ({drinkName.ToUpperInvariant()})";
        }
    }

    private void LoadLocalTimings()
    {
        double[] local = Drinks.GetSavedCalibration();
        PopulateTimingBoxes(local);
    }

    private void PopulateTimingBoxes(double[] timings)
    {
        if (timings == null || timings.Length != 16) return;

        Dispatcher.UIThread.Post(() =>
        {
            for (int i = 0; i < 16; i++)
            {
                if (_timingBoxes[i] != null)
                {
                    _timingBoxes[i].Text = timings[i].ToString("0.##", CultureInfo.InvariantCulture);
                }
            }
        });
    }

    private void SelectActiveField(int index)
    {
        if (index < 0 || index >= 16) return;
        _activeIndex = index;

        Dispatcher.UIThread.Post(() =>
        {
            for (int i = 0; i < 16; i++)
            {
                if (_timingBoxes[i] == null) continue;

                if (i == _activeIndex)
                {
                    _timingBoxes[i].BorderBrush = BrightGreenBrush;
                    _timingBoxes[i].Background = ActiveBackgroundBrush;
                }
                else
                {
                    _timingBoxes[i].BorderBrush = DefaultBorderBrush;
                    _timingBoxes[i].Background = DefaultBackgroundBrush;
                }
            }

            string type = _activeIndex < 8 ? "POWDER" : "WATER";
            int menu = _activeIndex < 8 ? (_activeIndex + 1) : (_activeIndex - 7);
            if (_activeFieldLabel != null)
            {
                _activeFieldLabel.Text = $"EDITING: MENU {menu} - {type} TIME (SEC)";
            }
        });
    }

    private async Task RefreshControllerDataAsync()
    {
        SetLoadingState(true);

        // 1. Check machine status
        var (statusOk, status, error) = await _calibrationService.GetMachineStatusAsync();
        UpdateStatusBadge(statusOk, status);

        // 2. Fetch existing calibration
        CalibrationResult calibResult = await _calibrationService.GetCalibrationAsync();
        if (calibResult.Success && calibResult.Timings != null)
        {
            PopulateTimingBoxes(calibResult.Timings);
        }

        SetLoadingState(false);
    }

    private void UpdateStatusBadge(bool ok, string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_machineStatusDot == null || _machineStatusText == null || _machineStatusBadge == null)
                return;

            if (!ok || string.IsNullOrWhiteSpace(status))
            {
                _machineStatusDot.Background = RedBrush;
                _machineStatusText.Text = "STATUS: OFFLINE";
                _machineStatusText.Foreground = RedBrush;
                _machineStatusBadge.BorderBrush = RedBrush;
                return;
            }

            string cleanStatus = status.ToUpperInvariant();
            if (cleanStatus == MachineBits.StatusIdle || cleanStatus == "B10")
            {
                _machineStatusDot.Background = GreenBrush;
                _machineStatusText.Text = "STATUS: B10 (IDLE)";
                _machineStatusText.Foreground = GreenBrush;
                _machineStatusBadge.BorderBrush = GreenBrush;
            }
            else if (cleanStatus == MachineBits.StatusBusy || cleanStatus == "B6")
            {
                _machineStatusDot.Background = AmberBrush;
                _machineStatusText.Text = "STATUS: B6 (BUSY)";
                _machineStatusText.Foreground = AmberBrush;
                _machineStatusBadge.BorderBrush = AmberBrush;
            }
            else
            {
                _machineStatusDot.Background = AmberBrush;
                _machineStatusText.Text = $"STATUS: {cleanStatus}";
                _machineStatusText.Foreground = AmberBrush;
                _machineStatusBadge.BorderBrush = AmberBrush;
            }
        });
    }

    private void SetLoadingState(bool isLoading)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_loadingProgressBar != null) _loadingProgressBar.IsVisible = isLoading;
            if (_saveCalibrationButton != null) _saveCalibrationButton.IsEnabled = !isLoading;
        });
    }

    private void ShowMessage(string message, IBrush color)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_statusMessageText != null)
            {
                _statusMessageText.Text = message;
                _statusMessageText.Foreground = color;
            }
        });
    }

    // ============================================================
    // SAVE CALIBRATION
    // ============================================================

    private async void SaveCalibrationButton_Click(object? sender, RoutedEventArgs e)
    {
        // 1. Validation: Extract and validate all 16 values
        double[] timings = new double[16];

        for (int i = 0; i < 16; i++)
        {
            string text = _timingBoxes[i]?.Text?.Trim() ?? "";

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value <= 0)
            {
                SelectActiveField(i);
                ShowMessage("UNSUCCESSFUL", RedBrush);
                return;
            }

            timings[i] = value;
        }

        // 2. Perform Idle check and Save via CalibrationService
        SetLoadingState(true);

        CalibrationResult result = await _calibrationService.SaveCalibrationAsync(timings);

        UpdateStatusBadge(result.MachineStatus != "OFFLINE", result.MachineStatus ?? "");

        if (result.Success)
        {
            ShowMessage("SUCCESSFUL", GreenBrush);
        }
        else
        {
            ShowMessage("UNSUCCESSFUL", RedBrush);
        }

        SetLoadingState(false);
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    // ============================================================
    // KEYPAD INPUT HANDLERS
    // ============================================================

    private void KeypadDigit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag == null) return;
        string digit = btn.Tag.ToString() ?? "";

        TextBox? active = _timingBoxes[_activeIndex];
        if (active == null) return;

        string current = active.Text ?? "";
        if (current == "0") current = "";

        active.Text = current + digit;
    }

    private void KeypadDot_Click(object? sender, RoutedEventArgs e)
    {
        TextBox? active = _timingBoxes[_activeIndex];
        if (active == null) return;

        string current = active.Text ?? "";
        if (string.IsNullOrEmpty(current))
        {
            active.Text = "0.";
        }
        else if (!current.Contains('.'))
        {
            active.Text = current + ".";
        }
    }

    private void KeypadBackspace_Click(object? sender, RoutedEventArgs e)
    {
        TextBox? active = _timingBoxes[_activeIndex];
        if (active == null) return;

        string current = active.Text ?? "";
        if (current.Length > 0)
        {
            active.Text = current.Substring(0, current.Length - 1);
        }
    }

    private void KeypadClear_Click(object? sender, RoutedEventArgs e)
    {
        TextBox? active = _timingBoxes[_activeIndex];
        if (active != null)
        {
            active.Text = "";
        }
    }

    private void KeypadAddHalf_Click(object? sender, RoutedEventArgs e)
    {
        AdjustActiveValue(0.5);
    }

    private void KeypadAddOne_Click(object? sender, RoutedEventArgs e)
    {
        AdjustActiveValue(1.0);
    }

    private void AdjustActiveValue(double delta)
    {
        TextBox? active = _timingBoxes[_activeIndex];
        if (active == null) return;

        if (!double.TryParse(active.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
        {
            val = _activeIndex < 8 ? 9.0 : 30.0;
        }

        val = Math.Max(0.1, Math.Round(val + delta, 2));
        active.Text = val.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void KeypadResetDefault_Click(object? sender, RoutedEventArgs e)
    {
        TextBox? active = _timingBoxes[_activeIndex];
        if (active == null) return;

        double def = _activeIndex < 8 ? 9.0 : 30.0;
        active.Text = def.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void PrevField_Click(object? sender, RoutedEventArgs e)
    {
        int prev = _activeIndex - 1;
        if (prev < 0) prev = 15;
        SelectActiveField(prev);
    }

    private void NextField_Click(object? sender, RoutedEventArgs e)
    {
        int next = (_activeIndex + 1) % 16;
        SelectActiveField(next);
    }
}
