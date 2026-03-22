-- City data seed file (02-cities.sql)
-- Inserts sample city data with upsert logic

USE CityDistanceService;

-- Insert sample city data
-- If a city already exists, it will be updated with the new values
INSERT INTO cities (CityId, CityName, Latitude, Longitude, CountryCode, Country, AdminRegion, Population) VALUES
('Q84', 'Barcelona', 41.3851, 2.1734, 'ES', 'Spain', 'Catalonia', 1621000),
('Q90', 'Paris', 48.8566, 2.3522, 'FR', 'France', 'Île-de-France', 2161000),
('Q64', 'Berlin', 52.5200, 13.4050, 'DE', 'Germany', 'Berlin', 3645000),
('Q55', 'Prague', 50.0755, 14.4378, 'CZ', 'Czech Republic', 'Prague', 1336000),
('Q1741', 'Vienna', 48.2082, 16.3738, 'AT', 'Austria', 'Vienna', 1911000),
('Q270', 'Brussels', 50.8503, 4.3517, 'BE', 'Belgium', 'Brussels-Capital', 1209000),
('Q2807', 'Madrid', 40.4168, -3.7038, 'ES', 'Spain', 'Community of Madrid', 3265000),
('Q34370', 'Rome', 41.9028, 12.4964, 'IT', 'Italy', 'Lazio', 2873000),
('Q1492', 'Lisbon', 38.7223, -9.1393, 'PT', 'Portugal', 'Lisbon', 504700),
('Q1754', 'Stockholm', 59.3293, 18.0686, 'SE', 'Sweden', 'Stockholm County', 975900)
ON DUPLICATE KEY UPDATE
    CityName = VALUES(CityName),
    Latitude = VALUES(Latitude),
    Longitude = VALUES(Longitude),
    CountryCode = VALUES(CountryCode),
    Country = VALUES(Country),
    AdminRegion = VALUES(AdminRegion),
    Population = VALUES(Population);

-- Verify data load
SELECT CONCAT('Loaded ', COUNT(*), ' cities') AS status FROM cities;