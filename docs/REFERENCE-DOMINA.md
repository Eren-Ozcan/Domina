# Referans oyun: Domina (Dolphin Barn, 2017)

Bu dosya **bizim oyunumuzu değil**, esinlendiğimiz oyunu anlatır: *Domina* — Dolphin Barn
Incorporated'ın Roma gladyatör okulu yönetim oyunu (Steam, 3 Nisan 2017; 2021'de
mağazadan kaldırıldı). Amaç oynanışını kalem kalem çıkarmak: hangi sistem hangi kararı
üretiyor, hangi sayı neye bağlı, oyuncu bir günü neyle dolduruyor.

> **Neden var:** GDD §1'deki "Domina'dan ne alıyoruz / neyi kasten değiştiriyoruz" tablosu
> dört satır. Dört satır bir referans değil. Bir mekaniği ödünç alırken onun **yanındaki**
> mekaniği bilmemek, alınan parçanın neden çalıştığını da bilmemek demektir.

## Kaynak ve güven seviyesi

Her madde bir işaret taşır:

| İşaret | Anlamı |
|---|---|
| **[K]** | Birden fazla kaynakta aynı şekilde geçiyor; güvenilir. |
| **[T]** | Tek kaynak, çoğu zaman bir strateji rehberi — yani oyuncunun **stratejisi**, oyunun kuralı olmayabilir. |
| **[?]** | Kaynaklar çelişiyor ya da sayı yok; oynayarak doğrulanacak. |
| **[V]** | Oynanış videosundan; ekranda görülen ya da oynayanın anlattığı. |

Kaynaklar (hepsi 2026-09-05'te okundu):

1. `steamcommunity.com/sharedfiles/filedetails/?id=1587630000` — "Beating Pro-Gamer
   Difficulty" (2018 sonrası sürüm; en ayrıntılı sistem anlatımı).
2. `?id=1123035492` — "Complete guide to Domina".
3. `?id=970561085` — "10 tips for new players".
4. `?id=1948681614` — "Ways to Win the Game" (MEAT stratejisi).
5. `?id=2336870776` — "Brief Guide on Winning the Game (inc DLC Beta)".
6. `?id=905549957` — "How to be a Pro Gamer".
7. `?id=2507877258` — "Get Those Last 6 Achievements" (dosya/kayıt yapısı ve içerik listesi).
8. `gameplay.tips/guides/1006-domina.html` ve `/6176-domina.html` (yukarıdakilerin
   derlemeleri).
9. Wikipedia — *Domina (video game)*; Steam tartışma başlıkları (stat açıklamaları,
   Twitch).
10. **Video:** "Domina Beginners Guide To Starting Right PLUS Tips & Tricks (2018
    Edition)" — 15:48, 1280×720. Transkript ve ekran kareleri okundu. **[V]** işaretli
    maddeler buradan. 2018 sürümü olduğu için bazı yerlerde daha yeni rehberlerle
    (kaynak 1) çelişiyor; çelişkiler ayrıca yazıldı.

**Önemli uyarı:** Kaynakların çoğu **strateji rehberi**. Rehber "şunu yap" der, "kural
budur" demez; ayrıca oyun 2017-2021 arası çok değişti (rehberlerin kendisi "eski
rehberler artık geçersiz" diyor — özellikle Faber ve EXP nerf'leri). Bu yüzden sayıların
büyük kısmı **[T]**. Kesinleşmesi oynayarak olur.

---

## 1. Çerçeve

- Oyuncu, babasından kalan **ludus**'u (gladyatör okulu) devralan bir kadındır (*domina* =
  hanım). Hedef okulun itibarını geri kazanmak. **[K]**
- Oyun **bir yıllık geri sayım**la işler ve bu **ekranda yazıyor**: sağ üstte
  `Days Left`. Videonun ilk karesinde **364** — yani yıl **365 gün**. **[V]**
- Sağ üstte ikinci bir sayaç var: `Next Battle: n` — **planlanmış dövüşe kaç gün kaldığı**.
  Videoda 3'ten geri sayıp dövüşten sonra 7'ye dönüyor. Yani oyun iki ayrı takvim
  taşıyor: yılın sonu ve **bir sonraki mecburi dövüş**. **[V]**
- Rehberler günleri geri sayarak konuşuyor: "300 gün kala orta oyun", "50-60 gün kala geç
  oyun", "6-10 gün kala Haruspex". **[T]**
- Zaman **gerçek zamanlı akar ve durdurulabilir**; rehberlerin hepsi "oyuna girer girmez
  duraklat" diye başlıyor. **[K]**
- Zorluk kademeleri var; en üstü **Pro-Gamer**. **[K]**
- Kayıp koşulu tek bir "game over" değil: en iyi gladyatörünü kaybetmek pratikte oyunu
  bitirir ("basically it is game over"), çünkü final için hazır kadro kalmaz. **[T]**

## 2. Gün ve ekran akışı

- Tek ekranda ludus görünür: avlu, eğitim aletleri, personel, kapı. Gladyatörler
  **sürüklenip** aletlerin yanına taşınır; alet de taşınabilir. **[K]**
- `TAB` HUD'ı açar: kim çalışıyor, kim boşta. **[T]**
- Dövüşler haritadan/masadan seçilir; pazar ekranı masadaki bir nesneden açılır. **[T]**
- Jupiter kartları ayrı bir masada durur, kart **sürüklenerek** bir gladyatöre ya da
  personele takılır. **[K]**

## 3. Kaynaklar ve ekonomi

**Üst çubuk (HUD)** dört kaynağı sürekli gösteriyor: `Coin`, `Water`, `Food`, `Wine`;
sağda `Next Battle` ve `Days Left`. Oyun akarken sağ üstte duraklat simgesi, duraklatınca
ekranın ortasında **PAUSED** yazıyor. **[V]**

**Videodaki başlangıç durumu (2018 sürümü):** `Coin 1000 · Water 400 · Food 800 ·
Wine 80 · Next Battle 3 · Days Left 364`. **[V]**

> Not: Bu, bizim 600 altınlık başlangıcımızla kıyaslanabilir tek sayı değil — Domina'da
> yiyecek ve su **stok olarak** başlıyor (800/400), bizde ambar boş. Domina'nın ilk günü
> "ne alacağım" değil, "neyi harcamayacağım" sorusuyla açılıyor.

| Kaynak | Ne işe yarar | Not |
|---|---|---|
| **Coin (altın)** | Köle, personel, araştırma, ekipman, iyileştirme, bahis | Tek para birimi **[K]** |
| **Food / Water** | Kadronun günlük tüketimi | Depo yükseltmeleri tüketimi/masrafı düşürür **[K]** |
| **Wine (şarap)** | Legate ve Magistrate'e rüşvet; gladyatör morali | Pazarda **günde 1 yenilenir, stok tavanı 2** **[T]** |
| **Stone (taş)** | Architect'in eğitim aleti inşası | Architect sürekli taş toplar **[K]** |

