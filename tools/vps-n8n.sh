#!/usr/bin/env bash
# Super Malaab — runs n8n (automation) on the VPS at https://n8n.supermalaab.com. Safe to run again (updates n8n).
#   bash <(curl -fsSL https://raw.githubusercontent.com/MBSharaf/super-app/main/tools/vps-n8n.sh)
# - n8n runs in Docker (container n8n, data in volume n8n_data), reachable only through nginx + SSL.
# - Its encryption key is made once in /etc/supermalaab/n8n.env (root only, never printed). Lose it = saved
#   credentials in n8n can't be read, so it is also kept in the daily backup.
# - The owner creates the n8n owner account himself on first visit.
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root"; exit 1; }
DOMAIN=n8n.supermalaab.com
ENVF=/etc/supermalaab/n8n.env
step() { printf '\n\033[1;32m==> %s\033[0m\n' "$*"; }

step "Settings (made once)"
if [ ! -f "$ENVF" ]; then
  umask 077
  cat > "$ENVF" <<EOT
N8N_ENCRYPTION_KEY=$(openssl rand -hex 32)
N8N_HOST=$DOMAIN
N8N_PROTOCOL=https
N8N_PORT=5678
WEBHOOK_URL=https://$DOMAIN/
N8N_EDITOR_BASE_URL=https://$DOMAIN/
N8N_PROXY_HOPS=1
N8N_SECURE_COOKIE=true
GENERIC_TIMEZONE=Africa/Cairo
TZ=Africa/Cairo
N8N_DIAGNOSTICS_ENABLED=false
N8N_PERSONALIZATION_ENABLED=false
N8N_RUNNERS_ENABLED=true
N8N_ENFORCE_SETTINGS_FILE_PERMISSIONS=true
EXECUTIONS_DATA_PRUNE=true
EXECUTIONS_DATA_MAX_AGE=336
EOT
  chmod 600 "$ENVF"
fi

step "n8n container"
docker pull -q docker.n8n.io/n8nio/n8n:latest >/dev/null
docker rm -f n8n >/dev/null 2>&1 || true
docker run -d --name n8n --restart unless-stopped \
  --env-file "$ENVF" -p 127.0.0.1:5678:5678 -v n8n_data:/home/node/.n8n --memory 1500m \
  docker.n8n.io/n8nio/n8n:latest >/dev/null

step "nginx site"
if [ ! -f /etc/nginx/sites-available/n8n ]; then
  cat > /etc/nginx/sites-available/n8n <<'EOT'
server {
    listen 80;
    listen [::]:80;
    server_name n8n.supermalaab.com;
    server_tokens off;
    client_max_body_size 16m;
    location / {
        proxy_pass http://127.0.0.1:5678;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_buffering off;
        proxy_read_timeout 300s;
    }
}
EOT
  ln -sf /etc/nginx/sites-available/n8n /etc/nginx/sites-enabled/n8n
fi
nginx -t -q && systemctl reload nginx

step "SSL"
if [ ! -d /etc/letsencrypt/live/$DOMAIN ]; then
  certbot --nginx -d $DOMAIN --non-interactive --agree-tos --register-unsafely-without-email --redirect
fi

step "Check"
for _ in $(seq 1 40); do curl -fs http://127.0.0.1:5678/healthz >/dev/null 2>&1 && break; sleep 3; done
echo "n8n:   $(docker inspect -f '{{.State.Status}}' n8n) $(docker exec n8n n8n --version 2>/dev/null)"
echo "https: $(curl -s -o /dev/null -w '%{http_code}' https://$DOMAIN/healthz)"
echo "SUPERMALAAB_N8N_DONE"
