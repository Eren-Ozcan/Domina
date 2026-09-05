# Tasarım Kararları (Karar Defteri)

> Bu dosya **kilitlenmiş** tasarım kararlarını tutar. Yol haritası için `ROADMAP.md`.
> Kararların dışarıdan doğrulanabilir dayanakları için `DESIGN-REFERENCES.md`.
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
| **Tetik: fırsat, eşik değil** | Savaşçı sabit bir mesafeye bakmaz, **boşluğa** bakar: *"şu an kimse bana vuramıyor ve birikmemi tamamlayacak kadar vaktim var mı?"* Her düşman için sorulan şey, ona vurabilir hale gelmesinin ne kadar süreceği — `(mesafe − menzili) ÷ hızı`. Biri bunu birikme süresinden kısa sürede yapabiliyorsa fırsat yoktur. **Fırlatma önceliklidir:** atacak mermisi olan atar, hücuma kalkmaz |
| **Karar savaşçının kendisidir** | Fırsat doğduğunda kullanılıp kullanılmayacağı **Saldırganlık**'la ölçeklenen bir zara bağlı: atılgan olan atlar, ölçülü olan mesafeyi yürüyerek kapatır. Zar **fırsat başına bir kez** atılır — fırsat sürdükçe her karar adımında yeniden değil |
| **Birikme** | Koşu hemen başlamaz: savaşçı önce **yerinde durup güç toplar**. Bu sürede kıpırdamaz, kaçınamaz, bloklayamaz ve **yediği ilk isabetle hücum dağılır** — koşu hiç başlamaz, hasar çarpanı kazanılmaz. Hücumun asıl bedeli burada ödenir |
| **Hız** | Hücum sırasında hareket hızı bir **çarpanla** artar (`Speed` stat'ının üstüne, `RetreatSpeedMultiplier` ile aynı yerden) |
| **Ödül** | Varışta yapılan ilk vuruş **hasar çarpanı** kazanır, ve çarpan **varış anındaki gerçek hızdan** çıkar: `1 + (varış hızı ÷ azami yürüme hızı) × oran`. Momentum hızdır — ağır Oni'nin hücumu Tengu'nunki kadar sert olamaz. Uzuv kaybı riski zaten hasar/maxHP oranından geldiği için (§7) sakatlanma olasılığı **kendiliğinden** artar — ayrı kural yazılmaz |
| **Bedel** | Taahhüt: birikirken savaşçı yerinden kıpırdamaz ve **yediği tek bir isabet** hamleyi harcatır. Ayrıca yol boyunca **menzilinden geçtiği her düşman** ona bir kez **fırsat saldırısı** yapar — kaçış penceresiyle (§5) aynı mekanik |
| **Hedefin karşılığı ayrıdır** | Yoldan geçilen düşman vuruşunu **kesin** alır; hücumun **hedefi** ise bir zara bağlı karşılık verir. Cepheden gelen gövdeyi tam anında karşılamak, yanından koşarak geçene vurmaktan zordur. Karşılık **tuttuğunda hücumun momentumu söner**: varış vuruşu yapılır ama hasar çarpanını kazanmaz. Nadirlik değil **ağırlık** — ve yeni bir ayar sayısı doğurmuyor, mevcut çarpanı iptal ediyor |
| **Savunma açık kalır** | Hücum eden savaşçı **normal oranıyla kaçınmayı sürdürür**. Savunmasızlık kaçışa özgüdür (§5) — hücumun bedeli savunmanın kapanması değil, hamlenin açıkta olmasıdır |
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

**Birikme ile mesafe eşiği aynı düğmenin iki ucudur.** Savaşçı birikirken düşman
yürümeye devam eder; sana yetişmesi `(mesafe − düşmanın menzili) ÷ düşmanın hızı` kadar
sürer. 320 birimden Tengu 0.73 sn'de, Kappa 0.88 sn'de, Oni 0.87 sn'de yetişiyor. Birikme
bu sürelerden uzunsa hücum daha kalkmadan dağılabilir — **hız stat'ı böylece ilk kez
"hücumu bozan şey" olarak iş görür**, ve fırlatma silahı mesafeden bağımsız olarak
biriken savaşçıyı vurabildiği için menzilli yokai hücumun doğal karşıtı olur.

### Neden sabit bir mesafe eşiği yok

Önce vardı — elle 320'ye kilitlenmişti — ve iki şeyi birden bozuyordu.

**Hücum bir açılış hamlesine hapsolmuştu.** İki hat 960 birim aradan başlar, birbirine
yürür ve bir daha hiçbir şey araya mesafe koymaz; savaşçılar yalnızca yaklaşır. Ölçüldü:
30.000 dövüşün hiçbirinde **1.75 sn'den sonra** hücuma kalkılmıyordu.

**Ve eşik yanlış soruyu soruyordu.** Savaşçının bilmesi gereken şey "hedefim 320 birimden
uzak mı" değil, *"birikmemi tamamlayacak vaktim var mı"*. Bu hesaplanabilir bir şeydir:

> gereken mesafe = **düşmanın menzili + düşmanın hızı × birikme süresi**

Mevcut kadroyla, birikme 0.75 sn: Kappa'ya **287**, Oni'ye **296**, Tengu'ya **327**. Elle
kilitlenmiş 320, bu bandın tam ortasıydı — yani doğru sayıyı ölçümle bulmuşuz ama yanlış
yerde tutuyormuşuz.

Kural fırsat değerlendirmesine çevrilince **üç ayar sayısı birden düştü**: sabit eşik
(türetiliyor), kalabalık kısıntısı (üç düşman yetişiyorsa zaten fırsat yok) ve yeniden
tutuşma penceresi (hedefi devrilen savaşçının önünde boşluk kendiliğinden açılıyor). En geç
hücum kalkışı **1.75 sn → 16.05 sn**.

**Kasıtlı kör nokta:** hesap yalnızca yürüyerek gelen tehdidi görür. Mermisi olan düşman
mesafeden bağımsız vurup birikmeyi bozabilir, ve savaşçının bunu önceden bilmesini
istemiyoruz — **menzilli yokai'yi hücumun doğal karşıtı yapan şey budur.** Hızlanmış bir
düşman hücumu da hesabın öngörmediği hızla gelir: hücumu bozan ikinci şey başka bir
hücumdur.

### Hücum sayıları — kilitlendi (2026-09-02)

3v3 senaryosunda 10.000 dövüş, `--policy never`:

Toplam **altı** sayı — mesafe eşiği, kalabalık çarpanı ve yeniden tutuşma eşiği kuralın
kendisinden türediği için silindi.

| Sayı | Değer | Ölçüm |
|---|---|---|
| Olasılık, Saldırganlık 0 | **0.35** | Gördüğü fırsatların kaçını kullandığı. Acemi (Sald. 40) 0.61, tengu (70) 0.81 |
| Olasılık, Saldırganlık 100 | **1.00** | Tek sıklık düğmesi. 0.35-1.00 → 1.88 kalkış / **1.63 tamamlanmış hücum**, yani adım başına atılan eski zarın (0.12-0.45) ürettiği sıklığın yerini tutuyor. Aynı kuralla 0.12-0.45 → 0.78 kalkış, 0.50-1.00 → 2.15 |
| Birikme | **0.75 sn** | Dağılma oranı 0.25 sn'de %5.7, 0.5'te %13.0, **0.75'te %23.6**, sonrası %23-24'te düzleşiyor. 0.75 eğrinin dizi: ötesi taahhüdü uzatır ama riski artırmaz |
| Hız çarpanı | **1.6** | **Denge düğmesi değil, sunum düğmesi** ve öyle kalıyor: zafer 1.0'da %84.8, 1.6'da %84.5, 2.5'te %84.4. İki tarafa birden işlediği için düz olması beklenir — canlanması gereken şey `Speed` stat'ıydı, bu çarpan değil |
| Hasar oranı (azami hızda) | **0.43** | Speed 50'lik savaşçıya 1.6× hızla ~1.50 çarpan verir. 0.25-0.6 bandı düz; 0.9 ve üstü **aleyhe** dönüyor (ölüm %43.2), çünkü oran iki tarafa da işler ve varyans zayıf tarafa yarar |
| Hedefin karşı vuruşu | **0.6** | **Tabanı hücum değil §5 koydu.** Hedefin topladığı karşılıklar, sayıca azalan tarafın başlıca geliri ve sayı üstünlüğünün çığa dönmesini engelleyen şey. 0.6'nın altında §5'in "çekmek ölümü azaltır" vaadi **tersine dönüyor** (0.25'te çeken %41.5, çekmeyen %40.0). 0.6'da çeken %39.6, çekmeyen %40.3 |
| Süre sınırı | **4.0 sn** | Hiç dolmuyor (mesafe ~0.6 sn'de kapanıyor). Sonsuz kovalamaya karşı emniyet supabı olarak duruyor |

Kilitli ayarla, hücum kapalı → açık:

| Senaryo | Zafer | Hücum/dövüş | Varış | Dağılan | En geç kalkış |
|---|---|---|---|---|---|
| duel | %66.4 → **%63.4** | 0.61 | %100 | %0 | 1.00 sn |
| 3v3 | %81.8 → %83.2 | 1.89 | %86.7 | %13.3 | **13.80 sn** |
| veteran | %98.4 → **%94.6** | 0.74 | %100 | %0 | 0.75 sn |
| 1v3 | %46.1 → %55.9 | 0.74 | %100 | %0 | 0.75 sn |

Tek düşmanlı senaryolarda hücum yeniden bir **açılış hamlesi**: karşında bir kişi varken
fırsat bir kez doğar, o da dövüşün başında. Bu artık kuralın kendisinden okunuyor, ölçüm
artığı değil — 3v3'te fırsat hatlar dağıldıkça yeniden doğuyor ve en geç kalkış 13.80 sn.

Hücumun katkısı iki senaryoda **eksi** — mekaniğin bir bedeli olduğunun ölçülebilir kanıtı
bu satırlardır. Teke tek dövüşte hücum artık açıkça kötü bir fikir: karşındaki tek kişi
sensin diye bakan kişidir, ve cepheden karşılık verme şansı en yüksek olan odur.

### Hedefin karşılığı neyi taşıyor

Kural yazılırken beklenen şey hücumun bedelini ayarlamaktı. Ölçüm başka bir şey gösterdi:
**hedefin karşı vuruşu, sayı üstünlüğünün çığa dönmesini engelleyen mekanizmadır.** Üç
kişiye karşı duran savaşçının başlıca geliri, üstüne gelenlerden topladığı karşılıklardır;
bu gelir kısılınca kalabalık tarafın avantajı katlanıyor ve **§5'in kaçış vaadi çöküyor** —
çeken oyuncu, sahada bıraktığı arkadaşları daha hızlı eridiği için daha çok ölü veriyor.

| Hedefin karşı vuruşu | 1v3 zafer | Çeken / çekmeyen ölüm (3v3) |
|---|---|---|
| 1.0 (kesin) | %63.8 | %38.1 / %40.2 |
| 0.7 | %59.0 | %39.1 / %40.3 |
| **0.6** | **%55.9** | **%39.6 / %40.3** |
| 0.5 | %52.6 | %40.1 / %40.1 |
| 0.35 | %47.7 | %41.0 / %40.2 ✗ |
| 0.25 | %44.7 | %41.5 / %40.0 ✗ |

0.6 bu yüzden zevkle değil **kısıtla** seçildi: tüm kilitli vaatleri ayakta tutan en düşük
değer. Zorunlu zamanlı kaçış merdiveni (§5) her değerde sağlam duruyor — bozulan yalnızca
"canın düşünce çek" davranışı, ki oyuncunun gerçekte yapacağı şey odur.

### Fırsat başına tek zar — `Speed`'i canlandıran şey

Hücum zarı önce **karar adımı başına** atılıyordu (0.2 sn'de bir). Bunun görünmeyen sonucu
şuydu: hücum sıklığı, savaşçının fırsat penceresinde ne kadar **oyalandığına** bağlıydı —
oyalanma süresi de mesafeyi kapatma hızıdır. Hızlı savaşçı pencereden çabuk geçtiği için
daha seyrek hücum ediyordu: 3v3'te dövüş başına Hız 0'da **2.20**, Hız 100'de **1.20**.

Bu, hasarın hıza bağlanmasını (§4 "Ödül") tam olarak götürüyordu. Hızlı savaşçı daha sert
ama daha seyrek vuruyor, iki eğri birbirini yiyor ve `Speed` ekseni ölçümde **atıl**
kalıyordu: zafer Hız 0'da %83.9, Hız 100'de %84.7 — gürültü kadar.

Zar fırsat başına bir kez atılınca sıklık hızdan **tamamen** koptu ve eksen canlandı:

| `Speed` | 0 | 25 | 50 | 75 | 100 |
|---|---|---|---|---|---|
| Zafer (3v3) | %83.6 | %83.6 | %84.5 | %85.0 | **%87.0** |
| Hücum/dövüş | 1.87 | 1.87 | 1.87 | 1.88 | 1.88 |

Kural olarak da doğrusu bu: fırsat gördüğünde bir kez karar verirsin, o fırsat sürdükçe
saniyede beş kez zar atmazsın. Bedeli, Saldırganlık'ın ayırt etme gücünün daralması —
band 0.12-0.45 (3.75 kat) yerine 0.35-1.00 (2.86 kat), çünkü aynı sıklığı daha az zarla
üretmek gerekti. `Speed`'in dojo'da gerçek bir düğme olması bu takasa değer.

**Bozulma ölçütü ölçümle değişti.** Önce "ağır darbe dağıtır" (§7'nin eşiği) denendi ve
**%0.0** çıktı: taze bir savaşçıya inen darbeler o eşiğe hemen hiç ulaşmıyor, kural
yazılıydı ama hiç işlemiyordu. **İsabet eden her darbe dağıtır** kuralıyla %23.6. Savaşçı
zaten kaçınamadığı için bu tek cümleyle okunur ve yeni bir denge sayısı doğurmaz.

**Bedelin ölçüsü kalabalığa göre değişiyor** ve bu kasıtlı: 3v3'te birikmelerin %23.6'sı,
1v3'te %31.6'sı dağılıyor, **düelloda %0'ı** — tek düşman 0.75 sn'de yetişemiyor.
Hücumu bozan şey kalabalıktır.

### Neden hücum savunmayı kapatmıyor

Bir süre kapatıyordu: hücum eden kaçınamıyor, bloklayamıyor ve üstüne isabet bonusu
yiyordu. Ölçüm bu kuralın **beklenmedik bir yönde** çalıştığını gösterdi.

`Domina.Sim`'in 1v3 kadrosunda yalnız veteranın zaferi, hücum kapalıyken %45.3, açıkken
%73.3'e fırlıyordu. İlk okumamız "hücum sayıca az olana yarıyor" idi; **yanlıştı.** Yalnız
savaşçının kendi hücumu fiilen sıfırlandığında bile zafer %68'de kalıyordu — yani farkı
yaratan oyuncunun hücumu değildi. Değişen tek şeyin düşmanın hücumu olduğu koşumlar sebebi
gösterdi:

| Düşmanın hücumu | 1v3 zafer | Düşman ölümü |
|---|---|---|
| Hiç hücum yok | %45.3 | %70.0 |
| Birikme 2.0 sn (hücumların %84.7'si dağılıyor) | %54.9 | %72.3 |
| Birikme 0.75 sn | %68.1 | %80.9 |
| Birikme 0 (hücumlar hep tamamlanıyor) | %79.0 | %90.6 |

Düşmanın hücumu ne kadar iyi işlerse yalnız savaşçı o kadar çok kazanıyordu: üç zayıf yokai
güçlü bir veterana **savunmasız** koşup ona kaçınılamaz bedava hasar hediye ediyordu.

**Kural kaldırıldı ve teşhis doğrulandı.** Savunma normal oranına dönünce 1v3 zaferi
%73.3'ten **%64.6'ya** indi — dokuz puan, tek bir kuralı kaldırmakla. Bedelin savunmanın
kapanmasında olması, hücumu *kimin yaptığına* göre asimetrik bir ceza üretiyordu.

### Hız hasara bağlandı — ve beklenen dengeyi getirmedi

Hasar çarpanı sabit 1.5 iken `ChargeSpeedMultiplier` ekseni ölçümde **atıldı**. Momentumu
gerçek hıza bağlamak (Mount & Blade'in couched lance kalıbı) bunu düzeltir diye
kuruldu — **düzeltmedi.**

Mekanizma çalışıyor: hızlı savaşçının varış vuruşu ölçülebilir biçimde sert (birim testiyle
bağlı). Ama denge düzeyinde etki yıkanıyor, çünkü **iki eğri ters yönde hareket ediyor.**
Oyuncu tarafının `Speed`'i ezilerek ölçüldü (3v3, hücum açık eksi hücum kapalı):

| Oyuncu tarafı Speed | Hücum/dövüş | Hücumun zafere katkısı |
|---|---|---|
| 0 | 2.19 | +3.4 puan |
| 50 | 1.72 | +2.5 puan |
| 100 | 1.20 | +2.6 puan |

Hızlı savaşçı **daha sert ama daha seyrek** hücum ediyor: hızlı yaklaştığı için fırsat
penceresi daha kısa açık kalıyor. İki etki birbirini götürüyor.

**Değişiklik yine de duruyor**, çünkü sayıyı değil modeli düzeltiyor: momentum artık hızdan
çıkıyor, Oni ile Tengu'nun hücumu aynı sertlikte değil, ve bir sabit yerine bir sabit
geldi — ayar sayısı altıda kaldı. Ama `Speed`'i **dojo'da gerçek bir hücum kaldıracı**
yapmak istiyorsak asıl engel burada yazılı: fırsat penceresi hıza ters çalışıyor.

**Asıl kazanç:** hücum artık her zaman doğru hamle değil. Kural kaldırılmadan önce
`veteran` dışında her senaryoda oyuncuya yarıyordu; şimdi düelloda **nötr** (%66.7 → %66.0)
ve donanımlı veteran için **zararlı** (%98.3 → %96.1). Bir hamlenin bedeli olduğunu
söyleyebilmek için bazen yapılmaması gerekir.

### Dövüş çözümlemesi (tam otomatik, manuel nişan yok)
Her vuruşma anı sırayla çözülür:
1. Saldıranın **Saldırganlık**'ı saldırı sıklığını belirler
2. Savunanın **Kaçınma**'sı için zar → kaçındıysa **stamina harcanır, hasar yok**
3. Kaçınamadıysa **Savunma** hasarı azaltır
4. Kalan hasar HP'den düşer
5. Vuruş yönü/açısı görsel çeşitlilik içindir, **mekanik sonucu etkilemez**

### Hedef seçimi (kilitlendi 2026-09-03)

"Kim kime vurur" bir sıralama değil, **savaşçının kendi kararıdır**. Eski kural bir karar
değildi: en yakını seç, ölene kadar ona sadık kal. Yaralı düşman fark edilmiyor, kuşamı
dağılmış açık bölge fark edilmiyor, üç savaşçı aynı hedefe yığılıp yığılmadıklarını
bilmiyordu.

Savaşçı her **karar adımında** (yeni bir hamleye başlarken, her tikte değil) düşmanları
puanlar; en yüksek puanlıyı seçer.

| Kalem | Ağırlık | Ne der |
|---|---|---|
| Mesafe | −1 puan / birim | Yürünecek yol. Menzil içindeki düşman ceza almaz |
| Yara | +120 × eksik can oranı | Bir düşmanı devirmek, üçünü birden yaralamaktan iyidir |
| Açık bölge | +90 × dağılmış parça oranı | Zırh yıpranmasının dövüş içi karşılığı (§7) |
| Kalabalık | −60 / aynı hedefteki takım arkadaşı | Takım tek düşmanı kovalarken diğerleri bedava vurmasın |
| Yapışkanlık | +80 (mevcut hedefe) | Yön değiştirmenin bedeli: gidilen yol boşa gider |
| **Fırsat penceresi** | 200 birim | Yara ve açık bölge kazançları bu mesafede sıfıra iner |

**Zar yok — puanlama deterministik.** Rastgelelik kararın kendisinde değil savaşçının
kimliğinde. Zar atılsaydı seçim her adımda titrer ve ölçüm gürültüye boğulurdu.

**Ölçüldü (3v3, 20.000 dövüş, `never`):** eski kural (yalnız mesafe) %72.28 zafer /
%49.11 ölüm; yeni kural **%72.17 / %50.30**. Zafer aynı, ölüm artıyor — odaklanan takım
daha çok öldürüyor, dövüş daha keskin bitiyor. Tam kuşamlı düşmana karşı (`3v3-armored`)
yeni kural açık ara iyi: %71.84 → **%72.68**.

> **Fırsat penceresi sonradan eklendi.** Sınırsız yara ağırlığı kuralı düpedüz bir
> zorluk artışına çeviriyordu: savaşçı yanındaki sağlam düşmanı bırakıp arenanın
> öbür ucundaki yaralıya yürüyor, yol boyunca bedava vuruş yiyordu (ağırlık 0'da %72.29,
> 120'de %71.77, 200'de %70.74 — tek yönlü aşağı). Pencere eklenince eğri düzeldi.

> **Açık bölge ağırlığı şimdilik uykuda.** Varsayılan dayanıklılıkta tek dövüşte parça
> dağılmadığı için (§7) kuralın ısırdığı yer sefer boyunca yıpranmış kuşamdır. Yıpranmış
> kuşamla ölçüldüğünde etkisi küçük ve oyuncu aleyhine (%70.06 → %69.91): açık bölgeyi
> düşman da görüyor.

> **Bestiary'nin (#3) girdisi burası.** Yokai'lerin davranış farkı ayrı bir davranış
> kodu değil, bu ağırlıkların yokai'ye göre farklı ayarlanmış hâli olacak: yaralıya
> saldıran çakal, kalabalığı umursamayan oni, yapışkanlığı yüksek takipçi.

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

### Blok (kilitlendi 2026-09-03)

Blok artık çekirdekte **ayrı bir durum** (`CombatState.Blocking`) — eskiden Savunma
statının içinde eriyordu, bu tablo da onu ayrı sayıyordu. Fark kapandı.

| Kalem | Kural |
|---|---|
| **Karar** | Savaşçının kendisi verir: şans = `Savunma ÷ 100 × 0.45`. Taban yok — Savunma 0 olan savaşçı hiç bloklamaz (kaçınmayla aynı şekil, hücumla değil) |
| **Şart** | Yakınlık değil **okunan hamle**: menzil içindeki bir düşmanın kılıcı toplanmış olmalı (`AttackWindup` ya da koşan hücum). Menzil ikisinin büyüğünden okunur — uzun saplının karşısındaki kısa bıçaklı da tehdit altındadır |
| **Süre** | 0.8 sn, bu sürede savaşçı **vurmaz**. Bloğun bedeli budur ve kaçınmadan pahalıdır: kaçınma bir darbeyi siler, blok bir süre satın alır |
| **Ritim** | Blok arkasına blok gelmez. Zar her adımda yeniden atılsaydı savunması yüksek savaşçı hiç vurmadan dövüşü kilitlerdi |
| **Tuttuğu** | Hasarın %70'i × silahın blok kalitesi. Kalite: çift el 1.0, künt 0.85, kesici 0.80, delici 0.70, **yumruk 0.30** — silahını düşüren savaşçı bloğunu da kaybeder |
| **Uzuv** | Bloklanan darbe uzuv **koparmaz**. Savunma statının imza cezasına karşı verdiği tek kesin söz |
| **Sarsıntı** | Künt silahın payı duruşa rağmen %75 işler. Kalkan yokken künt sınıfın dördüncü kazancı: duruş çelikten korur, sarsıntıdan korumaz |
| **Yan/arka** | Kuşatılan bloklayamaz — arkadan gelen vuruş duruşu görmez |
| **Kaçış** | Komut duruşu **anında** keser (yukarıdaki tablo) |

**Ölçüldü (3v3, 20.000 dövüş, `never`):** kural kapalıyken zafer %71.21 / ölüm %50.44 /
uzuv kaybı %5.19; açıkken **%72.39 / %49.19 / %4.96**. Savaşçı başına 0.20 darbe
karşılanıyor.

> **İlk hâli işe yaramıyordu.** Duruş yalnızca "menzilde düşman var mı" diye alınınca
> körlemesine alınıyordu: savaşçı başına yine 0.20 darbe karşılıyor ama zaferi
> **düşürüyordu** (%71.21 → %70.50), çünkü boşa alınan duruşlar saldırı döngüsünü
> yiyordu. Şartı "gelen vuruşu oku"ya çevirmek kuralın işaretini ters çevirdi.

> **Freni yok, dayanağı stat.** `MaxBlockChance` büyüdükçe oyuncu tek yönlü kazanıyor
> (0.25'te %71.61, 0.45'te %72.39, 1.0'da %74.08) — kuralın kendi içinde bir freni yok.
> Freni Savunma statının dojo'da başka statlarla yarışması sağlıyor. Sayı Faz 9'da
> yeniden bakılacak.

> **Yakalama aletine blok kayrılmadı.** Jitte/sai'ye çift el silah kadar blok kalitesi
> verilince ölçüm kilitli bir freni kırdı: ağır silah taşıyan düşmanın önünde jitte
> yanlış seçim olmaktan çıkıyordu (jitte-heavy %35.38 > katana-heavy %34.96). Override
> kaldırıldı; ikisi de tek elli künt aletin kalitesini (0.85) taşıyor.

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
- Silah tipi: kesici (kılıç/balta) → uzuv kaybı; künt (topuz) → sersemleme
  (aşağıda "Sersemletme")
- Zırh/savunma seviyesi riski **azaltır** (ekipmana yatırımı anlamlı kılar)

### Sersemletme (kilitlendi 2026-09-02)

Aynı **ağır darbe** iki ayrı zar attırır: uzuv kopma ve **sersemletme**. Hangi zarın
tuttuğu silahın sınıfına bağlıdır — takas budur:

| Sınıf | Uzuv kopma çarpanı | Sersemletme çarpanı |
|---|---|---|
| Kesici (katana, nodachi) | 1.0 | 0.25 |
| Delici (yari) | 0.5 | 0.15 |
| Künt (tetsubo, kanabō) | 0.15 | **1.0** |

**Sersemleyen savaşçı 0.9 saniye donar:** yürümez, vurmaz ve **kaçınamaz**. Kaçınmanın
kapanması kuralın asıl dişidir; ölçümde kaybedilen hamleden çok bu ısırıyor.

| Sayı | Değer |
|---|---|
| Zarın atıldığı eşik (darbe/azami can) | 0.20 |
| Taban şans | 0.35 |
| Donma süresi | 0.9 sn |
| Kafaya inen darbe çarpanı | 2.0 |
| Zırhın kopma direncinin sayılan payı | 0.6 |

**İki koruma kuralı:**

- **Çekilen savaşçı sersemlemez.** Sersemletme onu dondurduğu için künt silahlı bir
  düşman, oyuncunun tek müdahalesini (§5) tek zarla iptal edebilirdi
- **Sersemleyen tekrar sersemlemez** — süre yenilenmez, kilitlenme yok. Süre bitince
  savaşçı normal döngüsüne döner; bu arada basılan "Kaç" tuşu **yutulmaz**, beklemeye
  alınır ve orada işlenir

> **Neden bu kural yazıldı:** künt silah kopma çarpanında kesiciye kaybediyor
> (0.15'e karşı 1.0) ve karşılığında hiçbir şey almıyordu. Ölçüldü (aynı savaşçı, aynı
> düşman, yalnızca silah farklı; 20.000 dövüş): kural yokken kesici %91.57, künt %88.68
> zafer alıyor — künt her eksende kötü. Taban şans 0.35'te ikisi %92.06 / %92.08 ile
> başa baş. 0.60'ta künt öne geçiyor, 1.00'da baskın hâle geliyor.
>
> Bedelini oyuncu da öder: 3v3'te Oni'nin tetsubo'su artık ısırıyor ve oyuncu zaferi
> %69.31'den %65.20'ye iniyor. Mutlak denge Faz 9'un işi; buradaki sayılar sınıflar
> arası **oranı** tutar.

### Kılıç yakalama — jitte ve sai (kilitlendi 2026-09-03)

§4 elde taşınan kalkanı reddediyor (*tate* yere dayanan sabit siperdir, kol kalkanı
Japon savaşında yaygın değil) ve aynı mekanik ihtiyacı **jitte/sai**'nin karşıladığını
söylüyordu. Kural artık çekirdekte.

**Yakalama, kaçınmadan önce denenen ikinci savunma eksenidir.** Kaçınma darbeyi
ıskalatır ve orada biter; yakalama darbeyi **siler** ve saldıranı **0.6 saniye** açıkta
bırakır — kilitli savaşçı yürümez, vurmaz, kaçınamaz.

Zar üç şeyden beslenir:

| Çarpan | Nereden okunur | Değerler |
|---|---|---|
| Kavrayış (`CatchSkill`) | Savunanın aleti | jitte 1.0, sai 1.25, diğer her şey 0 |
| Yakalanabilirlik (`CatchFactor`) | Saldıranın silahı | kesici 1.0, delici 0.7, künt 0.25, yumruk 0 |
| Kaldıraç | Saldıranın silahı çift else | ×0.75 |

| Sayı | Değer |
|---|---|
| Taban şans | 0.24 |
| Kilit süresi | 0.6 sn |
| Stamina bedeli | 16 |
| İsabet 100'ken eklenen oran | 0.5 |

**Yakalama İsabet'e bağlanır, Kaçınma'ya değil.** İki savunma ekseni aynı stattan
beslenseydi ekipman kararı stat kararının kopyası olur, jitte yalnızca "kaçınması
yüksek savaşçının ikinci savunması" olurdu.

**Üç koruma kuralı:**

- **Yalnızca yakın dövüş yakalanır** — havada gelen mermi çengele oturmaz
- **Arkadan gelen vuruş yakalanmaz**: görülmeyen silah tutulamaz, ve kural burada da
  işleseydi kuşatmanın (§5) bedeli silinirdi
- **Çekilen savaşçı yakalamaz** — sersemletmedeki korumanın eşi: kaçış vaadinin üstüne
  yeni bir zar konmaz

İki alet (14 hasar / jitte 1.00 sn, sai 1.05 sn) bilerek **künt** sınıftadır: ikisi de
keskin değil, tutma ve dürtme aletidir. Aralarındaki fark hasar değil **hacim** — sai
dövüş başına 3.71, jitte 2.75 yakalar.

> **Ölçüm (1v1, 20.000 dövüş, `losing:0.7`; kontrol aynı gövdede katana):**
>
> | Silah | Zafer | Uzuv kaybı | Yakalama/dövüş |
> |---|---|---|---|
> | Katana (kontrol) | %73.09 | %0.90 | 0.00 |
> | Jitte | %72.63 | %0.52 | 2.75 |
> | Sai | %72.73 | %0.45 | 3.71 |
>
> Taban şans burada döndü: 0.15'te jitte %61.16, 0.20'de %68.53, 0.30'da %77.22.
> 0.24'te katana zaferde önde kalıyor, yakalama aletleri **eve sakat dönmemeyi** alıyor —
> her seçenek bir şeyde en iyi.
>
> **Ağır silah yakalamanın cevabıdır.** Düşman çift el nodachi taşıdığında jitte %29.37,
> katana %34.78 kazanıyor ve jitte uzuv korumasını da kaybediyor (%11.77'ye karşı
> %11.42) — yani jitte'yi nodachi'ye karşı taşımak düpedüz yanlış seçim. Kaldıraç
> çarpanı 0.5'te bu delik 14 puana çıkıyordu (%21.04); 0.75 tuzağı bir tercihe indiriyor.
>
> **Kilit süresi ölçümde neredeyse hiçbir şey yapmıyor** — sersemletme süresindeki
> bulgunun aynısı. 1v1'de 0 sn ile 1.2 sn arası yalnızca %72.44 → %73.72. Açılan pencere
> savaşçının zaten kendi saldırı döngüsünde beklediği boşluğa denk geliyor; kuralın
> ısırdığı yer **silinen vuruş**. 3v3'te takım değeri de ayrışmadı, ama sebebi başka:
> orada jitte taşıyan acemi dövüş başına yalnızca ~0.57 **yakalanabilir** vuruş görüyor
> (tavan `--catch-chance 1.0` ile ölçüldü). 0.6 sn, ekranda okunacak kadar uzun ve
> takasın döndüğü yerin altında olduğu için seçildi.
>
> **Silahın elden düşmesi bu tabloyu da değiştirdi (2026-09-03).** Yukarıdaki üç sayı
> kural gelmeden önce ölçüldü. Kuralla birlikte katana %75.02 / jitte %78.00 / sai %78.88:
> yakalama aletleri kılıç taşıyan düşmanın önünde artık zaferde de önde, çünkü
> yakaladıkları silahı avuçtan söküyorlar. Katana'nın "bir şeyde en iyi" olduğu yer
> değişti — zafer değil, **zırhlı düşman** (%60.81'e karşı jitte %34.14). Kaldıraç freni
> de ayakta: nodachi'ye karşı jitte %35.22, katana %37.84.
>
> **Stamina bedeli yakalamayı seyrekleştirerek değil, yorarak ısırıyor.** Bedel 0'da
> zafer %76.85, 16'da %72.63 — ama yakalama sayısı ikisinde de aynı (2.72 / 2.75).
> 8'de hiç bağlamıyor (%76.85), 30'da yıkıcı (%41.07).

### Zehir (kilitlendi 2026-09-03)

Zehir, **hasar azaltımının etrafından dolaşan tek yoldur**. Plaka kesiği durdurur, künt
kuvvetin bir payını durdurur (yukarıda 0.6), zehri hiç durdurmaz — doz zırha değil kana
işler. Silahın kendi çeliğinin hafif olması bunun bedelidir.

**Kural.** Zehirli silahın indirdiği her isabet, savunana bir **doz** bırakır — zar
atılmaz, namlu deriyi çizdiyse zehir de girmiştir. Doz saniyede bir can yer; hasar ne
zırhtan ne Savunma statından geçer. Doz **birikir** (tavana kadar), süre her yeni vuruşta
baştan kurulur.

| Sayı | Değer |
|---|---|
| Tik başına hasar (doz 1 iken) | 2.5 |
| Tik aralığı | 1.0 sn |
| Dozun ömrü | 6.0 sn |
| Azami doz | 3.0 |
| Zehirli tantō | 7 hasar / 0.85 sn (temiz tantō 13/0.85) |
| Zehirli shuriken | 12 hasar, 2 cephane (temiz shuriken 12, 4 cephane) |

**Üç sınır kuralı:**

- **Zehir uzuv koparmaz ve sersemletmez.** İkisi de *darbenin* sonucudur; zehirde vuran
  kimse yok. Zehirli silah oyunun imza mekaniğinden pay almaz — takasın yarısı budur
- **Zehrin öldürmesi ayrı bir sebeptir** (`DeathCause.Poison`): savaşçıyı sahadaki hiçbir
  vuruş düşürmemiştir
- **Çekilen savaşçının zehri durmaz.** Sersemletme ve yakalama, kaçış vaadinin üstüne
  *yeni bir zar* konmasın diye çekilene işlemez; zehir yeni bir zar değil, çoktan ödenmiş
  bir bedelin devamıdır — tuş bir panzehir değildir

> **Ölçüm (1v1, 20.000 dövüş, `losing:0.7`):**
>
> | Silah | Zırhsız oni | Ō-yoroi kuşanmış oni |
> |---|---|---|
> | Katana (kontrol) | %73.09 | %68.62 |
> | Temiz tantō | %31.14 | %1.23 |
> | Zehirli tantō | %72.19 | **%77.19** |
>
> Zehirli bıçak açık dövüşte katana ile başa baş, zırhın önünde öne geçiyor. **Zehirlinin
> karşısında ō-yoroi kuşanmak zarardır:** plaka dozu okumaz, ağırlığı ise vuruşu geciktirir.
>
> **İlk kurulum yanlış cevap veriyordu.** Bıçak 13 hasarla dururken zehir yalnızca zayıf
> bir silahı kurtarıyordu (%74.00 / %55.99) — zırhı hiç aşmıyordu, çünkü çıktısının çoğu
> hâlâ çelikti. Bıçak 7'ye indirilip doz büyütülünce çıktının %60'ı zehre geçti ve iddia
> ancak o zaman doğrulandı.
>
> **Asıl düğme doz tavanı:** 1'de zehirli bıçak düpedüz kötü (%16.71), 2'de hâlâ geride
> (%50.92), 3'te başa baş, 5'te baskın (%82.25). Dozun **ömrü** ise 6 saniyeden sonra
> neredeyse hiçbir şey yapmıyor (3 sn %52.90, 6 sn %72.19, 9 sn %73.11): hızlı vuran silah
> süreyi zaten sürekli yeniliyor. Tik aralığı tarafsız bir düğme değil, doğrudan hasar
> hızıdır (0.5 sn'de %94.93, 2 sn'de %30.40); 1 sn, dozun ömrüne bölününce zehri
> **sayılabilir** yapıyor — altı vuruş.
>
> **Silahın elden düşmesi bu tabloyu değiştirdi (2026-09-03).** Yukarıdaki sayılar kural
> gelmeden önce ölçüldü; kuralla birlikte zehirli bıçak da ō-yoroi'ye vururken elden
> düşüyor (%20.18) ve tablo şöyle oluyor: katana %75.02 / %60.81, zehirli tantō %74.26 /
> **%69.39**. Zehrin iddiası ayakta — zırhın önünde hâlâ tek gerçek cevap o — ama
> **"zehirlinin karşısında ağır kuşanmak zarardır" artık doğru değil**: düşürme zırha
> kaybettiği payı geri verdi, plaka zehirli bıçağın önünde 5 puan (kılıcın önünde 14)
> kazandırıyor.
>
> **Zehir oyuncunun üstüne döndüğünde** (3v3, tengu zehirli shuriken atıyor; kontrol aynı
> kadro): zafer %65.20'den %60.35'e, kaçış %8.10'dan %6.86'ya iniyor, ölüm %45.45'ten
> %50.44'e çıkıyor ve ölümlerin %1.3'ü doğrudan zehirden. §5'in merdiveni ayakta — kaçış
> hâlâ çalışıyor, yalnızca daha pahalı.

### Silahın elden düşmesi (kilitlendi 2026-09-03)

Zehir zırhın etrafından dolaşıyordu; düşürme zırhın **karşılığını** yazar. Plaka artık
yalnızca hasarı düşürmez, ona vuranın **kavrayışını da bozar**.

**Kural.** İki yerden gelir:

1. **Zırha inen vuruş** saldıranın kendi silahını elinden düşürebilir. Zar, silahın elden
   çıkma eğilimi ile **vurulan parçanın** sertliğinden çıkar; sertlik parçanın kopma
   direncinden okunur. Çıplak bölgeye inen vuruş **hiç** düşürmez — kavrayışı bozan şey
   et değil, plakadan dönen darbedir
2. **Yakalanan silah** çengelde avuçtan sökülebilir. Bu zar kilidin **üstüne binmez,
   yerine geçer**: elden çıkan silahla birlikte kenetlenme de çözülür, saldıran serbest
   kalır ama silahsız

Silah **kırılmaz, düşer**: arenada bir noktada durur ve dövüş bitince sahibine döner.
Bunun sebebi bilinçli — kırılma envanter, onarım ve yedek silah defteri açardı; oyunun
sorusu ekipman bakımı değil, **o dövüşün** nasıl gittiği.

**Yerden alma.** Düşen silahı **eli boş olan herkes** alabilir: düşüren, takım arkadaşı,
düşman. İki sınır var:

- **Elinde silah olan ne alır ne arar.** Yerdeki namluya bir adım bile atmaz; yoksa dövüş
  bir yağma turuna dönerdi
- **Kullanamayacağı silahı almaz**: kolunu kaybeden savaşçı yerdeki çift el silahı geçer

Silahsız savaşçı dövüşmeye devam eder — yumrukla (8 hasar, menzil 100). Kaybın büyüklüğü
silahtan silaha değişir: nodachi'sini düşüren savaşçı 34 hasarı ve 150 menzili birden
bırakır.

| Sınıf | Elden çıkma eğilimi | Gerekçe |
|---|---|---|
| Kesici | 1.0 | Plakaya saplanan ağız burkulur |
| Delici | 0.6 | Uç kayar, sap avuçta kalır |
| Künt | 0.2 | Geri tepen sopa avuçtan çıkmaz |
| Yumruk | 0 | Düşecek bir şey yok |

| Sayı | Değer |
|---|---|
| Zırha inen vuruşta taban şans | 0.05 |
| Yakalanan silahın düşme şansı | 0.05 |
| Sertlik payı (parçanın kopma direncinden) | 1.0 |
| Savrulma mesafesi | 250 birim, **karşıdakinin arkasına** |
| Alma mesafesi | 60 birim |

**Üç sınır kuralı:**

- **Mermi kimsenin silahını düşürmez** — fırlatılan şey zaten elden çıkmıştır
- **Düşürme kalıcı hale dokunmaz.** Çekirdek yalnızca olayı üretir (`WeaponDropped`,
  `WeaponPickedUp`); savaşçı dövüşten kendi kuşamıyla çıkar
- **Zar saldıranın silahına atılır**, savunanınkine değil

> **Ölçüm (1v1, 20.000 dövüş, `losing:0.7`, düşman ō-yoroi kuşanmış):**
>
> | Silah | Kural yok | Kural var | Düşürme | Yerden alınan |
> |---|---|---|---|---|
> | Nodachi (kesici) | %94.25 | %87.53 | %11.76 | %7.3 |
> | Tetsubo (künt) | %90.55 | **%89.20** | %2.73 | %4.8 |
> | Yari (delici) | %79.69 | %75.48 | %9.86 | %8.4 |
>
> **Takas plakanın önünde dönüyor.** Kural yokken kesici zırhlı düşmanın karşısında da
> önde (%94.25'e karşı %90.55); kuralla künt öne geçiyor. Künt sınıfın üçüncü kazancı
> budur ve tam olarak zırhın en kalın olduğu yerde ortaya çıkar.
>
> **Taban şans tarandı** (kesici / künt zaferi): 0.02'de %91.50 / %89.77 — kesici hâlâ
> önde, kural hiçbir şeyi çevirmiyor. 0.05 takasın döndüğü yer. 0.08'de %84.09 / %88.78,
> 0.20'de %72.93 / %86.83: kesici silah zırhlı düşmanın önünde taşınamaz hale geliyor.
>
> **Bedeli taşıyan şey mesafe değil, yön.** Silah sahibinin gerisine düşerse yerden alma
> yürüyüşü savaşçıyı dövüşten geri çeker ve **yavaş** bir düşmanın önünde düşürme bedava
> bir nefes molası olur — ölçüldü, savrulma mesafesi arttıkça oyuncunun zaferi
> *yükseliyordu* (%94.30 → %94.55, kural yokken %94.25). Yana savrulmak da aynı kapıya
> çıkıyor (%94.4). Karşıdakinin arkasına düşünce bedel gerçek oluyor, çünkü silaha gitmek
> düşmanın içinden geçmek demek. Mesafenin kendisi bundan sonra bir düğme değil: 150, 250
> ve 400 birim aynı sonucu veriyor (%87.43 / %87.45 / %87.48).
>
> **Yerden alma teke tekte çalışmaz, kalabalıkta çalışır:** 1v1'de düşen silahların
> %7.3'ü geri alınıyor, 3v3'te %40.4'ü. Hedef bölündüğü için kalabalıkta silahın başına
> gidilebiliyor — yani kuralın bedeli dövüşün şekline göre değişiyor, sabit bir ceza değil.
>
> **Yakalamanın düşürme şansı ayrı tarandı** (`jitte`/`sai`/`katana`, düşman kılıçlı ve
> zırhsız): 0'da %74.87 / %75.22 / %75.02 — yakalama aleti silah düşürmekle hiçbir şey
> kazanmaz. 0.05'te %78.00 / %78.88 / %75.02. **0.10'da fren kırılıyor** — jitte
> nodachi'ye karşı da doğru seçim olup (%38.27'ye karşı katana %37.84)
> `CatchTwoHandedFactor` anlamsızlaşıyor.
>
> **Kural yakalamayı üstün silah yapmıyor**, çünkü iki freni ayakta:
>
> | Eşleşme | Jitte | Katana |
> |---|---|---|
> | Kılıçlı, zırhsız oni | **%78.00** | %75.02 |
> | Çift el nodachi taşıyan oni | %35.22 | **%37.84** |
> | Ō-yoroi kuşanmış oni | %34.14 | **%60.81** |
>
> Her seçenek hâlâ bir şeyde en iyi: jitte kılıcın, katana zırhın, sai uzuv korumasının
> (%0.48) karşısında.
>
> **Takım bedeli** (3v3): yokai'lerin hepsi tam kuşam kuşandığında zafer %67.32'den
> **%65.35**'e iniyor ve oyuncu savaşçılarının %9.32'si dövüş içinde silahını düşürüyor.
> Kural olmadan düşmanı zırhlandırmak oyuncunun **işine yarıyordu** (%65.20 → %67.32;
> ağır kuşam vuruşu geciktirir); kural bunu tersine çeviriyor. Kalabalıkta düşen silahın
> %40'ı geri alındığı için duvar teke tekteki kadar sert değil.

### Zırh yuva yuvadır

Zırh tek bir sayı değil; **kafa / gövde / kılıç kolu / boştaki kol / sağ bacak /
sol bacak** için ayrı parçalardan oluşur — altı yuva. Hasar azaltımı ve uzuv kopma
direnci, darbenin **indiği bölgenin** parçasından okunur.

| Takım | Kafa | Gövde | Kılıç kolu | Boş kol | Sağ bacak | Sol bacak |
|---|---|---|---|---|---|---|
| Hafif keikogi | — | keikogi | — | — | — | — |
| Dō-maru | — | dō | kote | kote | suneate | suneate |
| Ō-yoroi | kabuto | ō-yoroi gövdeliği | ağır kote | ağır kote | ağır suneate | ağır suneate |

> **Neden uzuv uzuv (2026-09-02):** tek bir "kol" yuvası iki kolluğu tek parça sayıyordu
> ve kolunu kaybetmiş savaşçının kalan kolunu temsil edemiyordu. Ayrılınca hem tek kolu
> zırhlamak mümkün olur hem de kaybın kendisi taraflanır: kılıç kolu ayrı, boştaki kol
> ayrı bir kayıptır (aşağıda "Kalıcı cezalar").

> **Neden yuva yuva:** direnç tek skaler kaldığı sürece "iyi zırh" tek eksende ilerler ve
> ekipmanın asıl ilginç kararı — **ağır göğüslük, çıplak kollar**: ucuz ve hızlı, ama eve
> kolsuz dönme ihtimali yüksek — hiç var olmaz. Ölçümde bu doğrudan görünüyor: dō-maru'nun
> kafası açık olduğu için göz kaybı, kol ve bacak kaybının aksine ancak yarıya iniyor.

### Zırhın ağırlığı — kilitlendi (2026-09-02)

Zırhın dövüş içi bir bedeli olmadığı sürece kuşam bir **karar** değil, cüzdan kontrolüdür:
ölçümde ō-yoroi zaferi %69'dan %92'ye çıkarıyor, ölümü %45.5'ten %21'e, uzuv kaybını
%7.45'ten %0.61'e indiriyor ve karşılığında hiçbir şey ödemiyordu. Tek freni fiyattı, o da
ekonomi sayıları (Açık Karar #5) gelene kadar yok.

Ağırlık bunu sahaya taşır. Parça başına bir ağırlık vardır; takımın toplamı **saldırı
döngüsünü uzatır**.

| Parça | Ağırlık | | Takım | Toplam |
|---|---|---|---|---|
| Keikogi | 1 | | Zırhsız | 0 |
| Dō-maru gövdeliği | 4 | | Hafif keikogi | 1 |
| Ō-yoroi gövdeliği | 7 | | Dō-maru | 7 |
| Kote / ağır kote | 1.5 / 3 | | Ō-yoroi | 16 |
| Suneate / ağır suneate | 1.5 / 3 | | | |
| Kabuto | 3 | | | |

| Sayı | Değer | Ne yapar |
|---|---|---|
| `ArmorWeightAtFullPenalty` | 16 | Cezanın tamamının uygulandığı ağırlık — tam ō-yoroi |
| `ArmorAttackSlowdownAtFullWeight` | 0.75 | Tam kuşamda saldırı döngüsünün uzama oranı |

**0.75, takasın döndüğü eşiktir.** Üç kademe de bir şeyde en iyi olur: dō-maru dövüşü
kazanır (%71.8 zafer, %40.3 ölüm), ō-yoroi sakat dönmemeyi alır (uzuv kaybı %0.82'ye karşı
%3.38), hafif kuşam ucuz ve ağırlıksızdır. 0.60'ta ō-yoroi hâlâ her eksende önde
(%76.3 zafer, %37.0 ölüm); 0.90'da ağır kuşam düpedüz kötü (%64.3 zafer).

### Zırhın yıpranması ve dağılması (kilitlendi 2026-09-03)

Zırhın iki bedeli vardı: fiyat ve ağırlık. Üçüncüsü bu — **zırh bir sarf malzemesidir.**

**Kural.** Bir parça durdurduğu hasar kadar yıpranır: emdiği her puan kendi dayanıklılık
havuzundan düşer. Havuz bitince parça **ortada dağılır** ve o bölge dövüşün geri kalanında
çıplak kalır; dağılan parça **kalıcı olarak gider**. Silahla arasındaki fark kasıtlı —
düşen silah dövüş sonunda geri gelir, dağılan plaka gelmez.

**Yıpranma dövüşe değil savaşçıya aittir.** Bir parça tek dövüşte tükenmez; seferler
boyunca aşınır ve bir gün, kimsenin beklemediği bir dövüşün ortasında dağılır. Çekirdek
kalıcı hale dokunmaz: yıpranmayı ve dağılan yuvaları dövüş özetine yazar
(`ArmorDestroyed`), defteri dojo tutar — uzuv kaybındaki yolun aynısı.

**Havuz emilen hasardan düşer, gelenden değil.** Parçayı yıpratan şey durdurduğu darbedir.
Gelen hasardan düşseydi ince kumaş ile kalın plaka aynı hızda tükenir, kademe farkı
yalnızca sayıda kalırdı.

| Parça | Azaltım | Dayanıklılık |
|---|---|---|
| Keikogi | 4 | 40 |
| Dō-maru gövdeliği | 9 | 110 |
| Ō-yoroi gövdeliği | 14 | 180 |
| Kote / ağır kote | 4 / 6 | 45 / 75 |
| Suneate / ağır suneate | 4 / 6 | 45 / 75 |
| Kabuto | 8 | 90 |

Dağılan parça **ağırlığını da bırakır**: koruması giden savaşçı hızını geri alır. Aynı
şekilde sertliğini de bırakır — çıplak kalan bölge artık düşmanın silahını elinden
düşürmez.

> **Ölçüm (3v3, 20.000 dövüş, `losing:0.7`):**
>
> | Kuşam | Dövüş başına yıpranma | Takımın toplam dayanıklılığı | Ömür |
> |---|---|---|---|
> | Hafif keikogi | 5.4 | 40 | ~7 dövüş |
> | Dō-maru | 20.2 | 290 | ~14 dövüş |
> | Ō-yoroi | 38.7 | 570 | ~15 dövüş |
>
> Sayılar kuralın asıl iddiasını doğruluyor: **en çok emen kuşam en çok yıpranandır.**
> Ō-yoroi hafif kuşamın yedi katı hasar emiyor ve karşılığında yedi katından biraz fazla
> dayanıklılık taşıyor — yani pahalı kuşam korumayı satın alır, bedavaya değil.
>
> **Tek bir dövüşte parça dağılmaz** (varsayılan havuzlarda 20.000 dövüşte sıfır);
> dağılma anı, yıpranmış bir kuşamla sahaya çıkıldığında gelir. Havuz ölçeği düşürülerek
> o an ölçüldü: 0.25'te savaşçı başına 0.12 parça dağılıyor (zafer %69.92, uzuv kaybı
> %0.94), 0.1'de 0.89 parça (zafer %67.55, uzuv kaybı **%1.93**). Yani yıpranmış zırhla
> dövüşe girmek yalnızca daha çok hasar yemek değil, **uzuv kaybının iki katına çıkması**
> demek — kuşamı yenilememenin bedeli roster'da görünüyor.
>
> **Zincirin ucu:** zırhı dağılan düşman artık kimsenin silahını düşürmüyor. Yokai'lerin
> hepsi tam kuşamken oyuncunun silahını düşürme oranı %1.81; kuşam yıpranmış girdiğinde
> %1.41'e iniyor (zafer %65.35 → %63.83). Zırh gittikçe dövüş **daha ölümcül ama daha
> temiz** hale geliyor: daha çok kesik, daha az elden düşen silah.

### Denenip düşen iki hat

Ağırlık üç yerden ısırabilirdi; ikisi ölçümde düştü.

- **Stamina toparlanması: ölçülen sıfır.** Tam kuşamda rejenden %90 kesmek 3v3 zaferini
  %92.34'ten %92.33'e taşıdı. Stamina bu dövüşte zaten bağlayıcı değil; knob silindi.
- **Yürüme hızı: kazancı ölçülemedi, bedeli §5'in vaadi.** Hız cezası zaferi hiç
  kıpırdatmadı ama "Kaç" tuşunu işlevsiz bıraktı: kaçış sayaçla değil **mesafeyle** bittiği
  için (§5) kuşanmış savaşçı arenayı terk edemeden yetişiliyor. Tuşun kazandırdığı ölüm
  farkı %25 cezada 2.0 puandan **0.0 puana** indi (çeken %46.35, çekilmeyen %46.44; ceza
  yokken %44.32'ye karşı %46.33). Bu yüzden ağırlık yürüyüşe dokunmaz.

Kalan tek hat saldırı hızıdır ve sebebi ortak: **dövüş hasar alışverişiyle bitiyor**, o
alışverişe dokunmayan hiçbir ceza ölçülmüyor.

### İsabet bölgesi

Her isabet bir bölgeye iner. Ağırlıklar kasıtlı olarak eşit değil:

| Bölge | Ağırlık |
|---|---|
| Gövde | 45 |
| Bacak (her biri 12.5) | 25 |
| Kol (her biri 10) | 20 |
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

Ölçüldü (3v3, 20.000 dövüş, `losing:0.7` oyuncu modeli, ağırlık kilitli). Denge hedefi
bu tablodur:

| Kuşam | Açıkta kalan | Ağırlık | Ölüm | **Uzuv kaybı** | Zafer |
|---|---|---|---|---|---|
| Zırhsız | hepsi | 0 | %45.5 | **%7.45** | %69.1 |
| Hafif keikogi | kol, bacak, kafa | 1 | %42.5 | **%6.04** | %71.3 |
| Dō-maru | kafa | 7 | %40.3 | **%3.38** | %71.8 |
| Ō-yoroi | — | 16 | %41.6 | **%0.82** | %71.5 |

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
| Kılıç kolu | Saldırı gücü ×0.65, iki elli silah kullanamaz, tek elli animasyon setine geçer |
| Boştaki kol | Saldırı gücü ×0.85, iki elli silah yine kullanamaz |
| Bacak (her biri) | Kaçınma ×0.55, yürüme hızı ×0.60, topallama animasyonu |
| Göz | İsabet oranı ×0.75 |

Kayıplar birleşir: iki bacağını kaybeden savaşçının hızı ×0.36'ya iner. Kılıç kolu ile
boştaki kolun ayrılmasının sebebi, birincisinin vuruşun kendisi, ikincisinin denge
olması — ikisi de iki elli silahı bitirir ama tek elli dövüşen için boştaki kolun kaybı
taşınabilir bir kayıptır.

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

### Günün teklifi nasıl üretilir (2026-09-04)

Teklif **gün ile seferin tohumunun saf bir fonksiyonudur**: aynı gün, aynı tohum, daima
aynı teklif. Bu yüzden teklif kayıt dosyasına yazılmaz — dosyada gün ve tohum durur,
teklif yüklerken yeniden hesaplanır. Kaydı yükleyip beğenilmeyen teklifi yeniden yükleyerek
değiştirmek de bu sayede işe yaramaz.

- **Güç** tek bir eğri: 1. gün `StartingPower`, her gün `PowerPerDay` kadar artar, üstüne
  o güne düşen bir dalgalanma biner. Dalgalanma şart — eğri düz bir çizgi olsaydı her gün
  aynı teklif gelirdi ve "bırak" kararının anlamı kalmazdı; bırakmanın anlamı yarının
  farklı olabilmesi.
- **Kadro büyüklüğü** güçle açılır (ikinci düşman, sonra üçüncü) ama üst sınıra yapışmaz:
  aynı güç bazen tek ağır düşman, bazen üç düşman demek. İkisi aynı dövüş değil — biri
  hedef seçimini, diğeri dayanmayı sınar.
- **Kalabalık gücü bölmez:** üç düşman üç kat düşmandır. Bölseydi kalabalık teklif aynı
  tehdidi daha az canla taşırdı, yani aynı riski daha az ödülle satardı — ödül düşman
  canına bağlı (§11).
- **Yokai türleri eğride yer tutar:** kappa ve kitsune baştan, tengu 1.2, oni 1.5,
  jorōgumo 1.8 gücünden sonra sahaya çıkar.
- **Düello:** tekliflerin bir kısmı tam bir savaşçı dayatır (§10'un "encounter kendi
  kuralını yazar" maddesi). Ölçümde en pahalı kalem bu — tek savaşçının yanında hasarı
  paylaşacak kimse yok.

Güç, can ve hasarı **doğrudan**; isabet, savunma ve kaçınmayı **karekökle** büyütür.
Doğrusal büyütmede eğri bir yerde duvara dönüşüyor: düşman ıskalanmaz olurken oyuncu
ıskalar hâle geliyor ve zorluk artışı iki kere sayılıyor.

**Sayılar kilitli değil** (eğrinin dikliği, dalgalanma payı, düello oranı). Aşağıdaki
ölçüm neyin ölçüldüğünü söylüyor, hangi eğrinin doğru olduğunu değil.

### "Al ya da bırak" ölçüldü (2026-09-04)

500 dojo × 120 gün, üretilen tekliflerle. Tek fark politikanın teklifi eleme hakkı:

| Politika | Kapanan dojo | Ayakta kalınan gün (ortanca) | Ölüm / savaşçı-dövüş | Aç gün | Geri çevrilen |
|---|---|---|---|---|---|
| Her teklife girer | %99.2 | 54 | %9.2 | %7.1 | %0 |
| Kadro eksikken ağır teklifi çevirir | %0.4 | 120 (hepsi) | %7.1 | %50.8 | %67.7 |

İki başarısızlık birbirinin zıddı: **hiç reddetmeyen dojo kadrosunu, her ağırı reddeden
dojo kasasını kaybediyor** (ikincisinin bitiş kasası 0 ve günlerinin yarısı aç geçiyor).
Tuşun kendisi böylece kanıtlanmış oluyor — reddetmek bir kaçış değil, gün ile savaşçı
arasında yapılan bir takas.

> Ölçümün ikinci sonucu ekonomi turunun sınırını doğruluyor (§11): savaşçı-dövüş başına
> ölüm %9'a çıktığında hiçbir fiyat ayarı dojo'yu ayakta tutmuyor.

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

### Tek para birimi: altın (kilitlendi 2026-09-04)

Yiyecek, su ve ilaç ambarda sayılabilir stok olarak durur ama piyasadan **altınla**
alınır. Sebep: ekonominin kıt kaynağı bölünmezse "bugün neye harcayayım" kararı tek bir
sayıya bakar. Kasa **eksiye düşmez** — borç diye bir kalem yoktur; parası yetmeyen
alışveriş yapılmaz.

### Fiyatlar (kilitlendi 2026-09-04)

| Kalem | Sayı | Neyle ölçeklenir |
|---|---|---|
| Zafer ödülü | **0.45 altın / düşman canı** | Karşılaşmanın kendisi — aynı düşman aynı parayı öder |
| Çekilme / bozgun ödülü | **0** | §10: pes etmek o seferin ödülünü siler |
| Zırh parçası | **1.50 altın / dayanıklılık puanı** | Keikogi 60, dō-maru 165, ō-yoroi gövdeliği 270 |
| Onarım | **0.90 altın / yıpranma puanı** | Parçanın havuzundan fazlası ödenmez |
| Yiyecek / su | **2 / 1 altın**, savaşçı başına günde 1'er | Kadro büyüklüğü |
| İlaç | **12 altın**, revirdeki savaşçı başına günde 1 | Yaralı sayısı |
| Savaşçı alımı | **150 altın** | — |
| Başlangıç sermayesi | **600 altın** | — |

**Onarım daima yenilemeden ucuzdur** (0.90 < 1.50). Eşit ya da pahalı olsaydı onarım
diye bir karar kalmazdı: herkes parçayı dağılana kadar kullanıp yenisini alırdı. Aradaki
fark, "erken onar mı, sonuna kadar kullan mı" kararının tamamıdır — ölçümde iki uç
neredeyse başa baş çıkıyor (60 gün, erken onaran 959 altınla, sonuna kadar kullanan 1012
altınla bitiriyor), ama sonuna kadar kullanan dojo dövüş başına 3.36 parçayı **dövüşün
ortasında** kaybediyor. Altın eşitken riski taşıyan tarafı seçmek oyuncunun kararıdır.

### İlaç zorunlu vergi değil, hızlandırıcı

İlaçsız gün de bir revir günü eritir; ilaçlı gün iki. İlaç bu yüzden ekonominin en büyük
kalemi: günlük 34.5 altınlık tüketimin üçte ikisi ilaçtan gelir ve fiyatı doğrudan
takvimi belirler — bedava ilaçla boş gün oranı %28.3, 12 altında %33.8, 24 altında %59.1.
Yani "bugün sefere mi, revire mi" baskısını yaratan kalem ilacın fiyatıdır.

### Kıtlığın bedeli zaman, ölüm değil

Ambar yetmezse **revirdekiler önce** doyar. Aç kalan savaşçı o gün ne iyileşir ne
antrenman yapar; kimse açlıktan ölmez. Sıra keyfî olamazdı: yaralıyı aç bırakmak kıtlığı
telafisi olmayan bir cezaya çevirirdi.

### Savaşçı pazarı (2026-09-04)

Savaşçı almak bir **seçim**, bir düğme değil. Sabit statlı sabit fiyatlı savaşçıda "kimi
alayım" diye bir soru yoktur; para varsa alınır, yoksa alınmaz. Model Domina'nın köle
pazarından birebir alındı:

- **Adaylar farklı statlarla gelir ve statlar alım öncesi görünür.** Pazarlıkta gizli bir
  şey yok; karar, görünen statlarla fiyat arasında yapılır.
- **Fiyat statın kendisinden çıkar** — adayın taban savaşçıya göre ne kadar iyi olduğu
  fiyatı belirler. Sabit fiyat iyi adayı bedava, kötü adayı soygun yapardı.
- **Pazar kadronun seviyesini takip eder** (`RosterFollow` 0.7): erken oyunda usta savaşçı
  satışa çıkmaz, kadro geliştikçe pazar da gelişir. Takip olmasaydı ya baştan her şey
  satın alınabilirdi (antrenmanın anlamı kalmazdı), ya da geç oyunda pazar anlamsızlaşırdı.
  Kadro tamamen ölse bile pazar **acemi seviyesine** düşer, sıfıra değil — yoksa çöken
  dojo'nun toparlanma yolu kalmazdı.
- **Yetenek (`Talent`) ikinci eksen:** savaşçının antrenmandan ne kadar hızlı
  faydalanacağı, doğuştan gelen ve değişmeyen payı (0.6-1.4). Aynı statlarla gelen iki
  aday aynı hızda gelişmez. Fiyata girer ama statlardan daha az ağırlıkla: yetenek bir
  **vaat**, stat ise elde olan. **Dövüş bunu okumaz**, yalnızca antrenman okuyacak.
- **Liste birkaç günde bir yenilenir** (`RefreshDays` 2) ve **gün içinde donar**. Her gün
  yenilenseydi beğenilmeyen kadro bir gün beklenerek düzeltilir, seçim ertelenirdi. Gün
  içinde donmasaydı — pazar kadro ortalamasını takip ettiği için — bir aday satın almak
  kalan adayları değiştirir, liste istenildiği kadar çevrilebilirdi.

**Ölçüm (400 dojo × 60 gün, `patrol`):** iki alım stratejisi karşılaştırıldı.

| Alım politikası | Bitiş kasası | Sermayesini koruyan | Ölüm (dojo başına) | Kapanan dojo |
|---|---|---|---|---|
| Sabit fiyat, sabit stat (eski) | 815 | %60.8 | 6.21 | %1.5 |
| Pazardan **altın başına en çok stat** | 354 | %26.5 | 7.16 | %11.8 |
| Pazardan **parası yeten en iyisi** | 705 | %52.2 | 5.93 | %4.8 |

İki şey görünüyor. Birincisi, eski model **yerine koymayı sübvanse ediyormuş**: 150 altına
veteran kalitesinde savaşçı geliyordu, pazar bunu gerçekçi fiyata çekince dojo zorlanıyor.
İkincisi ve daha önemlisi, **"ucuz ham aday al" stratejisi şu an açıkça kaybediyor** —
çünkü ham aday gelişmiyor: antrenmanın stat etkisi henüz yazılmadı (ROADMAP Faz 3). İki
stratejinin rakip olması tasarımın hedefi; aradaki fark (354'e karşı 705 altın) antrenman
sisteminin kapatması gereken boşluğun ölçüsüdür.

### Rastgele olaylar (2026-09-04)

Günde **%15** olasılıkla bir aksilik çıkar. Beş tür var. Hepsi eksiltir — bağış, hazine, iyi haber yok:
§11 olayları tampon baskısı olarak tarif ediyor, çift yönlü bir tablo baskıyı ortadan
kaldırırdı.

| Olay | Etkisi |
|---|---|
| Hırsızlık | Kasanın **en fazla %12'si** gider; boş kasadan bir şey alınmaz |
| Erzak bozulması | O günün yiyeceği **en fazla iki katına** çıkar |
| Kuyunun bulanması | O günün suyu **en fazla iki katına** çıkar |
| İlacın küflenmesi | O gün revirdekiler ilaçsız kalır — iyileşme doğal hıza düşer |
| Hastalık | Sağlam bir savaşçı dövüşmeden **1-3 gün** revire düşer |

**Şiddet sabit değil, her seferinde sıfır ile üst sınır arasında çekilir.** Sabit oran
aksiliği hesaplanabilir bir vergiye çevirirdi: oyuncu kaybı baştan bilirse tampon tutmak
bir karar değil, aritmetik olurdu.

**Boş kasada hırsızlığın boşa düşmesi kasıtlı (karar 2026-09-04).** Kasanın boş olması
kalıcı bir hâl değil: oyuncu zırha, onarıma ya da yeni savaşçıya para biriktirdiği anda
kasa dolar — hırsızlık tam o anda ısırır. Yani olay, biriktirme kararının bedeli olarak
çalışır. Boş kasaya karşılık üretmek (erzağa el atmak gibi) bu bağı koparır ve batmakta
olan dojo'yu ikinci kez cezalandırırdı.

**Etki ambara değil, kasaya ve takvime vurur.** Günlük alışveriş ambarı tam ihtiyaç kadar
doldurduğu için (stok biriktirilmiyor) çalınan erzağın karşılığı sıfır olurdu; bu yüzden
olaylar ya altını götürüyor, ya o günün ihtiyacını büyütüyor, ya da bir savaşçının gününü
alıyor. Aksilik **upkeep'ten önce** işlenir: bozulan erzak o günün alışverişini
pahalılaştırmalı, ertesi güne ötelenmemeli.

Olay da teklif gibi gün ile tohumun **saf** fonksiyonudur (kayda yazılmaz, yeniden
yüklenerek değiştirilemez) ama **ayrı bir tuzla** üretilir: aynı tuz kullanılsaydı ağır
teklifin geldiği gün daima hırsızlık da olurdu ve iki sistem tek bir sisteme dönüşürdü.

**Ölçüm** (400 dojo × 60 gün, `patrol`): aksilik oranı tamponu yiyor ama oyunu tek başına
bitirmiyor.

| Günlük olay olasılığı | Bitiş kasası | Sermayesini koruyan | Aç gün | Kapanan dojo |
|---|---|---|---|---|
| %0 | 945 | %66.2 | %4.8 | %1.2 |
| **%15 (seçilen)** | **815** | **%60.8** | **%6.1** | **%1.5** |
| %30 | 562 | %44.8 | %9.4 | %3.0 |

%15'te tamponun yaklaşık yedide biri aksiliklere gidiyor — baskı hissediliyor, ama kasayı
belirleyen kalem hâlâ ilaç ve savaşçı. (Sayılar şiddetin rastgeleleştirilmiş hâline aittir;
sabit oranlı ilk ölçümde kayıp daha büyüktü: 766 altın, %58.5.) Sayı **kilitli değil**; oran ve şiddet Faz 9'da
diğer sayılarla birlikte kapanacak.

### Ölçüm (1000 dojo × 60 gün, `patrol` senaryosu)

Ekonomi ayrı bir senaryoda ölçüldü. Diğer senaryolar birer **denge sondası**dır ve kasten
ağır kurulmuşlardır — savaşçı-dövüş başına ölüm oranları %38-49 bandında. Böyle bir dövüş
her gün yapılamaz; o kadroyla ölçülen fiyat aslında savaşçı fiyatını ölçerdi. `patrol`
sıradan günün karşılaşmasıdır: zafer %98.6, savaşçı-dövüş başına ölüm %7.

| Ölçü | Sonuç |
|---|---|
| Dövüş başına gelir / kuşam gideri / net | 114.5 / 31.1 / **31.3 altın** |
| Günlük tüketim | 34.5 altın |
| 60 günde dövüş | 39.4 (dojo başına) |
| Boş gün (kadro sefere yetmiyor) | %33.8 |
| Aç gün | %4.4 |
| Ölüm / alınan savaşçı (dojo başına) | 6.48 / 5.90 |
| Sermayesini koruyan dojo | %66.4 |
| Kapanan dojo | %1.2 |

Ödül eğrisi dar: 0.35'te dojoların yalnızca %14.3'ü sermayesini koruyor ve %10.4 gün aç
geçiyor; 0.70'te kasa 3719 altına çıkıp para kısıt olmaktan çıkıyor. 0.45 ikisinin
arasındaki dizdir — dojo ayakta kalır ama zenginleşmez, ve günlerin üçte biri revir
bekleyerek geçer.

> **Ölçümün söylediği asıl şey:** ekonominin bağlayıcı kısıtı altın değil, **kadro**.
> Karşılaşma zorluğu savaşçı-dövüş başına %20'nin üstüne çıktığında hiçbir fiyat ayarı
> dojo'yu ayakta tutmuyor — gelir savaşçı yerine koymaya gidiyor ve kadro erimeye başlıyor.
> Zorluk eğrisi (§10) bu yüzden ekonominin de eğrisidir.

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

## Fikir Defteri (karar değil)

Burası kararlaştırılmamış, ama unutulmaması istenen fikirler içindir. Hiçbiri plana
girmiş bir iş değildir; Açık Kararlar tablosuyla karıştırılmamalı.

### Protez ve sakat savaşçı gereci

Sakatlık şu an tek yönlü: savaşçı zayıflar, telafi yolu yoktur (§7 "Kalıcı cezalar").
Bir gün telafi istenirse şekli şu olurdu — **protez yuvası**: tekagi (tek kollu için
pençe), takma bacak, gözü giden için dürbünlü miğfer gibi parçalar sakatlığın çarpanını
kısmen geri verir. Değerli tarafı, §7'nin bıraktığı "emekliye ayır mı, kullanmaya devam
mı" kararını **"yatırım yap mı"**ya çevirmesi: sakat veteran, uğruna para harcanan bir
karakter olur. Zenginleştirilmiş hâli her parçaya kendi bedelini de yükler (tekagi gücü
geri verir ama blok yapamaz gibi).

Şimdilik yazılmadı: 4-D'nin ölçülebilir yarısı (ağırlık, yuvalar) kilitlendi, bu ise
yeni bir ekipman yuvası ve yeni bir ölçüm turu demek.

---

## Açık Kararlar

| # | Konu | Not |
|---|---|---|
| ~~1~~ | ~~Parti boyutu~~ | **Kilitlendi (2026-08-29).** Üst sınır **4**, sayı oyuncunun kararı; düello/baskın gibi encounter'lar tam sayı dayatabilir (§10). Çekirdek zaten N savaşçı destekliyor. **Takip eden iş:** dört savaşçılık arena 2.2'de kamera ve okunurluk sorunu çıkarır |
| ~~2~~ | ~~Sefer/harita yapısı~~ | **Kapandı (2026-08-29).** Sefer tek oda/tek dövüş; günde **tek karşılaşma teklifi**, al ya da bırak; harita ekranı yok. **Boss yapısı kurulmuyor** — zorluk tek eğri üzerinde artar (§10) |
| 3 | Yokai bestiary detayı | Hangi yokai'ler, her birinin özel dövüş davranışı. **Girdisi hazır (2026-09-03):** davranış farkı ayrı kod değil, §4'teki hedef seçimi ağırlıklarının yokai'ye göre ayarlanmış hâli olacak. **Sayı tarafı yazıldı (2026-09-04):** `Domina.Core/Campaign/Bestiary.cs` — kappa, kitsune, tengu, oni, jorōgumo tek bir güç eğrisiyle ölçekleniyor ve eğride yer tutuyor (§10). Nue ve boss adayları yok: §10 boss yapısı kurmuyor. **Açık kalan yalnızca davranış** — o alan kalıba eklendiğinde encounter üretimi değişmez |
| 4-A | Ekipman — yakın dövüş silahları | **Kilitlendi.** Mevcut `Weapon` modeline sığar (fabrika + denge sayısı): wakizashi, tantō, naginata, kanabō, kama, bō/jō, ono, tekagi. Kavrayış hatları §4'te |
| 4-B | Ekipman — yeni kural gerektirenler | **Sersemletme kilitlendi (2026-09-02)** — kural ve sayılar §7'de; künt sınıfın karşılığı artık var (kesici %91.57 / künt %88.68 iken ikisi de ~%92). **Kılıç yakalama kilitlendi (2026-09-03)** — jitte/sai artık kalkanın bıraktığı boşluğu dolduruyor: taban şans 0.24, kilit 0.6 sn, çift el silaha karşı ×0.75; üç seçenek de bir şeyde en iyi (katana zaferde %73.09, sai uzuv korumasında %0.45). **Zehir kilitlendi (2026-09-03)** — doz zırhın etrafından dolaşır: tik başına 2.5, tik 1.0 sn, ömür 6.0 sn, azami doz 3.0; zehirli tantō açık dövüşte katana ile başa baş (%72.19'a karşı %73.09), zırhlı düşmanın önünde önde (%77.19'a karşı %68.62). Zehir uzuv koparmaz, sersemletmez ve çekilende de durmaz. **Silahın elden düşmesi kilitlendi (2026-09-03)** — zırhın karşılığı: zırha inen vuruşta taban şans 0.05, yakalanan silahta 0.05, elden çıkma eğilimi kesici 1.0 / delici 0.6 / künt 0.2. Silah kırılmaz, karşıdakinin arkasına 250 birim savrulur; eli boş olan herkes (düşüren, takım arkadaşı, düşman) alabilir, elinde silah olan ne alır ne arar. Ō-yoroi kuşanmış düşmanın önünde takas dönüyor (nodachi %87.53, tetsubo %89.20); yerden alma teke tekte %7.3, 3v3'te %40.4. **Madde kapandı** — kural ve sayılar §7'de. **Kalkan yok:** elde taşınan kalkan Japon savaşında yaygın değil (*tate* yere dayanan sabit siperdir); aynı mekanik ihtiyacı jitte/sai karşılar |
| 4-C | ~~Ekipman — uzam/mermi gerektirenler~~ | **Kapandı (2026-08-14).** Çekirdek mermi kazandı: `ThrownWeapon` ayrı bir yuvada taşınır, atış havada süre geçirir, uçuş sırasında hedef kaçabilir/ölebilir/sahadan çıkabilir. Yumi ve fukiya aynı yoldan gelir — yalnızca menzil/hız/cephane sayıları farklıdır. Makibishi hâlâ açık: o bir sarf malzemesi, mermi değil |
| ~~4-D~~ | ~~Ekipman — zırh ve sakat savaşçı~~ | **Zırh yıpranması eklendi (2026-09-03):** parça durdurduğu hasar kadar aşınır, havuzu bitince ortada dağılır ve **kalıcı olarak gider**; yıpranma savaşçıda birikir, tek dövüşte tükenmez (ō-yoroi ~15 dövüş, keikogi ~7). Kural ve sayılar §7'de. **Kilitlendi (2026-09-02).** Zırh üç kademe (keikogi / dō-maru / ō-yoroi) **altı yuvada** taşınır: kafa, gövde, kılıç kolu, boştaki kol, sağ bacak, sol bacak. Kuşamın bir **ağırlığı** vardır ve saldırı döngüsünü uzatır (`ArmorAttackSlowdownAtFullWeight` 0.75, tam ō-yoroi = 16) — §7'deki tablo. Sakat savaşçının cezaları taraflandı: kılıç kolu ×0.65, boştaki kol ×0.85, her bacak ×0.55 kaçınma / ×0.60 hız. **Sakata özel ekipman (protez) yazılmadı** — fikir olarak "Fikir Defteri"nde duruyor, açık karar değil |
| ~~5~~ | ~~Ekonomi sayıları~~ | **Kilitlendi (2026-09-04).** Tek para birimi altın; fiyatlar ve günlük tüketim §11'de tabloyla. Zafer ödülü düşman canı başına 0.45, zırh 1.50/dayanıklılık, onarım 0.90/yıpranma, ilaç 12, savaşçı 150, başlangıç 600. Ölçüm 1000 dojo × 60 gün (`patrol` senaryosu): dövüş başına net 31.3 altın, boş gün %33.8, kapanan dojo %1.2. **Ölçümün ortaya çıkardığı kısıt:** bağlayıcı kaynak altın değil kadro — karşılaşma zorluğu savaşçı-dövüş başına %20 ölümün üstüne çıkınca hiçbir fiyat dojo'yu ayakta tutmuyor. **Rastgele olaylar da eklendi (2026-09-04):** günde %15, beş tür, hepsi eksiltir; ölçüm §11'de (tamponun ~beşte biri). **Açık kalan:** isteğe bağlı riskli maçlar (§11) |
| ~~6~~ | ~~Görsel stil~~ | **Kilitlendi 2026-08-13 — bkz. §12** |
| 7 | Oyun adı | Henüz yok ("Domina" sadece klasör adı — final isim değil) |
| ~~12~~ | ~~Blok ayrı bir durum mu~~ | **Kilitlendi (2026-09-03).** Ayrı durum: `CombatState.Blocking`. Karar Savunma statından (`Savunma ÷ 100 × 0.45`), şart gelen vuruşu okumak, süre 0.8 sn, tuttuğu hasarın %70'i × silahın blok kalitesi. Bloklanan darbe uzuv koparmaz, künt sarsıntı %75 geçer. Ölçüldü: zafer %71.21 → %72.39, uzuv kaybı %5.19 → %4.96. Kural ve sayılar §5'te. **Kuralın kendi freni yok** — `MaxBlockChance` büyüdükçe tek yönlü kazanç; freni statın dojo'da yarışması, sayı Faz 9'da |
| 8 | Onur eşik sayıları | Seppuku eşiği, decay hızı, hedefli komut etki katsayısı — playtest ile. **Kaçmanın onur bedeli de burada** (`RetreatHonorPenalty`, §5): kural kilitli, sayı değil |
| ~~9~~ | ~~Kaçışta kısmi ödül~~ | **Düştü (2026-08-29).** Sefer tek dövüşse önceki odalarda toplanmış ganimet diye bir şey yok; çekilmek o dövüşün ödülünü siler, o kadar (§10) |
| ~~10~~ | ~~Seferin peşin bedeli~~ | **Kapandı (2026-08-29).** Girmek **bir gün** yer (kaçılsa da), düşman kadrosu **kısmen** görünür — yalnızca tehdit işareti (§10) |
| ~~11~~ | ~~Hücum sayıları~~ | **Kilitlendi (2026-09-02)** — tablo §4'te. Mesafe 320, olasılık 0.40, birikme 0.75 sn, hız 1.6, hasar 1.5. Ölçüm sırasında **birikme aşaması eklendi**: hücumun bedeli yalnızca yazılıydı, koşu 0.6 sn sürdüğü için hiç ölçülmüyordu. Ölçüm sırasında ayrıca **yeniden tutuşma** ve **statlar + kalabalık** kuralları eklendi; hücum artık açılış hamlesi değil ve karar savaşçının kimliğinden çıkıyor. **Takip eden iş:** yokai bestiary'sinde (#3) "kime hücum eder" bir karakter özelliği olarak kullanılabilir — düşüncesizce hücum eden yokai savunmasızlığın bedelini öder |
