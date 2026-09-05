# Durum Kaydı

Son güncelleme: 2026-09-04 (Faz 3 başladı — kadro, gün döngüsü, kayıt sistemi ve dövüş sonrası muhasebe)

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
| Faz 3 — Dojo / meta katman | 🟨 Başladı (kadro + gün döngüsü + kayıt + dövüş sonrası muhasebe; ekonomi ve antrenman etkisi bekliyor) |
| Faz 4+ | ⬜ Başlanmadı |

Doğrulama: `dotnet build` → 0 hata / 0 uyarı, `dotnet test` → 336/336 yeşil.
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

**4-B'de açık kalan:** silah kırılması.

---

## Zehir çekirdeğe girdi (2026-09-03)

Açık Karar **#4-B'nin üçüncü maddesi kapandı**: zehir artık kodda. Kural ve sayı
tablosu GDD §7'de.

Kural: zehirli silahın **her isabeti** savunana bir doz bırakır — zar yok, namlu deriyi
çizdiyse zehir de girmiştir. Doz saniyede bir can yer ve bu hasar **ne zırhtan ne
Savunma statından** geçer; zehir, hasar azaltımının etrafından dolaşan tek yoldur. Doz
birikir (tavan 3.0), süre her yeni vuruşta baştan kurulur (6.0 sn).

Kilitlenen sayılar: tik başına 2.5 hasar, tik aralığı 1.0 sn, dozun ömrü 6.0 sn, azami
doz 3.0. Zehirli tantō 7/0.85 (temiz tantō 13/0.85), zehirli shuriken 12 hasar / 2
cephane.

### İlk kurulum yanlış cevap veriyordu

Zehirli bıçak 13 hasarla dururken ölçüm "zehir işe yarıyor" diyordu ama **iddiayı
doğrulamıyordu**: açık dövüşte %74.00, ō-yoroi kuşanmış oni'ye karşı %55.99 (katana
%73.09 / %68.62). Yani zehir zırhı aşmıyor, yalnızca zayıf bir bıçağı kurtarıyordu —
çünkü çıktının çoğu hâlâ çelikti ve zırh çeliği okuyor.

Bıçak 7'ye indirilip doz büyütülünce çıktının %60'ı zehre geçti ve iddia doğrulandı:

| Silah | Zırhsız oni | Ō-yoroi kuşanmış oni |
|---|---|---|
| Katana (kontrol) | %73.09 | %68.62 |
| Temiz tantō | %31.14 | %1.23 |
| Zehirli tantō | %72.19 | **%77.19** |

Beklenmedik sonuç: **zehirlinin karşısında ağır kuşanmak zarardır.** Plaka dozu
durdurmuyor, ağırlığı ise oni'nin vuruşunu geciktiriyor — zırhın işareti bu eşleşmede
ters dönüyor.

### Asıl düğme doz tavanı, ömür değil

Tavan: 1'de zehirli bıçak düpedüz kötü (%16.71), 2'de hâlâ geride (%50.92), 3'te katana
ile başa baş, 5'te baskın (%82.25).

Ömür 6 saniyeden sonra neredeyse hiçbir şey yapmıyor (3 sn %52.90, 4.5 sn %68.83,
6 sn %72.19, 9 sn %73.11): hızlı vuran silah süreyi zaten sürekli yeniliyor, uzun ömür
yalnızca **son** vuruştan sonrasını uzatıyor — o da çoğu dövüşte bitmiş dövüş.

Tik aralığı tarafsız bir düğme değil, doğrudan hasar hızı (0.5 sn'de %94.93, 1 sn'de
%72.19, 2 sn'de %30.40). 1 sn seçildi çünkü dozun ömrüne bölününce zehir **sayılabilir**
oluyor: altı vuruş.

### Zehir oyuncunun üstüne dönünce

`3v3-poison` eklendi (tengu zehirli shuriken atıyor; kontrol aynı kadro): zafer
%65.20'den %60.35'e, kaçış %8.10'dan %6.86'ya iniyor, ölüm %45.45'ten %50.44'e çıkıyor
ve ölümlerin %1.3'ü doğrudan zehirden.

**Çekilen savaşçının zehri durmuyor** — bu kasıtlı: sersemletme ve yakalama kaçış
vaadinin üstüne *yeni bir zar* konmasın diye çekilene işlemiyor, ama zehir yeni bir zar
değil, çoktan ödenmiş bir bedelin devamı; tuş bir panzehir değil. Ölçüm §5'in
merdiveninin ayakta kaldığını söylüyor: kaçış hâlâ çalışıyor, yalnızca daha pahalı.

### Zehrin almadığı şeyler

Zehir uzuv koparmaz ve sersemletmez — ikisi de *darbenin* sonucu, zehirde vuran kimse
yok. Zehirle gelen ölüm ayrı bir sebep taşıyor (`DeathCause.Poison`), yoksa "başka türlü
öldürüyor" iddiası hiçbir sayaçta görünmezdi.

Yeni test dosyası: `PoisonTests` (9 test). Toplam 257 test yeşil.

**4-B'de açık kalan:** silah kırılması.

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

---

## Silahın elden düşmesi çekirdeğe girdi (2026-09-03)

Açık Karar **#4-B'nin son maddesi kapandı; madde tamamen kapandı.** Kural ve sayı
tablosu GDD §7'de.

Kural iki yerden gelir. **Zırha inen vuruş** saldıranın kavrayışını bozar: zar, silahın
elden çıkma eğilimi ile vurulan parçanın sertliğinden çıkar ve sertlik parçanın kopma
direncinden okunur — yani **çıplak bölgeye inen vuruş hiç düşürmez**. **Yakalanan silah**
çengelde avuçtan sökülebilir; bu zar kilidin üstüne binmez, **yerine geçer**.

Silah **kırılmaz, düşer**. İlk kurulum kırılmaydı; bakım defteri (envanter, onarım, yedek
silah) açtığı için düşmeye çevrildi. Düşen silah arenada durur, dövüş bitince sahibine
döner ve **eli boş olan herkes** alabilir: düşüren, takım arkadaşı, düşman. Elinde silah
olan ne alır ne arar — yerdeki namluya bir adım bile atmaz; kolunu kaybeden savaşçı da
yerdeki çift el silahı geçer.

Kilitlenen sayılar: zırha inen vuruşta taban şans 0.05, yakalanan silahta 0.05, sertlik
payı 1.0, savrulma mesafesi 250 birim, alma mesafesi 60 birim; elden çıkma eğilimi kesici
1.0 / delici 0.6 / künt 0.2 / yumruk 0.

### Takas plakanın önünde dönüyor

Zırhlı düşmana karşı üç sınıf (20.000 dövüş, `losing:0.7`):

| Silah | Kural yok | Kural var | Düşürme | Yerden alınan |
| --- | --- | --- | --- | --- |
| Nodachi (kesici) | %94.25 | %87.53 | %11.76 | %7.3 |
| Tetsubo (künt) | %90.55 | **%89.20** | %2.73 | %4.8 |
| Yari (delici) | %79.69 | %75.48 | %9.86 | %8.4 |

Künt sınıf, kesicinin plakanın önünde de üstün olduğu son yeri böyle kaybediyor. Taban
şans tarandı: 0.02'de hiçbir şey dönmüyor (%91.50 / %89.77), 0.05 takasın döndüğü yer,
0.08'den sonra kesici silah zırhlı düşmanın önünde taşınamaz oluyor (0.20'de %72.93).
Sertlik payı fazladan bir düğme değil, taban şansın kopyası çıktı (0.5'te düşürme
%11.71 → %6.04); 1.0'da bırakıldı.

