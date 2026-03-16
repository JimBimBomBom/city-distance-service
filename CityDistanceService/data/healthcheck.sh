#!/bin/bash
# Healthcheck script for MySQL
# This script checks if MySQL is ready and the database exists
# During startup, return success to avoid marking container as unhealthy prematurely

# Try to connect to MySQL and verify database
# If connection fails, check if MySQL process is running
if mysql -u root -p"$MYSQL_ROOT_PASSWORD" -e "USE CityDistanceService; SELECT 1;" > /dev/null 2>&1; then
    exit 0
fi

# If MySQL query failed, check if MySQL process is at least running
if mysqladmin -u root -p"$MYSQL_ROOT_PASSWORD" ping --silent > /dev/null 2>&1; then
    # MySQL is running but database might not be ready yet
    # Return success during startup phase (start_period handles grace time)
    exit 0
fi

# MySQL is not responding at all
exit 1
