using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Services;

/// <summary>
/// Machine status codes and endpoints for Raspberry Pi 5 hardware controller:
/// 
/// Order of bits for one drink:
/// B6 (busy) -> B20 (pickup wait / LED blinking) -> B21 (picked up) -> B10 (complete and idle)
/// If glass fails: B6 -> B3 (sorry page)
/// 
/// 15-Bit Packet Structure (/sendbits?bits=(...)):
/// - Bits 0-7  (Bit 1 - Bit 8)  : Drinks 1 to 8 (Canisters 1 to 8)
/// - Bit 8     (Bit 9)          : Protein Bar 1 (ID: 9)
/// - Bit 9     (Bit 10)         : Protein Bar 2 (ID: 10)
/// - Bit 10    (Bit 11)         : Liquid Base (0 = Water, 1 = Milk)
/// - Bits 11-14(Bits 12 - 15)   : Reserved (0)
/// 
/// Endpoints:
/// /sendbits   -> Sends 15-bit packet
/// /drinktime  -> T85 (glass detected + seconds), B3 (glass failed), B6 (not ready yet), INVALID
/// /getbits    -> B6 (busy), B20 (pickup wait), B21 (picked up), B10 (complete and idle), B3 (failed)
/// </summary>
public static class MachineBits
{
    // ---------------- 15-Bit Packet Indices ----------------
    public const int TotalBits = 15;
    public const int IndexBar1 = 8;          // Bit 9 (Index 8 in 0-based array)
    public const int IndexBar2 = 9;          // Bit 10 (Index 9 in 0-based array)
    public const int IndexLiquid = 10;       // Bit 11 (Index 10: 0 = Water, 1 = Milk)

    // ---------------- Machine status (/getbits) ----------------
    public const string StatusBusy = "B6";            // Machine busy preparing
    public const string StatusPickupWait = "B20";     // Pickup LED blinking, drink ready for pickup
    public const string StatusReady = "B20";          // Alias for B20 (Drink ready in chamber)
    public const string StatusPickedUp = "B21";       // Glass picked up from chamber
    public const string StatusPickup = "B21";         // Alias for B21
    public const string StatusIdle = "B10";           // Order complete and machine idle
    public const string StatusGlassFailed = "B3";     // Glass dispense failed
    public const string StatusPowderFailed = "B7";    // Legacy powder failed
    public const string StatusError = "B5";           // General machine error

    // ---------------- Legacy aliases ----------------
    public const string LegacyReady = "B12";

    // ---------------- Door status (/door) ----------------
    public const string DoorClose = "B14";
    public const string DoorOpen = "B15";

    // ---------------- WiFi configuration (/config) ----------------
    public const string WifiSuccess = "B2";
    public const string WifiFailed = "B4";

    // ---------------- Root ping (/) ----------------
    public const string Ping = "B11";

    // ---------------- Controller Network Configuration ----------------
    public static string ControllerHost => Drinks.ControllerIp;
    public const int ControllerPort = 5000;
    public static string BaseUrl => $"http://{ControllerHost}:{ControllerPort}";

    /// <summary>
    /// Builds the 15-bit integer packet for drinks (IDs 1-8) and protein bars (IDs 9-10).
    /// </summary>
    public static int[] CreateBitsPacket(int drinkId, string? baseType)
    {
        int[] bits = new int[TotalBits];

        // Drinks 1 to 8: Bits 1 to 8 (Indices 0 to 7)
        if (drinkId >= 1 && drinkId <= 8)
        {
            bits[drinkId - 1] = 1;
        }
        // Protein Bar 1: Bit 9 (Index 8)
        else if (drinkId == 9)
        {
            bits[IndexBar1] = 1;
        }
        // Protein Bar 2: Bit 10 (Index 9)
        else if (drinkId == 10)
        {
            bits[IndexBar2] = 1;
        }

        // Liquid Base (Index 10): 0 = Water, 1 = Milk
        if (string.Equals(baseType, "Milk", StringComparison.OrdinalIgnoreCase))
        {
            bits[IndexLiquid] = 1;
        }

        return bits;
    }

    /// <summary>
    /// Formats the bits array as "(b0,b1,b2,...)".
    /// </summary>
    public static string FormatBitsPacket(int[] bits)
    {
        return "(" + string.Join(",", bits) + ")";
    }

    /// <summary>
    /// Sends the 15-bit packet to the Raspberry Pi Flask controller.
    /// </summary>
    public static async Task<string?> SendDrinkBitsAsync(HttpClient httpClient, int drinkId, string? baseType)
    {
        int[] bits = CreateBitsPacket(drinkId, baseType);
        string packet = FormatBitsPacket(bits);
        string url = $"{BaseUrl}/sendbits?bits={Uri.EscapeDataString(packet)}";

        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(url);
            string result = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[SEND BITS] {packet} -> {result}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SEND BITS ERROR] {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Queries the controller for the real-time door status via GET /door.
    /// Returns "B14" (Door Closed), "B15" (Door Open), or null on non-success status code.
    /// Uses existing machine IP configuration via BaseUrl.
    /// </summary>
    public static async Task<string?> GetDoorStatusAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        string url = $"{BaseUrl}/door";
        using HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            return content.Trim('"', '\'', ' ', '\r', '\n', '\t');
        }
        return null;
    }
}