### Bedeli taşıyan şey mesafe değil, yön

Silahın nereye savrulduğu üç kez ölçüldü ve kuralın tamamı buna bakıyor:

- **Sahibinin gerisine** düşerse yerden alma yürüyüşü savaşçıyı dövüşten geri çeker ve
  yavaş bir düşmanın önünde düşürme **bedava** olur: mesafe arttıkça oyuncunun zaferi
  yükseliyor (%94.30 → %94.55; kural yokken %94.25). Yani kural hiçbir şeye mal olmuyor
- **Yana** savrulmak da aynı kapıya çıkıyor (%94.4): yavaş düşman hattı terk eden
  savaşçıyı cezalandıramıyor
- **Karşıdakinin arkasına** düşünce bedel gerçek: silaha gitmek düşmanın içinden geçmek
  demek, kişisel alan buna izin vermiyor. Takas ancak burada dönüyor

Yön oturunca mesafe bir düğme olmaktan çıkıyor: 150 / 250 / 400 birim %87.43 / %87.45 /
%87.48 veriyor. 250 seçildi (savaşçı boyuna yakın, ekranda okunur).

### Yerden alma teke tekte çalışmaz, kalabalıkta çalışır

Düşen silahların 1v1'de %7.3'ü, 3v3'te %40.4'ü geri alınıyor. Kalabalıkta hedef
bölündüğü için silahın başına gidilebiliyor — kuralın bedeli sabit bir ceza değil,
dövüşün şekline bağlı.

### Yakalamanın düşürme şansı: 0.10'da fren kırılıyor

