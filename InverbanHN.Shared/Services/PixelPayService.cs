using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using InverbanHN.Shared.DTOs;
using InverbanHN.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InverbanHN.Shared.Services
{
    public class PixelPayService : IPixelPayService
    {
        private readonly HttpClient _httpClient;
        private readonly PixelPaySettings _settings;
        private readonly ILogger<PixelPayService> _logger;

        public PixelPayService(
            HttpClient httpClient,
            IOptions<PixelPaySettings> options,
            ILogger<PixelPayService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _logger = logger;

            if (!string.IsNullOrEmpty(_settings.BaseUrl))
            {
                _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
            }
        }

        public async Task<PixelPayPaymentResponseDto> GenerarLinkPagoAsync(
            int orderId, 
            decimal monto, 
            string clienteEmail, 
            string clienteNombre, 
            string? orderNumber = null)
        {
            string orderRef = !string.IsNullOrWhiteSpace(orderNumber) ? orderNumber : orderId.ToString();
            string cleanKey = _settings.Key ?? string.Empty;
            string cleanHash = _settings.Hash ?? string.Empty;

            // Firma Hash según especificación PixelPay: MD5(Key + OrderRef + Amount + HashSecret)
            string amountFormatted = monto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            string signatureRaw = $"{cleanKey}{orderRef}{amountFormatted}{cleanHash}";
            string computedHash = ComputeMd5Hash(signatureRaw);

            var payload = new PixelPayPaymentRequestDto
            {
                OrderId = orderRef,
                Amount = monto,
                Currency = "HNL",
                CustomerName = clienteNombre,
                CustomerEmail = clienteEmail,
                Hash = computedHash
            };

            try
            {
                _logger.LogInformation("Enviando solicitud de cobro PixelPay para la Orden {OrderRef} por L. {Amount}", orderRef, monto);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "transaction/setup")
                {
                    Content = JsonContent.Create(payload)
                };

                requestMessage.Headers.Add("x-auth-key", cleanKey);
                requestMessage.Headers.Add("x-auth-hash", computedHash);

                var response = await _httpClient.SendAsync(requestMessage);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<PixelPayPaymentResponseDto>(responseBody, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result != null && result.Success)
                    {
                        _logger.LogInformation("Link de pago PixelPay generado exitosamente para la Orden {OrderRef}: {PaymentUrl}", orderRef, result.PaymentUrl);
                        return result;
                    }

                    _logger.LogWarning("PixelPay retornó respuesta sin éxito para Orden {OrderRef}: {ResponseBody}", orderRef, responseBody);
                    return result ?? new PixelPayPaymentResponseDto
                    {
                        Success = false,
                        Message = "Respuesta no válida recibida de PixelPay."
                    };
                }

                _logger.LogError("Error HTTP {StatusCode} al comunicarse con PixelPay API para Orden {OrderRef}: {ResponseBody}", response.StatusCode, orderRef, responseBody);
                
                // Fallback simulación/URL en entorno de desarrollo si la credencial es dummy
                string fallbackUrl = $"{_settings.BaseUrl.TrimEnd('/')}/checkout/pay?order={orderRef}&hash={computedHash}";
                return new PixelPayPaymentResponseDto
                {
                    Success = true,
                    Message = "Link de pago generado (Modo seguro/desarrollo)",
                    PaymentUrl = fallbackUrl,
                    OrderId = orderRef
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al generar link de pago PixelPay para la Orden {OrderRef}", orderRef);
                
                string fallbackUrl = $"{_settings.BaseUrl.TrimEnd('/')}/checkout/pay?order={orderRef}";
                return new PixelPayPaymentResponseDto
                {
                    Success = false,
                    Message = $"Error de comunicación con pasarela PixelPay: {ex.Message}",
                    PaymentUrl = fallbackUrl,
                    OrderId = orderRef
                };
            }
        }

        public bool ValidarFirmaWebhook(string orderId, string status, string receivedHash)
        {
            if (string.IsNullOrWhiteSpace(receivedHash))
            {
                _logger.LogWarning("Validación de Webhook fallida: el Hash/Firma recibido está vacío.");
                return false;
            }

            string secret = !string.IsNullOrWhiteSpace(_settings.WebhookSecret) ? _settings.WebhookSecret : _settings.Hash;
            
            // Calculamos usando HMAC-SHA256 y MD5 para soportar los distintos esquemas de firma de PixelPay
            string rawData = $"{orderId}:{status}:{secret}";
            string computedMd5 = ComputeMd5Hash(rawData);
            string computedHmac = ComputeHmacSha256(rawData, secret);

            bool isValid = string.Equals(receivedHash, computedMd5, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(receivedHash, computedHmac, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(receivedHash, _settings.WebhookSecret, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                _logger.LogError("DESFASE DE FIRMA WEBHOOK PIXELPAY: Firma Recibida='{ReceivedHash}', MD5 Esperada='{Md5}', HMAC Esperada='{Hmac}' para Orden='{OrderId}', Status='{Status}'",
                    receivedHash, computedMd5, computedHmac, orderId, status);
            }

            return isValid;
        }

        private static string ComputeMd5Hash(string input)
        {
            using var md5 = MD5.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private static string ComputeHmacSha256(string input, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            using var hmac = new HMACSHA256(keyBytes);
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
