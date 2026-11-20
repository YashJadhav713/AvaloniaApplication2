using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaloniaApplication2;

public static class AdminPasswordDefaults
{
    public const string DefaultPassword = "1234";
}

public class DrinkNutrition
{
    public string Serving { get; set; } = "350ml";
    public string Protein { get; set; } = "";
    public string Calories { get; set; } = "";
    public string BCAA { get; set; } = "";
    public string TotalSugars { get; set; } = "";
}

public class MilkNutrition
{
    public double Protein { get; set; } = 0;
    public double Calories { get; set; } = 0;
    public double BCAA { get; set; } = 0;
    public double TotalSugars { get; set; } = 0;
}

public class DrinkData
{
    public int Id { get; set; }

    // NEW: true for Protein Bars (no Water / Milk base).
    public bool IsBar { get; set; } = false;

    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string Image { get; set; } = "";
    public string Price { get; set; } = "0";
    public bool NutritionEnabled { get; set; } = true;
    public DrinkNutrition Nutrition { get; set; } = new DrinkNutrition();
}

public class DrinkSaveNutrition
{
    public string? Serving { get; set; }
    public string? Protein { get; set; }
    public string? Calories { get; set; }
    public string? BCAA { get; set; }
    public string? TotalSugars { get; set; }
}

public class DrinkSaveData
{
    public bool? Enabled { get; set; }
    public string? Name { get; set; }
    public string? Price { get; set; }
    public bool? NutritionEnabled { get; set; }
    public DrinkSaveNutrition? Nutrition { get; set; }
}

public class DrinksSaveFile
{
    public string AdminPassword { get; set; } =
        AdminPasswordDefaults.DefaultPassword;

    public DrinkSaveData? Drink1 { get; set; }
    public DrinkSaveData? Drink2 { get; set; }
    public DrinkSaveData? Drink3 { get; set; }
    public DrinkSaveData? Drink4 { get; set; }
    public DrinkSaveData? Drink5 { get; set; }
    public DrinkSaveData? Drink6 { get; set; }
    public DrinkSaveData? Drink7 { get; set; }
    public DrinkSaveData? Drink8 { get; set; }

    // NEW: Protein Bars (IDs 9 and 10)
    public DrinkSaveData? Bar1 { get; set; }
    public DrinkSaveData? Bar2 { get; set; }

    public bool PaymentEnabled { get; set; } = true;
    public string MerchantId { get; set; } = "";

    public bool MilkEnabled { get; set; } = true;
    public string MilkPrice { get; set; } = "10";

    public string MachineId { get; set; } = "";
    public string MachineName { get; set; } = "";
    public string ServiceContactNumber { get; set; } = "+91 98765 43210";

    public int Drink1Stock { get; set; } = 0;
    public int Drink2Stock { get; set; } = 0;
    public int Drink3Stock { get; set; } = 0;
    public int Drink4Stock { get; set; } = 0;
    public int Drink5Stock { get; set; } = 0;
    public int Drink6Stock { get; set; } = 0;
    public int Drink7Stock { get; set; } = 0;
    public int Drink8Stock { get; set; } = 0;

    // NEW: Protein Bar stock
    public int Bar1Stock { get; set; } = 0;
    public int Bar2Stock { get; set; } = 0;

    public int MilkStock { get; set; } = 0;

    public string ControllerIp { get; set; } = "127.0.0.1";

    public double[]? CalibrationTimings { get; set; }

    public MilkNutrition? MilkNutrition { get; set; }
}

public static class Drink
{
    public static readonly DrinkData First = new DrinkData
    {
        Id = 1,
        Enabled = true,
        Name = "Drink 1",
        Image = "avares://AvaloniaApplication2/Assets/Image/Drink1.png",
        Price = "300",
        NutritionEnabled = true,
        Nutrition = new DrinkNutrition
        {
            Serving = "350ml",
            Protein = "25 g",
            Calories = "128 kcal",
            BCAA = "5.85 g",
            TotalSugars = "0.90 g"
        }
    };

