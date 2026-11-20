using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaApplication2.Services;
using System;
using System.Globalization;
using System.Timers;

namespace AvaloniaApplication2.Assets.Pages
{
    public partial class AddOnsPage : UserControl
    {
        // ============================================================
        // THEME COLOURS (black, lime, orange - white for text only)
        // ============================================================

        private static readonly IBrush BlackBrush =
            Brush.Parse("#0A0A0A");

        private static readonly IBrush LimeBrush =
            Brush.Parse("#00A844");

        private static readonly IBrush OrangeBrush =
            Brush.Parse("#008A37");

        private static readonly IBrush WhiteBrush =
            Brushes.White;


        // ============================================================
        // TOTAL PRICE BOX POSITIONS
        //   Drinks : below the Water / Milk buttons
        //   Bars   : moved up into the space of the hidden base section
        // ============================================================

        private const double TotalPriceTopDrink = 1465;
        private const double TotalPriceTopBar = 1265;


        // ============================================================
        // SELECTED BASE
        //   "Water" / "Milk" for drinks, "None" for Protain Bars
        // ============================================================

        private string selectedBase = "Water";


        // ============================================================
        // EVENTS
        // ============================================================

        public event Action? BackRequested;

        public event Action? ContinueRequested;

        public event Action? HomeRequested;


        // ============================================================
        // INACTIVITY TIMER
        // ============================================================

        private const double InactivityTime = 2 * 60 * 1000;

        private readonly Timer inactivityTimer;


        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public AddOnsPage()
        {
            InitializeComponent();

            WaterButton.Click += WaterButton_Click;
            MilkButton.Click += MilkButton_Click;
            BackButton.Click += BackButton_Click;
            ContinueButton.Click += ContinueButton_Click;

            selectedBase = "Water";

            inactivityTimer = new Timer(InactivityTime);
            inactivityTimer.AutoReset = false;
            inactivityTimer.Elapsed += InactivityTimer_Elapsed;

            UpdatePage();

            ResetInactivityTimer();
        }


        // ============================================================
        // IS CURRENT ITEM A PROTAIN BAR
        // ============================================================

        private bool IsBarOrder()
        {
            return CurrentOrder.Drink != null &&
                   CurrentOrder.Drink.IsBar;
        }


        // ============================================================
        // WATER BUTTON
        // ============================================================

        private void WaterButton_Click(object? sender, RoutedEventArgs e)
        {
            ResetInactivityTimer();

            // Bars have no base option.
            if (IsBarOrder())
                return;

            SelectBase("Water");
        }


        // ============================================================
        // MILK BUTTON
        // ============================================================

        private void MilkButton_Click(object? sender, RoutedEventArgs e)
        {
            ResetInactivityTimer();

            // Bars have no base option.
            if (IsBarOrder())
                return;

            if (!Drinks.MilkEnabled)
                return;

            if (Drinks.GetMilkStock() <= 0)
                return;

            SelectBase("Milk");
        }


        // ============================================================
        // SELECT BASE
        // ============================================================

        private void SelectBase(string baseType)
        {
            ResetInactivityTimer();

            // Bars have no base option.
            if (IsBarOrder())
                return;

            // Milk must be enabled
            if (baseType == "Milk" && !Drinks.MilkEnabled)
            {
                baseType = "Water";
            }

            // Milk must have available stock
            if (baseType == "Milk" && Drinks.GetMilkStock() <= 0)
            {
                baseType = "Water";
            }

            selectedBase = baseType;

            if (CurrentOrder.Drink != null)
            {
                CurrentOrder.SetOrder(
                    CurrentOrder.Drink,
                    selectedBase);
            }

            UpdateNutrition();
            UpdateBaseSelection();
            UpdatePrice();
        }


        // ============================================================
        // UPDATE PAGE
        // ============================================================

        private void UpdatePage()
        {
            if (CurrentOrder.Drink == null)
                return;

            selectedBase = IsBarOrder() ? "None" : "Water";

            CurrentOrder.SetOrder(
                CurrentOrder.Drink,
                selectedBase);

            UpdateDrink();
            UpdateNutrition();
            UpdateBaseSelection();
            UpdatePrice();
        }


        // ============================================================
        // UPDATE DRINK
        // ============================================================

