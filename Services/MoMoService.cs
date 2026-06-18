using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TamThaiTuSport.Services
{
    public class MoMoService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MoMoService> _logger;
        private readonly HttpClient _httpClient;

        public MoMoService(IConfiguration config, ILogger<MoMoService> logger, HttpClient httpClient)
        {
            _config = config;
            _logger = logger;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Tạo yêu cầu thanh toán MoMo và trả về payUrl chuyển hướng khách hàng.
        /// </summary>
        public async Task<string?> CreatePaymentAsync(int orderId, long amount, string orderInfo, string? returnUrl = null, string? notifyUrl = null)
        {
            var partnerCode = _config["MoMoSettings:PartnerCode"]!;
            var accessKey = _config["MoMoSettings:AccessKey"]!;
            var secretKey = _config["MoMoSettings:SecretKey"]!;
            var apiEndpoint = _config["MoMoSettings:ApiEndpoint"]!;
            
            returnUrl ??= _config["MoMoSettings:ReturnUrl"]!;
            notifyUrl ??= _config["MoMoSettings:NotifyUrl"]!;

            var requestId = $"{partnerCode}_{DateTime.Now.Ticks}";
            var momoOrderId = $"ORDER_{orderId}_{DateTime.Now.Ticks}";
            var requestType = "payWithMethod";
            var extraData = ""; // Dữ liệu bổ sung nếu cần

            // 1. Tạo chuỗi ký tự thô theo đúng định dạng MoMo (thứ tự alphabet)
            var rawHash = $"accessKey={accessKey}" +
                          $"&amount={amount}" +
                          $"&extraData={extraData}" +
                          $"&ipnUrl={notifyUrl}" +
                          $"&orderId={momoOrderId}" +
                          $"&orderInfo={orderInfo}" +
                          $"&partnerCode={partnerCode}" +
                          $"&redirectUrl={returnUrl}" +
                          $"&requestId={requestId}" +
                          $"&requestType={requestType}";

            // 2. Tính chữ ký điện tử HMAC-SHA256
            var signature = ComputeHmacSha256(rawHash, secretKey);

            // 3. Tạo Payload JSON gửi đến MoMo
            var payload = new
            {
                partnerCode,
                partnerName = "Tam Thái Tử Sport",
                storeId = "TamThaiTuSport",
                requestId,
                amount,
                orderId = momoOrderId,
                orderInfo,
                redirectUrl = returnUrl,
                ipnUrl = notifyUrl,
                lang = "vi",
                extraData,
                requestType,
                autoCapture = true,
                signature
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(apiEndpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();
                
                // Write debug file
                try
                {
                    await System.IO.File.WriteAllTextAsync("momo_debug.log", $"Timestamp: {DateTime.Now}\nEndpoint: {apiEndpoint}\nPayload: {jsonPayload}\nResponse: {responseString}\n");
                }
                catch {}

                _logger.LogInformation("MoMo Response: {Response}", responseString);
                var result = JsonSerializer.Deserialize<JsonElement>(responseString);
                if (result.TryGetProperty("payUrl", out var payUrl))
                {
                    return payUrl.GetString();
                }
                _logger.LogWarning("MoMo không trả về payUrl: {Response}", responseString);
                return $"ERROR: MoMo Response (Code {response.StatusCode}): {responseString}";
            }
            catch (Exception ex)
            {
                try
                {
                    await System.IO.File.WriteAllTextAsync("momo_debug.log", $"Timestamp: {DateTime.Now}\nEndpoint: {apiEndpoint}\nPayload: {jsonPayload}\nException: {ex}\n");
                }
                catch {}

                _logger.LogError(ex, "Lỗi khi gọi MoMo API");
                return $"ERROR: Exception: {ex.Message}";
            }
        }

        /// <summary>
        /// Xác thực chữ ký phản hồi từ MoMo (Chống giả mạo dữ liệu trả về)
        /// </summary>
        public bool VerifySignature(IDictionary<string, string> momoParams, string receivedSignature)
        {
            var secretKey = _config["MoMoSettings:SecretKey"]!;
            var accessKey = _config["MoMoSettings:AccessKey"]!;

            string Get(string key) => momoParams.TryGetValue(key, out var val) ? val : "";

            // Tạo lại chuỗi raw hash kết quả từ các tham số nhận được
            var rawHash = $"accessKey={accessKey}" +
                          $"&amount={Get("amount")}" +
                          $"&extraData={Get("extraData")}" +
                          $"&message={Get("message")}" +
                          $"&orderId={Get("orderId")}" +
                          $"&orderInfo={Get("orderInfo")}" +
                          $"&orderType={Get("orderType")}" +
                          $"&partnerCode={Get("partnerCode")}" +
                          $"&payType={Get("payType")}" +
                          $"&requestId={Get("requestId")}" +
                          $"&responseTime={Get("responseTime")}" +
                          $"&resultCode={Get("resultCode")}" +
                          $"&transId={Get("transId")}";

            var computedSignature = ComputeHmacSha256(rawHash, secretKey);

            // So sánh an toàn dạng FixedTime (Tránh timing attack)
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(receivedSignature));
        }

        private static string ComputeHmacSha256(string data, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(dataBytes);
            return Convert.ToHexString(hashBytes).ToLower();
        }
    }
}
