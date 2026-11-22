#!/usr/bin/env bash
# Super Malaab — daily backup of the live database and uploaded files on the VPS.
#   supermalaab-backup             make a backup now
#   supermalaab-backup --install   install this command + a daily timer (03:30 Cairo)
#   supermalaab-backup --list      show the backups on this server
# Each backup is one folder in /var/backups/supermalaab/<date-time>/ (root only):
#   SuperMalaab.bak.gz  full SQL Server backup (checked with RESTORE VERIFYONLY)
#   files.tar.gz        uploaded files (storage) and data-protection keys (logins survive a restore)
#   n8n.tar.gz          n8n data + its encryption key (when n8n is installed)
# Backups older than KEEP_DAYS (default 14) are deleted. Restoring is done by the owner with supermalaab-restore.
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root"; exit 1; }
DEST=/var/backups/supermalaab
KEEP_DAYS="${KEEP_DAYS:-14}"
DB=SuperMalaab
sql() { docker exec mssql bash -c '/opt/mssql-tools18/bin/sqlcmd -C -I -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -h -1 -W -Q "$0"' "$1"; }

install_self() {
  [ "$(readlink -f "$0")" = /usr/local/bin/supermalaab-backup ] || install -m 700 "$0" /usr/local/bin/supermalaab-backup
  cat > /etc/systemd/system/supermalaab-backup.service <<'EOF'
[Unit]
Description=Super Malaab daily backup
After=docker.service
[Service]
Type=oneshot
ExecStart=/usr/local/bin/supermalaab-backup
EOF
  cat > /etc/systemd/system/supermalaab-backup.timer <<'EOF'
[Unit]
Description=Super Malaab daily backup at 03:30
[Timer]
OnCalendar=*-*-* 03:30:00
Persistent=true
RandomizedDelaySec=300
[Install]
WantedBy=timers.target
EOF
  systemctl daemon-reload
  systemctl enable --now supermalaab-backup.timer
  echo "Installed. Next run: $(systemctl show supermalaab-backup.timer -p NextElapseUSecRealtime --value)"
}

list_backups() {
  ls -1 "$DEST" 2>/dev/null | while read -r d; do echo "$d  $(du -sh "$DEST/$d" | cut -f1)"; done
}

backup() {
  local stamp dir inside
  stamp="$(date +%Y%m%d-%H%M)"
  dir="$DEST/$stamp"
  inside="/var/opt/mssql/backup/$DB-$stamp.bak"
  install -d -m 700 "$DEST" "$dir"
  docker exec mssql mkdir -p /var/opt/mssql/backup
  sql "BACKUP DATABASE [$DB] TO DISK='$inside' WITH INIT, CHECKSUM, NAME='$DB $stamp'" >/dev/null
  sql "RESTORE VERIFYONLY FROM DISK='$inside' WITH CHECKSUM" >/dev/null
  docker cp "mssql:$inside" "$dir/$DB.bak" >/dev/null
  docker exec mssql rm -f "$inside"
  gzip -6 "$dir/$DB.bak"
  tar -czf "$dir/files.tar.gz" -C /opt/supermalaab storage keys 2>/dev/null || tar -czf "$dir/files.tar.gz" -C /opt/supermalaab storage
  if docker volume inspect n8n_data >/dev/null 2>&1; then   # n8n workflows, credentials and its key
    tar -czf "$dir/n8n.tar.gz" -C /var/lib/docker/volumes/n8n_data _data -C /etc/supermalaab n8n.env
  fi
  chmod -R go-rwx "$dir"
  find "$DEST" -mindepth 1 -maxdepth 1 -type d -mtime +"$KEEP_DAYS" -exec rm -rf {} +
  echo "Backup OK: $dir ($(du -sh "$dir" | cut -f1)) — database verified"
}

case "${1:-}" in
  --install) install_self ;;
  --list) list_backups ;;
  "") backup ;;
  *) echo "Usage: supermalaab-backup [--install|--list]"; exit 1 ;;
esac