- Pazar **2 günde bir** yenilenir. **[T]** (Bir başka rehber şarabın **her gün** birer
  birer geldiğini söylüyor — çelişki **[?]**.)
- Rehberlerin ortak tavsiyesi: yiyecek/su stokunu ~1000 civarında tut. **[T]**
- Gelir kalemleri: planlanmış dövüş ödülleri, **pit fight** ödülü + bahis, **exhibition**
  ödülü (100-200 coin/dövüş **[T]**), bölge şampiyonu ödülleri, **kalabalık favorisi**
  (aşağıda), fazla köle/at/araba satışı.
- Gider kalemleri: köle alımı, personel maaşı/alım bedeli, araştırma, ekipman ve onarım,
  iyileştirme, yiyecek/su, şarap.
- **Personel de kaynak yiyor:** her personelin günlük **yiyecek/su tüketimi** var (aşağıdaki
  tabloya bak) — yani personel almak sadece altın değil, **ambar** kararı. Sacerdos'un
  tüketimi **hiç yok**; bu onu videonun gözünde ayrıca değerli yapıyor. **[V]**
- **Kıtlık olayları var:** kuraklık ve sel yiyecek/su arzını vuruyor; bu yüzden kendi
  üretimini kuran Architect kritik sayılıyor. **[V]**
- **Yiyecek satılabiliyor:** fazlası pazarda paraya çevriliyor, **7 yiyecek ≈ 1 altın**
  (video, 2018). Agricola günde ~20 yiyecek üretince bu günde ~3 altın demek — yani
  üretim fazlası bir gelir kalemi ama küçük. **[V]**
- **Ekipman fiyat eğrisi kademeli:** aynı parçayı defalarca yükseltirsin, fiyat normal
  seyrederken **bir yükseltme aniden pahalı** gelir, sonrasında yine ucuzlar. Rehberin
  sömürüsü: pahalı basamağı **Faber'e bedavaya yaptır**, sonra ucuz basamaklardan devam
  et. **[T]**

## 4. Gladyatör: statlar

Ekranda görünen değerler (Steam tartışmasından derlendi) **[K]**:

- **HP / Vitality** — can havuzu. Başlangıç kölelerinde ~120-160 tipik; sonda 300-500,
  sadece güç antrenmanına yatırılırsa 600+. **[T]**
- **Strength** — hasar tabanı.
- **Weapon skill** — silah yeterliliği.
- **Agility** — çeviklik.
- **Defense** — savunma.
- **Meditate / AI skill** — **oyuncu müdahale etmediğinde** gladyatörün kendi kendini ne
  kadar iyi yönettiği. Otomatik oynanış bunun üstünde durur. **[K]**
- **Morale / temperament** — moral; **statları etkiler**. **[K]**
- **Stamina** — salınan her vuruş yer; ağır iki elli silahlar 2-3 vuruşta nefesi bitirir
  ve hasar düşer. **[K]**
- **Weight / Final weight** — zırhın ağırlığı; ağır zırh yavaşlatır ve stamina yakar. **[K]**
- **Aggressive / Defensive tendency (aggro / turtle) ve Evasion** — **davranış eğilimleri**,
  yetenek değil:
  - yüksek **aggro** → düşmana yürür, saldırıyı başlatır ve sürdürür;
  - yüksek **turtle** → bekler, önce rakibin saldırmasını ister, bloklar;
  - yüksek **evasion** → yuvarlanarak kaçar, açı arar. **[K]**

### Gladyatör paneli — ekrandaki tam alan listesi **[V]**

Videoda iki gladyatör açıldı; panel şunları gösteriyor:

- **Ad ve memleket:** "Vettius of Melitensium", "Granius of Helvetia"
- **Sınıf:** THRAEX / MURMILLO (portrenin üstünde)
- **Weight: 91kg · Total: 124kg** — vücut ağırlığı ve **kuşamla birlikte** toplam
- **Temperament:** kaydırma çubuğu, etiketli ("Satisfied", "Neutral")
- **Health: 145/145** (yeşil bar) + **Heal** tuşu (canı tamken kapalı)
- **Training Balance** tablosu — sütunlar **Level** ve **Points**:
  | Satır | Örnek (Vettius) | Örnek (Granius) |
  |---|---|---|
  | Agility | 13 / 62 | 2 / 60 |
  | Weapon | 14 / 34 | 4 / 26 |
  | Defense | 13 / 49 | 5 / 36 |
  | Strength | 10 / **145HP MAX** | 5 / **158HP MAX** |
  | Meditate | **100** | 25 |
- **Aggro: 79 · Turtle: 21 · Evasive: 56 · Stamina: 50** (Granius: 69 / 34 / 63 / 50)
- **Victories: 3 · Losses: 1**
- Tuşlar: **Reward Wine [n]**, **Reward Coin [n]**, **Award Private Room** (kapalı),
  **Put to Death**, **Grant Freedom**, **Sell**, ileri/geri okları, **Close**

> Üç şey dikkat çekiyor. Birincisi **Level ve Points ayrı** — yani eğitim iki katmanlı:
> biriken puan ve ondan çıkan seviye. İkincisi **Strength satırının sağında puan değil
> "145HP MAX" yazıyor**: güç doğrudan can tavanı. Üçüncüsü **Aggro/Turtle/Evasive
> panelde sayı olarak duruyor** (79/21/56) — davranış eğilimi oyuncuya **açıkça** gösterilen
> bir sayı, gizli bir kişilik değil.

Oyunun kendi ipucu metni (Strength satırı): *"Increase Hitpoints, Attack Damage, and
Defense Resiliance."* **[V]**

**Videodaki tanımlar (2018)** — rehberlerdekinden daha net **[V]**:

| Stat | Ne yapıyor |
|---|---|
| **Strength** | Tek başına üç şey: **can**, **vuruş hasarı** ve **hasara direnç**. Bu yüzden "sadece güç bas" stratejisi çalışıyor. |
| **Agility** | **Hareket hızı**. |
| **Weapon** | **Vuruş hasarı**. |
| **Defense** | Saldırıya **direnme** yeteneği. |
| **Meditation** | Videonun tavsiyesi: **hiç eğitme** — dövüşlerden zaten kendiliğinden geliyor. |

> Not: Strength'in üç işi birden yapması, bizim ayrı ayrı tuttuğumuz `MaxHealth`,
> `Strength` ve `Defense` üçlüsünün Domina'da **tek slider**'a bağlı olduğu anlamına
> geliyor. Eğitim kararını sığlaştıran şey de bu: baskın strateji "hep güç".

