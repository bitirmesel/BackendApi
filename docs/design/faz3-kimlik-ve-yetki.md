# Faz 3 Tasarımı: Kimlik Doğrulama ve Yetkilendirme

> **Durum:** ✅ Onaylandı (2026-10-17; kararlar §7) · PR-13 yayında · **Tarih:** 2026-10-17 · **Kapsam:** ROADMAP PR-13, PR-14, PR-18, PR-19, PR-15 · **Bağlam:** Proje tezden sonra bir startup olarak sürecek; bu faz gerçek kullanıcılar (çocuklar, terapistler) için zorunlu.
>
> Bu doküman kod yazılmadan önce geçiş planını netleştirmek için yazıldı. §7'deki kararlar verilmeden PR açılmayacak.

---

## 1. Bugünkü durum (2026-10-17, `main`)

| Konu | Durum |
|---|---|
| Endpoint koruması | **Yok.** 54 endpoint'in hiçbiri `RequireAuthorization` kullanmıyor. JWT middleware kurulu ama hiçbir şeyi zorunlu kılmıyor. Kimliği bilinmeyen biri, ID'yi değiştirerek her çocuğun görevlerine, oturumlarına ve bildirimlerine erişebilir. |
| Şifre saklama | **Düz metin.** `therapists.password` ve `players.password` (ikincisi nullable). Giriş `==` ile karşılaştırılıyor. |
| Terapist token'ı | Var. 12 saat geçerli, claim'ler: `sub` = e-posta, `therapistId`, `role=therapist`. Panel yalnızca 2 serviste (dashboard, rapor) gönderiyor. |
| Oyuncu (çocuk) token'ı | **Yok.** `POST /api/player/auth/login` yalnızca `playerId` döndürüyor; Fonem bu kimliği saklayıp her istekte yolda kullanıyor. |
| İmzalama anahtarı | `appsettings.json` içinde, **herkese açık repoda**. Anahtar yoksa koddaki sabit varsayılana düşüyor. |
| Fonem hazırlığı | `ApiClient` token varsa zaten `Authorization: Bearer` ekliyor. `AuthService` giriş yanıtında `token` alanı varsa kaydediyor (ama `SharedPreferences`'ta, şifresiz). |
| Terapist ↔ öğrenci ilişkisi | `therapist_clients (therapist_id, player_id)` tablosu. Sahiplik kontrolü bunun üzerinden yapılacak. |
| Migration | `Program.cs` açılışta `db.Database.Migrate()` çalıştırıyor. Şema değişiklikleri deploy ile uygulanıyor (PR-27'de doğrulandı). |

## 2. Ön koşul: imzalama anahtarı (🔑 Render)

Anahtar herkese açıkken token zorunlu kılmak **güvenlik sağlamaz**: anahtarı bilen herkes istediği claim'le token üretebilir. Bu yüzden:

- **PR-01** merge edilmeli: anahtar koddan çıkar, yoksa uygulama açılışta hata verir.
- `Jwt__Key` için Render'da **yeni, rastgele (≥ 32 bayt)** bir değer girilmeli. Eski anahtarı yeniden kullanmak, anahtar repoda açıkta durduğu için anlamsız.

Bu adımlar Render erişimine bağlı. Aşağıdaki PR'ların **ilk ikisi (13, 14) anahtar olmadan da güvenle yapılabilir**, çünkü hiçbir şeyi zorunlu kılmıyorlar. **Zorlama adımı (PR-15b) ön koşul tamamlanmadan yayına çıkmayacak.**

## 3. Hedef

1. **Şifreler hash'li saklanır.** Düz metin şifre veritabanında kalmaz.
2. **Her istek kimin yaptığını kanıtlar.** Terapist ve çocuk için JWT.
3. **Sahiplik kontrolü yapılır:**
   - Çocuk yalnızca kendi verisine erişir.
   - Terapist yalnızca `therapist_clients` üzerinden bağlı olduğu çocukların verisine erişir.
   - Yönetici endpoint'leri ayrı bir role bağlanır ya da kapatılır.
4. **Mevcut kullanıcılar kesintisiz geçer.** Kimse şifresini sıfırlamak zorunda kalmaz. Güncellenmemiş uygulamalar, bozulmak yerine anlaşılır bir "güncelle" mesajı alır.

**Kapsam dışı:** OAuth/sosyal giriş, iki faktörlü doğrulama, e-posta ile şifre sıfırlama (ayrı bir kart olabilir).

## 4. Tasarım

### 4.1 Şifre hash'leme (PR-13)

- **Algoritma:** ASP.NET Core `PasswordHasher<T>`. .NET 8'de PBKDF2-HMAC-SHA512, 100.000 iterasyon kullanıyor, sürüm bilgisini hash içinde taşıyor ve yeni bir paket gerektirmiyor. Alternatif BCrypt.Net-Next (tez raporu BCrypt diyor). İkisi de kabul edilebilir; karar §7-1.
- **Şema:** Yeni `password_hash` sütunu (nullable) eklenir. Eski `password` sütunu geçiş bitene kadar kalır.
- **Şeffaf geçiş (giriş anında):**
  1. `password_hash` doluysa onunla doğrula. Gerekirse (eski sürüm hash) yeniden hash'le.
  2. Boşsa ve `password` düz metin eşleşiyorsa → `password_hash` yaz, `password`'ü `NULL` yap. Kullanıcı hiçbir şey fark etmez.
- **Yazma yolları** (kayıt, öğrenci oluşturma, şifre değiştirme) yalnızca `password_hash` yazar.
- **Toplu geçiş:** Hiç giriş yapmayan hesaplar düz metin kalır. Bunun için tek seferlik bir migration ya da admin işi tüm `password`'leri hash'leyip temizler. Düz metin elde olduğu için bunu beklemeden de yapabiliriz. **Önerim:** PR-13'te giriş anında geçişi koyalım, aynı deploy'da tek seferlik toplu hash işini de çalıştıralım. Böylece düz metin hemen biter.
- **Test:** Hasher birim testleri, geçiş senaryoları (hash var / yok / yanlış şifre) ve toplu işin idempotent olması.

### 4.2 Token'lar (PR-14)

- **Çocuk girişi** `token` da döndürür (mevcut alanlar aynı kalır). Fonem'in bugünkü sürümü bu alanı **otomatik kaydedip gönderiyor**, istemci değişikliği gerekmeden token dolaşıma girer.
- **Claim'ler:**
  - Terapist: `sub`, `role=therapist`, `therapistId`
  - Çocuk: `sub`, `role=player`, `playerId`
  - `sub` e-posta yerine **sabit ID** olmalı; e-posta değişebiliyor.
- **Süre:**
  - Terapist: 12 saat (bugünkü gibi).
  - Çocuk: daha uzun, çünkü çocuklardan sık şifre istemek gerçekçi değil. Öneri 30 gün, kayan yenileme ile: süresinin yarısı geçmiş token'la gelen istekte yanıt başlığında yeni token döner. Karar §7-2.
- **İptal:** `players.token_version` / `therapists.token_version`. Şifre değişince artar; token'daki değer eşleşmezse 401. Çalınan tabletteki oturumu kapatmanın tek yolu bu.

### 4.3 İstemciler (PR-18 Fonem, PR-19 panel)

- **Saklama:** `flutter_secure_storage` (Android Keystore / iOS Keychain). Mevcut `SharedPreferences` değeri ilk açılışta taşınıp silinir.
- **Gönderme:** **Her** istekte. Fonem'de `ApiClient` bunu zaten yapıyor, ama `FluentMeService` gibi kendi HTTP çağrısını yapan yerler tek tek kontrol edilecek. Panelde 2 servisten tümüne yayılacak (bugün çoğu çağrı doğrudan `http.post`; ortak bir istemciye toplanacak, bu B12'yi de kapatır).
- **401 davranışı:** Oturum temizlenir, giriş ekranına dönülür. Çocuk tarafında bu bir "tekrar giriş yap" ekranı; şifresiz çocuk hesapları için B24 kararı burada belirleyici (§7-3).
- **Sürüm başlığı:** Her istekte `X-App-Version` gönderilir. Backend, desteklenmeyen sürümlere `426 Upgrade Required` döner ve istemci "Uygulamayı güncelle" ekranı gösterir. Startup için kalıcı bir altyapı: ileride başka kırıcı değişikliklerde de kullanılır.

### 4.4 Yetkilendirme ve sahiplik (PR-15)

PR-15 iki parçaya bölünür. Böylece zorlamadan önce gerçek trafiği ölçebiliriz.

**PR-15a: Gözlem modu (zorlamasız)**
- Bir middleware, her istek için `endpoint, rol, token var/yok/geçersiz, sahiplik tutuyor mu` bilgisini loglar. Hiçbir isteği reddetmez.
- Amaç: Zorlamayı açmadan önce hangi istemcinin hangi çağrıyı token'sız yaptığını görmek.

**PR-15b: Zorlama**
- Politikalar: `Therapist`, `Player`, `Admin`.
- Endpoint grupları için politika matrisi:

| Grup | Politika | Sahiplik kuralı |
|---|---|---|
| `/api/auth/*`, `/api/player/auth/*`, `/api/lookup/*` | Anonim | — |
| `/api/players/{id}/…`, `/api/students/{id}` (GET/PATCH) | Player **veya** Therapist | Player: `playerId == id`. Therapist: `therapist_clients`'ta (therapistId, id) var |
| `/api/students` (liste/oluşturma), `/api/tasks` (POST), `/api/therapists/{id}/…`, `/api/dashboard/*`, `/api/invitations` | Therapist | `therapistId` claim'i yoldaki/gövdedeki ile aynı |
| `/api/invitations/redeem` | Player | — |
| `/api/gamesessions/*` | Player (yazma), Player/Therapist (okuma) | Oturumun sahibi; terapist bağlı olduğu çocuğunkini okur |
| `/api/pronunciation/*` | Player | Hız sınırı (çocuk başına dakikada N) |
| `/api/assets/*`, `/api/media/*`, `/api/admin/*`, debug listeleri (`GET /api/players`, `GET /api/tasks`) | Admin | Ya da tamamen kaldırılır |

- **Gövdeden gelen kimlikler** (`therapistId`, `playerId`) artık güvenilmez; claim'den alınır. Gövdede başka bir değer gelirse 403.
- **DTO'lar:** `POST /api/notifications` gibi entity bağlayan yerler DTO'ya geçer (B8). DataAnnotations doğrulaması minimal API'de açıkça çalıştırılır (B33).
- **Swagger:** "Authorize" düğmesi; production'da Swagger kapatılabilir.
- **Hız sınırı:** Giriş endpoint'lerinde IP ve hesap başına (kaba kuvvete karşı).

## 5. Yayın sırası

Her adım bir öncekini bozmadan yayına çıkar:

| # | Adım | Kırıcı mı? | Not |
|---|---|---|---|
| 1 | **PR-13** hash'leme + toplu geçiş | Hayır | İstemci değişmez. Ön koşul yok. |
| 2 | **PR-14** token'lar (`sub` = ID, çocuk token'ı, `token_version`) | Hayır | Fonem token'ı otomatik kaydedip göndermeye başlar. |
| 3 | 🔑 **PR-01** + yeni `Jwt__Key` | Mevcut terapist oturumları düşer (bir kez yeniden giriş) | Render erişimi |
| 4 | **PR-18 / PR-19** güvenli saklama, her istekte token, `X-App-Version` | Hayır | Yeni sürümler cihazlara/mağazaya |
| 5 | **PR-15a** gözlem modu | Hayır | 1-2 hafta trafik izlenir |
| 6 | **PR-15b** zorlama | **Evet**, eski sürümler için | Gözlem logları "token'sız istek ≈ 0" gösterince. Eski sürümler 426 ile "güncelle" ekranı görür. |

