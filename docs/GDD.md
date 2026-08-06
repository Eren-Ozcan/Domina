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
| Görsel teknik | **Saf 2D, cutout/iskelet animasyon** (Godot Skeleton2D + Bone2D) |

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

### Dövüş çözümlemesi (tam otomatik, manuel nişan yok)
Her vuruşma anı sırayla çözülür:
1. Saldıranın **Saldırganlık**'ı saldırı sıklığını belirler
2. Savunanın **Kaçınma**'sı için zar → kaçındıysa **stamina harcanır, hasar yok**
3. Kaçınamadıysa **Savunma** hasarı azaltır
4. Kalan hasar HP'den düşer
5. Vuruş yönü/açısı görsel çeşitlilik içindir, **mekanik sonucu etkilemez**

### Sınıf yok
Temel sürümde **tek karakter sınıfı: samuray**. Büyücü vb. varyasyonlar sonraya
(DLC/genişleme) bırakıldı.

---

## 5. Pes Etme ("Kaç") — Savaştaki Tek Müdahale

- **Tek tuş**, anlık karar. Mash/QTE **yok**. Otomatik eşik **yok** —
  oyuncu basmazsa savaşçı gerçekten ölür.
- Komut **tüm ekibi** kapsar: bir kez "kaç" denince sahadaki 1-3 savaşçının
  hepsi çekilir ve hepsi onur kaybeder. Tek bir savaşçı ayrıca çekilemez.

- Komut **seferi de bitirir**: o odadan sonrası iptal olur, ekip dojo'ya döner ve
  **o sefer ödülü alınmaz** (bkz. §10).

> **Neden ekip bazlı:** savaşçı bazlı olsaydı doğru oynanış "yara alanı hemen çek,
> kalanla devam et" olurdu — kayıpsız, sürekli tekrarlanan küçük bir optimizasyon.
> Ekip bazlı komut kararı **nadir ve ağır** yapar: bir savaşçıyı kurtarmak, seferi
> ve tüm ekibin onurunu bırakmak demektir. Uzuv kaybı mekaniğinin bedeli de böylece
> gerçek bir bedele bağlanır.

### Animasyon kesme (cancel window)
| Durum | Davranış |
|---|---|
| Idle, yaklaşma, saldırı sonrası toparlanma, blok | Komut **anında** işlenir |
| Saldırı vuruşuna kilitli an | Komut **buffer'lanır**, mevcut hareket bitince kaçış başlar |
| Blok duruşu | Neredeyse anında kaçışa geçer |

### Kaçış penceresi (savunmasızlık)
Kaçış başladığı andan arenadan çıkana kadar:
- Savaşçı **Kaçınma ve blok kullanamaz**
- Rakip **artırılmış isabet şansıyla** vurabilir
- Rakip bir **fırsat saldırısı** (opportunity attack) hakkı kazanır

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
- Zırh/savunma seviyesi riski **azaltır** (ekipmana yatırımı anlamlı kılar)

### Sonuç ağacı
| Durum | Sonuç |
|---|---|
| Hafif/orta darbe | Sadece HP hasarı, kalıcı etki yok |
| Ağır darbe + oyuncu zamanında pes ettiyse | **Yaşar ama uzvunu kaybeder** — kalıcı stat cezası + görsel değişiklik |
| Ağır darbe + müdahale yoksa/geç kalındıysa | **Ölüm** — gore/bitiriş animasyonu (Domina'nın yaklaşımı) |

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
- Sefere **1, 2 veya 3 savaşçı** gönderilir
- Sefer = art arda **fazlar** (oda-oda ilerleme), sonunda boss/büyük ödül
- **Pes etme seferi bitirir:** "kaç" denen odadan sonrası iptal, ekip dojo'ya döner,
  o seferin ödülü alınmaz (§5). Kaçmak savaşçıyı kurtarır ama seferi harcar —
  kararın ağırlığı buradan gelir.
- Üste kalan savaşçılar güvende; **sadece sefere giden ekip** permadeath riskinde
- Permadeath: ölen savaşçı kalıcı olarak gider

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

## Açık Kararlar

| # | Konu | Not |
|---|---|---|
| 1 | Parti boyutu seçimi | Tamamen oyuncu kararı mı, yoksa bazı encounter'lar zorunlu sayı mı dayatıyor? |
| 2 | Sefer/harita yapısı | Kaç faz, düz mü dallanan (node-map) mı, boss ritmi |
| 3 | Yokai bestiary detayı | Hangi yokai'ler, her birinin özel dövüş davranışı |
| 4 | Ekipman sistemi | Silah/zırh tipleri, tek-kollu savaşçıya özel ekipman kuralları |
| 5 | Ekonomi sayıları | Kaynak türleri, fiyatlar, gün döngüsü uzunluğu |
| 6 | Görsel **stil** | Teknik karar verildi (2D cutout/skeletal — §2). Açık kalan: **stil** — piksel mi, sumi-e/ukiyo-e mürekkep mi, başka mı. Sanat yönü ilhamı: Trek to Yomi, Okami |
| 7 | Oyun adı | Henüz yok ("Domina" sadece klasör adı — final isim değil) |
| 8 | Onur eşik sayıları | Seppuku eşiği, decay hızı, hedefli komut etki katsayısı — playtest ile |
| 9 | Kaçışta kısmi ödül | Sefer iptal olduğunda **önceki odalarda** toplanan ganimet elde kalıyor mu, o da mı gidiyor? "Hepsi gider" kaçmayı çok cezalandırıp asla kullanılmaz yapabilir; "hepsi kalır" ise geç kaçmayı bedava yapar (§5, §10) |