0'da yakalama aleti silah düşürmekle hiçbir şey kazanmıyor (jitte %74.87, katana %75.02).
0.05'te jitte %78.00 / sai %78.88 ile kılıç taşıyan düşmanın önünde öne geçiyor.
**0.10'da jitte nodachi'ye karşı da doğru seçim oluyor** (%38.27'ye karşı katana %37.84)
ve `CatchTwoHandedFactor` anlamsızlaşıyor — 0.05 bu yüzden.

Kural yakalamayı üstün silah yapmıyor, çünkü iki freni de duruyor: nodachi'ye karşı jitte
%35.22 / katana %37.84, ō-yoroi kuşanmış düşmana karşı jitte %34.14 / katana %60.81.
"Her seçenek bir şeyde en iyi" deseni ayakta — yalnızca katana'nın en iyi olduğu yer
değişti: zafer değil, **zırhlı düşman**.

### Zırh ilk kez gerçek bir duvar

3v3'te yokai'lerin hepsini tam kuşam yapmak kural olmadan oyuncunun **işine yarıyordu**
(%65.20 → %67.32; ağır kuşam vuruşu geciktirir). Kuralla birlikte %65.35'e iniyor ve
oyuncu savaşçılarının %9.32'si dövüş içinde silahını düşürüyor.

### Kural iki kilitli tabloyu bozdu

Düşürme zırhla dövüşen **herkesi** etkilediği için daha önce kilitlenen ölçümler kaydı —
kural kapatıldığında (`--disarm-chance 0 --disarm-catch 0`) eski sayılar birebir geri
geliyor (3v3 %65.20), yani kayan tek şey bu kural.

- **Zehir:** zehirli tantō da ō-yoroi'ye vururken silahını düşürüyor (%20.18). Yeni tablo
  katana %75.02 / %60.81, zehirli tantō %74.26 / **%69.39**. Zehrin iddiası ayakta, ama
  **"zehirlinin karşısında ağır kuşanmak zarardır" artık doğru değil**
- **Yakalama:** katana %75.02 / jitte %78.00 / sai %78.88. Yakalama aletleri kılıçlı
  düşmanın önünde artık zaferde de önde

GDD §7'nin ilgili iki bölümüne bu düzeltmeler işlendi.

### Ölçümü besleyen yeni araçlar

Beş senaryo (`blade-armored`, `club-armored`, `spear-armored`, `jitte-armored`,
`3v3-armored`), beş düğme (`--disarm-chance`, `--disarm-catch`, `--disarm-armor-share`,
`--drop-distance`, `--pickup-radius`) ve üç sayaç: silahını düşürme oranı, düşenlerin
yerden alınma oranı ve **düşmanın** düşürme oranı. Sonuncusu ayrı duruyor çünkü plakaya
vurup silahını elinden kaçıran düşmanı kimse düşürmemiştir — düşüreni olmayan tek olay bu.

Yeni durum: `CombatState` değişmedi (silahsızlık bir durum değil, bir işaret),
`CombatantSnapshot.Disarmed` (HUD "· silahsız" yazar), `RigReactionKind.WeaponLost`,
`GroundWeapon`. Yeni test dosyası: `DisarmTests` (12 test). Toplam 271 test yeşil.

> **Yol boyunca çıkan iki hata:** `ArenaPoint.MovedAwayFrom` istenen mesafeyi kaynağa
> olan uzaklıkla kırpıyordu (yön birim vektöre indirgenmemişti), o yüzden savrulma
> mesafesi ilk ölçümlerde hiçbir şey yapmıyor gibi göründü. `Combatant.Weapon` her
> okumada yeni bir `Weapon.Fists()` üretiyordu; dövüş başına ~109 KB ayırma demekti ve
> `ThroughputTests` yakaladı — tek bir statik örneğe indirildi.

**#4-B'de açık kalan yok.** Ekipmanın kalıcı bedeli (yedek silah, onarım) bilinçli olarak
**yok**: düşen silah dövüş sonunda geri geliyor.

---

## Zırh yıpranıyor ve dağılıyor (2026-09-03)

Ekipmanın son ucu kapandı: silah düşer ve geri alınır, **zırh yıpranır ve gider**.

Kural: bir parça durdurduğu hasar kadar aşınır — emdiği her puan kendi dayanıklılık
havuzundan düşer. Havuz bitince parça dövüşün ortasında dağılır, o bölge çıplak kalır ve
parça **kalıcı olarak gider**. Yıpranma dövüşe değil **savaşçıya** aittir: tek dövüşte
tükenmez, seferler boyunca birikir (`Warrior.ArmorWear`), dövüş onu okur ama yazmaz —
kalıcı hale işlemek dojo katmanının işi, uzuv kaybındaki yolun aynısı.

Dayanıklılıklar: keikogi 40, dō-maru 110, ō-yoroi gövdeliği 180, kote 45 / ağır kote 75,
suneate 45 / ağır suneate 75, kabuto 90. Tek denge düğmesi `ArmorDurabilityScale`.

### En çok emen kuşam en çok yıpranan kuşam

3v3, 20.000 dövüş, `losing:0.7`:

| Kuşam | Dövüş başına yıpranma | Takımın dayanıklılığı | Ömür |
| --- | --- | --- | --- |
| Hafif keikogi | 5.4 | 40 | ~7 dövüş |
| Dō-maru | 20.2 | 290 | ~14 dövüş |
| Ō-yoroi | 38.7 | 570 | ~15 dövüş |

Ō-yoroi hafif kuşamın yedi katı hasar emiyor ve yedi katından biraz fazla dayanıklılık
taşıyor: pahalı kuşam korumayı satın alıyor, bedavaya almıyor.

### Dağılma anı: uzuv kaybı ikiye katlanıyor

Varsayılan havuzlarda tek bir dövüşte parça dağılmıyor (20.000 dövüşte sıfır) — dağılma,
**yıpranmış kuşamla** sahaya çıkıldığında geliyor. Havuz ölçeği düşürülerek ölçüldü:

| Ölçek | Dağılan parça (savaşçı başına) | Zafer | Uzuv kaybı |
| --- | --- | --- | --- |
| 1.0 | 0.00 | %70.27 | %0.83 |
| 0.25 | 0.12 | %69.92 | %0.94 |
| 0.1 | 0.89 | %67.55 | **%1.93** |

Yıpranmış zırhla dövüşe girmek yalnızca daha çok hasar yemek değil; plakası dağılan
savaşçının uzuv kaybı iki katına çıkıyor. Kuşamı yenilememenin bedeli roster'da görünüyor.

### Zincirin ucu: gitmiş zırh silah da düşürmez

Dağılan parça ağırlığını da sertliğini de bırakıyor. Yokai'ler tam kuşamken oyuncunun
silahını düşürme oranı %1.81, kuşam yıpranmış girdiğinde %1.41 (zafer %65.35 → %63.83):
zırh gittikçe dövüş daha ölümcül ama daha temiz hale geliyor — daha çok kesik, daha az
elden düşen silah.

### Yeni durum ve araçlar

`ArmorPiece.Durability`, `ArmorWearSet` (değer türü — özet savaşçı başına üretiliyor,
sözlük ayırmak ölçümü yavaşlatırdı), `Warrior.ArmorWear`, `HitLocationSet`,
`ArmorDestroyed` olayı, `CombatantSnapshot.DestroyedArmor` (rig kuşamı buradan söker),
`RigReactionKind.ArmorShattered`, `--armor-durability` düğmesi ve iki sayaç (dövüş başına
yıpranma, dağılan parça oranı). Yeni test dosyası: `ArmorDurabilityTests` (7 test).
Toplam 278 test yeşil.


---

## Blok ayrı bir durum oldu (2026-09-03)

Açık Karar **#12 kapandı.** Blok GDD §5'te ayrı bir durum sayılıyordu ama çekirdekte
Savunma statının içinde eriyordu; doküman bunu "kod ile fark (açık)" diye taşıyordu.
Fark kapandı: `CombatState.Blocking` gerçek bir hamle.

Kural ve sayı tablosu GDD §5'te. Özet: karar Savunma statından çıkar (taban yok —
Savunma 0 hiç bloklamaz), şart gelen vuruşu okumaktır, duruş 0.8 sn sürer ve o sürede
savaşçı vurmaz, hasarın %70'i × silahın blok kalitesi silinir, uzuv kopmaz, künt
sarsıntının %75'i geçer.

### İlk hâli kuralı ters çalıştırıyordu

Duruş yalnızca "menzilde düşman var mı" diye alınınca körlemesine alınıyordu: savaşçı
başına yine 0.20 darbe karşılıyor ama **zaferi düşürüyordu** (%71.21 → %70.50). Boşa
alınan her duruş saldırı döngüsünden yeniyordu; blok, Savunma statının karşılığı değil
cezasıydı. Şart "gelen vuruşu oku"ya (`AttackWindup` ya da koşan hücum) çevrilince
işaret döndü: **%72.39 zafer, ölüm %50.44 → %49.19, uzuv kaybı %5.19 → %4.96.**

