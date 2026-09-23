using System.Data;
using Dapper;

namespace InverbanHN.Shared.Data;

public static class DbInitializer
{
    public static void EnsureTablesExist(IDbConnection connection)
    {
        try
        {
            var ddl = @"
                IF OBJECT_ID('dbo.SubOrders', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.SubOrders (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        StoreId INT NOT NULL DEFAULT 1,
                        CustomerId INT NULL,
                        OrderNumber NVARCHAR(100) NOT NULL,
                        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
                        Status NVARCHAR(50) NOT NULL DEFAULT 'Confirmado',
                        TrackingNumber NVARCHAR(100) NULL,
                        CreatedAt DATETIME2 DEFAULT GETUTCDATE()
                    );
                END;

                IF SCHEMA_ID('Sales') IS NULL
                    EXEC('CREATE SCHEMA [Sales]');

                IF OBJECT_ID('Sales.Payout_Requests', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Sales].[Payout_Requests] (
                        Payout_ID INT IDENTITY(1,1) PRIMARY KEY,
                        Store_ID INT NOT NULL,
                        Requested_Amount_LPS DECIMAL(18,2) NOT NULL,
                        Bank_Name NVARCHAR(150) NOT NULL,
                        Bank_Account_Number NVARCHAR(100) NOT NULL,
                        Account_Holder_Name NVARCHAR(150) NOT NULL,
                        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
                        Admin_Notes NVARCHAR(MAX) NULL,
                        Created_At DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        Processed_At DATETIME2 NULL
                    );
                END;

                IF OBJECT_ID('Sales.Shipping_Rules', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Sales].[Shipping_Rules] (
                        Rule_ID INT IDENTITY(1,1) PRIMARY KEY,
                        Store_ID INT NOT NULL,
                        Zone_Name NVARCHAR(100) NOT NULL DEFAULT 'Nacional', -- Ej: San Pedro Sula, Tegucigalpa, Nacional
                        Standard_Cost_LPS DECIMAL(18,2) NOT NULL DEFAULT 120.00,
                        Free_Shipping_Min_LPS DECIMAL(18,2) NULL, -- Ej: 1000.00 (Gratis si Subtotal >= 1000)
                        Estimated_Days INT NOT NULL DEFAULT 2,
                        Is_Active BIT NOT NULL DEFAULT 1,
                        Created_At DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF SCHEMA_ID('Core') IS NULL
                    EXEC('CREATE SCHEMA [Core]');

                IF OBJECT_ID('Core.Addresses', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Core].[Addresses] (
                        Address_ID INT IDENTITY(1,1) PRIMARY KEY,
                        User_ID INT NOT NULL,
                        Address_Line1 NVARCHAR(250) NOT NULL,
                        Address_Line2 NVARCHAR(250) NULL,
                        City NVARCHAR(100) NOT NULL,
                        Department NVARCHAR(100) NOT NULL, -- Ej: Cortés, Francisco Morazán
                        Zip_Code NVARCHAR(20) NULL,
                        Is_Default BIT NOT NULL DEFAULT 0,
                        Created_At DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF OBJECT_ID('Sales.Wallet_Transactions', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Sales].[Wallet_Transactions] (
                        Transaction_ID INT IDENTITY(1,1) PRIMARY KEY,
                        User_ID INT NOT NULL,
                        Amount_LPS DECIMAL(18,2) NOT NULL, -- Positivo para Crédito/Reembolso/Depósito, Negativo para Débito/Compra
                        Transaction_Type NVARCHAR(50) NOT NULL, -- 'Deposit', 'Refund', 'Purchase', 'Adjustment'
                        Description NVARCHAR(250) NULL,
                        Reference_ID NVARCHAR(100) NULL, -- Ej: OrderNumber o GiftCardCode
                        Created_At DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF OBJECT_ID('Core.Stores', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Core].[Stores] (
                        Store_ID INT IDENTITY(1,1) PRIMARY KEY,
                        Store_Name NVARCHAR(150) NOT NULL,
                        RTN NVARCHAR(14) NULL,
                        Inventory_Mode NVARCHAR(50) NOT NULL DEFAULT 'Tienda',
                        Billing_Type NVARCHAR(50) NOT NULL DEFAULT 'Managed',
                        CAI NVARCHAR(100) NULL,
                        Range_Start BIGINT NULL,
                        Range_End BIGINT NULL,
                        CAI_Expiry_Date DATETIME2 NULL,
                        Is_Active BIT NOT NULL DEFAULT 1,
                        Created_At DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF OBJECT_ID('Core.User_Stores', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Core].[User_Stores] (
                        User_Store_ID INT IDENTITY(1,1) PRIMARY KEY,
                        User_ID INT NOT NULL,
                        Store_ID INT NOT NULL,
                        Role NVARCHAR(50) NOT NULL DEFAULT 'StoreAdmin',
                        Assigned_At DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF OBJECT_ID('Core.Api_Keys', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Core].[Api_Keys] (
                        Key_ID INT IDENTITY(1,1) PRIMARY KEY,
                        App_Name NVARCHAR(100) NOT NULL,
                        Key_Hash NVARCHAR(256) NOT NULL,
                        Prefix NVARCHAR(20) NOT NULL,
                        Is_Active BIT NOT NULL DEFAULT 1,
                        Created_At DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF OBJECT_ID('Core.Audit_Logs', 'U') IS NULL
                BEGIN
                    CREATE TABLE [Core].[Audit_Logs] (
                        Log_ID INT IDENTITY(1,1) PRIMARY KEY,
                        Table_Name NVARCHAR(100) NOT NULL,
                        Action NVARCHAR(50) NOT NULL,
                        Operator_User_ID INT NOT NULL,
                        Details NVARCHAR(MAX) NULL,
                        Created_At DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END";

            connection.Execute(ddl);
        }
        catch
        {
            // Ignore DDL errors if read-only permissions
        }
    }
}
