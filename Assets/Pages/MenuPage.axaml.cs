using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaApplication2.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace AvaloniaApplication2.Assets.Pages;

public partial class MenuPage : UserControl
{
    public event Action? BackRequested;
    public event Action? ContinueRequested;

    private int selectedDrinkId = 0;

    private const double CardWidth = 306;
    private const double CardHeight = 380;
    private const double HorizontalGap = 20;
    private const double VerticalGap = 25;
    private const double FirstRowTop = 20;

    private const double InactivityTime = 2 * 60 * 1000;

    private const string SelectedBadgeName = "SelectedBadge";

    private readonly Timer inactivityTimer;

    // ============================================================
    // NUTRIPOD THEME COLOURS
    //   Black  = card surface
    //   Lime   = selection / primary accent
    //   Orange = price / secondary accent
    // ============================================================

    private static readonly IBrush NormalBackground =
        Brush.Parse("#121212");

    private static readonly IBrush NormalBorder =
        Brush.Parse("#3A3A3A");

    private static readonly IBrush SelectedBackground =
        Brush.Parse("#072B14");

    private static readonly IBrush LimeBrush =
        Brush.Parse("#00A844");

    private static readonly IBrush DisabledBackground =
        Brush.Parse("#1A1A1A");

    private static readonly IBrush DisabledForeground =
        Brush.Parse("#6A6A6A");

    public MenuPage()
    {
        InitializeComponent();

        inactivityTimer = new Timer(InactivityTime);
        inactivityTimer.AutoReset = false;
        inactivityTimer.Elapsed += InactivityTimer_Elapsed;

        CreateDrinkCards();

        ResetInactivityTimer();
    }

    // ============================================================
    // GET ALL DRINKS
    // Drinks first, Protain Bars always after the drinks.
    // (Drink 8 is disabled, so it is filtered out below.)
    // ============================================================

    private List<DrinkData> GetAllDrinks()
    {
        return new List<DrinkData>
        {
            Drink.First,
            Drink.Second,
            Drink.Third,
            Drink.Fourth,
            Drink.Fifth,
            Drink.Sixth,
            Drink.Seventh,
            Drink.Eighth,

            Bar.First,
            Bar.Second
        };
    }

    // ============================================================
    // CHECK DRINK AVAILABILITY
    // ============================================================

    private bool IsDrinkAvailable(DrinkData drink)
    {
        // Admin disabled the drink.
        if (!drink.Enabled)
            return false;

        // Drink must have stock remaining.
        return Drinks.GetStock(drink.Id) > 0;
    }

    // ============================================================
    // CREATE DRINK CARDS
    // ============================================================

    private void CreateDrinkCards()
    {
        DrinkCardsCanvas.Children.Clear();

        List<DrinkData> allDrinks = GetAllDrinks();

        // Hide drinks / bars disabled in Admin settings.
        List<DrinkData> visibleDrinks = allDrinks
            .Where(drink => drink.Enabled)
            .ToList();

        int totalRows = (visibleDrinks.Count + 2) / 3;
        DrinkCardsCanvas.Height = Math.Max(1290, FirstRowTop + totalRows * (CardHeight + VerticalGap) + 20);

        int firstAvailableDrinkId = 0;

        for (int i = 0; i < visibleDrinks.Count; i++)
        {
            DrinkData drink = visibleDrinks[i];

            Button card = CreateDrinkCard(drink);

            // Remember the first enabled drink that has stock.
            if (firstAvailableDrinkId == 0 &&
                IsDrinkAvailable(drink))
            {
                firstAvailableDrinkId = drink.Id;
            }

            // Use the visible-card index so hidden drinks
            // do not leave empty spaces in the layout.
            int row = i / 3;
            int column = i % 3;

            // Centre each row according to how many cards it holds
            // (3 cards = full row, fewer = centred).
            int cardsInRow = Math.Min(3, visibleDrinks.Count - (row * 3));

            double rowWidth =
                (CardWidth * cardsInRow) +
                (HorizontalGap * (cardsInRow - 1));

            double rowStartX = (1080 - rowWidth) / 2;

            double left = rowStartX +
                          column * (CardWidth + HorizontalGap);

            double top = FirstRowTop +
                         row * (CardHeight + VerticalGap);

            Canvas.SetLeft(card, left);
            Canvas.SetTop(card, top);

            DrinkCardsCanvas.Children.Add(card);
        }

        // Default on opening menu: NO drink is selected.
        if (CurrentOrder.DrinkId > 0 && IsDrinkAvailable(GetDrinkById(CurrentOrder.DrinkId)))
        {
            selectedDrinkId = CurrentOrder.DrinkId;
        }
        else
        {
            selectedDrinkId = 0;
        }

        UpdateCardSelection();

        // Prevent NEXT when no product is selected yet.
        ContinueButton.IsEnabled = selectedDrinkId > 0;
        ContinueButton.Opacity = selectedDrinkId > 0 ? 1.0 : 0.4;
    }