### Zincirleme blok dövüşü kilitliyordu

Zar her karar adımında yeniden atılınca savunması yüksek savaşçı arka arkaya bloklayıp
hiç vurmuyordu — sabit zarla koşan 47 test bunu anında yakaladı. Kural: blok arkasına
blok gelmez. Duruş bir ritme bağlandı — karşıla, sonra karşılık ver.

### Kuralın kendi freni yok

`MaxBlockChance` büyüdükçe oyuncu tek yönlü kazanıyor: 0.25'te %71.61, 0.45'te %72.39,
0.70'te %73.05, 1.0'da %74.08. İçeride bir fren yok; freni Savunma statının dojo'da
başka statlarla yarışması sağlıyor. Sayı Faz 9'a bırakıldı.

### Yakalama aletine blok kayrılmadı

Jitte/sai'ye çift el silah kadar blok kalitesi verilmişti; ölçüm bunun **kilitli bir
freni kırdığını** gösterdi — ağır silah taşıyan düşmanın önünde jitte yanlış seçim
olmaktan çıkıyordu (jitte-heavy %35.38 > katana-heavy %34.96). Override kaldırıldı.

---

## Hedef seçimi bir karar oldu (2026-09-03)

`FindTarget` tek satırlık bir sıralamaydı: en yakını seç, ölene kadar ona sadık kal.
Artık savaşçı her karar adımında düşmanları puanlıyor — mesafe, yara, açık bölge,
takım arkadaşlarının yığılması, ve yön değiştirmenin bedeli. Ağırlıklar `CombatTuning`'de,
puanlama **deterministik** (zar yok): rastgelelik kararın kendisinde değil savaşçının
kimliğinde.

Tablo GDD §4'te.

### Fırsat penceresi olmadan kural düz bir zorluk artışıydı

Sınırsız yara ağırlığı savaşçıyı yanındaki sağlam düşmanı bırakıp arenanın öbür ucundaki
yaralıya yürütüyor, yol boyunca bedava vuruş yediriyordu. Tek yönlü aşağı: ağırlık 0'da
%72.29, 40'ta %72.28, 80'de %72.03, 120'de %71.77, 200'de %70.74. Yara ve açık bölge
kazançları 200 birimde sıfıra inen bir pencereye bağlanınca eğri düzeldi: **%72.17**
(eski kural %72.28) ve tam kuşamlı düşmana karşı yeni kural açık ara iyi — `3v3-armored`
%71.84 → **%72.68**.

Ölüm oranı yine de artıyor (%49.11 → %50.30): odaklanan takım daha çok öldürüyor, dövüş
daha keskin bitiyor.

### Açık bölge ağırlığı şimdilik uykuda

Varsayılan dayanıklılıkta tek dövüşte parça dağılmıyor, dolayısıyla ağırlığın ısırdığı
yer sefer boyunca yıpranmış kuşam. Yıpranmış kuşamla ölçüldüğünde etkisi küçük ve oyuncu
aleyhine (%70.06 → %69.91) — açık bölgeyi düşman da görüyor.

### Ölçüm bir performans borcunu ödetti

Puanlama tik başına koşarken 10.000 dövüş bütçenin iki katına çıktı (19.86 sn / 10 sn) ve
dövüş başına ayırma 350 KB'ye fırladı. İki düzeltme: hedef yalnızca karar adımlarında
seçiliyor (yürüyüş döngüsü seçimi devralıyor), ve `HitLocationSet.Count()` artık iterator
yerine bit sayımı kullanıyor.

### Kilitli tablolar korundu — ama payları inceldi

| Karşılaştırma | Önce | Sonra |
|---|---|---|
| kesici / künt (blade / club) | %92.52 / %92.51 | %92.25 / %92.90 |
| jitte / katana | %78.00 / %75.02 | %78.70 / %78.28 |
| sai | %78.88 | %80.55 |
| jitte-heavy / katana-heavy (fren) | %35.22 / %37.84 | %34.72 / %34.96 |
| jitte-armored / katana-armored (fren) | %41.38 / %60.32 | %35.44 / %60.77 |
| zehirli / katana, zırhlı düşmana | %77.19 / %68.62 | %71.85 / %60.77 |

Yönler duruyor: künt kesiciyle başa baş, jitte katanayı geçiyor, iki fren de ayakta,
zehir zırhın önünde hâlâ tek doğru cevap. Ama **jitte'nin payı 2.98 puandan 0.42'ye,
ağır silah freni 2.62 puandan 0.24'e indi** — blok, yakalama aletinin yaptığı işin bir
kısmını herkese dağıtıyor. Açık dövüşte zehirli bıçak katananın 0.90 puan gerisindeyken
şimdi 2.04 puan geride. Bu üç sayı Faz 9'da yeniden bakılmalı.

### Yeni durum ve araçlar

`CombatState.Blocking`, `BlockRaised` / `AttackBlocked` olayları, `Weapon.BlockFactor`,
`WarriorBattleSummary.BlocksPerformed`, `RigReactionKind.Block` + `Guard()` duruşu,
altı yeni ayar düğmesi (`MaxBlockChance`, `BlockSeconds`, `BlockDamageReduction`,
`BlockDismembermentShare`, `BlockStunShare`, `BlockStaminaCost`) ve beş hedef seçimi
ağırlığı (`TargetDistanceWeight`, `TargetWoundedWeight`, `TargetExposedWeight`,
`TargetCrowdPenalty`, `TargetStickiness`, `TargetOpportunityRange`). Sim tarafında
`--block-chance`, `--block-seconds`, `--block-reduction`, `--target-wounded`,
`--target-exposed`, `--target-crowd`, `--target-sticky` ve savaşçı başına blok sayacı.

Yeni test dosyaları: `BlockTests` (8 test), `TargetSelectionTests` (6 test).
Toplam 292 test yeşil, 0 uyarı.

---

## Dojo çekirdeğe girdi (2026-09-04)

