#!/bin/bash
# Healthcheck script for MySQL
# Only returns success when database is fully initialized and accessible

# Try to connect to MySQL and verify database exists
if mysql -u root -p"$MYSQL_ROOT_PASSWORD" -e "USE CityDistanceService; SELECT 1;" > /dev/null 2>&1; then
    # Database exists and is accessible
    exit 0
fi

# Database not ready - return failure
# This prevents the app from starting before MySQL is fully initialized
exit 1
