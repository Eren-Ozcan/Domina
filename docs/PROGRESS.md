# Durum Kaydı

Son güncelleme: 2026-09-03 (kılıç yakalama çekirdeğe girdi; #4-B'de yalnızca zehir ve silah kırılması kaldı)

Bu dosya "şu an nerede kaldık" sorusunun cevabıdır. Plan `ROADMAP.md`'de, tasarım
kararları `GDD.md`'de; burada yalnızca **yapılanın ve sıradakinin** anlık fotoğrafı var.

---

## Özet

| Faz | Durum |
| --- | --- |
| Faz 0 — İskele | ✅ Tamam |
| Faz 1 — Simülasyon çekirdeği | ✅ Tamam (kabul kriterleri ölçüldü) |
| Faz 2.1 — Görselleştirme omurgası | ✅ Tamam (kabul kriteri testle bağlandı) |
| Faz 2.2 — Sanat ve cila | ⬜ Görsel stil kararına bağlı |
| Faz 3+ | ⬜ Başlanmadı |

Doğrulama: `dotnet build` → 0 hata / 0 uyarı, `dotnet test -c Release` → 245/245 yeşil.
Godot projesi ayrı derleniyor: `dotnet build src/Game/Domina.Game.csproj`.

> `dotnet format --verify-no-changes` **temiz değil**: 6 adet IDE1006 (`_` öneki)
> uyarısı var — `HudModel.cs` ve `ArmorWeightTests.cs`. Eski bir borç, bu turda
> oluşmadı; sayı 2026-09-03'te değişmedi.

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

## Faz 2.1 — kapanış (2026-08-12)

Omurga "çalışıyor"dan "bitti"ye çekildi. İki iş yapıldı: **sunum mantığı motordan
ayrıldı ve testle bağlandı**, ardından ortaya çıkan boşluklar kapatıldı.

### Yeni katman: `Domina.Presentation`

Godot'a bağımlı olmayan bir kütüphane. İçinde dövüşün ekranda nasıl göründüğüne dair
kararlar var; Godot'un `Vector2`'si yerine kendi `ScenePoint`'i kullanılıyor.

| Tip | İşi |
| --- | --- |
| `ArenaLayout` / `ArenaChoreography` | kim nerede durur, hamle nereye gider, ceset nerede kalır |
| `ReactionReader` | olay akışı → tek seferlik görsel tepkiler |
| `RigAnimator` / `RigPose` | durum + tepki → 14 kemik açısı |
| `HudModel` | tuşta ve panelde ne yazar |
| `DemoRoster` / `ArenaArguments` | geçici kadro, `--seed` / `--speed` |

`src/Game` artık ince: düğümleri kurar, gelen açıyı uygular, kopan zinciri ayırır.

> **Neden ayrı proje:** motor açmadan test edilemeyen karar hiç test edilmez. Faz 2
> tek testsiz fazdı; şimdi `ArenaPlaybackTests` dövüşü `BattleArena._Process` ile aynı
> sırayla oynatıp bilançodaki uzuv kaybının ekrandaki kopmayla **aynı uzuv** üzerinden
> tuttuğunu sınıyor — yani Faz 2'nin kabul kriteri artık bir cümle değil bir test.
> Motorsuz koşabiliyor olması ayrımın da canlı kanıtı: `src/Game`'e bir sızıntı olursa
> bu testler derlenmez.

### Kapatılan boşluklar

Ayrım netleşince görülen eksikler:

- **Saldırının üç sonucu ekranda aynı görünüyordu.** 13 olay türünden yalnızca 3'ü
  görsele bağlıydı; ıska ve kaçınmanın karşılığı yoktu. Artık ıska savurma
  (`Overswing`), kaçınma yana kaçış üretiyor. Kaçınma stamina harcayan bir çekirdek
  mekaniği — ekranda karşılığı yoksa oyuncu staminanın nereye gittiğini göremez.
- **Fırsat saldırısının işareti yoktu.** Kaçışın bedeli normal vuruştan ayırt
  edilemiyordu ("tuşa bastım, sonra canım gitti"). Çekirdekte bir duruma karşılık
  gelmediği için — kaçan avın arkasından anında çözülür — boşta bekleyen savaşçının
  duruşunu geçici olarak devralan ayrı bir vuruş eklendi.
- **Ölen savaşçı hattına ışınlanıyordu.** Konum yalnızca duruma bakılarak
  hesaplanıyordu; hamlenin ortasında ölen geri sıçrıyordu. Koreografi artık ölüm
  öncesi kareyi hatırlıyor.
- **Kaçan savaşçı arenanın ortasında yok oluyordu.** Kaçış mesafesi sabit 460 px'ti,
  kadraj 1920 px. Mesafe artık kadrajdan hesaplanıyor.
- **Bacağını kaybeden savaşçı kaçarken topallamıyordu.** Kaçış duruşu sakatlık
  katmanından geçmiyordu: kalça son topallama değerinde asılı kalıyor, sağlam bacak
  normal koşu döngüsünü oynatıyordu.
- **Buffer'lanmış komut panelde görünmüyordu.** Tuş basmadan önce "3 kilitli" diyordu
  ama bastıktan sonra panel hâlâ "saldırıyor" yazıyordu — komut yutulmuş gibi.
- **Kopan uzuv yerde yatarken hâlâ animasyon alıyordu.** Zincir ayrıldıktan sonra da
  referans duruyordu, duruş her karede yerdeki kola da uygulanıyordu; düşme dönüşü
  bu yüzden hiç görünmüyordu.
- **Acı parlaması takım rengini ikinci kez çarpıyordu.** Uzuvlar zaten renkliydi;
  `Modulate`'e takım rengi basmak savaşçıyı kızartmak yerine karartıyordu.

### Doğrulama

- 57 yeni test (toplam 173), `dotnet format` temiz, Godot katmanı ayrı derleniyor
- Motorlu/motorsuz karşılaştırma birebir: seed 20260806 → **PlayerVictory / 15,2 sn**,
  seed 81 → **PlayerDefeat / 32,0 sn** — ikisi de hem arenada hem motorsuz oynatmada
- Testler seed sabitlemiyor, aradıkları durumu (ölüm, kaçış, uzuv kaybı) üreten ilk
  seed'i tarayarak buluyor. Faz 9'da denge sayıları değiştiğinde sabit bir seed sessizce
  anlamını yitirirdi: test yeşil kalır ama artık bir şey sınamazdı.

---

## Motor doğrulaması (2026-08-13)

Uzam değişikliğinden sonra Godot katmanı çalıştırıldı:

```bash
dotnet build src/Game/Domina.Game.csproj                       # 0 hata / 0 uyarı
tools/Godot_v4.7-stable_mono_win64/..._console.exe --headless --path src/Game -- --seed 81
```

Headless koşu sorunsuz tamamlandı ve **motorlu/motorsuz karşılaştırma tuttu**:
seed 81 → hem arenada hem `Domina.Sim`'de **PlayerWipe / 18,8 sn**. Yani uzam
çekirdeğe girerken mimari kural (görselleştirme simülasyonu etkilemez) korundu.

> **Yapılmayan iş — gözle kontrol.** Pencere açıldı ama dövüş izlenmedi. İlk kez
> ekranda olacak beş şey doğrulanmayı bekliyor: gerçek yürüme, derinliğin dikey
> kayma + ölçek + çizim sırasıyla okunması, menzil dışından kılıç sallanmaması,
> hedef değişince yön dönmesi, ve hedef uzaklaşınca ıska. Sonraki oturumda
> `--seed 81 --speed 1` ile bakılacak.

---

## Uzuv kaybı oranı — karara bağlandı (2026-08-14)

Bekleyen soru kapandı, ama beklenenden farklı bir yerden. Hedef "%5" olarak konmuştu;
kullanıcı bu sayının keyfî olduğunu ve oranın **kuşamın fonksiyonu** olması gerektiğini
söyledi — iyi zırhlı savaşçı eve sağlam dönmeli. Bu doğru çıktı ve mekanizma zaten
kısmen kodda duruyordu, yanlış eksene bağlıydı.

### Zırh yuva yuva oldu

`Armor` artık tek skaler değil, **kafa/gövde/kol/bacak** için ayrı parçalar
(`ArmorPiece`). Hasar azaltımı ve kopma direnci darbenin indiği bölgeden okunuyor.
İsabet bölgeleri zaten bunun için vardı — `CombatTuning`'deki yorum sebebini yazıyordu
ama karşılığı yoktu.

Ölçüm için `--armor none|light|medium|heavy` eklendi (senaryonun geri kalanı sabitken
zırh eksenini izole eder) ve rapor artık uzuvları **parça parça** sayıyor.

### İki mantık hatası bulundu

**1. Uzuv kaybederek kazanmak imkânsızdı.** Kopma `PlayerIntervened` istiyordu, o bayrağı
yalnızca "Kaç" tuşu açıyordu, tuş da §5 gereği seferi bitiriyordu. 20.000 dövüş, en kötü
zırh, **zafer + uzuv kaybı: 0 kez**. Sakat dönen her savaşçı terk edilmiş bir seferin
anıtıydı; tek kollu bir şampiyon eve asla gelemiyordu.

Sonuç ağacı ikiye ayrıldı (GDD §7 güncellendi): **öldürmeyen** ağır darbe tuşsuz da
koparır ve dövüş sürer; **öldürücü** darbede eski kural geçerli — tuşa basılmışsa uzuvla
yaşar, basılmamışsa ölür.

> **Yazarken düzeltilen tarif.** Bu ilk önce "tuş ölümü uzuv kaybına çevirir" diye
> yazılmıştı; kullanıcı bunun ağacın tek dalı olduğunu söyledi. Tuş bir **takas değil** —
> sonucu basıldığı anda bilinmeyen bir çekilme başlatıyor ve bedeli bir merdivene düşüyor:
> herkes yarasız kaçtı → herkes yaralı kaçtı → uzuv kayıplı kaçtı → ekibin bir kısmı
> kaçtı → kimse kaçamadı. Merdiven ölçüldü ve GDD §5'e işlendi.
>
> Ölçüm için `--policy at:<saniye>` eklendi: cana bakan politikalar merdivenin bedelsiz
> ucunu **kurgu gereği** ölçemiyordu (can düşmüşse zaten yara alınmıştır). `at:0` ile
> görüldü ki temastan önce basmak **%100 yarasız** çıkış veriyor.

### Kaçış bedelsiz olmaktan çıktı (2026-08-14)

Kullanıcı "%100 olmamalı" dedi. Sebep tek değil üçtü ve üçü birden düzeltildi.

| Neden bedelsizdi | Ne eklendi |
|---|---|
| `MoveSpeed` tek sabitti — kovalayan ile kaçan aynı hızda, net kapanma sıfır | `WarriorStats.Speed` (0-100). Oni 25, Kappa 55, Tengu 85. Kaçan ayrıca `RetreatSpeedMultiplier` kadar yavaşlar. Bacak kaybı hızı da düşürür (`Disability.SpeedMultiplier`) |
| Yakın dövüş arenanın uzak yarısına ulaşmıyordu | `ThrownWeapon` + `Projectile`: mermi havada süre geçirir, varışta çözülür. Yeni olaylar `ProjectileLaunched/Hit/Missed`; sunum tarafında `ProjectileView`/`ProjectileTracker` |
| Arenayı terk etmenin kendisi bedava | `EscapeMishapChance` (0.30): çıkışta kaza yarası. Canı **1'in altına indirmez** — amacı ölüm değil, bedelsizliği kaldırmak |

**Yazarken bulunan asıl sorun:** hız eklenince avcı yetişiyor ama **hiç vuramıyordu**.
Kural "hamleye kilitlenen yürüyemez" idi; avcı yetişip savuruyor, hamle boyunca donuyor,
kaçan menzilden çıkıyor, kılıç her seferinde boşluğa iniyordu. Kovalamaca için kural
askıya alındı: kovalayan **hamle sırasında** koşmaya devam eder. Toparlanmada değil —
ikisi de açıkken kovalayan hiç durmuyor ve kaçış tamamen çöküyordu (ekiplerin %76'sı
kırılıyordu, %30 yerine). `RetreatSpeedMultiplier` 0.85/0.92/1.0 tarandı, **0.92** seçildi.

Yeni merdiven (3v3, 20.000 dövüş, hafif kuşam):

| Basamak | Temasta önce | 2. sn | Can %50 | Sayıca geri kalınca |
|---|---|---|---|---|
| 1 · hepsi kaçtı, yarasız | %35.0 | — | — | — |
| 2 · hepsi kaçtı, yaralı | %65.0 | %87.1 | %6.8 | — |
| 3 · hepsi kaçtı, uzuv kayıplı | — | %7.4 | %2.3 | — |
| 4 · kısmi kaçış, uzuvsuz | — | %4.9 | %71.8 | %29.6 |
| 5 · kısmi kaçış, uzuv kayıplı | — | %0.6 | %17.6 | %9.3 |
| 6 · kimse kaçamadı | — | — | %1.5 | %61.1 |

> **Uzuv kaybı bandı bozulmadı** — zırhın belirlediği oranlar neredeyse aynı kaldı
> (zırhsız %8.6, hafif %6.6, orta %2.9, ağır %0.4). Değişen ölüm ve kaçış: kaçmak artık
> pahalı, o yüzden ölüm %41.6'dan %49.1'e çıktı, kaçış %8.7'den %4.1'e indi.

Bu geçişte GDD Açık Karar **#4-C kapandı** (fırlatma hattı §4'te artık "Aktif") ve §5'e
merdiven ile üç mekanik işlendi. Yeni test dosyası `RangedAndFlightTests`. Toplam 190 test.

**2. Aynı uzuv birden çok kez kopabiliyordu.** `_lostParts` savaşçı başına tek `BodyPart`
tutuyordu ve her yeni kayıp öncekini siliyordu; `AlreadyLost` yalnızca sonuncusuna baktığı
için kolunu kaybeden savaşçı bacağını kaybedince "kolu duruyor" sayılıyordu. Ölçüldü:
tek savaşçıda **22 kopma**, `[Kol, Bacak, Kol, Bacak, ...]`. Kayıt artık `BodyPartSet` —
tekrarsızlığı tipin kendisi taşıyor, üstelik değer eşitliği olduğu için özet record'ları
determinizm testlerinde karşılaştırılabilir kalıyor.

Aynı hata, birden fazla uzvunu kaybeden savaşçının fazladan kayıplarını da yutuyordu:
zırhsız 3v3'te 5440 sakat savaşçıya karşılık **5830 kopan uzuv** var.

### Ölçülen band (3v3, 20.000 dövüş, `losing:0.7`)

| Kuşam | Ölüm | **Uzuv kaybı** | Zafer |
|---|---|---|---|
| Zırhsız | %41.6 | **%8.6** | %68 |
| Hafif keikogi | %38.8 | **%6.7** | %70 |
| Dō-maru | %22.5 | **%2.9** | %89 |
| Ō-yoroi | %16.3 | **%0.4** | %96 |

`BaseDismembermentChance` **0.35 → 0.05**. Eski değer, kopmanın yalnızca kaçış
penceresinde ateşlendiği ağaca göre ayarlanmıştı; yeni ağaçta aynı sayı uzuv kaybını
%45'e çıkarıyordu. 0.35/0.15/0.08/0.05/0.03 tarandı — knob ölüm ve zafer oranlarını
kayda değer biçimde oynatmıyor, yalnızca uzuv kaybını ölçekliyor.

> **Tuş artık oranı belirlemiyor, ölümü belirliyor.** Aynı kuşamda hiç çekilmeyen ile
> erken çeken oyuncunun uzuv kaybı neredeyse aynı (%8.8'e karşı %8.0); ölüm %48'den
> %29'a düşüyor. Kazanılan dövüşlerin **%16.5'i** eve sakat bir savaşçı getiriyor.

Testler: `TestBuilders.PointBlank` artık `BaseDismembermentChance`'i **sabitliyor** —
sonuç ağacını sınayan testler kuralı sınıyor, dengeyi değil; denge sayısı devralınsaydı
her ayarda kırılırlardı. Toplam 180 test yeşil.

### Bundan geriye kalan

Önceki oturumun dört seçeneğinden **C · künt silah kalıcı yaralanma yapsın** hâlâ açık
(kullanıcı "sonraki tura" dedi). GDD §7 "künt → kırık/sersemleme" vaat ediyor, çekirdekte
karşılığı yok; tetsubo'nun kopma çarpanı 0.15 olduğu için ağır zırhlı savaşçıya karşı
neredeyse hiçbir kalıcı etkisi olmuyor. B/D seçenekleri artık gereksiz — oranı
yükseltmek için konmuşlardı.

---

## Sersemletme çekirdeğe girdi (2026-09-02)

Açık Karar **#4-B'nin ilk maddesi kapandı**: künt silahın sersemletme etkisi artık
çekirdekte. GDD §7'nin "kod ile fark" notu silindi, yerine kural ve sayı tablosu geldi.

Kural: aynı ağır darbe iki zar attırır — uzuv kopma ve sersemletme. Sersemleyen savaşçı
0.9 saniye donar; yürümez, vurmaz, **kaçınamaz**.

### Ölçüm — takas nerede dönüyor

İki yeni senaryo eklendi (`blade` / `club`): aynı savaşçı, aynı düşman, **yalnızca silah
sınıfı farklı** (Nodachi 34/1.60 karşısında Tetsubo 30/1.55). Ayrım bu kadar dar olmasa
"künt silah işe yaradı" cümlesi statlardan mı silahtan mı geliyor ayrışmazdı.

| Taban şans | Kesici zafer | Künt zafer |
| --- | --- | --- |
| 0 (kural yok) | %91.57 | %88.68 |
| 0.15 | %91.89 | %90.46 |
| **0.35** | **%92.06** | **%92.08** |
| 0.60 | %92.30 | %93.83 |
| 1.00 | %92.70 | %95.67 |

> Kural yokken künt silah **her eksende** kötüydü: kopma çarpanında kaybediyor
> (0.15'e karşı 1.0), karşılığında hiçbir şey almıyordu. 0.35 iki sınıfı başa baş
> getiriyor; ondan sonrası künt lehine bozuluyor.

Eşik de tarandı ve 0.20'de bırakıldı: 0.30'da sersemletme neredeyse hiç ateşlenmiyor ve
künt yine geriye düşüyor (%89.07'ye karşı %91.55) — yani sorun aynen geri geliyor.
0.10'da **kesici silah da** sersemletmeye başlıyor ve iki taraf birden zayıflıyor.

### Sürede beklenmedik sonuç

0.5 ile 0.9 saniye arasında **hiçbir fark yok** (%92.06 / %92.08). Sebep: bu bantta
sersemleme çoğunlukla savaşçının zaten beklemekte olduğu boşluğa denk geliyor. Kuralın
ısıran tarafı kaybedilen hamle değil, **kapanan kaçınma**. Diş 1.0 saniyenin üstünde
çıkıyor — 1.4'te künt %94.16'ya fırlıyor. 0.9 o eşiğin hemen altında ve ekranda
okunacak kadar uzun olduğu için seçildi.

### 4-D'nin kademe takası ayakta kaldı

Zırh sersemletmeyi de damperliyor ama kopma direncinin tamamıyla değil, **0.6 payıyla**
(künt kuvvet plakanın altından geçer). Ölçüldü (3v3, 20.000 dövüş): pay 0'da savaşçı
başına 0.51, 0.6'da 0.33, 1.0'da 0.22 sersemleme. 0.6'da kademeler hâlâ bir şeyde en
iyi: dō-maru daha az ölüm (%43.78'e karşı %44.15), ō-yoroi daha az uzuv kaybı
(%0.83'e karşı %3.43).

### Bedeli oyuncu da ödüyor

3v3'te Oni'nin tetsubo'su artık ısırıyor: oyuncu zaferi %69.31'den **%65.20**'ye,
savaşçı başına yenen sersemleme 0.39'a çıktı. Mutlak denge Faz 9'un işi; bu turda
tutulan şey sınıflar arası **oran**.

### İki koruma kuralı

- **Çekilen savaşçı sersemlemez** — yoksa künt silahlı düşman §5'in tek müdahalesini
  tek zarla iptal ederdi
- **Sersemleyen tekrar sersemlemez**, süre yenilenmez. Süre bitince buffer'lanmış "Kaç"
  komutu işlenir: sersemletme komutu **yutmaz**, geciktirir

Yeni test dosyası: `StunTests` (9 test). Toplam 233 test yeşil.

**4-B'de açık kalanlar:** zehir, jitte/sai ile kılıç yakalama, silah kırılması.

---

## Kılıç yakalama çekirdeğe girdi (2026-09-03)

Açık Karar **#4-B'nin ikinci maddesi kapandı**: jitte ve sai artık GDD §4'ün kalkanı
reddederken bıraktığı boşluğu dolduruyor. Kural ve sayı tablosu §7'de.

Kural: yakalama **kaçınmadan önce** denenen ikinci savunma eksenidir. Kaçınma darbeyi
ıskalatır ve orada biter; yakalama darbeyi siler **ve** saldıranı 0.6 sn kilitler —
kilitli savaşçı yürümez, vurmaz, kaçınamaz. Zar savunanın kavrayışından, saldıranın
silahının yakalanabilirliğinden ve savunanın **İsabet**'inden beslenir.

### Ölçüm — takas nerede dönüyor

Üç yeni senaryo (`katana` / `jitte` / `sai`): aynı savaşçı, aynı düşman, üçü de **tek
el**, yalnızca silah farklı. El sayısı sabit tutuldu ki ölçülen şey silah sınıfı değil
yakalama olsun.

| Silah | Zafer | Uzuv kaybı | Yakalama/dövüş |
| --- | --- | --- | --- |
| Katana (kontrol) | %73.09 | %0.90 | 0.00 |
| Jitte | %72.63 | %0.52 | 2.75 |
| Sai | %72.73 | %0.45 | 3.71 |

Taban şans tarandı: 0.15'te jitte %61.16, 0.20'de %68.53, 0.30'da %77.22. **0.24**
takasın döndüğü yer — katana zaferde önde kalıyor, yakalama aletleri eve sakat
dönmemeyi alıyor. 4-D'nin "her seçenek bir şeyde en iyi" deseni ayakta.

### Ağır silah yakalamanın cevabı

İki senaryo daha (`jitte-heavy` / `katana-heavy`): düşman çift el nodachi taşıyor.
Jitte %29.37, katana %34.78 kazanıyor ve jitte uzuv korumasını da kaybediyor (%11.77'ye
karşı %11.42) — nodachi'ye karşı jitte düpedüz yanlış seçim. Kaldıraç çarpanı 0.5'te
delik 14 puana çıkıyordu (%21.04); **0.75** tuzağı bir tercihe indiriyor.

### Kilit süresi yine ölçülmedi — ama sebebi başka

Sersemletme süresindeki bulgunun aynısı: 1v1'de 0 sn ile 1.2 sn arası yalnızca
%72.44 → %73.72. Açılan pencere savaşçının zaten beklediği boşluğa denk geliyor;
kuralın ısırdığı yer **silinen vuruş**.

Kilidin takım değerini ölçmek için `3v3-jitte` eklendi ve orada da ayrışmadı — ama
sebebi farklı: jitte taşıyan acemi dövüş başına yalnızca **~0.57 yakalanabilir vuruş**
görüyor (tavan `--catch-chance 1.0` ile ölçüldü, 0.19/savaşçı). Kalabalıkta hedef
bölünüyor, Tengu mermi atıyor, Oni'nin tetsubo'su zaten zor yakalanıyor. **Kilidin takım
değeri hâlâ ölçülmemiş bir soru.** 0.6 sn, ekranda okunacak kadar uzun ve takasın
döndüğü yerin altında olduğu için seçildi.

### Stamina bedeli beklenmedik yerden ısırıyor

Bedel 0'da zafer %76.85, 16'da %72.63 — ama **yakalama sayısı ikisinde de aynı**
(2.72 / 2.75). Yani bedel yakalamayı seyrekleştirmiyor, savaşçıyı **yoruyor**: stamina
saldırıyı ve kaçınmayı besliyor. 8'de hiç bağlamıyor, 30'da yıkıcı (%41.07).

### Sai'nin ilk sayıları yanlıştı

13/1.05 ile başladı ve düpedüz kötüydü (%61.92): fazladan kavrayış kaybedilen hasarı
ödemiyordu. 14/1.05'te üçü de yarım puan içinde. Jitte ile farkı hasar değil **hacim** —
sai daha çok yakalar, yani kalabalığa karşı daha çok işi olmalı. **Bu kuşatma ölçümü
yapılmadı.**

Yeni durum: `CombatState.WeaponBound` — sersemlemeden ayrı tutuldu, çünkü sebebi de
ekrandaki görüntüsü de ayrı (sersemleyen çöker ve salınır, yakalanan gergin durur).
Yeni test dosyası: `WeaponCatchTests` (12 test). Toplam 245 test yeşil.

**4-B'de açık kalanlar:** zehir, silah kırılması.

> **Not:** `ThroughputTests` Debug'da bütçenin (10 sn) sınırında duruyor ve tüm süit
> birlikte koşarken düşebiliyor. Değişiklikten bağımsız: izole koşuda üçer kez
> ölçüldü, değişiklikten önce de sonra da 7-9 sn. Release'te 2 sn. Bütçe ya
> yükseltilmeli ya da test Release'e bağlanmalı.

---

## Sıradaki iş

**Faz 2.2 — sanat üretimi.** Stil kararı verildi, önünde engel kalmadı.

### Görsel stil — karara bağlandı (2026-08-13)

**Karanlık Edo ahşap baskı × katmanlı kâğıt tiyatrosu.** Tam kural GDD §12'de.

Süreç: beş aday stil için aynı sahne üretildi (dojo, antrenman alanı, silah atölyesi,
strateji odası), sonra kazanan yönde parça ayrılmış karakter sayfası istendi.

| Aday | Sonuç |
|---|---|
| Sumi-e | **Elendi.** Gri yıkama üstünde gri figür — savaşçı zeminden ayrılmıyor, dört zırh kademesi monokromda okunmuyor |
| Ukiyo-e | Çalışıyor ama arka plan figür kadar kontrastlı; dövüş sahnesinde savaşçıyı yer |
| Ahşap baskı + kâğıt tiyatrosu | **Seçildi** |
| Gotik boyama / temiz vektör | Değerlendirildi, kimlik veya maliyet gerekçesiyle geçildi |

Seçim gerekçesi estetik değil: kâğıt tiyatrosunda değer boşluğu **figürle zemin
arasında** (açık kâğıt / koyu kütle). Ukiyo-e'de bu ayrım renkten geliyor, burada
değerden — değer küçültmede hayatta kalır, renk kalmaz. 128 px testi bunu doğruladı.

Ayrıca stil, rig'in zayıflığını kendi diline çeviriyor: düz bir uzuv mekanik
döndüğünde boyalı figürde yanlış görünür, kâğıt kesiminde doğru görünür.

> **Kritik üretim kuralı — temiz varlık, dokulu ekran.** Doku ve ışık parçaya
> pişirilmez; ahşap baskı hissi tam ekran `CanvasLayer` overlay'inden, gün döngüsü
> `CanvasModulate` tintinden gelir. Aksi hâlde 15 parçanın her biri ayrı
> ışıklandırılmak zorunda kalır. Kan/vermilion tint dışında tutulur, yoksa vurgu ölür.

### Hedef seçimi rastgele oldu (2026-08-13)

Çekirdek artık hedefi listedeki ilk ayakta düşman yerine **rastgele** seçiyor; hedef
düşman ölene/kaçana kadar yapışkan. `CombatantSnapshot.TargetId` eklendi — koreografi
hedefi artık kendisi türetmiyor, çekirdekten okuyor (eski kopyalanmış kural silindi).

| 3v3, 10.000 dövüş, `below:0.3` | Ön saf | Rastgele |
| --- | --- | --- |
| Zafer | %5.1 | **%36.1** |
| Oyuncu ölümü | %32.0 | %25.8 |
| Kaçış | %63.3 | %40.8 |
| Uzuv kaybı | %2.50 | **%1.10** |

> Eski kuralda üç düşman da aynı savaşçıya yükleniyor, o savaşçı hızla eşiğin altına
> düşüyor ve politika tüm ekibi çekiyordu. Rastgele hedefleme hasarı yayıyor.
>
> **Not:** odaklı ateş matematiksel olarak daha güçlü AI'dır (ölen savaşçı hasar
> vermez), yani bu değişiklik düşmanı zayıflattı. **Uzuv kaybı yarıya indi** — oyunun
> imza mekaniği. Faz 9'da zorluk düşman sayısı/statlarından geri alınmalı, hedefleme
> kuralından değil.

Testler: bir yeni test — aynı kadrodaki iki savaşçı farklı hedeflere hamle yapıyor;
koreografi hedefi türetseydi ikisi aynı noktaya koşardı.

### Arena bir düzlem oldu (2026-08-13) — en büyük değişiklik

Çekirdeğe **uzam** girdi: her savaşçının `ArenaPoint` konumu var, gerçekten yürüyor,
silahın menzili var, kuşatma mümkün. Fizik motoru yok — kendi kinematiğimiz, sabit
tick, determinizm bozulmadı.

Getirdikleri:

- **Menzil:** menzil dışında saldırı başlamıyor; uzun silah uzaktan vuruyor
- **Hedefleme uzamdan çıkıyor:** "en yakın düşman". Rastgele seçim gerekmiyor
- **Iska:** hedef hamle sırasında menzilden çıkarsa kılıç boşluğa iniyor
- **Kuşatma:** arkadan gelen vuruş daha isabetli, daha ağır, kaçınılamaz
- **Kaçışın bedeli mesafeye bağlı:** çekilirken menzilindeki **her** düşman bedava
  vuruş kazanıyor; çevrildiysen kaçmak üç darbe demek
- **Kaçış artık sayaç değil mesafe:** savaşçı arenayı gerçekten terk ediyor

> **Sunum katmanı küçüldü.** `ArenaChoreography` sahte bir uzam taklit ediyordu: hamle
> mesafesi, kaçış mesafesi, ölünün düştüğü yerin hatırlanması. Hepsi silindi; sınıf
> artık yalnızca arena düzlemini ekrana yansıtıyor (derinlik = dikey kayma + ölçek +
> çizim sırası, brawler sahnelemesi) ve tek bir görsel süsleme bırakıyor: kılıcı
> toplarken geri yaslanma. Ölünün yerinde kalması bedava geldi — çekirdek ölüyü
> hareket ettirmiyor.

Ölçüm (3v3, 10.000 dövüş, `below:0.3`): zafer %67.8, ölüm %33.6, kaçış %12.3,
uzuv kaybı %0.56. Hız 16.000 → **8.700 dövüş/sn** (kriter: 10.000 dövüş < 10 sn,
hâlâ sekiz kat üstünde). Dövüş süresi 7.9 → 13.1 sn (yaklaşma süresi eklendi).

### Uzuv kaybı hedefe çekildi (2026-08-13)

Gün boyunca %2.50 → %1.10 → %0.87 → %0.56'ya kadar aşınmıştı. Hedef: makul oynayan
oyuncunun savaşçılarının ~%5'i sakat dönsün.

Ölçerek arandı. `Domina.Sim`'e iki bayrak eklendi: **`--grievous`** (ağır darbe eşiği)
ve **`--sever`** (kopma şansı) — Faz 9'un denge çalışması bunlarla yapılacak.

Bulunan iki şey:

1. **`--sever` beklendiği gibi davranmıyor.** Yükseltmek uzuv kaybını *azaltabiliyor*:
   düşmanlar da uzuv kaybediyor, zayıflıyor, dövüş erken bitiyor. Ayrıca kopma zarı
   tutmazsa müdahale edilmemiş savaşçı da ölmüyor — yani bu sayı hem ölümü hem
   sakatlığı aynı anda ölçekliyor.
2. **Asıl belirleyici tuning değil, oyuncunun ne zaman çektiği.** Aynı ayarlarla
   politika %30 → %50 → %70 olunca uzuv kaybı %1.3 → %5.6 → %7.7 oluyor.

Yapılan tek değişiklik: `GrievousSeverityThreshold` **0.28 → 0.20**. Eşik, silah
hasarlarının kümelendiği yerin hemen altına çekildi — 0.24 ile 0.28 arasında hiçbir
fark yok, çünkü o aralığa düşen darbe hiç yok.

| Politika | Zafer | Ölüm | Kaçış | Uzuv kaybı |
| --- | --- | --- | --- | --- |
| `below:0.2` | %59.8 | %54.5 | %3.8 | %0.88 |
| `below:0.3` | %53.7 | %55.0 | %6.3 | %1.27 |
| **`below:0.5`** | %11.6 | %43.2 | %47.7 | **%5.57** |
| `below:0.7` | %1.3 | %9.1 | %89.8 | %7.73 |

> Geç çeken oyuncu **ceset** getiriyor, erken çeken **sakat**. Hedeflenen tam olarak bu:
> tuşun ne zaman basıldığı roster'ın nasıl göründüğünü belirliyor. GDD §7'ye bu tablo
> denge hedefi olarak işlendi.
>
> Zafer oranının politikaya göre %60'tan %1'e savrulması ayrı bir sorun — **Faz 9'un
> asıl işi bu**, uzuv kaybı değil.

> ⚠️ **Bu tablo 2026-08-14'te geçersizleşti.** Sonuç ağacı ikiye ayrılınca uzuv kaybı
> artık çekme politikasının değil **kuşamın** fonksiyonu oldu; GDD §7'deki denge hedefi
> zırh bandıyla değiştirildi. Bkz. yukarıdaki "Uzuv kaybı oranı — karara bağlandı".

Yeni test dosyası: `MovementTests` (yaklaşma, menzil, uzun silah mesafesi, kişisel
alan, kuşatılmış kaçışın bedeli, ıska). Toplam 175 test yeşil.

### İsabet bölgesi eklendi (2026-08-13)

Her isabet artık bir bölgeye iniyor: gövde 45 / bacak 25 / kol 20 / kafa 10 ağırlıkla
(`CombatTuning`). Amaç ileride gelecek **yuva yuva zırhın** anlamlı olması — bölgeler
eşit olsaydı gövde zırhı dörtte bir değerinde kalırdı.

> **Yazarken yakalanan tasarım sızıntısı.** İlk sürümde gövdeye inen ağır darbe uzuv
> koparmıyordu; ölçüm oyuncu zaferini %36'dan **%53'e** çıkardı. Sebep: müdahale
> %45 ihtimalle **tamamen bedava** hâle gelmişti — ölümden dönüyorsun, hiçbir şey
> kaybetmiyorsun. GDD §7'nin vaadi bunun tersi. Kural düzeltildi: bölge hasarı ve zırhı
> ilgilendirir, sonuç ağacını değil; gövde vuruşunda kopan uzuv kalanlar arasından aynı
> ağırlıklarla seçilir. `DismembermentTests.BlowsToTheTorsoStillCostALimb` bunu
> koruyor.

Düzeltme sonrası sayılar rastgele hedeflemedeki değerlere döndü: zafer %35.9, ölüm
%26.0, kaçış %40.9, uzuv kaybı %1.17. Toplam 175 test yeşil.

### İlk iş

Varlık üretim şartnamesi netleşti; sıradaki somut adım **tam ekran doku overlay'i +
gün tinti**, ardından kesik yüzey varlıkları (omuz kütüğü, kalça kütüğü, kopan uzvun
ucu). Konsept sayfalarında kesik yüzey **düz vermilion disk + koyu kontur** olarak
sadeleştirilecek — kemik detayı 128 px'te kayboluyor, kopmayı taşıyan şey eksik
zincirin siluetidir.

### Silah yeterliliği — karara bağlandı (2026-08-13)

Üç soru cevaplandı ve **GDD §4'e işlendi**; artık burada değil orada yaşıyor.

| Soru | Karar |
|---|---|
| Hat sayısı | **Üç** — tek el / çift el / fırlatma. Fırlatma hattı uykuda, çekirdeğe mermi/uzam girene kadar (Açık Karar #4-C) |
| Büyüme kaynağı | **Dövüşte kullanım + dojo antrenmanı** |
| Acemi cezası | Yeterlilik 0'da **kuşanılabilir**, isabet belirgin düşük |

Aynı geçişte: GDD §2'nin Skeleton2D satırı düzeltildi, §5 (blok) ve §7 (künt sersemletme)
altına "kod ile fark" notları düşüldü, Açık Karar #4 dörde bölündü (A kilitli, B/C/D açık).

> Kullanıcının eklediği nüans GDD'ye girdi: kavrayış kaybı **yalnızca hattı** sıfırlar.
> Statlar hatta bağlı değil, yani sakat usta sıfırdan başlamıyor — **acemi isabetli bir
> veteran** olarak dönüyor. Risk keskin ama yıkıcı değil.

Kod tarafında henüz **hiçbir şey yok** — yeterlilik Faz 3 (meta katman) ile gelir;
çekirdekteki karşılığı `CombatTuning` içinde isabet ve saldırı hızı çarpanı olacak.

### Akılda tutulacaklar
- Denge sayıları **kasıtlı olarak ham**. İlk ölçüm 3v3'te oyuncu ölüm oranını
  %45-57 gösteriyor; bu Faz 9'un işi, şimdi ayarlanmayacak (bkz. ROADMAP riski).
- `Domina.Chat` hâlâ boş iskele (Faz 5). Test projesi de boş — `dotnet test`
  "no test is available" uyarısı buradan geliyor, hata değil.
- Dövüş savaşçıların kalıcı halini değiştirmiyor; ölüm/sakatlığı kalıcı hale
  işlemek **meta katmanın** işi ve henüz yazılmadı (Faz 3). Arenadaki kadro da
  geçici — `DemoRoster` Faz 3'te gerçek roster'a bırakacak.
- Sanat geldiğinde değişecek yer bellidir: `WarriorRig.Limb` (kemiğe asılı çizim) ve
  `RigAnimator`'daki duruş sayıları. `RigPose`'un alanları ve `BattleArena` değişmez —
  yordamsal duruşun yerini AnimationPlayer alsa bile arayüz aynı kalır.
