#!/usr/bin/env bash
# Super Malaab — connects the live server to an SMTP sender (Brevo by default) so confirmation and reset emails go out.
# The owner runs it and types the SMTP login and key himself; the key is never shown on screen or stored anywhere else.
#   supermalaab-set-smtp
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root"; exit 1; }
ENV=/etc/supermalaab/app.env
HOST="${SMTP_HOST:-smtp-relay.brevo.com}"
PORT="${SMTP_PORT:-587}"

read -r -p "SMTP login (Brevo: SMTP & API page, looks like xxxx@smtp-brevo.com): " LOGIN
read -r -s -p "SMTP key (hidden while you type/paste): " KEY; echo
[ -n "$LOGIN" ] && [ -n "$KEY" ] || { echo "Login and key are both needed."; exit 1; }

set_value() {   # replaces KEY=... in app.env, or adds it
  local name="$1" value="$2" tmp
  tmp="$(mktemp)"
  grep -v "^${name}=" "$ENV" > "$tmp" || true
  printf '%s=%s\n' "$name" "$value" >> "$tmp"
  cat "$tmp" > "$ENV"; rm -f "$tmp"
}
set_value EMAIL__MODE Smtp
set_value EMAIL__SMTP_HOST "$HOST"
set_value EMAIL__SMTP_PORT "$PORT"
set_value EMAIL__SMTP_USER "$LOGIN"
set_value EMAIL__SMTP_PASSWORD "$KEY"
set_value EMAIL__FROM no-reply@supermalaab.com
chmod 600 "$ENV"
unset KEY

systemctl restart supermalaab
echo "Saved. The site now sends real email through $HOST."
