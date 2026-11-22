#!/usr/bin/env bash
# Super Malaab — puts a backup made by supermalaab-backup back on the live server. The owner runs it himself.
#   supermalaab-restore                  list the backups
#   supermalaab-restore 20261123-0330    restore that backup (asks you to type RESTORE first)
# It first takes a fresh backup of the current state, so a restore can itself be undone.
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root"; exit 1; }
DEST=/var/backups/supermalaab
DB=SuperMalaab
sql() { docker exec mssql bash -c '/opt/mssql-tools18/bin/sqlcmd -C -I -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -h -1 -W -Q "$0"' "$1"; }

if [ -z "${1:-}" ]; then echo "Backups:"; ls -1 "$DEST"; echo "Usage: supermalaab-restore <name>"; exit 0; fi
DIR="$DEST/$1"
[ -f "$DIR/$DB.bak.gz" ] || { echo "No backup named $1"; exit 1; }

echo "This replaces the LIVE database and uploaded files with the backup $1."
read -r -p "Type RESTORE to continue: " OK
[ "$OK" = RESTORE ] || { echo "Cancelled. Nothing changed."; exit 1; }

echo "Taking a safety backup of the current state first..."
supermalaab-backup

systemctl stop supermalaab
trap 'systemctl start supermalaab' EXIT
inside="/var/opt/mssql/backup/restore-$1.bak"
gunzip -c "$DIR/$DB.bak.gz" > "/tmp/restore-$1.bak"
docker cp "/tmp/restore-$1.bak" "mssql:$inside" >/dev/null
rm -f "/tmp/restore-$1.bak"
docker exec -u 0 mssql chown mssql "$inside"
sql "ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$DB] FROM DISK='$inside' WITH REPLACE, CHECKSUM; ALTER DATABASE [$DB] SET MULTI_USER;" >/dev/null
docker exec mssql rm -f "$inside"
tar -xzf "$DIR/files.tar.gz" -C /opt/supermalaab
chown -R supermalaab:supermalaab /opt/supermalaab/storage /opt/supermalaab/keys
echo "Restored $1. Starting the site..."
