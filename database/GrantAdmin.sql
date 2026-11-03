-- Grant Admin to the requested existing, verified, unlocked account.
-- Run against FashionStoreDB in SQL Server Management Studio (SSMS).
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Email nvarchar(254) = N'mongtrucbay02@gmail.com';
DECLARE @Updated int;

UPDATE dbo.Users
SET Role = N'Admin'
WHERE LOWER(Email) = LOWER(@Email)
  AND EmailVerifiedAt IS NOT NULL
  AND IsLocked = 0;

SET @Updated = @@ROWCOUNT;

IF @Updated <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51010, N'Không cấp quyền: tài khoản không tồn tại, email chưa xác minh, hoặc tài khoản đang bị khóa.', 1;
END;

COMMIT TRANSACTION;

SELECT Email, Role, EmailVerifiedAt, IsLocked
FROM dbo.Users
WHERE LOWER(Email) = LOWER(@Email);
