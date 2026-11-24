#!/usr/bin/env bash
# Super Malaab — daily backup of the live database and uploaded files on the VPS.
#   supermalaab-backup             make a backup now
#   supermalaab-backup --install   install this command + a daily timer (03:30 Cairo)
#   supermalaab-backup --list      show the backups on this server
#   supermalaab-backup --offsite-setup   connect Google Drive once (owner pastes one address; nothing secret is shown)
#   supermalaab-backup --show-key  print the off-site encryption key (owner keeps it outside the server)
#   supermalaab-backup --offsite-list   list the backups on Google Drive
#   supermalaab-backup --fetch <name>   copy one Drive backup back here (then supermalaab-restore <name>)
# Each backup is one folder in /var/backups/supermalaab/<date-time>/ (root only):
#   SuperMalaab.bak.gz  full SQL Server backup (checked with RESTORE VERIFYONLY)
#   files.tar.gz        uploaded files (storage) and data-protection keys (logins survive a restore)
#   <volume>.tar.gz     n8n data + its encryption key (each n8n volume found); docker-compose.tar.gz: /docker
# Backups older than KEEP_DAYS (default 14) are deleted. Restoring is done by the owner with supermalaab-restore.
# Off-site copy: when Google Drive is connected (--offsite-setup), each backup is also copied, encrypted with
# rclone crypt, to the Drive folder SuperMalaab-backups/<date-time>; copies older than OFFSITE_DAYS (30) are deleted.
# Without the key in /etc/supermalaab/backup-offsite.key the Drive copies cannot be read: keep it somewhere safe.
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root"; exit 1; }
DEST=/var/backups/supermalaab
KEEP_DAYS="${KEEP_DAYS:-14}"
DB=SuperMalaab
OFFSITE_DAYS="${OFFSITE_DAYS:-30}"
KEY=/etc/supermalaab/backup-offsite.key
RC=/root/.config/rclone/rclone.conf
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

offsite_setup() {
  command -v rclone >/dev/null || curl -fsSL https://rclone.org/install.sh | bash >/dev/null
  install -d -m 700 /etc/supermalaab /root/.config/rclone
  if [ ! -s "$KEY" ]; then   # a rebuilt server: the key saved from before is needed to read the old Drive copies
    read -r -s -p "Paste the off-site key you saved before (hidden), or press Enter to make a new one: " oldkey; echo
    if [ -n "$oldkey" ]; then printf '%s' "$oldkey" > "$KEY"; else openssl rand -base64 32 | tr -d '\n' > "$KEY"; fi
    chmod 600 "$KEY"; unset oldkey
  fi
  local log; log="$(mktemp)"
  rclone authorize drive --auth-no-open-browser > "$log" 2>&1 &
  local pid=$!
  for _ in $(seq 1 20); do grep -q 'http://127.0.0.1:53682/auth' "$log" && break; sleep 1; done
  echo
  # rclone's local page only redirects to Google's sign-in page; take that address so it opens on any computer
  local local_url google_url
  local_url="$(grep -o 'http://127.0.0.1:53682/auth[^ ]*' "$log" | head -1)"
  google_url="$(curl -s -o /dev/null -w '%{redirect_url}' "$local_url")"
  echo "1) Open this address in Chrome, choose the Super Malaab Google account and press Allow:"
  echo "   ${google_url:-$local_url}"
  echo "   (after Allow the page shows 'This site can't be reached' — that is expected)"
  echo "2) Copy the whole address from Chrome's address bar (it starts with http://127.0.0.1:53682/?state=)"
  read -r -p "   and paste it here, then press Enter: " back
  curl -s "$back" >/dev/null || true
  wait "$pid" || true
  local token; token="$(sed -n '/Paste the following/,/End paste/p' "$log" | grep -o '{.*}' | head -1)"
  rm -f "$log"
  [ -n "$token" ] || { echo "Google Drive was not connected (no token). Run --offsite-setup again."; exit 1; }
  rclone config create gdrive drive token "$token" --non-interactive >/dev/null
  rclone config create offsite crypt remote gdrive:SuperMalaab-backups password "$(rclone obscure "$(cat "$KEY")")" \
    filename_encryption off directory_name_encryption false --non-interactive >/dev/null
  chmod 600 "$RC"
  rclone mkdir offsite: && echo "Google Drive connected."
  echo "Backups on Google Drive:"; rclone lsf offsite: --dirs-only 2>/dev/null | tr -d / | tail -5
  echo "To bring one back: supermalaab-backup --fetch <name>  then  supermalaab-restore <name>"
  echo; echo "IMPORTANT: run  supermalaab-backup --show-key  and keep the key outside this server (password manager)."
}