    // ============================================================
    // CREATE SINGLE DRINK CARD
    // ============================================================

    private Button CreateDrinkCard(DrinkData drink)
    {
        int stock = Drinks.GetStock(drink.Id);

        bool outOfStock = stock <= 0;

        Button button = new Button
        {
            Classes = { "drinkCard" },

            Width = CardWidth,
            Height = CardHeight,

            Background = outOfStock
                ? DisabledBackground
                : NormalBackground,

            BorderBrush = NormalBorder,
            BorderThickness = new Thickness(3),
            Padding = new Thickness(0),

            RenderTransformOrigin =
                new RelativePoint(0.5, 0.5, RelativeUnit.Relative),

            Tag = drink.Id,

            // Out-of-stock drinks cannot be selected.
            IsEnabled = !outOfStock,

            Opacity = outOfStock ? 0.85 : 1.0
        };

        Grid grid = new Grid();

        // ========================================================
        // DRINK IMAGE
        // ========================================================

        Image image = new Image
        {
            Width = 260,
            Height = 255,

            Stretch = Stretch.Uniform,

            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Center,

            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Top,

            Margin = new Thickness(0, 10, 0, 0),

            Opacity = outOfStock ? 0.3 : 1.0
        };

        try
        {
            image.Source = DrinkImageService.LoadDrinkBitmap(drink);
        }
        catch
        {
            image.Source = null;
        }

        grid.Children.Add(image);

        // ========================================================
        // DRINK NAME
        // ========================================================

        TextBlock name = new TextBlock
        {
            Text = drink.Name,

            FontSize = 21,
            FontWeight = FontWeight.Bold,

            Foreground = outOfStock
                ? DisabledForeground
                : Brushes.White,

            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Center,

            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Top,

            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,

            Margin = new Thickness(8, 275, 8, 0)
        };

        grid.Children.Add(name);

        // ========================================================
        // PRICE (ORANGE ACCENT)
        // ========================================================

        TextBlock price = new TextBlock
        {
            Text = $"RS. {drink.Price}",

            FontSize = 23,
            FontWeight = FontWeight.Black,

            Foreground = outOfStock
                ? DisabledForeground
                : LimeBrush,

            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Center,

            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Top,

            TextAlignment = TextAlignment.Center,

            Margin = new Thickness(8, 325, 8, 0)
        };

        grid.Children.Add(price);

        // ========================================================
        // SELECTED BADGE (LIME TICK, TOP RIGHT)
        // ========================================================

        Border badge = new Border
        {
            Name = SelectedBadgeName,

            Width = 34,
            Height = 34,

            CornerRadius = new CornerRadius(17),

            Background = LimeBrush,

            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Right,

            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Top,

            Margin = new Thickness(0, 10, 10, 0),

            IsVisible = false,

            Child = new TextBlock
            {
                Text = "✓",
                FontSize = 20,
                FontWeight = FontWeight.Black,
                Foreground = Brush.Parse("#0A0A0A"),

                HorizontalAlignment =
                    Avalonia.Layout.HorizontalAlignment.Center,

                VerticalAlignment =
                    Avalonia.Layout.VerticalAlignment.Center
            }
        };

        grid.Children.Add(badge);

        // ========================================================
        // OUT OF STOCK LABEL
        // ========================================================

        if (outOfStock)
        {
            Border stockOverlay = new Border
            {
                Background = Brush.Parse("#E60A0A0A"),

                BorderBrush = LimeBrush,
                BorderThickness = new Thickness(2),

                CornerRadius = new CornerRadius(8),

                Padding = new Thickness(14, 10),

                HorizontalAlignment =
                    Avalonia.Layout.HorizontalAlignment.Center,

                VerticalAlignment =
                    Avalonia.Layout.VerticalAlignment.Center
            };

            TextBlock stockText = new TextBlock
            {
                Text = "OUT OF STOCK",

                FontSize = 20,
                FontWeight = FontWeight.Black,

                Foreground = LimeBrush,

                TextAlignment = TextAlignment.Center
            };

            stockOverlay.Child = stockText;

            grid.Children.Add(stockOverlay);
        }

        button.Content = grid;

        button.Click += DrinkCard_Click;

        return button;
    }

