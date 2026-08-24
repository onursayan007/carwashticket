# carwashticket

## API tiplerini üretme

`web/src/api/types.ts` backend'in Swagger şemasından üretilir, **elle düzenlenmez**.

Backend'de endpoint veya DTO değiştirdiğinde API'yi çalıştırıp şunu çalıştır:

```bash
cd web && npm run gen:api
```

Şema adresi varsayılan olarak `https://localhost:7001/swagger/v1/swagger.json`.
Farklıysa `API_SCHEMA_URL` ile ver:

```bash
API_SCHEMA_URL=https://localhost:5001/swagger/v1/swagger.json npm run gen:api
```

Üretilen tipler ham şema; uygulamada `web/src/types.ts` içindeki daraltılmış
sürümleri kullan.
