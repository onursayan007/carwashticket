# Azure'a demo dağıtımı

Öğrenci aboneliğiyle bu projeyi yayınlamak için adımlar.

## Mimari

Tek **Container App** içinde iki konteyner:

```
İnternet ──HTTPS──> Container App (tek ingress)
                      ├── nginx      : SPA'yı sunar, /api'yi localhost:8080'e geçirir
                      └── api        : ASP.NET Core, dışarı kapalı
                                          │
                                          ▼
                            Azure Database for PostgreSQL
```

Aynı Container App içindeki konteynerler `localhost` paylaşır. Bu yüzden yerelde
`api:8080` olan nginx upstream'i Azure'da `127.0.0.1:8080` oluyor — `API_UPSTREAM`
ortam değişkeniyle ayarlanıyor.

**Neden tek app:** SPA ve API aynı origin'de kalıyor. CORS yok, çerez `SameSite=Lax`
ile çalışıyor, Azure ücretsiz HTTPS veriyor.

---

## Maliyet

| Servis | Ücretsiz kapsam | Sonrası |
|---|---|---|
| Container Apps | Ayda 180.000 vCPU-sn + 2M istek | Kredi düşer |
| PostgreSQL Flexible Server (B1ms) | Yeni hesaplarda 12 ay ücretsiz | ~13 $/ay |
| GitHub Container Registry | Genel imajlar ücretsiz | — |

Öğrenci aboneliğinde 100 $ kredi var ve kredi kartı istemiyor. Kredi biterse
kaynaklar durur, sürpriz fatura gelmez.

> ACR (Azure Container Registry) yerine **ghcr.io** kullanıyoruz — Basic ACR ayda
> ~5 $ tutuyor, genel imajlar için gereksiz.

---

## 1. Hazırlık

```bash
# Azure CLI (macOS)
curl -sL https://aka.ms/InstallAzureCLIDeb | bash   # Linux
brew install azure-cli                              # macOS (Homebrew varsa)

az login
az account set --subscription "<abonelik-adı>"

az extension add --name containerapp --upgrade
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights
```

```bash
# Değişkenler — bir kez ayarla, sonraki komutlar bunları kullanır
RG=carwash-rg
LOC=westeurope
APP=carwash
PGSERVER=carwash-pg-$RANDOM      # global benzersiz olmalı
PGPASS='<güçlü-bir-şifre>'
GHUSER=onursayan007
```

```bash
az group create --name $RG --location $LOC
```

---

## 2. Veritabanı

```bash
az postgres flexible-server create \
  --resource-group $RG \
  --name $PGSERVER \
  --location $LOC \
  --tier Burstable \
  --sku-name Standard_B1ms \
  --storage-size 32 \
  --version 17 \
  --admin-user carwashadmin \
  --admin-password "$PGPASS" \
  --database-name carwashticket \
  --public-access 0.0.0.0 \
  --yes
```

`--public-access 0.0.0.0` yalnızca **Azure servislerine** izin verir, internete
açmaz. Container App bu sayede bağlanabilir.

---

## 3. İmajları yayınla

GitHub Container Registry'ye push edeceğiz. Önce `write:packages` yetkili bir
token gerekiyor:

```bash
echo "<github-token>" | docker login ghcr.io -u $GHUSER --password-stdin

# API
docker build -f api/Dockerfile -t ghcr.io/$GHUSER/carwash-api:latest .
docker push ghcr.io/$GHUSER/carwash-api:latest

# Web — MapTiler anahtarı derleme anında gömülür
docker build -f web/Dockerfile \
  --build-arg VITE_API_BASE_URL="" \
  --build-arg VITE_MAPTILER_KEY="<maptiler-anahtarı>" \
  -t ghcr.io/$GHUSER/carwash-web:latest .
docker push ghcr.io/$GHUSER/carwash-web:latest
```

İmajları **public** yap (GitHub → Packages → Package settings → Change visibility),
yoksa Container Apps çekemez.

---

## 4. Container App

```bash
az containerapp env create \
  --resource-group $RG \
  --name $APP-env \
  --location $LOC
```

