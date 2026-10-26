# Backups and restore

Scripts: `tools/ops/backup.sh` and `tools/ops/restore.sh` (bash; they run on the Linux VPS and on Windows in Git Bash).
Settings: `tools/ops/backup.env` (copy `backup.env.example`; ignored by git).

## What one backup holds

One encrypted file, `superapp-<date>-<time>.tar.gz.enc`, plus its `.sha256` checksum:

| Part | Why |
|---|---|
| The database (`BACKUP DATABASE … WITH CHECKSUM`, then `RESTORE VERIFYONLY`) | All data |
| `storage/` | Uploaded photos, ID cards, papers, designs, attachments |
| `keys/` | The data-protection keys. **National ID numbers are encrypted with them**: a database restored without its keys cannot read them (and everyone is signed out). |

The file is encrypted with AES-256 (`openssl`, passphrase `SA_BACKUP_PASSPHRASE`) because it holds ID cards and the keys.

**Not in the backup:** `.env` (secrets) and the passphrase itself. Keep both in a password manager, outside the server.
Without the passphrase no backup can be opened.

## Setting it up on the VPS (once)

1. Install the SQL Server tools (`mssql-tools18`, gives `sqlcmd`), plus `openssl` and, for the off-site copy, `rclone`.
2. Make the backup folder; SQL Server's own account must be able to write into it:
   ```bash
   sudo mkdir -p /var/backups/superapp && sudo chown mssql:superapp /var/backups/superapp && sudo chmod 770 /var/backups/superapp
   ```
3. `cp tools/ops/backup.env.example tools/ops/backup.env`, fill it in, `chmod 600 tools/ops/backup.env`.
4. Off-site copy (strongly advised — a backup on the same server dies with the server): `rclone config` once
   (e.g. Backblaze B2 or Google Drive), then set `SA_OFFSITE=remote:folder`.
5. Run it once by hand: `tools/ops/backup.sh`.
6. Every night at 03:30 (cron, as the user that runs the API):
   ```
   30 3 * * * /var/www/superapp/tools/ops/backup.sh >> /var/log/superapp-backup.log 2>&1
   ```

Local backups older than `SA_KEEP_DAYS` (14) are deleted; the off-site copy keeps its own history.

## Restoring

Always try a backup under a **new name** first — it never touches the live database:

```bash
tools/ops/restore.sh /var/backups/superapp/superapp-20261027-033000.tar.gz.enc SuperApp_Check
```

The real restore (after a crash or a bad mistake):

1. Stop the API (`sudo systemctl stop superapp`).
2. `tools/ops/restore.sh <file> SuperApp --replace --files /var/www/superapp`
   — the database is replaced; the current `storage/` and `keys/` are kept as `storage.before-<time>` / `keys.before-<time>`.
3. Start the API and check the site (sign in, open a booking, the admin's ID review).

The script checks the checksum first, refuses a wrong passphrase, and refuses to overwrite an existing database
without `--replace`.

## Monthly restore drill

Once a month restore the latest backup as `SuperApp_Check`, look at a few rows, then drop it. A backup that was
never restored is not a backup.

## Tested (2026-10-27, LocalDB in Git Bash)

Backup → restore into a new name (rows, files and keys back) → refused over an existing database → `--replace` after
the rows were deleted (rows back, old folders kept) → wrong passphrase refused → damaged file caught by the checksum.
