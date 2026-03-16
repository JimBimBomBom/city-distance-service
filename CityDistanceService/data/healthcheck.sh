#!/bin/bash
# Healthcheck script for MySQL
# Returns success if MySQL is accepting connections (database may still be initializing)
# Only fails if MySQL is completely down

# Check if MySQL process is accepting connections
if mysqladmin -u root -p"$MYSQL_ROOT_PASSWORD" ping --silent > /dev/null 2>&1; then
    # MySQL is running and accepting connections
    # During startup, this is sufficient to not mark container unhealthy
    # The app has its own retry logic for database readiness
    exit 0
fi

# MySQL is not responding at all
exit 1