Faz 3'ün ilk parçası: **kadro, gün döngüsü ve kayıt sistemi**. Üçü de
`Domina.Core/Dojo` altında ve motora bağımlı değil — arayüz yok, Godot yok.

### Kadro, dövüşün bilmediği hâli taşıyor

`Warrior` dövüşün okuduğu kalıcı hâl olarak kaldı; yanına `RosterEntry` geldi ve meta
durumu o taşıyor: kalan revir günü, o günkü uğraş, tamamlanmış antrenman günü. Ayrı
tutulmasının sebebi mimari kural — dövüş çözümleyicisi takvimi bilmez ve toplu
simülasyon aynı savaşçıyı on binlerce kez koşturur, orada "gün" diye bir şey yoktur.

`Roster` iki kuralı zorluyor:

- **Ölen silinmez.** Permadeath kalıcı, ama savaşçının adı, onuru ve sakatlıkları
  kayıtta durur. Canlılık `Warrior.IsAlive`'dan okunur.
- **İsim eşsizliği yalnızca canlılar arasında.** GDD §6'nın kuralı: X ölünce adı havuza
  döner, ileride yeni bir X gelebilir. Chat komutunun (`!ronin-<isim>`) tek bir hedefe
  çözülmesini sağlayan şey bu.

### Gün döngüsü deterministik

`DojoState.AdvanceDay()` bir günü kapatır: antrenman sayaçları işler, revir günleri erir,
onur nötre doğru bir adım kayar. Rastgelelik **yok** — aynı durum aynı çağrılarla aynı
sonucu verir. Karşılaşmaya girmek de tam bir gün yer (GDD §10), yani sefer katmanı da
dövüş bitince aynı çağrıyı yapacak; gün başına iki kez çağrılmaz.

Onur decay'i eşiğin öbür tarafına **sarkmıyor**: nötre olan mesafe adımdan küçükse
savaşçı tam 50'ye oturur. Aksi hâlde onur nötrün etrafında salınırdı.

Kapanan gün bir `DayReport` döndürüyor (revirden çıkanlar, antrenman görenler) — "bugün
ne oldu" ekranının girdisi.

### Kayıt: dosya dengeyi taşımıyor

Kayıt ayrı bir tip ailesi (`DojoSnapshot` ve arkadaşları), canlı modelin serileştirilmiş
hâli değil. Sebep ölçülebilir bir tehlike: canlı model doğrudan yazılsaydı her denge
alanı (menzil, uzuv kopma çarpanı, blok kalitesi, zırh dayanıklılığı) dosyaya girer ve
**eski kayıt yeni dengeyi geri getirirdi** — oyuncu bir sonraki yamada düzeltilen sayıyı
kaydından geri yüklerdi. Dosyaya yalnızca oyuncunun ürettiği şey yazılıyor: kim, hangi
adla, hangi statlarla, ne kuşanmış, ne kaybetmiş.

GDD §2'nin üç kuralı da testle bağlandı:

| Kural | Karşılığı |
|---|---|
| Versiyonlu | `DojoSnapshot.CurrentVersion`; daha yeni sürümden gelen dosya yükleniyor ama uyarı bırakıyor |
| Merge-on-load | Eksik alan varsayılanıyla, tanınmayan alan yok sayılarak yükleniyor |
| try/catch | `Load` **hiçbir koşulda fırlatmıyor**; bozuk metin başarısız bir `LoadResult` döndürüyor |

Bozuk tek bir savaşçı kaydı kadronun geri kalanını götürmüyor: çakışan kimlik atlanıyor,
adsız kayda ad veriliyor, aynı adı taşıyan ikinci canlı yeniden adlandırılıyor — hepsi
uyarı listesine yazılarak. Merge-on-load'ın bedeli dosyanın sessizce eksik yüklenmesidir;
uyarılar tam olarak bunu görünür kılmak için taşınıyor.

### Kasıtlı olarak yazılmayanlar

- **Antrenman statlara dokunmuyor.** Yalnızca gün sayıyor. Etkisi ölçülüp kilitlenmeden
  bir sayı uydurmak, sonradan sökülmesi zor bir denge borcu olurdu.
- ~~**İlaçla iyileşme hızlandırma yok.**~~ 2026-09-04'te yazıldı — aşağıya bakın.
- **Karşılaşma teklifi yok.** Faz 4'ün işi; gün döngüsü onu bekleyecek şekilde duruyor.

### Dövüşün bilançosu kadroya yazılıyor

`BattleAftermath` dövüş sonucunu alıp kalıcı hale çeviren **tek yer**. Çekirdek hâlâ
kalıcı hale dokunmuyor — ölüm, uzuv kaybı, dağılan zırh ve emilen yıpranma dövüş
özetinde birer rapor; geri dönüşsüz hale gelmeleri burada oluyor. Ayrım mimari kuralın
gereği: toplu simülasyon aynı kadroyu on binlerce kez koşturuyor ve hiçbirinde savaşçının
kalıcı hali bozulmamalı.

| Girdi | Kadroya etkisi |
|---|---|
| `Died` | `Roster.Kill` — permadeath; revir günü ve onur işlenmez |
| `LostParts` | Kalıcı sakatlık; aynı uzuv ikinci kez kaybedilmez |
| `ArmorWear` | Savaşçının yıpranma defterine **eklenir** (seferler boyunca birikir) |
| `DestroyedArmor` | Parça kuşamdan çıkar ve o yuvanın yıpranması **sıfırlanır** |
| Kalan can + uzuv sayısı | Revir günü |
| İsabet oranı, kaçış | `HonorEngine` üzerinden onur |

Dağılan yuvanın sayacının sıfırlanması gerekiyordu: yıpranma **parçaya** ait, yuvaya
değil. Sayaç kalsaydı yerine takılan yepyeni parça dağılanın defterini devralır ve ilk
darbede dağılırdı.

Takım filtresi de kural: yokai'ler kendi kimliklerini taşıyor ve bu kimlikler
kadrodakilerle **çakışabilir**. Filtre olmasaydı düşmanın kaybettiği kol dojo'daki bir
savaşçıya yazılabilirdi.

