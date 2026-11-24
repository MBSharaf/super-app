#!/usr/bin/env bash
# Super Malaab — turns on phone push notifications (Firebase) on the live server.
# The owner runs it and pastes the Firebase service-account key himself (the JSON file from
# Firebase → Project settings → Service accounts → Generate new private key). The key is hidden while pasted,
# never printed, and saved readable only by root and the site.
#   supermalaab-set-push            paste the key (then press Enter, then Ctrl+D)
#   supermalaab-set-push --off      turn push off (the key file is deleted)
#   supermalaab-set-push --move     move a key saved by an older version to where the site can read it
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root"; exit 1; }
ENV=/etc/supermalaab/app.env
# Its own folder: /etc/supermalaab is root only (700), so the site could not reach a key kept there
DIR=/etc/supermalaab-push
KEY=$DIR/fcm.json
OLD=/etc/supermalaab/fcm.json
NAME=PUSH__FCM__CREDENTIALSFILE

set_value() {   # replaces NAME=... in app.env, or adds it (empty value = remove)
  local name="$1" value="$2" tmp
  tmp="$(mktemp)"
  grep -v "^${name}=" "$ENV" > "$tmp" || true
  if [ -n "$value" ]; then printf '%s=%s\n' "$name" "$value" >> "$tmp"; fi
  cat "$tmp" > "$ENV"; rm -f "$tmp"
  chmod 600 "$ENV"
}

if [ "${1:-}" = "--off" ]; then
  set_value "$NAME" ""
  rm -f "$KEY" "$OLD"
  systemctl restart supermalaab
  echo "Push notifications are off."; exit 0
fi

if [ "${1:-}" = "--move" ]; then   # an older server kept the key where the site can't read it
  [ -f "$OLD" ] || { echo "No key at $OLD."; exit 1; }
  install -d -m 750 -o root -g supermalaab "$DIR"
  install -m 640 -o root -g supermalaab "$OLD" "$KEY" && rm -f "$OLD"
  set_value "$NAME" "$KEY"
  systemctl restart supermalaab
  echo "Moved. Push notifications are ON (key at $KEY)."; exit 0
fi

echo "Paste the whole Firebase key file now (nothing shows while you paste)."
echo "Then press Enter, then Ctrl+D."
TMP="$(mktemp)"; chmod 600 "$TMP"
trap 'stty echo 2>/dev/null || true; rm -f "$TMP"' EXIT
stty -echo 2>/dev/null || true
cat > "$TMP"
stty echo 2>/dev/null || true
echo

if ! python3 - "$TMP" <<'PY'
import json, sys
k = json.load(open(sys.argv[1]))
ok = k.get("type") == "service_account" and k.get("project_id") == "super-malaab-app" \
     and "BEGIN PRIVATE KEY" in k.get("private_key", "") and k.get("client_email", "").endswith(".iam.gserviceaccount.com")
sys.exit(0 if ok else 1)
PY
then
  echo "That is not the service-account key of the Firebase project super-malaab-app. Nothing saved."; exit 1
fi

install -d -m 750 -o root -g supermalaab "$DIR"
install -m 640 -o root -g supermalaab "$TMP" "$KEY"
rm -f "$OLD"
set_value "$NAME" "$KEY"
systemctl restart supermalaab
sleep 5
if ! sudo -u supermalaab test -r "$KEY"; then
  echo "Saved, but the site can't read $KEY: push stays off. Check the folder's owner and mode."
elif systemctl is-active --quiet supermalaab; then
  echo "Saved. Push notifications are ON (key at $KEY, readable by root and the site only)."
else
  echo "The site did not start again. Run: journalctl -u supermalaab -n 50"
fi
