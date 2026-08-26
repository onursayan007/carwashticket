# Azure'a demo dağıtımı

Öğrenci aboneliğiyle bu projeyi yayınlamak için adımlar.

## Mimari

Tek **Container App** içinde iki konteyner:

```
İnternet ──HTTPS──> Container App (tek ingress)
                      ├── web (nginx) : SPA'yı sunar, /api'yi localhost:8080'e geçirir
                      └── api         : ASP.NET Core, dışarı kapalı
                                           │
                                           ▼
                             Azure Database for PostgreSQL
```

Aynı Container App içindeki konteynerler `localhost` paylaşır. Bu yüzden yerelde
`api:8080` olan nginx upstream'i Azure'da `127.0.0.1:8080` oluyor — `API_UPSTREAM`
ortam değişkeniyle ayarlanıyor.

**Neden tek app:** SPA ve API aynı origin'de kalıyor. CORS yok, çerez `SameSite=Lax`
ile çalışıyor, Azure ücretsiz HTTPS veriyor.

## Maliyet

| Servis | Ücretsiz kapsam | Sonrası |
|---|---|---|
| Container Apps | Ayda 180.000 vCPU-sn + 2M istek | Kredi düşer |
| PostgreSQL Flexible Server (B1ms) | Yeni hesaplarda 12 ay ücretsiz | ~13 $/ay |
| GitHub Container Registry | Genel imajlar ücretsiz | — |

Öğrenci aboneliğinde 100 $ kredi var, kredi kartı istemiyor. Kredi biterse kaynaklar
durur, sürpriz fatura gelmez.

---

## 1. İmajlar (GitHub yapıyor)

İmajlar `.github/workflows/publish.yml` ile **her push'ta otomatik** derlenip
`ghcr.io`'ya gönderiliyor. Yerelde `docker build` / `docker push` yapmana gerek yok.

Tek gereken: harita anahtarını GitHub'a secret olarak eklemek.

```
GitHub → repo → Settings → Secrets and variables → Actions → New repository secret
  Name:  MAPTILER_KEY
  Value: <maptiler anahtarın>
```

Secret eklemeden de çalışır ama harita düşük detaylı demo katmanına düşer.

Push'tan sonra Actions sekmesinden "İmajları yayınla" işinin yeşil olduğunu gör, sonra:

```
GitHub → repo → Packages → carwash-api  → Package settings → Change visibility → Public
GitHub → repo → Packages → carwash-web  → Package settings → Change visibility → Public
```

İmajlar **public** olmalı, yoksa Container Apps çekemez.

---

## 2. Azure hazırlığı

```bash
brew install azure-cli          # macOS
az login
az account set --subscription "<abonelik-adı>"

az extension add --name containerapp --upgrade
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights
```

```bash
RG=carwash-rg
LOC=westeurope
APP=carwash
PGSERVER=carwash-pg-$RANDOM        # global benzersiz olmalı
PGPASS='<güçlü-bir-şifre>'
GHUSER=<github-kullanıcı-adın>

az group create --name $RG --location $LOC
```

---

## 3. Veritabanı

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

`--public-access 0.0.0.0` yalnızca **Azure servislerine** izin verir, internete açmaz.

---

## 4. Container App

```bash
az containerapp env create -g $RG -n $APP-env -l $LOC
```

Önce ingress'siz oluşturup FQDN'i öğreniyoruz, sonra adres içeren ayarları
güncelliyoruz. `containerapp.yaml` dosyasını oluştur:

```yaml
properties:
  configuration:
    ingress:
      external: true
      targetPort: 80
      transport: auto
  template:
    containers:
      - name: web
        image: ghcr.io/<GHUSER>/carwash-web:latest
        env:
          # Aynı app içinde konteynerler localhost paylaşıyor
          - name: API_UPSTREAM
            value: "127.0.0.1:8080"
        resources: { cpu: 0.25, memory: 0.5Gi }

      - name: api
        image: ghcr.io/<GHUSER>/carwash-api:latest
        env:
          - name: ASPNETCORE_ENVIRONMENT
            value: Development          # seed verisi + Swagger + mock ödeme
          - name: ASPNETCORE_URLS
            value: http://+:8080
          - name: ConnectionStrings__Postgres
            value: "Host=<PGSERVER>.postgres.database.azure.com;Database=carwashticket;Username=carwashadmin;Password=<PGPASS>;SslMode=Require"
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
          - name: Payment__UseMock
            value: "true"
          - name: Payment__CommissionRate
            value: "0.10"
        resources: { cpu: 0.5, memory: 1.0Gi }

    scale:
      minReplicas: 0        # kullanılmadığında uyur, kredi yakmaz
      maxReplicas: 1
```

```bash
az containerapp create -g $RG -n $APP --environment $APP-env --yaml containerapp.yaml
```

Adresi öğren ve adres içeren iki ayarı ver:

```bash
FQDN=$(az containerapp show -g $RG -n $APP --query properties.configuration.ingress.fqdn -o tsv)
echo "https://$FQDN"

az containerapp update -g $RG -n $APP \
  --set-env-vars "Spa__BaseUrl=https://$FQDN" \
                 "Payment__CallbackUrl=https://$FQDN/api/payments/callback" \
                 "Cors__AllowedOrigins__0=https://$FQDN"
```

> `Spa__BaseUrl` sahte 3DS ekranına yönlendirmek için kullanılıyor; boş kalırsa
> ödeme akışı hata verir.

---

## 5. Kontrol

```bash
curl -s -o /dev/null -w "%{http_code}\n" "https://$FQDN"                # 200
curl -s -o /dev/null -w "%{http_code}\n" "https://$FQDN/api/stations"   # 401 (giriş yok)
open "https://$FQDN"
```

Giriş ekranındaki demo butonlarıyla dene. Seed verisi (88 yıkama noktası) ilk
açılışta otomatik yüklenir.

---

## Güncelleme

Kod değiştiğinde:

```bash
git push                                    # Actions yeni imajı yayınlar
az containerapp update -g $RG -n $APP \
  --set-env-vars "REDEPLOY=$(date +%s)"     # yeni imajı çekmesi için tetikler
```

`:latest` etiketi aynı kaldığı için Container Apps kendiliğinden yeni imajı çekmez;
revizyonu tetiklemek gerekir. Daha sağlıklısı imajı `:${{ github.sha }}` etiketiyle
sabitleyip `--image` ile güncellemek.

---

## Dikkat edilecekler

**`minReplicas: 0`** — uzun süre kullanılmazsa konteyner uyur, ilk istek 10-20 sn
sürer. Demoyu göstereceğin gün bir kez ısıt. Krediyi korumak için bilerek böyle.

**`ASPNETCORE_ENVIRONMENT=Development`** — demo için gerekli: seed verisi, Swagger ve
sahte 3DS ekranı bu ortamda açılıyor. Bunun bedeli: `mock-callback` ucu internete açık
ve kimlik doğrulaması istemeden siparişi "ödenmiş" yapabiliyor. Portföy demosu için
kabul edilebilir, gerçek para dönen bir üründe asla olmaz.

**`Jwt__Key`** düz metin duruyor. Ciddi bir dağıtımda secret kullan:

```bash
az containerapp secret set -g $RG -n $APP --secrets jwt-key="<anahtar>"
# yaml'da: secretRef: jwt-key
```

**Veritabanı yedeği yok.** Demo verisi seed'den geliyor; silinirse yeniden oluşur.