Revir günü sayıları (`RecoveryDaysAtFullDamage` 6, `RecoveryDaysPerLostLimb` 5,
`RecoveryFreeDamageShare` 0.25) **kilitli değil** — GDD §7 yalnızca "yara ağırlığına
göre" diyor. Bedava hasar payı olmadan her dövüş bir gün revir demek olurdu ve gün
döngüsünün asıl kararı (bugün sefere mi, antrenmana mı) kendiliğinden ortadan kalkardı.

Yeni test dosyaları: `DojoTests` (15 test), `DojoSaveTests` (13 test),
`DojoAftermathTests` (16 test). Toplam 336 test yeşil, 0 uyarı.


## 2026-09-04 — Ekonomi sayıları ölçüldü ve kilitlendi (Açık Karar #5)

Kasa katmanı girdi ve fiyatlar ölçümle kapandı. Yeni dosyalar:
`Domina.Core/Dojo/EconomyTuning.cs` (bütün sayılar tek yerde),
`Domina.Core/Dojo/Quartermaster.cs` (fiyat sorusu ile alışverişi ayıran tek kapı),
`Domina.Sim/CampaignRunner.cs` (dojo'yu gün gün oynatan ölçüm aracı).

### Neden dövüş değil, sefer dizisi ölçüldü

Ekonomi tek dövüşe bakarak kilitlenemiyor: zırhın bedeli seferler boyunca birikiyor,
revir günü geliri değil **zamanı** yiyor, ölen savaşçının yerine alınan da kasadan
çıkıyor. Ölçüm birimi bu yüzden dövüş değil dojo ömrü oldu —
`Domina.Sim --mode campaign` aynı kadroyu günlerce oynatıp kasanın eğrisine bakıyor.
Oyuncunun yerine sabit bir politika oynuyor (onar, yenile, adam al, sefere çık);
politikanın akıllı olması değil, **aynı** olması gerekiyor.

### Ölçüm ayrı bir senaryoda yapıldı: `patrol`

Mevcut senaryoların hepsi birer denge sondası — bir kuralın ucunu görebilmek için kasten
ağır kurulmuşlar ve savaşçı-dövüş başına ölüm oranları %38-49 bandında. Böyle bir dövüş
her gün yapılamıyor: kadro günde bir cenaze kaldıramıyor ve o kadroyla ölçülen fiyat
aslında savaşçı fiyatını ölçüyor, zırhın ya da ilacın fiyatını değil. `patrol` sıradan
günün karşılaşması: zafer %98.6, savaşçı-dövüş başına ölüm %7.

### Kilitlenen sayılar (1000 dojo × 60 gün)

| Kalem | Sayı |
|---|---|
| Zafer ödülü | 0.45 altın / düşman canı (çekilme ve bozgun: 0) |
| Zırh parçası | 1.50 altın / dayanıklılık puanı |
| Onarım | 0.90 altın / yıpranma puanı |
| Yiyecek / su | 2 / 1 altın, savaşçı başına günde 1'er |
| İlaç | 12 altın, revirdeki savaşçı başına günde 1 |
| Savaşçı alımı / başlangıç sermayesi | 150 / 600 altın |

Sonuç: dövüş başına net **31.3 altın**, günlük tüketim 34.5, boş gün %33.8, aç gün %4.4,
sermayesini koruyan dojo %66.4, kapanan dojo %1.2.

### Ölçümün ortaya çıkardığı üç şey

1. **Bağlayıcı kısıt altın değil, kadro.** Karşılaşma zorluğu savaşçı-dövüş başına %20
   ölümün üstüne çıktığında hiçbir fiyat ayarı dojo'yu ayakta tutmuyor — gelirin tamamı
   savaşçı yerine koymaya gidiyor. Zorluk eğrisi (GDD §10) aynı zamanda ekonominin eğrisi.
2. **Takvimi ilacın fiyatı belirliyor.** Günlük tüketimin üçte ikisi ilaç. Bedava ilaçla
   boş gün %28.3, 12 altında %33.8, 24 altında %59.1 — "bugün sefere mi, revire mi"
   baskısını yaratan kalem bu.
3. **Ödül eğrisi dar.** 0.35'te dojoların %14.3'ü sermayesini koruyor, 0.70'te kasa 3719
   altına çıkıp para kısıt olmaktan çıkıyor. 0.45 ikisinin arasındaki diz.

### İki kural, ölçümden çıktı

- **Onarım daima yenilemeden ucuz** (0.90 < 1.50). Eşit ya da pahalı olsaydı onarım diye
  bir karar kalmazdı. Ölçümde iki uç neredeyse başa baş (erken onaran 959, sonuna kadar
  kullanan 1012 altınla bitiriyor) — ama sonuna kadar kullanan, parçayı **dövüşün
  ortasında** kaybediyor. Altın eşitken riski seçmek oyuncunun kararı.
- **Kıtlığın bedeli zaman, ölüm değil.** Ambar yetmezse revirdekiler önce doyuyor; aç
  savaşçı o gün ne iyileşiyor ne antrenman yapıyor, ama kimse açlıktan ölmüyor. Sıra
  keyfî olamazdı: yaralıyı aç bırakmak kıtlığı telafisi olmayan bir cezaya çevirirdi.

### İlaç artık gerçekten iyileştiriyor

İlaçsız gün bir revir günü eritiyor, ilaçlı gün iki. İlaç bu yüzden zorunlu bir vergi
değil, hızlandırıcı — Faz 3'ün "revir/hekim" maddesi bununla kapandı.

Doğrulama: build 0 hata / 0 uyarı, `dotnet test -c Release` 353/353 yeşil (+17:
`EconomyTests` 12, `CampaignRunnerTests` 5). Yeni dosyalarda `dotnet format` temiz.


## 2026-09-04 (ikinci tur) — Günün karşılaşma teklifi (Faz 4 girişi)

Yeni klasör `Domina.Core/Campaign`: `Bestiary` (ölçeklenebilir yokai kalıpları),
`EncounterGenerator` + `EncounterTuning` (günün teklifi ve zorluk eğrisi), `EncounterOffer`
(tehdit bandı, kaba tanım, dayatılan ekip sayısı), `Expedition` (teklifi dövüşe çeviren
tek köprü).

### Teklif kayıtta durmuyor

Üretim gün ile seferin tohumunun **saf** bir fonksiyonu. Kazandığı iki şey var: kayıt
dosyası bestiary'yi taşımıyor (eski kayıt yeni dengeyi geri getiremez, GDD §2) ve oyuncu
beğenmediği teklifi kaydı yeniden yükleyerek değiştiremiyor. `DojoSnapshot` yalnızca bir
`Seed` alanı kazandı — biçim bozulmadı, eksik alan varsayılanla yükleniyor.

### Köprü tek yerde

`DojoState` dövüşü kurmuyor, `Battle` günü kapatmıyor. `Expedition.Send` üçünü sırayla
yapıyor: dövüşü koşturur, sonucu kadroya yazar (`BattleAftermath`), ödülü öder ve **günü
kendi kapatır**. Sefer bir gün yiyor, kaçılsa da (GDD §10) — "gir-bak-kaç" döngüsünü
kapatan kalem bu. `Expedition.Refuse` ekibi gerekçesiyle geri çeviriyor: boş ekip, dünün
teklifi, yanlış ekip sayısı (düello tam bir savaşçı ister), kadroda olmayan savaşçı,
revirdeki savaşçı.

### Ölçüm: "al ya da bırak" gerçekten bir karar mı

`Domina.Sim --mode campaign --offers on` artık sabit senaryo yerine üretilen teklifleri
oynatıyor. 500 dojo × 120 gün, tek fark politikanın teklifi eleme hakkı:

| Politika | Kapanan dojo | Ayakta kalınan gün (ortanca) | Ölüm / savaşçı-dövüş | Aç gün | Geri çevrilen |
|---|---|---|---|---|---|
| Her teklife girer | %99.2 | 54 | %9.2 | %7.1 | %0 |
| Kadro eksikken ağır teklifi çevirir | %0.4 | 120 (hepsi) | %7.1 | %50.8 | %67.7 |

İki başarısızlık birbirinin zıddı: hiç reddetmeyen dojo **kadrosunu**, her ağırı reddeden
dojo **kasasını** kaybediyor. Reddetmek bir kaçış değil, gün ile savaşçı arasında takas.

### Ölçüm sırasında düzeltilen kural

İlk hâlde kalabalık teklifin gücü düşmanlara **bölünüyordu** (`power / √sayı`). Sonuç:
aynı tehdit daha az canla taşınıyor, ödül düşman canına bağlı olduğu için (§11) kalabalık
teklif aynı riski yarı fiyata satıyordu — ölçümde dojo'lar %99 oranında kapanıyordu.
Kalabalık artık gücü bölmüyor: üç düşman üç kat düşman, üç kat ödül.

### Kasıtlı olarak yazılmayanlar

- **Eğrinin sayıları kilitlenmedi.** Ölçüm neyin ölçüldüğünü söylüyor, hangi eğrinin doğru
  olduğunu değil; eğri Faz 9'un denge turunda kapanacak.
- **Yokai davranışı yok.** Bestiary'de yalnızca sayılar var (Açık Karar #3'ün açık kalan
  yarısı davranış). Kalıba bir alan eklendiğinde encounter üretimi değişmeyecek.
