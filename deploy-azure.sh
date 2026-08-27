#!/usr/bin/env bash
# Azure'a demo dağıtımı. Tek seferde çalışır, bittiğinde adresi yazar.
#
#   az login
#   ./deploy-azure.sh
#
# Gereken: az CLI, Azure aboneliği ve ghcr.io'da PUBLIC yapılmış iki imaj.

set -euo pipefail

GH_USER="${GH_USER:-onursayan007}"
RG="${RG:-carwash-rg}"
LOC="${LOC:-westeurope}"
APP="${APP:-carwash}"

# Öğrenci aboneliklerinde bazı bölgeler kapalı olabiliyor ("location is restricted").
# Sırayla denenir, ilk açık olan kullanılır.
REGIONS="${REGIONS:-northeurope swedencentral germanywestcentral francecentral uksouth westeurope eastus}"

# Postgres sunucu adı Azure genelinde benzersiz olmalı.
PG_SERVER="${PG_SERVER:-carwash-pg-$RANDOM$RANDOM}"
PG_USER="carwashadmin"
PG_DB="carwashticket"

say() { printf "\n\033[1;34m▸ %s\033[0m\n" "$1"; }

# --- Ön kontroller -----------------------------------------------------------
command -v az >/dev/null || { echo "az CLI kurulu değil: brew install azure-cli"; exit 1; }
az account show >/dev/null 2>&1 || { echo "Önce giriş yap: az login"; exit 1; }

say "Abonelik"
az account show --query "{ad:name, durum:state}" -o table

# Şifreler burada üretiliyor; hiçbir yere yazılmıyor.
PG_PASS="$(openssl rand -base64 24 | tr -d '/+=' | head -c 24)Aa1!"
JWT_KEY="$(openssl rand -base64 48)"

say "Uzantı ve sağlayıcılar"
az extension add --name containerapp --upgrade --only-show-errors >/dev/null
az provider register --namespace Microsoft.App --wait >/dev/null
az provider register --namespace Microsoft.OperationalInsights --wait >/dev/null
az provider register --namespace Microsoft.DBforPostgreSQL --wait >/dev/null

say "Kaynak grubu: $RG"
az group create -n "$RG" -l "$LOC" -o none

# Script tekrar çalıştırılırsa var olan sunucuyu kullan, yenisini açma.
EXISTING_PG="$(az postgres flexible-server list -g "$RG" --query "[0].name" -o tsv 2>/dev/null || true)"

if [ -n "$EXISTING_PG" ]; then
  PG_SERVER="$EXISTING_PG"
  say "PostgreSQL zaten var: $PG_SERVER (şifre yenileniyor)"
  az postgres flexible-server update -g "$RG" -n "$PG_SERVER" --admin-password "$PG_PASS" -o none
else
  # --database-name yeni CLI'de yalnızca elastic cluster'da geçerli; veritabanını ayrıca açıyoruz.
  CREATED=""

  for region in $REGIONS; do
    say "PostgreSQL deneniyor: $region (birkaç dakika sürer)"

    if az postgres flexible-server create \
        --resource-group "$RG" \
        --name "$PG_SERVER" \
        --location "$region" \
        --tier Burstable \
        --sku-name Standard_B1ms \
        --storage-size 32 \
        --version 17 \
        --admin-user "$PG_USER" \
        --admin-password "$PG_PASS" \
        --public-access 0.0.0.0 \
        --yes -o none 2>/tmp/pg_error; then
      LOC="$region"
      CREATED="evet"
      echo "  $region uygun."
      break
    fi

    echo "  $region olmadı: $(tail -1 /tmp/pg_error | cut -c1-90)"
  done

  if [ -z "$CREATED" ]; then
    echo
    echo "Hiçbir bölgede PostgreSQL açılamadı. Denenen: $REGIONS"
    echo "Farklı bölge denemek için: REGIONS=\"japaneast eastus2\" ./deploy-azure.sh"
    exit 1
  fi
fi

say "Veritabanı: $PG_DB"
az postgres flexible-server db create -g "$RG" -s "$PG_SERVER" -d "$PG_DB" -o none 2>/dev/null \
  || echo "  (zaten var)"

PG_HOST="$PG_SERVER.postgres.database.azure.com"

