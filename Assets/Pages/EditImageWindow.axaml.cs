using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AvaloniaApplication2.Services;
using System;
using System.IO;

namespace AvaloniaApplication2.Assets.Pages;

public partial class EditImageWindow : Window
{
    private int _drinkId;
    private bool _isBar;

    private string? _pendingImagePath;
    private Bitmap? _previewBitmap;
    private bool _initialized;

    // "DRINK" for drinks (IDs 1-8), "BAR" for Protein Bars (IDs 9-10)
    private string ItemWord => _isBar ? "BAR" : "DRINK";

    public EditImageWindow()
    {
        InitializeComponent();

        NewImageButton.Click += NewImageButton_Click;
        ResetDefaultButton.Click += ResetDefaultButton_Click;
        SaveButton.Click += SaveButton_Click;
        CancelButton.Click += CancelButton_Click;

        Closed += EditImageWindow_Closed;
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
    // LOAD IMAGE FOR SELECTED DRINK / BAR
    // ============================================================

    public void LoadDrink(int drinkId)
    {
        DrinkData? drink = GetDrink(drinkId);

        if (drink == null ||
            string.IsNullOrWhiteSpace(drink.Image))
        {
            _initialized = false;
            StatusText.Text = "INVALID DRINK ID";
            SaveButton.IsEnabled = false;
            ResetDefaultButton.IsEnabled = false;
            return;
        }

        _drinkId = drink.Id;
        _isBar = drink.IsBar;

        _pendingImagePath = null;
        _initialized = true;

        DrinkIdText.Text = $"{ItemWord} ID: {_drinkId} - {drink.Name.ToUpperInvariant()}";

        SaveButton.IsEnabled = false;
        NewImageButton.IsEnabled = true;

        LoadCurrentImage();

        StatusText.Text = "SELECT A NEW IMAGE TO REPLACE";
    }

    // ============================================================
    // LOAD CURRENT IMAGE
    // ============================================================

    private void LoadCurrentImage()
    {
        try
        {
            DrinkData? drink = GetDrink(_drinkId);
            Bitmap? bmp = DrinkImageService.LoadDrinkBitmap(drink);

            if (bmp != null)
            {
                SetPreview(bmp);
            }
            else
            {
                DrinkImagePreview.Source = null;
                _previewBitmap?.Dispose();
                _previewBitmap = null;
            }

            ResetDefaultButton.IsEnabled = DrinkImageService.HasCustomImage(drink);
        }
        catch (Exception ex)
        {
            DrinkImagePreview.Source = null;
            _previewBitmap?.Dispose();
            _previewBitmap = null;
            StatusText.Text = $"IMAGE LOAD FAILED: {ex.Message}";
        }
    }

    // ============================================================
    // UPDATE PREVIEW
    // ============================================================

    private void SetPreview(Bitmap bitmap)
    {
        Bitmap? oldBitmap = _previewBitmap;

        _previewBitmap = bitmap;
        DrinkImagePreview.Source = bitmap;

        oldBitmap?.Dispose();
    }

    // ============================================================
    // SELECT NEW IMAGE
    // ============================================================

    private async void NewImageButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (!_initialized)
            return;

        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = $"SELECT {ItemWord} IMAGE",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Image files")
                        {
                            Patterns =
                            [
                                "*.png",
                                "*.jpg",
                                "*.jpeg",
                                "*.bmp",
                                "*.webp"
                            ]
                        }
                    ]
                });

            if (files.Count == 0)
                return;

            string selectedPath = files[0].TryGetLocalPath()
                ?? throw new InvalidOperationException(
                    "The selected image is not a local file.");

            using (FileStream stream = File.OpenRead(selectedPath))
            {
                SetPreview(new Bitmap(stream));
            }

            _pendingImagePath = selectedPath;

            SaveButton.IsEnabled = true;
            StatusText.Text = "NEW IMAGE SELECTED (CLICK SAVE TO APPLY)";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"IMAGE SELECTION FAILED: {ex.Message}";
        }
    }

    // ============================================================
    // RESET TO DEFAULT
    // ============================================================

    private void ResetDefaultButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (!_initialized)
            return;

        try
        {
            DrinkData? drink = GetDrink(_drinkId);
            if (drink != null)
            {
                DrinkImageService.ResetToDefaultImage(drink);
                _pendingImagePath = null;
                SaveButton.IsEnabled = false;
                LoadCurrentImage();
                StatusText.Text = "IMAGE RESTORED TO DEFAULT";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"RESET FAILED: {ex.Message}";
        }
    }

    // ============================================================
    // SAVE IMAGE
    // ============================================================

    private void SaveButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (!_initialized ||
            string.IsNullOrWhiteSpace(_pendingImagePath))
        {
            StatusText.Text = "SELECT AN IMAGE FIRST";
            return;
        }

        SaveButton.IsEnabled = false;
        NewImageButton.IsEnabled = false;
        ResetDefaultButton.IsEnabled = false;

        try
        {
            DrinkData? drink = GetDrink(_drinkId);
            if (drink == null)
                throw new InvalidOperationException("Invalid drink item.");

            DrinkImageService.SaveCustomImage(drink, _pendingImagePath);

            _pendingImagePath = null;
            StatusText.Text = "IMAGE SAVED";

            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"SAVE FAILED: {ex.Message}";

            SaveButton.IsEnabled = true;
            NewImageButton.IsEnabled = true;
            ResetDefaultButton.IsEnabled = DrinkImageService.HasCustomImage(GetDrink(_drinkId));
        }
    }

    // ============================================================
    // CANCEL
    // ============================================================

    private void CancelButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        _pendingImagePath = null;
        Close();
    }

    // ============================================================
    // CLEANUP
    // ============================================================

    private void EditImageWindow_Closed(
        object? sender,
        EventArgs e)
    {
        DrinkImagePreview.Source = null;

        _previewBitmap?.Dispose();
        _previewBitmap = null;
    }
}