offsite_copy() {   # never fails the local backup
  local dir="$1" stamp="$2"
  [ -f "$RC" ] && rclone listremotes 2>/dev/null | grep -qx 'offsite:' || { echo "Off-site: not connected (supermalaab-backup --offsite-setup)"; return 0; }
  if rclone copy "$dir" "offsite:$stamp" --retries 3 2>/tmp/supermalaab-offsite.err; then
    rclone delete offsite: --min-age "${OFFSITE_DAYS}d" 2>/dev/null; rclone rmdirs offsite: --leave-root 2>/dev/null
    echo "Off-site: copied to Google Drive (SuperMalaab-backups/$stamp, encrypted)"
  else
    echo "Off-site: FAILED — $(tail -1 /tmp/supermalaab-offsite.err)"
  fi
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
  # n8n workflows, credentials and its key: ours (n8n_data + n8n.env) or Hostinger's template (<project>_n8n_data,
  # its key lives inside the volume's config file)
  for v in $(docker volume ls -q | grep -E '(^|_)n8n_data$' || true); do
    if [ -f /etc/supermalaab/n8n.env ] && [ "$v" = n8n_data ]; then
      tar -czf "$dir/$v.tar.gz" -C /var/lib/docker/volumes/$v _data -C /etc/supermalaab n8n.env
    else
      tar -czf "$dir/$v.tar.gz" -C /var/lib/docker/volumes/$v _data
    fi
  done
  [ -d /docker ] && tar -czf "$dir/docker-compose.tar.gz" --exclude='*/data' -C / docker 2>/dev/null || true
  if [ -d /etc/supermalaab/android ]; then   # the Android app's signing key (every app version needs the same one)
    tar -czf "$dir/android-key.tar.gz" -C /etc/supermalaab android
  fi
  chmod -R go-rwx "$dir"
  find "$DEST" -mindepth 1 -maxdepth 1 -type d -mtime +"$KEEP_DAYS" -exec rm -rf {} +
  echo "Backup OK: $dir ($(du -sh "$dir" | cut -f1)) — database verified"
  offsite_copy "$dir" "$stamp"
}

case "${1:-}" in
  --install) install_self ;;
  --list) list_backups ;;
  --offsite-setup) offsite_setup ;;
  --show-key) cat "$KEY"; echo ;;
  --offsite-list) rclone lsf offsite: --dirs-only | tr -d / ;;
  --fetch)   # copy one Drive backup back to this server (decrypted), ready for supermalaab-restore
    [ -n "${2:-}" ] || { echo "Usage: supermalaab-backup --fetch <name>   (names: --offsite-list)"; exit 1; }
    install -d -m 700 "$DEST/$2"
    rclone copy "offsite:$2" "$DEST/$2" --retries 3
    gzip -t "$DEST/$2/$DB.bak.gz" && echo "Fetched $2 ($(du -sh "$DEST/$2" | cut -f1)). Now run: supermalaab-restore $2" \
      || echo "The copy can't be read: is the key the one saved before?" ;;
  "") backup ;;
  *) echo "Usage: supermalaab-backup [--install|--list|--offsite-setup|--show-key|--offsite-list|--fetch <name>]"; exit 1 ;;
esac