# Uygulama veritabanıyla aynı bölgede olsun; gecikme düşer.
say "Container Apps ortamı ($LOC)"
if ! az containerapp env show -g "$RG" -n "$APP-env" -o none 2>/dev/null; then
  az containerapp env create -g "$RG" -n "$APP-env" -l "$LOC" -o none 2>/tmp/env_error || {
    echo "  $LOC olmadı, alternatif bölgeler deneniyor..."

    for region in $REGIONS; do
      az containerapp env create -g "$RG" -n "$APP-env" -l "$region" -o none 2>/dev/null && {
        echo "  $region uygun."
        break
      }
    done
  }
fi

# --- Uygulamalar ---------------------------------------------------------------
# İki ayrı Container App: API içeriye kapalı, web dışarı bakıyor ve /api'yi
# API'nin iç adresine geçiriyor. Tarayıcı açısından tek origin, CORS yok.
#
# Tek app içinde iki konteyner de olurdu ama o yol YAML gerektiriyor ve CLI
# sürümleri arasında kırılgan; bu yapı düz komutlarla kuruluyor.

API_APP="$APP-api"

say "API uygulaması (dışarı kapalı)"
if ! az containerapp show -g "$RG" -n "$API_APP" -o none 2>/dev/null; then
  az containerapp create \
    -g "$RG" -n "$API_APP" \
    --environment "$APP-env" \
    --image "ghcr.io/$GH_USER/carwash-api:latest" \
    --ingress internal --target-port 8080 \
    --cpu 0.5 --memory 1.0Gi \
    --min-replicas 1 --max-replicas 1 \
    -o none
fi

az containerapp update -g "$RG" -n "$API_APP" \
  --set-env-vars \
    "ASPNETCORE_ENVIRONMENT=Development" \
    "ASPNETCORE_URLS=http://+:8080" \
    "ConnectionStrings__Postgres=Host=$PG_HOST;Database=$PG_DB;Username=$PG_USER;Password=$PG_PASS;SslMode=Require" \
    "Database__MigrateOnStartup=true" \
    "Jwt__Key=$JWT_KEY" \
    "Jwt__Issuer=carwashticket" \
    "Jwt__Audience=carwashticket-web" \
    "Auth__RefreshCookieSecure=true" \
    "Auth__RefreshCookieSameSite=Lax" \
    "Payment__UseMock=true" \
    "Payment__CommissionRate=0.10" \
    "Https__Redirect=false" \
  -o none

API_FQDN="$(az containerapp show -g "$RG" -n "$API_APP" --query properties.configuration.ingress.fqdn -o tsv)"
say "API iç adresi: $API_FQDN"

say "Web uygulaması (dışarı açık)"
if ! az containerapp show -g "$RG" -n "$APP" -o none 2>/dev/null; then
  az containerapp create \
    -g "$RG" -n "$APP" \
    --environment "$APP-env" \
    --image "ghcr.io/$GH_USER/carwash-web:latest" \
    --ingress external --target-port 80 \
    --cpu 0.25 --memory 0.5Gi \
    --min-replicas 1 --max-replicas 1 \
    -o none
fi

# nginx /api isteklerini buraya geçirecek.
az containerapp update -g "$RG" -n "$APP" \
  --set-env-vars "API_UPSTREAM=$API_FQDN" -o none

FQDN="$(az containerapp show -g "$RG" -n "$APP" --query properties.configuration.ingress.fqdn -o tsv)"

# Dış adres ancak web uygulaması kurulunca belli oluyor; adrese bağlı
# ayarları API'ye şimdi veriyoruz.
say "Adres ayarları: https://$FQDN"
az containerapp update -g "$RG" -n "$API_APP" \
  --set-env-vars \
    "Spa__BaseUrl=https://$FQDN" \
    "Payment__CallbackUrl=https://$FQDN/api/payments/callback" \
    "Cors__AllowedOrigins__0=https://$FQDN" \
  -o none

say "Kontrol"
for i in $(seq 1 30); do
  CODE="$(curl -s -o /dev/null -w '%{http_code}' "https://$FQDN" || true)"
  [ "$CODE" = "200" ] && break
  sleep 5
done

printf "\n\033[1;32m✓ Hazır:\033[0m https://%s\n" "$FQDN"
printf "  Swagger : https://%s/swagger\n" "$FQDN"
printf "  Giriş   : demo@test.com / Demo123!\n\n"
printf "Silmek için: az group delete -n %s --yes --no-wait\n" "$RG"
