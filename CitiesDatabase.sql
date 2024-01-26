CREATE DATABASE CityDatabase;

USE CityDatabase;

CREATE TABLE Cities (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Latitude DECIMAL(7, 6) NOT NULL,
    Longitude DECIMAL(7, 6) NOT NULL,
    Country VARCHAR(100),
    Population INT,
)