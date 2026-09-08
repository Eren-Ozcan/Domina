# Terimler

Projede geçen Japonca terimler ve çekirdeğin mekanik sözcükleri. Amaç, GDD ya da kod
okurken terimin ne olduğunu ve **oyunda ne iş yaptığını** tek yerden görebilmek.
Sayılar burada özet olarak durur; bağlayıcı olan `docs/GDD.md`'dir.

---

## Silahlar

### Tek el

| Terim | Nedir | Oyundaki işi |
|---|---|---|
| **katana** | Klasik samuray kılıcı, kesici | Dengeli seçenek. Ölçümde "en iyi olduğu yer" **zırhlı düşman** |
| **wakizashi** | Kısa kılıç, katananın yardımcısı | Daha hızlı, daha az hasar |
| **tantō** | Hançer | Zehirli hâli var: 7 hasar / 0.85 sn |
| **kama** | Orak; kısa saplı, kesici | Tarım aleti kökenli kısa kesici |
| **ono** | Balta | Ağır tek el kesici |
| **tekagi** | Parmaklara takılan pençe/kanca | Kısa menzilli tırmalayıcı |
| **jitte** | **Çatallı demir çubuk, keskin değil.** Edo dönemi kolluk silahı: çatalıyla kılıcı kırmadan tutar | Kılıç yakalamanın ana aleti (kavrayış 1.0). 14 hasar / 1.00 sn, künt sınıfı |
| **sai** | Üç çatallı demir sopa, jitte'nin akrabası (iki yanda da çatal) | Daha çok yakalar (kavrayış 1.25; dövüş başına 3.71 yakalama, jitte 2.75). 14 hasar / 1.05 sn, künt |

### Çift el

| Terim | Nedir | Oyundaki işi |
|---|---|---|
| **nodachi** | Devasa uzun kılıç | Ağır, yavaş, yüksek hasar. Yakalanması zor (kaldıraç ×0.75) |
| **naginata** | Ucunda kavisli bıçak olan mızrak | Menzilli yakın dövüş |
| **kanabō** | Dikenli/topuzlu demir sopa | Künt |
| **tetsubo** | Kanabō'nun ağır versiyonu, demir topuz | Künt sınıfının ağır ucu; sersemletmeyi taşıyan silah |
| **bō / jō** | Uzun sopa (~180 cm) / kısa sopa (~125 cm) | Öldürücü olmayan künt |
| **yari** | Düz uçlu mızrak | Delici (zırh delme 0.5 / 0.15) |

### Fırlatma

| Terim | Nedir | Oyundaki işi |
|---|---|---|
| **shuriken** | Atma yıldızı/bıçağı | 12 hasar, 4 cephane; zehirli hâli 12 hasar / 2 cephane |
| **kunai** | Atma bıçağı (kazma aleti kökenli) | Kısa menzilli mermi |
| **yumi** | Japon uzun yayı (asimetrik; alt kısmı kısa) | Karar turunda **iki elli menzilli sınıf** olarak girdi |
| **fukiya** | Üfleme borusu, zehirli iğne atar | Zehir taşıyıcı mermi |

---

## Zırh

| Terim | Nedir | Oyundaki işi |
|---|---|---|
| **keikogi** | Antrenman kıyafeti, kumaş — zırh sayılmaz | En hafif kuşam. Dayanıklılık 1, ~7 dövüş, 40 altın |
| **dō** | Gövdelik (göğüs zırhı) | Zırhın çekirdek parçası, gövde yuvası |
| **dō-maru** | Hafif, vücuda sarılan gövde zırhı (yaya asker zırhı) | Orta kademe: dayanıklılık 7 |
| **ō-yoroi** | "Büyük zırh" — atlı samurayın ağır kutu zırhı | En pahalı, en koruyucu: dayanıklılık 16, ~15 dövüş, 570 altın |
| **kabuto** | Miğfer | Baş yuvası (ō-yoroi kuşamıyla gelir) |
| **kote** | Zırhlı kolluk | Kol yuvası; ağır kote ō-yoroi kuşamında |
| **suneate** | Baldır zırhı, incik koruyucu | Bacak yuvası; ağır suneate ō-yoroi kuşamında |
| **ō-sode** | ō-yoroi'nin geniş omuz plakası | Karar turunda **kalkanın yerini alan parça** oldu: elde kalkan reddedildiği için blokun ekipman tarafını omuz yuvası taşır |
| **tate** | **Elde taşınan kalkan değil** — yere dayanan sabit tahta siper | Bu yüzden ekipman değil: karar turunda **saha özelliği** yapıldı |

