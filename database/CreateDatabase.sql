-- Run in SSMS or sqlcmd with Windows Authentication. No existing database is deleted.
USE master;
GO
IF DB_ID(N'FashionStoreDB') IS NULL CREATE DATABASE [FashionStoreDB];
GO
USE [FashionStoreDB];
GO
-- Then run Schema.sql in this database, or run the application's --init-sql command.
