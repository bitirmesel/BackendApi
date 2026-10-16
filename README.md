# BackendApi (DktApi)

ParroTalk'un ASP.NET Core 8 backend'i. Unity çocuk uygulaması ve Flutter terapist paneli bu API'yi kullanır.

- Mimari: [ARCHITECTURE.md](https://github.com/bitirmesel/GraduationProject/blob/main/ARCHITECTURE.md)
- Yol haritası: [ROADMAP.md](https://github.com/bitirmesel/GraduationProject/blob/main/ROADMAP.md)
- Canlı: Render (Docker), `main`'e merge edilen her değişiklik otomatik deploy edilir.

## Yapılandırma ve sırlar

**Hiçbir şifre, anahtar veya bağlantı cümlesi repoya yazılmaz.** Değerler şu sırayla okunur (sonraki öncekini ezer):

1. `appsettings.json` / `appsettings.{Ortam}.json` — yalnızca gizli olmayan varsayılanlar
2. `dotnet user-secrets` — yalnızca yerel geliştirmede (`Development`)
3. Ortam değişkenleri — Render'da **Environment** sekmesi

| Ortam değişkeni | Zorunlu | Açıklama |
|---|---|---|
| `ConnectionStrings__Pg` | ✅ | PostgreSQL bağlantı cümlesi (`Host=...;Port=5432;Database=...;Username=...;Password=...;SslMode=Require`). Yoksa `DATABASE_URL` denenir (Render'ın `postgres://...` biçimi de desteklenir). |
| `Jwt__Key` | ✅ | JWT imzalama anahtarı, **en az 32 karakter**. Eksikse uygulama açılmaz. |
| `FluentMe__User`, `FluentMe__Password`, `FluentMe__ApiKey` | Kelime telaffuzu için | Eksikse uygulama açılır ama `/api/pronunciation/check` 503 döner. |
| `CLOUDINARY_CLOUD_NAME`, `CLOUDINARY_API_KEY`, `CLOUDINARY_API_SECRET` | Medya ve telaffuz için | Cloudinary hesabı. |
| `OPENAI_API_KEY` | Hece telaffuzu için | Whisper (`/api/pronunciation/check-syllable`). |

> Ortam değişkenlerinde iç içe ayarlar `__` (iki alt çizgi) ile yazılır: `Jwt:Key` → `Jwt__Key`.

Rastgele bir JWT anahtarı üretmek için (Git Bash / macOS / Linux):

```bash
openssl rand -base64 48
```

## Yerel çalıştırma

Gereksinim: .NET 8 SDK.

Sırları bir kez makinene kaydet (değerler repo dışında, kullanıcı profilinde saklanır):

```bash
dotnet user-secrets set "ConnectionStrings:Pg" "Host=...;Port=5432;Database=...;Username=...;Password=..."
dotnet user-secrets set "Jwt:Key" "<en-az-32-karakter-rastgele-deger>"
dotnet user-secrets set "FluentMe:User" "..."
dotnet user-secrets set "FluentMe:Password" "..."
dotnet user-secrets set "FluentMe:ApiKey" "..."
dotnet user-secrets set "CLOUDINARY_CLOUD_NAME" "..."
dotnet user-secrets set "CLOUDINARY_API_KEY" "..."
dotnet user-secrets set "CLOUDINARY_API_SECRET" "..."
dotnet user-secrets set "OPENAI_API_KEY" "..."
```

Kayıtlı sırları görmek için `dotnet user-secrets list`. Sonra:

```bash
dotnet run
```

Swagger: http://localhost:5194/swagger

> ⚠️ Uygulama açılışta veritabanı migration'larını otomatik uygular. Ayrı bir geliştirme veritabanı gelene kadar
> (ROADMAP PR-03) bağlandığın veritabanının hangisi olduğuna dikkat et.

## Katkı

`main`'e doğrudan push yok. Her iş `feature/PR-XX-...` dalında yapılır ve PR şablonu doldurularak açılır.
