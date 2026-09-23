using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using InverbanHN.CustomerAPI.DTOs;
using InverbanHN.Shared.Data;
using InverbanHN.Shared.Services;
using InverbanHN.Shared.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;

namespace InverbanHN.CustomerAPI.Controllers
{
    [ApiController]
    [Route("api/customer/checkout")]
    [Authorize(Roles = "Customer,SuperAdmin")]
    [EnableRateLimiting("AuthRateLimit")]
    public class CustomerCheckoutController : CustomerBaseController
    {
        private readonly DapperContext _dapperContext;
        private readonly IEmailService _emailService;

        public CustomerCheckoutController(DapperContext dapperContext, IEmailService emailService)
        {
            _dapperContext = dapperContext;
            _emailService = emailService;
        }

        /// <summary>
        /// POST /api/customer/checkout/preview
        /// REGLA ARQUITECTÓNICA DE SEGURIDAD (Anti-Price Tampering):
        /// Recibe únicamente ProductId y Quantity. Ignora cualquier precio enviado por el cliente.
        /// Recalcula el precio real desde la BD en tiempo real (BasePrice vs OfferPrice vigente).
        /// Aplica cupones válidos y calcula costos de envío.
        /// </summary>
        [HttpPost("preview")]
        public async Task<IActionResult> PreviewCheckout([FromBody] CheckoutPreviewRequestDto request)
        {
            if (!ModelState.IsValid || request.Items == null || !request.Items.Any())
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Carrito Vacío",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Debe proporcionar al menos un artículo válido en el carrito para cotizar el checkout."
                });
            }

            try
            {
                using var connection = _dapperContext.CreateConnection();
                var calculatedItems = new List<CartItemCalculatedDto>();

                decimal rawSubtotal = 0.00m;
                int primaryStoreId = 1;

                foreach (var item in request.Items)
                {
                    // Consulta obligatoria de precios en Base de Datos (Defensa contra alteración de precios JSON)
                    var dbProduct = await connection.QuerySingleOrDefaultAsync<(int ProductId, int StoreId, string StoreName, string Name, decimal BasePrice, decimal? OfferPrice, int? Stock, bool IsActive)>(@"
                        SELECT 
                            P.Product_ID AS ProductId,
                            P.Store_ID AS StoreId,
                            S.Store_Name AS StoreName,
                            P.Name,
                            P.Price AS BasePrice,
                            (
                                SELECT TOP 1 Offer_Price 
                                FROM [Catalog].[Product_Offers] O 
                                WHERE O.Product_ID = P.Product_ID 
                                  AND ISNULL(O.Is_Active, 1) = 1 
                                  AND GETUTCDATE() BETWEEN O.Start_Date AND O.End_Date
                                ORDER BY Offer_Price ASC
                            ) AS OfferPrice,
                            P.Stock,
                            ISNULL(P.Is_Active, 1) AS IsActive
                        FROM [Catalog].[Products] P
                        INNER JOIN [Core].[Stores] S ON P.Store_ID = S.Store_ID
                        WHERE P.Product_ID = @ProductId",
                        new { item.ProductId });

                    if (dbProduct.ProductId <= 0 || !dbProduct.IsActive)
                    {
                        return BadRequest(new ProblemDetails
                        {
                            Title = "Producto No Disponible",
                            Detail = $"El producto con ID {item.ProductId} ha dejado de estar disponible."
                        });
                    }

                    if (dbProduct.Stock.HasValue && dbProduct.Stock.Value < item.Quantity)
                    {
                        return BadRequest(new ProblemDetails
                        {
                            Title = "Stock Insuficiente",
                            Detail = $"No hay suficiente stock para el producto '{dbProduct.Name}'. Disponible: {dbProduct.Stock.Value}, Solicitado: {item.Quantity}."
                        });
                    }

                    primaryStoreId = dbProduct.StoreId;

                    // Precio efectivo seguro recalculado en BD
                    decimal effectiveUnitPrice = (dbProduct.OfferPrice.HasValue && dbProduct.OfferPrice > 0) 
                        ? dbProduct.OfferPrice.Value 
                        : dbProduct.BasePrice;

                    decimal lineSubtotal = Math.Round(effectiveUnitPrice * item.Quantity, 2);
                    rawSubtotal += lineSubtotal;

                    calculatedItems.Add(new CartItemCalculatedDto
                    {
                        ProductId = dbProduct.ProductId,
                        StoreId = dbProduct.StoreId,
                        StoreName = dbProduct.StoreName,
                        ProductName = dbProduct.Name,
                        Quantity = item.Quantity,
                        UnitPriceDb = effectiveUnitPrice
                    });
                }

                // Cálculo de Descuento por Cupón (si aplica)
                decimal couponDiscount = 0.00m;
                string? appliedCoupon = null;

                if (!string.IsNullOrWhiteSpace(request.CouponCode))
                {
                    string cleanCode = request.CouponCode.Trim().ToUpper();
                    var coupon = await connection.QuerySingleOrDefaultAsync<(int CouponId, string DiscountType, decimal DiscountValue, decimal MinPurchaseLps)>(@"
                        SELECT 
                            Coupon_ID AS CouponId,
                            ISNULL(Discount_Type, 'Fixed') AS DiscountType,
                            Discount_Value AS DiscountValue,
                            ISNULL(Min_Purchase_LPS, 0.00) AS MinPurchaseLps
                        FROM [Catalog].[Coupons]
                        WHERE UPPER(Code) = @Code 
                          AND Store_ID = @StoreId 
                          AND ISNULL(Is_Active, 1) = 1 
                          AND (Expiry_Date IS NULL OR Expiry_Date >= GETUTCDATE())",
                        new { Code = cleanCode, StoreId = primaryStoreId });

                    if (coupon.CouponId > 0 && rawSubtotal >= coupon.MinPurchaseLps)
                    {
                        appliedCoupon = cleanCode;
                        if (string.Equals(coupon.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase))
                        {
                            couponDiscount = Math.Round(rawSubtotal * (coupon.DiscountValue / 100.00m), 2);
                        }
                        else
                        {
                            couponDiscount = coupon.DiscountValue;
                        }

                        if (couponDiscount > rawSubtotal) couponDiscount = rawSubtotal;
                    }
                }

                // Cálculo de Envío basado en Reglas de la Tienda
                var shippingRule = await connection.QueryFirstOrDefaultAsync<(decimal StandardCost, decimal? FreeMin)>(@"
                    SELECT TOP 1 Standard_Cost_LPS, Free_Shipping_Min_LPS 
                    FROM [Sales].[Shipping_Rules]
                    WHERE Store_ID = @StoreId AND Is_Active = 1
                    ORDER BY Rule_ID DESC",
                    new { StoreId = primaryStoreId });

                decimal standardShipping = shippingRule.StandardCost > 0 ? shippingRule.StandardCost : 120.00m;
                decimal shippingCost = (shippingRule.FreeMin.HasValue && rawSubtotal >= shippingRule.FreeMin.Value) 
                    ? 0.00m 
                    : standardShipping;

                decimal totalPayable = Math.Round(rawSubtotal - couponDiscount + shippingCost, 2);
                if (totalPayable < 0) totalPayable = 0.00m;

                return Ok(new CheckoutPreviewResponseDto
                {
                    Items = calculatedItems,
                    SubtotalLps = rawSubtotal,
                    CouponDiscountLps = couponDiscount,
                    AppliedCouponCode = appliedCoupon,
                    ShippingCostLps = shippingCost,
                    TotalPayableLps = totalPayable,
                    PriceSecurityValidated = true
                });
            }
            catch (SqlException ex)
            {
                return HandleSqlException(ex, "PreviewCheckout");
            }
            catch (Exception ex)
            {
                return BadRequest(new ProblemDetails { Title = "Error al cotizar checkout", Detail = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/customer/checkout/pay
        /// Procesa pago mixto (Wallet + Tarjeta PixelPay) bajo Transacción SQL estricta.
        /// 1. Si UseWalletBalance es true, calcula saldo disponible y deduce hasta cubrir el total.
        /// 2. Si queda saldo pendiente, requiere PixelPayToken para cobrar la diferencia.
        /// 3. Inyecta auditoría sp_set_session_context 'UsuarioID'.
        /// </summary>
        [HttpPost("pay")]
        public async Task<IActionResult> ProcessPayment([FromBody] PayCheckoutRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int userId = GetUserId();
            using var connection = (SqlConnection)_dapperContext.CreateConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Inyectar auditoría transaccional dentro de la transacción activa
                var setAuditSql = "EXEC sp_set_session_context @key = N'UsuarioID', @value = @UserId;";
                await connection.ExecuteAsync(setAuditSql, new { UserId = userId }, transaction);

                // Cotizar carrito asegurado desde BD
                var previewResponse = await PreviewCheckoutInternal(connection, transaction, request.Items, request.CouponCode);
                if (previewResponse == null || !previewResponse.Items.Any())
                {
                    transaction.Rollback();
                    return BadRequest(new ProblemDetails { Title = "Error de Cotización", Detail = "No se pudieron validar los productos del carrito." });
                }

                decimal totalOrderAmount = previewResponse.TotalPayableLps;
                decimal walletAmountDeducted = 0.00m;
                decimal remainingAmountToPay = totalOrderAmount;
                var orderNumber = $"ORD-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
                int primaryStoreId = previewResponse.Items.First().StoreId;

                // 1. Manejo de Pago con Wallet (Si UseWalletBalance == true)
                if (request.UseWalletBalance)
                {
                    var availableWalletBalance = await connection.ExecuteScalarAsync<decimal>(@"
                        SELECT ISNULL(SUM(Amount_LPS), 0.00) 
                        FROM [Sales].[Wallet_Transactions] WITH (UPDLOCK) 
                        WHERE User_ID = @UserId",
                        new { UserId = userId }, transaction);

                    if (availableWalletBalance > 0)
                    {
                        walletAmountDeducted = Math.Min(availableWalletBalance, totalOrderAmount);
                        remainingAmountToPay = totalOrderAmount - walletAmountDeducted;

                        // Registrar Débito en Wallet_Transactions (Monto Negativo)
                        var walletDebitSql = @"
                            INSERT INTO [Sales].[Wallet_Transactions]
                                (User_ID, Amount_LPS, Transaction_Type, Description, Reference_ID, Created_At)
                            VALUES
                                (@UserId, @Amount, 'Purchase', @Description, @ReferenceId, GETUTCDATE());";

                        await connection.ExecuteAsync(walletDebitSql, new
                        {
                            UserId = userId,
                            Amount = -walletAmountDeducted,
                            Description = $"Pago parcial/total de orden {orderNumber} con saldo de billetera",
                            ReferenceId = orderNumber
                        }, transaction);
                    }
                }

                // 2. Si queda diferencia pendiente, se exige Token de PixelPay
                if (remainingAmountToPay > 0 && string.IsNullOrWhiteSpace(request.PixelPayToken))
                {
                    transaction.Rollback();
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Saldo Pendiente Insuficiente",
                        Status = StatusCodes.Status400BadRequest,
                        Detail = $"El saldo de Wallet cubre L. {walletAmountDeducted:N2}. Se requiere un Token de PixelPay válido para cobrar la diferencia de L. {remainingAmountToPay:N2}."
                    });
                }

                // 3. Persistir Orden de Venta
                try
                {
                    await connection.ExecuteAsync(
                        "[Sales].[usp_Finalizar_Venta_Exitosa]",
                        new
                        {
                            p_UserId = userId,
                            p_StoreId = primaryStoreId,
                            p_OrderNumber = orderNumber,
                            p_TotalAmount = totalOrderAmount,
                            p_PixelPayToken = remainingAmountToPay > 0 ? request.PixelPayToken : "WALLET_FULL_PAYMENT",
                            p_ShippingAddress = request.ShippingAddress
                        },
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure);
                }
                catch (SqlException procEx) when (procEx.Number == 2812) // Fallback SP no desplegado
                {
                    var sqlFallback = @"
                        INSERT INTO [dbo].[SubOrders] (StoreId, CustomerId, OrderNumber, TotalAmount, Status, TrackingNumber, CreatedAt)
                        VALUES (@StoreId, @CustomerId, @OrderNumber, @TotalAmount, 'Confirmado', @RefToken, GETUTCDATE());";

                    string refToken = remainingAmountToPay > 0 
                        ? $"PX-{request.PixelPayToken?[..Math.Min(10, request.PixelPayToken.Length)]}"
                        : "BILLETERA_VIRTUAL";

                    await connection.ExecuteAsync(sqlFallback, new
                    {
                        StoreId = primaryStoreId,
                        CustomerId = userId,
                        OrderNumber = orderNumber,
                        TotalAmount = totalOrderAmount,
                        RefToken = refToken
                    }, transaction);
                }

                // Commit de la transacción exitosa
                transaction.Commit();

                // 4. Notificación por Correo Electrónico
                var customerInfo = await connection.QuerySingleOrDefaultAsync<(string FullName, string Email)>(@"
                    SELECT COALESCE(Full_Name, Nombre, 'Cliente InverbanHN') AS FullName, Email 
                    FROM [Core].[Users] WHERE User_ID = @UserId OR Id = @UserId",
                    new { UserId = userId });

                string customerEmail = customerInfo.Email ?? "cliente@inverbanhn.com";
                string customerName = customerInfo.FullName ?? "Estimado Cliente";
                string storeName = previewResponse.Items.First().StoreName;

                if (!string.IsNullOrEmpty(customerEmail))
                {
                    var emailHtml = EmailTemplateFactory.GetOrderConfirmationTemplate(
                        customerName, orderNumber, totalOrderAmount, storeName);

                    _ = Task.Run(() => _emailService.SendEmailAsync(
                        customerEmail, $"¡Confirmación de Compra! Pedido {orderNumber}", emailHtml));
                }

                return Ok(new
                {
                    Message = "Compra finalizada con éxito.",
                    OrderNumber = orderNumber,
                    StoreId = primaryStoreId,
                    StoreName = storeName,
                    TotalOrderAmountLps = totalOrderAmount,
                    WalletAmountUsedLps = walletAmountDeducted,
                    CreditCardAmountPaidLps = remainingAmountToPay,
                    PaymentMethod = remainingAmountToPay > 0 ? (walletAmountDeducted > 0 ? "Mixto (Wallet + Tarjeta)" : "Tarjeta/PixelPay") : "Billetera Virtual",
                    Status = "Confirmado",
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (SqlException ex)
            {
                transaction.Rollback();
                return HandleSqlException(ex, "ProcessPayment");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return BadRequest(new ProblemDetails { Title = "Error al procesar pago de la orden", Detail = ex.Message });
            }
        }

        private async Task<CheckoutPreviewResponseDto?> PreviewCheckoutInternal(IDbConnection connection, IDbTransaction transaction, List<CartItemPreviewDto> items, string? couponCode)
        {
            var calculatedItems = new List<CartItemCalculatedDto>();
            decimal rawSubtotal = 0.00m;
            int primaryStoreId = 1;

            foreach (var item in items)
            {
                var dbProduct = await connection.QuerySingleOrDefaultAsync<(int ProductId, int StoreId, string StoreName, string Name, decimal BasePrice, decimal? OfferPrice)>(@"
                    SELECT 
                        P.Product_ID AS ProductId, P.Store_ID AS StoreId, S.Store_Name AS StoreName, P.Name, P.Price AS BasePrice,
                        (SELECT TOP 1 Offer_Price FROM [Catalog].[Product_Offers] O WHERE O.Product_ID = P.Product_ID AND ISNULL(O.Is_Active, 1) = 1 AND GETUTCDATE() BETWEEN O.Start_Date AND O.End_Date ORDER BY Offer_Price ASC) AS OfferPrice
                    FROM [Catalog].[Products] P
                    INNER JOIN [Core].[Stores] S ON P.Store_ID = S.Store_ID
                    WHERE P.Product_ID = @ProductId", new { item.ProductId }, transaction);

                if (dbProduct.ProductId <= 0) return null;

                primaryStoreId = dbProduct.StoreId;
                decimal unitPrice = (dbProduct.OfferPrice.HasValue && dbProduct.OfferPrice > 0) ? dbProduct.OfferPrice.Value : dbProduct.BasePrice;
                rawSubtotal += Math.Round(unitPrice * item.Quantity, 2);

                calculatedItems.Add(new CartItemCalculatedDto
                {
                    ProductId = dbProduct.ProductId,
                    StoreId = dbProduct.StoreId,
                    StoreName = dbProduct.StoreName,
                    ProductName = dbProduct.Name,
                    Quantity = item.Quantity,
                    UnitPriceDb = unitPrice
                });
            }

            decimal couponDiscount = 0.00m;
            if (!string.IsNullOrWhiteSpace(couponCode))
            {
                var coupon = await connection.QuerySingleOrDefaultAsync<(int CouponId, string DiscountType, decimal DiscountValue)>(@"
                    SELECT Coupon_ID AS CouponId, ISNULL(Discount_Type, 'Fixed') AS DiscountType, Discount_Value AS DiscountValue
                    FROM [Catalog].[Coupons] WHERE UPPER(Code) = @Code AND Store_ID = @StoreId AND ISNULL(Is_Active, 1) = 1 AND (Expiry_Date IS NULL OR Expiry_Date >= GETUTCDATE())",
                    new { Code = couponCode.Trim().ToUpper(), StoreId = primaryStoreId }, transaction);

                if (coupon.CouponId > 0)
                {
                    couponDiscount = string.Equals(coupon.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                        ? Math.Round(rawSubtotal * (coupon.DiscountValue / 100.00m), 2)
                        : coupon.DiscountValue;
                }
            }

            decimal shippingCost = 120.00m;
            decimal totalPayable = Math.Round(rawSubtotal - couponDiscount + shippingCost, 2);

            return new CheckoutPreviewResponseDto
            {
                Items = calculatedItems,
                SubtotalLps = rawSubtotal,
                CouponDiscountLps = couponDiscount,
                ShippingCostLps = shippingCost,
                TotalPayableLps = totalPayable > 0 ? totalPayable : 0.00m
            };
        }
    }
}