> Bizim §4'teki "davranış farkı ayrı kod değil, hedef seçimi ağırlıkları" kararının
> referanstaki karşılığı tam olarak budur: Domina'da sınıf farkı **ayrı bir dövüş sistemi
> değil**, aynı sistemde farklı eğilim sayılarıdır.

## 5. Eğitim

- Her gladyatörde stat başına **slider** var; oyuncu eğitim vaktini paylaştırır. **[K]**
- **Auto-train** kutusu Doctore'da açılır; açıkken herkes durmadan çalışır. **[K]**
- Yaygın açılış: önce **Meditate 100'e** (AI iyi olsun diye), sonra tamamen silaha. **[T]**
  Bir rehber tam tersini söylüyor: "sliderlara dokunma, meditate'i maks etme, dengeyi
  bozuyorsun; onun yerine bol bol exhibition koş" **[?]**.
- **Dövüşmek antrenmandan hızlı öğretiyor.** Tekrarlanan iddia: AI becerisi 1 dövüşte,
  10 günlük antrenman kadar artıyor. Doctore Emeritus'un *Master Mimic* becerisiyle bir
  dövüşte **+100 AI**, **+10-30 silah/güç** görülebiliyor. **[T]**
- Eğitim aletleri (Architect kurar): **palus** (talim direği), **coal pit**, **stones**,
  **bath** (iyileşme hızı), apothecary. Rehber "15 palus" hedefi veriyor. **[T]**
- Stat **tavanları** var ve Doctore/Doctore Emeritus araştırmalarıyla yükseliyor; eğitim
  süresi **%75'e kadar** kısalabiliyor. **[T]**
- **Sınıf eğitimi yalnızca sınıfsız kölelere yapılabiliyor**; sınıf seçildikten sonra
  değiştirilemiyor. Video sınıf araştırmalarını (Murmillo/Retiarius açılışı) "pahalı ve
  çok uzun, alma" diye eliyor: eğitilmiş sınıflı gladyatörler zaten **dövüş ödülü** ve
  **Legate'ten satın alma** yoluyla geliyor. **[V]**
- Video, sıfırdan köle eğitmenin **çok uzun** sürdüğünü söylüyor — hazır gladyatör almak
  neredeyse her zaman daha hızlı. **[V]**

## 6. Sınıflar

Sınıf **sonradan atanır** (köle → gladyatör) ve ekipman şablonunu belirler. **[K]**

| Sınıf | Karakter | Eğilim profili |
|---|---|---|
| **Murmillo** | Kılıç + kalkan, hücumcu | yüksek aggro / düşük turtle / orta evasion **[K]** |
| **Thraex** | Savunmacı | orta aggro / yüksek turtle / düşük evasion **[K]** |
| **Retiarius** | Ağ + mızrak, mesafeli, ağ ile debuff | düşük aggro / orta turtle / yüksek evasion **[K]** |
| **Scissor** | Çift el, hücumcu | **[T]** |
| **Velite** | Uzun menzilli yakın dövüş | **[T]** |
| **Sagittarius** | Okçu; çok-kişilikte destek, teke tekte zayıf | **[T]** |
| **Charioteer** | Araba yarışı için | **[T]** |
| **Behemoth** | Dev/canavar; ayrı bir düşman tipi ve başarım hedefi | **[T]** |

- Sınıf atamanın bir maliyeti var: rehber "**dövüşmeyecek köleye sınıf verme**, yoksa
  ajan/Faber onun ekipmanını yükseltmeye başlar ve asıl adamının parasını yer" diyor. **[T]**

### Sınıf seçimi ayrı bir ekran **[V]**

Gladyatör panelinden **"SELECT GLADIATOR CLASS"** ekranı açılıyor: üç portre düğmesi —
`Murmillo`, `Thraex`, `Retiarius`. Oyunun **ikon öncelikli** tek ekranı; geri kalan her
yer metin düğmesi.

### Harita: "Map of Games" **[V]**

İtalya haritası; bölgelerin çoğunda **asma kilit**, birinde **yeşil tik**. Yanında hedefi
düz metinle yazan bir kutu:

> "You need to defeat at least **3 Regional Champions** to be considered for the Final
> Championship in Rome. **1 / 3** have been defeated."

Yani finale girmek bir **ön koşul**: en az 3 bölge şampiyonu. Rehberlerin "Big 3'ü erken
bitir" demesinin sebebi strateji değil, **kapı**.

### Patron olayı — birebir metin **[V]**

> "Magistrate Atilius Antonius has agreed to become a patron of your ludus! He has adopted
> **Tullus of Lechia**. The Magistrate will be responsible for this gladiator's **food and
> water** until the day that he dies on the field of battle."

Tuşsuz, "Press any key" ile kapanan bir bildirim. Patronaj tam olarak şu: **bir
gladyatörün yiyecek ve suyunu NPC ödüyor, ölene kadar**.

## 7. Personel (employees)

Personel **slot**larda durur; slot sayısı sınırlı ve parayla (1500 altın) genişletilebilir
**[T]**. Kritik kural: **birini kovarsan onun araştırdığı bonuslar da gider** — Architect'in
inşa ettiği binalar kalır, ama diğerlerinin pasif bonusları düşer. **[K]**

**Videodan alınan gerçek işe alım listesi (fiyat / günlük tüketim / vaat)** **[V]**:

| Personel | Fiyat | Günlük tüketim | Ekranda yazan |
|---|---|---|---|
| **Agent** | 13 | 1 yiyecek, 1 su | "Dirty work, free pit fights" |
| **Bard** | 13 | 1 yiyecek, 1 su | "Morale..." (kesik) |
| **Agricultor** | 25 | 1 su | "4 Food/day" |
| **Educator** | 30 | 1 yiyecek, 2 su | "Morale, AI Proficiency" |
| **Medicus** | 34 | 1 yiyecek, 1 su | "Gladiator Healing" |
| **Emptor** | 45 | 2 yiyecek, 1 su | "Reduced costs on upgrades and resources." |
| **Architect** | 65 | 2 yiyecek, 1 su | "Ludus upgrades" |
| **Haruspex** | 72 | 1 yiyecek, 1 su | "Sacrifices to the Gods" |
| **Faber** | 75 | 1 su, 1 yiyecek | "Inexpensive upgrades and equipment repairs." |
| **Sacerdos** | 100 | **hiçbir şey** | "Healing, Training, and Morale Boost" |
| **Vintner** | 100 | 1 su, 1 yiyecek | "Wine, Magistrate Favour" |

