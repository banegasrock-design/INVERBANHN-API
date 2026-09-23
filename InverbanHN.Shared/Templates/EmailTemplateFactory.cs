using System;

namespace InverbanHN.Shared.Templates
{
    public static class EmailTemplateFactory
    {
        private const string PrimaryMagenta = "#E20074";
        private const string BackgroundDark = "#111827";
        private const string BackgroundLight = "#F9FAFB";
        private const string CardBackground = "#FFFFFF";

        /// <summary>
        /// Plantilla 1: Confirmación de Compra Exitosamente Procesada
        /// </summary>
        public static string GetOrderConfirmationTemplate(string customerName, string orderNumber, decimal total, string storeName)
        {
            return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Confirmación de Compra - {orderNumber}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: {BackgroundLight}; font-family: 'Segoe UI', Arial, sans-serif; color: #1F2937;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse; padding: 20px 0;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" style=""width: 100%; max-width: 600px; background-color: {CardBackground}; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08);"">
                    <!-- Header Magenta -->
                    <tr>
                        <td style=""background-color: {PrimaryMagenta}; padding: 30px; text-align: center;"">
                            <h1 style=""color: #FFFFFF; margin: 0; font-size: 26px; font-weight: bold; letter-spacing: 1px;"">INVERBAN<span style=""color: #FFD700;"">HN</span></h1>
                            <p style=""color: #FCE7F3; margin: 5px 0 0 0; font-size: 14px;"">Marketplace Oficial de Honduras</p>
                        </td>
                    </tr>
                    <!-- Body Content -->
                    <tr>
                        <td style=""padding: 40px 30px;"">
                            <h2 style=""color: {BackgroundDark}; margin-top: 0;"">¡Gracias por tu compra, {customerName}! 🎉</h2>
                            <p style=""font-size: 16px; line-height: 1.6; color: #4B5563;"">
                                Tu pedido ha sido confirmado con éxito. El comercio <strong>{storeName}</strong> se encuentra preparando tus productos para el envío.
                            </p>
                            
                            <!-- Box Detalles -->
                            <div style=""background-color: #F3F4F6; border-left: 4px solid {PrimaryMagenta}; padding: 20px; border-radius: 6px; margin: 25px 0;"">
                                <p style=""margin: 0 0 10px 0; font-size: 15px;""><strong>Número de Orden:</strong> <span style=""color: {PrimaryMagenta}; font-weight: bold;"">{orderNumber}</span></p>
                                <p style=""margin: 0 0 10px 0; font-size: 15px;""><strong>Tienda Responsable:</strong> {storeName}</p>
                                <p style=""margin: 0; font-size: 18px;""><strong>Total Pagado:</strong> <span style=""color: #059669; font-weight: bold;"">L. {total:N2}</span></p>
                            </div>

                            <p style=""font-size: 14px; color: #6B7280; text-align: center; margin-top: 30px;"">
                                Puedes seguir el estado de tu pedido en tiempo real desde tu perfil de usuario en InverbanHN.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: {BackgroundDark}; padding: 20px; text-align: center; color: #9CA3AF; font-size: 12px;"">
                            © {DateTime.UtcNow.Year} InverbanHN. Todos los derechos reservados. | Honduras
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        /// <summary>
        /// Plantilla 2: Actualización de Estado de Orden (Envío y Número de Guía)
        /// </summary>
        public static string GetOrderStatusChangedTemplate(string customerName, string orderNumber, string newStatus, string? trackingNumber)
        {
            string trackingSection = !string.IsNullOrWhiteSpace(trackingNumber) 
                ? $@"<div style=""background-color: #EEF2FF; border: 1px dashed #6366F1; padding: 15px; border-radius: 6px; margin-top: 15px; text-align: center;"">
                        <p style=""margin: 0; font-size: 13px; color: #4338CA;"">Número de Guía / Tracking Logístico:</p>
                        <p style=""margin: 5px 0 0 0; font-size: 20px; font-weight: bold; color: #1E1B4B; letter-spacing: 2px;"">{trackingNumber}</p>
                     </div>" 
                : "";

            return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Actualización de Pedido - {orderNumber}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: {BackgroundLight}; font-family: 'Segoe UI', Arial, sans-serif; color: #1F2937;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse; padding: 20px 0;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" style=""width: 100%; max-width: 600px; background-color: {CardBackground}; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08);"">
                    <!-- Header Magenta -->
                    <tr>
                        <td style=""background-color: {PrimaryMagenta}; padding: 25px; text-align: center;"">
                            <h1 style=""color: #FFFFFF; margin: 0; font-size: 24px; font-weight: bold;"">INVERBANHN LOGÍSTICA</h1>
                        </td>
                    </tr>
                    <!-- Body Content -->
                    <tr>
                        <td style=""padding: 35px 30px;"">
                            <h2 style=""color: {BackgroundDark}; margin-top: 0;"">Hola, {customerName} 👋</h2>
                            <p style=""font-size: 16px; line-height: 1.6; color: #4B5563;"">
                                El estado de tu pedido <strong>{orderNumber}</strong> ha sido actualizado a:
                            </p>
                            
                            <!-- Badge de Estado -->
                            <div style=""text-align: center; margin: 25px 0;"">
                                <span style=""background-color: {PrimaryMagenta}; color: #FFFFFF; padding: 10px 25px; border-radius: 30px; font-size: 18px; font-weight: bold; display: inline-block;"">
                                    🚚 {newStatus.ToUpper()}
                                </span>
                            </div>

                            {trackingSection}

                            <p style=""font-size: 14px; color: #6B7280; margin-top: 30px;"">
                                Tu paquete ya se encuentra en ruta para la entrega. Gracias por confiar en el comercio local.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: {BackgroundDark}; padding: 20px; text-align: center; color: #9CA3AF; font-size: 12px;"">
                            © {DateTime.UtcNow.Year} InverbanHN Marketplace | Notificación Automática de Envío
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        /// <summary>
        /// Plantilla 3: Alerta de Seguridad (Cambio de Contraseña u Operación Crítica)
        /// </summary>
        public static string GetSecurityAlertTemplate(string userName, string action)
        {
            return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Alerta de Seguridad - InverbanHN</title>
</head>
<body style=""margin: 0; padding: 0; background-color: {BackgroundLight}; font-family: 'Segoe UI', Arial, sans-serif; color: #1F2937;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse; padding: 20px 0;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" style=""width: 100%; max-width: 600px; background-color: {CardBackground}; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08);"">
                    <!-- Header Dark/Magenta -->
                    <tr>
                        <td style=""background-color: {BackgroundDark}; border-bottom: 4px solid {PrimaryMagenta}; padding: 25px; text-align: center;"">
                            <h1 style=""color: #FFFFFF; margin: 0; font-size: 22px; font-weight: bold;"">🔒 SEGURIDAD DE LA CUENTA</h1>
                        </td>
                    </tr>
                    <!-- Body Content -->
                    <tr>
                        <td style=""padding: 35px 30px;"">
                            <h2 style=""color: {BackgroundDark}; margin-top: 0;"">Estimado/a {userName},</h2>
                            <p style=""font-size: 16px; line-height: 1.6; color: #4B5563;"">
                                Te notificamos que se ha realizado la siguiente acción de seguridad en tu cuenta de comercio:
                            </p>
                            
                            <div style=""background-color: #FEF2F2; border-left: 4px solid #EF4444; padding: 15px; border-radius: 6px; margin: 20px 0;"">
                                <p style=""margin: 0; font-size: 16px; color: #991B1B; font-weight: bold;"">Acción: {action}</p>
                                <p style=""margin: 5px 0 0 0; font-size: 13px; color: #B91C1C;"">Fecha: {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC</p>
                            </div>

                            <p style=""font-size: 14px; color: #4B5563; line-height: 1.5;"">
                                Si fuiste tú quien realizó este cambio, puedes ignorar este mensaje de seguridad. 
                                <strong style=""color: #DC2626;"">Si NO reconoces esta actividad, por favor contacta inmediatamente a soporte de InverbanHN.</strong>
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: {BackgroundDark}; padding: 20px; text-align: center; color: #9CA3AF; font-size: 12px;"">
                            © {DateTime.UtcNow.Year} InverbanHN Security Team | Alerta Automatizada
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }
    }
}
