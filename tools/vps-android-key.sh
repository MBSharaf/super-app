#!/usr/bin/env bash
# Super Malaab — makes the Android app's signing key on the live server (once) and shows what the owner copies into
# GitHub → Settings → Secrets and variables → Actions. The key never goes into the repository or the chat.
# The same key must sign every version of the app (Google sign-in and Google Play recognise the app by it),
# so it is kept here, readable only by root, and never replaced once made.
#   bash <(curl -fsSL https://raw.githubusercontent.com/MBSharaf/super-app/main/tools/vps-android-key.sh)          make it (or show it again)
#   ... vps-android-key.sh --fingerprint      only the SHA-1 / SHA-256 (safe to share, needed by Google Cloud)
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root"; exit 1; }
DIR=/etc/supermalaab/android
STORE=$DIR/upload.keystore
PASS=$DIR/password
ALIAS=supermalaab

if [ ! -f "$STORE" ]; then
  install -d -m 700 "$DIR"
  umask 077
  openssl rand -hex 20 > "$PASS"
  TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
  openssl req -x509 -newkey rsa:2048 -sha256 -nodes -days 10000 -subj "/CN=Super Malaab/O=Super Malaab/C=EG" \
    -keyout "$TMP/key.pem" -out "$TMP/cert.pem" 2>/dev/null
  openssl pkcs12 -export -name "$ALIAS" -inkey "$TMP/key.pem" -in "$TMP/cert.pem" -out "$STORE" -passout "file:$PASS"
  chmod 600 "$STORE" "$PASS"
  echo "A new signing key was made and saved in $DIR (root only). Keep it: every app version needs the same key."
  echo
fi

fingerprints() {
  openssl pkcs12 -in "$STORE" -passin "file:$PASS" -nokeys -clcerts 2>/dev/null | openssl x509 -noout -fingerprint -sha1 | sed 's/^.*=/SHA-1:   /'
  openssl pkcs12 -in "$STORE" -passin "file:$PASS" -nokeys -clcerts 2>/dev/null | openssl x509 -noout -fingerprint -sha256 | sed 's/^.*=/SHA-256: /'
}

if [ "${1:-}" = "--fingerprint" ]; then fingerprints; exit 0; fi

echo "=== Add these 4 secrets in GitHub: Settings > Secrets and variables > Actions > New repository secret ==="
echo
echo "Name: ANDROID_KEY_ALIAS"
echo "Value: $ALIAS"
echo
echo "Name: ANDROID_KEYSTORE_PASSWORD"
echo "Value: $(cat "$PASS")"
echo
echo "Name: ANDROID_KEY_PASSWORD"
echo "Value: $(cat "$PASS")   (the same as above)"
echo
echo "Name: ANDROID_KEYSTORE_BASE64"
echo "Value (one long line, copy all of it):"
base64 -w0 "$STORE"; echo
echo
echo "=== Fingerprints (not secret, send these to Claude for Google sign-in) ==="
fingerprints
echo
echo "When done, clear the screen with: clear"
