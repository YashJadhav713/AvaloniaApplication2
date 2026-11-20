using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Services;

/// <summary>
/// Result container for Calibration operations.
/// </summary>
public class CalibrationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? MachineStatus { get; set; }
    public double[]? Timings { get; set; }
}

/// <summary>
/// Service communicating with the Raspberry Pi hardware controller
/// for status verification and powder/water dispensing calibration.
/// </summary>
public class CalibrationService
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(6)
    };

    public string ControllerIp => Drinks.ControllerIp;
    public int ControllerPort => MachineBits.ControllerPort;

    public string BaseUrl => MachineBits.BaseUrl;

    public CalibrationService()
    {
    }

    /// <summary>
    /// Gets current machine status code (e.g. B10, B6, B20, etc.) from GET /status.
    /// Does not wait in queue, returns real-time status.
    /// </summary>
    public async Task<(bool Success, string Status, string Error)> GetMachineStatusAsync()
    {
        try
        {
            string url = $"{BaseUrl}/status";
            using HttpResponseMessage response = await _httpClient.GetAsync(url);
            string content = (await response.Content.ReadAsStringAsync()).Trim();

            // Strip any JSON quotes or line breaks: e.g. "B10" -> B10
            string rawStatus = content.Trim('"', '\'', ' ', '\r', '\n');
            return (true, rawStatus, "");
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    /// <summary>
    /// Loads existing calibration from GET /getcalibration.
    /// Supports both:
    /// - Standard Python endpoint (returns 8 powder values)
    /// - Enhanced endpoint (returns all 16 powder and water values)
    /// </summary>
    public async Task<CalibrationResult> GetCalibrationAsync()
    {
        double[] timings = Drinks.GetSavedCalibration();

        try
        {
            string url = $"{BaseUrl}/getcalibration";
            using HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return new CalibrationResult
                {
                    Success = false,
                    Message = $"Controller returned HTTP {response.StatusCode}.",
                    Timings = timings
                };
            }

            string json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                return new CalibrationResult
                {
                    Success = false,
                    Message = "Empty calibration response from controller.",
                    Timings = timings
                };
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                for (int m = 1; m <= 8; m++)
                {
                    // Check Powder1 .. Powder8
                    if (root.TryGetProperty($"Powder{m}", out JsonElement pEl) ||
                        root.TryGetProperty($"powder{m}", out pEl))
                    {
                        if (pEl.TryGetDouble(out double pVal) && pVal > 0)
                            timings[m - 1] = pVal;
                    }

                    // Check Water1 .. Water8 (if provided by backend)
                    if (root.TryGetProperty($"Water{m}", out JsonElement wEl) ||
                        root.TryGetProperty($"water{m}", out wEl))
                    {
                        if (wEl.TryGetDouble(out double wVal) && wVal > 0)
                            timings[m + 7] = wVal;
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                int idx = 0;
                foreach (JsonElement item in root.EnumerateArray())
                {
                    if (idx < 16 && item.TryGetDouble(out double v) && v > 0)
                    {
                        timings[idx] = v;
                    }
                    idx++;
                }
            }

            // Persist loaded timings locally
            Drinks.SetSavedCalibration(timings);

            return new CalibrationResult
            {
                Success = true,
                Message = "Calibration loaded successfully.",
                Timings = timings
            };
        }
        catch (Exception ex)
        {
            return new CalibrationResult
            {
                Success = false,
                Message = $"Cannot connect to Raspberry Pi at {ControllerIp}:{ControllerPort} ({ex.Message}). Check connection.",
                Timings = timings
            };
        }
    }

    /// <summary>
    /// Validates machine status (must be idle B10) and sends:
    /// GET /calibration?time=[p1,p2,p3,p4,p5,p6,p7,p8,w1,w2,w3,w4,w5,w6,w7,w8]
    /// </summary>
    public async Task<CalibrationResult> SaveCalibrationAsync(double[] timings)
    {
        // 1. Validation: Array size and positive numeric values
        if (timings == null || timings.Length != 16)
        {
            return new CalibrationResult
            {
                Success = false,
                Message = "Calibration requires all 16 values (8 powder, 8 water)."
            };
        }

        for (int i = 0; i < 16; i++)
        {
            if (timings[i] <= 0 || double.IsNaN(timings[i]) || double.IsInfinity(timings[i]))
            {
                string type = i < 8 ? $"Powder {i + 1}" : $"Water {i - 7}";
                return new CalibrationResult
                {
                    Success = false,
                    Message = $"Invalid value for {type}. Timings must be greater than zero."
                };
            }
        }

        // 2. Machine Idle Validation: Call GET /status and verify "B10"
        var (statusOk, status, statusErr) = await GetMachineStatusAsync();
        if (!statusOk)
        {
            return new CalibrationResult
            {
                Success = false,
                Message = $"Cannot connect to Raspberry Pi at {ControllerIp}:{ControllerPort}. Check connection.",
                MachineStatus = "OFFLINE"
            };
        }

        if (!string.Equals(status, MachineBits.StatusIdle, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "B10", StringComparison.OrdinalIgnoreCase))
        {
            return new CalibrationResult
            {
                Success = false,
                Message = "Calibration is available only when the machine is idle.",
                MachineStatus = status
            };
        }

        // 3. Format all 16 values into JSON array string with InvariantCulture
        string[] formatted = new string[16];
        for (int i = 0; i < 16; i++)
        {
            formatted[i] = timings[i].ToString("0.##", CultureInfo.InvariantCulture);
        }
        string jsonArray = "[" + string.Join(",", formatted) + "]";

        // 4. Send GET /calibration?time=... with URL encoding
        try
        {
            string url = $"{BaseUrl}/calibration?time={Uri.EscapeDataString(jsonArray)}";
            using HttpResponseMessage response = await _httpClient.GetAsync(url);
            string responseText = (await response.Content.ReadAsStringAsync()).Trim();

            if (response.IsSuccessStatusCode && responseText.Contains("OK", StringComparison.OrdinalIgnoreCase))
            {
                // Save locally
                Drinks.SetSavedCalibration(timings);

                return new CalibrationResult
                {
                    Success = true,
                    Message = "Calibration saved and applied successfully.",
                    MachineStatus = status,
                    Timings = timings
                };
            }
            else if (responseText.Contains("INVALID", StringComparison.OrdinalIgnoreCase))
            {
                return new CalibrationResult
                {
                    Success = false,
                    Message = "Invalid calibration values.",
                    MachineStatus = status
                };
            }
            else
            {
                return new CalibrationResult
                {
                    Success = false,
                    Message = $"Controller response: {responseText}",
                    MachineStatus = status
                };
            }
        }
        catch (Exception ex)
        {
            return new CalibrationResult
            {
                Success = false,
                Message = $"Cannot connect to Raspberry Pi at {ControllerIp}:{ControllerPort} ({ex.Message}). Check connection.",
                MachineStatus = status
            };
        }
    }
}