- **Rastgele olaylar yok** (GDD §11) — ekonominin açık kalan tek kalemi.

Doğrulama: build 0 hata / 0 uyarı, `dotnet test -c Release` 368/368 yeşil (+15:
`EncounterTests` 12, `CampaignRunnerTests` +3). Yeni dosyalarda `dotnet format` temiz.


## 2026-09-04 (üçüncü tur) — Rastgele olaylar (GDD §11'in son kalemi)

`Domina.Core/Dojo/RandomEvents.cs`: günde %15 olasılıkla bir aksilik. Beş tür —
hırsızlık, erzak bozulması, kuyunun bulanması, ilacın küflenmesi, hastalık.
Hepsi eksiltir; bağış ya da hazine yok, çünkü §11 olayları **tampon baskısı** olarak
tarif ediyor ve çift yönlü bir tablo baskıyı ortadan kaldırırdı.

### Etkinin ambara vurmaması gerekiyordu

İlk akla gelen olay "erzak çalındı" ama günlük alışveriş ambarı tam ihtiyaç kadar
dolduruyor (`Quartermaster.Restock`), yani stok neredeyse hep sıfır — çalınan erzağın
karşılığı da sıfır olurdu. Bu yüzden olaylar üç yerden birine vuruyor: **kasa** (hırsızlık),
**o günün faturası** (bozulan erzak, bulanan kuyu, küflenen ilaç) ya da **takvim**
(hastalık).

Aksilik `AdvanceDay` içinde upkeep'ten **önce** işleniyor: bozulan erzak o günün
alışverişini pahalılaştırmalı, ertesi güne ötelenmemeli.

### Ayrı tuz

Olay da teklif gibi gün ile tohumun saf fonksiyonu, ama karıştırma sabiti ayrı. Aynı tuz
kullanılsaydı ağır teklifin geldiği gün daima hırsızlık da olurdu ve iki sistem tek bir
sisteme dönüşürdü. Test bunu tutuyor: olay günleri ağır teklif günlerinin alt kümesi değil.

### Ölçüm (400 dojo × 60 gün, `patrol`)

| Günlük olay olasılığı | Bitiş kasası | Sermayesini koruyan | Aç gün | Kapanan dojo |
|---|---|---|---|---|
| %0 | 945 | %66.2 | %4.8 | %1.2 |
| **%15 (seçilen)** | **766** | **%58.5** | **%6.7** | **%2.2** |
| %30 | 562 | %44.8 | %9.4 | %3.0 |

%15'te tamponun yaklaşık beşte biri aksiliklere gidiyor. Baskı hissediliyor ama kasayı
belirleyen kalem hâlâ ilaç ve savaşçı — olaylar oyunu tek başına bitirmiyor.

