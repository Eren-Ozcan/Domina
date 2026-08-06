# Durum Kaydı

Son güncelleme: 2026-08-06 (Faz 2 omurgası)

Bu dosya "şu an nerede kaldık" sorusunun cevabıdır. Plan `ROADMAP.md`'de, tasarım
kararları `GDD.md`'de; burada yalnızca **yapılanın ve sıradakinin** anlık fotoğrafı var.

---

## Özet

| Faz | Durum |
| --- | --- |
| Faz 0 — İskele | ✅ Tamam |
| Faz 1 — Simülasyon çekirdeği | ✅ Tamam (kabul kriterleri ölçüldü) |
| Faz 2 — Dövüş görselleştirme | 🟡 Omurga tamam, sanat stil kararına bağlı |
| Faz 3+ | ⬜ Başlanmadı |

Doğrulama: `dotnet build` → 0 hata / 0 uyarı, `dotnet test` → 114/114 yeşil.
Godot projesi ayrı derleniyor: `dotnet build src/Game/Domina.Game.csproj`.

---

## Yapıldı

### Faz 0 — İskele
- .NET 10 SDK, Godot 4.7 .NET sürümü (repoya girmez, `tools/` altında)
- `Directory.Build.props`: `net8.0`, nullable açık, `TreatWarningsAsErrors`
- Çözüm: `Domina.Core`, `Domina.Chat`, `Domina.Sim` + üç test projesi
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
> `SeppukuTests.TheArtificialAudienceJudgesTheRightWarriorsHonor` bu hatanın
> geri gelmemesi için var.

### Faz 1.6 — Toplu simülasyon aracı
`Domina.Sim` artık çalışan bir CLI: seed aralığında N dövüş koşturur, dövüş başına
CSV satırı yazar ve oranları özetler.

```bash
dotnet run --project src/Domina.Sim -c Release -- --scenario 3v3 --battles 10000 --policy below:0.3 --out sonuc.csv
```

- Senaryolar kodda sabit (`duel`, `3v3`, `veteran`, `ambush`) — iki ölçüm aynı
  kadroyu karşılaştırsın diye
- `--policy` oyuncunun "çek" tuşunun yerine geçer. **Uzuv kaybı yalnızca zamanında
  müdahale edilen dövüşlerde oluştuğu için bu şart:** `never` ile koşulan bir parti
  hiç sakat savaşçı üretmez, yalnızca ölü üretir
- Oranların paydası dövüş sayısı değil sahaya çıkan savaşçı sayısı (3v3'te bir
  dövüşte üç savaşçı ölebilir)

### Faz 1 testleri
6 testten **112**'ye çıktı:

| Dosya | Kapsam |
| --- | --- |
| `DeterminismTests` | aynı seed = aynı dövüş, aynı olay akışı |
| `DismembermentTests` | ağır darbe sonuç ağacı, silah/zırh etkisi, kalıcı sakatlık |
| `RetreatTests` | komut buffer'lama, savunmasızlık penceresi, fırsat saldırısı |
| `HonorTests` | performans/chat/hedefli oy etkileri, ödül çarpanı, decay |
| `SeppukuTests` | kuyruk, tek oy kuralı, beraberlik, af bağışıklığı, AI kararı |
| `BattleFlowTests` | uçtan uca 3v3, olay akışı ↔ özet tutarlılığı, süre limiti |
| `ThroughputTests` | 10.000 dövüş bütçesi, dövüş başına ayırma |
| `BatchRunnerTests` / `SimCliTests` | toplama doğruluğu, argüman ayrıştırma, CSV |

### Kabul kriteri ölçümü
"10.000 dövüş < 10 saniye" **ölçüldü ve geçildi**: Release'te ~1 sn
(≈10.000 dövüş/sn), Debug'da ~2 sn.

> Yol boyunca bulunan sorun: `Battle.FindTarget` ve `CountActive` her tick'te her
> savaşçı için LINQ lambda'sı kuruyordu — dövüş başına ~279 KB ayırma. Düz döngüye
> çevrildi; sonuçlar birebir aynı kaldı, hız iki katına çıktı (4.200 → 10.000
> dövüş/sn). `ThroughputTests.PerBattleAllocationStaysSmallWithoutEvents` bunun
> geri gelmesini engelliyor.

---

## Faz 2 — omurga (2026-08-06)

Dövüş artık ekranda izleniyor. Sanat **kasıtlı olarak geçici**: her uzuv düz renkli
bir çubuk, yani stickman. Stil kararı verilmeden gerçek sanata girmemek için.

```bash
dotnet build src/Game/Domina.Game.csproj
tools/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64.exe --path src/Game -- --seed 81
```

### Kilitlenen rig (değiştirmesi pahalı)
15 parça, kök ayak hizasında, karakter 256 px:

```
Root → Hip → Torso → Head
                   → Arm_Upper → Arm_Fore → Arm_Hand → Weapon   (×2)
            → Leg_Thigh → Leg_Shin → Leg_Foot                   (×2)
```

Kopma noktaları **omuz** ve **kalça**. Sanat değiştiğinde her kemiğe asılı çizim
değişir, hiyerarşi aynı kalır — ama parça listesi veya oranlar değişirse tüm
animasyonlar yeniden yapılır.

