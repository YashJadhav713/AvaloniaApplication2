using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaApplication2.Services;
using System;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Assets.Pages
{
    public partial class UpdateStockPage : UserControl
    {
        public event Action? BackRequested;

        private bool isSaving = false;
        private TextBox _activeBox;
        private TextBlock? _activeStockLabel;
        private (TextBox Box, string Name)[] _stockBoxes = Array.Empty<(TextBox, string)>();

        public UpdateStockPage()
        {
            InitializeComponent();

            _activeBox = MilkStockBox;
            _activeStockLabel = this.FindControl<TextBlock>("ActiveStockLabel");
            _stockBoxes = new (TextBox, string)[]
            {
                (MilkStockBox, "MILK STOCK"),
                (Drink1StockBox, "DRINK 1 STOCK"),
                (Drink2StockBox, "DRINK 2 STOCK"),
                (Drink3StockBox, "DRINK 3 STOCK"),
                (Drink4StockBox, "DRINK 4 STOCK"),
                (Drink5StockBox, "DRINK 5 STOCK"),
                (Drink6StockBox, "DRINK 6 STOCK"),
                (Drink7StockBox, "DRINK 7 STOCK"),
                (Drink8StockBox, "DRINK 8 STOCK"),
                (Bar1StockBox, "PROTEIN BAR 1"),
                (Bar2StockBox, "PROTEIN BAR 2")
            };

            foreach (var item in _stockBoxes)
            {
                var box = item.Box;
                box.GotFocus += (s, e) => SetActiveBox(box);
            }

            LoadStock();
            UpdateActiveVisual();

            SaveStockButton.Click += async (_, _) => await SaveStockAsync();

            BackButton.Click += (_, _) =>
            {
                BackRequested?.Invoke();
            };
        }

        private void SetActiveBox(TextBox box)
        {
            _activeBox = box;
            UpdateActiveVisual();
        }

        private void UpdateActiveVisual()
        {
            string label = "EDITING: STOCK";
            foreach (var item in _stockBoxes)
            {
                bool isCurrent = item.Box == _activeBox;
                item.Box.BorderBrush = isCurrent ? Avalonia.Media.Brush.Parse("#00A844") : Avalonia.Media.Brush.Parse("#005C25");
                item.Box.BorderThickness = isCurrent ? new Avalonia.Thickness(2.5) : new Avalonia.Thickness(2);
                if (isCurrent)
                {
                    label = $"EDITING: {item.Name}";
                }
            }
            if (_activeStockLabel != null)
            {
                _activeStockLabel.Text = label;
            }
        }

        private void PrevStock_Click(object? sender, RoutedEventArgs e)
        {
            int index = Array.FindIndex(_stockBoxes, b => b.Box == _activeBox);
            if (index > 0)
            {
                SetActiveBox(_stockBoxes[index - 1].Box);
                _activeBox.Focus();
            }
            else if (_stockBoxes.Length > 0)
            {
                SetActiveBox(_stockBoxes[_stockBoxes.Length - 1].Box);
                _activeBox.Focus();
            }
        }

        private void NextStock_Click(object? sender, RoutedEventArgs e)
        {
            int index = Array.FindIndex(_stockBoxes, b => b.Box == _activeBox);
            if (index >= 0 && index < _stockBoxes.Length - 1)
            {
                SetActiveBox(_stockBoxes[index + 1].Box);
                _activeBox.Focus();
            }
            else if (_stockBoxes.Length > 0)
            {
                SetActiveBox(_stockBoxes[0].Box);
                _activeBox.Focus();
            }
        }

        private void KeypadDigit_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string digit)
            {
                string current = _activeBox.Text?.Trim() ?? "";
                if (current == "0") current = "";
                _activeBox.Text = current + digit;
            }
        }

        private void KeypadAddTen_Click(object? sender, RoutedEventArgs e)
        {
            if (int.TryParse(_activeBox.Text, out int val))
            {
                _activeBox.Text = (val + 10).ToString();
            }
            else
            {
                _activeBox.Text = "10";
            }
        }

        private void KeypadBackspace_Click(object? sender, RoutedEventArgs e)
        {
            string current = _activeBox.Text ?? "";
            if (current.Length > 0)
            {
                _activeBox.Text = current.Substring(0, current.Length - 1);
                if (string.IsNullOrEmpty(_activeBox.Text))
                {
                    _activeBox.Text = "0";
                }
            }
        }

        private void KeypadClear_Click(object? sender, RoutedEventArgs e)
        {
            _activeBox.Text = "0";
        }

        private void LoadStock()
        {
            MilkStockBox.Text = Drinks.GetMilkStock().ToString();

            Drink1StockBox.Text = Drinks.GetStock(1).ToString();
            Drink2StockBox.Text = Drinks.GetStock(2).ToString();
            Drink3StockBox.Text = Drinks.GetStock(3).ToString();
            Drink4StockBox.Text = Drinks.GetStock(4).ToString();
            Drink5StockBox.Text = Drinks.GetStock(5).ToString();
            Drink6StockBox.Text = Drinks.GetStock(6).ToString();
            Drink7StockBox.Text = Drinks.GetStock(7).ToString();
            Drink8StockBox.Text = Drinks.GetStock(8).ToString();

            // Protein Bars
            Bar1StockBox.Text = Drinks.GetStock(9).ToString();
            Bar2StockBox.Text = Drinks.GetStock(10).ToString();

            // Dynamic Labels showing real customized names
            Drink1Label.Text = $"{Drink.First.Name.ToUpperInvariant()} (ID: 1)";
            Drink2Label.Text = $"{Drink.Second.Name.ToUpperInvariant()} (ID: 2)";
            Drink3Label.Text = $"{Drink.Third.Name.ToUpperInvariant()} (ID: 3)";
            Drink4Label.Text = $"{Drink.Fourth.Name.ToUpperInvariant()} (ID: 4)";
            Drink5Label.Text = $"{Drink.Fifth.Name.ToUpperInvariant()} (ID: 5)";
            Drink6Label.Text = $"{Drink.Sixth.Name.ToUpperInvariant()} (ID: 6)";
            Drink7Label.Text = $"{Drink.Seventh.Name.ToUpperInvariant()} (ID: 7)";
            Drink8Label.Text = $"{Drink.Eighth.Name.ToUpperInvariant()} (ID: 8)";
            Bar1Label.Text = $"{Bar.First.Name.ToUpperInvariant()} (ID: 9)";
            Bar2Label.Text = $"{Bar.Second.Name.ToUpperInvariant()} (ID: 10)";
        }

        private async Task SaveStockAsync()
        {
            // Prevent multiple clicks while saving
            if (isSaving)
                return;

            if (!int.TryParse(MilkStockBox.Text, out int milkStock) || milkStock < 0 ||
                !int.TryParse(Drink1StockBox.Text, out int stock1) || stock1 < 0 ||
                !int.TryParse(Drink2StockBox.Text, out int stock2) || stock2 < 0 ||
                !int.TryParse(Drink3StockBox.Text, out int stock3) || stock3 < 0 ||
                !int.TryParse(Drink4StockBox.Text, out int stock4) || stock4 < 0 ||
                !int.TryParse(Drink5StockBox.Text, out int stock5) || stock5 < 0 ||
                !int.TryParse(Drink6StockBox.Text, out int stock6) || stock6 < 0 ||
                !int.TryParse(Drink7StockBox.Text, out int stock7) || stock7 < 0 ||
                !int.TryParse(Drink8StockBox.Text, out int stock8) || stock8 < 0 ||
                !int.TryParse(Bar1StockBox.Text, out int stock9) || stock9 < 0 ||
                !int.TryParse(Bar2StockBox.Text, out int stock10) || stock10 < 0)
            {
                StatusText.Text = "ENTER VALID STOCK (0 OR MORE)";
                return;
            }

            isSaving = true;
            SaveStockButton.IsEnabled = false;

            try
            {
                // Save milk stock
                if (!Drinks.SetMilkStock(milkStock))
                {
                    StatusText.Text = "MILK STOCK SAVE FAILED";
                    return;
                }

                // Save drink and bar stocks
                bool saved =
                    Drinks.SetStock(1, stock1) &&
                    Drinks.SetStock(2, stock2) &&
                    Drinks.SetStock(3, stock3) &&
                    Drinks.SetStock(4, stock4) &&
                    Drinks.SetStock(5, stock5) &&
                    Drinks.SetStock(6, stock6) &&
                    Drinks.SetStock(7, stock7) &&
                    Drinks.SetStock(8, stock8) &&
                    Drinks.SetStock(9, stock9) &&
                    Drinks.SetStock(10, stock10);

                if (!saved)
                {
                    StatusText.Text = "SAVE FAILED - PLEASE TRY AGAIN";
                    return;
                }

                // Sync all updated drink, bar, and milk stocks to VendiBite API
                _ = Task.Run(async () => await VendiBiteService.UpdateStockAsync());

                // Show success message before leaving
                StatusText.Text = "STOCK SAVED SUCCESSFULLY";

                // Keep the message visible for 1.5 seconds
                await Task.Delay(1500);

                // Return to Admin Page
                BackRequested?.Invoke();
            }
            finally
            {
                isSaving = false;
                SaveStockButton.IsEnabled = true;
            }
        }
    }
}