---

## Mekanik terimleri

### Yakalama — "şans" ve "kilit"

Yakalama, kaçınmadan önce denenen **ikinci savunma eksenidir**. Düşman yakın dövüşte her
vurmaya kalktığında bir zar atılır.

**Şans** = ne sıklıkla tuttuğun.

```
şans = 0.24 × kavrayış × yakalanabilirlik × kaldıraç   (+ İsabet payı)
```

- **kavrayış** — savunanın aleti: jitte 1.0, sai 1.25, **diğer her şey 0**
- **yakalanabilirlik** — saldıranın silahı: kesici 1.0, delici 0.7, künt 0.25, yumruk 0
- **kaldıraç** — saldıranın silahı çift else ×0.75
- **İsabet payı** — İsabet 100'ken +0.5 (Kaçınma'ya değil, İsabet'e bağlanır)

Örnek: sai'li savaşçı, katanalı düşman → 0.24 × 1.25 × 1.0 = **%30**.
Aynı savaşçı, çift el nodachi'li düşman → ×0.75 ile **%22.5**.

**Kilit** = tuttuğunda ne kazandığın. İki şey birden olur:

1. Darbe **silinir** (hasar yok)
2. Saldıran **0.6 saniye** açıkta kalır — kilitli savaşçı **yürümez, vurmaz, kaçınamaz**

0.6 sn kısa görünür ama tipik vuruş süresi 0.85-1.05 sn: kilit, düşmanın sıradaki
vuruşunu fiilen yer. Kaçınma darbeyi ıskalatıp orada biter; yakalama darbeyi siler
**ve bedava zaman verir**. Bedeli **16 stamina** — sürekli yakalayan savaşçı yorulur.

Ölçümün okunuşu: jitte/sai zaferi katanadan fazla kazandırmaz; kazandırdığı şey
**eve sakat dönmemek** (düşman daha az vurduğu için uzuv kaybı düşer).

### Sersemletme

Ağır darbe **iki ayrı zar** attırır: uzuv kopma ve sersemletme. Hangisinin tuttuğunu
silahın sınıfı belirler — takas budur:

| Sınıf | Uzuv kopma çarpanı | Sersemletme çarpanı |
|---|---|---|
| Kesici (katana, nodachi) | 1.0 | 0.25 |
| Delici (yari) | 0.5 | 0.15 |
| Künt (tetsubo, kanabō) | 0.15 | **1.0** |

Sersemleyen savaşçı **0.9 saniye donar**: yürümez, vurmaz ve **kaçınamaz**. Asıl dişi
olan kaçınmanın kapanmasıdır. Zar yalnızca darbe azami canın **%20**'sini geçince atılır;
taban şans 0.35, kafaya inen darbede ×2.0. Çekilen savaşçı sersemlemez, sersemleyen
tekrar sersemlemez (süre yenilenmez).