        private void UpdateDrink()
        {
            DrinkData? drink = CurrentOrder.Drink;

            if (drink == null)
                return;

            DrinkNameText.Text = drink.Name;

            try
            {
                DrinkImage.Source = DrinkImageService.LoadDrinkBitmap(drink);
            }
            catch
            {
                DrinkImage.Source = null;
            }
        }


        // ============================================================
        // UPDATE NUTRITION
        // ============================================================

        private void UpdateNutrition()
        {
            DrinkData? drink = CurrentOrder.Drink;

            if (drink == null)
                return;

            double protein =
                ParseNutritionValue(drink.Nutrition.Protein);

            double calories =
                ParseNutritionValue(drink.Nutrition.Calories);

            double bcaa =
                ParseNutritionValue(drink.Nutrition.BCAA);

            double totalSugars =
                ParseNutritionValue(drink.Nutrition.TotalSugars);

            // Add milk nutrition only when Milk is selected,
            // enabled, and in stock. (Never for a bar.)
            if (!drink.IsBar &&
                selectedBase == "Milk" &&
                Drinks.MilkEnabled &&
                Drinks.GetMilkStock() > 0)
            {
                protein += Drinks.Milk.Protein;
                calories += Drinks.Milk.Calories;
                bcaa += Drinks.Milk.BCAA;
                totalSugars += Drinks.Milk.TotalSugars;
            }

            ServingValue.Text = drink.Nutrition.Serving;

            ProteinValue.Text = FormatNumber(protein) + " g";
            CaloriesValue.Text = FormatNumber(calories) + " kcal";
            BcaaValue.Text = FormatNumber(bcaa) + " g";
            SugarValue.Text = FormatNumber(totalSugars) + " g";
        }


        // ============================================================
        // GET DRINK ML
        // ============================================================

        private int GetDrinkML()
        {
            DrinkData? drink = CurrentOrder.Drink;

            if (drink == null)
                return 0;

            string serving = drink.Nutrition.Serving;

            if (string.IsNullOrWhiteSpace(serving))
                return 0;

            string number = "";

            foreach (char c in serving)
            {
                if (char.IsDigit(c))
                {
                    number += c;
                }
                else if (number.Length > 0)
                {
                    break;
                }
            }

            if (int.TryParse(number, out int ml))
                return ml;

            return 0;
        }


        // ============================================================
        // STYLE HELPERS
        // ============================================================

        // SELECTED   : lime fill, black text
        // UNSELECTED : black fill, orange border, white text
        private void StyleWater(bool selected)
        {
            WaterButton.Background =
                selected ? LimeBrush : BlackBrush;

            WaterButton.BorderBrush =
                selected ? LimeBrush : OrangeBrush;

            WaterTitleText.Foreground =
                selected ? BlackBrush : WhiteBrush;

            WaterSubText.Foreground =
                selected ? BlackBrush : OrangeBrush;
        }

        private void StyleMilk(bool selected)
        {
            MilkButton.Background =
                selected ? LimeBrush : BlackBrush;

            MilkButton.BorderBrush =
                selected ? LimeBrush : OrangeBrush;

            MilkTitleText.Foreground =
                selected ? BlackBrush : WhiteBrush;

            MilkPriceText.Foreground =
                selected ? BlackBrush : OrangeBrush;
        }


        // ============================================================
        // UPDATE BASE SELECTION
        // ============================================================

