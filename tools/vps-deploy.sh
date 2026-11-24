#!/usr/bin/env bash
# Super Malaab — builds and (re)starts the site + API on the VPS prepared by tools/vps-setup.sh. Safe to run again.
#   bash <(curl -fsSL https://raw.githubusercontent.com/MBSharaf/super-app/main/tools/vps-deploy.sh) [commit]
# - Code: /opt/supermalaab/src (git), build output: /opt/supermalaab/app, site files: /opt/supermalaab/site
# - Settings and secrets: /etc/supermalaab/app.env (root only, made once, never printed). Edit it to change settings.
# - Database: SuperMalaab on the local SQL Server, with its own login (not sa).
# - Database updates are NOT applied here: the owner runs `supermalaab-migrate` himself (it applies the migrations bundle).
set -euo pipefail
[ "$(id -u)" = 0 ] || { echo "Run as root"; exit 1; }
REF="${1:-main}"
BASE=/opt/supermalaab
step() { printf '\n\033[1;32m==> %s\033[0m\n' "$*"; }
sql() { docker exec mssql bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -h -1 -W -Q "$0"' "$1"; }

step "Code ($REF)"
if [ ! -d $BASE/src/.git ]; then git clone --filter=blob:none https://github.com/MBSharaf/super-app.git $BASE/src; fi
git -C $BASE/src fetch --quiet origin
git -C $BASE/src checkout --quiet --force "$REF"
[ "$REF" = main ] && git -C $BASE/src reset --quiet --hard origin/main
echo "commit: $(git -C $BASE/src log --oneline -1)"

step "Database and login (made once)"
if [ ! -f /etc/supermalaab/app.env ]; then
  DBPASS="Db$(openssl rand -hex 16)!"
  sql "IF DB_ID('SuperMalaab') IS NULL CREATE DATABASE SuperMalaab;"
  sql "IF SUSER_ID('supermalaab_app') IS NULL CREATE LOGIN supermalaab_app WITH PASSWORD='$DBPASS', CHECK_POLICY=ON; ELSE ALTER LOGIN supermalaab_app WITH PASSWORD='$DBPASS';"
  sql "USE SuperMalaab; IF USER_ID('supermalaab_app') IS NULL CREATE USER supermalaab_app FOR LOGIN supermalaab_app; ALTER ROLE db_owner ADD MEMBER supermalaab_app;"
  umask 077
  cat > /etc/supermalaab/app.env <<EOF
# Super Malaab server settings (root only). After editing: systemctl restart supermalaab
ASPNETCORE_ENVIRONMENT=Staging
ASPNETCORE_URLS=http://127.0.0.1:5000
HTTPS_PORT=443
DOTNET_CLI_TELEMETRY_OPTOUT=1
CONNECTIONSTRINGS__DEFAULT=Server=127.0.0.1,1433;Database=SuperMalaab;User Id=supermalaab_app;Password=$DBPASS;TrustServerCertificate=True;Encrypt=True
SITE__ROOT=$BASE/site
SITE__PUBLICURL=https://supermalaab.com/
SITE__BASEPATH=/
ALLOWEDHOSTS=supermalaab.com;www.supermalaab.com;localhost;127.0.0.1
SECURITY__DATA_PROTECTION_KEYS_PATH=$BASE/keys
STORAGE__LOCAL_ROOT=$BASE/storage
SERILOG__WRITETO__1__ARGS__PATH=$BASE/logs/superapp-.log
SEED__ONSTARTUP=true
SEED__DEMODATA=false
SEED__CEO_NAME=Sharaf
SEED__CEO_EMAIL=
SEED__CEO_PASSWORD=
EMAIL__MODE=File
EMAIL__FILE_FOLDER=$BASE/emails
EMAIL__FROM=no-reply@supermalaab.com
EMAIL__FROM_NAME=Super Malaab
EMAIL__SMTP_HOST=
EMAIL__SMTP_PORT=587
EMAIL__SMTP_USER=
EMAIL__SMTP_PASSWORD=
PAYMENTS__PUBLICBASEURL=https://supermalaab.com
LAUNCH__ALLOWSIMULATEDPAYMENTS=true
EXTERNALLOGINS__GOOGLE__CLIENTID=426602784194-5el1fkohelnem4ph1vchfgrk2qtcraaj.apps.googleusercontent.com
EXTERNALLOGINS__FACEBOOK__APPID=
EXTERNALLOGINS__FACEBOOK__APPSECRET=
DOCS__ENABLED=false
FOOTBALL__THESPORTSDB__KEY=123
FEATURES__STOREENABLED=true
FEATURES__SHOPPAYMENT=cod
EOF
  chmod 600 /etc/supermalaab/app.env
fi
# The site lives at the domain root (supermalaab.com/pitches); old /super-app/ links redirect there.
# The public address is used in e-mail links (reset password, confirm e-mail), the sitemap and robots.txt.
sed -i '/^SITE__PUBLICURL=/d;/^SITE__BASEPATH=/d' /etc/supermalaab/app.env
printf 'SITE__PUBLICURL=https://supermalaab.com/\nSITE__BASEPATH=/\n' >> /etc/supermalaab/app.env

step "Build the API"
cd $BASE/src
dotnet publish src/SuperApp.Api -c Release -o $BASE/app.new --nologo -v q
rm -rf $BASE/app.old; [ -d $BASE/app ] && mv $BASE/app $BASE/app.old; mv $BASE/app.new $BASE/app

step "Database logins: the site gets only what it needs"
# Least privilege: the site's login (supermalaab_app) can read and write data and draw receipt numbers, but cannot
# change tables, drop anything or read other databases. Database updates use a separate owner login
# (supermalaab_migrate) that only supermalaab-migrate uses; its connection string is in /etc/supermalaab/migrate.env.
if [ ! -f /etc/supermalaab/migrate.env ]; then
  MIGPASS="Mg$(openssl rand -hex 16)!"
  sql "IF SUSER_ID('supermalaab_migrate') IS NULL CREATE LOGIN supermalaab_migrate WITH PASSWORD='$MIGPASS', CHECK_POLICY=ON; ELSE ALTER LOGIN supermalaab_migrate WITH PASSWORD='$MIGPASS';"
  sql "USE SuperMalaab; IF USER_ID('supermalaab_migrate') IS NULL CREATE USER supermalaab_migrate FOR LOGIN supermalaab_migrate; ALTER ROLE db_owner ADD MEMBER supermalaab_migrate;"
  ( umask 077; echo "CONNECTIONSTRINGS__DEFAULT=Server=127.0.0.1,1433;Database=SuperMalaab;User Id=supermalaab_migrate;Password=$MIGPASS;TrustServerCertificate=True;Encrypt=True" > /etc/supermalaab/migrate.env )
fi
# Schemas made by the old owner-level site login belong to it, and a schema's owner can drop or change anything in it:
# hand every such schema to dbo first (grants are given again right after).
sql "USE SuperMalaab; DECLARE @s nvarchar(max) = N'';
SELECT @s += N'ALTER AUTHORIZATION ON SCHEMA::' + QUOTENAME(name) + N' TO dbo; ' FROM sys.schemas WHERE principal_id = USER_ID('supermalaab_app');
EXEC sp_executesql @s;" >/dev/null
sql "USE SuperMalaab;
ALTER ROLE db_datareader ADD MEMBER supermalaab_app;
ALTER ROLE db_datawriter ADD MEMBER supermalaab_app;
GRANT EXECUTE TO supermalaab_app;
GRANT UPDATE ON SCHEMA::[money] TO supermalaab_app;
IF IS_ROLEMEMBER('db_owner','supermalaab_app') = 1 ALTER ROLE db_owner DROP MEMBER supermalaab_app;" >/dev/null
echo "site login: $(sql "SET NOCOUNT ON; USE SuperMalaab; SELECT CONCAT('db_owner=', IS_ROLEMEMBER('db_owner','supermalaab_app'), ' schemas_owned=', (SELECT COUNT(*) FROM sys.schemas WHERE principal_id = USER_ID('supermalaab_app')))" | grep db_owner | tr -d ' ') (both 0 = limited)"

step "Database updates bundle (applied by the owner with supermalaab-migrate)"
export PATH="$PATH:/root/.dotnet/tools"
command -v dotnet-ef >/dev/null || dotnet tool install --global dotnet-ef --version '10.*' >/dev/null
CONNECTIONSTRINGS__DEFAULT='Server=none;Database=none' dotnet ef migrations bundle --project src/SuperApp.Infrastructure --startup-project src/SuperApp.Api \
  -o $BASE/migrate-bundle --force --self-contained -r linux-x64 >/dev/null
cat > /usr/local/bin/supermalaab-migrate <<'EOF'
#!/usr/bin/env bash
# Applies the waiting database updates to the live database, then restarts the site.
set -euo pipefail
# Read only the connection string (app.env is a systemd file, not a shell script)
# The owner login (migrate.env) changes tables; the site's own login is not allowed to
ENVF=/etc/supermalaab/migrate.env; [ -f "$ENVF" ] || ENVF=/etc/supermalaab/app.env
export CONNECTIONSTRINGS__DEFAULT="$(grep '^CONNECTIONSTRINGS__DEFAULT=' "$ENVF" | cut -d= -f2-)"
/opt/supermalaab/migrate-bundle --connection "$CONNECTIONSTRINGS__DEFAULT"
systemctl restart supermalaab
echo "Database is up to date."
EOF
chmod 700 /usr/local/bin/supermalaab-migrate

step "Site files"
rm -rf $BASE/site.new && mkdir -p $BASE/site.new
for f in css js models vendor icons index.html 404.html og-image.png robots.txt sitemap.xml manifest.json sw.js offline.html llms.txt; do
  [ -e "$f" ] && cp -r "$f" $BASE/site.new/
done
rm -rf $BASE/site.old; [ -d $BASE/site ] && mv $BASE/site $BASE/site.old; mv $BASE/site.new $BASE/site
chown -R supermalaab:supermalaab $BASE/app $BASE/site $BASE/keys $BASE/storage $BASE/logs $BASE/emails

step "Service"
cat > /etc/systemd/system/supermalaab.service <<EOF
[Unit]
Description=Super Malaab site and API
After=network-online.target docker.service
Wants=network-online.target

[Service]
User=supermalaab
WorkingDirectory=$BASE/app
EnvironmentFile=/etc/supermalaab/app.env
ExecStart=/usr/bin/dotnet $BASE/app/SuperApp.Api.dll
Restart=always
RestartSec=5
KillSignal=SIGINT
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full
ProtectHome=true
ReadWritePaths=$BASE/keys $BASE/storage $BASE/logs $BASE/emails

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable --quiet supermalaab
systemctl restart supermalaab

step "nginx"
if [ ! -f /etc/nginx/sites-available/supermalaab ]; then
  cat > /etc/nginx/sites-available/supermalaab <<'EOF'
map $http_upgrade $connection_upgrade { default upgrade; '' close; }
server {
    listen 80;
    listen [::]:80;
    server_name supermalaab.com www.supermalaab.com;
    server_tokens off;
    client_max_body_size 25m;
    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_read_timeout 120s;
    }
}
EOF
  ln -sf /etc/nginx/sites-available/supermalaab /etc/nginx/sites-enabled/supermalaab
  rm -f /etc/nginx/sites-enabled/default
