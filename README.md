# Araç Yıkama Bileti

## Canlı Demo
https://carwash.thankfulfield-d5ec1329.swedencentral.azurecontainerapps.io/

Akaryakıt istasyonlarında ve oto yıkamacılarda **kasada sıra beklemeden** yıkama hizmeti satın alma platformu. Müşteri haritadan işyeri seçer, ödeme yapar, QR bilet alır; yıkama noktasında bilet okutulur.

Çok kiracılı (multi-tenant) yazıldı: tek istasyona da kurulabilir, yüzlerce işyerine de.

<img width="330" height="718" alt="image" src="https://github.com/user-attachments/assets/cab7e26a-1533-409a-9ab1-ccfaf0d1ea4f" />


```bash
docker compose up --build
# http://localhost:8080
```

---

## Demo hesapları

| Rol | E-posta | Şifre | Ne yapar |
|---|---|---|---|
| Müşteri | `demo@test.com` | `Demo123!` | Haritadan seçer, sepet oluşturur, öder, biletlerini görür |
| QR okuyucu | `staff@test.com` | `Demo123!` | Kamerayla bilet okutur |
| İşyeri | `isyeri@test.com` | `Demo123!` | Fiyat belirler, ciro ve hakedişini görür |
| Admin | `admin@test.com` | `Demo123!` | Yeni işyeri açar, geçici şifre gönderir |

Ödeme **mock modda**: gerçek kart çekilmez, sahte bir 3DS ekranı üzerinden "Onayla" veya "Reddet" seçilir.

---

## Roller ve akış

```
Admin ──> işyeri açar, geçici şifre gider
            │
            ▼
İşyeri ──> fiyatlarını girer (su, köpük, paket…)
            │
            ▼
Müşteri ─> haritadan seçer ─> sepet ─> ödeme ─> QR bilet
                                                   │
                                                   ▼
                                        QR okuyucu ─> bilet kullanılır
```

İki iş modeli destekleniyor:

- **Self servis** — birim satılır (2 su + 1 köpük). Her birim için ayrı QR üretilir, tek tek okutulur.
- **Tam hizmet** — paket satılır (dış yıkama, pasta cila, detailing). Aracı işyeri teslim alır.

---

## Teknoloji

| Katman | Seçim |
|---|---|
| Backend | ASP.NET Core 9, C# |
| Veritabanı | PostgreSQL 17 + EF Core (Npgsql) |
| Frontend | Vue 3 (Composition API), TypeScript, Vite, Pinia, Tailwind v4 |
| Harita | MapLibre GL + MapTiler |
| Kimlik | ASP.NET Identity + JWT (15 dk) + refresh token (7 gün, httpOnly) |
| Test | xUnit, gerçek PostgreSQL üzerinde entegrasyon testleri |

---

## Mühendislik kararları

Bu projeyi sıradan bir CRUD uygulamasından ayıran şeyler:

### Idempotent sipariş oluşturma

Aynı `Idempotency-Key` ile gelen ikinci istek yeni sipariş yaratmaz. Sadece "önce sorgula, sonra yaz" değil — **yarış durumu da ele alınıyor**: iki istek aynı anda geldiğinde ikisi de "kayıt yok" görür, unique index birini reddeder, Postgres `23505` yakalanıp kazananın sonucu döndürülür.

> [`OrderService.CreateAsync`](api/Orders/OrderService.cs)

### Çift kayıtlı muhasebe defteri

Her para hareketi aynı `TransactionId` altında Debit + Credit satırları olarak yazılır; tutarlar daima pozitif, yön ayrı alanda. Dengesiz bir set **yazılmadan önce** reddedilir.

Panel'deki ciro/komisyon/hakediş siparişlerden değil **defterden** hesaplanır — böylece iadeler kendiliğinden netleşir ve rapor defterle her zaman tutar.

> [`LedgerService`](api/Ledger/LedgerService.cs)

### Atomik bilet doğrulama

Bilet kullanımı tek `ExecuteUpdateAsync` ile yapılır ve **yetki kontrolü aynı SQL ifadesinin içindedir**:

```csharp
.Where(t => t.Code == code
         && t.Status == TicketStatus.Issued
         && t.ExpiresAt > now
         && db.StationStaff.Any(ss => ss.UserId == staffUserId
                                   && ss.StationId == t.StationId))
.ExecuteUpdateAsync(...)
```

Yetkiyi ayrı sorguda kontrol etseydik kontrol ile güncelleme arasında pencere kalırdı. Böylece iki personel aynı bileti aynı anda kullanamaz — biri 1 satır, diğeri 0 satır alır.

Geçersiz kod, süresi dolmuş bilet, kullanılmış bilet ve başka istasyonun bileti **aynı yanıtı** döner; aksi halde rastgele kod deneyerek bilgi toplanabilirdi.

> [`TicketService.RedeemAsync`](api/Tickets/TicketService.cs)

### Durum makinesi, derleyici tarafından zorlanıyor

`Order.Status` setter'ı `private`. Bir controller `order.Status = ...` yazarsa **derlenmez**. Geçişler yalnızca `OrderStateMachine` üzerinden yapılır, izin verilen geçişler tek tabloda tanımlıdır.

> [`OrderStateMachine`](api/Orders/OrderStateMachine.cs)

### Ödeme sağlayıcısı soyutlaması

`IPaymentProvider` arkasında iki uygulama var. Kural: **"iyzico" kelimesi tüm kod tabanında tek dosyada geçer** — yapılandırma anahtarları ve DI kaydı bile sağlayıcıdan bağımsız isimlendirildi.