Bu kural künt sınıfın **karşılığıdır**: künt silah uzuv koparmada kesiciye kaybeder
(0.15'e karşı 1.0), kazandığı şey dondurmaktır.

### Silah düşürme

Silah **kırılmaz, düşer** — arenada bir noktada durur, dövüş bitince sahibine döner.
Zar **saldıranın** silahına atılır: plakaya saplanan ağız burkulur, silah vuranın
elinden çıkar.

| Tetikleyici | Taban şans |
|---|---|
| Zırha inen vuruş | 0.05 |
| Yakalanan silahın çengelde sökülmesi | 0.05 |

| Sınıf | Elden çıkma eğilimi | Gerekçe |
|---|---|---|
| Kesici | 1.0 | Plakaya saplanan ağız burkulur |
| Delici | 0.6 | Uç kayar, sap avuçta kalır |
| Künt | 0.2 | Geri tepen sopa avuçtan çıkmaz |
| Yumruk | 0 | Düşecek bir şey yok |

Silah **karşıdakinin arkasına 250 birim** savrulur — ölçümde bedeli taşıyan şey mesafe
değil **yön** oldu: kendi arkasına ya da yana düşerse kural bedava, hatta faydalı çıkıyor.
Eli boş olan **herkes** alabilir (düşüren, takım arkadaşı, düşman); elinde silah olan
ne alır ne arar, kullanamayacağı silahı da almaz. Silahsız savaşçı yumrukla dövüşür
(8 hasar, menzil 100). Mermi kimsenin silahını düşürmez.

Kuralın bedeli dövüşün şekline göre değişir: düşen silahların 1v1'de %7.3'ü,
3v3'te %40.4'ü yerden alınıyor.

> **Üçüncü tetikleyici (karar 2026-09-07, henüz ölçülmedi):** sersemletme de silah
> düşürür. Sersemleme tuttuğunda ayrı bir zar atılır ve silahı düşen ilk kez **savunan**
> taraf olur; şansı sersemleyenin kendi silah sınıfı belirler (yukarıdaki eğilim tablosu
> ikinci yönde de çalışır — künt tutan zor düşürür). Taban şans künt sınıfı baskın
> yapmayacak yerde aranacak.

### Statlar

Sekiz sayı (`WarriorStats`), artı ayrı duran Onur. Acemi taban değerleri parantezde.

| Stat | Kod adı | Ne belirler |
|---|---|---|
| **Can** | `MaxHealth` (100) | Azami can. Uzuv kopma ve sersemletme eşiği de buradan okunur: zar, darbe azami canın %20'sini geçince atılır — canı yüksek savaşçı sakatlanmaya da dirençlidir |
| **Saldırganlık** | `Aggression` (40) | Gördüğü hücum fırsatlarının kaçını kullandığı. 0'da 0.35, 100'de 1.00 olasılık |
| **Savunma** | `Defense` (35) | Alınan hasarı azaltır **ve blok şansını verir** (`Savunma ÷ 100 × 0.45`; Savunma 0 olan hiç bloklamaz) |
| **Kaçınma** | `Evasion` (35) | Darbeyi ıskalatma denemesi; stamina harcar |
| **Güç** | `Strength` (40) | Vuruşun hasarı |
| **İsabet** | `Accuracy` (55) | Vurma şansı **ve yakalama** (yakalama İsabet'e bağlanır, Kaçınma'ya değil — iki savunma ekseni aynı stattan beslenmesin diye) |
| **Stamina** | `MaxStamina` (100) | Koşma, kaçınma, saldırı ve yakalama (16) tüketir; azaldıkça hasar ve isabet düşer |
| **Hız** | `Speed` (50) | Yürüme/koşma hızı. Geç eklendi: hız sabitken kovalayan ile kaçan aynı hızda gidiyor, **kaçış her zaman başarılı** oluyordu |

**Onur** (`Honor`, 0-100, başlangıç 50) bir dövüş statı değildir: ödül çarpanına,
seppuku eşiğine ve chat oylamasına bağlanır (bkz. `docs/GDD.md` §6).

Antrenman bu sekiz statı **dört talimle** kaplar: Vuruş (İsabet + Saldırganlık),
Siper (Savunma + Güç), Ayak (Kaçınma + Hız), Kondisyon (Can + Stamina) — ikinci stat
yarım pay alır.

### Uzuv kaybı

Uzuv kaybı riski, tek darbenin **azami cana oranı** eşiği (%20) geçince doğar; düşük can
ön koşul **değildir**, ilk darbede de olabilir. Kesici silah koparır (çarpan 1.0),
künt sersemletir (0.15). Bloklanan darbe uzuv **koparmaz**.

Savaşçı **ölmez, sakat kalır** — kalıcı cezalarla dövüşmeye devam eder:

| Kayıp | Etki |
|---|---|
| **Kılıç kolu** | Saldırı gücü ×0.65, iki elli silah kullanamaz, tek elli animasyona geçer |
| **Boştaki kol** | Saldırı gücü ×0.85, iki elli silah yine kullanamaz |
| **Bacak** (her biri) | Kaçınma ×0.55, yürüme hızı ×0.60 |
| **Göz** | İsabet ×0.75 |

Kayıplar birleşir (iki bacak → hız ×0.36). İki kolun ayrılmasının sebebi: kılıç kolu
vuruşun kendisi, boştaki kol dengedir — ikisi de çift el silahı bitirir ama tek elli
dövüşen için boştaki kolun kaybı taşınabilir. Sonuç, oyuncuya bırakılan bir karardır:
**emekliye ayır mı, kullanmaya devam mı.** Kazanılan dövüşlerin %16.5'i eve sakat bir
savaşçı getiriyor.

### Zehir — "doz" ne demek

Zehirli silahın indirdiği **her isabet** savunana bir **doz** bırakır; zar atılmaz,
namlu deriyi çizdiyse zehir girmiştir. Doz saniyede bir can yer ve bu hasar **ne zırhtan
ne Savunma statından geçer** — zehrin bütün değeri budur: plakayı delmez, etrafından dolanır.

| Sayı | Değer | Ne demek |
|---|---|---|
| Tik başına hasar | 2.5 | Doz 1 iken saniyede yenen can |
| Tik aralığı | 1.0 sn | Ne sıklıkla can yediği |
| Dozun ömrü | 6.0 sn | Vurulmazsa zehrin geçme süresi (her yeni vuruşta baştan kurulur) |
| Azami doz | 3.0 | Üst üste zehirlemenin tavanı — **asıl düğme budur**, ömrü değil |

Zehir uzuv koparmaz ve sersemletmez (ikisi de *darbenin* sonucudur; zehirde vuran kimse
yoktur — takasın yarısı budur). Zehrin öldürmesi ayrı bir sebeptir (`DeathCause.Poison`).
Çekilen savaşçının zehri **durmaz**: tuş bir panzehir değildir.

### Blok

Blok çekirdekte ayrı bir durumdur (`CombatState.Blocking`) — Savunma statının içinde
erimiş bir sayı değil.

| Kalem | Kural |
|---|---|
| **Şans** | `Savunma ÷ 100 × 0.45`. Taban yok: Savunma 0 olan hiç bloklamaz |
| **Şart** | Yakınlık değil **okunan hamle** — menzildeki düşmanın kılıcı toplanmış olmalı. (İlk hâlinde şart yalnızca "menzilde düşman var mı"ydı ve kural zaferi *düşürüyordu*) |
| **Süre** | 0.8 sn, bu sürede savaşçı **vurmaz**. Kaçınma bir darbeyi siler, blok bir **süre** satın alır — pahalı olan budur |
| **Tuttuğu** | Hasarın %70'i × silahın blok kalitesi |
| **Ritim** | Blok arkasına blok gelmez |
| **Uzuv** | Bloklanan darbe uzuv koparmaz |
| **Sarsıntı** | Künt silahın payı duruşa rağmen %75 işler — künt sınıfın dördüncü kazancı |
| **Yan/arka** | Kuşatılan bloklayamaz |

**Blok kalitesi** (elde ne varsa): çift el 1.0, künt 0.85, kesici 0.80, delici 0.70,
**yumruk 0.30** — silahını düşüren savaşçı bloğunu da kaybeder. Jitte/sai'ye çift el
kalitesi verilmesi denendi ve kilitli bir freni kırdı (ağır silahlı düşmanın önünde
jitte yanlış seçim olmaktan çıkıyordu); ikisi de tek elli künt kalitesini taşır.

Karar turunda blok **ikiye ayrıldı**: ne sıklıkla bloklandığı stattan, ne kadar kestiği
ekipmandan (omuz parçası **ō-sode**) okunur. El kalkanı yoktur.

### Diğer

| Terim | Nedir | Oyundaki işi |
|---|---|---|
| **omamori** | Tapınaktan alınan bez muska/tılsım | Referans oyunun "Jupiter kartları"nın karşılığı: savaşçıya **veya personele** takılır, sökülüp devredilir, satılır; arzını tapınak ilişkisi belirler |
| **seppuku** | Onurlu intihar | Onuru eşiğin (30) altına düşen savaşçı chat oylamasına gider; ronin çoğunluk → kalıcı ölüm, bushi çoğunluk → af (onur 45) |
| **bushi** | Savaşçı/samuray | Chat komutu: onur (+) |
| **rōnin** | Efendisiz samuray | Chat komutu: onur (−) |
| **dojo** | Talim yeri | Oyuncunun üssü; tesis ağacı ve personel burada |
| **sensei** | Öğretmen, eğitmen | Antrenman tarafının personeli |
| **yōkai** | Japon folklorunun doğaüstü yaratıkları | Düşman havuzu (bestiary kararı hâlâ açık) |
| **oni** | İblis, boynuzlu dev | Ağır düşman arketipi (tetsubo taşır) |
| **tengu** | Kanatlı dağ iblisi | Hızlı/menzilli düşman arketipi (zehirli shuriken atar) |