fi
# Sign-in answers carry several cookies (session, antiforgery, remember-me): the default nginx header buffer is too
# small and answers "502 upstream sent too big header", so the buffers are raised for the whole server
printf 'proxy_buffer_size 32k;\nproxy_buffers 8 32k;\nproxy_busy_buffers_size 64k;\n' > /etc/nginx/conf.d/supermalaab-buffers.conf
# Cloudflare sits in front of the site: nginx takes the visitor's real IP from CF-Connecting-IP, but only when the
# request comes from Cloudflare's own ranges (cloudflare.com/ips), so rate limits and logs see the real visitor
{ for r in 173.245.48.0/20 103.21.244.0/22 103.22.200.0/22 103.31.4.0/22 141.101.64.0/18 108.162.192.0/18 \
    190.93.240.0/20 188.114.96.0/20 197.234.240.0/22 198.41.128.0/17 162.158.0.0/15 104.16.0.0/13 104.24.0.0/14 \
    172.64.0.0/13 131.0.72.0/22 2400:cb00::/32 2606:4700::/32 2803:f800::/32 2405:b500::/32 2405:8100::/32 \
    2a06:98c0::/29 2c0f:f248::/32; do echo "set_real_ip_from $r;"; done; echo 'real_ip_header CF-Connecting-IP;'; } \
  > /etc/nginx/conf.d/supermalaab-cloudflare.conf