Aşağıdaki dosyayı `containerapp.yaml` adıyla kaydet ve `<...>` yerlerini doldur:

```yaml
properties:
  configuration:
    ingress:
      external: true
      targetPort: 80          # nginx
      transport: auto
  template:
    containers:
      - name: web
        image: ghcr.io/<kullanici>/carwash-web:latest
        env:
          # Aynı app içinde konteynerler localhost paylaşıyor
          - name: API_UPSTREAM
            value: "127.0.0.1:8080"
        resources: { cpu: 0.25, memory: 0.5Gi }

      - name: api
        image: ghcr.io/<kullanici>/carwash-api:latest
        env:
          - name: ASPNETCORE_ENVIRONMENT
            value: Development          # seed verisi + Swagger + mock ödeme
          - name: ASPNETCORE_URLS
            value: http://+:8080
          - name: ConnectionStrings__Postgres
            value: "Host=<pgserver>.postgres.database.azure.com;Database=carwashticket;Username=carwashadmin;Password=<sifre>;SslMode=Require"
          - name: Database__MigrateOnStartup
            value: "true"
          - name: Jwt__Key
            value: "<openssl rand -base64 48 çıktısı>"
          - name: Jwt__Issuer
            value: carwashticket
          - name: Jwt__Audience
            value: carwashticket-web
          - name: Auth__RefreshCookieSecure
            value: "true"               # Azure HTTPS veriyor
          - name: Auth__RefreshCookieSameSite
            value: Lax
          - name: Spa__BaseUrl
            value: "https://<app-fqdn>"
          - name: Payment__UseMock
            value: "true"
          - name: Payment__CommissionRate
            value: "0.10"
          - name: Payment__CallbackUrl
            value: "https://<app-fqdn>/api/payments/callback"
        resources: { cpu: 0.5, memory: 1.0Gi }

    scale:
      minReplicas: 0          # kullanılmadığında sıfıra iner, kredi yakmaz
      maxReplicas: 1
```

```bash
az containerapp create \
  --resource-group $RG \
  --name $APP \
  --environment $APP-env \
  --yaml containerapp.yaml
```

Adresi öğren:

```bash
az containerapp show -g $RG -n $APP --query properties.configuration.ingress.fqdn -o tsv
```

`Spa__BaseUrl` ve `Payment__CallbackUrl` bu adresi içermeli. İlk oluşturmada FQDN
bilinmediği için app'i kurduktan sonra bir kez güncelle:

```bash
FQDN=$(az containerapp show -g $RG -n $APP --query properties.configuration.ingress.fqdn -o tsv)

az containerapp update -g $RG -n $APP \
  --set-env-vars "Spa__BaseUrl=https://$FQDN" "Payment__CallbackUrl=https://$FQDN/api/payments/callback"
```

---

## 5. Kontrol

```bash
curl -s "https://$FQDN/api/stations" -o /dev/null -w "%{http_code}\n"   # 401 beklenir (giriş yok)
open "https://$FQDN"
```

Demo hesaplarıyla gir (README'deki tablo). Seed verisi ilk açılışta yüklenir.

---

## Dikkat edilecekler

**`minReplicas: 0`** — uzun süre kullanılmazsa konteyner uyur, ilk istek 10-20 sn
sürer. Demoyu göstereceğin gün bir kez ısıtman iyi olur. Krediyi korumak için
bilerek böyle.

**`ASPNETCORE_ENVIRONMENT=Development`** — demo için gerekli: seed verisi, Swagger
ve sahte 3DS ödeme ekranı bu ortamda açılıyor. Gerçek bir üründe bu asla
`Development` olmaz.

**`Jwt__Key`** düz metin env olarak duruyor. Ciddi bir dağıtımda Azure Key Vault
veya Container Apps secret kullanılmalı:

```bash
az containerapp secret set -g $RG -n $APP --secrets jwt-key="<anahtar>"
# ardından env'de: secretRef: jwt-key
```

**Veritabanı yedeği yok.** Demo verisi zaten seed'den geliyor; silinirse yeniden
oluşur.
