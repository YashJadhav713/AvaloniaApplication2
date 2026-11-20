using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaApplication2;

namespace AvaloniaApplication2.Assets.Pages;

public partial class NutritionEditWindow : Window
{
    private int _drinkId;
    private bool _loaded;
    private TextBox _activeBox;
    private TextBlock? _activeNutritionLabel;
    private (TextBox Box, string Name)[] _boxes = System.Array.Empty<(TextBox, string)>();

    // "DRINK" or "PROTEIN BAR" - chosen automatically from DrinkData.IsBar.
    private string _itemLabel = "DRINK";

    public NutritionEditWindow()
    {
        InitializeComponent();

        _activeBox = ServingTextBox;
        _activeNutritionLabel = this.FindControl<TextBlock>("ActiveNutritionLabel");
        _boxes = new (TextBox Box, string Name)[]
        {
            (ServingTextBox, "SERVING SIZE"),
            (ProteinTextBox, "PROTEIN"),
            (CaloriesTextBox, "CALORIES"),
            (BCAAtextBox, "BCAA"),
            (TotalSugarsTextBox, "TOTAL SUGARS")
        };

        foreach (var item in _boxes)
        {
            var box = item.Box;
            box.GotFocus += (s, e) => SetActiveBox(box);
        }
        UpdateActiveVisual();

        SaveButton.Click += SaveButton_Click;
        CancelButton.Click += CancelButton_Click;
    }

    // ============================================================
    // LOAD SELECTED ITEM (drinks 1-8, protein bars 9-10)
    // ============================================================

    public void LoadDrink(int drinkId)
    {
        DrinkData? drink = GetDrink(drinkId);

        if (drink == null)
        {
            _loaded = false;
            StatusText.Text = "INVALID ITEM ID";
            SaveButton.IsEnabled = false;
            return;
        }

        _drinkId = drink.Id;

        _itemLabel = drink.IsBar ? "PROTEIN BAR" : "DRINK";

        Title = $"EDIT {_itemLabel} NUTRITION";

        DrinkIdText.Text = $"{_itemLabel} ID: {_drinkId}";

        // Serving hint differs: "350ml" for drinks, "1 bar" for bars.
        ServingTextBox.PlaceholderText =
            drink.IsBar ? "e.g. 1 bar" : "e.g. 350ml";

        ServingTextBox.Text =
            drink.Nutrition.Serving;

        ProteinTextBox.Text =
            drink.Nutrition.Protein;

        CaloriesTextBox.Text =
            drink.Nutrition.Calories;

        BCAAtextBox.Text =
            drink.Nutrition.BCAA;

        TotalSugarsTextBox.Text =
            drink.Nutrition.TotalSugars;

        StatusText.Text = "";

        SaveButton.IsEnabled = true;
        _loaded = true;
    }

    // ============================================================
    // GET ITEM BY FIXED ID
    // 1-8  = drinks
    // 9-10 = protein bars
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
    // SAVE NUTRITION
    // ============================================================

    private void SaveButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (!_loaded)
        {
            StatusText.Text = $"NO {_itemLabel} SELECTED";
            return;
        }

        DrinkData? drink = GetDrink(_drinkId);

        if (drink == null)
        {
            StatusText.Text = $"INVALID {_itemLabel} ID";
            return;
        }

        string serving =
            ServingTextBox.Text?.Trim() ?? "";

        string protein =
            ProteinTextBox.Text?.Trim() ?? "";

        string calories =
            CaloriesTextBox.Text?.Trim() ?? "";

        string bcaa =
            BCAAtextBox.Text?.Trim() ?? "";

        string sugars =
            TotalSugarsTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(serving))
        {
            StatusText.Text = "ENTER SERVING";
            return;
        }

        if (string.IsNullOrWhiteSpace(protein))
        {
            StatusText.Text = "ENTER PROTEIN";
            return;
        }

        if (string.IsNullOrWhiteSpace(calories))
        {
            StatusText.Text = "ENTER CALORIES";
            return;
        }

        if (string.IsNullOrWhiteSpace(bcaa))
        {
            StatusText.Text = "ENTER BCAA";
            return;
        }

        if (string.IsNullOrWhiteSpace(sugars))
        {
            StatusText.Text = "ENTER TOTAL SUGARS";
            return;
        }

        bool saved = Drinks.SaveSelected(
            _drinkId,
            drink.Enabled,
            drink.Name,
            drink.Price,
            drink.NutritionEnabled,
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

        Close();
    }

    // ============================================================
    // CANCEL WITHOUT SAVING
    // ============================================================

    private void CancelButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Close();
    }

    // ============================================================
    // ON-SCREEN KEYPAD METHODS
    // ============================================================

    private void SetActiveBox(TextBox box)
    {
        _activeBox = box;
        UpdateActiveVisual();
    }

    private void UpdateActiveVisual()
    {
        string label = "EDITING: NUTRITION";
        foreach (var item in _boxes)
        {
            if (item.Box == _activeBox)
            {
                label = $"EDITING: {item.Name}";
                break;
            }
        }
        if (_activeNutritionLabel != null)
        {
            _activeNutritionLabel.Text = label;
        }
    }

    private void KeypadNext_Click(object? sender, RoutedEventArgs e)
    {
        int index = System.Array.FindIndex(_boxes, b => b.Box == _activeBox);
        if (index >= 0 && index < _boxes.Length - 1)
        {
            SetActiveBox(_boxes[index + 1].Box);
            _activeBox.Focus();
        }
        else if (_boxes.Length > 0)
        {
            SetActiveBox(_boxes[0].Box);
            _activeBox.Focus();
        }
    }

    private void KeypadDigit_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeBox == null) return;
        if (sender is not Button btn || btn.Tag is not string digit) return;

        string current = _activeBox.Text ?? "";
        if (digit == "." && current.Contains('.'))
        {
            return;
        }

        int caret = _activeBox.CaretIndex;
        if (caret < 0 || caret > current.Length) caret = current.Length;

        string updated = current.Insert(caret, digit);
        _activeBox.Text = updated;
        _activeBox.CaretIndex = caret + digit.Length;
    }

    private void KeypadBackspace_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeBox == null) return;
        string current = _activeBox.Text ?? "";
        if (current.Length == 0) return;

        int caret = _activeBox.CaretIndex;
        if (caret <= 0 || caret > current.Length) caret = current.Length;

        if (caret > 0)
        {
            string updated = current.Remove(caret - 1, 1);
            _activeBox.Text = updated;
            _activeBox.CaretIndex = caret - 1;
        }
    }

    private void KeypadClear_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeBox == null) return;
        _activeBox.Text = "";
        _activeBox.CaretIndex = 0;
    }
}
