using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaApplication2.Services;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Assets.Pages;

public partial class MilkSettingsPage : UserControl
{
    public event Action? BackRequested;

    private bool _milkEnabled;
    private bool _milkNutritionEnabled = true;
    private TextBox _activeTextBox;
    private TextBlock? _activeKeypadLabel;

    // ================================================================
    // BRUSHES (theme)
    //   SELECTED   : lime fill, black text, lime border
    //   UNSELECTED : dark fill, white text, dark-lime border
    // ================================================================

    private static readonly IBrush LimeBrush =
        Brush.Parse("#00A844");

    private static readonly IBrush BlackBrush =
        Brush.Parse("#0A0A0A");

    private static readonly IBrush PanelBrush =
        Brush.Parse("#121212");

    private static readonly IBrush WhiteBrush =
        Brush.Parse("#FFFFFF");

    private static readonly IBrush DefaultBorderBrush =
        Brush.Parse("#005C25");

    // ================================================================
    // CONSTRUCTOR
    // ================================================================

    public MilkSettingsPage()
    {
        InitializeComponent();

        LoadMilkSettings();

        MilkOnButton.Click += MilkOnButton_Click;
        MilkOffButton.Click += MilkOffButton_Click;

        NutritionOnButton.Click += NutritionOnButton_Click;
        NutritionOffButton.Click += NutritionOffButton_Click;

        EditNutritionButton.Click += EditNutritionButton_Click;

        SaveButton.Click += SaveButton_Click;
        CancelButton.Click += CancelButton_Click;

        NutritionSaveButton.Click += NutritionSaveButton_Click;
        NutritionCancelButton.Click += NutritionCancelButton_Click;

        _activeTextBox = MilkPriceTextBox;
        _activeKeypadLabel = this.FindControl<TextBlock>("ActiveKeypadLabel");

        MilkPriceTextBox.GotFocus += (s, e) => SetActiveTextBox(MilkPriceTextBox, "MILK PRICE");
        ProteinTextBox.GotFocus += (s, e) => SetActiveTextBox(ProteinTextBox, "PROTEIN");
        CaloriesTextBox.GotFocus += (s, e) => SetActiveTextBox(CaloriesTextBox, "CALORIES");
        BCAAtextBox.GotFocus += (s, e) => SetActiveTextBox(BCAAtextBox, "BCAA");
        TotalSugarsTextBox.GotFocus += (s, e) => SetActiveTextBox(TotalSugarsTextBox, "TOTAL SUGARS");
    }

    // ================================================================
    // LOAD SETTINGS
    // ================================================================

    private void LoadMilkSettings()
    {
        _milkEnabled = Drinks.MilkEnabled;

        MilkPriceTextBox.Text = Drinks.MilkPrice;

        _milkNutritionEnabled = true;

        LoadNutritionValues();

        UpdateMilkButtons();
        UpdateNutritionButtons();
    }

    // ================================================================
    // LOAD NUTRITION
    // ================================================================

    private void LoadNutritionValues()
    {
        ProteinTextBox.Text =
            Drinks.Milk.Protein.ToString(
                "0.##",
                CultureInfo.InvariantCulture);

        CaloriesTextBox.Text =
            Drinks.Milk.Calories.ToString(
                "0.##",
                CultureInfo.InvariantCulture);

        BCAAtextBox.Text =
            Drinks.Milk.BCAA.ToString(
                "0.##",
                CultureInfo.InvariantCulture);

        TotalSugarsTextBox.Text =
            Drinks.Milk.TotalSugars.ToString(
                "0.##",
                CultureInfo.InvariantCulture);
    }

    // ================================================================
    // OPTION BUTTON STYLE (selected / unselected)
    // ================================================================

    private static void SetOptionStyle(
        Button button,
        bool selected)
    {
        button.Background =
            selected ? LimeBrush : PanelBrush;

        button.Foreground =
            selected ? BlackBrush : WhiteBrush;

        button.BorderBrush =
            selected ? LimeBrush : DefaultBorderBrush;
    }

    // ================================================================
    // MILK ON
    // ================================================================

    private void MilkOnButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        _milkEnabled = true;

