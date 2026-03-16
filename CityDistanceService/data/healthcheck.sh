#!/bin/bash
# Healthcheck that waits for MySQL to be ready AND verifies database exists

# First check if MySQL is accepting connections
if ! mysqladmin -u root -p"$MYSQL_ROOT_PASSWORD" ping --silent 2>/dev/null; then
    echo "MySQL not ready yet" >&2
    exit 1
fi

# Then check if the database exists and is usable
mysql -u root -p"$MYSQL_ROOT_PASSWORD" -e "USE CityDistanceService; SELECT 1;" > /dev/null 2>&1
exit $?
