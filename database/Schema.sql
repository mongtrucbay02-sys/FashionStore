-- FashionStore relational schema, SQL Server 2017+ (compatibility level 140+).
-- Run in the target database. Re-running updates procedures without deleting data.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF OBJECT_ID(N'dbo.SchemaVersions',N'U') IS NULL
CREATE TABLE dbo.SchemaVersions(Version int NOT NULL PRIMARY KEY, AppliedAt datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET());
GO
IF OBJECT_ID(N'dbo.Categories',N'U') IS NULL
CREATE TABLE dbo.Categories(
  [Id] int NOT NULL,
  [Name] nvarchar(200) NOT NULL,
  [Description] nvarchar(max) NOT NULL,
  CONSTRAINT PK_Categories PRIMARY KEY(Id)
);
GO
IF OBJECT_ID(N'dbo.Users',N'U') IS NULL
CREATE TABLE dbo.Users(
  [Id] uniqueidentifier NOT NULL,
  [FullName] nvarchar(200) NOT NULL,
  [Email] nvarchar(254) NOT NULL,
  [PasswordHash] nvarchar(max) NOT NULL,
  [Phone] nvarchar(64) NOT NULL,
  [Address] nvarchar(2000) NOT NULL,
  [Role] nvarchar(10) NOT NULL,
  [IsLocked] bit NOT NULL,
  [EmailVerifiedAt] datetimeoffset NULL,
  CONSTRAINT PK_Users PRIMARY KEY(Id), CONSTRAINT UQ_Users_Email UNIQUE(Email), CONSTRAINT CK_Users_Role CHECK(Role IN (N'User',N'Admin'))
);
GO
IF OBJECT_ID(N'dbo.Products',N'U') IS NULL
CREATE TABLE dbo.Products(
  [Id] int NOT NULL,
  [Name] nvarchar(200) NOT NULL,
  [Description] nvarchar(max) NOT NULL,
  [Price] decimal(18,2) NOT NULL,
  [CategoryId] int NOT NULL,
  [Image] nvarchar(1000) NOT NULL,
  [Color] nvarchar(100) NOT NULL,
  [Active] bit NOT NULL,
  [Audience] nvarchar(20) NOT NULL,
  [LowStockThreshold] int NULL,
  CONSTRAINT PK_Products PRIMARY KEY(Id), CONSTRAINT FK_Products_Categories FOREIGN KEY(CategoryId) REFERENCES dbo.Categories(Id), CONSTRAINT CK_Products_Price CHECK(Price>=0), CONSTRAINT CK_Products_Threshold CHECK(LowStockThreshold IS NULL OR LowStockThreshold BETWEEN 0 AND 1000)
);
GO
IF OBJECT_ID(N'dbo.Orders',N'U') IS NULL
CREATE TABLE dbo.Orders(
  [Id] int NOT NULL,
  [UserEmail] nvarchar(254) NOT NULL,
  [FullName] nvarchar(200) NOT NULL,
  [Phone] nvarchar(64) NOT NULL,
  [Address] nvarchar(2000) NOT NULL,
  [PaymentMethod] nvarchar(40) NOT NULL,
  [Subtotal] decimal(18,2) NOT NULL,
  [ShippingFee] decimal(18,2) NOT NULL,
  [Total] decimal(18,2) NOT NULL,
  [StockDeducted] bit NOT NULL,
  [Status] nvarchar(50) NOT NULL,
  [CreatedAt] datetime2 NOT NULL,
  CONSTRAINT PK_Orders PRIMARY KEY(Id), CONSTRAINT CK_Orders_Money CHECK(Subtotal>=0 AND ShippingFee>=0 AND Total=Subtotal+ShippingFee)
);
GO
IF OBJECT_ID(N'dbo.Reviews',N'U') IS NULL
CREATE TABLE dbo.Reviews(
  [Id] int NOT NULL,
  [ProductId] int NOT NULL,
  [UserKey] uniqueidentifier NOT NULL,
  [UserId] int NOT NULL,
  [UserName] nvarchar(200) NOT NULL,
  [Rating] int NOT NULL,
  [Content] nvarchar(max) NOT NULL,
  [CreatedAt] datetime2 NOT NULL,
  CONSTRAINT PK_Reviews PRIMARY KEY(Id), CONSTRAINT CK_Reviews_Rating CHECK(Rating BETWEEN 1 AND 5)
);
GO
IF OBJECT_ID(N'dbo.StockMovements',N'U') IS NULL
CREATE TABLE dbo.StockMovements(
  [Id] int NOT NULL,
  [ProductId] int NOT NULL,
  [ProductName] nvarchar(200) NOT NULL,
  [Size] nvarchar(50) NOT NULL,
  [Type] nvarchar(40) NOT NULL,
  [Change] int NOT NULL,
  [StockAfter] int NOT NULL,
  [Note] nvarchar(200) NOT NULL,
  [OrderId] int NULL,
  [By] nvarchar(200) NOT NULL,
  [CreatedAt] datetime2 NOT NULL,
  CONSTRAINT PK_StockMovements PRIMARY KEY(Id)
);
GO
IF OBJECT_ID(N'dbo.OutfitProducts',N'U') IS NULL
CREATE TABLE dbo.OutfitProducts(
  [Id] nvarchar(80) NOT NULL,
  [StoreProductId] int NOT NULL,
  [Category] nvarchar(20) NOT NULL,
  [Kind] nvarchar(100) NOT NULL,
  [Name] nvarchar(200) NOT NULL,
  [Color] nvarchar(100) NOT NULL,
  [ColorName] nvarchar(100) NOT NULL,
  [Hex] nvarchar(12) NOT NULL,
  CONSTRAINT PK_OutfitProducts PRIMARY KEY(Id), CONSTRAINT FK_OutfitProducts_Products FOREIGN KEY(StoreProductId) REFERENCES dbo.Products(Id)
);
GO
IF OBJECT_ID(N'dbo.QrPaymentSettings',N'U') IS NULL
CREATE TABLE dbo.QrPaymentSettings(
  Id int NOT NULL,
  [Enabled] bit NOT NULL,
  [BankBin] nvarchar(6) NOT NULL,
  [BankName] nvarchar(100) NOT NULL,
  [AccountNumber] nvarchar(19) NOT NULL,
  [AccountName] nvarchar(100) NOT NULL,
  CONSTRAINT PK_QrPaymentSettings PRIMARY KEY (Id), CONSTRAINT CK_QrSettings_Id CHECK(Id=1)
);
GO
IF OBJECT_ID(N'dbo.ProductSizes',N'U') IS NULL
CREATE TABLE dbo.ProductSizes(ProductId int NOT NULL, Size nvarchar(50) NOT NULL, Stock int NOT NULL, SortOrder int NOT NULL,
 CONSTRAINT PK_ProductSizes PRIMARY KEY(ProductId,Size), CONSTRAINT FK_ProductSizes_Products FOREIGN KEY(ProductId) REFERENCES dbo.Products(Id) ON DELETE CASCADE,
 CONSTRAINT CK_ProductSizes_Stock CHECK(Stock>=0));
