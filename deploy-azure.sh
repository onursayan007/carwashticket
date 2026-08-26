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

say "PostgreSQL: $PG_SERVER (birkaç dakika sürer)"
az postgres flexible-server create \
  --resource-group "$RG" \
  --name "$PG_SERVER" \
  --location "$LOC" \
  --tier Burstable \
  --sku-name Standard_B1ms \
  --storage-size 32 \
  --version 17 \
  --admin-user "$PG_USER" \
  --admin-password "$PG_PASS" \
  --database-name "$PG_DB" \
  --public-access 0.0.0.0 \
  --yes -o none

PG_HOST="$PG_SERVER.postgres.database.azure.com"

say "Container Apps ortamı"
az containerapp env create -g "$RG" -n "$APP-env" -l "$LOC" -o none

# --- Uygulama tanımı ---------------------------------------------------------
# Tek app içinde iki konteyner: nginx dışarı bakar, API sadece localhost'ta.
say "Uygulama tanımı hazırlanıyor"
cat > /tmp/containerapp.yaml <<YAML
properties:
  configuration:
    ingress:
      external: true
      targetPort: 80
      transport: auto
  template:
    containers:
      - name: web
        image: ghcr.io/$GH_USER/carwash-web:latest
        env:
          - name: API_UPSTREAM
            value: "127.0.0.1:8080"
        resources:
          cpu: 0.25
          memory: 0.5Gi
      - name: api
        image: ghcr.io/$GH_USER/carwash-api:latest
        env:
          - name: ASPNETCORE_ENVIRONMENT
            value: Development
          - name: ASPNETCORE_URLS
            value: http://+:8080
          - name: ConnectionStrings__Postgres
            value: "Host=$PG_HOST;Database=$PG_DB;Username=$PG_USER;Password=$PG_PASS;SslMode=Require"
          - name: Database__MigrateOnStartup
            value: "true"
          - name: Jwt__Key
            value: "$JWT_KEY"
          - name: Jwt__Issuer
            value: carwashticket
          - name: Jwt__Audience
            value: carwashticket-web
          - name: Auth__RefreshCookieSecure
            value: "true"
          - name: Auth__RefreshCookieSameSite
            value: Lax
          - name: Payment__UseMock
            value: "true"
          - name: Payment__CommissionRate
            value: "0.10"
        resources:
          cpu: 0.5
          memory: 1.0Gi
    scale:
      minReplicas: 0
      maxReplicas: 1
YAML

say "Uygulama oluşturuluyor"
az containerapp create -g "$RG" -n "$APP" --environment "$APP-env" --yaml /tmp/containerapp.yaml -o none

FQDN="$(az containerapp show -g "$RG" -n "$APP" --query properties.configuration.ingress.fqdn -o tsv)"

# Adres ancak app oluştuktan sonra belli oluyor; adrese bağlı ayarları şimdi veriyoruz.
say "Adres ayarları: https://$FQDN"
az containerapp update -g "$RG" -n "$APP" \
  --set-env-vars \
    "Spa__BaseUrl=https://$FQDN" \
    "Payment__CallbackUrl=https://$FQDN/api/payments/callback" \
    "Cors__AllowedOrigins__0=https://$FQDN" \
  -o none

rm -f /tmp/containerapp.yaml

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
