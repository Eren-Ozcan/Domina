# Tasarım Kararları (Karar Defteri)

> Bu dosya **kilitlenmiş** tasarım kararlarını tutar. Yol haritası için `ROADMAP.md`.
> Açık kalan kararlar en altta "Açık Kararlar" bölümünde.

## 1. Konsept

Yokai temalı, **eğitmen/okul yönetimi + tam otomatik dövüş** oyunu. Oyuncu bir dojo
(okul) yönetir, samuray savaşçıları yetiştirir, onları faz-faz ilerleyen seferlere
gönderir. Dövüşe müdahale neredeyse yoktur — asıl oyun **geliştirme ve karar** katmanındadır.

Yayıncı Twitch/Kick'e bağlandığında chat savaşçı havuzunu, ekonomiyi ve savaşçıların
yaşam/ölüm kararlarını etkiler. **Yayınsız tek oyunculu mod tam eşdeğerdir** —
chat'in yaptığı her şeyi AI seyirci simüle eder.

### Referans oyunlar ve neyi alıyoruz
| Oyun | Aldığımız |
|---|---|
| Domina | Okul/eğitmen yönetimi, otomatik dövüş, chat entegrasyonu felsefesi, pes etme, gore |
| Darkest Dungeon | Üs + roster + 1-3 kişilik sefer + permadeath |
| Battle Brothers | Kalıcı sakatlık (uzuv kaybı sonrası yaşamaya devam) |
| Hades | Faz/oda bazlı sefer ilerlemesi |

