#!/usr/bin/env bash
# Super Malaab — prepares a fresh Ubuntu 24.04 VPS (Hostinger KVM) for the site and API. Safe to run again.
#   bash <(curl -fsSL https://raw.githubusercontent.com/MBSharaf/super-app/main/tools/vps-setup.sh)
# Installs: security updates, firewall (SSH + web only), fail2ban, nginx + certbot, .NET 10 SDK, Docker and
# SQL Server 2022 Express (in Docker, reachable from this machine only). Generated passwords stay in
# /etc/supermalaab (root only) and are never printed.
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root"; exit 1; }
export DEBIAN_FRONTEND=noninteractive
step() { printf '\n\033[1;32m==> %s\033[0m\n' "$*"; }

step "System updates"
apt-get update -y
apt-get -o Dpkg::Options::=--force-confold upgrade -y
apt-get install -y curl ca-certificates gnupg ufw fail2ban unattended-upgrades nginx certbot python3-certbot-nginx git unzip jq openssl libicu74
timedatectl set-timezone Africa/Cairo || true
dpkg-reconfigure -f noninteractive unattended-upgrades

step "Swap (2 GB, for builds)"
if ! swapon --show | grep -q /swapfile; then
  fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
  grep -q '^/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

step "Firewall: only SSH, HTTP and HTTPS"
ufw allow OpenSSH
ufw allow 'Nginx Full'
ufw --force enable
systemctl enable --now fail2ban

step ".NET 10 SDK"
if ! /usr/share/dotnet/dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
  ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
fi
grep -q DOTNET_CLI_TELEMETRY_OPTOUT /etc/environment || echo 'DOTNET_CLI_TELEMETRY_OPTOUT=1' >> /etc/environment

step "Docker"
command -v docker >/dev/null || curl -fsSL https://get.docker.com | sh
systemctl enable --now docker

step "SQL Server 2022 Express (localhost only)"
install -d -m 700 /etc/supermalaab
if [ ! -f /etc/supermalaab/sql.env ]; then
  echo "MSSQL_SA_PASSWORD=Sa$(openssl rand -hex 16)!" > /etc/supermalaab/sql.env
  chmod 600 /etc/supermalaab/sql.env
fi
if ! docker ps -a --format '{{.Names}}' | grep -qx mssql; then
  docker run -d --name mssql --restart unless-stopped \
    -e ACCEPT_EULA=Y -e MSSQL_PID=Express --env-file /etc/supermalaab/sql.env \
    -p 127.0.0.1:1433:1433 -v mssql-data:/var/opt/mssql --memory 3g \
    mcr.microsoft.com/mssql/server:2022-latest
fi

step "App user and folders"
id supermalaab >/dev/null 2>&1 || useradd --system --home /opt/supermalaab --shell /usr/sbin/nologin supermalaab
install -d -o supermalaab -g supermalaab -m 750 /opt/supermalaab /opt/supermalaab/app /opt/supermalaab/keys /opt/supermalaab/storage /opt/supermalaab/logs /opt/supermalaab/emails

step "Waiting for SQL Server"
for _ in $(seq 1 30); do
  docker exec mssql bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "SELECT 1" -b' >/dev/null 2>&1 && break
  sleep 3
done

step "Summary"
echo "Ubuntu:     $(lsb_release -ds)"
echo ".NET SDK:   $(dotnet --version)"
echo "nginx:      $(nginx -v 2>&1 | cut -d/ -f2)"
echo "Docker:     $(docker --version | cut -d' ' -f3 | tr -d ,)"
echo "SQL Server: $(docker exec mssql bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -h -1 -W -Q "SET NOCOUNT ON; SELECT LEFT(@@VERSION, 45)"' 2>/dev/null | head -1 || echo 'not ready yet')"
echo "Firewall:   $(ufw status | head -1)"
ufw status | sed -n '5,20p'
echo "SUPERMALAAB_SETUP_DONE"