> **Skeleton2D + Bone2D kullanılmadı** (ROADMAP'te öyle yazıyordu). Bone2D mesh
> deformasyonu içindir; bizim ihtiyacımız uzvu deforme etmek değil koparmak. Düz
> `Node2D` hiyerarşisinde kopma = düğümü zincirinden ayırmak, ki GDD §2'nin tarif
> ettiği şey tam olarak bu. Kolunu kaybeden savaşçının silahı da zincirle birlikte
> gidiyor — çekirdekteki `UsableWeapon` kuralı görselde bedava geliyor.

### Çalışan şeyler
- Dövüş **gerçek zamanla adımlanıyor**, kayıt oynatılmıyor — oyuncu dövüş sürerken
  müdahale edebildiği için karar canlı simülasyona işlemek zorunda
- Görsel iki kanaldan sürülüyor: sürekli hal anlık görüntülerden, anlık tepkiler
  (sarsıntı, uzuv kopması, ölüm) olay akışından
- **Tek "ÇEK" tuşu, ekibin tamamını çeker.** Tuş kaç savaşçının etkileneceğini ve
  kaçının vuruşa kilitli olduğunu gösteriyor: *"EKİBİ ÇEK (3) · 3 kilitli"*.
  Kilitli olanların kaçışı vuruş bitince başlar, bu gecikme sürpriz olmamalı
- Hamle sabit değil hedefe kadar: çekirdek herkesi aynı ön sıradaki düşmana
  yönelttiği için sabit hamle arkadaki savaşçıları boşluğa kılıç sallatıyordu

### Tasarım değişikliği: pes etme ekip bazlı oldu

GDD §5 önce "her savaşçı ayrı ayrı çekilebilir" diyordu; **değiştirildi**. Artık tek
komut sahadaki 1-3 savaşçının hepsini çeker ve hepsi onur kaybeder.

Gerekçe: savaşçı bazlı olsaydı doğru oynanış "yara alanı hemen çek, kalanla devam et"
olurdu — kayıpsız, sürekli tekrarlanan küçük bir optimizasyon. Ekip bazlı komut kararı
nadir ve ağır yapıyor.

Etkisi ölçüldü (3v3, 10.000 dövüş, `--policy below:0.3`):

| | Savaşçı bazlı | Ekip bazlı |
| --- | --- | --- |
| Zafer | %61.7 | **%5.1** |
| Ölüm | %56.7 | %32.0 |
| Kaçış | %8.2 | %63.3 |
| Uzuv kaybı | %1.15 | %2.50 |

Yani "canı %30'a düşeni çek" politikası artık neredeyse her dövüşü terk etmek demek —
tam olarak amaçlanan şey. Uzuv kaybının iki katına çıkması da beklenen: müdahale
herkesi ölüm yerine sakatlıkla kurtarıyor.

> Bu, `Domina.Sim`'deki politika eşiklerinin **anlamını değiştirdi**. Faz 9'da denge
> bakılırken `below:0.3` artık "temkinli oyuncu" değil "ilk zorlukta kaçan oyuncu"
> demek; anlamlı karşılaştırma için çok daha düşük eşikler gerekecek.

### Çekirdeğe eklenenler
`CombatantSnapshot`'a `StateProgress` (0-1) ve `CanCancel` eklendi; `Combatant`
durum geçişlerini artık `BeginState` üzerinden yapıyor. Animasyonu kesme
penceresiyle eşleştirmek bunlarsız mümkün değildi. Davranış değişmedi —
determinizm testleri dahil hepsi yeşil kaldı.

### Doğrulama
- Aynı seed (20260806) hem arenada hem `Domina.Sim`'de **15,2 sn / PlayerVictory**
  → görselleştirme simülasyonu bozmuyor
- Toplu simülasyonun "seed 52'de uzuv kaybı var" dediği dövüş arenada da uzuv
  kaybıyla sonuçlanıyor; kopan kol silahıyla birlikte yere düşüyor

> Doğrulama sırasında bulunup düzeltilen iki hata: kopan uzuv sahne
> koordinatlarında zemini yanlış hesaplayıp havada asılı kalıyordu; ölmüş bir
> savaşçının tuşu hâlâ "ÇEKİLİYOR" yazıyordu (komut işliyormuş gibi okunuyordu).

---

## Sıradaki iş

**Görsel stil kararı** (GDD Açık Karar #6). Faz 2.2'nin tamamı buna bağlı ve
başka hiçbir iş bunu beklemiyor — omurga bitti.

Değerlendirme sırasında ölçüt güzellik değil üretilebilirlik: siluet okunuyor mu,
uzuvlara bölünüp döndürülünce dağılıyor mu, 128 px'te okunuyor mu, ve dört zırh
kademesi birbirinden ayırt ediliyor mu.

> Piksel sanat seçilirse bu bir sanat değişimi değil **mimari değişim** olur:
> piksel ızgarası kemik döndürmede bozulur, kalıcı uzuv kaybı kombinatoryal
> sprite üretimine geri döner (GDD §2 bunu zaten elemişti).

### Akılda tutulacaklar
- Denge sayıları **kasıtlı olarak ham**. İlk ölçüm 3v3'te oyuncu ölüm oranını
  %45-57 gösteriyor; bu Faz 9'un işi, şimdi ayarlanmayacak (bkz. ROADMAP riski).
- `Domina.Chat` hâlâ boş iskele (Faz 5). Test projesi de boş — `dotnet test`
  "no test is available" uyarısı buradan geliyor, hata değil.
- Dövüş savaşçıların kalıcı halini değiştirmiyor; ölüm/sakatlığı kalıcı hale
  işlemek **meta katmanın** işi ve henüz yazılmadı (Faz 3). Arenadaki kadro da
  geçici — gerçek roster Faz 3'te gelecek.