    public static readonly DrinkData Second = new DrinkData
    {
        Id = 2,
        Enabled = true,
        Name = "Drink 2",
        Image = "avares://AvaloniaApplication2/Assets/Image/Drink2.png",
        Price = "350",
        NutritionEnabled = true,
        Nutrition = new DrinkNutrition
        {
            Serving = "350ml",
            Protein = "26 g",
            Calories = "135 kcal",
            BCAA = "6.10 g",
            TotalSugars = "0.65 g"
        }
    };

    public static readonly DrinkData Third = new DrinkData
    {
        Id = 3,
        Enabled = true,
        Name = "Drink 3",
        Image = "avares://AvaloniaApplication2/Assets/Image/Drink3.png",
        Price = "400",
        NutritionEnabled = true,
        Nutrition = new DrinkNutrition
        {
            Serving = "350ml",
            Protein = "25 g",
            Calories = "122 kcal",
            BCAA = "5.93 g",
            TotalSugars = "0.78 g"
        }
    };

    public static readonly DrinkData Fourth = new DrinkData
    {
        Id = 4,
        Enabled = true,
        Name = "Drink 4",
        Image = "avares://AvaloniaApplication2/Assets/Image/Drink4.png",
        Price = "450",
        NutritionEnabled = true,
        Nutrition = new DrinkNutrition
        {
            Serving = "350ml",
            Protein = "27 g",
            Calories = "140 kcal",
            BCAA = "6.20 g",
            TotalSugars = "0.80 g"
        }
    };

    public static readonly DrinkData Fifth = new DrinkData
    {
        Id = 5,
        Enabled = true,
        Name = "Drink 5",
        Image = "avares://AvaloniaApplication2/Assets/Image/Drink5.png",
        Price = "500",
        NutritionEnabled = true,
        Nutrition = new DrinkNutrition
        {
            Serving = "350ml",
            Protein = "28 g",
            Calories = "145 kcal",
            BCAA = "6.40 g",
            TotalSugars = "0.85 g"
        }
    };

    public static readonly DrinkData Sixth = new DrinkData
    {
        Id = 6,
        Enabled = true,
        Name = "Drink 6",
        Image = "avares://AvaloniaApplication2/Assets/Image/Drink6.png",
        Price = "550",
        NutritionEnabled = true,
        Nutrition = new DrinkNutrition
        {
            Serving = "350ml",
            Protein = "29 g",
            Calories = "150 kcal",
            BCAA = "6.60 g",
            TotalSugars = "0.90 g"
        }
    };

    public static readonly DrinkData Seventh = new DrinkData
    {
        Id = 7,
        Enabled = true,
        Name = "Drink 7",
        Image = "avares://AvaloniaApplication2/Assets/Image/Drink7.png",
        Price = "600",
        NutritionEnabled = true,
        Nutrition = new DrinkNutrition
        {
            Serving = "350ml",
            Protein = "30 g",
            Calories = "155 kcal",
            BCAA = "6.80 g",
            TotalSugars = "0.95 g"
        }
    };

    // DRINK 8 IS DISABLED (all logic kept, only Enabled = false)
    public static readonly DrinkData Eighth = new DrinkData
    {
        Id = 8,
        Enabled = false,
        Name = "Drink 8",
        Image = "avares://AvaloniaApplication2/Assets/Image/Drink8.png",
        Price = "650",
        NutritionEnabled = true,
        Nutrition = new DrinkNutrition
        {
            Serving = "350ml",
            Protein = "31 g",
            Calories = "160 kcal",
            BCAA = "7.00 g",
            TotalSugars = "1.00 g"
        }
    };
}

// ============================================================
// PROTEIN BARS (IDs 9 and 10)
// Placeholder price / nutrition / image - edit as needed.
// ============================================================
public static class Bar
{
    public static readonly DrinkData First = new DrinkData
    {
        Id = 9,
        IsBar = true,
        Enabled = true,
        Name = "Protein Bar 1",
        Image = "avares://AvaloniaApplication2/Assets/Image/Bar1.png",
        Price = "150",
        NutritionEnabled = true,
        Nutrition = new DrinkNutrition
        {
            Serving = "1 bar",
            Protein = "20 g",
            Calories = "200 kcal",
            BCAA = "4.50 g",
            TotalSugars = "3.00 g"
        }
    };