Gerçek sağlayıcı yokken uygulama sessizce mock'a düşmez, ayağa kalkarken hata verir.

> [`IPaymentProvider`](api/Payments/IPaymentProvider.cs)

### Kiracı izolasyonu

Her sorgu `StationId` ile filtrelenir. Panel'in beş ucu da tek bir yetki kapısından geçer; işyeri başka istasyonun verisini isterse `403` alır, hizmet güncellemede istasyon filtresi sorgunun içindedir (önce bul sonra kontrol et değil).

> [`PanelController.ResolveStationAsync`](api/Controllers/PanelController.cs)

### Refresh token rotasyonu

Token'ın kendisi değil **SHA-256 özeti** saklanır; veritabanı sızarsa oturum açılamaz. Her yenilemede eski token iptal edilir. Access token istemcide sadece bellekte tutulur, `localStorage`'a yazılmaz.

> [`AuthController`](api/Controllers/AuthController.cs), [`session.ts`](web/src/api/session.ts)

### Değerlendirme: türetilmiş veri asla elle güncellenmez

`Station.RatingAverage` bir önbellek; her yeni yorumda `Reviews` tablosundan **yeniden hesaplanır**, üzerine eklenmez. Sipariş başına tek yorum unique index ile garanti; ikinci deneme `409` alır. Sahiplik ve durum kontrolü tek sorguda yapılır — başkasının siparişi "bulunamadı" döner, "yetkisiz" değil.

Yorumlarda tam ad gösterilmez, baş harfe indirgenir (`Onur S.`).

> [`ReviewsController`](api/Controllers/ReviewsController.cs)

### Tip güvenliği: şemadan üretilen istemci tipleri

Frontend tipleri elle yazılmaz, backend'in Swagger şemasından üretilir (`npm run gen:api`). Backend'de bir DTO değişirse frontend **derlenmez**.

---

## Testler

```bash
dotnet test api.tests/CarWashTicket.Api.Tests.csproj
```

7 entegrasyon testi, **gerçek PostgreSQL üzerinde** (her koşuda yaratılıp silinen ayrı veritabanı), HTTP seviyesinde gerçek JWT ile:

| Test | Ne kanıtlıyor |
|---|---|
| Aynı idempotency key → tek sipariş | İki yanıtta aynı `OrderId`, veritabanında tek kayıt |
| Aynı webhook olayı → tek işlem | 1 webhook kaydı, **1 bilet, 3 defter satırı** (ikinci set oluşmamış) |
| Aynı bilet iki kez okutulamaz | İlki başarılı, ikincisi reddedilir |
| Ödenmemiş siparişe bilet yok | Durum `AwaitingPayment`, bilet sayısı 0 |
| Defter dengesi sıfır | Debit − Credit = 0 |
| Kiracı izolasyonu | Başka istasyon → `403`, kendi istasyonu → boş liste |
| Çok kalemli sipariş | 2 su + 1 köpük → 3 ayrı bilet, farklı kodlar |

**InMemory sağlayıcı bilerek kullanılmadı.** Test edilen davranışların yarısı (unique index çakışması, `ExecuteUpdateAsync`) InMemory'de doğrulanamaz — orada testler yeşil yanıp hiçbir şey kanıtlamazdı.

---

## Yerel geliştirme (Docker'sız)

<details>
<summary>Aç</summary>

Gereksinimler: .NET 9 SDK, Node 22+, PostgreSQL 17.

```bash
# Veritabanı
createdb carwashticket

# Gizli ayarlar
cd api
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Database=carwashticket;Username=postgres;Password=..."
dotnet ef database update --project CarWashTicket.Api.csproj

# API
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=https://localhost:7001 \
  dotnet run --project api/CarWashTicket.Api.csproj --no-launch-profile

# Web
cd web && cp .env.example .env && npm install && npm run dev
```

Ayrı origin'de çalışıldığı için tarayıcının dev sertifikasına güvenmesi gerekir:
`dotnet dev-certs https --trust`

Backend DTO'ları değiştiğinde istemci tiplerini yenile: `cd web && npm run gen:api`

</details>

---

## Dağıtım

Azure Container Apps + PostgreSQL Flexible Server ile adım adım:
**[DEPLOY-AZURE.md](DEPLOY-AZURE.md)**

---

## Bilinçli olarak yapılmayanlar

Bu bir portföy projesi; ürünleşme adımlarının bir kısmı kapsam dışı bırakıldı:

- **iyzico entegrasyonu yazıldı ama sandbox'ta doğrulanmadı.** `IyzicoPaymentProvider` soyutlamanın gerçek bir sağlayıcıyla nasıl kullanılacağını gösteriyor; alıcı bilgileri yer tutucu, webhook imza şeması doğrulanmadı. Canlıya çıkmadan önce test edilmeli.
- **Başlangıç puanları demo verisi.** Değerlendirme akışı çalışıyor; ilk gerçek yorum geldiğinde o işyerinin puanı `Reviews` tablosundan yeniden hesaplanır ve seed değerinin yerini alır.
- **Hakediş ödemesi (`Settled`) ve iade akışı** durum makinesinde tanımlı ama tetikleyen uç yok.
- **Süre dolumu için arka plan işi yok** — `Expired` durumuna geçiren zamanlanmış görev eklenmedi.
- Şifre sıfırlama, personel yönetimi, sayfalama, rate limiting.

---

## Lisans

Örnek/portföy projesi.
