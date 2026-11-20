using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaApplication2;
using AvaloniaApplication2.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Assets.Pages;

public partial class DrinkEditPage : UserControl
{
    public event Action? SaveRequested;
    public event Action? CancelRequested;

    public event Action<int>? EditImageRequested;
    public event Action<int>? EditNutritionRequested;

    private int _drinkId;
    private bool _isBar;
    private bool _drinkEnabled;
    private bool _nutritionEnabled;
    private bool _loaded;

    private Button? _drinkOnBtn;
    private Button? _drinkOffBtn;
    private Button? _nutritionOnBtn;
    private Button? _nutritionOffBtn;

    // ON-SCREEN KEYPAD CONTROLS
    private TextBox _activeTextBox;
    private bool _isCaps = true;
    private readonly List<Button> _letterButtons = new();

    private Border? _nameBoxBorder;
    private Border? _priceBoxBorder;
    private TextBlock? _activeFieldLabel;
    private Button? _editNameTabBtn;
    private Button? _editPriceTabBtn;
    private Button? _capsBtn;

    // "DRINK" for drinks (IDs 1-8), "BAR" for Protain Bars (IDs 9-10)
    private string ItemWord => _isBar ? "BAR" : "DRINK";

    // ============================================================
    // BRUSHES (Dark Green theme)
    //   SELECTED   : dark green fill (#00A844), black text, dark green border
    //   UNSELECTED : distinct dark fill (#1A241C), white text, crisp visible green border (#00A844)
    // ============================================================

    private static readonly IBrush GreenBrush =
        Brush.Parse("#00A844");

    private static readonly IBrush BlackBrush =
        Brush.Parse("#0A0A0A");

    private static readonly IBrush PanelBrush =
        Brush.Parse("#121212");

    private static readonly IBrush UnselectedOptionBgBrush =
        Brush.Parse("#1A241C");

    private static readonly IBrush WhiteBrush =
        Brush.Parse("#FFFFFF");

    private static readonly IBrush DefaultBorderBrush =
        Brush.Parse("#00A844");

    public DrinkEditPage()
    {
        InitializeComponent();

        _activeTextBox = NameTextBox;

        _drinkOnBtn = this.FindControl<Button>("DrinkOnButton");
        _drinkOffBtn = this.FindControl<Button>("DrinkOffButton");
        _nutritionOnBtn = this.FindControl<Button>("NutritionOnButton");
        _nutritionOffBtn = this.FindControl<Button>("NutritionOffButton");

        _nameBoxBorder = this.FindControl<Border>("NameBoxBorder");
        _priceBoxBorder = this.FindControl<Border>("PriceBoxBorder");
        _activeFieldLabel = this.FindControl<TextBlock>("ActiveFieldLabel");
        _editNameTabBtn = this.FindControl<Button>("EditNameTabButton");
        _editPriceTabBtn = this.FindControl<Button>("EditPriceTabButton");
        _capsBtn = this.FindControl<Button>("CapsButton");

        CollectLetterButtons(this.FindControl<StackPanel>("RowLetters1"));
        CollectLetterButtons(this.FindControl<StackPanel>("RowLetters2"));
        CollectLetterButtons(this.FindControl<StackPanel>("RowLetters3"));

        NameTextBox.GotFocus += (s, e) => SetActiveTextBox(NameTextBox);
        PriceTextBox.GotFocus += (s, e) => SetActiveTextBox(PriceTextBox);

        if (_nameBoxBorder != null)
        {
            _nameBoxBorder.PointerPressed += (s, e) =>
            {
                SetActiveTextBox(NameTextBox);
                NameTextBox.Focus();
            };
        }

        if (_priceBoxBorder != null)
        {
            _priceBoxBorder.PointerPressed += (s, e) =>
            {
                SetActiveTextBox(PriceTextBox);
                PriceTextBox.Focus();
            };
        }

        if (_drinkOnBtn != null)
        {
            _drinkOnBtn.Click += (s, e) =>
            {
                if (!_loaded) return;
                _drinkEnabled = true;
                UpdateDrinkEnabledButtons();
            };
        }

        if (_drinkOffBtn != null)
        {
            _drinkOffBtn.Click += (s, e) =>
            {
                if (!_loaded) return;
                _drinkEnabled = false;
                UpdateDrinkEnabledButtons();
            };
        }

        if (_nutritionOnBtn != null)
        {
            _nutritionOnBtn.Click += (s, e) =>
            {
                if (!_loaded) return;
                _nutritionEnabled = true;
                UpdateNutritionEnabledButtons();
            };
        }

        if (_nutritionOffBtn != null)
        {
            _nutritionOffBtn.Click += (s, e) =>
            {
                if (!_loaded) return;
                _nutritionEnabled = false;
                UpdateNutritionEnabledButtons();
            };
        }

        EditImageButton.Click += EditImageButton_Click;
        EditNutritionButton.Click += EditNutritionButton_Click;
        SaveButton.Click += SaveButton_Click;
        CancelButton.Click += CancelButton_Click;

        UpdateActiveBoxVisual();
        UpdateCapsVisual();
    }