        private void UpdateBaseSelection()
        {
            // ========================================================
            // PROTAIN BAR : NO WATER / MILK OPTION
            // ========================================================

            if (IsBarOrder())
            {
                selectedBase = "None";

                // Hide the whole base section.
                BaseTitleBox.IsVisible = false;
                WaterButton.IsVisible = false;
                MilkButton.IsVisible = false;

                // Move the total price up to close the gap.
                Canvas.SetTop(TotalPriceBox, TotalPriceTopBar);

                return;
            }

            // ========================================================
            // DRINK : SHOW BASE SECTION
            // ========================================================

            BaseTitleBox.IsVisible = true;
            WaterButton.IsVisible = true;

            Canvas.SetTop(TotalPriceBox, TotalPriceTopDrink);

            // ========================================================
            // MILK ENABLED
            // ========================================================

            if (Drinks.MilkEnabled)
            {
                MilkButton.IsVisible = true;

                Canvas.SetLeft(WaterButton, 60);

                bool milkOutOfStock = Drinks.GetMilkStock() <= 0;

                if (milkOutOfStock)
                {
                    // Milk is enabled, but stock is zero.
                    MilkPriceText.Text = "OUT OF STOCK";

                    MilkButton.IsEnabled = false;
                    MilkButton.Opacity = 0.45;

                    // Keep Water selected.
                    selectedBase = "Water";

                    StyleWater(true);
                    StyleMilk(false);
                }
                else
                {
                    MilkButton.IsEnabled = true;
                    MilkButton.Opacity = 1.0;

                    MilkPriceText.Text =
                        "+ " + Drinks.MilkPrice + "/-";

                    bool waterSelected = selectedBase == "Water";

                    StyleWater(waterSelected);
                    StyleMilk(!waterSelected);
                }
            }

            // ========================================================
            // MILK DISABLED
            // ========================================================

            else
            {
                // Preserve the existing Milk OFF behavior.
                selectedBase = "Water";

                MilkButton.IsVisible = false;

                // Reset this for a future page instance if needed.
                MilkButton.IsEnabled = true;
                MilkButton.Opacity = 1.0;

                Canvas.SetLeft(WaterButton, 307);

                StyleWater(true);
            }
        }


        // ============================================================
        // UPDATE PRICE
        // ============================================================

        private void UpdatePrice()
        {
            DrinkData? drink = CurrentOrder.Drink;

            if (drink == null)
                return;

            string price = Drinks.GetOrderPrice(
                drink,
                selectedBase);

            PriceText.Text = "Total : Rs. " + price;
        }


        // ============================================================
        // PREVIOUS
        // ============================================================

        private void BackButton_Click(object? sender, RoutedEventArgs e)
        {
            ResetInactivityTimer();

            BackRequested?.Invoke();
        }


        // ============================================================
        // NEXT
        // ============================================================

        private void ContinueButton_Click(object? sender, RoutedEventArgs e)
        {
            ResetInactivityTimer();

            DrinkData? drink = CurrentOrder.Drink;

            if (drink == null)
                return;

            if (drink.IsBar)
            {
                // Bars have no base.
                selectedBase = "None";
            }
            else if (!Drinks.MilkEnabled || Drinks.GetMilkStock() <= 0)
            {
                // Never continue with Milk if it is disabled or out of stock.
                selectedBase = "Water";
            }

            CurrentOrder.SetOrder(drink, selectedBase);

            ContinueRequested?.Invoke();
        }


        // ============================================================
        // RESET INACTIVITY TIMER
        // ============================================================

        private void ResetInactivityTimer()
        {
            if (inactivityTimer.Enabled)
            {
                inactivityTimer.Stop();
            }

            inactivityTimer.Start();
        }


        // ============================================================
        // INACTIVITY TIMER
        // ============================================================

        private void InactivityTimer_Elapsed(
            object? sender,
            ElapsedEventArgs e)
        {
            inactivityTimer.Stop();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                HomeRequested?.Invoke();
            });
        }


        // ============================================================
        // PARSE NUTRITION VALUE
        // ============================================================

        private double ParseNutritionValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            string number = "";

            foreach (char c in value)
            {
                if (char.IsDigit(c) || c == '.' || c == ',')
                {
                    number += c;
                }
                else if (number.Length > 0)
                {
                    break;
                }
            }

            if (double.TryParse(
                number.Replace(',', '.'),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double result))
            {
                return result;
            }

            return 0;
        }


        // ============================================================
        // FORMAT NUMBER
        // ============================================================

        private string FormatNumber(double value)
        {
            if (Math.Abs(value % 1) < 0.001)
            {
                return value.ToString(
                    "0",
                    CultureInfo.InvariantCulture);
            }

            return value.ToString(
                "0.##",
                CultureInfo.InvariantCulture);
        }


        // ============================================================
        // CLEANUP
        // ============================================================

        protected override void OnDetachedFromVisualTree(
            VisualTreeAttachmentEventArgs e)
        {
            inactivityTimer.Stop();
            inactivityTimer.Dispose();

            base.OnDetachedFromVisualTree(e);
        }
    }
}