Her adım ayrı bir PR'dır. CI ve review'dan geçer. Adım 6 senin açık onayınla yayına çıkar.

## 6. Riskler

| Risk | Önlem |
|---|---|
| Zorlama sonrası güncellenmemiş Fonem'ler çalışmaz | Gözlem modu + `426` "güncelle" ekranı + zorlamayı grup grup açmak |
| Çocuk şifresini hatırlamaz | Uzun ömürlü token + kayan yenileme. Terapistin panelden şifre sıfırlaması (B24 kararıyla birlikte) |
| Toplu hash işi yarıda kalır | İdempotent: yalnızca `password_hash IS NULL AND password IS NOT NULL` satırlarına dokunur |
| `therapist_clients` eksik ya da tutarsız → terapist kendi öğrencisini göremez | PR-15a logları sahiplik uyuşmazlıklarını da gösterir; veri zorlamadan önce düzeltilir. Ayrıca PR-03 şema dökümü |
| Anahtar yenilenmeden zorlama | §2: PR-15b, PR-01 ve yeni anahtar olmadan merge edilmez (PR'a merge koşulu olarak yazılır) |

## 7. Kararlar (2026-10-17, onaylandı)

1. **Hash algoritması:** ASP.NET Core `PasswordHasher`.
2. **Çocuk token'ı:** 30 gün + kayan yenileme.
3. **Şifresiz çocuk hesapları (B24):** Terapist panelinden üretilen kısa giriş kodu. Çocuk e-posta ve şifre bilmek zorunda kalmaz. Ayrıntılı tasarım PR-14 kapsamında.
4. **Yönetici endpoint'leri:** `Admin` rolüyle korunur.
5. **Sıra:** PR-13 ve PR-14 Render erişimi beklenmeden yapılır; zorlama (PR-15b) PR-01 ve yeni `Jwt__Key` olmadan yayına çıkmaz.

### Uygulama notları

- **PR-13 ([BE#13](https://github.com/bitirmesel/BackendApi/pull/13), 2026-10-17):**
  - `password_hash` sütunları eklendi; giriş anında yükseltme ve arka planda backfill çalışıyor.
  - **Düz metin sütunu henüz silinmedi.** Eski `8nfn` servisi aynı veritabanını düz metinle okuyor. Silme geri alınamaz olduğu için ayrı adım: **PR-13b**. Koşul: hash'le girişlerin canlıda doğrulanması ve tercihen `8nfn`'in kapatılması.