    // ============================================================
    // OPTION BUTTON STYLE (selected / unselected)
    // ============================================================

    private static void SetOptionStyle(Button? button, bool selected)
    {
        if (button == null) return;
        button.Background = selected ? GreenBrush : UnselectedOptionBgBrush;
        button.Foreground = selected ? BlackBrush : WhiteBrush;
        button.BorderBrush = selected ? GreenBrush : DefaultBorderBrush;
        button.FontWeight = selected ? FontWeight.Black : FontWeight.Bold;
    }

    // ============================================================
    // LOAD SELECTED DRINK / BAR
    // ============================================================

    public void LoadDrink(int drinkId)
    {
        DrinkData? drink = GetDrink(drinkId);

        if (drink == null)
        {
            _loaded = false;
            StatusText.Text = "INVALID DRINK ID";
            return;
        }

        _drinkId = drink.Id;
        _isBar = drink.IsBar;
        _drinkEnabled = drink.Enabled;
        _nutritionEnabled = drink.NutritionEnabled;

        // Labels: DRINK or BAR
        TitleText.Text = $"EDIT {ItemWord}";
        DrinkIdText.Text = $"{ItemWord} ID: {_drinkId}";
        NameLabelText.Text = $"{ItemWord} NAME";
        StatusLabelText.Text = $"{ItemWord} STATUS";

        NameTextBox.Text = drink.Name;
        PriceTextBox.Text = drink.Price;

        UpdateDrinkEnabledButtons();
        UpdateNutritionEnabledButtons();

        _activeTextBox = NameTextBox;
        _activeTextBox.CaretIndex = _activeTextBox.Text?.Length ?? 0;
        UpdateActiveBoxVisual();
        UpdateCapsVisual();

        StatusText.Text = "";
        _loaded = true;
    }

    private void UpdateDrinkEnabledButtons()
    {
        SetOptionStyle(_drinkOnBtn, _drinkEnabled);
        SetOptionStyle(_drinkOffBtn, !_drinkEnabled);
    }

    private void UpdateNutritionEnabledButtons()
    {
        SetOptionStyle(_nutritionOnBtn, _nutritionEnabled);
        SetOptionStyle(_nutritionOffBtn, !_nutritionEnabled);
    }

    // ============================================================
    // ON-SCREEN KEYPAD LOGIC
    // ============================================================

    private void CollectLetterButtons(StackPanel? panel)
    {
        if (panel == null) return;
        foreach (var child in panel.Children)
        {
            if (child is Button btn && btn.Classes.Contains("keypadLetter"))
            {
                _letterButtons.Add(btn);
            }
        }
    }

    private void SetActiveTextBox(TextBox target)
    {
        _activeTextBox = target;
        _activeTextBox.CaretIndex = _activeTextBox.Text?.Length ?? 0;
        UpdateActiveBoxVisual();
    }

