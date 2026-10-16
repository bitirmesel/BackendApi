## [PR-XX] Başlık

> Yol haritası: [ROADMAP.md](https://github.com/bitirmesel/BackendApi/blob/main/docs/ROADMAP.md) · Mimari: [ARCHITECTURE.md](https://github.com/bitirmesel/BackendApi/blob/main/docs/ARCHITECTURE.md)

> ⚠️ `main`'e merge edilen her değişiklik **anında Render'a deploy edilir.** Kullanımdaki Unity ve Flutter sürümlerini kırmadığından emin ol.

### Ne?
<!-- Bu PR neyi değiştiriyor? Kısa madde işaretleri. -->

### Neden?
<!-- Hangi bulguyu/hatayı çözüyor? ROADMAP kartına ve ARCHITECTURE bölümüne referans ver. -->

### Nasıl? (adımlar)
1.

### API / veritabanı etkisi
- [ ] Endpoint eklendi/değişti → ARCHITECTURE §5.6 / §7 güncellendi
- [ ] Yanıt biçimi değişti → **geriye uyumlu mu?** (yeni alan eklendi, eski alan silinmedi)
- [ ] Migration eklendi → yerel/dev veritabanında denendi (prod'da değil)
- [ ] Yeni ortam değişkeni gerekiyor → aşağıda listelendi

### Merge öncesi / sonrası yapılacaklar 🔑
<!-- Render Environment'a eklenecek değişkenler, panel ayarları, merge sırası vb. Yoksa "Yok". -->

### Nasıl test edildi?
- [ ] `dotnet build` hatasız
- [ ] Swagger'da ilgili endpoint'ler denendi (istek/yanıt örneği ekle)
- [ ] Kabul kriterleri (ROADMAP kartındaki) tek tek denendi
- [ ] Merge sonrası canlıda doğrulandı

### Riskler ve geri alma
<!-- Neyi kırabilir? Geri almak için ne yapılır (revert yeterli mi, migration geri alınmalı mı)? -->

### İlgili PR'lar
<!-- Örn: bitirmesel/GraduationProject#5 — bu PR'dan sonra merge edilmeli -->

### Kontrol listesi
- [ ] Dal adı `feature/PR-XX-...` veya `fix/PR-XX-...`
- [ ] Repoya sır (şifre, anahtar, connection string) eklenmedi
- [ ] Entity doğrudan dönülmüyor, DTO kullanılıyor
- [ ] ROADMAP.md durum sütunu güncellendi