    public static readonly DrinkData Second = new DrinkData
    {
        Id = 10,
        IsBar = true,
        Enabled = true,
        Name = "Protein Bar 2",
        Image = "avares://AvaloniaApplication2/Assets/Image/Bar2.png",
        Price = "200",
        NutritionEnabled = true,
        Nutrition = new DrinkNutrition
        {
            Serving = "1 bar",
            Protein = "22 g",
            Calories = "210 kcal",
            BCAA = "4.80 g",
            TotalSugars = "3.50 g"
        }
    };
}

public static class Drinks
{
    private static readonly string ConfigFile =
        Path.Combine(AppContext.BaseDirectory, "drinks.json");

    private static DrinksSaveFile _savedData = new DrinksSaveFile();

    // ADMIN PASSWORD: FIXED PROPERTY
    public static string AdminPassword =>
        string.IsNullOrWhiteSpace(_savedData.AdminPassword)
            ? AdminPasswordDefaults.DefaultPassword
            : _savedData.AdminPassword;
    public static bool SetAdminPassword(string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) ||
            newPassword.Length != 4)
            return false;

        foreach (char c in newPassword)
        {
            if (c < '0' || c > '9')
                return false;
        }

        string oldPassword = _savedData.AdminPassword;

        _savedData.AdminPassword = newPassword;

        if (SaveJson())
            return true;

        _savedData.AdminPassword = oldPassword;
        return false;
    }

    public static bool PaymentEnabled => _savedData.PaymentEnabled;

    public static string MerchantId => _savedData.MerchantId ?? "";

    public static string MachineId => _savedData.MachineId ?? "";

    public static string MachineName => _savedData.MachineName ?? "";

    public static string ServiceContactNumber =>
        string.IsNullOrWhiteSpace(_savedData.ServiceContactNumber)
            ? "+91 98765 43210"
            : _savedData.ServiceContactNumber;

    public static bool SetServiceContactNumber(string number)
    {
        _savedData.ServiceContactNumber = number?.Trim() ?? "+91 98765 43210";
        return Save();
    }

    public static bool MilkEnabled => _savedData.MilkEnabled;

    public static string MilkPrice
    {
        get
        {
            if (!double.TryParse(_savedData.MilkPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out double price))
                return "0";

            if (price < 0)
                return "0";

            return price.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    public static readonly MilkNutrition Milk = new MilkNutrition
    {
        Protein = 3.2,
        Calories = 61,
        BCAA = 0.5,
        TotalSugars = 5.0
    };

    static Drinks()
    {
        Load();
    }

    public static void Load()
    {
        try
        {
            _savedData = new DrinksSaveFile();

            if (!File.Exists(ConfigFile))
                return;

            string json = File.ReadAllText(ConfigFile);

            if (string.IsNullOrWhiteSpace(json))
                return;

            DrinksSaveFile? loaded =
                JsonSerializer.Deserialize<DrinksSaveFile>(json);

            if (loaded == null)
                return;

            _savedData = loaded;

            // USE DEFAULT IF PASSWORD IS MISSING OR EMPTY
            if (string.IsNullOrWhiteSpace(_savedData.AdminPassword))
            {
                _savedData.AdminPassword =
                    AdminPasswordDefaults.DefaultPassword;
            }

            _savedData.MerchantId ??= "";
            _savedData.MachineId ??= "";
            _savedData.MachineName ??= "";
            _savedData.ControllerIp ??= "127.0.0.1";

            NormalizeMilkPrice();

            if (_savedData.MilkNutrition != null)
            {
                Milk.Protein = _savedData.MilkNutrition.Protein;
                Milk.Calories = _savedData.MilkNutrition.Calories;
                Milk.BCAA = _savedData.MilkNutrition.BCAA;
                Milk.TotalSugars = _savedData.MilkNutrition.TotalSugars;
            }

            ApplySavedData(Drink.First, _savedData.Drink1);
            ApplySavedData(Drink.Second, _savedData.Drink2);
            ApplySavedData(Drink.Third, _savedData.Drink3);
            ApplySavedData(Drink.Fourth, _savedData.Drink4);
            ApplySavedData(Drink.Fifth, _savedData.Drink5);
            ApplySavedData(Drink.Sixth, _savedData.Drink6);
            ApplySavedData(Drink.Seventh, _savedData.Drink7);
            ApplySavedData(Drink.Eighth, _savedData.Drink8);

            ApplySavedData(Bar.First, _savedData.Bar1);
            ApplySavedData(Bar.Second, _savedData.Bar2);
        }
        catch
        {
            _savedData = new DrinksSaveFile();
        }
    }

    public static bool SetMilkNutrition(double protein, double calories, double bcaa, double sugars)
    {
        try
        {
            _savedData.MilkNutrition ??= new MilkNutrition();
            _savedData.MilkNutrition.Protein = Math.Max(0, protein);
            _savedData.MilkNutrition.Calories = Math.Max(0, calories);
            _savedData.MilkNutrition.BCAA = Math.Max(0, bcaa);
            _savedData.MilkNutrition.TotalSugars = Math.Max(0, sugars);

            Milk.Protein = _savedData.MilkNutrition.Protein;
            Milk.Calories = _savedData.MilkNutrition.Calories;
            Milk.BCAA = _savedData.MilkNutrition.BCAA;
            Milk.TotalSugars = _savedData.MilkNutrition.TotalSugars;

            return SaveJson();
        }
        catch
        {
            return false;
        }
    }

    private static void NormalizeMilkPrice()
    {
        if (string.IsNullOrWhiteSpace(_savedData.MilkPrice))
        {
            _savedData.MilkPrice = "0";
            return;
        }

        if (!double.TryParse(_savedData.MilkPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out double price))
        {
            _savedData.MilkPrice = "0";
            return;
        }

        if (price < 0)
        {
            _savedData.MilkPrice = "0";
            return;
        }

        _savedData.MilkPrice = price.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static void ApplySavedData(
        DrinkData drink,
        DrinkSaveData? saved)
    {
        if (saved == null)
            return;

        if (saved.Enabled.HasValue)
            drink.Enabled = saved.Enabled.Value;

        if (!string.IsNullOrWhiteSpace(saved.Name))
            drink.Name = saved.Name;

        if (!string.IsNullOrWhiteSpace(saved.Price))
            drink.Price = saved.Price;

        if (saved.NutritionEnabled.HasValue)
            drink.NutritionEnabled = saved.NutritionEnabled.Value;

        if (saved.Nutrition == null)
            return;

        if (!string.IsNullOrWhiteSpace(saved.Nutrition.Serving))
            drink.Nutrition.Serving = saved.Nutrition.Serving;

        if (!string.IsNullOrWhiteSpace(saved.Nutrition.Protein))
            drink.Nutrition.Protein = saved.Nutrition.Protein;

        if (!string.IsNullOrWhiteSpace(saved.Nutrition.Calories))
            drink.Nutrition.Calories = saved.Nutrition.Calories;

        if (!string.IsNullOrWhiteSpace(saved.Nutrition.BCAA))
            drink.Nutrition.BCAA = saved.Nutrition.BCAA;

        if (!string.IsNullOrWhiteSpace(saved.Nutrition.TotalSugars))
            drink.Nutrition.TotalSugars = saved.Nutrition.TotalSugars;
    }

    public static DrinkSaveData? GetSavedData(int drinkId)
    {
        return drinkId switch
        {
            1 => _savedData.Drink1,
            2 => _savedData.Drink2,
            3 => _savedData.Drink3,
            4 => _savedData.Drink4,
            5 => _savedData.Drink5,
            6 => _savedData.Drink6,
            7 => _savedData.Drink7,
            8 => _savedData.Drink8,
            9 => _savedData.Bar1,
            10 => _savedData.Bar2,
            _ => null
        };
    }

    public static bool SaveSelected(
        int drinkId,
        bool enabled,
        string? name,
        string? price,
        bool nutritionEnabled,
        string? serving,
        string? protein,
        string? calories,
        string? bcaa,
        string? sugars)
    {
        try
        {
            DrinkSaveData? saved = GetSavedData(drinkId);

            if (saved == null)
                saved = new DrinkSaveData();

            saved.Enabled = enabled;

            if (!string.IsNullOrWhiteSpace(name))
                saved.Name = name.Trim();

            if (!string.IsNullOrWhiteSpace(price))
                saved.Price = price.Trim();

            saved.NutritionEnabled = nutritionEnabled;

            if (!string.IsNullOrWhiteSpace(serving) ||
                !string.IsNullOrWhiteSpace(protein) ||
                !string.IsNullOrWhiteSpace(calories) ||
                !string.IsNullOrWhiteSpace(bcaa) ||
                !string.IsNullOrWhiteSpace(sugars))
            {
                saved.Nutrition ??= new DrinkSaveNutrition();

                if (!string.IsNullOrWhiteSpace(serving))
                    saved.Nutrition.Serving = serving.Trim();

                if (!string.IsNullOrWhiteSpace(protein))
                    saved.Nutrition.Protein = protein.Trim();

                if (!string.IsNullOrWhiteSpace(calories))
                    saved.Nutrition.Calories = calories.Trim();

                if (!string.IsNullOrWhiteSpace(bcaa))
                    saved.Nutrition.BCAA = bcaa.Trim();

                if (!string.IsNullOrWhiteSpace(sugars))
                    saved.Nutrition.TotalSugars = sugars.Trim();
            }

            switch (drinkId)
            {
                case 1: _savedData.Drink1 = saved; break;
                case 2: _savedData.Drink2 = saved; break;
                case 3: _savedData.Drink3 = saved; break;
                case 4: _savedData.Drink4 = saved; break;
                case 5: _savedData.Drink5 = saved; break;
                case 6: _savedData.Drink6 = saved; break;
                case 7: _savedData.Drink7 = saved; break;
                case 8: _savedData.Drink8 = saved; break;
                case 9: _savedData.Bar1 = saved; break;
                case 10: _savedData.Bar2 = saved; break;
                default: return false;
            }

            if (!SaveJson())
                return false;

            ResetRuntimeToDefaults();

            ApplySavedData(Drink.First, _savedData.Drink1);
            ApplySavedData(Drink.Second, _savedData.Drink2);
            ApplySavedData(Drink.Third, _savedData.Drink3);
            ApplySavedData(Drink.Fourth, _savedData.Drink4);
            ApplySavedData(Drink.Fifth, _savedData.Drink5);
            ApplySavedData(Drink.Sixth, _savedData.Drink6);
            ApplySavedData(Drink.Seventh, _savedData.Drink7);
            ApplySavedData(Drink.Eighth, _savedData.Drink8);

            ApplySavedData(Bar.First, _savedData.Bar1);
            ApplySavedData(Bar.Second, _savedData.Bar2);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetPaymentEnabled(bool enabled)
    {
        try
        {
            _savedData.PaymentEnabled = enabled;
            return SaveJson();
        }
        catch
        {
            return false;
        }
    }

    public static bool SetMerchantId(string? merchantId)
    {
        try
        {
            _savedData.MerchantId = merchantId?.Trim() ?? "";
            return SaveJson();
        }
        catch
        {
            return false;
        }
    }

    public static bool SetVMMSettings(
        string? machineId,
        string? machineName)
    {
        try
        {
            _savedData.MachineId = machineId?.Trim() ?? "";
            _savedData.MachineName = machineName?.Trim() ?? "";
            return SaveJson();
        }
        catch
        {
            return false;
        }
    }

    public static bool SetMilkEnabled(bool enabled)
    {
        try
        {
            _savedData.MilkEnabled = enabled;
            return SaveJson();
        }
        catch
        {
            return false;
        }
    }

    public static bool SetMilkPrice(string? price)
    {
        try
        {
            double finalPrice = 0;

            if (!string.IsNullOrWhiteSpace(price) &&
                double.TryParse(price.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) &&
                parsed >= 0)
            {
                finalPrice = parsed;
            }

            _savedData.MilkPrice = finalPrice.ToString("0.##", CultureInfo.InvariantCulture);
            return SaveJson();
        }
        catch
        {
            return false;
        }
    }

    public static string ControllerIp =>
        string.IsNullOrWhiteSpace(_savedData.ControllerIp)
            ? "127.0.0.1"
            : _savedData.ControllerIp.Trim();

    public static bool SetControllerIp(string? ip)
    {
        _savedData.ControllerIp = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
        return SaveJson();
    }

    public static double[] GetSavedCalibration()
    {
        if (_savedData.CalibrationTimings != null && _savedData.CalibrationTimings.Length == 16)
        {
            return (double[])_savedData.CalibrationTimings.Clone();
        }
        return new double[] { 9.0, 9.0, 9.0, 9.0, 9.0, 9.0, 9.0, 9.0, 30.0, 30.0, 30.0, 30.0, 30.0, 30.0, 30.0, 30.0 };
    }

    public static bool SetSavedCalibration(double[] timings)
    {
        if (timings == null || timings.Length != 16)
            return false;

        _savedData.CalibrationTimings = (double[])timings.Clone();
        return SaveJson();
    }

    public static string GetOrderPrice(
        DrinkData drink,
        string baseType)
    {
        double drinkPrice = 0;

        if (!double.TryParse(drink.Price, NumberStyles.Float, CultureInfo.InvariantCulture, out drinkPrice))
            drinkPrice = 0;

        if (drinkPrice < 0)
            drinkPrice = 0;

        // Bars never add milk price.
        if (drink.IsBar)
            return drinkPrice.ToString("0.##", CultureInfo.InvariantCulture);

        if (!string.Equals(
                baseType,
                "Milk",
                StringComparison.OrdinalIgnoreCase))
        {
            return drinkPrice.ToString("0.##", CultureInfo.InvariantCulture);
        }

        double milkPrice = 0;

        if (!double.TryParse(MilkPrice, NumberStyles.Float, CultureInfo.InvariantCulture, out milkPrice))
            milkPrice = 0;

        if (milkPrice < 0)
            milkPrice = 0;

        return (drinkPrice + milkPrice).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static bool SaveJson()
    {
        try
        {
            NormalizeMilkPrice();

            _savedData.MerchantId =
                _savedData.MerchantId?.Trim() ?? "";

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition =
                    JsonIgnoreCondition.WhenWritingNull
            };

            string json = JsonSerializer.Serialize(_savedData, options);
            string temporaryFile = ConfigFile + ".tmp";

            File.WriteAllText(temporaryFile, json);

            try
            {
                if (File.Exists(ConfigFile))
                {
                    File.Replace(temporaryFile, ConfigFile, null);
                }
                else
                {
                    File.Move(temporaryFile, ConfigFile);
                }
            }
            catch
            {
                File.Copy(temporaryFile, ConfigFile, true);
                try { File.Delete(temporaryFile); } catch { }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ResetRuntimeToDefaults()
    {
        Drink.First.Enabled = true;
        Drink.First.NutritionEnabled = true;
        Drink.First.Name = "Drink 1";
        Drink.First.Price = "300";
        Drink.First.Nutrition.Serving = "350ml";
        Drink.First.Nutrition.Protein = "25 g";
        Drink.First.Nutrition.Calories = "128 kcal";
        Drink.First.Nutrition.BCAA = "5.85 g";
        Drink.First.Nutrition.TotalSugars = "0.90 g";

        Drink.Second.Enabled = true;
        Drink.Second.NutritionEnabled = true;
        Drink.Second.Name = "Drink 2";
        Drink.Second.Price = "350";
        Drink.Second.Nutrition.Serving = "350ml";
        Drink.Second.Nutrition.Protein = "26 g";
        Drink.Second.Nutrition.Calories = "135 kcal";
        Drink.Second.Nutrition.BCAA = "6.10 g";
        Drink.Second.Nutrition.TotalSugars = "0.65 g";

        Drink.Third.Enabled = true;
        Drink.Third.NutritionEnabled = true;
        Drink.Third.Name = "Drink 3";
        Drink.Third.Price = "400";
        Drink.Third.Nutrition.Serving = "350ml";
        Drink.Third.Nutrition.Protein = "25 g";
        Drink.Third.Nutrition.Calories = "122 kcal";
        Drink.Third.Nutrition.BCAA = "5.93 g";
        Drink.Third.Nutrition.TotalSugars = "0.78 g";

        Drink.Fourth.Enabled = true;
        Drink.Fourth.NutritionEnabled = true;
        Drink.Fourth.Name = "Drink 4";
        Drink.Fourth.Price = "450";
        Drink.Fourth.Nutrition.Serving = "350ml";
        Drink.Fourth.Nutrition.Protein = "27 g";
        Drink.Fourth.Nutrition.Calories = "140 kcal";
        Drink.Fourth.Nutrition.BCAA = "6.20 g";
        Drink.Fourth.Nutrition.TotalSugars = "0.80 g";

        Drink.Fifth.Enabled = true;
        Drink.Fifth.NutritionEnabled = true;
        Drink.Fifth.Name = "Drink 5";
        Drink.Fifth.Price = "500";
        Drink.Fifth.Nutrition.Serving = "350ml";
        Drink.Fifth.Nutrition.Protein = "28 g";
        Drink.Fifth.Nutrition.Calories = "145 kcal";
        Drink.Fifth.Nutrition.BCAA = "6.40 g";
        Drink.Fifth.Nutrition.TotalSugars = "0.85 g";

        Drink.Sixth.Enabled = true;
        Drink.Sixth.NutritionEnabled = true;
        Drink.Sixth.Name = "Drink 6";
        Drink.Sixth.Price = "550";
        Drink.Sixth.Nutrition.Serving = "350ml";
        Drink.Sixth.Nutrition.Protein = "29 g";
        Drink.Sixth.Nutrition.Calories = "150 kcal";
        Drink.Sixth.Nutrition.BCAA = "6.60 g";
        Drink.Sixth.Nutrition.TotalSugars = "0.90 g";

        Drink.Seventh.Enabled = true;
        Drink.Seventh.NutritionEnabled = true;
        Drink.Seventh.Name = "Drink 7";
        Drink.Seventh.Price = "600";
        Drink.Seventh.Nutrition.Serving = "350ml";
        Drink.Seventh.Nutrition.Protein = "30 g";
        Drink.Seventh.Nutrition.Calories = "155 kcal";
        Drink.Seventh.Nutrition.BCAA = "6.80 g";
        Drink.Seventh.Nutrition.TotalSugars = "0.95 g";

        // DRINK 8 IS DISABLED BY DEFAULT
        Drink.Eighth.Enabled = false;
        Drink.Eighth.NutritionEnabled = true;
        Drink.Eighth.Name = "Drink 8";
        Drink.Eighth.Price = "650";
        Drink.Eighth.Nutrition.Serving = "350ml";
        Drink.Eighth.Nutrition.Protein = "31 g";
        Drink.Eighth.Nutrition.Calories = "160 kcal";
        Drink.Eighth.Nutrition.BCAA = "7.00 g";
        Drink.Eighth.Nutrition.TotalSugars = "1.00 g";

        // PROTEIN BARS
        Bar.First.Enabled = true;
        Bar.First.NutritionEnabled = true;
        Bar.First.Name = "Protein Bar 1";
        Bar.First.Price = "150";
        Bar.First.Nutrition.Serving = "1 bar";
        Bar.First.Nutrition.Protein = "20 g";
        Bar.First.Nutrition.Calories = "200 kcal";
        Bar.First.Nutrition.BCAA = "4.50 g";
        Bar.First.Nutrition.TotalSugars = "3.00 g";

        Bar.Second.Enabled = true;
        Bar.Second.NutritionEnabled = true;
        Bar.Second.Name = "Protein Bar 2";
        Bar.Second.Price = "200";
        Bar.Second.Nutrition.Serving = "1 bar";
        Bar.Second.Nutrition.Protein = "22 g";
        Bar.Second.Nutrition.Calories = "210 kcal";
        Bar.Second.Nutrition.BCAA = "4.80 g";
        Bar.Second.Nutrition.TotalSugars = "3.50 g";

        // MILK NUTRITION DEFAULTS
        Milk.Protein = 3.2;
        Milk.Calories = 61;
        Milk.BCAA = 0.5;
        Milk.TotalSugars = 5.0;
    }

    public static bool Save()
    {
        try
        {
            return SaveJson();
        }
        catch
        {
            return false;
        }
    }

    public static void DeleteSavedConfiguration()
    {
        try
        {
            if (File.Exists(ConfigFile))
                File.Delete(ConfigFile);

            _savedData = new DrinksSaveFile();
            ResetRuntimeToDefaults();
        }
        catch
        {
        }
    }

    public static int GetStock(int drinkId)
    {
        return drinkId switch
        {
            1 => _savedData.Drink1Stock,
            2 => _savedData.Drink2Stock,
            3 => _savedData.Drink3Stock,
            4 => _savedData.Drink4Stock,
            5 => _savedData.Drink5Stock,
            6 => _savedData.Drink6Stock,
            7 => _savedData.Drink7Stock,
            8 => _savedData.Drink8Stock,
            9 => _savedData.Bar1Stock,
            10 => _savedData.Bar2Stock,
            _ => 0
        };
    }

    public static int GetMilkStock()
    {
        return _savedData.MilkStock;
    }

    public static bool SetMilkStock(int quantity)
    {
        if (quantity < 0)
            return false;

        try
        {
            _savedData.MilkStock = quantity;
            return SaveJson();
        }
        catch
        {
            return false;
        }
    }

    public static bool ReduceMilkStock()
    {
        int stock = GetMilkStock();

        if (stock <= 0)
            return false;

        return SetMilkStock(stock - 1);
    }

    public static bool SetStock(int drinkId, int quantity)
    {
        if (quantity < 0)
            return false;

        switch (drinkId)
        {
            case 1: _savedData.Drink1Stock = quantity; break;
            case 2: _savedData.Drink2Stock = quantity; break;
            case 3: _savedData.Drink3Stock = quantity; break;
            case 4: _savedData.Drink4Stock = quantity; break;
            case 5: _savedData.Drink5Stock = quantity; break;
            case 6: _savedData.Drink6Stock = quantity; break;
            case 7: _savedData.Drink7Stock = quantity; break;
            case 8: _savedData.Drink8Stock = quantity; break;
            case 9: _savedData.Bar1Stock = quantity; break;
            case 10: _savedData.Bar2Stock = quantity; break;
            default: return false;
        }

        return SaveJson();
    }

    public static bool ReduceStock(int drinkId)
    {
        int stock = GetStock(drinkId);

        if (drinkId < 1 || drinkId > 10 || stock <= 0)
            return false;

        return SetStock(drinkId, stock - 1);
    }
}

public static class CurrentOrder
{
    public static int DrinkId { get; private set; }

    public static string DrinkName { get; private set; } = "";

    public static string Base { get; private set; } = "Water";

    public static string Price { get; private set; } = "0";

    public static string OrderId { get; private set; } = "";

    public static DrinkData? Drink { get; private set; }

    public static void SetOrder(DrinkData drink, string baseType)
    {
        Drink = drink;
        DrinkId = drink.Id;
        DrinkName = drink.Name;

        // Bars have no Water / Milk base.
        Base = drink.IsBar ? "None" : baseType;

        Price = Drinks.GetOrderPrice(drink, Base);

        OrderId = "";
    }

    public static void SetOrderId(string? apiOrderId)
    {
        OrderId = apiOrderId?.Trim() ?? "";
    }

    public static void Clear()
    {
        Drink = null;
        DrinkId = 0;
        DrinkName = "";
        Base = "Water";
        Price = "0";
        OrderId = "";
    }
}