    private void EditNameTab_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveTextBox(NameTextBox);
        NameTextBox.Focus();
    }

    private void EditPriceTab_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveTextBox(PriceTextBox);
        PriceTextBox.Focus();
    }

    private void UpdateActiveBoxVisual()
    {
        bool isName = _activeTextBox == NameTextBox;

        if (_nameBoxBorder != null)
        {
            _nameBoxBorder.BorderBrush = isName ? GreenBrush : DefaultBorderBrush;
            _nameBoxBorder.BorderThickness = isName ? new Thickness(2.5) : new Thickness(2);
        }

        if (_priceBoxBorder != null)
        {
            _priceBoxBorder.BorderBrush = !isName ? GreenBrush : DefaultBorderBrush;
            _priceBoxBorder.BorderThickness = !isName ? new Thickness(2.5) : new Thickness(2);
        }

        if (_activeFieldLabel != null)
        {
            _activeFieldLabel.Text = isName
                ? $"EDITING: {ItemWord} NAME"
                : $"EDITING: {ItemWord} PRICE";
        }

        if (_editNameTabBtn != null)
        {
            _editNameTabBtn.Background = isName ? GreenBrush : PanelBrush;
            _editNameTabBtn.Foreground = isName ? BlackBrush : WhiteBrush;
            _editNameTabBtn.BorderBrush = isName ? GreenBrush : DefaultBorderBrush;
        }

        if (_editPriceTabBtn != null)
        {
            _editPriceTabBtn.Background = !isName ? GreenBrush : PanelBrush;
            _editPriceTabBtn.Foreground = !isName ? BlackBrush : WhiteBrush;
            _editPriceTabBtn.BorderBrush = !isName ? GreenBrush : DefaultBorderBrush;
        }
    }

    private void KeypadChar_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeTextBox == null) return;
        if (sender is not Button btn || btn.Tag is not string val || string.IsNullOrEmpty(val)) return;

        // If editing price, only allow digits and decimal point.
        if (_activeTextBox == PriceTextBox)
        {
            char c = val[0];
            bool isDigit = char.IsDigit(c);
            bool isDot = c == '.';

            if (!isDigit && !isDot)
            {
                return; // Ignore letters for price
            }

            string cur = _activeTextBox.Text ?? "";
            if (isDot && cur.Contains('.'))
            {
                return; // Allow only one decimal point
            }
        }

        string charToInsert = _isCaps ? val.ToUpperInvariant() : val.ToLowerInvariant();
        InsertText(charToInsert);
    }

    private void KeypadSpace_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeTextBox == null) return;
        if (_activeTextBox == PriceTextBox) return; // Disallow spaces in price
        InsertText(" ");
    }

    private void InsertText(string textToInsert)
    {
        if (_activeTextBox == null) return;

        string current = _activeTextBox.Text ?? "";
        int caret = _activeTextBox.CaretIndex;

        if (caret < 0 || caret > current.Length)
        {
            caret = current.Length;
        }

        string updated = current.Insert(caret, textToInsert);
        _activeTextBox.Text = updated;
        _activeTextBox.CaretIndex = caret + textToInsert.Length;
    }

    private void KeypadBackspace_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeTextBox == null) return;

        string current = _activeTextBox.Text ?? "";
        if (current.Length == 0) return;

        int caret = _activeTextBox.CaretIndex;
        if (caret <= 0 || caret > current.Length)
        {
            caret = current.Length;
        }

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

    private void KeypadCaps_Click(object? sender, RoutedEventArgs e)
    {
        _isCaps = !_isCaps;
        UpdateCapsVisual();
    }

    private void UpdateCapsVisual()
    {
        if (_capsBtn != null)
        {
            _capsBtn.Background = _isCaps ? GreenBrush : PanelBrush;
            _capsBtn.Foreground = _isCaps ? BlackBrush : WhiteBrush;
            _capsBtn.BorderBrush = _isCaps ? GreenBrush : DefaultBorderBrush;
        }

        foreach (var btn in _letterButtons)
        {
            if (btn.Tag is string tag)
            {
                btn.Content = _isCaps ? tag.ToUpperInvariant() : tag.ToLowerInvariant();
            }
        }
    }

    private void KeypadSwitch_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeTextBox == NameTextBox)
        {
            SetActiveTextBox(PriceTextBox);
            PriceTextBox.Focus();
        }
        else
        {
            SetActiveTextBox(NameTextBox);
            NameTextBox.Focus();
        }
    }

    // ============================================================
    // GET DRINK / BAR BY FIXED ID
    // ============================================================

    private static DrinkData? GetDrink(int drinkId)
    {
        return drinkId switch
        {
            1 => Drink.First,
            2 => Drink.Second,
            3 => Drink.Third,
            4 => Drink.Fourth,
            5 => Drink.Fifth,
            6 => Drink.Sixth,
            7 => Drink.Seventh,
            8 => Drink.Eighth,
            9 => Bar.First,
            10 => Bar.Second,
            _ => null
        };
    }

    // ============================================================
    // OPEN IMAGE EDITOR
    // ============================================================

    private void EditImageButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        EditImageRequested?.Invoke(_drinkId);
    }

    // ============================================================
    // OPEN NUTRITION EDITOR
    // ============================================================

    private void EditNutritionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        EditNutritionRequested?.Invoke(_drinkId);
    }

    // ============================================================
    // SAVE DRINK / BAR
    // ============================================================

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            StatusText.Text = $"NO {ItemWord} SELECTED";
            return;
        }

        string name = NameTextBox.Text?.Trim() ?? "";
        string price = PriceTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = $"ENTER {ItemWord} NAME";
            return;
        }

        if (!decimal.TryParse(
                price,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal parsedPrice)
            || parsedPrice < 0)
        {
            StatusText.Text = "ENTER VALID PRICE";
            return;
        }

        price = parsedPrice.ToString(
            "0.##",
            CultureInfo.InvariantCulture);

        DrinkData? drink = GetDrink(_drinkId);

        if (drink == null)
        {
            StatusText.Text = "INVALID DRINK ID";
            return;
        }

        // Keep existing nutrition values.
        string? serving = drink.Nutrition?.Serving;
        string? protein = drink.Nutrition?.Protein;
        string? calories = drink.Nutrition?.Calories;
        string? bcaa = drink.Nutrition?.BCAA;
        string? sugars = drink.Nutrition?.TotalSugars;

        bool saved = Drinks.SaveSelected(
            _drinkId,
            _drinkEnabled,
            name,
            price,
            _nutritionEnabled,
            serving,
            protein,
            calories,
            bcaa,
            sugars);

        if (!saved)
        {
            StatusText.Text = "SAVE FAILED";
            return;
        }

        // Sync updated drink/bar status, name, and stock to VendiBite API
        _ = Task.Run(async () => await VendiBiteService.UpdateStockAsync());

        StatusText.Text = "SAVED";
        SaveRequested?.Invoke();
    }

    // ============================================================
    // CANCEL EDITING
    // ============================================================

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        CancelRequested?.Invoke();
    }
}