GO
IF OBJECT_ID(N'dbo.OrderItems',N'U') IS NULL
CREATE TABLE dbo.OrderItems(OrderId int NOT NULL, LineNumber int NOT NULL, ProductId int NOT NULL, ProductName nvarchar(200) NOT NULL,
 Size nvarchar(50) NOT NULL, Quantity int NOT NULL, UnitPrice decimal(18,2) NOT NULL,
 CONSTRAINT PK_OrderItems PRIMARY KEY(OrderId,LineNumber), CONSTRAINT FK_OrderItems_Orders FOREIGN KEY(OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
 CONSTRAINT CK_OrderItems_Quantity CHECK(Quantity>0), CONSTRAINT CK_OrderItems_Price CHECK(UnitPrice>=0));
GO
IF OBJECT_ID(N'dbo.OrderQrPayments',N'U') IS NULL
CREATE TABLE dbo.OrderQrPayments(OrderId int NOT NULL PRIMARY KEY, BankBin nvarchar(6) NOT NULL, BankName nvarchar(100) NOT NULL,
 AccountNumber nvarchar(19) NOT NULL, AccountName nvarchar(100) NOT NULL, Reference nvarchar(50) NOT NULL UNIQUE, Status int NOT NULL,
 ReportedAt datetimeoffset NULL, PaidAt datetimeoffset NULL, ConfirmedBy nvarchar(200) NOT NULL, BankTransactionReference nvarchar(100) NOT NULL,
 CONSTRAINT FK_OrderQrPayments_Orders FOREIGN KEY(OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
 CONSTRAINT CK_OrderQrPayments_Status CHECK(Status IN (0,1,2)));
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'UX_Qr_BankTransaction' AND object_id=OBJECT_ID(N'dbo.OrderQrPayments'))
CREATE UNIQUE INDEX UX_Qr_BankTransaction ON dbo.OrderQrPayments(BankTransactionReference) WHERE Status=2 AND BankTransactionReference<>N'';
GO
IF OBJECT_ID(N'dbo.LegacySnapshots',N'U') IS NULL
CREATE TABLE dbo.LegacySnapshots(Name nvarchar(100) NOT NULL PRIMARY KEY, Content nvarchar(max) NOT NULL CHECK(ISJSON(Content)=1));
GO
IF OBJECT_ID(N'dbo.ImportHistory',N'U') IS NULL
CREATE TABLE dbo.ImportHistory(Id int NOT NULL PRIMARY KEY CHECK(Id=1), ImportedAt datetimeoffset NOT NULL, SourceDescription nvarchar(200) NOT NULL);
GO
CREATE OR ALTER VIEW dbo.InventorySummary AS
SELECT p.Id AS ProductId,p.Name,p.CategoryId,p.Active,COALESCE(SUM(s.Stock),0) AS TotalStock,COALESCE(p.LowStockThreshold,2) AS LowStockThreshold
FROM dbo.Products p LEFT JOIN dbo.ProductSizes s ON s.ProductId=p.Id GROUP BY p.Id,p.Name,p.CategoryId,p.Active,p.LowStockThreshold;
GO
CREATE OR ALTER PROCEDURE dbo.StoreRead @Collection nvarchar(100) AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @Result nvarchar(max);
 IF @Collection=N'categories' SET @Result=(SELECT t.[Id],t.[Name],t.[Description] FROM dbo.Categories t ORDER BY t.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
 ELSE IF @Collection=N'users' SET @Result=(SELECT t.[Id],t.[FullName],t.[Email],t.[PasswordHash],t.[Phone],t.[Address],t.[Role],t.[IsLocked],t.[EmailVerifiedAt] FROM dbo.Users t ORDER BY t.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
 ELSE IF @Collection=N'products' SET @Result=(SELECT t.[Id],t.[Name],t.[Description],t.[Price],t.[CategoryId],t.[Image],t.[Color],t.[Active],t.[Audience],t.[LowStockThreshold],COALESCE((SELECT SUM(Stock) FROM dbo.ProductSizes WHERE ProductId=t.Id),0) AS Stock,
 COALESCE((SELECT STRING_AGG(CAST(Size AS nvarchar(max)),N',') WITHIN GROUP(ORDER BY SortOrder) FROM dbo.ProductSizes WHERE ProductId=t.Id),N'') AS Sizes,
 JSON_QUERY(N'{'+COALESCE((SELECT STRING_AGG(CAST(N'"'+STRING_ESCAPE(Size,'json')+N'":'+CONVERT(nvarchar(12),Stock) AS nvarchar(max)),N',') FROM dbo.ProductSizes WHERE ProductId=t.Id),N'')+N'}') AS SizeStock FROM dbo.Products t ORDER BY t.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
 ELSE IF @Collection=N'orders' SET @Result=(SELECT t.[Id],t.[UserEmail],t.[FullName],t.[Phone],t.[Address],t.[PaymentMethod],t.[Subtotal],t.[ShippingFee],t.[Total],t.[StockDeducted],t.[Status],t.[CreatedAt],JSON_QUERY((SELECT ProductId,ProductName,Size,Quantity,UnitPrice FROM dbo.OrderItems i WHERE i.OrderId=t.Id ORDER BY LineNumber FOR JSON PATH)) AS Items,
 JSON_QUERY((SELECT BankBin,BankName,AccountNumber,AccountName,Reference,Status,ReportedAt,PaidAt,ConfirmedBy,BankTransactionReference FROM dbo.OrderQrPayments p WHERE p.OrderId=t.Id FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)) AS QrPayment FROM dbo.Orders t ORDER BY t.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
 ELSE IF @Collection=N'reviews' SET @Result=(SELECT t.[Id],t.[ProductId],t.[UserKey],t.[UserId],t.[UserName],t.[Rating],t.[Content],t.[CreatedAt] FROM dbo.Reviews t ORDER BY t.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
 ELSE IF @Collection=N'stock-movements' SET @Result=(SELECT t.[Id],t.[ProductId],t.[ProductName],t.[Size],t.[Type],t.[Change],t.[StockAfter],t.[Note],t.[OrderId],t.[By],t.[CreatedAt] FROM dbo.StockMovements t ORDER BY t.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
 ELSE IF @Collection=N'outfit-products' SET @Result=(SELECT t.[Id],t.[StoreProductId],t.[Category],t.[Kind],t.[Name],t.[Color],t.[ColorName],t.[Hex] FROM dbo.OutfitProducts t ORDER BY t.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
 ELSE IF @Collection=N'qr-payment-settings' SET @Result=(SELECT t.[Enabled],t.[BankBin],t.[BankName],t.[AccountNumber],t.[AccountName] FROM dbo.QrPaymentSettings t  FOR JSON PATH,INCLUDE_NULL_VALUES);
 ELSE IF @Collection=N'inventory-before-sizes' SET @Result=(SELECT Content FROM dbo.LegacySnapshots WHERE Name=@Collection);
 ELSE THROW 51000,N'Unknown store collection.',1;
 SELECT COALESCE(@Result,N'[]');
END;
GO
CREATE OR ALTER PROCEDURE dbo.StoreWrite @Collection nvarchar(100), @Data nvarchar(max) AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_WARNINGS ON;
 IF ISJSON(@Data)<>1 OR LEFT(LTRIM(@Data),1)<>N'[' THROW 51001,N'Store data must be a JSON array.',1;
 IF @Collection=N'categories'
 BEGIN
  DECLARE @Categories TABLE([Id] int,[Name] nvarchar(max),[Description] nvarchar(max));
  INSERT @Categories SELECT [Id],[Name],[Description] FROM OPENJSON(@Data) WITH([Id] int,[Name] nvarchar(max),[Description] nvarchar(max));
  UPDATE t SET t.[Name]=s.[Name],t.[Description]=s.[Description] FROM dbo.Categories t JOIN @Categories s ON s.Id=t.Id;
  INSERT dbo.Categories([Id],[Name],[Description]) SELECT s.[Id],s.[Name],s.[Description] FROM @Categories s WHERE NOT EXISTS(SELECT 1 FROM dbo.Categories t WHERE t.Id=s.Id);
  DELETE t FROM dbo.Categories t WHERE NOT EXISTS(SELECT 1 FROM @Categories s WHERE s.Id=t.Id);
 END
 ELSE IF @Collection=N'users'
 BEGIN
  DECLARE @Users TABLE([Id] uniqueidentifier,[FullName] nvarchar(max),[Email] nvarchar(max),[PasswordHash] nvarchar(max),[Phone] nvarchar(max),[Address] nvarchar(max),[Role] nvarchar(max),[IsLocked] bit,[EmailVerifiedAt] datetimeoffset);
  INSERT @Users SELECT [Id],[FullName],[Email],[PasswordHash],[Phone],[Address],[Role],[IsLocked],[EmailVerifiedAt] FROM OPENJSON(@Data) WITH([Id] uniqueidentifier,[FullName] nvarchar(max),[Email] nvarchar(max),[PasswordHash] nvarchar(max),[Phone] nvarchar(max),[Address] nvarchar(max),[Role] nvarchar(max),[IsLocked] bit,[EmailVerifiedAt] datetimeoffset);
  UPDATE t SET t.[FullName]=s.[FullName],t.[Email]=s.[Email],t.[PasswordHash]=s.[PasswordHash],t.[Phone]=s.[Phone],t.[Address]=s.[Address],t.[Role]=s.[Role],t.[IsLocked]=s.[IsLocked],t.[EmailVerifiedAt]=s.[EmailVerifiedAt] FROM dbo.Users t JOIN @Users s ON s.Id=t.Id;
  INSERT dbo.Users([Id],[FullName],[Email],[PasswordHash],[Phone],[Address],[Role],[IsLocked],[EmailVerifiedAt]) SELECT s.[Id],s.[FullName],s.[Email],s.[PasswordHash],s.[Phone],s.[Address],s.[Role],s.[IsLocked],s.[EmailVerifiedAt] FROM @Users s WHERE NOT EXISTS(SELECT 1 FROM dbo.Users t WHERE t.Id=s.Id);
  DELETE t FROM dbo.Users t WHERE NOT EXISTS(SELECT 1 FROM @Users s WHERE s.Id=t.Id);
 END
 ELSE IF @Collection=N'products'
 BEGIN
  DECLARE @Products TABLE([Id] int,[Name] nvarchar(max),[Description] nvarchar(max),[Price] decimal(18,2),[CategoryId] int,[Image] nvarchar(max),[Color] nvarchar(max),[Active] bit,[Audience] nvarchar(max),[LowStockThreshold] int);
  INSERT @Products SELECT [Id],[Name],[Description],[Price],[CategoryId],[Image],[Color],[Active],[Audience],[LowStockThreshold] FROM OPENJSON(@Data) WITH([Id] int,[Name] nvarchar(max),[Description] nvarchar(max),[Price] decimal(18,2),[CategoryId] int,[Image] nvarchar(max),[Color] nvarchar(max),[Active] bit,[Audience] nvarchar(max),[LowStockThreshold] int);
  UPDATE t SET t.[Name]=s.[Name],t.[Description]=s.[Description],t.[Price]=s.[Price],t.[CategoryId]=s.[CategoryId],t.[Image]=s.[Image],t.[Color]=s.[Color],t.[Active]=s.[Active],t.[Audience]=s.[Audience],t.[LowStockThreshold]=s.[LowStockThreshold] FROM dbo.Products t JOIN @Products s ON s.Id=t.Id;
  INSERT dbo.Products([Id],[Name],[Description],[Price],[CategoryId],[Image],[Color],[Active],[Audience],[LowStockThreshold]) SELECT s.[Id],s.[Name],s.[Description],s.[Price],s.[CategoryId],s.[Image],s.[Color],s.[Active],s.[Audience],s.[LowStockThreshold] FROM @Products s WHERE NOT EXISTS(SELECT 1 FROM dbo.Products t WHERE t.Id=s.Id);
  DELETE dbo.ProductSizes;
  INSERT dbo.ProductSizes(ProductId,Size,Stock,SortOrder)
  SELECT p.Id,sz.value,COALESCE(stock.Stock,0),CONVERT(int,sz.[key])
  FROM OPENJSON(@Data) WITH(Id int,AvailableSizes nvarchar(max) AS JSON,SizeStock nvarchar(max) AS JSON) p
  CROSS APPLY OPENJSON(p.AvailableSizes) sz
  OUTER APPLY(SELECT TRY_CONVERT(int,ss.value) AS Stock FROM OPENJSON(p.SizeStock) ss WHERE ss.[key]=sz.value) stock;
  DELETE t FROM dbo.Products t WHERE NOT EXISTS(SELECT 1 FROM @Products s WHERE s.Id=t.Id);
 END
 ELSE IF @Collection=N'orders'
 BEGIN
  DECLARE @Orders TABLE([Id] int,[UserEmail] nvarchar(max),[FullName] nvarchar(max),[Phone] nvarchar(max),[Address] nvarchar(max),[PaymentMethod] nvarchar(max),[Subtotal] decimal(18,2),[ShippingFee] decimal(18,2),[Total] decimal(18,2),[StockDeducted] bit,[Status] nvarchar(max),[CreatedAt] datetime2);
  INSERT @Orders SELECT [Id],[UserEmail],[FullName],[Phone],[Address],[PaymentMethod],[Subtotal],[ShippingFee],[Total],[StockDeducted],[Status],[CreatedAt] FROM OPENJSON(@Data) WITH([Id] int,[UserEmail] nvarchar(max),[FullName] nvarchar(max),[Phone] nvarchar(max),[Address] nvarchar(max),[PaymentMethod] nvarchar(max),[Subtotal] decimal(18,2),[ShippingFee] decimal(18,2),[Total] decimal(18,2),[StockDeducted] bit,[Status] nvarchar(max),[CreatedAt] datetime2);
  DELETE dbo.OrderItems; DELETE dbo.OrderQrPayments;
  UPDATE t SET t.[UserEmail]=s.[UserEmail],t.[FullName]=s.[FullName],t.[Phone]=s.[Phone],t.[Address]=s.[Address],t.[PaymentMethod]=s.[PaymentMethod],t.[Subtotal]=s.[Subtotal],t.[ShippingFee]=s.[ShippingFee],t.[Total]=s.[Total],t.[StockDeducted]=s.[StockDeducted],t.[Status]=s.[Status],t.[CreatedAt]=s.[CreatedAt] FROM dbo.Orders t JOIN @Orders s ON s.Id=t.Id;
  INSERT dbo.Orders([Id],[UserEmail],[FullName],[Phone],[Address],[PaymentMethod],[Subtotal],[ShippingFee],[Total],[StockDeducted],[Status],[CreatedAt]) SELECT s.[Id],s.[UserEmail],s.[FullName],s.[Phone],s.[Address],s.[PaymentMethod],s.[Subtotal],s.[ShippingFee],s.[Total],s.[StockDeducted],s.[Status],s.[CreatedAt] FROM @Orders s WHERE NOT EXISTS(SELECT 1 FROM dbo.Orders t WHERE t.Id=s.Id);
  DELETE t FROM dbo.Orders t WHERE NOT EXISTS(SELECT 1 FROM @Orders s WHERE s.Id=t.Id);
  INSERT dbo.OrderItems(OrderId,LineNumber,ProductId,ProductName,Size,Quantity,UnitPrice)
  SELECT p.Id,CONVERT(int,i.[key]),v.ProductId,v.ProductName,v.Size,v.Quantity,v.UnitPrice
  FROM OPENJSON(@Data) WITH(Id int,Items nvarchar(max) AS JSON) p CROSS APPLY OPENJSON(p.Items) i
  CROSS APPLY OPENJSON(i.value) WITH(ProductId int,ProductName nvarchar(max),Size nvarchar(max),Quantity int,UnitPrice decimal(18,2)) v;
  INSERT dbo.OrderQrPayments(OrderId,BankBin,BankName,AccountNumber,AccountName,Reference,Status,ReportedAt,PaidAt,ConfirmedBy,BankTransactionReference)
  SELECT p.Id,v.BankBin,v.BankName,v.AccountNumber,v.AccountName,v.Reference,v.Status,v.ReportedAt,v.PaidAt,v.ConfirmedBy,v.BankTransactionReference
  FROM OPENJSON(@Data) WITH(Id int,QrPayment nvarchar(max) AS JSON) p
  CROSS APPLY OPENJSON(p.QrPayment) WITH(BankBin nvarchar(max),BankName nvarchar(max),AccountNumber nvarchar(max),AccountName nvarchar(max),Reference nvarchar(max),Status int,ReportedAt datetimeoffset,PaidAt datetimeoffset,ConfirmedBy nvarchar(max),BankTransactionReference nvarchar(max)) v;
 END
 ELSE IF @Collection=N'reviews'
 BEGIN
  DECLARE @Reviews TABLE([Id] int,[ProductId] int,[UserKey] uniqueidentifier,[UserId] int,[UserName] nvarchar(max),[Rating] int,[Content] nvarchar(max),[CreatedAt] datetime2);
  INSERT @Reviews SELECT [Id],[ProductId],[UserKey],[UserId],[UserName],[Rating],[Content],[CreatedAt] FROM OPENJSON(@Data) WITH([Id] int,[ProductId] int,[UserKey] uniqueidentifier,[UserId] int,[UserName] nvarchar(max),[Rating] int,[Content] nvarchar(max),[CreatedAt] datetime2);
  UPDATE t SET t.[ProductId]=s.[ProductId],t.[UserKey]=s.[UserKey],t.[UserId]=s.[UserId],t.[UserName]=s.[UserName],t.[Rating]=s.[Rating],t.[Content]=s.[Content],t.[CreatedAt]=s.[CreatedAt] FROM dbo.Reviews t JOIN @Reviews s ON s.Id=t.Id;
  INSERT dbo.Reviews([Id],[ProductId],[UserKey],[UserId],[UserName],[Rating],[Content],[CreatedAt]) SELECT s.[Id],s.[ProductId],s.[UserKey],s.[UserId],s.[UserName],s.[Rating],s.[Content],s.[CreatedAt] FROM @Reviews s WHERE NOT EXISTS(SELECT 1 FROM dbo.Reviews t WHERE t.Id=s.Id);
  DELETE t FROM dbo.Reviews t WHERE NOT EXISTS(SELECT 1 FROM @Reviews s WHERE s.Id=t.Id);
 END
 ELSE IF @Collection=N'stock-movements'
 BEGIN
  DECLARE @StockMovements TABLE([Id] int,[ProductId] int,[ProductName] nvarchar(max),[Size] nvarchar(max),[Type] nvarchar(max),[Change] int,[StockAfter] int,[Note] nvarchar(max),[OrderId] int,[By] nvarchar(max),[CreatedAt] datetime2);
  INSERT @StockMovements SELECT [Id],[ProductId],[ProductName],[Size],[Type],[Change],[StockAfter],[Note],[OrderId],[By],[CreatedAt] FROM OPENJSON(@Data) WITH([Id] int,[ProductId] int,[ProductName] nvarchar(max),[Size] nvarchar(max),[Type] nvarchar(max),[Change] int,[StockAfter] int,[Note] nvarchar(max),[OrderId] int,[By] nvarchar(max),[CreatedAt] datetime2);
  UPDATE t SET t.[ProductId]=s.[ProductId],t.[ProductName]=s.[ProductName],t.[Size]=s.[Size],t.[Type]=s.[Type],t.[Change]=s.[Change],t.[StockAfter]=s.[StockAfter],t.[Note]=s.[Note],t.[OrderId]=s.[OrderId],t.[By]=s.[By],t.[CreatedAt]=s.[CreatedAt] FROM dbo.StockMovements t JOIN @StockMovements s ON s.Id=t.Id;
  INSERT dbo.StockMovements([Id],[ProductId],[ProductName],[Size],[Type],[Change],[StockAfter],[Note],[OrderId],[By],[CreatedAt]) SELECT s.[Id],s.[ProductId],s.[ProductName],s.[Size],s.[Type],s.[Change],s.[StockAfter],s.[Note],s.[OrderId],s.[By],s.[CreatedAt] FROM @StockMovements s WHERE NOT EXISTS(SELECT 1 FROM dbo.StockMovements t WHERE t.Id=s.Id);
  DELETE t FROM dbo.StockMovements t WHERE NOT EXISTS(SELECT 1 FROM @StockMovements s WHERE s.Id=t.Id);
 END
 ELSE IF @Collection=N'outfit-products'
 BEGIN
  DECLARE @OutfitProducts TABLE([Id] nvarchar(max),[StoreProductId] int,[Category] nvarchar(max),[Kind] nvarchar(max),[Name] nvarchar(max),[Color] nvarchar(max),[ColorName] nvarchar(max),[Hex] nvarchar(max));
  INSERT @OutfitProducts SELECT [Id],[StoreProductId],[Category],[Kind],[Name],[Color],[ColorName],[Hex] FROM OPENJSON(@Data) WITH([Id] nvarchar(max),[StoreProductId] int,[Category] nvarchar(max),[Kind] nvarchar(max),[Name] nvarchar(max),[Color] nvarchar(max),[ColorName] nvarchar(max),[Hex] nvarchar(max));
  UPDATE t SET t.[StoreProductId]=s.[StoreProductId],t.[Category]=s.[Category],t.[Kind]=s.[Kind],t.[Name]=s.[Name],t.[Color]=s.[Color],t.[ColorName]=s.[ColorName],t.[Hex]=s.[Hex] FROM dbo.OutfitProducts t JOIN @OutfitProducts s ON s.Id=t.Id;
  INSERT dbo.OutfitProducts([Id],[StoreProductId],[Category],[Kind],[Name],[Color],[ColorName],[Hex]) SELECT s.[Id],s.[StoreProductId],s.[Category],s.[Kind],s.[Name],s.[Color],s.[ColorName],s.[Hex] FROM @OutfitProducts s WHERE NOT EXISTS(SELECT 1 FROM dbo.OutfitProducts t WHERE t.Id=s.Id);
  DELETE t FROM dbo.OutfitProducts t WHERE NOT EXISTS(SELECT 1 FROM @OutfitProducts s WHERE s.Id=t.Id);
 END
 ELSE IF @Collection=N'qr-payment-settings'
 BEGIN
  DELETE dbo.QrPaymentSettings;
  INSERT dbo.QrPaymentSettings(Id,[Enabled],[BankBin],[BankName],[AccountNumber],[AccountName]) SELECT 1,[Enabled],[BankBin],[BankName],[AccountNumber],[AccountName] FROM OPENJSON(@Data) WITH([Enabled] bit,[BankBin] nvarchar(max),[BankName] nvarchar(max),[AccountNumber] nvarchar(max),[AccountName] nvarchar(max));
 END
 ELSE IF @Collection=N'inventory-before-sizes'
 BEGIN
  UPDATE dbo.LegacySnapshots SET Content=@Data WHERE Name=@Collection;
  IF @@ROWCOUNT=0 INSERT dbo.LegacySnapshots(Name,Content) VALUES(@Collection,@Data);
 END
 ELSE THROW 51000,N'Unknown store collection.',1;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE Version=1) INSERT dbo.SchemaVersions(Version) VALUES(1);
GO
