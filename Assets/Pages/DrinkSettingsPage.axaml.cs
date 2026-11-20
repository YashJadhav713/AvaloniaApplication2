using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaApplication2;
using AvaloniaApplication2.Services;
using System;

namespace AvaloniaApplication2.Assets.Pages;

public partial class DrinkSettingsPage : UserControl
{
    public event Action? BackRequested;

    public event Action<int>? DrinkSelectedRequested;

    // Drinks are IDs 1-8, Protain Bars are IDs 9-10.
    private const int LastItemId = 10;

    // COLORS (theme)

    private static readonly IBrush CardBrush =
        Brush.Parse("#121212");

    private static readonly IBrush WhiteBrush =
        Brush.Parse("#FFFFFF");

    private static readonly IBrush SoftWhiteBrush =
        Brush.Parse("#99FFFFFF");

    private static readonly IBrush LimeBrush =
        Brush.Parse("#00A844");

    private static readonly IBrush DefaultBorderBrush =
        Brush.Parse("#005C25");

    private static readonly IBrush DisabledTextBrush =
        Brush.Parse("#707070");

    // CONSTRUCTOR

    public DrinkSettingsPage()
    {
        InitializeComponent();

        BackButton.Click += BackButton_Click;

        SlideUpButton.Click += (s, e) =>
        {
            DrinksScrollViewer.Offset = new Vector(0, Math.Max(0, DrinksScrollViewer.Offset.Y - 500));
        };

        SlideDownButton.Click += (s, e) =>
        {
            DrinksScrollViewer.Offset = new Vector(0, DrinksScrollViewer.Offset.Y + 500);
        };

        RefreshDrinks();
    }

    // REFRESH ALL DRINKS (1-8) AND BARS (9-10)

    public void RefreshDrinks()
    {
        DrinksPanel.Children.Clear();

        for (int drinkId = 1; drinkId <= LastItemId; drinkId++)
        {
            DrinkData? drink = GetDrink(drinkId);

            if (drink == null)
                continue;

            DrinksPanel.Children.Add(
                CreateDrinkCard(drink));
        }
    }

    // CREATE DRINK / BAR CARD

    private Button CreateDrinkCard(DrinkData drink)
    {
        int drinkId = drink.Id;

        Button card = new Button
        {
            Classes = { "settingsCard" },

            Width = 900,
            Height = 280,
            Margin = new Thickness(0, 0, 0, 15),

            Padding = new Thickness(20),

            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Center,

            HorizontalContentAlignment =
                Avalonia.Layout.HorizontalAlignment.Stretch,

            VerticalContentAlignment =
                Avalonia.Layout.VerticalAlignment.Center
        };

        // CARD LAYOUT

        Grid content = new Grid
        {
            ColumnDefinitions =
                new ColumnDefinitions("220,*"),

            RowDefinitions =
                new RowDefinitions("*,*,*,*,*")
        };

        // FIXED IMAGE FROM THE ITEM'S IMAGE PATH
        // (Drink1.png ... Drink8.png, Bar1.png, Bar2.png)

        Image image = new Image
        {
            Width = 190,
            Height = 250,

            Stretch = Stretch.Uniform,

            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Center,

            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Center
        };

        try
        {
            image.Source = DrinkImageService.LoadDrinkBitmap(drink);
        }
        catch
        {
            // Keep the card available if its image is missing.
        }

        Grid.SetColumn(image, 0);
        Grid.SetRowSpan(image, 5);

        content.Children.Add(image);

        // FIXED ID (lime)  -  "DRINK ID" or "BAR ID"

        TextBlock idText = new TextBlock
        {
            Text = drink.IsBar
                ? $"BAR ID: {drinkId}"
                : $"DRINK ID: {drinkId}",

            FontFamily = "Cascadia Mono",
            FontSize = 24,
            FontWeight = FontWeight.Black,

            Foreground = LimeBrush,

            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Center
        };

        Grid.SetColumn(idText, 1);
        Grid.SetRow(idText, 0);

        content.Children.Add(idText);

        // EDITABLE NAME

        TextBlock nameText = new TextBlock
        {
            Text = $"NAME: {drink.Name}",

            FontFamily = "Cascadia Mono",
            FontSize = 20,
            FontWeight = FontWeight.Bold,

            Foreground = WhiteBrush,

            TextWrapping = TextWrapping.Wrap,

            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Center
        };

        Grid.SetColumn(nameText, 1);
        Grid.SetRow(nameText, 1);

        content.Children.Add(nameText);

        // PRICE

        TextBlock priceText = new TextBlock
        {
            Text = $"PRICE: {drink.Price}",

            FontFamily = "Cascadia Mono",
            FontSize = 22,

            Foreground = WhiteBrush,

            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Center
        };

        Grid.SetColumn(priceText, 1);
        Grid.SetRow(priceText, 2);

        content.Children.Add(priceText);

        // ENABLED STATUS

        TextBlock statusText = new TextBlock
        {
            Text = drink.Enabled
                ? "STATUS: ON"
                : "STATUS: OFF",

            FontFamily = "Cascadia Mono",
            FontSize = 22,
            FontWeight = FontWeight.Bold,

            Foreground = drink.Enabled
                ? LimeBrush
                : DisabledTextBrush,

            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Center
        };

        Grid.SetColumn(statusText, 1);
        Grid.SetRow(statusText, 3);

        content.Children.Add(statusText);

        // EDIT INSTRUCTION

        TextBlock instructionText = new TextBlock
        {
            Text = "TAP TO EDIT",

            FontFamily = "Cascadia Mono",
            FontSize = 18,

            Foreground = SoftWhiteBrush,

            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Center
        };

        Grid.SetColumn(instructionText, 1);
        Grid.SetRow(instructionText, 4);

        content.Children.Add(instructionText);

        card.Content = content;

        // OPEN EDIT PAGE USING FIXED ID

        card.Click += (sender, e) =>
        {
            DrinkSelectedRequested?.Invoke(drinkId);
        };

        return card;
    }

    // GET DRINK / BAR BY FIXED ID

    private static DrinkData? GetDrink(int drinkId)
    {
        DrinkData[] drinks =
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

        foreach (DrinkData drink in drinks)
        {
            if (drink.Id == drinkId)
                return drink;
        }

        return null;
    }

    // BACK BUTTON

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }
}
