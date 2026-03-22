#!/bin/bash
# Cleanup stale MySQL socket files before starting
rm -f /var/run/mysqld/mysqld.sock /var/run/mysqld/mysqld.sock.lock

# Execute the original MySQL entrypoint with all arguments
exec /usr/local/bin/docker-entrypoint.sh "$@"