Ölçüm sırasında bir şey daha görüldü: teklif kipinde (dojo zaten kasası boş yaşarken)
aksiliklerin etkisi neredeyse yok — boş kasadan çalınacak bir şey yok. Yani olay tablosu
**varlıklı** dojo'yu cezalandırıyor, batmakta olanı değil. Tampon baskısı tam olarak bu
demek.

Doğrulama: build 0 hata / 0 uyarı, `dotnet test -c Release` 378/378 yeşil
(+10: `DayEventTests`). Yeni dosyada `dotnet format` temiz.


### Ek (aynı gün) — şiddet rastgeleleşti, pas düştü

İki düzeltme geldi:

- **Pas olayı kaldırıldı.** Kuşamın yıpranması zaten dövüşten geliyor; ikinci bir kaynak
  gerekmiyordu.
- **Oranlar sabit değil.** Yazılı sayılar artık **üst sınır**: hırsızlık kasanın en fazla
  %12'sini, bozulma ve bulanan kuyu o günün faturasının en fazla iki katını, hastalık en
  fazla 3 günü alır; gerçek miktar her seferinde sıfır ile üst sınır arasında çekilir.
  Gerekçe: sabit oran aksiliği hesaplanabilir bir vergiye çevirir, oyuncu kaybı baştan
  bilirse tampon tutmak karar değil aritmetik olur.

Ölçüm (400 dojo × 60 gün, `patrol`, %15): bitiş kasası 815 altın, sermayesini koruyan
%60.8, aç gün %6.1, kapanan dojo %1.5 — beklendiği gibi sabit oranlı hâlden (766 / %58.5)
biraz daha hafif, çünkü kayıp artık ortalamada üst sınırın yarısı.

Test sayısı 378 (`DayEventTests` 10 test; pas testi düştü, şiddetin sabit olmadığını
tutan test eklendi).


### Karar: boş kasada hırsızlık boşa düşsün (2026-09-04)

Ölçümde "kasası boş dojo hırsızlıktan etkilenmiyor" diye not düşülmüştü; bunun bir eksik
değil **karar** olduğu netleşti. Kasanın boş olması kalıcı bir hâl değil — oyuncu zırh,
onarım ya da yeni savaşçı için para biriktirdiğinde kasa dolar ve hırsızlık tam o anda
ısırır. Olay böylece biriktirme kararının bedeli oluyor.

Değerlendirilip **reddedilen** iki alternatif:

- Boş kasada hırsızın erzağa el atması (o günün faturasını artırması): batmakta olan
  dojo'yu ikinci kez cezalandırırdı ve olayın "biriktirdiğin şeye vurur" anlamını silerdi.
- Toplu alım + ambar kapasitesi ile gerçek bir stok tamponu kurmak: fikir olarak duruyor
  ama ekonomiye yeni bir karar ekler, bu yüzden kendi ölçüm turunu ister — bugünkü
  kilitli fiyatlarla karıştırılmadı.


## 2026-09-04 (dördüncü tur) — Savaşçı pazarı

Domina'nın köle pazarı araştırıldı ve modeli birebir alındı (kaynak: Steam tartışmaları ve
oyuncu rehberleri). Oradaki üç kural: adaylar rastgele statlarla gelir ve statlar alım
öncesi görünür, pazarın kalitesi oyuncunun mevcut kadrosunu takip eder, ve seviye başına
gelişim hızı savaşçıya göre değişir.

Yeni dosya `Domina.Core/Dojo/RecruitMarket.cs`; `Warrior` bir `Talent` alanı kazandı
(kayda giriyor, dövüş okumuyor — antrenman okuyacak).

### Üç kural koda geçti

- **Fiyat stattan çıkıyor.** Adayın taban savaşçıya göre skoru fiyatı belirliyor; yetenek
  de fiyata giriyor ama yarım ağırlıkla (yetenek bir vaat, stat elde olan).
- **Pazar kadroyu takip ediyor** (%70). Kadro tamamen ölürse pazar acemi seviyesine
  düşüyor, sıfıra değil — çöken dojo'nun toparlanma yolu kapanmasın diye.
- **Liste iki günde bir yenileniyor ve gün içinde donuyor.** Donmasaydı, pazar kadro
  ortalamasını takip ettiği için bir aday almak kalan adayları değiştirirdi: oyuncu ucuz
  birini alıp listeyi istediği kadar çevirebilirdi. Test bunu tutuyor.

### Ölçüm iki şeyi ortaya çıkardı (400 dojo × 60 gün, `patrol`)

| Alım politikası | Bitiş kasası | Sermayesini koruyan | Ölüm (dojo başına) | Kapanan dojo |
|---|---|---|---|---|
| Sabit fiyat, sabit stat (eski) | 815 | %60.8 | 6.21 | %1.5 |
| Pazardan altın başına en çok stat | 354 | %26.5 | 7.16 | %11.8 |
| Pazardan parası yeten en iyisi | 705 | %52.2 | 5.93 | %4.8 |

1. **Eski model yerine koymayı sübvanse ediyormuş:** 150 altına veteran kalitesinde
   savaşçı geliyordu. Pazar bunu gerçek fiyata çekince dojo zorlanıyor — bu bir denge
   bozulması değil, gizli bir sübvansiyonun görünür olması.
2. **"Ucuz ham al, eğit" stratejisi şu an kaybediyor** çünkü ham aday gelişmiyor:
   antrenmanın stat etkisi yazılmadı. İki stratejinin rakip olması tasarımın hedefi;
   354'e karşı 705 altınlık fark, antrenman sisteminin kapatması gereken boşluğun ölçüsü.
   Sıradaki iş bu.

`Domina.Sim` iki yeni düğme kazandı: `--market on|off` ve `--market-pick value|best`.

Doğrulama: build 0 hata / 0 uyarı, `dotnet test -c Release` 388/388 yeşil
(+10: `RecruitMarketTests`). Yeni dosyalarda `dotnet format` temiz.
