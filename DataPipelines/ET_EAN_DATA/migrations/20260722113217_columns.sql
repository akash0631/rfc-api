-- V2 Retail Data Lake ÃÂ· Column Migration
-- Table : dbo.ET_EAN_DATA
-- Change: ADD SAD
-- Date  : 2026-07-22
-- Run this on the Azure SQL Server connected via VPN

USE [V2_DataLake];
GO

ALTER TABLE dbo.ET_EAN_DATA ADD SAD NVARCHAR(255) NULL;
GO
