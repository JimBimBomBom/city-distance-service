-- Database initialization script (01-init-db.sql)
-- Creates essential tables and marks migrations as applied

CREATE DATABASE IF NOT EXISTS CityDistanceService;
USE CityDistanceService;

-- Main cities table
CREATE TABLE IF NOT EXISTS cities (
    CityId VARCHAR(20) PRIMARY KEY,
    CityName VARCHAR(255) NOT NULL,
    Latitude DECIMAL(10, 8) NOT NULL,
    Longitude DECIMAL(11, 8) NOT NULL,
    CountryCode VARCHAR(2) NULL,
    Country VARCHAR(100) NULL,
    AdminRegion VARCHAR(100) NULL,
    Population INT NULL,
    INDEX IX_Cities_CityName (CityName),
    INDEX IX_cities_CountryCode (CountryCode)
);