# HTTP/2: the browser gets the site's files in parallel over one connection (certbot writes "listen 443 ssl;").
# The old file is kept and put back if nginx does not accept the change.
NGX=/etc/nginx/sites-available/supermalaab
if grep -q 'listen 443 ssl;' $NGX; then
  cp $NGX /root/supermalaab-nginx.before-http2
  sed -i 's/listen 443 ssl;/listen 443 ssl http2;/; s/listen \[::\]:443 ssl ipv6only=on;/listen [::]:443 ssl http2 ipv6only=on;/' $NGX
  nginx -t -q 2>/dev/null || cp /root/supermalaab-nginx.before-http2 $NGX
fi
grep -q 'server_tokens off' /etc/nginx/nginx.conf || sed -i 's/# server_tokens off;/server_tokens off;/' /etc/nginx/nginx.conf
nginx -t -q && systemctl reload nginx

step "Check"
for _ in $(seq 1 40); do curl -fs http://127.0.0.1:5000/health >/dev/null 2>&1 && break; sleep 3; done
echo "service: $(systemctl is-active supermalaab)"
echo "health:  $(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5000/health)"
echo "page:    $(curl -s -o /dev/null -w '%{http_code}' -H 'Host: supermalaab.com' -H 'X-Forwarded-Proto: https' http://127.0.0.1/)"
echo "SUPERMALAAB_DEPLOY_DONE"
