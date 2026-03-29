#!/bin/bash
# MySQL Docker Entrypoint Wrapper
# Cleans up stale socket files before starting MySQL to handle container restarts

set -e

# Clean up any stale MySQL socket files and locks
# This fixes the "Another process with pid X is using unix socket file" error
# that occurs when the container is restarted

echo "[Entrypoint-Wrapper] Cleaning up stale MySQL socket files..."

# Remove socket lock file
rm -f /var/run/mysqld/mysqld.sock.lock

# Remove socket file itself
rm -f /var/run/mysqld/mysqld.sock

# Remove X Protocol socket
rm -f /var/run/mysqld/mysqlx.sock

# Remove any PID files that might be stale
rm -f /var/run/mysqld/mysqld.pid
rm -f /var/lib/mysql/*.pid

echo "[Entrypoint-Wrapper] Socket cleanup complete. Starting MySQL..."

# Execute the original MySQL entrypoint with all arguments
exec /usr/local/bin/docker-entrypoint.sh "$@"