> Fiyatlar **çok ucuz** (13-100 altın, başlangıç kasası 1000). Yani personel kararı bir
> **para** kararı değil, **slot** kararı: aynı anda sınırlı sayıda personel tutulabiliyor
> ve kovulan personelin araştırmaları gidiyor. Bizim okul ağacının fiyatla sınırlanması
> (GDD §10) buradan **kasten** ayrılıyor.

| Personel | Ne yapar | Notlar |
|---|---|---|
| **Doctore** | Eğitimi yönetir; auto-train; beceri ağacı (Humility, Deep Breathing, Blade Control, Net/Polearm Defense, Attack Vector/Rolling Attack, Interpretive Dance, Automatic Yield, sınıf açılışları, Mind Control) | Oyunun **bedava** başlangıç personeli **[K]** |
| **Doctore Emeritus** | Pahalı üst sürüm: eğitim süresi **-%75**, dövüşten kazanılan EXP artışı, stat tavanı artışı, *Deeper Humility* (%20 canda otomatik teslim), *Master Mimic* (rakipten öğrenme), *Infinity Weapon* (tek vuruş birden çok düşmana), kritik şansı | Rehberlerin çoğunun **stratejik merkezi**; "300 gün kalaya kadar al" **[T]** |
| **Medicus** | Otomatik iyileştirme; *wash hands*, *antiseptics* | Yaralı sayısı yüksekken **[K]** |
| **Faber** | Ekipmanı **otomatik onarır ve yükseltir**; blueprint araştırmaları alım fiyatını düşürür | Otomatik yükseltme sıklığı nerf yedi **[T]** |
| **Faber Emeritus** | Üst sürüm; iki Faber birlikte çalıştırılabiliyor | **[T]** |
| **Architect** | Taş toplar, **palus/coal pit/bath/depo** inşa eder, avluyu ve personel alanını genişletir | Kadro kapasitesini **28'e** kadar çıkarır **[T]** |
| **Architect Emeritus** | Üst sürüm; eğitim alanı ve depo yükseltmeleri | **[T]** |
| **Agent (Sneaky)** | **Silah/zırh çalar**, **pit fight ayarlar**, bahis; yakalanırsa kaybedilir ve yeniden alınır | İtibarı "Dark Figure" olunca neredeyse hiç yakalanmıyor **[T]** |
| **Sacerdos (rahip)** | Dualar: *Prayer to Venus* (ludus'ta ve **dövüş ortasında** yenilenme), *Prayer to Mars*, Neptune; pasif stat artışı | Erken oyunda hayatta kalma **[T]** |
| **Bard** | Şarkılar: iyileştirme, silah, çeviklik, **moral** | Moral tavanı için ozan + hamam + Educator birlikte **[T]** |
| **Educator** | *Philosophy*, *Anatomy*, *Focus*, dövüş yeterliliği, moral | Kovulursa bonusları gider — sonuna kadar tutulur **[T]** |
| **Emptor** | Pazarlık: ekipman ve erzak indirimi (Faber indirimiyle **birikir**) | Son alışveriş turunda alınıp sonra kovuluyor **[T]** |
| **Haruspex** | Düşmana **lanet**: canını yarıya indirme, Jupiter kartlarını söktürme, dövüş yeterliliğini düşürme | Lanetler **bir sonraki dövüşte tükenir** — final öncesi 6-10 gün hiç dövüşülmez **[T]** |

### Videonun (2018) anlattığı ayrıntılar **[V]**

- **Aynı anda yalnızca 3 personel** tutulabiliyor. (Daha yeni bir rehber 6 slot ve
  1500 altınlık slot yükseltmesinden söz ediyor — sürüm farkı **[?]**.)
- **Architect** videoya göre açılışın en önemli personeli, çünkü **yiyecek + su + şarap
  üretimini tek başına** veren tek personel ve **kurduğu binalar o gittikten sonra da
  kalıyor**. Kuraklık/sel olayları arzı vurduğu için kendi üretimin hayat kurtarıyor.
  Kovulduktan sonra **geri alınamıyor** (videonun iddiası **[?]**), o yüzden her şeyi
  kurmadan kovma.
- **İnşa sırası (video):** önce **palus** (3 tur), sonra **stones**, sonra **coal pit**,
  sonra **bath**. Etkiler: palus **eğitim süresini kısaltır**, stones **gücü artırır**,
  coal pit **çevikliği** etkiler (transkript bulanık **[?]**), bath **morali artırır ve
  sakatlanmayı azaltır**.
- **Faber**: bedava onarım + bedava otomatik yükseltme. Asıl değer **blueprint**'lerde:
  **kask ve kalkan dışındaki her parça "armor" sayılıyor** (omuzluk, göğüslük, etek,
  bacaklık) — yani **tek bir armor blueprint dördünü birden ucuzlatıyor**. Ayrıca silah
  blueprint'i, daha hızlı onarım ve **ağ (net) yenileme**.
- **Sacerdos**: araştırmaları **1 tur** sürüyor (en hızlısı); **suyu şaraba çeviriyor**;
  savunma, güç ve çevikliği artırıyor; iyileştiriyor; ara sıra silah yükseltiyor.
- **Agricola** (videoda "agri-tower"): alır almaz **+4 yiyecek**, her araştırmayla
  **+4 yiyecek** daha — videoda **günde 20 yiyecek**'e çıkıyor.
- **Educator** araştırmaları: **Focus** (bütün eğitim sürelerini kısaltır, **4 tur**),
  **Psychology** (kaybetmenin/teslim olmanın **utancını** azaltır), **Anatomy** (saldırı
  hasarı ve **kritik şansı**), maksimum stamina ve **stamina yenilenme hızı**, daha hızlı
  iyileşme, **AI dövüş yeterliliği**.
- **Medicus** ve **Agent** videoya göre "ilginç ama zayıf": çoğu personel zaten taban
  iyileştirme veriyor.

### Doctore beceri ağacı — ekrandaki tam düğüm listesi **[V]**

Panelin adı **"Special Training Maneuvers"**; 3 sütunlu bir ızgara, düğümler arasında
bağlantı çizgileri var (ön koşul zinciri). Ekranda okunan bütün düğümler:

`Wolf Courage` · `Attack Shuffle` · `Berserk` · `Attack Vector` · `Throw Weapons` ·
`Weight Training` · `Critical Strike` · `Disarming Weapon` · `Mindfulness` ·
`Blade Control` · `Murmillo Training` · `Mind Control` · `Rolling Attack` ·
`Dismemberment` · `Automatic Yield` · `Disarming Shield` · `Wind Sprints` · `Low Stance` ·
`Defense Shuffle` · `Endurance Training` · `Evasive Roll` · `Grip Techniques` ·
`Interpretive Dance` · `Aimed Defense` · `Deep Breathing` · `Net Defense` · `Aimed Attack` ·
`Shield Control` · `Polearm Defense` · `Nimble Stance` · `Retiarius Training` ·
`Humility` (en altta, ortada).

Panelin altında: **Fire Employee**, **Enable Automatic Gladiator Training** kutusu ve
**Close**.

**Ekranda görülen fiyat/süre örnekleri** — her düğüm hem **altın** hem **tur** (kum saati)
istiyor, bazıları ayrıca yiyecek/su:

| Düğüm | Maliyet | İpucu metni |
|---|---|---|
| `Murmillo Training` | **400 altın · 16 tur** | "Unlock the Murmillo Class" |
| `Retiarius Training` | **500 altın · 17 tur** | (sınıf açar) |
| `Disarming Weapon` | **67 altın · 6 tur** | "Gladiator has higher chance of disarming opponent during a successful attack." |
| `Mind Control` | **31 altın · 6 tur · 10 yiyecek · 10 su** | "Allows you to directly control one gladiator on the field of battle." |

> Ölçek şu: sıradan bir beceri **67 altın**, sınıf açmak **400 altın ve 16 tur**. Rehberlerin
> "sınıf araştırması alma" demesinin sebebi bu — 400 altın, başlangıç kasasının %40'ı.
> **Mind control'ün 31 altın olması** ayrıca ilginç: oyunun en tartışmalı özelliği (elle
> oynama) neredeyse bedava, ama kalabalık bundan hoşlanmıyor — maliyet parada değil,
> **gelirde**.

