#!/usr/bin/env bash
# Super App backup: the database, uploaded files and the data-protection keys, in ONE encrypted file.
#
# The keys matter as much as the database: national ID numbers are encrypted with them, so a database restored
# without its keys cannot read them. That is also why the file is encrypted (AES-256, passphrase from the settings).
#
#   tools/ops/backup.sh                 # settings from the environment, or from tools/ops/backup.env
#
# Settings (environment variables; see tools/ops/backup.env.example):
#   SA_DB_SERVER        SQL Server address, e.g. localhost  or  (localdb)\MSSQLLocalDB
#   SA_DB_NAME          database name (SuperApp)
#   SA_DB_USER          SQL login (empty = Windows / trusted sign-in); the password goes in SQLCMDPASSWORD
#   SA_APP_DIR          the API folder holding storage/ and keys/ (e.g. /var/www/superapp)
#   SA_STORAGE_DIR      uploaded files (default $SA_APP_DIR/storage)
#   SA_KEYS_DIR         data-protection keys (default $SA_APP_DIR/keys)
#   SA_BACKUP_DIR       where backups are kept (e.g. /var/backups/superapp)
#   SA_SQL_BACKUP_DIR   the same folder as SQL Server sees it (default: SA_BACKUP_DIR; on Linux the mssql user must be able to write it)
#   SA_BACKUP_PASSPHRASE  passphrase for the encryption (keep a copy outside the server!)
#   SA_KEEP_DAYS        days to keep local backups (default 14)
#   SA_OFFSITE          optional rclone target for an off-site copy, e.g. b2:superapp-backups
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
[ -f "$here/backup.env" ] && set -a && . "$here/backup.env" && set +a

: "${SA_DB_SERVER:?Set SA_DB_SERVER}" "${SA_DB_NAME:=SuperApp}" "${SA_APP_DIR:?Set SA_APP_DIR}" "${SA_BACKUP_DIR:?Set SA_BACKUP_DIR}"
: "${SA_BACKUP_PASSPHRASE:?Set SA_BACKUP_PASSPHRASE (and keep a copy somewhere safe)}"
SA_STORAGE_DIR="${SA_STORAGE_DIR:-$SA_APP_DIR/storage}"
SA_KEYS_DIR="${SA_KEYS_DIR:-$SA_APP_DIR/keys}"
SA_KEEP_DAYS="${SA_KEEP_DAYS:-14}"
[[ "$SA_DB_NAME" =~ ^[A-Za-z0-9_]+$ ]] || { echo "SA_DB_NAME may only hold letters, digits and _" >&2; exit 2; }

log() { echo "[$(date '+%F %T')] $*"; }
sql() { if [ -n "${SA_DB_USER:-}" ]; then sqlcmd -S "$SA_DB_SERVER" -U "$SA_DB_USER" -C -b -h -1 -Q "$1"; else sqlcmd -S "$SA_DB_SERVER" -E -b -h -1 -Q "$1"; fi; }
# SQL Server on Windows needs a Windows path; under Git Bash convert ours
winpath() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else echo "$1"; fi; }

stamp="$(date '+%Y%m%d-%H%M%S')"
name="superapp-$stamp"
mkdir -p "$SA_BACKUP_DIR"
work="$(mktemp -d "$SA_BACKUP_DIR/.work-XXXXXX")"
trap 'rm -rf "$work"' EXIT
chmod 777 "$work"   # SQL Server's own account writes the .bak here (Linux: mssql)

sql_dir="${SA_SQL_BACKUP_DIR:+$SA_SQL_BACKUP_DIR/$(basename "$work")}"
sql_dir="${sql_dir:-$(winpath "$work")}"
bak="$sql_dir/$SA_DB_NAME.bak"
[ "${sql_dir#*\\}" != "$sql_dir" ] && bak="$sql_dir\\$SA_DB_NAME.bak"

log "Backing up database $SA_DB_NAME"
sql "BACKUP DATABASE [$SA_DB_NAME] TO DISK = N'$bak' WITH CHECKSUM, INIT, NAME = N'$name'" >/dev/null
sql "RESTORE VERIFYONLY FROM DISK = N'$bak' WITH CHECKSUM" >/dev/null
log "Database backup verified"

mkdir -p "$work/files"
[ -d "$SA_STORAGE_DIR" ] && cp -a "$SA_STORAGE_DIR" "$work/files/storage" || log "No uploaded files folder at $SA_STORAGE_DIR (skipped)"
[ -d "$SA_KEYS_DIR" ] && cp -a "$SA_KEYS_DIR" "$work/files/keys" || { echo "No keys folder at $SA_KEYS_DIR — without it encrypted ID numbers cannot be read after a restore" >&2; exit 3; }
printf 'database=%s\ncreated=%s\nhost=%s\n' "$SA_DB_NAME" "$stamp" "$(hostname)" > "$work/files/MANIFEST"
# The database's logical file names (data / log), so restore.sh can place the files without guessing
sql "SET NOCOUNT ON; SELECT CONCAT(name, '|', CASE type WHEN 1 THEN 'ldf' ELSE 'mdf' END) FROM [$SA_DB_NAME].sys.database_files" \
  | tr -d '\r' | sed -e 's/[[:space:]]*$//' -e '/^$/d' > "$work/files/FILES"
mv "$work/$SA_DB_NAME.bak" "$work/files/database.bak"

out="$SA_BACKUP_DIR/$name.tar.gz.enc"
tar -C "$work/files" -czf - . | openssl enc -aes-256-cbc -pbkdf2 -iter 200000 -salt -pass env:SA_BACKUP_PASSPHRASE -out "$out"
(cd "$SA_BACKUP_DIR" && sha256sum "$(basename "$out")" > "$(basename "$out").sha256")
log "Saved $out ($(du -h "$out" | cut -f1))"

find "$SA_BACKUP_DIR" -maxdepth 1 -name 'superapp-*.tar.gz.enc*' -mtime +"$SA_KEEP_DAYS" -print -delete | sed 's/^/Removed old backup /'

if [ -n "${SA_OFFSITE:-}" ]; then
  rclone copy "$out" "$SA_OFFSITE" && rclone copy "$out.sha256" "$SA_OFFSITE"
  log "Copied off-site to $SA_OFFSITE"
fi
log "Done"
