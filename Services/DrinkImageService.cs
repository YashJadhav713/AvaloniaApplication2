using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;

namespace AvaloniaApplication2.Services;

/// <summary>
/// Centralized service for loading and managing drink and bar images.
/// Checks the local application data directory for custom images saved by the administrator,
/// falling back to the bundled application assets.
/// </summary>
public static class DrinkImageService
{
    public static string ImageDirectory
    {
        get
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SHAKE LAB",
                "DrinkImages");

            try
            {
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
            }
            catch
            {
                // Fallback gracefully if directory cannot be created
            }

            return directory;
        }
    }

    public static string? GetCustomImagePath(DrinkData? drink)
    {
        if (drink == null || string.IsNullOrWhiteSpace(drink.Image))
            return null;

        string fileName = Path.GetFileName(drink.Image);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return Path.Combine(ImageDirectory, fileName);
    }

    public static bool HasCustomImage(DrinkData? drink)
    {
        string? path = GetCustomImagePath(drink);
        return path != null && File.Exists(path);
    }

    public static bool ResetToDefaultImage(DrinkData? drink)
    {
        try
        {
            string? path = GetCustomImagePath(drink);
            if (path != null && File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch
        {
            // Ignore deletion errors
        }

        return false;
    }

    public static Bitmap? LoadDrinkBitmap(DrinkData? drink)
    {
        if (drink == null)
            return null;

        // 1. Check for custom administrator-uploaded image in local storage
        try
        {
            string? customPath = GetCustomImagePath(drink);
            if (customPath != null && File.Exists(customPath))
            {
                using FileStream stream = File.OpenRead(customPath);
                return new Bitmap(stream);
            }
        }
        catch
        {
            // If custom file is locked, unreadable, or corrupted, fall back to asset
        }

        // 2. Fall back to bundled application asset
        if (!string.IsNullOrWhiteSpace(drink.Image))
        {
            try
            {
                using Stream assetStream = AssetLoader.Open(new Uri(drink.Image));
                return new Bitmap(assetStream);
            }
            catch
            {
                // Asset missing or failed to decode
            }
        }

        return null;
    }

    public static Bitmap? LoadDrinkBitmap(int drinkId)
    {
        DrinkData? drink = drinkId switch
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

        return LoadDrinkBitmap(drink);
    }

    public static bool SaveCustomImage(DrinkData? drink, string sourceFilePath)
    {
        if (drink == null || string.IsNullOrWhiteSpace(drink.Image) || string.IsNullOrWhiteSpace(sourceFilePath))
            return false;

        string? finalPath = GetCustomImagePath(drink);
        if (string.IsNullOrWhiteSpace(finalPath))
            return false;

        string temporaryPath = finalPath + ".tmp";

        try
        {
            // Validate the selected image.
            using (FileStream source = File.OpenRead(sourceFilePath))
            using (Bitmap validation = new Bitmap(source))
            {
                if (validation.PixelSize.Width <= 0 || validation.PixelSize.Height <= 0)
                    throw new InvalidDataException("The selected image is invalid.");
            }

            // Convert the selected image to a temporary PNG.
            using (FileStream source = File.OpenRead(sourceFilePath))
            using (Bitmap bitmap = new Bitmap(source))
            using (FileStream destination = File.Create(temporaryPath))
            {
                bitmap.Save(destination, new PngBitmapEncoderOptions());
            }

            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(temporaryPath, finalPath);
            return true;
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch { }
            }
            throw;
        }
    }
}
