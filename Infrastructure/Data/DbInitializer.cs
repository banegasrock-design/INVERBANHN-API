using System;
using System.Linq;
using Domain.Entities.Core;
using Infrastructure.Data.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public static class DbInitializer
{
    public static void Initialize(MarketplaceDbContext context)
    {
        void SafeExecute(string sql)
        {
            try
            {
                context.Database.ExecuteSqlRaw(sql);
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("db_init_log.txt", $"[WARN] SQL execution failed: {ex.Message}\nSQL: {sql}\n\n");
            }
        }

        // 1. Asegurar Esquemas
        SafeExecute("IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Core') EXEC('CREATE SCHEMA [Core]')");
        SafeExecute("IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Logistics') EXEC('CREATE SCHEMA [Logistics]')");
        SafeExecute("IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Catalog') EXEC('CREATE SCHEMA [Catalog]')");

        // Eliminar FKs desactualizadas que apuntaban a tablas viejas
        SafeExecute("IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Image_Product') ALTER TABLE [Logistics].[Product_Images] DROP CONSTRAINT [FK_Image_Product]");
        SafeExecute("IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Variant_Product') ALTER TABLE [Logistics].[Product_Variants] DROP CONSTRAINT [FK_Variant_Product]");

        // 2. Asegurar Tabla Users
        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Core].[Users]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Core].[Users] (
                    [User_ID] INT IDENTITY(1,1) NOT NULL,
                    [Full_Name] NVARCHAR(200) NOT NULL,
                    [Email] NVARCHAR(200) NOT NULL,
                    [Password_Hash] NVARCHAR(MAX) NOT NULL,
                    [Role_Name] NVARCHAR(50) DEFAULT 'Customer' NOT NULL,
                    [Is_Active] BIT DEFAULT 1 NOT NULL,
                    [Created_At] DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
                    CONSTRAINT [PK_Users] PRIMARY KEY ([User_ID])
                );
                CREATE UNIQUE INDEX [IX_Users_Email] ON [Core].[Users] ([Email]);
            END");

        SafeExecute("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Core].[Users]') AND name = 'Phone') ALTER TABLE [Core].[Users] ADD [Phone] NVARCHAR(50) NULL");

        // 3. Asegurar Tabla Stores
        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Core].[Stores]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Core].[Stores] (
                    [Store_ID] INT IDENTITY(1,1) NOT NULL,
                    [Store_Name] NVARCHAR(100) NOT NULL,
                    [Store_Slug] NVARCHAR(100) NULL,
                    [Tax_ID_RTN] NVARCHAR(20) NULL,
                    [Inventory_Mode] NVARCHAR(50) NOT NULL,
                    [Billing_Type] NVARCHAR(50) NOT NULL,
                    [Store_Type] NVARCHAR(50) DEFAULT 'Marketplace' NOT NULL,
                    [Created_At] DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
                    CONSTRAINT [PK_Stores] PRIMARY KEY ([Store_ID])
                );
            END");

        string[] storeCols = { "Store_Slug", "Owner_User_ID", "Is_Active", "HasIsrWithholding" };
        foreach (var col in storeCols)
        {
            string typeDef = col switch
            {
                "Is_Active" => "BIT DEFAULT 1",
                "HasIsrWithholding" => "BIT DEFAULT 0",
                "Owner_User_ID" => "INT NULL",
                _ => "NVARCHAR(100) NULL"
            };
            SafeExecute($"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Core].[Stores]') AND name = '{col}') ALTER TABLE [Core].[Stores] ADD [{col}] {typeDef}");
        }

        // Remover restricciones CHECK restrictivas sobre Billing_Type si existen
        SafeExecute(@"
            DECLARE @chkName NVARCHAR(200);
            SELECT @chkName = name FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('Core.Stores') AND definition LIKE '%Billing_Type%';
            IF @chkName IS NOT NULL EXEC('ALTER TABLE [Core].[Stores] DROP CONSTRAINT [' + @chkName + ']');
        ");

        // 4. Asegurar Tabla Categories y Products
        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Logistics].[Categories]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Logistics].[Categories] (
                    [Category_ID] INT IDENTITY(1,1) NOT NULL,
                    [Category_Name] NVARCHAR(100) NOT NULL,
                    [Category_Slug] NVARCHAR(100) NULL,
                    [Default_Commission_Percentage] DECIMAL(18,2) DEFAULT 0 NOT NULL,
                    CONSTRAINT [PK_Categories] PRIMARY KEY ([Category_ID])
                );
            END");

        SafeExecute("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Logistics].[Categories]') AND name = 'Category_Slug') ALTER TABLE [Logistics].[Categories] ADD [Category_Slug] NVARCHAR(100) NULL");

        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Catalog].[Products]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Catalog].[Products] (
                    [Product_ID] INT IDENTITY(1,1) NOT NULL,
                    [Store_ID] INT NOT NULL,
                    [SKU] NVARCHAR(100) NOT NULL,
                    [Name] NVARCHAR(200) NOT NULL,
                    [Description] NVARCHAR(MAX) NULL,
                    [Category_ID] INT NOT NULL,
                    [Brand] NVARCHAR(100) NULL,
                    [Weight_Kg] DECIMAL(18,2) NOT NULL,
                    [Width_Cm] DECIMAL(18,2) NOT NULL,
                    [Height_Cm] DECIMAL(18,2) NOT NULL,
                    [Length_Cm] DECIMAL(18,2) NOT NULL,
                    [Status_Name] NVARCHAR(50) DEFAULT 'Activo' NOT NULL,
                    [Created_At] DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
                    CONSTRAINT [PK_Products] PRIMARY KEY ([Product_ID])
                );
            END");

        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Logistics].[Product_Variants]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Logistics].[Product_Variants] (
                    [Variant_ID]          INT IDENTITY(1,1) NOT NULL,
                    [Product_ID]          INT NOT NULL,
                    [SKU]                 NVARCHAR(100) NOT NULL,
                    [Variant_Name]        NVARCHAR(200) NOT NULL,
                    [Variant_Description] NVARCHAR(MAX) NULL,
                    [Price_Amount]        DECIMAL(18,2) NOT NULL,
                    [Weight_Kg]           DECIMAL(18,2) NULL,
                    [Is_Digital_Download] BIT DEFAULT 0 NOT NULL,
                    [Offer_Type]          NVARCHAR(100) DEFAULT 'Sin Oferta' NOT NULL,
                    CONSTRAINT [PK_Product_Variants] PRIMARY KEY ([Variant_ID])
                );
            END");

        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Logistics].[Product_Images]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Logistics].[Product_Images] (
                    [Image_ID]         INT IDENTITY(1,1) NOT NULL,
                    [Product_ID]       INT NOT NULL,
                    [Variant_ID]       INT NULL,
                    [Image_URL]        NVARCHAR(500) NOT NULL,
                    [Thumbnail_URL]    NVARCHAR(500) NULL,
                    [Medium_URL]       NVARCHAR(500) NULL,
                    [Alt_Text]         NVARCHAR(200) NULL,
                    [Is_Main]          BIT DEFAULT 0 NOT NULL,
                    [Display_Order]    INT DEFAULT 0 NOT NULL,
                    [Mime_Type]        NVARCHAR(100) NULL,
                    [Image_Size_Bytes] BIGINT NULL,
                    [Created_At]       DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
                    CONSTRAINT [PK_Product_Images] PRIMARY KEY ([Image_ID])
                );
            END");

        // 4b. Asegurar Tablas de Logística, Ventas y Marketing
        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Carriers]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [dbo].[Carriers] (
                    [Id] INT IDENTITY(1,1) NOT NULL,
                    [Name] NVARCHAR(100) NOT NULL,
                    CONSTRAINT [PK_Carriers] PRIMARY KEY ([Id])
                );
            END");

        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SubOrders]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [dbo].[SubOrders] (
                    [Id] UNIQUEIDENTIFIER NOT NULL,
                    [OrderNumber] NVARCHAR(50) NOT NULL,
                    [TotalAmount] DECIMAL(18,2) NOT NULL,
                    [CustomerId] INT NOT NULL,
                    [StoreId] INT NOT NULL,
                    [CarrierId] INT NOT NULL,
                    [Status] NVARCHAR(MAX) NOT NULL,
                    [TrackingNumber] NVARCHAR(MAX) NULL,
                    [RequiresCarrierCollection] BIT NOT NULL,
                    [AmountToCollect] DECIMAL(18,2) NOT NULL,
                    CONSTRAINT [PK_SubOrders] PRIMARY KEY ([Id])
                );
            END");

        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ShippingRates]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [dbo].[ShippingRates] (
                    [Id] INT IDENTITY(1,1) NOT NULL,
                    [OriginCity] NVARCHAR(100) NOT NULL,
                    [DestinationCity] NVARCHAR(100) NOT NULL,
                    [BaseRate] DECIMAL(18,2) NOT NULL,
                    [CarrierId] INT NOT NULL,
                    CONSTRAINT [PK_ShippingRates] PRIMARY KEY ([Id])
                );
            END");

        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CustomerWallets]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [dbo].[CustomerWallets] (
                    [Id] INT IDENTITY(1,1) NOT NULL,
                    [CustomerId] INT NOT NULL,
                    [Balance] DECIMAL(18,2) NOT NULL,
                    [RowVersion] ROWVERSION NOT NULL,
                    CONSTRAINT [PK_CustomerWallets] PRIMARY KEY ([Id])
                );
            END");

        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GiftCards]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [dbo].[GiftCards] (
                    [Id] INT IDENTITY(1,1) NOT NULL,
                    [Code] NVARCHAR(50) NOT NULL,
                    [Amount] DECIMAL(18,2) NOT NULL,
                    [IsRedeemed] BIT NOT NULL,
                    [RedeemedAt] DATETIME2 NULL,
                    [RedeemedByWalletId] INT NULL,
                    CONSTRAINT [PK_GiftCards] PRIMARY KEY ([Id])
                );
            END");

        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WalletTransactions]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [dbo].[WalletTransactions] (
                    [Id] INT IDENTITY(1,1) NOT NULL,
                    [WalletId] INT NOT NULL,
                    [Amount] DECIMAL(18,2) NOT NULL,
                    [TransactionType] NVARCHAR(50) NOT NULL,
                    [ReferenceId] NVARCHAR(100) NULL,
                    [CreatedAt] DATETIME2 NOT NULL,
                    CONSTRAINT [PK_WalletTransactions] PRIMARY KEY ([Id])
                );
            END");

        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[User_Notification_Preferences]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [dbo].[User_Notification_Preferences] (
                    [Id] INT IDENTITY(1,1) NOT NULL,
                    [CustomerId] INT NOT NULL,
                    [Topic] NVARCHAR(50) NOT NULL,
                    [Channel] NVARCHAR(50) NOT NULL,
                    [IsEnabled] BIT NOT NULL,
                    CONSTRAINT [PK_User_Notification_Preferences] PRIMARY KEY ([Id])
                );
            END");

        // 5. Sembrar Usuario SuperAdmin
        try
        {
            if (!context.Users.Any(u => u.Email == "armando.banegas@inverbanhn.com"))
            {
                context.Users.Add(new User
                {
                    FullName = "Armando Banegas",
                    Email = "armando.banegas@inverbanhn.com",
                    PasswordHash = "SuperAdmin2026!",
                    RoleName = "SuperAdmin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                context.SaveChanges();
            }
        }
        catch { }

        var armando = context.Users.FirstOrDefault(u => u.Email == "armando.banegas@inverbanhn.com");
        int ownerId = armando?.Id ?? 1;

        // Sembrar Tiendas
        SafeExecute($@"
            IF NOT EXISTS (SELECT * FROM [Core].[Stores] WHERE Owner_User_ID = {ownerId})
            BEGIN
                INSERT INTO [Core].[Stores] (Store_Name, Store_Slug, Tax_ID_RTN, Inventory_Mode, Billing_Type, Store_Type, Created_At, Owner_User_ID, Is_Active)
                VALUES ('Tienda Principal Armando', 'tienda-principal-armando', '08019999000001', 'Ecommerce', 'Managed', 'Marketplace', GETUTCDATE(), {ownerId}, 1);
            END");

        SafeExecute($@"
            IF NOT EXISTS (SELECT * FROM [Core].[Stores] WHERE Store_Name = 'INVERBANHN')
            BEGIN
                INSERT INTO [Core].[Stores] (Store_Name, Store_Slug, Tax_ID_RTN, Inventory_Mode, Billing_Type, Store_Type, Created_At, Owner_User_ID, Is_Active)
                VALUES ('INVERBANHN', 'inverbanhn', '08019999000002', 'Ecommerce', 'Managed', 'Marketplace', GETUTCDATE(), {ownerId}, 1);
            END");

        // Sembrar Categorías
        SafeExecute(@"
            IF NOT EXISTS (SELECT * FROM [Logistics].[Categories])
            BEGIN
                INSERT INTO [Logistics].[Categories] (Category_Name, Category_Slug, Default_Commission_Percentage)
                VALUES 
                    ('Electrónica', 'electronica', 10.0), 
                    ('Hogar', 'hogar', 5.0), 
                    ('Ferretería', 'ferreteria', 8.0),
                    ('Logística', 'logistica', 8.0),
                    ('Servicios Profesionales', 'servicios-profesionales', 12.0);
            END");

        // Sembrar Productos de INVERBANHN en la Base de Datos SQL
        SafeExecute(@"
            DECLARE @StoreId INT = (SELECT TOP 1 Store_ID FROM [Core].[Stores] WHERE Store_Name = 'INVERBANHN');
            IF @StoreId IS NULL SET @StoreId = 1;

            DECLARE @CatLogistica INT = (SELECT TOP 1 Category_ID FROM [Logistics].[Categories] WHERE Category_Name = 'Logística');
            DECLARE @CatElectronica INT = (SELECT TOP 1 Category_ID FROM [Logistics].[Categories] WHERE Category_Name = 'Electrónica');
            DECLARE @CatServicios INT = (SELECT TOP 1 Category_ID FROM [Logistics].[Categories] WHERE Category_Name = 'Servicios Profesionales');

            IF @CatLogistica IS NULL SET @CatLogistica = 1;
            IF @CatElectronica IS NULL SET @CatElectronica = 1;
            IF @CatServicios IS NULL SET @CatServicios = 1;

            IF NOT EXISTS (SELECT * FROM [Catalog].[Products] WHERE SKU = 'IVB-BOX-001')
                INSERT INTO [Catalog].[Products] (Store_ID, SKU, Name, Description, Category_ID, Brand, Weight_Kg, Width_Cm, Height_Cm, Length_Cm, Status_Name, Created_At)
                VALUES (@StoreId, 'IVB-BOX-001', 'Caja de Cartón Corrugado - Mediana (Pack 25)', 'Cajas de alta resistencia certificadas por CAEX y Cargo Expreso.', @CatLogistica, 'InverbanHN Logistics', 8.5, 40, 30, 30, 'Activo', GETUTCDATE());

            IF NOT EXISTS (SELECT * FROM [Catalog].[Products] WHERE SKU = 'IVB-PRN-002')
                INSERT INTO [Catalog].[Products] (Store_ID, SKU, Name, Description, Category_ID, Brand, Weight_Kg, Width_Cm, Height_Cm, Length_Cm, Status_Name, Created_At)
                VALUES (@StoreId, 'IVB-PRN-002', 'Impresora Térmica de Etiquetas Zebra ZD220', 'Impresora industrial de transferencia térmica para guías logísticas de despacho.', @CatElectronica, 'Zebra', 2.1, 22, 17, 28, 'Activo', GETUTCDATE());

            IF NOT EXISTS (SELECT * FROM [Catalog].[Products] WHERE SKU = 'IVB-SRV-003')
                INSERT INTO [Catalog].[Products] (Store_ID, SKU, Name, Description, Category_ID, Brand, Weight_Kg, Width_Cm, Height_Cm, Length_Cm, Status_Name, Created_At)
                VALUES (@StoreId, 'IVB-SRV-003', 'Servicio de Embalaje Profesional In-Situ', 'Embalaje a medida de mercancías pesadas y frágiles en las bodegas del cliente.', @CatServicios, 'InverbanHN Express', 0, 0, 0, 0, 'Activo', GETUTCDATE());

            IF NOT EXISTS (SELECT * FROM [Catalog].[Products] WHERE SKU = 'IVB-DIG-004')
                INSERT INTO [Catalog].[Products] (Store_ID, SKU, Name, Description, Category_ID, Brand, Weight_Kg, Width_Cm, Height_Cm, Length_Cm, Status_Name, Created_At)
                VALUES (@StoreId, 'IVB-DIG-004', 'Cupo de Envíos CAEX Prepago (10 Guías Nacionales)', 'Paquete digital prepagado para la cotización instantánea de 10 guías de transporte.', @CatLogistica, 'CAEX Honduras', 0, 0, 0, 0, 'Activo', GETUTCDATE());

            IF NOT EXISTS (SELECT * FROM [Catalog].[Products] WHERE SKU = 'IVB-SCL-005')
                INSERT INTO [Catalog].[Products] (Store_ID, SKU, Name, Description, Category_ID, Brand, Weight_Kg, Width_Cm, Height_Cm, Length_Cm, Status_Name, Created_At)
                VALUES (@StoreId, 'IVB-SCL-005', 'Báscula Industrial Digital 150Kg', 'Báscula de alta precisión con plataforma de acero para pesaje de paquetería.', @CatElectronica, 'Rhino', 5.2, 40, 10, 50, 'Activo', GETUTCDATE());

            IF NOT EXISTS (SELECT * FROM [Catalog].[Products] WHERE SKU = 'IVB-SRV-006')
                INSERT INTO [Catalog].[Products] (Store_ID, SKU, Name, Description, Category_ID, Brand, Weight_Kg, Width_Cm, Height_Cm, Length_Cm, Status_Name, Created_At)
                VALUES (@StoreId, 'IVB-SRV-006', 'Consultoría SAR - Configuración de Autoimpresor', 'Asesoría experta para la obtención de rangos CAI y facturación oficial.', @CatServicios, 'InverbanHN Legal', 0, 0, 0, 0, 'Activo', GETUTCDATE());

            -- Asegurar Precios e Imágenes en SQL Database
            INSERT INTO [Logistics].[Product_Variants] (Product_ID, SKU, Variant_Name, Variant_Description, Price_Amount, Offer_Type)
            SELECT p.Product_ID, p.SKU, p.Name, p.Description, 
                   CASE p.SKU 
                        WHEN 'IVB-BOX-001' THEN 450.00
                        WHEN 'IVB-PRN-002' THEN 4800.00
                        WHEN 'IVB-SRV-003' THEN 1200.00
                        WHEN 'IVB-DIG-004' THEN 1850.00
                        WHEN 'IVB-SCL-005' THEN 3500.00
                        WHEN 'IVB-SRV-006' THEN 2500.00
                        ELSE 350.00 
                   END, 
                   'Sin Oferta'
            FROM [Catalog].[Products] p
            WHERE NOT EXISTS (SELECT 1 FROM [Logistics].[Product_Variants] v WHERE v.Product_ID = p.Product_ID);

            INSERT INTO [Logistics].[Product_Images] (Product_ID, Image_URL, Alt_Text, Is_Main, Display_Order)
            SELECT p.Product_ID,
                   CASE p.SKU
                        WHEN 'IVB-BOX-001' THEN 'https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?auto=format&fit=crop&w=800&q=80'
                        WHEN 'IVB-PRN-002' THEN 'https://images.unsplash.com/photo-1612815154858-60aa4c59eaa6?auto=format&fit=crop&w=800&q=80'
                        WHEN 'IVB-SRV-003' THEN 'https://images.unsplash.com/photo-1578575437130-527eed3abbec?auto=format&fit=crop&w=800&q=80'
                        WHEN 'IVB-DIG-004' THEN 'https://images.unsplash.com/photo-1526304640581-d334cdbbf45e?auto=format&fit=crop&w=800&q=80'
                        WHEN 'IVB-SCL-005' THEN 'https://images.unsplash.com/photo-1526170375885-4d8ecf77b99f?auto=format&fit=crop&w=800&q=80'
                        WHEN 'IVB-SRV-006' THEN 'https://images.unsplash.com/photo-1450133064473-71024230f91b?auto=format&fit=crop&w=800&q=80'
                        ELSE 'https://images.unsplash.com/photo-1523275335684-37898b6baf30?auto=format&fit=crop&w=800&q=80'
                   END,
                   p.Name, 1, 1
            FROM [Catalog].[Products] p
            WHERE NOT EXISTS (SELECT 1 FROM [Logistics].[Product_Images] img WHERE img.Product_ID = p.Product_ID);
        ");
    }
}