### Diğer personelin araştırma listeleri (ekrandan) **[V]**

- **Faber → araştırmalar (ekrandan):** `Automatic Upgrade`, `Improved Furnace`,
  `Improved Anvil`, `Helmet Blueprints`, `Weapon Blueprints`, `Armor Blueprints`,
  `Shield Blueprints`, `Rebuild Nets`; altında **iki onay kutusu**: `Auto Repair`
  (işaretli) ve `Auto Upgrade`. İpucu: *"Automatically repair damaged equipment, free of
  cost."* **[V]**
- **Architect → "Building Tasks":** `Palus`, `Baths`, `Grain Shelter`, `Water Well`,
  `Wall Reinforcement`, `Private Gladiator Quarters`, `Dig Hot Coal Pit`, `Wine Cellar`,
  `Apothecary`, `Gather Stones`. Kömür çukurunun ipucu: *"Hot coals under a gladiator's
  feet will decrease agility training time."* — yani **çevikliğin eğitim süresini
  kısaltıyor**, çevikliği artırmıyor (§19'daki soru kapandı). Kuyunun ipucu daha da net:
  *"Building a well will make the ludus more resilient during droughts, and will produce
  water. (**+2 to 5 Water/day**)"* — **30 altın · 30 su · 15 taş**. Bina, kıtlık olayına
  karşı **sigorta** olarak açıkça pazarlanıyor. **[V]**
- **Sacerdos → dualar:** `Prayer to Neptune`, `Juno`, `Apollo`, `Mars`, `Mercury`, `Venus`,
  `Vulcan`. Mercury'nin maliyeti: **7 altın · 1 tur · 10 şarap · 30 yiyecek · 30 su**,
  ipucu *"Occasional upgrade to gladiator weapon."* — yani duaların bedeli altından çok
  **ambar**.
- **Bard → şarkılar:** `Song of Venus`, `Juno`, `Minerva`, `Vesta`, `Diana`. Diana:
  **11 altın · 6 tur · 10 şarap · 20 su**, *"boost gladiator weapon training speed"*.
- **Educator:** `Teachings of Galen`, `Teachings of Dioscorides`, `Anatomy`,
  `Psychology`, `Philosophy`, `Focus`.
  Dioscorides: *"cleanliness can help their body heal more quickly after injury"*.

### Videonun anlattığı sıralama **[V]**

Videonun sırası: **Humility → Aimed Attack → Blade Control → Aimed Defense → Evasive Roll
→ Shield Control**. Sonrası tercihe kalıyor; videonun devamı: **Disarm Weapon**
(silahsız kalan rakip pratikte ölü), **Weight Training** (ağır zırh/silah taşımak için),
**Attack Vector** (**saldırı hızı**), **Polearm Defense**, **Deep Breathing**,
**Grip Techniques** (silahının elinden alınmasına karşı), **Net Defense**.

İki önemli not:

- **Automatic Yield ile Berserk birbirini dışlıyor** — birini alırsan diğerini alamıyorsun.
  Yani "kaybederken teslim ol" ile "kaybederken çıldır" **aynı kararın iki ucu**. **[V]**
- Bir beceri (transkriptte adı net değil, ~17 tur) gladyatörün **özgürlük istemeden önce
  kaç zafer taşıyacağını** artırıyor. **[V]**
- **Mind control** ayrı bir araştırma; videocu hiç kullanmamış. **[V]**

## 8. Ekipman

- Yuvalar: **silah, kalkan, kask, göğüs, omuz (pauldron), bel/etek, bacak (greaves)**,
  ayrıca Retiarius için **ağ**. **[K]** Panelde yuvalar `PRIMARY` / `SECONDARY` diye
  etiketli; çift silah kullanılabiliyor (videoda iki tane "Wooden Gladius"). **[V]**
- **Her parçanın üç sayısı var:** saldırı, savunma, **kilogram** — ekranda
  `A:+9 D:+9 2kg` biçiminde. Altında yeşil bir bar: **dayanıklılık**. **[V]**
- **Yükseltme ekranda karşılaştırmalı:** parçanın üstüne gelince solda `DOWNGRADE
  <L-Click>`, sağda `UPGRADE <R-Click>` çıkıyor ve **her iki yönün de fiyatı** görünüyor.
  Videodaki örnek: *Improved Leather Chestplate (D:+9, 3kg)* → yükseltme *Centurion's Mail
  (D:+11, **22kg**)* **-23 altın**; düşürme *Standard Leather Chest Plate (D:+8, 4kg)*
  **+7 altın** (parça satılıyor). **[V]**

> Buradaki denge kolu bizde yok: **+2 savunma için 19 kilo**. Ağırlık stamina ve hız
> demek olduğu için "daha iyi zırh" düz bir iyileşme değil, açık bir takas. Bizim
> `ArmorPiece.Weight` alanı var ama karar ekranda bu kadar çıplak görünmüyor.
- Her parçanın **yükseltme basamakları** var; Faber otomatik yükseltir, oyuncu parayla
  atlar. Onarım ayrı bir iş. **[K]**
- Rehberlerin "iyi fiyat/performans" seti: Gladius, Elite Roman Centurion Shield, Death's
  Helmet, Centurion Mail, Centurion Leathers, Onyx Greaves. **[T]**
- Fiyat örnekleri **[T]**: çift gladius 80 altın; tipik Murmillo takımı 412 altın; 13
  kişilik temel kuşam 6.400 altın; 13 Zweihander 3.600 altın.
- **Ağırlık gerçek bir maliyet:** ağır zırh yavaşlatır, stamina yakar; rehberler "ağır
  zırhlı rakip 2-3 vuruşta nefessiz kalır" diye **düşmanın zırhını zayıflık** olarak
  kullanıyor. **[K]**
- Video, ekipman yatırımını **4-5 gladyatöre** dağıtmayı öneriyor: tek adama yatırmak,
  birden çok kişilik dövüşlerde kadro bulunamamasına yol açıyor; herkese dağıtmak ise
  parayı eritiyor. **[V]**
- Ters teşvik: iyi ekipman **EXP'yi düşürür** — dövüş çabuk bitince öğrenme az olur. Bu
  yüzden eğitimdeki gladyatörlere kasten zayıf kuşam veriliyor. **[T]**

## 9. Dövüş türleri

| Tür | Nasıl gelir | Ne verir | Risk |
|---|---|---|---|
| **Scheduled / forced fight** | Legate ve Magistrate ayarlar | Ödül + itibar | Ölüm **[K]** |
| **Pit fight** | Agent ayarlar | Ödül + **bahis** (rehber: her dövüşe 150 altın) | Rakibin gücü **görünmez** **[T]** |
| **Exhibition** | Legate/Magistrate'in gösteri maçı | EXP + coin; **teslim var, ölüm yok** | Kalıcı sakatlık (impediment) riski, *Deeper Humility* ile kapanır **[T]** |
| **Regional champions ("Big 3" + 9 dövüş)** | Haritadaki sabit rakipler | Coin + **mavi Jupiter kartı** | Sabit ve **her oyunda aynı** **[K]** |
| **Chariot race / Beast mode / Gravitas** | Özel etkinlikler (at, araba, aslan gerekir) | Coin, başarım | **[T]** |
| **Final championship** | Yıl sonunda | Oyunun sonu | **15 iyi kuşanmış gladyatör**, hepsi her statta **100+** **[K]** |

- **Bölge şampiyonları sabit:** aynı statlar, aynı dövüş, her oyunda. Oyuncu ezberleyip
  hazırlanabiliyor. **[K]**
- Finalden sonra bir dövüş daha var ve o **daha kolay**; özgür bırakılan gladyatörler
  buraya dönüyor. **[T]**

### Dövüş teklifi ekranı — "Arena Battle" **[V]**

Dövüş bir **sözleşme kartı** olarak geliyor; ekranda şunlar yazıyor:

| Alan | Videodaki örnekler |
|---|---|
| **Host** | "The Emperor" |
| **Game Type** | "Championship" / "1 vs 1" |
| **Victory Reward** | 213 altın · 75 yiyecek · 12 su · 2 şarap · **2 köle** — ya da 131 altın · 166 yiyecek · 155 su · **2 köle** |
| **Participation Cost** | "11 altın · 2 yiyecek · 1 su · **6 gün**" — ya da "None" |
| **Surrender Allowed** | **Yes / No** |
| **Obstacles** | "It's a mystery." / "Lions" |
| **Pick Your Gladiators** | "(Mind Control Not Researched)" uyarısı; her yuvada portre + `AI` etiketi; sayaç **Selected/MAX: 2/3** ya da **0/1** |
| **Opponent Gladiators** | Rakip portresi, adı ("Ancus the Animal", "Clodius", "Dirkus Digglerus"), `AI` etiketi |
| Tuşlar | **Pick Gladiators · Reject Terms · Accept Terms** (kimse seçilmeden Accept kapalı) |

> Dört tane doğrudan bizi ilgilendiren şey var:
> 1. **Katılım bedeli gün yiyor** ("6 days") — bizim "gün tek iş yer" kuralının referanstaki
>    karşılığı; orada gün **dövüşün fiyatı**, sabit bir kural değil.
> 2. **"Surrender Allowed: Yes/No" sözleşmenin bir alanı** — yani pes etme hakkı dövüş
>    başına değişiyor. Bizde çekilme her zaman açık; bu kapatılabilir bir kol olabilirdi.
> 3. **Ödül sadece altın değil:** yiyecek, su, şarap ve **köle**. Ödül kadroyu doğrudan
>    büyütüyor.
> 4. **Engeller (Obstacles) sözleşmede yazıyor** ve "It's a mystery" bile bir seçenek —
>    bilinmezlik açıkça satılıyor.

### Dövüş ekranı **[V]**

- Üstten geniş açı arena zemini; ekranın alt üçte biri **kalabalık**.
- Sağ üstte oyuncunun savaşçıları: portre + can barı, **sayı olarak** `141/141`, `24/145`.
- Altında **geri sayan süre**: `2:49 → 2:29 → 2:09`. Yani dövüşün **süre sınırı** var.
- Sağ altta rakip: `Ancus the Animal 470/550 → 325/550 → 106/550`.
- Vuruşlarda **kırmızı hasar sayıları** havalanıyor (`-13`, `-10`) + kan efekti.
- **Hiçbir yetenek tuşu, bekleme süresi ya da komut çubuğu yok** — mind control
  araştırılmadıysa dövüş tamamen izleniyor.

### Zafer ekranı **[V]**

Büyük **VICTORY** başlığı, altında iki kart — her savaşçı için **"AI Training MAX"** ve
dövüşten kazanılan eğitim:

- 1. savaşçı: `Agility +13 · Weapon +12 · Strength +8 · Defense +4`
- 2. savaşçı: `Agility +10 · Weapon +11 · Strength +6 · Defense +1`

Altında **Rewards**: `213 altın · 75 yiyecek · 2 şarap · 12 su` + iki köle portresi
(`Papirianus`, `Granius`) + bir kart (`2X Production`) + `Weapon Master` kartı + ayrı bir
kalem: **`73` — "Crowd Favour"**.

> İki çıkarım: (1) **Dövüş gerçekten eğitim veriyor** ve miktarı ekranda yazıyor — "dövüş
> antrenmandan hızlı öğretir" iddiası burada görünür hâle geliyor. (2) **Kalabalık favorisi
> ayrı bir ödül satırı** (73 altın), dövüş ödülünün yanında duruyor — yani "iyi dövüş"
> ile "kazanmak" ayrı ödüllendiriliyor.

## 10. Dövüşün kuralları

- Dövüş **gerçek zamanlı**. Oyuncu istersen **tek bir gladyatörü doğrudan sürer**
  (saldırı, blok, kaçış); istemezsen hepsi AI ile dövüşür. **[K]**
- **Kalabalık (crowd favor) bir sistem:** dövüş uzadıkça, sahada olay çoğaldıkça
  kalabalık daha çok seviyor ve **daha çok para** ödüyor. Kalabalık, oyuncunun
  gladyatörünü **elle sürmesinden hoşlanmıyor** ("mind control"). **[T]**
- **Teslim (yield/missio):** `Automatic Yield` becerisinin **oyun içi ipucu metni**:
  *"Gladiator will automatically yield and surrender if they are less than 10% HP."* —
  taban eşik **%10**. Rehberlerin söylediği **%20**, Doctore Emeritus'un *Deeper Humility*
  yükseltmesinden geliyor olmalı; iki sayı aynı kolun iki kademesi. **[V]**
- Teslim eden gladyatör hem **kalıcı sakatlık** almıyor hem de kaybettiği maçtan sağ
  çıkıyor. **[T]** Ayrıca teslim **her dövüşte mümkün değil**: sözleşmede
  `Surrender Allowed: Yes/No` alanı var. **[V]**
- **Kalıcı sakatlık (impediment)** var ve gladyatörü işe yaramaz hâle getirebiliyor;
  sakatlanan köleler ya azat ediliyor ya da sakatlığın önemsiz olduğu bir sınıfa
  (charioteer, sagittarius) kaydırılıyor. **[T]**
- **Ölüm kalıcı.** Ölen gladyatör gider; oyuncular menüden çıkıp kaydı geri yükleyerek
  hile yapıyor. **[T]**
- **Aslan/canavar** sahaya girebiliyor; bloklanmazsa çok yüksek hasar veriyor ve
  gladyatörler onu görmezden gelip birbirine saldırma hatası yapıyor. **[T]**

## 11. Zorluk ölçeklenmesi

- Oyun **kazandıkça zorlaşıyor**: üst üste kazanılan (planlanmış) dövüşler ve
  ludus'un genel gücü/ekipmanı zorluğu yukarı çekiyor. Ölünce **geri inmiyor**. **[T]**
- Bunun doğurduğu oynanış: **kasten kaybetmek**. Rehberlerin hepsi "çıplak köleyi
  gönder, maçı ver, kalabalığı memnun et, şampiyonu koru" diyor. **[K]**
- Ludus **kalabalıksa** rakipler zayıflıyor ("çoğu pit dövüşçüsünde silah bile olmuyor").
  **[T]** — bu ölçeklemenin ikinci ucu: oyuncu kadro büyüterek zorluğu aşağı çekiyor.

> Bu, referansın en tartışmalı kolu: zorluk oyuncunun **başarısını** cezalandırıyor ve
> optimal oynanış "kasten kaybet" oluyor. Bizim ölçümde bağlayıcı kaynağın kadro olması
> (GDD §11) benzer bir yerden geliyor ama ceza mekaniği bizde **yok**.

## 12. Jupiter kutsamaları (kartlar)

- Kart **sürüklenip** bir gladyatöre ya da personele takılır; **satılabilir** (erken oyunda
  para lazımsa satmak öneriliyor). **[K]**
- Etkiler: hasar, savunma, otomatik iyileşme, **araştırma maliyeti/süresi indirimi**
  (%15 ve %33 gibi), Attack Stance, Riposte, çift silah ustalığı, AI yeterliliği...
- **Mavi kartlar** şampiyonluk ödülü olarak geliyor ve en değerlileri bunlar. **[T]**
- **Ekranda görülen kartlar ve birebir metinleri** **[V]**:
  | Kart | Metin |
  |---|---|
  | `Recover Cards` | "Place this card on any entity to recover applied cards." |
  | `Weapon Master` | "All attacks do 35% more damage." |
  | `Rebuff Tolerance` | "Gladiator recovers faster after hitting opponents defense" |
  | `2X Production` (mavi çerçeve) | "Employees who produce resources will generate 2X more [does not stack]" |
- Kart ekranında **SORT** ve **DISCARD** tuşları var — yani kartlar bir **el/deste**
  olarak tutuluyor. **[V]**
- Kartlar **dövüş ödülü olarak da** düşüyor (zafer ekranında `Weapon Master` göründü). **[V]**
- Rakiplerin de kartları olabiliyor; Haruspex bunları söküyor. **[T]**
- **Kart taşımanın bedeli yok:** kart bir gladyatörden alınıp başkasına, hatta bir
  personele **serbestçe** takılabiliyor. Videonun kullanımı: "araştırma maliyetini %10
  düşüren" kartı o an **araştırma yapan** personele tak, iş bitince başkasına geçir;
  iyileşme kartını o an yaralı olana tak. **[V]**
- Tutorial'ı oynarsan **Jupiter kartlarına erişemiyorsun** — rehber bu yüzden tutorial'ı
  atlamayı öneriyor. **[T]**

## 13. Patronaj: şarap ve iki NPC

- **Legate** ve **Magistrate** dövüşleri ayarlayan iki NPC. Onlara **şarap** göndererek
  gözüne girilir; memnun NPC daha iyi ve daha kârlı maçlar ayarlar. **[K]**
- Rüşketin bedeli **her seferinde ikiye katlanır** ve **tam miktar** gönderilmelidir
  (64, 128, 256...); eksik gönderirsen kızıyorlar. Rehber "4 kez gönder" diyor. **[T]**
- Ayrıca bir gladyatöre **patron** olmaları istenebiliyor; patronun gladyatörü ölünce
  yeniden istemek gerekiyor. **[T]**
- **Legate paneli (ekrandan)** **[V]**: başlık "Legate Germanicus Terentius"; bir
  **Temperament** çubuğu ("Neutral"); **Bribery: [1 ▲▼]** sayacı + **Send Wine** tuşu; ve
  eylem listesi: **Suggest Gladiator Patronage**, **Purchase Gladiators**,
  **Arrange Exhibition Match** (kapalı), **Sell Secret \<Magistrate\>** (kapalı),
  **Blackmail \<Legate Secret\>** (kapalı).

  > Yani şarap tek kol değil: **sır satmak ve şantaj** ayrı bir sistem ve kilitli geliyor
  > (Agent'in casusluğuyla açılıyor olmalı). Ayrıca **gösteri maçı ayarlamak** da bu
  > panelden ve kapalı — ilişki seviyesine bağlı.

- **Videonun açılış hilesi:** başta **kullanmayacağın bütün gladyatörleri sat/kov**, elde
  yalnızca iki ana gladyatör kalsın; sonra Legate memnun olana kadar şarap gönder ve
  **patron ol** de — geriye iki kişi kaldığı için patronaj **kesin onlardan birine** düşer.
  Aynısını Magistrate ile yap, ikinci gladyatör de patronlu olur. **Patronlu gladyatörün
  yiyecek/suyunu patron karşılıyor** — yani bu, kaynak tasarrufu için yapılan bir seçim
  daraltma numarası. **[V]**
- Bir rehber tam tersini savunuyor: ilk rüşvetten sonra ilişkiyi umursama, nasılsa
  memnun tutulamıyor **[?]**.

## 14. Olaylar

- Rastgele olaylar var ve bazıları **büyük** hediyeler veriyor: satın alınabilen/ele
  geçirilen gladyatör, Galyalıların baskınında Legate'in hediye ettiği asker (her statta
  **100-150** ile gelebiliyor), aslan sahibi olma. **[T]**
- Personel **ölebiliyor**; NPC'ler ölebiliyor (bu yüzden elde şarap tutulur). **[T]**
- Kapının önünde başıboş gladyatör bulma olayı da var. **[T]**

## 15. Moral

- Moral **statları etkiliyor**. Yükseltme yolları: **coin ya da şarap hediye etmek**
  (ucuz), hamam, Bard şarkıları, Educator. **[K]**
- **Kalabalıklaşma morali düşürüyor**: kadro fazla büyüyünce gladyatörler rahatsız oluyor;
  Doctore Emeritus'un ilk becerisi bu cezayı yumuşatıyor (18 kişiye kadar rahat). **[T]**

## 16. Özgürlük (freedom)

- Gladyatörler yeterince dövüştükten sonra (rehber: ~10 dövüş) **özgürlük istiyor**;
  verilmezse **kaçmaya kalkıyor**. **[T]**
- Azat edilen dövüşçü **final sonrası dövüşte geri dönüyor**, o yüzden geç oyunda kadronun
  yarısı bilerek azat ediliyor. **[T]**
- Kadro tavana dayanınca yer açmak için de azat ediliyor. **[T]**

## 17. Twitch entegrasyonu

- Ayarlardan yayın adı girilerek bağlanılıyor; oyunun botu (`domina_bot`) sohbete girip
  **oy istiyor** ve girdi topluyor. **[K]**
- İzleyici adıyla **gladyatör isimlendirme**, olaylarda **oylama** ve izleyici tepkisine
  göre **ödül artışı** anlatılıyor. **[T]** — Kaynak zinciri burada zayıf; ayrıntılı
  komut listesi bulunamadı, oyun mağazadan kalktığı için resmî belge de yok. **[?]**
- Entegrasyon **isteğe bağlı**; kapalıyken oynanış değişmiyor. **[K]**

> Bizim Faz 5'in girdisi tam olarak bu bölüm. GDD §8'de "isim havuzu chat'ten gelir,
> herkes dahil, `!no` ile çıkış" kararı Domina modelini korumak üzere alınmıştı; ama
> Domina'nın **oylama** tarafının ayrıntısı hâlâ bilinmiyor.

---

## 18. Bizim oyunla eşleştirme

| Domina'da | Bizde | Durum |
|---|---|---|
| Gerçek zamanlı akan gün, duraklatmalı | Ayrık **gün** adımı, tek karar | Kasıtlı fark |
| Doğrudan gladyatör sürme (mind control) | **Yok** — dövüş tam otomatik | Kasıtlı fark (GDD §1) |
| Eğilim statları (aggro/turtle/evasion) | Hedef seçimi ağırlıkları (§4) | **Aynı fikir**; yokai profilleri buradan türeyecek |
| Mash-QTE ile pes etme | **Tek tuş** çekilme kararı | Kasıtlı fark |
| Teslim = sakatlıktan kaçış | Bizde çekilme ödülü siler, sakatlık ayrı | Farklı; ölçülmedi |
| Kalabalık favorisi = para | **Yok** | Açık soru: onur bunun yerini tutuyor mu? |
| Kazandıkça zorlaşma + kasten kaybetme | **Yok** | Bilinçli olarak alınmadı |
| Personel + araştırma ağacı | Okul/tesis ağacı (§10) | Daralttık: personel yok, tesis var |
| Sabit bölge şampiyonları | Kelle avı sözleşmeleri | Benzer rol, farklı çerçeve |
| Yıl sonu tek final (15 rakip) | **Yok** | Açık soru: kampanyanın sonu ne? |
| Azat etme / kaçma | **Yok** | Açık soru: seppuku eşiği bunun karşılığı mı? |
| Ölüm kalıcı | Aynı | Ortak |
| İsim havuzu chat'ten | Aynı (Faz 5) | Ortak |

## 19. Oynarken doğrulanacaklar

Video ilk turda şu soruları **kapattı**: takvim 365 gün (`Days Left 364` ilk karede),
stat panelinin tam alan listesi, personelin günlük yiyecek/su tüketimi, kömür çukurunun
ne yaptığı (çevikliğin **eğitim süresini** kısaltıyor), Doctore ağacının düğüm adları ve
fiyat ölçeği, dövüş sözleşmesinin alanları, kalabalık favorisinin **ayrı bir ödül satırı**
olduğu.

Açık kalanlar:

1. Bir günde kaç iş yapılabiliyor? Dövüşün "6 gün" katılım bedeli var ama alışveriş,
   araştırma ve eğitim aynı gün içinde nasıl sıralanıyor?
2. Teslim eşiği gerçekten %20 mi; `Surrender Allowed: No` olan dövüşte teslim olmaya
   çalışan ne oluyor?
3. Kalıcı sakatlık (impediment) ekranda nasıl görünüyor, hangi statı ne kadar düşürüyor?
4. Kalabalık favorisi nasıl hesaplanıyor — süre mi, vuruş sayısı mı, ölüm mü?
5. Zorluk ölçeklemesi görünür bir sayı mı, yoksa yalnızca hissediliyor mu?
6. Jupiter kartlarının tam listesi ve etkilerinin büyüklüğü (elimizde 4 tanesi var).
7. Twitch tarafında chat'in gerçekten neyi oylayabildiği — hâlâ hiçbir ekran görüntüsü yok.
8. Ekonominin gerçek eğrisi: ilk 10 günde ne kadar altın giriyor/çıkıyor? (Videoda kasa
   1000'den 680'e iniyor, sonra dövüşle 954'e çıkıyor — tek örnek.)
9. **Personel slotu kaç?** Video 3 diyor (2018), daha yeni rehber 6 + 1500 altınlık
   genişletme diyor.
10. **Architect gerçekten geri alınamıyor mu?**
11. Sır satma / şantaj sistemi nasıl açılıyor ve ne veriyor?
12. `Level` ile `Points` arasındaki dönüşüm ne? (Örn. Agility 13. seviye = 62 puan.)
13. Sınıf araştırması (400 altın) ile dövüş ödülü olarak gelen sınıf arasındaki fark ne?

---

*Bu dosya oynanış ve video incelemesiyle güncellenecek; her güncellemede işaretler
(**[K]/[T]/[?]/[V]**) yeniden gözden geçirilmeli.*
