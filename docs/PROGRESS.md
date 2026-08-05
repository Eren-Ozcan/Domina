# Durum Kaydı

Son güncelleme: 2026-08-05

Bu dosya "şu an nerede kaldık" sorusunun cevabıdır. Plan `ROADMAP.md`'de, tasarım
kararları `GDD.md`'de; burada yalnızca **yapılanın ve sıradakinin** anlık fotoğrafı var.

---

## Özet

| Faz | Durum |
| --- | --- |
| Faz 0 — İskele | ✅ Tamam |
| Faz 1 — Simülasyon çekirdeği | 🟡 1.1–1.5 tamam, 1.6 (toplu simülasyon) ve testlerin çoğu eksik |
| Faz 2+ | ⬜ Başlanmadı |

Doğrulama: `dotnet build` → 0 hata / 0 uyarı, `dotnet test` → 6/6 yeşil.

---

## Yapıldı

### Faz 0 — İskele
- .NET 10 SDK, Godot 4.7 .NET sürümü (repoya girmez, `tools/` altında)
- `Directory.Build.props`: `net8.0`, nullable açık, `TreatWarningsAsErrors`
- Çözüm: `Domina.Core`, `Domina.Chat`, `Domina.Sim` + iki test projesi
- Godot projesi (`src/Game`) hem derleniyor hem headless çalışıyor; kendi
  `Directory.Build.props`'u ile kök ayarlarından yalıtıldı
- GitHub Actions: push'ta build + test

### Faz 1.2 — Deterministik RNG
- `IRandomSource` + `SeededRandom` (xoshiro256\*\*)
- **`System.Random` bilerek kullanılmadı:** algoritması .NET sürümleri arasında
  değişebilir, bu da "aynı seed = aynı dövüş" garantisini bozardı
- Testler için `FixedRandom` (scripted RNG)

### Faz 1.1 — Veri modeli
`Warrior` (benzersiz ID + ayrı isim alanı), `WarriorStats`, `Injury`/`Disability`
(`BodyPart` bazlı kalıcı stat modifikatörleri), `Weapon` (kesici/künt, el sayısı),
`Armor`.

### Faz 1.3–1.4 — Dövüş çözümleyici
- Olay akışı (`BattleEvent` hiyerarşisi) — çözümleyici animasyondan tamamen habersiz
- `CombatTuning`: tüm denge sayıları tek dosyada
- Adım adım simülasyon, çok savaşçılı takım desteği
- Kaçış: komut buffer'lama, savunmasızlık penceresi, rakibe fırsat saldırısı
- Ağır darbe sonuç ağacı (hafif / ağır+pes / ağır+müdahalesiz)
- `CombatantSnapshot`: salt okunur dışa bakış; dövüşe tek müdahale noktası
  `Battle.CommandRetreat`

### Faz 1.5 — Onur motoru
`CrowdVerdict` (oran tabanlı ödül çarpanı), `HonorEngine`, `SeppukuArbiter`
(kuyruk, 60 sn oylama penceresi, kullanıcı başına tek oy, af + 15 dk bağışıklık,
sıfır oyda AI kararı).

> Yazım sırasında bulunup düzeltilen hata: `SeppukuArbiter` oylamayı açarken
> savaşçıyı kuyruktan çıkarıyor, sonra sıfır-oy durumunda AI'ın bakacağı onur
> değerini bulamıyordu. Aktif kayıt ayrıca saklanacak şekilde düzeltildi.

---

## Sıradaki iş (Faz 1'in kalanı)

1. **Test kapsamı** — şu an yalnızca determinizm testleri var (6 test).
   Eksikler: uzuv kaybı, pes etme/kaçış penceresi, onur hesabı, seppuku kuyruğu,
   uçtan uca 3v3 dövüş.
2. **Faz 1.6 — toplu simülasyon CLI'ı.** `Domina.Sim/Program.cs` hâlâ iskelet.
   Seed aralığında N dövüş koşup ölüm/sakatlık/kazanma oranlarını CSV'ye yazmalı.
   Denge çalışmasının tamamı buna dayanıyor; Faz 1'de yapılmazsa Faz 9'da acı çekilir.
3. **Kabul kriteri ölçümü** — "10.000 dövüş < 10 saniye" henüz hiç ölçülmedi.

Bunlar bitmeden Faz 2'ye (görselleştirme) geçilmemeli — çekirdek ölçülebilir
olmadan üstüne görsel katman koymak, sonradan denge yapmayı imkânsızlaştırır.
