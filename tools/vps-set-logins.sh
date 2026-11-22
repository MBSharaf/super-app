#!/usr/bin/env bash
# Super Malaab — turns on "Sign in with Google / Facebook" on the live server.
# The owner runs it and types the ids himself; the Facebook App Secret is hidden while typed and never printed.
# Leave a question empty (just press Enter) to keep the value that is already saved.
#   supermalaab-set-logins
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root"; exit 1; }
ENV=/etc/supermalaab/app.env

read -r -p "Google Client ID (ends with .apps.googleusercontent.com): " GOOGLE_ID
read -r -p "Facebook App ID (numbers only): " FB_ID
read -r -s -p "Facebook App Secret (hidden while you type/paste): " FB_SECRET; echo

if [ -n "$GOOGLE_ID" ] && [[ "$GOOGLE_ID" != *.apps.googleusercontent.com ]]; then
  echo "That does not look like a Google Client ID (it must end with .apps.googleusercontent.com). Nothing saved."; exit 1
fi
if [ -n "$FB_ID" ] && ! [[ "$FB_ID" =~ ^[0-9]+$ ]]; then
  echo "The Facebook App ID is numbers only. Nothing saved."; exit 1
fi
if [ -n "$FB_ID" ] && [ -z "$FB_SECRET" ] && ! grep -q '^EXTERNALLOGINS__FACEBOOK__APPSECRET=.' "$ENV"; then
  echo "Facebook needs both the App ID and the App Secret. Nothing saved."; exit 1
fi

set_value() {   # replaces NAME=... in app.env, or adds it
  local name="$1" value="$2" tmp
  tmp="$(mktemp)"
  grep -v "^${name}=" "$ENV" > "$tmp" || true
  printf '%s=%s\n' "$name" "$value" >> "$tmp"
  cat "$tmp" > "$ENV"; rm -f "$tmp"
}
if [ -n "$GOOGLE_ID" ]; then set_value EXTERNALLOGINS__GOOGLE__CLIENTID "$GOOGLE_ID"; fi
if [ -n "$FB_ID" ]; then set_value EXTERNALLOGINS__FACEBOOK__APPID "$FB_ID"; fi
if [ -n "$FB_SECRET" ]; then set_value EXTERNALLOGINS__FACEBOOK__APPSECRET "$FB_SECRET"; fi
chmod 600 "$ENV"
unset FB_SECRET

systemctl restart supermalaab
sleep 5
echo "Saved. Sign-in buttons now on: $(curl -s https://supermalaab.com/api/v1/auth/providers | tr -d '\n' | cut -c1-200)"
