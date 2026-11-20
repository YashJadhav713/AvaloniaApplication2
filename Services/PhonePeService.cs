using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AvaloniaApplication2.Services;

public class PhonePeService
{
    // ============================================================
    // HTTP
    // ============================================================

    private readonly HttpClient _httpClient;


    // ============================================================
    // PHONEPE SETTINGS
    // ============================================================

    private readonly PhonePeSettings _settings;


    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public PhonePeService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        _settings = LoadSettings();
    }


    // ============================================================
    // LOAD PHONEPE SETTINGS
    //
    // ALL PHONEPE VALUES COME FROM:
    //
    // appsettings.json
    //
    // No PhonePe values are hard-coded here.
    // ============================================================

    private static PhonePeSettings LoadSettings()
    {
        PhonePeSettings settings =
            new PhonePeSettings();

        try
        {
            string file =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "appsettings.json");


            if (!File.Exists(file))
                return settings;


            string json =
                File.ReadAllText(file);


            if (string.IsNullOrWhiteSpace(json))
                return settings;


            using JsonDocument document =
                JsonDocument.Parse(json);


            if (!document.RootElement.TryGetProperty(
                    "PhonePe",
                    out JsonElement phonePe))
            {
                return settings;
            }


            // ----------------------------------------------------
            // BASE URL
            // ----------------------------------------------------

            if (phonePe.TryGetProperty(
                    "BaseUrl",
                    out JsonElement baseUrl))
            {
                settings.BaseUrl =
                    baseUrl.GetString() ?? "";
            }


            // ----------------------------------------------------
            // PROVIDER ID
            // ----------------------------------------------------

            if (phonePe.TryGetProperty(
                    "ProviderId",
                    out JsonElement providerId))
            {
                settings.ProviderId =
                    providerId.GetString() ?? "";
            }


            // ----------------------------------------------------
            // SALT KEY
            // ----------------------------------------------------

            if (phonePe.TryGetProperty(
                    "SaltKey",
                    out JsonElement saltKey))
            {
                settings.SaltKey =
                    saltKey.GetString() ?? "";
            }


            // ----------------------------------------------------
            // SALT KEY INDEX
            // ----------------------------------------------------

            if (phonePe.TryGetProperty(
                    "SaltKeyIndex",
                    out JsonElement saltKeyIndex))
            {
                settings.SaltKeyIndex =
                    saltKeyIndex.GetString() ?? "";
            }


            // ----------------------------------------------------
            // STORE ID
            // ----------------------------------------------------

            if (phonePe.TryGetProperty(
                    "StoreId",
                    out JsonElement storeId))
            {
                settings.StoreId =
                    storeId.GetString() ?? "";
            }


            // ----------------------------------------------------
            // MERCHANT ORDER ID
            // ----------------------------------------------------

            if (phonePe.TryGetProperty(
                    "MerchantOrderId",
                    out JsonElement merchantOrderId))
            {
                settings.MerchantOrderId =
                    merchantOrderId.GetString() ?? "";
            }


            // ----------------------------------------------------
            // EXPIRES IN
            // ----------------------------------------------------

            if (phonePe.TryGetProperty(
                    "ExpiresIn",
                    out JsonElement expiresIn))
            {
                if (expiresIn.TryGetInt32(
                        out int value))
                {
                    settings.ExpiresIn =
                        value;
                }
            }


            // ----------------------------------------------------
            // MESSAGE
            // ----------------------------------------------------

            if (phonePe.TryGetProperty(
                    "Message",
                    out JsonElement message))
            {
                settings.Message =
                    message.GetString() ?? "";
            }
        }
        catch
        {
            // Keep empty settings.
        }


        return settings;
    }


    // ============================================================
    // SHA256
    // ============================================================

    private static string ComputeSha256(
        string value)
    {
        using SHA256 sha256 =
            SHA256.Create();


        byte[] hash =
            sha256.ComputeHash(
                Encoding.UTF8.GetBytes(value));


        StringBuilder result =
            new StringBuilder();


        foreach (byte b in hash)
        {
            result.Append(
                b.ToString("x2"));
        }


        return result.ToString();
    }


    // ============================================================
    // CREATE TRANSACTION ID
    //
    // IMPORTANT:
    //
    // GUID without '-' = 32 characters.
    //
    // Do NOT use Substring(0, 35).
    // ============================================================

    private static string CreateTransactionId()
    {
        return Guid.NewGuid()
            .ToString()
            .Replace("-", "");
    }


    // ============================================================
    // CREATE PAYMENT
    //
    // MID:
    //     Comes from SPOTT AdminPage.
    //
    // AMOUNT:
    //     Comes from CurrentOrder.Price.
    //
    // No ₹1-₹100 restriction.
    // ============================================================

    public async Task<PhonePeResponse>
        CreatePaymentAsync(
            string mid,
            decimal amount)
    {
        // --------------------------------------------------------
        // MID CHECK
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(mid))
        {
            return new PhonePeResponse
            {
                Success = false,

                Code =
                    "INVALID_MID",

                Status =
                    "FAILED",

                Message =
                    "MID is empty."
            };
        }


        // --------------------------------------------------------
        // AMOUNT CHECK
        // --------------------------------------------------------

        if (amount <= 0)
        {
            return new PhonePeResponse
            {
                Success = false,

                Code =
                    "INVALID_AMOUNT",

                Status =
                    "FAILED",

                Message =
                    "Payment amount must be greater than zero."
            };
        }


        // --------------------------------------------------------
        // CONFIG CHECK
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                _settings.BaseUrl)
            ||
            string.IsNullOrWhiteSpace(
                _settings.ProviderId)
            ||
            string.IsNullOrWhiteSpace(
                _settings.SaltKey)
            ||
            string.IsNullOrWhiteSpace(
                _settings.SaltKeyIndex))
        {
            return new PhonePeResponse
            {
                Success = false,

                Code =
                    "INVALID_CONFIGURATION",

                Status =
                    "FAILED",

                Message =
                    "PhonePe configuration is missing."
            };
        }


        // --------------------------------------------------------
        // RUPEES TO PAISE
        // --------------------------------------------------------

        long amountPaise =
            (long)Math.Round(
                amount * 100,
                MidpointRounding.AwayFromZero);


        // --------------------------------------------------------
        // TRANSACTION ID
        // --------------------------------------------------------

        string transactionId =
            CreateTransactionId();


        // --------------------------------------------------------
        // PHONEPE ENDPOINT
        // --------------------------------------------------------

        string apiEndpoint =
            "/v3/qr/init";


        // --------------------------------------------------------
        // REQUEST DATA
        // --------------------------------------------------------

        var data =
            new
            {
                merchantId =
                    mid,

                terminalId =
                    mid,

                transactionId =
                    transactionId,

                amount =
                    amountPaise,

                merchantOrderId =
                    _settings.MerchantOrderId,

                storeId =
                    _settings.StoreId,

                expiresIn =
                    _settings.ExpiresIn,

                message =
                    _settings.Message
            };


        // --------------------------------------------------------
        // JSON
        // --------------------------------------------------------

        string dataJson =
            JsonSerializer.Serialize(
                data);


        // --------------------------------------------------------
        // BASE64
        // --------------------------------------------------------

        string base64Data =
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    dataJson));


        // --------------------------------------------------------
        // X-VERIFY
        // --------------------------------------------------------

        string checksum =
            ComputeSha256(
                base64Data +
                apiEndpoint +
                _settings.SaltKey)
            +
            "###"
            +
            _settings.SaltKeyIndex;


        // --------------------------------------------------------
        // REQUEST BODY
        // --------------------------------------------------------

        var requestBody =
            new
            {
                request =
                    base64Data
            };


        string requestJson =
            JsonSerializer.Serialize(
                requestBody);


        // --------------------------------------------------------
        // URL
        // --------------------------------------------------------

        string url =
            _settings.BaseUrl.TrimEnd('/') +
            apiEndpoint;


        using HttpRequestMessage request =
            new HttpRequestMessage(
                HttpMethod.Post,
                url);


        request.Headers.Add(
            "X-VERIFY",
            checksum);


        request.Headers.Add(
            "X-PROVIDER-ID",
            _settings.ProviderId);


        request.Content =
            new StringContent(
                requestJson,
                Encoding.UTF8,
                "application/json");


        // --------------------------------------------------------
        // CALL PHONEPE
        // --------------------------------------------------------

        try
        {
            HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request);


            string responseText =
                await response.Content
                    .ReadAsStringAsync();


            // ----------------------------------------------------
            // HTTP ERROR
            // ----------------------------------------------------

            if (!response.IsSuccessStatusCode)
            {
                return new PhonePeResponse
                {
                    Success = false,

                    Code =
                        "HTTP_" +
                        (int)response.StatusCode,

                    Status =
                        "FAILED",

                    TransactionId =
                        transactionId,

                    Message =
                        string.IsNullOrWhiteSpace(
                            responseText)
                            ? "PhonePe request failed."
                            : responseText
                };
            }


            // ----------------------------------------------------
            // PARSE RESPONSE
            // ----------------------------------------------------

            using JsonDocument document =
                JsonDocument.Parse(
                    responseText);


            JsonElement root =
                document.RootElement;


            string code =
                root.TryGetProperty(
                    "code",
                    out JsonElement codeElement)
                    ? codeElement.GetString() ?? ""
                    : "";


            // ----------------------------------------------------
            // PHONEPE CREATE SUCCESS
            // ----------------------------------------------------

            if (!code.Equals(
                    "SUCCESS",
                    StringComparison.OrdinalIgnoreCase))
            {
                string message =
                    root.TryGetProperty(
                        "message",
                        out JsonElement messageElement)
                        ? messageElement.GetString() ?? ""
                        : "";


                return new PhonePeResponse
                {
                    Success = false,

                    Code =
                        code,

                    Status =
                        "FAILED",

                    TransactionId =
                        transactionId,

                    Message =
                        string.IsNullOrWhiteSpace(
                            message)
                            ? "Reinitiate the transaction."
                            : message
                };
            }


            // ----------------------------------------------------
            // DATA
            // ----------------------------------------------------

            if (!root.TryGetProperty(
                    "data",
                    out JsonElement dataElement))
            {
                return new PhonePeResponse
                {
                    Success = false,

                    Code =
                        "INVALID_RESPONSE",

                    Status =
                        "FAILED",

                    TransactionId =
                        transactionId,

                    Message =
                        "PhonePe did not return payment data."
                };
            }


            // ----------------------------------------------------
            // QR STRING
            // ----------------------------------------------------

            string qrString =
                dataElement.TryGetProperty(
                    "qrString",
                    out JsonElement qrElement)
                    ? qrElement.GetString() ?? ""
                    : "";


            if (string.IsNullOrWhiteSpace(
                qrString))
            {
                return new PhonePeResponse
                {
                    Success = false,

                    Code =
                        "QR_NOT_FOUND",

                    Status =
                        "FAILED",

                    TransactionId =
                        transactionId,

                    Message =
                        "PhonePe did not return a QR code."
                };
            }


            // ----------------------------------------------------
            // QR READY
            // ----------------------------------------------------

            return new PhonePeResponse
            {
                Success = true,

                Code =
                    "SUCCESS",

                Status =
                    "PAYMENT_PENDING",

                TransactionId =
                    transactionId,

                QrString =
                    qrString,

                Message =
                    "Scan QR and pay."
            };
        }
        catch (Exception ex)
        {
            return new PhonePeResponse
            {
                Success = false,

                Code =
                    "EXCEPTION",

                Status =
                    "FAILED",

                TransactionId =
                    transactionId,

                Message =
                    ex.Message
            };
        }
    }


    // ============================================================
    // CHECK PAYMENT STATUS
    // ============================================================

    public async Task<PhonePeResponse>
        CheckPaymentStatusAsync(
            string mid,
            string transactionId)
    {
        // --------------------------------------------------------
        // MID
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(mid))
        {
            return new PhonePeResponse
            {
                Success = false,

                Code =
                    "INVALID_MID",

                Status =
                    "FAILED",

                Message =
                    "MID is empty."
            };
        }


        // --------------------------------------------------------
        // TRANSACTION ID
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                transactionId))
        {
            return new PhonePeResponse
            {
                Success = false,

                Code =
                    "INVALID_TRANSACTION",

                Status =
                    "FAILED",

                Message =
                    "Transaction ID is empty."
            };
        }


        // --------------------------------------------------------
        // ENDPOINT
        // --------------------------------------------------------

        string apiEndpoint =
            "/v3/transaction/" +
            mid +
            "/" +
            transactionId +
            "/status";


        // --------------------------------------------------------
        // X-VERIFY
        // --------------------------------------------------------

        string checksum =
            ComputeSha256(
                apiEndpoint +
                _settings.SaltKey)
            +
            "###"
            +
            _settings.SaltKeyIndex;


        // --------------------------------------------------------
        // URL
        // --------------------------------------------------------

        string url =
            _settings.BaseUrl.TrimEnd('/') +
            apiEndpoint;


        using HttpRequestMessage request =
            new HttpRequestMessage(
                HttpMethod.Get,
                url);


        request.Headers.Add(
            "X-VERIFY",
            checksum);


        request.Headers.Add(
            "X-PROVIDER-ID",
            _settings.ProviderId);


        // --------------------------------------------------------
        // CALL PHONEPE
        // --------------------------------------------------------

        try
        {
            HttpResponseMessage response =
                await _httpClient.SendAsync(
                    request);


            string responseText =
                await response.Content
                    .ReadAsStringAsync();


            // ----------------------------------------------------
            // HTTP ERROR
            //
            // Keep payment pending.
            // ----------------------------------------------------

            if (!response.IsSuccessStatusCode)
            {
                return new PhonePeResponse
                {
                    Success = false,

                    Code =
                        "HTTP_" +
                        (int)response.StatusCode,

                    Status =
                        "PAYMENT_PENDING",

                    TransactionId =
                        transactionId,

                    Message =
                        "Unable to check payment status."
                };
            }


            // ----------------------------------------------------
            // PARSE
            // ----------------------------------------------------

            using JsonDocument document =
                JsonDocument.Parse(
                    responseText);


            JsonElement root =
                document.RootElement;


            string code =
                root.TryGetProperty(
                    "code",
                    out JsonElement codeElement)
                    ? codeElement.GetString() ?? ""
                    : "";


            // ----------------------------------------------------
            // PAYMENT SUCCESS
            // ----------------------------------------------------

            if (code.Equals(
                    "PAYMENT_SUCCESS",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new PhonePeResponse
                {
                    Success = true,

                    Code =
                        "PAYMENT_SUCCESS",

                    Status =
                        "PAYMENT_SUCCESS",

                    TransactionId =
                        transactionId,

                    Message =
                        "Payment received successfully."
                };
            }


            // ----------------------------------------------------
            // PAYMENT PENDING
            // ----------------------------------------------------

            if (code.Equals(
                    "PAYMENT_PENDING",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new PhonePeResponse
                {
                    Success = false,

                    Code =
                        "PAYMENT_PENDING",

                    Status =
                        "PAYMENT_PENDING",

                    TransactionId =
                        transactionId,

                    Message =
                        "Waiting for payment."
                };
            }


            // ----------------------------------------------------
            // INTERNAL SERVER ERROR
            //
            // Working reference treats this as pending.
            // ----------------------------------------------------

            if (code.Equals(
                    "INTERNAL_SERVER_ERROR",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new PhonePeResponse
                {
                    Success = false,

                    Code =
                        "INTERNAL_SERVER_ERROR",

                    Status =
                        "PAYMENT_PENDING",

                    TransactionId =
                        transactionId,

                    Message =
                        "Waiting for payment."
                };
            }


            // ----------------------------------------------------
            // FAILED STATES
            // ----------------------------------------------------

            if (code.Equals(
                    "PAYMENT_FAILED",
                    StringComparison.OrdinalIgnoreCase)
                ||
                code.Equals(
                    "PAYMENT_CANCELLED",
                    StringComparison.OrdinalIgnoreCase)
                ||
                code.Equals(
                    "PAYMENT_ERROR",
                    StringComparison.OrdinalIgnoreCase)
                ||
                code.Equals(
                    "AUTHORIZATION_FAILED",
                    StringComparison.OrdinalIgnoreCase)
                ||
                code.Equals(
                    "PAYMENT_DECLINED",
                    StringComparison.OrdinalIgnoreCase)
                ||
                code.Equals(
                    "TRANSACTION_NOT_FOUND",
                    StringComparison.OrdinalIgnoreCase))
            {
                string message =
                    root.TryGetProperty(
                        "message",
                        out JsonElement messageElement)
                        ? messageElement.GetString() ?? code
                        : code;


                return new PhonePeResponse
                {
                    Success = false,

                    Code =
                        code,

                    Status =
                        "PAYMENT_FAILED",

                    TransactionId =
                        transactionId,

                    Message =
                        message
                };
            }


            // ----------------------------------------------------
            // UNKNOWN RESPONSE
            //
            // Treat as pending.
            // ----------------------------------------------------

            return new PhonePeResponse
            {
                Success = false,

                Code =
                    code,

                Status =
                    "PAYMENT_PENDING",

                TransactionId =
                    transactionId,

                Message =
                    "Waiting for payment."
            };
        }
        catch (JsonException)
        {
            return new PhonePeResponse
            {
                Success = false,

                Code =
                    "INVALID_JSON",

                Status =
                    "PAYMENT_PENDING",

                TransactionId =
                    transactionId,

                Message =
                    "Unable to read payment status."
            };
        }
        catch (Exception ex)
        {
            return new PhonePeResponse
            {
                Success = false,

                Code =
                    "EXCEPTION",

                Status =
                    "PAYMENT_PENDING",

                TransactionId =
                    transactionId,

                Message =
                    ex.Message
            };
        }
    }


    // ============================================================
    // PHONEPE SETTINGS
    // ============================================================

    public class PhonePeSettings
    {
        public string BaseUrl { get; set; } = "";

        public string ProviderId { get; set; } = "";

        public string SaltKey { get; set; } = "";

        public string SaltKeyIndex { get; set; } = "";

        public string StoreId { get; set; } = "";

        public string MerchantOrderId { get; set; } = "";

        public int ExpiresIn { get; set; }

        public string Message { get; set; } = "";
    }


    // ============================================================
    // PHONEPE RESPONSE
    // ============================================================

    public class PhonePeResponse
    {
        public bool Success { get; set; }

        public string Code { get; set; } = "";

        public string Message { get; set; } = "";

        public string TransactionId { get; set; } = "";

        public string QrString { get; set; } = "";

        public string QrImage { get; set; } = "";

        public string Status { get; set; } = "";
    }
}