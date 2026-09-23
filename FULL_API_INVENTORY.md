# Inventario Maestro de Endpoints — INVERBANHN API
**Versión**: 1.1.0 (SuperAdmin & Identity)
**Estado**: Desarrollo Activo

Este documento contiene el listado completo de los endpoints disponibles en la API del Ecosistema Marketplace, organizados por módulo y nivel de acceso.

---

## 1. Seguridad y Acceso (Core)

### 1.1 Autenticación (`AuthController`) [NEW]
- `POST /api/auth/login`: Iniciar sesión (Devuelve JWT para Armando Banegas y otros usuarios).

---

## 2. Administración de Sistema (Admin)

### 2.1 Analytics (`AdminAnalyticsController`)
- `GET /api/admin/analytics/gift-cards/liability`: Pasivo por Gift Cards.
- `GET /api/admin/analytics/billing/pending-upload`: Facturas pendientes de carga.
- `GET /api/admin/analytics/notifications/marketing-reach`: Estadísticas de alcance.

### 2.2 Gestión de Tiendas (`StoreAdminController`)
- `POST /api/admin/stores`: Crear tienda (Configuración RTN/CAI).
- `GET /api/admin/stores/{id}`: Obtener detalle de tienda.

### 2.3 Clientes (`CustomerAdminController`) [NEW]
- `GET /api/admin/customers`: Buscar clientes (Nombre/Email/Tel).
- `GET /api/admin/customers/{id}`: Detalle de perfil de cliente.
- `PUT /api/admin/customers/{id}`: Editar información (Requiere Auditoría SQL).
- `DELETE /api/admin/customers/{id}`: Eliminar cuenta (Requiere Auditoría SQL).
- `GET /api/admin/customers/{id}/snapshot`: **Vista 360 del Cliente**. Devuelve en un solo JSON:
    - Perfil básico.
    - Últimas 5 direcciones de envío.
    - Saldo actual en Wallet.
    - Últimas 3 facturas emitidas (SAR).

### 2.4 Órdenes (`AdminOrderController`)
- `POST /api/admin/orders/cancel-refund`: Cancelación de orden con reembolso automático a Wallet y notificación SignalR.

---

## 3. Catálogo y Marketing (Catalog / Marketing)

### 3.1 Productos (`ProductController`)
- `GET /api/products`: Catálogo público (Filtros: Categoría, Precio, Tienda).
- `GET /api/products/{id}`: Detalle técnico del producto.
- `POST /api/products`: Crear producto (Solo Vendedores/Admin).
- `PUT /api/products/{id}`: Actualizar stock y precios.

### 3.2 Cupones y Promociones (`PromotionController`)
- `POST /api/promotions/validate`: Validar cupón de descuento.
- `GET /api/promotions/active`: Listar banners y campañas vigentes.

---

## 4. Ventas y Checkout (Sales)

### 4.1 Checkout (`CheckoutController`)
- `POST /api/checkout/initialize`: Crear intención de pago.
- `POST /api/checkout/pixelpay-webhook`: Procesar respuesta de pasarela (PixelPay). Ejecuta `[Sales].[usp_Finalizar_Venta_Exitosa]`.

### 4.2 Impuestos y Facturación (`TaxController`)
- `GET /api/tax/isr-withholding`: Consultar retenciones generadas (1% ISR).
- `POST /api/tax/sar-upload`: Carga de facturas manuales para tiendas sin autoimpresor.

---

## 5. Logística y Envíos (Carrier / Ops)

### 5.1 Despacho (`DispatchController`)
- `POST /api/dispatch/process/{subOrderId}`: Iniciar proceso de despacho y asignación de motorista.

### 5.2 Integración Carrier (`CarrierController`)
- `POST /api/carrier/quote`: Cotizar envío con múltiples operadoras (CAEX, Cargo Expreso).
- `POST /api/carrier/generate-guide/{subOrderId}`: Generar guía de transporte oficial.
- `POST /api/carrier/webhook`: Notificación de cambio de estado de guía (Entregado, Rechazado).

---

## 6. Servicios Financieros (Customer / Wallet)

### 6.1 Wallet (`WalletController`)
- `POST /api/wallet/{customerId}/redeem`: Canjear Gift Card por saldo.
- `POST /api/wallet/{customerId}/mixed-payment`: Pago combinado (Saldo Wallet + Tarjeta).

### 6.2 Reportes (`ReportsController`)
- `GET /api/reports/bank-exports`: Exportación de archivos para pagos masivos a tiendas.

---
**Nota**: Este inventario refleja la estructura actual de controladores en la solución .NET 10. Los endpoints marcados como `[NEW]` fueron implementados para fortalecer la seguridad y la visibilidad administrativa de Armando Banegas.
