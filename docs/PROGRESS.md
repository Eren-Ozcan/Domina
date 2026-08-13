# Durum Kaydı

Son güncelleme: 2026-08-13 (silah yeterliliği karara bağlandı, GDD'ye işlendi)

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

Doğrulama: `dotnet build` → 0 hata / 0 uyarı, `dotnet test` → 173/173 yeşil,
`dotnet format --verify-no-changes` temiz.
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

## Bekleyen karar — uzuv kaybı oranı nasıl yükselecek

**Sonraki oturum bu soruyla açılacak.** Hedef: makul oynayan oyuncunun savaşçılarının
kabaca **%5'i** sakat dönsün. Gerçekçi oyuncu modelinde (`losing:0.7`) ölçülen: **%1.3**.

**Tuning ile çözülmüyor.** `--sever` 0.35 → 0.95 yapıldığında uzuv kaybı yalnızca
%1.30 → %1.94 oldu ve doygunluğa girdi. Darboğaz kopma zarı değil: savaşçı, oyuncu
müdahale ettikten *sonra* ağır darbe yiyecek kadar sahada kalmıyor. Uygulanan tek
değişiklik `GrievousSeverityThreshold` 0.28 → 0.20.

Dört seçenek kondu, hiçbiri seçilmedi:

| Seçenek | Ne değişir |
|---|---|
| **A · olduğu gibi bırak** | Oran oyun tarzının sonucu olur: temkinli oyuncu (%5.6) sakat getirir, gidişata bakan (%1.3) ceset |
| **B · kaçış penceresini uzat** | Kaçan daha yavaş çıkar ya da düşman peşinden gelir; hem ölümü hem sakatlığı artırır, "kaçmak da tehlikeli" tonunu güçlendirir |
| **C · künt silah kalıcı yaralanma yapsın** | GDD §7 zaten "künt → kırık/sersemleme" vaat ediyor ama çekirdekte yok; Oni'nin tetsubo'su şu an neredeyse hiç sakatlamıyor (çarpan 0.15). Kesici sayılarına dokunmadan ikinci bir kalıcı hasar kaynağı |
| **D · müdahale refleks penceresi olsun** | Ağır darbeden *sonra* N saniye içinde basmak. Oran üzerinde en güçlü kontrol, ama GDD §5'in "tek tuş, anlık karar" ilkesini QTE'ye yaklaştırır |

Öneri: **C + B**. D en güçlü kontrolü verir ama kullanıcının Domina'da kaçındığı
QTE çizgisine yaklaşır.

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
