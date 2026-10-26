#!/usr/bin/env bash
# Restores a backup made by backup.sh: the database, uploaded files and data-protection keys.
#
#   tools/ops/restore.sh <file.tar.gz.enc> <target database> [--files <app dir>] [--replace]
#
#   <target database>  restore under this name. Use a NEW name (e.g. SuperApp_Check) to test a backup safely;
#                      restoring over an existing database needs --replace (stop the API first).
#   --files <dir>      also put storage/ and keys/ back into this API folder (an existing folder is kept as *.before-<time>)
#
# Same settings as backup.sh (tools/ops/backup.env): SA_DB_SERVER, SA_DB_USER + SQLCMDPASSWORD, SA_BACKUP_DIR,
# SA_SQL_BACKUP_DIR, SA_BACKUP_PASSPHRASE.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
[ -f "$here/backup.env" ] && set -a && . "$here/backup.env" && set +a

file="${1:?Usage: restore.sh <file.tar.gz.enc> <target database> [--files <app dir>] [--replace]}"
target="${2:?Give the database name to restore into}"
shift 2
files_to=""; replace=0
while [ $# -gt 0 ]; do
  case "$1" in
    --files) files_to="${2:?--files needs the API folder}"; shift 2 ;;
    --replace) replace=1; shift ;;
    *) echo "Unknown option $1" >&2; exit 2 ;;
  esac
done
: "${SA_DB_SERVER:?Set SA_DB_SERVER}" "${SA_BACKUP_DIR:?Set SA_BACKUP_DIR}" "${SA_BACKUP_PASSPHRASE:?Set SA_BACKUP_PASSPHRASE}"
[[ "$target" =~ ^[A-Za-z0-9_]+$ ]] || { echo "The database name may only hold letters, digits and _" >&2; exit 2; }

log() { echo "[$(date '+%F %T')] $*"; }
sql() { if [ -n "${SA_DB_USER:-}" ]; then sqlcmd -S "$SA_DB_SERVER" -U "$SA_DB_USER" -C -b -h -1 -W -Q "$1"; else sqlcmd -S "$SA_DB_SERVER" -E -b -h -1 -W -Q "$1"; fi; }
winpath() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else echo "$1"; fi; }
join() { case "$1" in *\\*) echo "$1\\$2" ;; *) echo "$1/$2" ;; esac; }

if [ -f "$file.sha256" ]; then
  (cd "$(dirname "$file")" && sha256sum -c "$(basename "$file").sha256" >/dev/null) || { echo "Checksum does not match — the file is damaged" >&2; exit 4; }
  log "Checksum OK"
fi

exists="$(sql "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'$target'" | tr -d '[:space:]')"
if [ "$exists" != "0" ] && [ "$replace" != 1 ]; then
  echo "Database $target already exists. Pick a new name, or add --replace (stop the API first)." >&2; exit 5
fi

mkdir -p "$SA_BACKUP_DIR"
work="$(mktemp -d "$SA_BACKUP_DIR/.restore-XXXXXX")"
trap 'rm -rf "$work"' EXIT
chmod 777 "$work"
openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 -pass env:SA_BACKUP_PASSPHRASE -in "$file" | tar -C "$work" -xzf - \
  || { echo "Could not open the backup — wrong passphrase or damaged file" >&2; exit 4; }
log "Opened backup: $(tr '\n' ' ' < "$work/MANIFEST")"

sql_dir="${SA_SQL_BACKUP_DIR:+$SA_SQL_BACKUP_DIR/$(basename "$work")}"
sql_dir="${sql_dir:-$(winpath "$work")}"
bak="$(join "$sql_dir" database.bak)"
data_dir="$(sql "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400))" | tr -d '\r' | sed 's/[[:space:]]*$//')"
data_dir="${data_dir%[\\/]}"
# Each data / log file goes next to the server's other databases, named after the target (FILES comes from backup.sh)
moves=""
while IFS='|' read -r logical ext; do
  [ -n "$logical" ] || continue
  moves="$moves${moves:+, }MOVE N'$logical' TO N'$(join "$data_dir" "${target}_${logical}.$ext")'"
done < "$work/FILES"

log "Restoring database into $target"
[ "$exists" != "0" ] && sql "ALTER DATABASE [$target] SET SINGLE_USER WITH ROLLBACK IMMEDIATE" >/dev/null
sql "RESTORE DATABASE [$target] FROM DISK = N'$bak' WITH CHECKSUM, $moves$( [ "$replace" = 1 ] && echo ', REPLACE')" >/dev/null
sql "ALTER DATABASE [$target] SET MULTI_USER" >/dev/null
log "Database restored: $(sql "SET NOCOUNT ON; SELECT CONCAT((SELECT COUNT(*) FROM [$target].sys.tables), ' tables')" | tr -d '\r')"

if [ -n "$files_to" ]; then
  mkdir -p "$files_to"
  for part in storage keys; do
    [ -d "$work/$part" ] || continue
    [ -e "$files_to/$part" ] && mv "$files_to/$part" "$files_to/$part.before-$(date '+%Y%m%d-%H%M%S')"
    cp -a "$work/$part" "$files_to/$part"
    log "Put back $part/ into $files_to"
  done
fi
log "Done"