        UpdateMilkButtons();
    }

    // ================================================================
    // MILK OFF
    // ================================================================

    private void MilkOffButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        _milkEnabled = false;

        UpdateMilkButtons();
    }

    // ================================================================
    // MILK BUTTONS
    // ================================================================

    private void UpdateMilkButtons()
    {
        SetOptionStyle(MilkOnButton, _milkEnabled);
        SetOptionStyle(MilkOffButton, !_milkEnabled);
    }

    // ================================================================
    // NUTRITION ON
    // ================================================================

    private void NutritionOnButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        _milkNutritionEnabled = true;

        UpdateNutritionButtons();
    }

    // ================================================================
    // NUTRITION OFF
    // ================================================================

    private void NutritionOffButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        _milkNutritionEnabled = false;

        UpdateNutritionButtons();
    }

    // ================================================================
    // NUTRITION BUTTONS
    // ================================================================

    private void UpdateNutritionButtons()
    {
        SetOptionStyle(NutritionOnButton, _milkNutritionEnabled);
        SetOptionStyle(NutritionOffButton, !_milkNutritionEnabled);
    }

    // ================================================================
    // OPEN NUTRITION
    // ================================================================

    private void EditNutritionButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        LoadNutritionValues();

        NutritionEditor.IsVisible = true;
    }

    // ================================================================
    // SAVE NUTRITION
    // ================================================================

    private void NutritionSaveButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        double protein = ParseNutritionValue(ProteinTextBox.Text);
        double calories = ParseNutritionValue(CaloriesTextBox.Text);
        double bcaa = ParseNutritionValue(BCAAtextBox.Text);
        double sugars = ParseNutritionValue(TotalSugarsTextBox.Text);

        Drinks.SetMilkNutrition(protein, calories, bcaa, sugars);

        NutritionEditor.IsVisible = false;
    }

    // ================================================================
    // CANCEL NUTRITION
    // ================================================================

    private void NutritionCancelButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        LoadNutritionValues();

        NutritionEditor.IsVisible = false;
    }

    // ================================================================
    // PARSE VALUE
    // ================================================================

    private static double ParseNutritionValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        if (!double.TryParse(
                value.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double result))
        {
            return 0;
        }

        if (result < 0)
            return 0;

        return result;
    }

    // ================================================================
    // SAVE MAIN SETTINGS
    // ================================================================

    private void SaveButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Drinks.SetMilkEnabled(_milkEnabled);

        Drinks.SetMilkPrice(MilkPriceTextBox.Text);

        _ = Task.Run(async () => await VendiBiteService.UpdateStockAsync());

        BackRequested?.Invoke();
    }

    // ================================================================
    // CANCEL MAIN SETTINGS
    // ================================================================

    private void CancelButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    // ================================================================
    // ON-SCREEN KEYPAD METHODS
    // ================================================================

    private void SetActiveTextBox(TextBox target, string name)
    {
        _activeTextBox = target;
        if (_activeKeypadLabel != null)
        {
            _activeKeypadLabel.Text = $"EDITING: {name}";
        }
    }

    private void KeypadDigit_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeTextBox == null) return;
        if (sender is not Button btn || btn.Tag is not string digit) return;

        string current = _activeTextBox.Text ?? "";
        if (digit == "." && current.Contains('.'))
        {
            return;
        }

        int caret = _activeTextBox.CaretIndex;
        if (caret < 0 || caret > current.Length) caret = current.Length;

        string updated = current.Insert(caret, digit);
        _activeTextBox.Text = updated;
        _activeTextBox.CaretIndex = caret + digit.Length;
    }

    private void KeypadBackspace_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeTextBox == null) return;
        string current = _activeTextBox.Text ?? "";
        if (current.Length == 0) return;

        int caret = _activeTextBox.CaretIndex;
        if (caret <= 0 || caret > current.Length) caret = current.Length;

        if (caret > 0)
        {
            string updated = current.Remove(caret - 1, 1);
            _activeTextBox.Text = updated;
            _activeTextBox.CaretIndex = caret - 1;
        }
    }

    private void KeypadClear_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeTextBox == null) return;
        _activeTextBox.Text = "";
        _activeTextBox.CaretIndex = 0;
    }
}