### Domina'dan kasıtlı farklar
- Rakip **insan değil, yokai** (Domina'da insan vs insan arena)
- Uzuv kaybı Domina'da sadece **ölüm gore'u**; bizde **hayatta kalıp kalıcı sakat kalma** var
- Pes etme Domina'da **mash-QTE**; bizde **tek tuş karar**
- Chat isim havuzunda Domina modeli **korunuyor** (herkes dahil + `!no` ile çıkış)
- Motor: Domina C++/Allegro; biz Godot 4 + C#

---

## 2. Teknik Kararlar

| Konu | Karar |
|---|---|
| Motor | **Godot 4.x** |
| Dil | **C#** (.NET) — kullanıcının TS geçmişine en yakın tipli dil |
| Platform | **Steam** (Windows öncelikli) |
| Mimari | Çekirdek simülasyon **Godot'suz saf C#** — motor bağımsız, unit test edilebilir |
| RNG | **Seed'li deterministik** — dövüş tekrar oynatılabilir/debug edilebilir |
| Chat | Platform-bağımsız **adapter katmanı**; Twitch ve Kick aynı iç event'lere düşer |
| Kayıt | **Versiyonlu save + merge-on-load + try/catch** (Domina projesindeki kalıp) |
| Görsel teknik | **Saf 2D, cutout/iskelet animasyon** (düz `Node2D` hiyerarşisi — Skeleton2D/Bone2D **değil**) |

> **Neden Skeleton2D değil:** Bone2D mesh deformasyonu içindir; bizim ihtiyacımız uzvu
> deforme etmek değil **koparmak**. Düz `Node2D` zincirinde kopma = düğümü zincirinden
> ayırmak, ki aşağıdaki gerekçenin tarif ettiği şey tam olarak bu. Kolunu kaybeden
> savaşçının silahı da zincirle birlikte gider.

### Neden runtime-skeletal, baked-frame değil
Domina kare-bazlı sprite sheet kullanıyor (her animasyon karesi önceden pişirilmiş).
Bizde uzuv kaybı **kalıcı** olduğu için kare-bazlı gidilirse her kombinasyon
(sağ kolsuz, sol kolsuz, tek bacaklı, kolsuz+bacaksız...) için tüm animasyon setinin
ayrı ayrı çizilmesi gerekir → kombinatoryal patlama.

İskelet-bazlı cutout'ta uzuv kopması **çalışma anında bir node'u ayırmak**tır;
yeni sanat varlığı gerekmez. Sadece birkaç özel animasyon (tek elli saldırı,
topallama) eklenir. Bu, estetik değil **kalıcı sakatlık mekaniğinin zorunlu kıldığı**
teknik karardır.

Referans oyunlar: Darkest Dungeon (birebir bizim yapı), Rayman Origins/Legends
(kalite tavanı), Ori, Hades, Guacamelee.

---

## 3. Tema

**Yokai / Samuray.** Oyuncu bir dojo'nun sensei'si; savaşçılar Japon mitolojisinin
yokai'lerine karşı savaşır. Onur (bushido) kavramı hem ekonominin hem yaşam/ölüm
kararının merkezinde.

Neden: bestiary çok geniş (Oni, Kitsune, Tengu, Kappa, Yuki-onna, Nue...), Steam'de
kanıtlanmış talep, Hades ile doğrudan kıyaslanma riski yok, Domina'dan tam kopuk.

---

## 4. Savaşçı ve Dövüş

### Statlar (savaşçı başına)
- **HP** — can
- **Saldırganlık (Aggression)** — saldırı sıklığı/agresiflik
- **Savunma (Defense)** — alınan hasarı azaltır
- **Kaçınma (Evasion)** — dodge/roll denemesi şansı, stamina harcar
- **Güç (Strength)** — hasar
- **Stamina** — koşma/yuvarlanma/saldırı tüketir; azaldıkça hasar ve isabet düşer
- **Onur (Honor)** — 0-100, başlangıç 50 (bkz. §6)

### Arena bir düzlem — savaşçılar gerçekten yürür

Dövüş soyut değil **uzamsal**: her savaşçının arena düzleminde bir konumu var
(X hat boyunca, Y derinlik). Fizik motoru yok — kendi kinematiğimiz, sabit tick,
tam determinizm.

| Kural | Sonucu |
|---|---|
| **Silah menzili** | Uzun silah uzaktan vurur, kısa silah yaklaşmak zorundadır. Menzil dışındayken saldırı **başlamaz** |
| **Hedef = en yakın düşman** | Kural uzamdan çıkar, ayrıca yazılması gerekmez. Hedef yapışkandır: ölene/kaçana kadar korunur, yoksa savaşçı iki düşman arasında salınıp hiç vuramaz |
| **Vuruşa kilitlenmek yer bağlar** | Hamleyi başlattıysan yürüyemezsin; hedef menzilden çıkarsa kılıç **boşluğa iner** |
| **Kuşatma** | Arkadan gelen vuruş daha isabetli, daha ağır, ve **kaçınılamaz** |
| **Kişisel alan** | Savaşçılar üst üste binmez |
| **Kaçış gerçek mesafedir** | Sayaç değil: kaçan arenayı gerçekten terk etmek zorunda |

> **Kuşatmanın asıl bedeli kaçışta ödenir:** çekilme komutu verildiğinde
> **menzilindeki her düşman** bedava bir vuruş kazanır. Çevrildiysen kaçmak bir
> darbe değil üç darbe demektir. "Kaçmadan önce kuşatılma" böylece gerçek bir
> uzamsal karar oluyor.

> **Neden Godot fiziği değil:** çarpışma çözümü sürümden sürüme ve platformdan
> platforma değişir; "aynı seed = aynı dövüş" garantisi ölürdü. Uzam çekirdekte,
> saf C# ve deterministik.

> **Ölçülen maliyet:** 16.000 dövüş/sn'den **8.700 dövüş/sn**'ye indik. Kabul kriteri
> "10.000 dövüş < 10 sn" — hâlâ sekiz katı üstündeyiz.

Kamera hâlâ yandan bakar; derinlik ekranda dikey kayma + hafif ölçek + çizim sırası
olarak gösterilir (2D brawler sahnelemesi). Düzlem gerçek, görüntü düz — yani kesilmiş
kâğıt parçalarından kurulu rig bozulmaz (§12).

### Hücum (charge) — mesafenin karşılığı

Herkesin tek bir sabit hızda yürüdüğü bir arenada mesafenin anlamı yok. Hücum,
uzağa düşmüş savaşçıya mesafeyi bir fırsata çevirme yolu verir — bedeli kendini
savunmasız bırakmaktır.

| Kural | Karar |
|---|---|
| **Tetik** | `Idle` durumdaki savaşçı hedefine bir **mesafe eşiğinden** uzaksa, her karar anında seed'li bir **olasılıkla** hücuma kalkar. Eşiğin altında asla hücum edilmez — zaten menzile varılıyor. **Fırlatma önceliklidir:** atacak mermisi olan atar, hücuma kalkmaz |
| **Hız** | Hücum sırasında hareket hızı bir **çarpanla** artar (`Speed` stat'ının üstüne, `RetreatSpeedMultiplier` ile aynı yerden) |
| **Ödül** | Varışta yapılan **ilk vuruş hasar çarpanı** kazanır (momentum). Uzuv kaybı riski zaten hasar/maxHP oranından geldiği için (§7) sakatlanma olasılığı **kendiliğinden** artar — ayrı kural yazılmaz |
| **Bedel** | Savaşçı kaçınamaz, bloklayamaz; yol boyunca **menzilinden geçtiği her düşman** ona bir kez **fırsat saldırısı** yapar — kaçış penceresiyle (§5) aynı mekanik |
| **Iskalama** | Hedef ölür, sahadan çıkar ya da **kaçmaya başlarsa** hücum boşa gider: savaşçı `Idle`'a düşer, ödül alınmaz. Bir süre sınırı da vardır — o kadar sürede varılamayan hücum aynı şekilde boşa gider |
| **Kaçana hücum yok** | Kaçmakta olan hedef hücum başlatmaz ve başlamış hücumu bitirir. Ölçüldü: aksi hâlde hızlanma kaçışın tek ayar düğmesini (`RetreatSpeedMultiplier`, §5) devre dışı bırakıyor |
| **"Çek" komutu hücumu keser** | Hücum savaşçının **kendi kararlarına** karşı taahhütlüdür — ondan vazgeçip başka hamle seçemez — ama oyuncunun komutu ayrı bir eksendir ve hücumu anında keser. Bedeli ödenmiştir: yenen bedava vuruşlar geri gelmez, hasar çarpanı harcanmaz |

**Taahhüt nerede duruyor:** savaşçı hücuma kalktıktan sonra fikir değiştirip başka bir
hamle seçemez; hücum ya varır ya boşa gider. Kesen tek şey oyuncunun "çek" komutudur.
Ölçüldü — komut da kesemeseydi, tuşa basıldığı anda koşmakta olan savaşçı hücumu
bitirmek, düşman hattına varmak ve oradan kaçmaya başlamak zorunda kalıyordu; bu, **ilk
temasta basmayı geç basmaktan ölümcül** yapıp §5'in merdivenini ters çeviriyordu
(3v3, 2.000 dövüş: ilk temasta %18,9 ölü, 2. saniyede %13,2). Komut hücumu kesince
merdiven yerine oturdu: **%1,0 → %13,2 → %36,6**.

**Sayılar açık:** mesafe eşiği, olasılık, hız çarpanı, hasar çarpanı ve süre sınırı
`CombatTuning` üzerinden ayarlanır ve **`Domina.Sim` ile ölçüldükten sonra** kilitlenir —
önden seçilmez (Açık Karar #11).

### Dövüş çözümlemesi (tam otomatik, manuel nişan yok)
Her vuruşma anı sırayla çözülür:
1. Saldıranın **Saldırganlık**'ı saldırı sıklığını belirler
2. Savunanın **Kaçınma**'sı için zar → kaçındıysa **stamina harcanır, hasar yok**
3. Kaçınamadıysa **Savunma** hasarı azaltır
4. Kalan hasar HP'den düşer
5. Vuruş yönü/açısı görsel çeşitlilik içindir, **mekanik sonucu etkilemez**

### Sınıf yok
Temel sürümde **tek karakter sınıfı: samuray**. Büyücü vb. varyasyonlar sonraya
(DLC/genişleme) bırakıldı. Savaşçılar sınıfla değil **silah yeterliliğiyle** ayrışır.

### Silah yeterliliği (weapon proficiency)

Yeterlilik **silah adı başına değil, kavrayış (grip) başına** tutulur. Üç hat:

| Hat | Kapsam | Durum |
|---|---|---|
| **Tek el** | Katana, wakizashi, tantō, kama, ono, tekagi | Aktif |
| **Çift el** | Nodachi, naginata, kanabō, bō/jō | Aktif |
| **Fırlatma** | Shuriken, kunai, yumi, fukiya | **Aktif** — çekirdek mermiyi 2026-08-14'te kazandı |

**Yeterlilik yalnızca isabeti ve saldırı hızını etkiler; ham hasarı asla etkilemez.**
Hasara da işlerse Güç (Strength) ile çarpışıp dengeyi patlatır.

**Büyüme:** hem dövüşte kullanım hem dojo antrenmanı. Antrenman şart — sakat bir
savaşçının yeni hattını sıfırdan eğiteceği yer orası; yoksa doğru oynanış
"sakat kalacaksa bırak ölsün" olur ve §7'nin "emekliye ayır mı" sorusu sahteleşir.

**Acemi cezası:** yeterlilik 0'da silah **kuşanılabilir**, ama isabet belirgin düşüktür.
"Eşiğin altında hiç kuşanamaz" kuralı roster'ı kilitler — yeni savaşçı hiçbir şey tutamaz.

**Hatlar arası aktarım yok** — yeterlilik kavrayışa bağlıdır. Aktarım statlar üzerinden
dolaylı olur: Güç, Savunma, Kaçınma, HP ve Stamina hatta bağlı değildir ve kavrayış
değişince kalır.

> **Neden kavrayış başına — ve neden riskli:** `Disability.BlocksTwoHandedWeapons` zaten
> var. Yani **çift el ustası kolunu kaybederse ömrünün emeğini kaybeder.** Riskin bu
> keskinliği kasıtlı; ama yıkıcı değil: savaşçı sıfırdan başlamaz, statlarını ve
> deneyimini korur — **acemi isabetli bir veteran** olarak döner. Tek el hattı da
> sıfırdan eğitilebilir kalmalıdır (yukarıdaki antrenman kuralı).
>
> Çift el, kırılganlığının bedelini **hasar tavanıyla** öder: Nodachi 34 / Katana 22
> farkı korunur. Kesin sayılar Faz 9'un işi.

---

## 5. Pes Etme ("Kaç") — Savaştaki Tek Müdahale

- **Tek tuş**, anlık karar. Mash/QTE **yok**. Otomatik eşik **yok** —
  oyuncu basmazsa savaşçı gerçekten ölür.
- **Savaş başlamadan çekilmek yok.** Tuş **ilk isabete kadar kapalıdır**: eşik
  hamle değil, kan. Iskalanan hamleler boyunca ekip hâlâ ucuza çekilebilir —
  zırhın karşılığı budur.
- Komut **tüm ekibi** kapsar: bir kez "kaç" denince sahadaki 1-3 savaşçının
  hepsi çekilir ve hepsi onur kaybeder. Tek bir savaşçı ayrıca çekilemez.

- Komut **seferi de bitirir**: o odadan sonrası iptal olur, ekip dojo'ya döner ve
  **o sefer ödülü alınmaz** (bkz. §10).

> **Neden ekip bazlı:** savaşçı bazlı olsaydı doğru oynanış "yara alanı hemen çek,
> kalanla devam et" olurdu — kayıpsız, sürekli tekrarlanan küçük bir optimizasyon.
> Ekip bazlı komut kararı **nadir ve ağır** yapar: bir savaşçıyı kurtarmak, seferi
> ve tüm ekibin onurunu bırakmak demektir. Uzuv kaybı mekaniğinin bedeli de böylece
> gerçek bir bedele bağlanır.

### Tuş ne zaman açılır

| An | Tuş |
|---|---|
| Savaş başladı, henüz kimse vurmadı | **Kapalı** — üstünde "SAVAŞ BAŞLAMADI" yazar |
| İlk isabet düştü (hangi taraf vurursa vursun) | **Açık**, dövüş bitene kadar açık kalır |

Kapalı tuşa basmak sessizce yutulmaz; çekirdek basışı sayar ve olay üretir, metni
arayüz yazar:

- **Oyun başında en fazla 3 kez** kuralı öğreten kısa bir bilgi çıkar, sonra susar.
  Bilineni tekrarlamak bilgi değil gürültüdür.
- **Aynı dövüşte üst üste 11. basışta** alaycı bir cevap gelir ve
  **"Dereyi görmeden paçaları sıvama"** başarımı açılır.

> **Neden hamle değil isabet:** yayını çeken düşman 30 m öteden nişan alırken tuşun
> açılması gerekiyor; erişim eşiği menzilli düşmanda yanlış çalışırdı. İsabet eşiği
> hem yakın dövüşte hem menzilde aynı şeyi söyler: *sana dokundular.*

> **Neden hiç açılmıyor değil de kapalı duruyor:** tuş gizlenseydi kuralın varlığı
> öğrenilmezdi. Oyuncu tuşun yokluğunu değil, ne zaman geleceğini merak etmeli.

### Animasyon kesme (cancel window)
| Durum | Davranış |
|---|---|
| Idle, yaklaşma, saldırı sonrası toparlanma, blok | Komut **anında** işlenir |
| **Hücum** (§4) | Komut **anında** işlenir — koşu kesilir, hücum boşa gider |
| Saldırı vuruşuna kilitli an | Komut **buffer'lanır**, mevcut hareket bitince kaçış başlar |
| Blok duruşu | Neredeyse anında kaçışa geçer |

> **Kod ile fark (açık):** blok burada ayrı bir durum sayılıyor, çekirdekte değil —
> şu an Savunma (Defense) statının içinde eriyor. Ayrı bir durum olarak gerekiyor mu,
> yoksa bu tablo mu güncellenmeli: Faz 9'a kadar karara bağlanacak.

### Kaçışın bedeli bir merdivendir

Tuş bir takas değildir — "şu kadar verip şunu al" diye bir kural yoktur. Basıldığı anda
sonucu **bilinmez**; çekilme, aşağıdaki merdivenin bir basamağında biter. Basma anı seni
bu merdivende tek yönlü aşağı kaydırır.

| Basamak | Ne olur |
|---|---|
| **1** | ~~Herkes kaçtı, kimse yara almadı~~ — **artık ulaşılamaz**, aşağıya bakınız |
| **2** | Herkes kaçtı, yaralılar var |
| **3** | Herkes kaçtı, uzuv kaybeden(ler) var |
| **4** | Ekibin bir kısmı kaçtı, kalanlar öldü — kaçanlar sağlam |
| **5** | Ekibin bir kısmı kaçtı, kalanlar öldü — kaçanlar uzuv kaybetmiş |
| **6** | Kimse kaçamadı |

Ölçüldü (3v3, 20.000 dövüş, hafif kuşam, temas kuralı devrede). Sütunlar tuşun ne
zaman basıldığını temsil eder:

| Basamak | Tuş açılır açılmaz | 2. sn | Can %50 | Sayıca geri kalınca |
|---|---|---|---|---|
| 1 · hepsi kaçtı, yarasız | **%0.0** | %0.0 | %0.0 | %0.0 |
| 2 · hepsi kaçtı, yaralı | %92.8 | %91.8 | %10.6 | — |
| 3 · hepsi kaçtı, uzuv kayıplı | %7.2 | %8.2 | %2.3 | — |
| 4 · kısmi kaçış, uzuvsuz | — | — | %71.1 | %11.1 |
| 5 · kısmi kaçış, uzuv kayıplı | — | — | %12.9 | %2.7 |
| 6 · kimse kaçamadı | — | — | %3.1 | **%86.2** |

> **Birinci basamak kapandı (2026-08-29).** Eskiden temastan önce basmak dövüşlerin
> **%35'inde** ekibi tertemiz eve getiriyordu; ölçüm 20.000 dövüşte **sıfır** uzuv
> kaybı, **sıfır** ölüm veriyordu. Yapısal bir dalı vardı: kaçış zarı en fazla 12 hasar
> verir, 100 azami cana karşı şiddeti 0.12'de kalır ve 0.20'lik ağır darbe eşiğini hiç
> geçmez. Yani "silah dokunmadan uzuv gitmez" kuralı, temas öncesi kaçışı bedelsiz
> kılıyordu.
>
> Çözüm sakatlanmaya yeni bir zar eklemek **değil**, tuşu geciktirmek oldu: kimse
> dokunmadan çekilme diye bir şey yok. Ekranda sebepsiz kopan uzuv çizilmiyor
> (ortada yalnızca `Stumble` var, kopma animasyonu yok) ama basamak da bedava değil —
> tuş açılır açılmaz basıldığında bile ekiplerin **%7.2'si** en az bir uzuv kaybediyor.

### Kaçışı bedelsiz olmaktan çıkaran üç şey

Merdivenin üst ucu bir noktada **%100 yarasız çıkış** veriyordu: kaçmanın hiçbir riski
yoktu. Sebebi tek tek bulundu ve üçü birden düzeltildi.

| Neden bedelsizdi | Ne eklendi |
|---|---|
| Hız tek bir sabitti; kovalayan ile kaçan aynı hızda gidiyor, net kapanma sıfır kalıyordu | **Hız bir stat oldu.** Oni yavaş (25), Kappa orta (55), Tengu hızlı (85). Kaçan ayrıca yavaşlar — sırtı dönük koşuyor |
| Yakın dövüş arenanın uzak yarısına ulaşamıyordu; belli bir mesafeden sonra kaçan dokunulmazdı | **Fırlatma uyandı** (§4). Mermi havada süre geçirir; hedef uçuş sırasında kaçabilir, ölebilir, sahadan çıkabilir |
| Arenayı terk etmenin kendisi hiçbir şeye mal olmuyordu | **Kaçış zarı.** Burkulan ayak, dönüş yolunda kanayan yara. Öldürmez — canı 1'in altına indirmez |

> **Kovalamacanın ayrı bir kuralı var.** Normalde hamleye kilitlenen savaşçı yürüyemez.
> Kaçan bir hedefe karşı bu askıya alınır: kovalayan kılıcını savururken de koşar. Bu
> olmadan avcı yetişip hamleye başlıyor, hamle boyunca donuyor, kaçan menzilden çıkıyor
> ve kılıç **her seferinde** boşluğa iniyordu — yani yetişen düşman hiç vuramıyordu.
> Toparlanmada ise durur; yoksa kovalayan hiç yavaşlamıyor ve kaçış tamamen çöküyor
> (ölçüldü: sayıca geri kalınca çekilen ekibin %76'sı kırılıyordu).

> **Erken basmak hâlâ en iyi seçenek** ama tertemiz çıkış diye bir şey kalmadı: tuş
> açılır açılmaz basıldığında ekiplerin **%92.8'i yaralı**, **%7.2'si uzuv kayıplı**
> dönüyor. Kaçmanın asıl bedeli yine de **seferin kendisi** — sefer iptal olur, ödül
> alınmaz, tüm ekip onur kaybeder.
>
> Geç basmanın bedeli ise sert: sayıca geri kaldıktan sonra çekilmek dövüşlerin
> **%86'sında** ekibin tamamına mal oluyor. Merdivenin söylediği şey net — *kaçacaksan
> erken kaç.*

### Kaçmanın onur bedeli

Çekilmek onur düşürür. İki ayrı kalemden:

| Kalem | Nerede | Not |
|---|---|---|
| Performans içindeki ceza | `HonorTuning.EscapePerformancePenalty` | İsabet oranıyla harmanlanır: iyi dövüşüp sonra çekilen, kötü dövüşenden hâlâ iyidir |
| Tuşun düz bedeli | `HonorTuning.RetreatHonorPenalty` | Performanstan bağımsız, her çekilişte uygulanır |

İkinci kalem ayrı duruyor çünkü tek başına performansa gömülü ceza yeterince isabetli
bir savaşçının kaçtığı hâlde onur **kazanmasına** izin veriyordu. Çekilmenin bedeli,
ne kadar iyi dövüşüldüğünden bağımsız olarak görünmeli.

> **Sayı henüz kararlaştırılmadı** (Açık Karar #8). Koddaki değer yer tutucudur;
> ölçüm-önce-karar kuralı gereği Faz 9'da oturacak.

### Kaçış penceresi (savunmasızlık)
Kaçış başladığı andan arenadan çıkana kadar:
- Savaşçı **Kaçınma ve blok kullanamaz**
- Rakip **artırılmış isabet şansıyla** vurabilir
- **Menzilindeki her düşman** bir fırsat saldırısı (opportunity attack) kazanır —
  yani bedel kaç düşmanın seni çevirdiğine bağlı (§4)
- **Daha hızlı düşman peşine düşer** ve yetişirse vurmaya devam eder
- **Menzilli düşman arkandan atar**; mermi sen sahadan çıkmadan yetişirse vurur
- Kaçış sayaçla değil **mesafeyle** biter: savaşçı arenayı gerçekten terk etmeli,
  bu süre boyunca savunmasız kalır
- Arenayı terk ederken bir **kaza yarası** zarı atılır (öldürmez)

---

## 6. Onur Sistemi (Bushi / Ronin)

### Ölçek
0-100 kalıcı stat, başlangıç 50. Zamanla nötre doğru yavaş **decay** (troll saldırısı
kalıcı ceza olmasın; sadece sürekli onursuzluk seppuku'ya götürsün).

### Chat komutları
| Komut | Etki |
|---|---|
| `!bushi` | Aktif dövüşteki savaşçı(lar)a onur (+) |
| `!ronin` | Aktif dövüşteki savaşçı(lar)a onur (−) |
| `!bushi-<isim>` | Hedefli, dövüş dışındaki savaşçıya (+) — **küçük etki** |
| `!ronin-<isim>` | Hedefli (−) — **küçük etki** |

- Dövüş performansından gelen onur değişimi **büyük**; hedefli komutlar **küçük** etkilidir
  (griefing'e karşı).
- İsim bulunamazsa (ölmüş/yok) **sessizce yok sayılır** — chat'e geri bildirim yok.
- Bir isim aynı anda **en fazla bir canlı savaşçıya** ait olabilir → isim çözümleme belirsizliği yok.
  (X ölür, sonra yeni bir X gelebilir.)

### Ekonomi etkisi (ödül çarpanı)
```
bushiOrani = bushi / (bushi + ronin)          // hiç oy yoksa nötr
odulCarpani = clamp(0.5 .. 1.5)               // %100 ronin = 0.5x, %100 bushi = 1.5x
```
Ham sayı değil **oran** kullanılır → küçük ve büyük chat'ler arasında adalet.
Alt/üst sınır spam ile uçlara çekilmeyi engeller.

Aynı oran pes etme sonrası **hayatta kalma şansına** da uygulanır (onurlu çekilme mi,
korkakça kaçış mı).

### Seppuku
Onur eşiğin altına düşerse savaşçı **hemen ölmez** → oylamaya gider.

**Kuyruk mekaniği:**
1. Eşiğe düşen savaşçı **kuyruğa** girer
2. Aktif dövüş varsa **dövüş bitene kadar bekler**
3. Dövüş bitince **60 saniyelik** oylama penceresi açılır
4. Aynı anda **asla iki oylama olmaz** — birden fazla savaşçı eşiğe düşerse sırayla işlenir
5. Oylama penceresinde `!bushi` / `!ronin` **sadece bu oylamaya** sayılır
6. **Kullanıcı başına tek oy** (spam koruması)
7. Bushi çoğunluk → **af**, onur eşiğin biraz üstüne toparlanır
   Ronin çoğunluk → **seppuku** (permadeath)
8. **Tek bir oy bile** geçerlidir; minimum katılım eşiği yok
9. **Sıfır oy** gelirse **AI karar verir**

**Cooldown:** Affedilen savaşçı **15 dakika** bağışıklık kazanır. Bu sürede onuru
eşiğin altına inse bile yeni oylama tetiklenmez.

---

## 7. Yaralanma ve Uzuv Kaybı

### Tetikleyici
Savaşın **herhangi bir anında** (ilk darbe dahil) olabilir. Düşük HP ön koşul **değil**.
Tek darbenin **max HP'ye oranı** eşiği geçerse uzuv kaybı riski doğar.

**Riski etkileyen faktörler:**
- Darbe/maxHP oranı (sert vuruş = yüksek risk)
- Silah tipi: kesici (kılıç/balta) → uzuv kaybı; künt (topuz) → kırık/sersemleme
  (**kod ile fark:** künt silahın sersemletme etkisi çekirdekte henüz yok; künt/kesici
  ayrımı yalnızca uzuv kaybı riskini değiştiriyor)
- Zırh/savunma seviyesi riski **azaltır** (ekipmana yatırımı anlamlı kılar)

### Zırh yuva yuvadır

Zırh tek bir sayı değil; **kafa / gövde / kol / bacak** için ayrı parçalardan oluşur.
Hasar azaltımı ve uzuv kopma direnci, darbenin **indiği bölgenin** parçasından okunur.

| Takım | Kafa | Gövde | Kol | Bacak |
|---|---|---|---|---|
| Hafif keikogi | — | keikogi | — | — |
| Dō-maru | — | dō | kote | suneate |
| Ō-yoroi | kabuto | ō-yoroi gövdeliği | ağır kote | ağır suneate |

> **Neden yuva yuva:** direnç tek skaler kaldığı sürece "iyi zırh" tek eksende ilerler ve
> ekipmanın asıl ilginç kararı — **ağır göğüslük, çıplak kollar**: ucuz ve hızlı, ama eve
> kolsuz dönme ihtimali yüksek — hiç var olmaz. Ölçümde bu doğrudan görünüyor: dō-maru'nun
> kafası açık olduğu için göz kaybı, kol ve bacak kaybının aksine ancak yarıya iniyor.

### İsabet bölgesi

Her isabet bir bölgeye iner. Ağırlıklar kasıtlı olarak eşit değil:

| Bölge | Ağırlık |
|---|---|
| Gövde | 45 |
| Bacak | 25 |
| Kol | 20 |
| Kafa | 10 |

> **Neden gövde baskın:** bölgeler eşit olsaydı gövde zırhı dört parçadan yalnızca biri
> olurdu ve değersizleşirdi; zırh yatırımı "hepsini eşit dağıt" gibi düz bir
> optimizasyona dönerdi. Gövde baskın olunca gövde zırhı ana yatırım, uzuv zırhları
> uzmanlaşma kararı olur.

Bölge **hasarı ve zırhı** ilgilendirir; sonuç ağacını değil. Gövdeye inen ağır darbe de
bir uzva mal olur — kopan uzuv o zaman kalan uzuvlar arasından aynı ağırlıklarla seçilir.

> **Ölçülen sebep:** "gövdeye inen darbe koparmasın" denince müdahale neredeyse risksiz
> hâle geldi ve oyuncu zaferi %36'dan %53'e çıktı (10.000 dövüş, 3v3). Oysa §7'nin
> vaadi "tuşa basmak hayat kurtarır ama **bedelsiz değildir**". Bölgenin sonuç ağacını
> ezmesine izin verilmiyor.

### Sonuç ağacı

Ağır darbe kopma zarını tutturduğunda **darbenin öldürücü olup olmadığı** belirleyicidir:

```
ağır darbe + kopma zarı tuttu
  ├─ can > 0  → uzuv gider, savaşçı sahada kalır, DÖVÜŞ SÜRER
  └─ can ≤ 0
       ├─ oyuncu "Kaç"a basmış → uzvunu kaybeder ama YAŞAR
       └─ basmamış             → ÖLÜM (gore/bitiriş animasyonu)
```

| Durum | Sonuç |
|---|---|
| Hafif/orta darbe | Sadece HP hasarı, kalıcı etki yok |
| Ağır darbe, öldürmüyor | **Uzvunu kaybeder ve dövüşmeye devam eder** — tuş gerekmez |
| Ağır darbe, öldürücü + oyuncu zamanında pes ettiyse | **Yaşar ama uzvunu kaybeder** — kalıcı stat cezası + görsel değişiklik |
| Ağır darbe, öldürücü + müdahale yoksa/geç kalındıysa | **Ölüm** |

> **Neden ikiye ayrıldı.** Önceki kuralda uzuv kaybı yalnızca `PlayerIntervened` iken
> oluşuyordu; o bayrağı da sadece "Kaç" tuşu açıyor ve §5 gereği tuş **seferi bitiriyor**.
> Sonuç: **uzuv kaybederek kazanmak matematiksel olarak imkânsızdı.** 20.000 dövüş
> ölçüldü, zafer + uzuv kaybı: **0 kez**. Sakat dönen her savaşçı terk edilmiş bir
> seferin anıtıydı; tek kollu bir şampiyon eve asla gelemiyordu. Bu, aşağıdaki "emekliye
> ayır mı, kullanmaya devam mı" kararıyla çelişiyor — o karar sakat veteranın bir şey
> kazanmış olmasını varsayar.
>
> Tuşun ağırlığı korunuyor. Ama tuş bir **takas değil**: "ölümü uzuv kaybına çevirmek"
> ağacın yalnızca bir dalı. Tuş, sonucu basıldığı anda bilinmeyen bir **çekilme**
> başlatır; bedeli §5'teki merdivende bir yere düşer.

### Ne sıklıkla olmalı

Uzuv kaybı oyunun imza mekaniği; nadir bir sürpriz değil, **düzenli olarak yaşanan bir
sonuç** olmalı. Oran **tek bir sayı değil, kuşamın fonksiyonu** — iyi zırhlı savaşçı eve
sağlam döner, ucuz kuşamlı dönmez.

Ölçüldü (3v3, 20.000 dövüş, `losing:0.7` oyuncu modeli). Denge hedefi bu tablodur:

| Kuşam | Açıkta kalan | Ölüm | **Uzuv kaybı** | Zafer |
|---|---|---|---|---|
| Zırhsız | hepsi | %41.6 | **%8.6** | %68 |
| Hafif keikogi | kol, bacak, kafa | %38.8 | **%6.7** | %70 |
| Dō-maru | kafa | %22.5 | **%2.9** | %89 |
| Ō-yoroi | — | %16.3 | **%0.4** | %96 |

Karışık bir orta-oyun kadrosu (çoğu hafif/orta kuşamda) böylece kabaca **%5** civarında
sakat üretir — ama bu bir sabit değil, oyuncunun ekipman harcamasının sonucu.

> **Tuş oranı belirlemiyor; ölümü belirliyor.** Aynı kuşamda hiç çekilmeyen ile erken
> çeken oyuncunun uzuv kaybı neredeyse aynı (%8.8'e karşı %8.0), ölümü ise %48'den
> %29'a düşüyor. Kural değişmeden önce tersi geçerliydi ve oran tamamen tuşun ne zaman
> basıldığına bağlıydı — o zaman da zafer + uzuv kaybı imkânsızdı.

Kazanılan dövüşlerin **%16.5'i** eve sakat bir savaşçı getiriyor (hafif kuşam ölçümü):
sakat kadro artık yalnızca terk edilen seferlerin değil, **pahalıya kazanılan
zaferlerin** de kaydı.

### Kalıcı cezalar
| Kayıp | Etki |
|---|---|
| Kol | Saldırı gücü düşer, iki elli silah kullanamaz, tek elli animasyon setine geçer |
| Bacak | Kaçınma/çeviklik düşer, topallama animasyonu |
| Göz | İsabet oranı düşer |

Savaşçı kullanılamaz hale gelmez — "zayıflamış ama görev yapabilir" ara durumda kalır.
Oyuncuya **"emekliye ayır mı, kullanmaya devam mı"** kararı bırakır.

### Görsel uygulama
Modüler karakter: uzuvlar ayrı mesh/kemik. Kopma anında özel dismemberment animasyonu +
kan VFX. Sonrasında **kalıcı olarak** eksik-uzuv modeline ve değişmiş animasyon setine geçilir.

### İyileşme
- Yaralı savaşçı X gün sefere çıkamaz (yara ağırlığına göre süre)
- Doğal iyileşme yavaş; **revir/hekim binası** + **ilaç kaynağı** hızlandırır
- İlaç, ekonomiye yeni bir kaynak türü olarak eklenir

---

## 8. Chat Entegrasyonu

### Katılım (opt-out — Domina modeli)
- **Chat'teki herkes varsayılan olarak havuzdadır.** Yeni recruit slotu açıldığında
  sistem chat'te konuşmuş kullanıcılardan rastgele çeker.
- **`!no`** yazan kullanıcı havuzdan çıkar (Domina'daki gibi) — rıza korunur
- **`!join`** öncelik sinyalidir ("beni öne al"), zorunlu değildir
- Havuz boşsa (single-player veya hiç konuşan yoksa) → **Japon isim havuzundan** üretir
- Üretilen/çekilen isim **oyuncu tarafından her zaman düzenlenebilir**
- **Kullanıcı adı filtresi zorunlu** — küfürlü/uygunsuz adlar havuza alınmaz
  (Steam'de yayınlanan bir oyunda gerekli)

> **Neden opt-out, opt-in değil:** Mekaniğin gücü **sürpriz**. Opt-in hem sürprizi
> öldürür hem de chat'in çoğunluğu olan lurker'ları dışarıda bırakır → küçük
> yayınlarda havuz sürekli boş kalır, sistem hep fallback'e düşer.

### Bits / Kicks ("Kahraman Çağır")
- Chat üyesi platformun native bağış sistemiyle (Twitch Bits / Kick Kicks) bağış yapar
- Adı **garantili** olarak rostere girer (rastgele çekiliş beklemez)
- **Bağış miktarı başlangıç eğitim seviyesini belirler**, **üst sınır vardır**
  (tek büyük bağışçı dengesiz karakter yaratamasın)
- Permadeath ve uzuv kaybı **aynen geçerli** → pay-to-win değil, **pay-for-head-start**

### Kapsam kuralı
Tüm chat etkileşimi **yalnızca o yayıncının kendi oturumunu** etkiler. Paylaşımlı/global
ekonomi, kanal puanı sistemi, gerçek para bahsi **yok** (Steam politikası uyumu).

---

## 9. AI Seyirci (Tek Oyunculu Eşdeğerlik)

Gerçek chat yoksa **veya** bir oylamaya hiç oy gelmezse AI devreye girer.

- AI, gerçek chat'in bakacağı **aynı performans sinyallerine** bakar: isabet oranı,
  saldırganlık, alınan hasar, dövüşün "gösterişliliği"
- Kendi bushi/ronin oranını üretir → aynı ödül çarpanı formülü çalışır
- Seppuku oylamasında onur seviyesine **ağırlıklı olasılıkla** karar verir
  (onur ne kadar düşükse seppuku ihtimali o kadar yüksek)

Sonuç: single-player, yayın moduyla **mekanik olarak eşdeğer**. Hiçbir sistem kapalı kalmaz.

---

## 10. Sefer ve Roster

- Üs: dojo — antrenman alanları, revir, eğitmen (sensei) skill tree'si
- Sefere **en fazla 4 savaşçı** gönderilir; sayı oyuncunun kararıdır. Bazı
  karşılaşmalar (düello, baskın) **tam bir sayı dayatır** — o encounter kendi kuralını
  yazar, üst sınır yine 4
- **Sefer = tek oda, tek dövüş** (yönelim, 2026-08-29). Art arda odalardan geçilen bir
  koşu yok; günlük döngüde bir karşılaşma seçilir, dövüş biter, ekip dojo'ya döner
- **Pes etme seferi bitirir:** dövüş terk edilir, o seferin ödülü alınmaz (§5). Tek
  dövüşlük seferde bu ikisi aynı cümle — çekilmek o dövüşün ödülünü siler, başka bir
  şeyi değil
- Üste kalan savaşçılar güvende; **sadece sefere giden ekip** permadeath riskinde
- Permadeath: ölen savaşçı kalıcı olarak gider

> **Tek dövüşlük seferin sonucu:** zincir olmadığı için oda-oda yıpranma yok. Zorluk
> tek karşılaşmanın kendisinden gelmek zorunda; can eritme üzerinden kurulamaz.
> Denge tarafında bu iyi haber — `Domina.Sim` zaten tek dövüş simüle ediyor, zincir
> modellemesi gerekmiyor.
>
### Karşılaşmaya girmenin bedeli (kilitlendi 2026-08-29)

- **Girmek bir gün yer** — kaçılsa da yer. Kaynak muhasebesi gerektirmez, ekonomi
  sayıları henüz açıkken de çalışır, ve zaman zaten en kıt kaynaktır. "Gir–bak–kaç"
  döngüsünü kapatan kalem budur
- **Düşman kadrosu kısmen görünür:** girmeden önce yalnızca kaba bir tehdit işareti
  okunur (yokai türü ya da zorluk bandı). Tam kadro ve statlar görünmez — seçim
  bilgili olur, sürpriz ölmez
- **Günde tek karşılaşma teklifi:** al ya da bırak. Girilmezse gün dojo'da geçer.
  Liste/harita ekranı yok

> Girişin gün yemesi, kaçmanın onur bedeli (§5) ve tuşun ilk temasa kadar kapalı
> olması birlikte keşif döngüsünü kapatır: bakmak için girmek bir gün ve bir dövüş
> göze almak demektir.

### Zorluk eğrisi — boss yok (2026-08-29)

Sefer zinciri olmadığı için boss ritmi diye ayrı bir yapı **kurulmuyor**. Karşılaşmalar
gün geçtikçe **tek bir eğri üzerinde zorlaşır**; ayrı boss encounter'ı, boss takvimi
veya tehdit sayacı yok. Bestiary'deki boss adayları (Gashadokuro, Shuten-dōji,
Yamata-no-Orochi) şimdilik yalnızca eğrinin üst ucundaki güçlü düşmanlardır. Boss
fikri ileride tekrar açılabilir; şu an kural değil.

### Skill tree derinliği
| Katman | Derinlik |
|---|---|
| **Okul + Eğitmen** | **Gelişmiş/derin** — antrenman hızı, tesis kilitleri, ekonomi bonusları, revir iyileştirmeleri |
| **Savaşçı** | **Basit** — temel stat/yetenek ilerlemesi |

Gerekçe: asıl uzun vadeli yatırım okulda olsun → savaşçı ölümü koca bir yatırım kaybı
gibi hissettirmesin, permadeath'in acısı dengelensin.

---

## 11. Ekonomi

Domina'nın modeli referans alınır:
- Gelir: dövüş ödülleri (chat onur çarpanıyla ölçeklenir), isteğe bağlı riskli maçlar
- Gider: ekipman, kaynak (yiyecek/su), **ilaç/tedavi**, savaşçı alımı
- Rastgele olaylar kaynak eksiltebilir → tampon tutma baskısı
- İsteğe bağlı riskli maçlar **reddedilebilir** (Domina'daki gibi risk opsiyonel)

---

## 12. Görsel Stil

**Karanlık Edo ahşap baskı × katmanlı kâğıt tiyatrosu (paper theater).**
Ukiyo-e estetik referans; üretim dili kâğıt tiyatrosu.

### Ayrım: temiz varlık, dokulu ekran

Doku **varlığa gömülmez, ekrana shader olarak uygulanır**.

| Katman | İçerik |
|---|---|
| Varlıklar (uzuv çizimleri) | Düz renk, sert kenarlı iki tonlu gölge, kalın kontur, **gradyan yok**, şeffaf zemin |
| Tam ekran overlay (`CanvasLayer`) | Kâğıt greni, hafif mürekkep yayılması, kenar vinyeti |
| Global renk (`CanvasModulate`) | Günün saati (dawn/gündüz/gece) — **kan/vermilion tint dışında kalır** |

Gerekçe: rig 15 parçayı çalışma anında döndürüyor. Doku ve ışık parçaya pişirilirse
uzuv döndüğünde ışık yönü yanlışlanır ve her parça ayrı ayrı ışıklandırılmak zorunda
kalır — tek kişilik üretimde karşılanamaz. Doku ekrandan gelince varlıklar temiz
kalır, ahşap baskı hissi tek yerden ve bedava gelir. Aynı katman gün döngüsü
varyantlarını da bedava üretir.

### Kâğıt tiyatrosu neden

Rig'in zayıflığını stile çeviriyor. Düz bir uzuv mekanik döndüğünde boyalı bir
figürde "çizim neden böyle dönüyor" diye okunur; kâğıt kesiminde "kâğıt parça
dönüyor" diye okunur. Aynı hareket, kabul edilebilir hâle geliyor.

### Palet — yedi rol

| Rol | Kullanım |
|---|---|
| Kâğıt / zemin | Sıcak kırık beyaz — sahnenin taban değeri |
| Mürekkep / gölge | Zifiri koyu — kontur ve gölge kütlesi |
| Taş / metal | Nötr gri |
| İndigo | **Oyuncu takımı** |
| Ochre | **Yokai / düşman** |
| Toprak / ahşap | Yapı ve mobilya |
| Vermilion | **Yalnızca kan ve kritik vurgu** — başka hiçbir yerde kullanılmaz |

Değer boşluğu figürle zemin arasındadır: sahne açık kâğıt, figür koyu kütle. Renk
değil **değer** ayrımı olduğu için küçültmede hayatta kalır.

### Zırh kademeleri siluetle ayrılır, renkle değil

Renkle ayırmak mümkün değil: palet aynı anda iki takımı da ayırt etmek zorunda. Fark
**hacimden ve şekilden** gelir, kademe başına siluet belirgin şekilde büyür:

| Kademe | Siluet |
|---|---|
| Çıplak | Dar, yumuşak, dökümlü kumaş |
| Deri | Vücuda oturan, sivri hatlı |
| İple asılı ahşap tahta | Omuzdan sarkan düz levhalar — hantal, düzensiz, gövdeden ayrık |
| Demir parçası | Tek bir plaka; gövdenin bir bölümü metal, gerisi kumaş |
| Yarım demir | Gövde ve omuzlar metal, kollar/bacaklar açık |
| Tam demir | Her yeri kaplayan zırh; geniş omuz, boynuzlu kabuto, siluet neredeyse iki katı |

> **Ton notu:** bu liste saray samurayından çok **derme çatma ronin** dünyasına bakıyor
> (iple asılı tahta, tek demir parçası). Bu kasıtlı: dojo fakir başlıyor, ekipman
> zenginleştikçe siluet ağırlaşıyor. Görsel ilerleme ekonominin doğrudan karşılığı olur.
>
> **Kademe listesi örnektir, kilitli değildir.** Kilitli olan kural: malzeme sözlüğü tüm
> yuvalarda ortak olacak (karışık takım kaza değil karakter görünsün) ve her kademede
> siluet gözle görülür şekilde büyüyecek.

### Zırh yuva yuva kuşanılır

Zırh tek parça değil; **yuva başına** kuşanılır ve yükseltilir: miğfer · gövde · omuz
(sağ/sol) · kol (sağ/sol) · etek/kalça · baldır (sağ/sol).

- Vuruş bir bölgeye iner (bkz. §7 → İsabet bölgesi); **hasarı ve uzuv kopma riskini o
  bölgenin zırhı belirler**, savaşçının ortalaması değil
- Yan (sağ/sol) **nereye vurulduğunu** belirler, cezanın ne olduğunu belirlemez: hangi
  kol koparsa kopsun stat cezası aynıdır
- Sınırlı bütçeyle **hangi uzvu koruyacaksın** kararı doğar; bu doğrudan §4'teki silah
  yeterliliğine bağlanır — çift el ustasının kolunu korumak ömrünün emeğini korumaktır

> Referans: Domina'da zırh tam olarak böyle çalışıyor — miğfer, gövde, omuzluk, etek,
> baldırlık ayrı parçalar ve "her zırh parçası, giyen o bölgeden vurulduğunda hasar
> soğuruyor". Bizde ek olarak **uzuv kopma riski** de o parçadan okunur.

Üretim maliyeti ek değil: §12 zaten "taban beden + kemiklere asılan ekipman parçaları"
diyor, yuva yuva zırh bunun doğal sonucu.

**Üretim:** kademe başına yeni gövde çizilmez. Tek **taban beden** + aynı kemiklere
asılan **ekipman parçaları** (kabuto, omuzluk, göğüslük, etek, kol/baldır koruması).
Böylece kademe = parça kombinasyonu + palet; renk ve materyal ucuz eksen olur, yalnızca
yeni **şekil** eklemek maliyetlidir. Açık Karar #4-D'nin istediği parça parça kuşanma da
bedava gelir.

Siluet sınıfı sayısı sınırlı tutulur — 128 px'te ayırt edilen şey renk değil şekildir.
Aynı siluet içinde istenildiği kadar renk/materyal varyantı olabilir.

### Eklem ve kopma kuralları

- **Omuz pivotu** gövdeye çizilen *sode* (omuzluk) altında kalır
- **Kalça pivotu** *kusazuri* / hakama eteği altında kalır
- **Kopma yalnızca omuz ve kalçadadır.** Dirsek, diz, bilek ve ayak bileği **hareket
  eder ama kopmaz**: çekirdekte `BodyPart` yalnızca `Arm / Leg / Eye` tanır ve bilekten
  kopmayla koldan kopmanın mekanik farkı yoktur — ikisi de o kolu kullanılamaz yapar
- El ve ayak **ayrı çizim olmak zorunda değil**; `RigPose` bunlara ayrı açı vermiyor,
  ebeveyninin dönüşünü miras alıyorlar. Ön kola ve baldıra katılırlarsa kademe başına
  dört çizim eksilir
- Her uzuv parçası üst ucunda **bindirme payı** taşır; ebeveyn parçanın altında gizlenir
- Kesik yüzey: **düz koyu kontur + düz vermilion disk**. Kemik/anatomi detayı yok —
  128 px'te kayboluyor. Kopmayı taşıyan şey lekenin kendisi değil **eksik zincirin
  siluetidir**

### Sanat referansı

Ukiyo-e ahşap baskı (Total War: Shogun 2 arayüzü, Muramasa), Trek to Yomi (kontrast),
Okami (kâğıt dokusu). Sumi-e **elendi**: gri yıkama üstünde gri figür, savaşçı zeminden
ayrılmıyor ve dört zırh kademesi monokromda okunmuyor.

---

## Açık Kararlar

| # | Konu | Not |
|---|---|---|
| ~~1~~ | ~~Parti boyutu~~ | **Kilitlendi (2026-08-29).** Üst sınır **4**, sayı oyuncunun kararı; düello/baskın gibi encounter'lar tam sayı dayatabilir (§10). Çekirdek zaten N savaşçı destekliyor. **Takip eden iş:** dört savaşçılık arena 2.2'de kamera ve okunurluk sorunu çıkarır |
| ~~2~~ | ~~Sefer/harita yapısı~~ | **Kapandı (2026-08-29).** Sefer tek oda/tek dövüş; günde **tek karşılaşma teklifi**, al ya da bırak; harita ekranı yok. **Boss yapısı kurulmuyor** — zorluk tek eğri üzerinde artar (§10) |
| 3 | Yokai bestiary detayı | Hangi yokai'ler, her birinin özel dövüş davranışı |
| 4-A | Ekipman — yakın dövüş silahları | **Kilitlendi.** Mevcut `Weapon` modeline sığar (fabrika + denge sayısı): wakizashi, tantō, naginata, kanabō, kama, bō/jō, ono, tekagi. Kavrayış hatları §4'te |
| 4-B | Ekipman — yeni kural gerektirenler | Sersemletme, zehir, jitte/sai ile kılıç yakalama, silah kırılması. Uzam gerekmez, çekirdeğe yeni kural gerekir. **Kalkan yok:** elde taşınan kalkan Japon savaşında yaygın değil (*tate* yere dayanan sabit siperdir); aynı mekanik ihtiyacı jitte/sai karşılar |
| 4-C | ~~Ekipman — uzam/mermi gerektirenler~~ | **Kapandı (2026-08-14).** Çekirdek mermi kazandı: `ThrownWeapon` ayrı bir yuvada taşınır, atış havada süre geçirir, uçuş sırasında hedef kaçabilir/ölebilir/sahadan çıkabilir. Yumi ve fukiya aynı yoldan gelir — yalnızca menzil/hız/cephane sayıları farklıdır. Makibishi hâlâ açık: o bir sarf malzemesi, mermi değil |
| 4-D | Ekipman — zırh ve sakat savaşçı | Zırh kademeleri, tek-kollu savaşçıya özel ekipman kuralları |
| 5 | Ekonomi sayıları | Kaynak türleri, fiyatlar, gün döngüsü uzunluğu |
| ~~6~~ | ~~Görsel stil~~ | **Kilitlendi 2026-08-13 — bkz. §12** |
| 7 | Oyun adı | Henüz yok ("Domina" sadece klasör adı — final isim değil) |
| 8 | Onur eşik sayıları | Seppuku eşiği, decay hızı, hedefli komut etki katsayısı — playtest ile. **Kaçmanın onur bedeli de burada** (`RetreatHonorPenalty`, §5): kural kilitli, sayı değil |
| ~~9~~ | ~~Kaçışta kısmi ödül~~ | **Düştü (2026-08-29).** Sefer tek dövüşse önceki odalarda toplanmış ganimet diye bir şey yok; çekilmek o dövüşün ödülünü siler, o kadar (§10) |
| ~~10~~ | ~~Seferin peşin bedeli~~ | **Kapandı (2026-08-29).** Girmek **bir gün** yer (kaçılsa da), düşman kadrosu **kısmen** görünür — yalnızca tehdit işareti (§10) |
| 11 | Hücum sayıları | Mesafe eşiği, tick başına hücum olasılığı, hız çarpanı, hasar çarpanı. Kural §4'te kilitli; sayılar `Domina.Sim` ölçümünden sonra (bkz. Açık Karar 8'in yöntemi) |
