#!/usr/bin/env bash
# Deploys the site (front end only) to the supermalaab.com cPanel hosting. Run it in cPanel → Terminal:
#   bash <(curl -sSL https://raw.githubusercontent.com/MBSharaf/super-app/main/tools/deploy-cpanel.sh) [commit]
# It downloads only the site files of that commit (default: main), copies them into public_html, sets the base
# folder to "/" (the site is at the domain root here) and keeps cPanel's PHP block at the end of .htaccess.
# The first deploy saved the hosting's original files in ~/backup-default.
set -euo pipefail
ref="${1:-main}"
cd ~
rm -rf sa-src && mkdir sa-src
curl -sSL "https://codeload.github.com/MBSharaf/super-app/tar.gz/${ref}" | tar xz -C sa-src --strip-components=1 --wildcards \
  '*/.htaccess' '*/404.html' '*/index.html' '*/offline.html' '*/manifest.json' '*/og-image.png' '*/robots.txt' \
  '*/sitemap.xml' '*/sw.js' '*/chatbot/*' '*/css/*' '*/icons/*' '*/js/*' '*/models/*' '*/vendor/*'
sed -i 's|<base href="/super-app/" />|<base href="/" />|' sa-src/index.html
grep -q '<base href="/" />' sa-src/index.html || { echo "index.html: base tag not found, nothing deployed"; exit 1; }
cp -r sa-src/. public_html/
if [ -f backup-default/.htaccess ]; then cat sa-src/.htaccess backup-default/.htaccess > public_html/.htaccess; fi
echo "Deployed ${ref}: $(find sa-src -type f | wc -l) files"
