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

**Önemli uyarı:** Kaynakların çoğu **strateji rehberi**. Rehber "şunu yap" der, "kural
budur" demez; ayrıca oyun 2017-2021 arası çok değişti (rehberlerin kendisi "eski
rehberler artık geçersiz" diyor — özellikle Faber ve EXP nerf'leri). Bu yüzden sayıların
büyük kısmı **[T]**. Kesinleşmesi oynayarak olur.

---

## 1. Çerçeve

- Oyuncu, babasından kalan **ludus**'u (gladyatör okulu) devralan bir kadındır (*domina* =
  hanım). Hedef okulun itibarını geri kazanmak. **[K]**
- Oyun **bir yıllık geri sayım**la işler: ekranda "kalan gün" durur, sıfırlandığında
  **final şampiyonası** oynanır. Rehberler günleri geri sayarak konuşuyor: "300 gün kala
  orta oyun", "50-60 gün kala geç oyun", "6-10 gün kala Haruspex". Başlangıcın 365 gün
  olduğu doğrudan yazmıyor. **[?]**
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

## 7. Personel (employees)

Personel **slot**larda durur; slot sayısı sınırlı ve parayla (1500 altın) genişletilebilir
**[T]**. Kritik kural: **birini kovarsan onun araştırdığı bonuslar da gider** — Architect'in
inşa ettiği binalar kalır, ama diğerlerinin pasif bonusları düşer. **[K]**

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

## 8. Ekipman

- Yuvalar: **silah, kalkan, kask, göğüs, omuz (pauldron), bel/etek, bacak (greaves)**,
  ayrıca Retiarius için **ağ**. **[K]**
- Her parçanın **yükseltme basamakları** var; Faber otomatik yükseltir, oyuncu parayla
  atlar. Onarım ayrı bir iş. **[K]**
- Rehberlerin "iyi fiyat/performans" seti: Gladius, Elite Roman Centurion Shield, Death's
  Helmet, Centurion Mail, Centurion Leathers, Onyx Greaves. **[T]**
- Fiyat örnekleri **[T]**: çift gladius 80 altın; tipik Murmillo takımı 412 altın; 13
  kişilik temel kuşam 6.400 altın; 13 Zweihander 3.600 altın.
- **Ağırlık gerçek bir maliyet:** ağır zırh yavaşlatır, stamina yakar; rehberler "ağır
  zırhlı rakip 2-3 vuruşta nefessiz kalır" diye **düşmanın zırhını zayıflık** olarak
  kullanıyor. **[K]**
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

## 10. Dövüşün kuralları

- Dövüş **gerçek zamanlı**. Oyuncu istersen **tek bir gladyatörü doğrudan sürer**
  (saldırı, blok, kaçış); istemezsen hepsi AI ile dövüşür. **[K]**
- **Kalabalık (crowd favor) bir sistem:** dövüş uzadıkça, sahada olay çoğaldıkça
  kalabalık daha çok seviyor ve **daha çok para** ödüyor. Kalabalık, oyuncunun
  gladyatörünü **elle sürmesinden hoşlanmıyor** ("mind control"). **[T]**
- **Teslim (yield/missio):** Doctore araştırması *Automatic Yield*, Doctore Emeritus'un
  *Deeper Humility I* becerisi "**%20 canda teslim ol**" verir. Teslim eden gladyatör
  hem **kalıcı sakatlık** almıyor hem de kaybettiği maçtan sağ çıkıyor. **[T]**
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
- Rakiplerin de kartları olabiliyor; Haruspex bunları söküyor. **[T]**
- Tutorial'ı oynarsan **Jupiter kartlarına erişemiyorsun** — rehber bu yüzden tutorial'ı
  atlamayı öneriyor. **[T]**

## 13. Patronaj: şarap ve iki NPC

- **Legate** ve **Magistrate** dövüşleri ayarlayan iki NPC. Onlara **şarap** göndererek
  gözüne girilir; memnun NPC daha iyi ve daha kârlı maçlar ayarlar. **[K]**
- Rüşketin bedeli **her seferinde ikiye katlanır** ve **tam miktar** gönderilmelidir
  (64, 128, 256...); eksik gönderirsen kızıyorlar. Rehber "4 kez gönder" diyor. **[T]**
- Ayrıca bir gladyatöre **patron** olmaları istenebiliyor; patronun gladyatörü ölünce
  yeniden istemek gerekiyor. **[T]**
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

Aşağıdakiler **[T]** ya da **[?]** işaretli olduğu için oynayışla kapanacak. Video ya da
kendi oturumunda özellikle bunlara bak:

1. Takvim gerçekten 365 gün mü, ekranda ne yazıyor?
2. Bir günde kaç iş yapılabiliyor — dövüş günü yiyor mu, alışveriş yiyor mu?
3. Pazar yenilenme aralığı ve şarap stok tavanı.
4. Stat ekranındaki **tam alan listesi** (ekran görüntüsü şart).
5. Eğitim slider'ları: kaç stat, tavan var mı, günlük kazanç ne kadar?
6. Teslim eşiği gerçekten %20 mi, teslim eden ölüyor mu?
7. Kalıcı sakatlık türleri neler, hangi statı ne kadar düşürüyor?
8. Kalabalık favorisi ödülü nasıl hesaplanıyor (süre mi, vuruş sayısı mı)?
9. Zorluk ölçeklemesi görünür bir sayı mı, yoksa yalnızca hissediliyor mu?
10. Jupiter kartlarının tam listesi ve etkilerinin büyüklüğü.
11. Twitch tarafında chat'in gerçekten neyi oylayabildiği.
12. Ekonominin gerçek eğrisi: ilk 10 günde ne kadar altın giriyor/çıkıyor?

---

*Bu dosya oynanış ve video incelemesiyle güncellenecek; her güncellemede işaretler
(**[K]/[T]/[?]**) yeniden gözden geçirilmeli.*
