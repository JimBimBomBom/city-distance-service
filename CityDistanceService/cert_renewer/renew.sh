#!/bin/bash
set -e

CERT_PATH="/certs"
DAYS_VALID=90
SLEEP_DAYS=1
NGINX_CONTAINER="nginx_reverse_proxy"

while true; do
  echo "[INFO] Generating new self-signed certificate..."
  openssl req -x509 -nodes -newkey rsa:2048 \
    -keyout "${CERT_PATH}/privkey.pem" \
    -out "${CERT_PATH}/fullchain.pem" \
    -days $DAYS_VALID \
    -subj "/C=US/ST=None/L=None/O=None/CN=$(ip route get 1 | awk '{print $7;exit}')"

  echo "[INFO] Certificate generated."

  # Ask Nginx container to reload
  docker ps --format '{{.Names}}'

  if docker ps --format '{{.Names}}' | grep -q "^${NGINX_CONTAINER}$"; then
    echo "[INFO] Reloading Nginx configuration..."
    docker exec ${NGINX_CONTAINER} nginx -s reload || echo "[WARN] Could not reload nginx."
  else
    echo "[WARN] Nginx container not found. Skipping reload."
  fi

  echo "[INFO] Sleeping for ${SLEEP_DAYS} days before next renewal..."
  sleep ${SLEEP_DAYS}d
done