    // ============================================================
    // DRINK CARD CLICK
    // ============================================================

    private void DrinkCard_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ResetInactivityTimer();

        if (sender is not Button button)
            return;

        if (button.Tag is not int drinkId)
            return;

        DrinkData drink = GetDrinkById(drinkId);

        // Recheck admin setting and stock before selection.
        if (!IsDrinkAvailable(drink))
            return;

        SelectDrink(drinkId);
    }

    // ============================================================
    // SELECT DRINK
    // ============================================================

    private void SelectDrink(int drinkId)
    {
        DrinkData drink = GetDrinkById(drinkId);

        if (!IsDrinkAvailable(drink))
            return;

        selectedDrinkId = drinkId;

        // Enable NEXT button
        ContinueButton.IsEnabled = true;
        ContinueButton.Opacity = 1.0;

        UpdateCardSelection();
    }

    // ============================================================
    // UPDATE CARD SELECTION VISUAL
    // ============================================================

    private void UpdateCardSelection()
    {
        foreach (Control child in DrinkCardsCanvas.Children)
        {
            if (child is not Button button)
                continue;

            if (button.Tag is not int drinkId)
                continue;

            bool isSelected = selectedDrinkId > 0 && drinkId == selectedDrinkId;

            if (isSelected)
            {
                button.Background = SelectedBackground;
                button.BorderBrush = LimeBrush;
                button.BorderThickness = new Thickness(3);
            }
            else
            {
                bool disabled = !button.IsEnabled;

                button.Background = disabled
                    ? DisabledBackground
                    : NormalBackground;

                button.BorderBrush = NormalBorder;
                button.BorderThickness = new Thickness(3);
            }

            // Show / hide the lime tick badge.
            if (button.Content is Grid grid)
            {
                Border? badge = grid.Children
                    .OfType<Border>()
                    .FirstOrDefault(b => b.Name == SelectedBadgeName);

                if (badge != null)
                    badge.IsVisible = isSelected;
            }
        }
    }

    // ============================================================
    // GET DRINK BY ID
    // ============================================================

    private DrinkData GetDrinkById(int id)
    {
        return id switch
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
            _ => Drink.First
        };
    }

    // ============================================================
    // BACK BUTTON
    // ============================================================

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ResetInactivityTimer();

        BackRequested?.Invoke();
    }

    // ============================================================
    // CONTINUE BUTTON
    // ============================================================

    private void ContinueButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ResetInactivityTimer();

        if (selectedDrinkId <= 0)
            return;

        DrinkData drink = GetDrinkById(selectedDrinkId);

        // Final safety check before continuing.
        if (!IsDrinkAvailable(drink))
        {
            CreateDrinkCards();
            return;
        }

        // Bars have no Water / Milk base.
        CurrentOrder.SetOrder(drink, drink.IsBar ? "None" : "Water");

        ContinueRequested?.Invoke();
    }

    // ============================================================
    // RESET INACTIVITY TIMER
    // ============================================================

    private void ResetInactivityTimer()
    {
        if (!inactivityTimer.Enabled)
        {
            inactivityTimer.Start();
        }
        else
        {
            inactivityTimer.Stop();
            inactivityTimer.Start();
        }
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
            BackRequested?.Invoke();
        });
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