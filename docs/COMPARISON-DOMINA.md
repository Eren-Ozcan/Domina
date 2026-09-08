# Domina ile kalem kalem karşılaştırma

Bu dosya üç soruya cevap verir: **neyi aynı yaptık**, **neyi bilerek başka türlü yaptık**,
**neyi hiç yapmadık** — ve yapmadıklarımızın hangisi karar, hangisi boşluk.

Karşı taraf: *Domina* (Dolphin Barn, 2017). Sistem dökümü `REFERENCE-DOMINA.md`, ekran
dökümü `REFERENCE-DOMINA-UI.md`. Bizim taraf: `docs/GDD.md` + kodun bugünkü hâli
(2026-09-05).

Her satırda **durum** işareti:

| İşaret | Anlamı |
|---|---|
| ✅ | Bizde var ve çalışıyor (kodda, testli) |
| 🟡 | Karar verildi, kod yok ya da yarım |
| 🔵 | **Bilerek almadık** — gerekçesi yazılı |
| ⚪ | **Boşluk** — ne karar var ne kod; düşünülmemiş ya da askıda |

---

## 1. Çerçeve ve zaman

| Konu | Domina | Bizde | Durum |
|---|---|---|---|
| Zaman akışı | **Gerçek zamanlı**, duraklatmalı; gün kendiliğinden akar | **Ayrık gün**; oyuncu bir karar verene kadar hiçbir şey olmaz | 🔵 |
| Takvim | **365 gün** geri sayım, ekranda `Days Left` | Gün sayacı var, **üst sınır yok** — kampanya bitmiyor | ⚪ |
| İkinci saat | `Next Battle: n` — mecburi dövüşe kalan gün | Yok; günün teklifi her gün yeniden üretilir, kaçırmanın cezası yok | ⚪ |
| Bitiş | Yıl sonunda **final şampiyonası**, 15 rakip | Yok | ⚪ |
| Finale giriş koşulu | Haritada **en az 3 bölge şampiyonu** yenilmiş olmalı | Yok (kelle avı sözleşmeleri var ama kapı değil) | ⚪ |
| Zorluk kademesi | Menüden seçilir (en üstü Pro-Gamer) | Yok | ⚪ |
| Kaybetme | Şampiyonu ölünce pratikte oyun biter (resmî game over değil) | Dojo kapanabiliyor (ölçümde %1.2) ama **anlatısal bir son yok** | ⚪ |

> **En büyük boşluk burada.** Domina'nın bütün ekonomi baskısı "365 gün sonra 15 kişilik
> bir orduya çıkacaksın" hedefinden geliyor: her altın o güne yatırım. Bizde kampanyanın
> **sonu yok**, dolayısıyla "bugün mü harcayayım, sonraya mı saklayayım" sorusunun
> pusulası da yok. Ölçümlerimiz 60 gün üzerinden yapılıyor ama 60. günde ne olduğu
> tanımsız. GDD'de bu bir Açık Karar olarak bile durmuyor.

## 2. Kadro ve savaşçı

| Konu | Domina | Bizde | Durum |
|---|---|---|---|
| Kadro büyüklüğü | Bina yükseltmeleriyle **28'e** kadar; kalabalık morali düşürür | Sınır yok; ölçümde 4 hedefleniyor | ⚪ |
| Sefere giden | Dövüş sözleşmesi belirler (1v1, 2/3, 15 kişi) | **En çok 4**; teklif bazen tam sayı dayatır | 🔵 |
| Statlar | HP, Strength, Weapon, Defense, Agility, Meditate + türetilmiş Aggro/Turtle/Evasive/Stamina | **8 stat:** MaxHealth, Aggression, Defense, Evasion, Strength, Accuracy, MaxStamina, Speed | ✅ |
| Güç ne yapıyor | **Strength = can + hasar + direnç** (tek slider) | Üçü **ayrı stat** (MaxHealth / Strength / Defense) | 🔵 |
| Davranış eğilimi | Aggro / Turtle / Evasive **açık sayı**, sınıfa göre | `Aggression` var; hedef seçimi ağırlıkları §4'te — **yokai profilleri henüz yok** | 🟡 |
| Sınıf | 8 sınıf (Murmillo, Thraex, Retiarius, Scissor, Velite, Sagittarius, Charioteer, Behemoth) | **Sınıf yok** (GDD §4) — kimlik silahtan ve yoldan gelir | 🔵 |
| "Yol" / uzmanlaşma | Sınıf seçimi geri alınamaz | **WarriorPath:** None / Blade / Stone / Shadow, geri alınamaz | ✅ |
| Yetenek farkı | Kölenin başlangıç statları | `Talent` çarpanı — antrenmandan ne kadar faydalandığı | ✅ |
| İsim | Chat'ten (Twitch) ya da havuzdan | Şimdilik yerel havuz, Faz 5'te chat | 🟡 |
| Ölüm | Kalıcı | Kalıcı | ✅ |
| Kalıcı sakatlık | "Impediment" var; sakat savaşçı işe yaramaz hâle gelebilir | **Uzuv kaybı**, taraflı cezalarla (kılıç kolu ×0.65, bacak ×0.55 kaçınma) — savaşçı yaşamaya devam eder | ✅ 🔵 |
| Moral | Temperament çubuğu; **statları etkiler**; coin/şarap/hamam/ozanla yükselir | **Onur** var ama moral **yok** — savaşçının keyfi diye bir kaynak yok | 🔵 |
| Azat etme | ~10 zaferden sonra özgürlük ister, verilmezse kaçar; azat edilen finalde döner | Yok | ⚪ |
| Öldürme/satma | `Put to Death`, `Sell` düğmeleri | Yok — savaşçıdan kurtulma yolu yalnızca ölüm ya da seppuku | ⚪ |

## 3. Eğitim

| Konu | Domina | Bizde | Durum |
|---|---|---|---|
| Nasıl | Stat başına **kaydırıcı**, gün boyu sürekli; `Level` + `Points` | Günlük **tek talim** seçimi: `Strikes / Guard / Footwork / Conditioning` | 🔵 |
| Hız kolu | Palus, taş, kömür çukuru, hamam + Doctore/Bard/Educator araştırmaları | Okul düğümleri: Talimhane, İç dojo, Kata ustası | ✅ |
| Tavan | Doctore araştırmalarıyla yükselen stat tavanı | `FormsMaster` tavanı yükseltir | ✅ |
| Dövüşten öğrenme | **Baskın yol** — bir dövüş 10 günlük antrenman kadar; zafer ekranında `+13 Agility` diye yazıyor | **Yok.** Dövüş stat kazandırmıyor; ilerleme yalnızca antrenman günlerinden | ⚪ |
| Zararsız eğitim maçı | **Exhibition** — teslim var, ölüm yok; ana EXP kaynağı | Yok. Her dövüş ölümcül | ⚪ |
| Otomatik eğitim | `Enable Automatic Gladiator Training` kutusu | Talim seçili kalır, gün kapanınca işler | ✅ |

> **İkinci büyük boşluk.** Domina'da bir savaşçı **dövüşerek** büyür; antrenman onun
> yavaş yedeği. Bizde tam tersi: dövüş yalnızca eritir. Bu, iki oyunun temposunu bambaşka
> yapıyor — onlarda "riske gir, güçlen", bizde "riske gir, yıprat". Bizim tarafın gerekçesi
> yazılı değil; ölçümde **bağlayıcı kaynağın kadro olması** (GDD §11) muhtemelen bunun
> sonucu.

## 4. Ekonomi

| Konu | Domina | Bizde |
|---|---|---|
| Para | Tek: coin | Tek: **altın** ✅ |
| Stok kaynakları | Food, Water, **Wine**, Stone | Yiyecek, Su, **İlaç** ✅ 🔵 |
| Başlangıç | 1000 altın · 800 yiyecek · 400 su · 80 şarap | **600 altın**, ambar boş, 4 savaşçı 🔵 |
| Savaşçı fiyatı | Magistrate'ten değişken; ödül olarak da geliyor | **150 altın**, pazarda 10 aday, fiyat stat+yeteneğe göre ✅ |
| Personel gideri | Alım bedeli **13-100 altın** + günlük yiyecek/su | Personel yok 🔵 |
| Zırh/onarım | Kademeli fiyat eğrisi; Faber bedavaya yükseltiyor | Dayanıklılık başına **1.50**, onarım **0.90/yıpranma** ✅ |
| Günlük tüketim | Savaşçı + personel başına yiyecek/su | Savaşçı başına 1 yiyecek + 1 su; revirdekine ilaç 12 ✅ |
| Dövüş ödülü | Altın + yiyecek + su + şarap + **köle** | Yalnızca **altın** (düşman canı × 0.45 + risk primi) 🔵 |
| "İyi dövüş" ödülü | **Crowd Favour** ayrı kalem (örn. 73 altın) | Yok — ödül yalnızca sonuca bağlı ⚪ |
| Kumar | Pit dövüşlerine **bahis** (150 altın) | Yok ⚪ |
| Hırsızlık | Agent silah/zırh çalar | Yok 🔵 |
| Pazarlık | Emptor indirimi, Faber indirimiyle birikir | `Broker` ve `Steward` düğümleri ✅ |
| Kıtlık | Kuraklık/sel arzı vurur; kuyu/ambar sigortadır | Rastgele olaylar: hırsızlık, bozulma, kuyu, küflü ilaç, hastalık ✅ |
| Olay sıklığı | Bilinmiyor | **Günde %15**, beş tür, hepsi eksiltir ✅ |

## 5. Dövüş

| Konu | Domina | Bizde | Durum |
|---|---|---|---|
| Çözüm | Gerçek zamanlı, motorun içinde | **Motordan bağımsız çekirdek**, seed'li deterministik | 🔵 |
| Müdahale | `Mind Control` araştırılırsa **tek savaşçı elle sürülür**; kalabalık bundan hoşlanmaz | Yok — dövüş tam otomatik | 🔵 |
| Tek müdahale | Mash-QTE ile pes etme | **Tek tuş "Kaç"** kararı, açılma koşullu, onur bedelli | 🔵 |
| Teslim eşiği | `Automatic Yield`: **%10 canın altında** otomatik teslim | Çekilme kararı oyuncunun; otomatik eşik yok | 🔵 |
| Teslim hakkı | Sözleşmede **`Surrender Allowed: Yes/No`** | Çekilme **her zaman** açık | ⚪ |
| Süre sınırı | Ekranda geri sayan sayaç (~3 dk) | `BattleOutcome.TimeLimit` var ✅ | ✅ |
| Blok | Var (kalkan, `Shield Control`) | `CombatState.Blocking`, Savunma statından türeyen karar ✅ | ✅ |
| Hücum | Görünmüyor | Charge: mesafe 320, olasılık 0.40, birikme 0.75 sn ✅ | ✅ |
| Silah düşürme | `Disarming Weapon` becerisi | Zırha inen vuruşta 0.05 taban, silah yere savrulur, yerden alınabilir ✅ | ✅ |
| Silah yakalama | Yok | **Jitte/sai** kilitleme ✅ | ✅ (bizde fazladan) |
| Zehir | Yok | Doz/tik/ömür kurallarıyla ✅ | ✅ (bizde fazladan) |
| Sersemletme | Yok | Künt silahların karşılığı ✅ | ✅ (bizde fazladan) |
| Mermi | `Throw Weapons` becerisi, Sagittarius sınıfı | `ThrownWeapon` ayrı yuvada, uçuş süresi modellenmiş ✅ | ✅ |
| Zırh | Yuva yuva, ağırlık staminayı yakar; `A/D/kg` ekranda | **Altı yuva**, ağırlık saldırı döngüsünü uzatır, yıpranma birikir ve parça **dağılır** ✅ | ✅ |
| Uzuv kopması | `Dismemberment` becerisi — çoğunlukla ölüm gore'u | **Yaşayıp sakat kalma** — sistemin merkezinde 🔵 | ✅ |
| Sahadaki engel | `Obstacles: Lions / Tigers / It's a mystery` — sözleşmede yazıyor | Yok ⚪ | ⚪ |
| Rakip | İnsan gladyatörler + canavarlar | **Yokai** (kappa, kitsune, tengu, oni, jorōgumo) 🔵 | ✅ |

## 6. Dövüş türleri

| Domina | Bizde | Durum |
|---|---|---|
| Planlanmış dövüş (Legate/Magistrate ayarlar, gün yer) | **Günün teklifi** — al ya da bırak, girmek bir gün yer | ✅ |
| Pit fight (rakip görünmez, bahis var) | Yok | ⚪ |
| Exhibition (ölümsüz eğitim maçı) | Yok | ⚪ |
| Bölge şampiyonları (9 sabit rakip, finale kapı) | **Kelle avı sözleşmeleri** — süreli, onur ödüllü, kabul gün yemez | ✅ 🔵 |
| Chariot race / Beast mode / Gravitas | Yok | 🔵 |
| Final şampiyonası | Yok | ⚪ |

## 7. Yönetim katmanı

| Konu | Domina | Bizde | Durum |
|---|---|---|---|
| Yapı | **Personel** (14 rol) + her birinin araştırma ağacı | **Okul ağacı**: 9 düğüm, üç kol (eğitim / sağlık / ekonomi) | 🔵 |
| Slot kısıtı | Aynı anda 3 (2018) ya da 6 personel; kovulan personelin bonusu **gider** | Düğüm alınınca **kalıcı** — geri alma yok, slot yok | 🔵 |
| Maliyet | Personel 13-100 altın + **günlük stok**; araştırma altın + tur + stok | Düğüm **yalnızca altın**; kol içinde sıra zorunlu | 🔵 |
| Bina | Palus, hamam, kuyu, ambar, şarap mahzeni, özel oda, duvar | Soyut düğümler (Talimhane, Revir...) — fiziksel bina yok | 🔵 |
| İnşa süresi | Kum saati (tur) | Anında | 🔵 |
| NPC ilişkisi | Legate + Magistrate: şarap rüşveti, patronaj, **sır satma, şantaj** | Yok — teklifleri kimse "ayarlamıyor" | ⚪ |
| Patronaj | NPC bir savaşçının **yiyecek/suyunu ölene kadar** öder | Yok | ⚪ |
| Kart sistemi | Jupiter kutsamaları: sürüklenip savaşçıya/personele takılır, satılır, elde tutulur | Yok | 🔵 |

## 8. Chat / seyirci

| Konu | Domina | Bizde | Durum |
|---|---|---|---|
| Bağlantı | Ayarlardan yayın adı; `domina_bot` sohbete girer | Platform-bağımsız adapter (Twitch + Kick aynı iç olaya düşer) | 🟡 |
| Katılım | İzleyici adıyla gladyatör | **Aynı model**: herkes dahil, `!no` ile çıkış | 🟡 |
| Oylama | Olaylarda oy | **Seppuku oylaması** (`SeppukuArbiter`), onur komutları | ✅ |
| Ekonomi etkisi | İzleyici tepkisine göre ödül artışı (kaynak zayıf) | **Onur → ödül çarpanı** (GDD §6) | ✅ |
| Yayınsız oynanış | Entegrasyon kapalıyken oyun aynı | **AI seyirci tam eşdeğer** (GDD §9) | 🔵 |
| Kalabalık parası | Crowd Favour — dövüşün *seyirliğine* ödeme | Yok; kalabalık yalnızca onur üzerinden konuşuyor | ⚪ |

## 9. Bizde olup Domina'da olmayanlar

Bunlar "eksik" değil, **bizim eklediklerimiz** — hepsi kodda ve testli:

1. **Motordan bağımsız, deterministik çekirdek.** On binlerce dövüş simüle edilerek denge
   ölçülüyor (`Domina.Sim`). Domina'nın böyle bir kolu yok; denge sayıları oyuncuların
   tahminleriyle konuşuluyor.
2. **Ölçülmüş ekonomi.** 1000 dojo × 60 gün koşuları; fiyatların gerekçesi ölçüm.
3. **Kılıç yakalama (jitte/sai), zehir, sersemletme** — üç ayrı silah kimliği.
4. **Zırh yıpranması ve dövüşün ortasında dağılması.**
5. **Uzuv kaybının hayatta kalarak sürmesi** ve taraflı cezalar (kılıç kolu / boş kol /
   bacak ayrı).
6. **Onur sistemi** — 0-100, decay'li, ödül çarpanına ve seppuku oylamasına bağlı.
7. **Sözleşme onuru:** kelle avı kabul edilip dönülmezse kadro onur kaybeder.
8. **Yol seçimi** (Blade / Stone / Shadow) — sınıf yerine geri alınamaz uzmanlaşma.
9. **Versiyonlu, merge-on-load kayıt** ve uyarıların oyuncuya gösterilmesi.

## 10. Sayı sayı özet

| Ölçü | Domina | Bizde |
|---|---|---|
| Başlangıç kasası | 1000 | **600** |
| Başlangıç kadrosu | 3 | **4** |
| Başlangıç ambarı | 800 yiyecek / 400 su / 80 şarap | **boş** |
| Kampanya uzunluğu | 365 gün | **sınırsız** (ölçüm 60 gün) |
| Sefer ekibi | Sözleşmeye göre 1-15 | **en çok 4** |
| Pazar | 10 aday? (bilinmiyor) — Magistrate'ten | **10 aday**, her gün yenilenir |
| Savaşçı fiyatı | değişken | **150 altın** taban |
| Sıradan araştırma | ~20-70 altın + 6 tur | Okul düğümü: altın, süre yok |
| Sınıf açma | **400-500 altın, 16-17 tur** | — (sınıf yok) |
| Teslim eşiği | **%10** (yükseltmeyle %20) | oyuncu kararı |
| Dövüş süresi | ~3 dakika sayaç | `TimeLimit` var |
| Olay sıklığı | bilinmiyor | **günde %15** |

---

## 11. Buradan çıkan iş listesi

Aşağıdakiler öneri; hiçbiri karar değil. Sıralama **etkiye göre**.

### A. Kampanyanın sonu yok (en büyük boşluk)

Domina'nın bütün gerilimi 365 günlük geri sayımdan geliyor. Bizde gün sayacı var ama
hedef yok. Üç seçenek: (1) sabit uzunlukta sezon + kapanış dövüşü, (2) açık uçlu ama
artan zorluk, (3) kelle avlarını kapıya çevirip "üç kelle = son sözleşme". Ölçümlerimiz
zaten 60 gün üzerinden; **60 günün sonunda ne olduğu tanımlanmalı**.

### B. Dövüş neden hiçbir şey öğretmiyor?

Referansta savaşçıyı büyüten şey dövüş. Bizde dövüş yalnızca eritiyor, büyüme yalnızca
antrenman gününden geliyor — yani **sefere çıkmak saf kayıp**, ödül dışında bir sebebi yok.
Bu bilinçli bir karar mı, yoksa hiç konuşulmamış mı? GDD'de gerekçe yok. Ölçümdeki
"bağlayıcı kaynak kadro" bulgusu doğrudan buradan çıkıyor olabilir.

### C. Zararsız dövüş yok

Exhibition, Domina'da hem eğitim hem gelir hem de "yeni adamı dene" kapısı. Bizde her
dövüş ölümcül, dolayısıyla acemi savaşçıyı denemenin **hiçbir güvenli yolu yok**. Antrenman
bunun yerini tutmuyor çünkü antrenman bir karar değil, bir bekleme.

### D. Moral diye bir kaynak yok

Onur, savaşçının **itibarı**; keyfi değil. Domina'da moral statları doğrudan etkiliyor ve
"parayla moral satın alma" küçük ama sürekli bir karar üretiyor. Bizde savaşçıya
harcanabilecek tek şey ekipman.

### E. Kadrodan kurtulma yolu yok

Domina'da fazla/sakat savaşçı satılır, azat edilir, öldürülür. Bizde yalnızca ölür.
Sakat kalan savaşçı sonsuza kadar kadroda duruyor ve yiyecek yiyor — bu bir karar değil,
bir sızıntı.

### F. "İyi dövüş" ödüllendirilmiyor

Crowd Favour, kazanmakla *iyi dövüşmeyi* ayırıyor. Bizde ödül yalnızca sonuca bağlı;
onur sistemi bunun yerini kısmen tutuyor ama **paraya** dönmüyor.

### G. Teslim hakkı sözleşmeye bağlanabilir

`Surrender Allowed: No` tek satırlık bir kural ama bütün bir dövüşün karakterini
değiştiriyor. Bizim çekilme tuşumuz her zaman açık; bazı sözleşmelerin bunu kapatması
kelle avlarına ağırlık katardı.

### H. Alınmayacaklar (bilerek)

- Elle savaşçı sürme (mind control) — GDD §1.
- Kazandıkça zorlaşma + kasten kaybetme sömürüsü — oyuncunun başarısını cezalandırıyor.
- Personel + slot ekonomisi — okul ağacı bunun yerine geçti, daha az mikro yönetim.
- Kart/kutsama sistemi — ikinci bir rastgelelik katmanı; bizde rastgelelik zaten günlük
  olaylarda ve dövüşte.
- Şarap rüşveti / şantaj — chat entegrasyonu zaten "dışarıdaki güç" rolünü üstleniyor.

---

*Güncelleme kuralı: bu dosya `REFERENCE-DOMINA*.md` ya da GDD değiştiğinde birlikte
güncellenir. ⚪ işaretli her satır ya bir GDD kararına ya da bilinçli bir "almıyoruz"
satırına dönüşmeli.*

---

# 12. Değişenler — satır satır karar turu (2026-09-05)

Bu bölüm, yukarıdaki tablolar tek tek gözden geçirilirken alınan kararları tutar.
Yukarısı **olduğu gibi bırakılır** (tarihsel kayıt); güncel karar burada yazandır.
Çakışma olursa **bu bölüm geçerlidir**. Tur bitince GDD'ye işlenecek.

## Bölüm 1 — Çerçeve ve zaman ✔ tamamlandı

| Satır | Eski durum | Yeni karar | Not |
|---|---|---|---|
| Zaman akışı | Ayrık gün 🔵 | **Gerçek zaman + duraklat** (Domina modeli) | Çekirdek sabit tik'le ilerlemeye devam eder, determinizm korunur; gerçek zaman yalnızca tik'i saatin sürmesi. Ayrık günü varsayan gün-kapanışı/olay kodu yeniden kurulacak. Chat oylaması için duraklat-ya-da-pencere kuralı gerekir. |
| Takvim | Üst sınır yok ⚪ | **Sabit geri sayım**, ekranda kalan gün | Uzunluk sayısı Bölüm 10'da kararlaştırılacak (ölçümler 60 gün üzerinden). |
| İkinci saat | Yok ⚪ | **Mecburi dövüş sayacı** (`Sıradaki dövüş: n gün`) | Kaçırmanın cezası var; sonsuz güvenli antrenman sömürüsünü kapatır. Ceza türü ve n sayısı ayrıca belirlenecek. |
| Bitiş | Yok ⚪ | **Final turnuvası** — son gün, elemeli, tüm kadro | Sezonun hedefi kadro genişliği ve derinliği. Rakip sayısı belirlenecek. |
| Finale giriş koşulu | Yok ⚪ | **Kelle avı kapısı** — n kelle tamamlanmadan finale girilemez | Mevcut kelle avı sistemine amaç verir. n (Domina'da 3) belirlenecek. |
| Zorluk kademesi | Yok ⚪ | **Kademe seçimi**: Çırak / Usta / Efsane | "Usta" denge ölçümünün tabanı; diğer kademeler çarpanla türetilir, ayrı ölçüm koşusu değil. |
| Kaybetme | Anlatısal son yok ⚪ | **Net son: dojo kapanır** | Kasa ve kadro bitince kapanış ekranı + sezon özeti (gün, zafer, ölü sayısı). Geri sayım + mecburi dövüş varken kaybetmek gerçek ihtimal. |

### Bölüm 1'in getirdiği yeni işler
- Gerçek zaman geçişi: mevcut ayrık gün mimarisi (gün kapanışı, olay tetikleme, chat oylaması) yeniden tasarlanmalı — ROADMAP'e risk olarak girecek.
- Final turnuvası, mecburi dövüş sayacı, kademe çarpanları, kapanış ekranı: dördü de yeni sistem.
- Açık sayılar: sezon uzunluğu, mecburi dövüş aralığı ve cezası, finale kapı kelle sayısı, turnuva rakip sayısı.

## Bölüm 2 — Kadro ve savaşçı ✔ tamamlandı

| Satır | Eski durum | Yeni karar | Not |
|---|---|---|---|
| Kadro büyüklüğü | Sınır yok ⚪ | **Kademeli tavan**, dojo yükseltmesiyle açılır (başlangıç ~6) | Kadro büyütmek yatırım kararı olur; final turnuvası için derinlik biriktirmek anlam kazanır. Her kademe altın + günlük stok yükü getirir. |
| Sefere giden | En çok 4 🔵 | **Aynı: en çok 4** | Dövüş sahnesi okunaklı kalır; denge ölçümü bu varsayımla yapıldı. Final turnuvasında derinlik yedek kadro olarak işe yarar. |
| Statlar | 8 stat ✅ | **9. stat: İrade** eklenir | Seppuku direnci, panik eşiği, onur kazancı. Domina'daki Meditate'in karşılığı. |
| Güç ne yapıyor | Üçü ayrı 🔵 | **Aynı: üçü ayrı** (MaxHealth / Strength / Defense) | Antrenman seçimi anlamlı kalsın: dayanıklı ama vurucu olmayan savaşçı mümkün. |
| Davranış eğilimi | Tek `Aggression` 🟡 | **Üç eğilim, açık sayı**: Saldırgan / Savunmacı / Kaçıngıl | Hem savaşçıda hem yokai'de ekranda görünür. Dövüş tam otomatik olduğu için oyuncunun dövüşü önceden okuyabilmesi kritik. |
| Sınıf | Sınıf yok 🔵 | **Sınıf sistemi eklenir** — açılır, atanır, geri alınmaz | Yol ile birlikte var olur: **Yol = stat eğilimi, Sınıf = rol** (silah + davranış). GDD §4'ün "sınıf yok" kararı geçersiz. |
| Yol / uzmanlaşma | 3 yol, geri alınamaz ✅ | **Aynı** — ama sakatlıkta **sınıf** yeniden seçilir, Yol asla | Yol 20 antrenman günüyle kazanılıyor; sakatlık o emeği silmemeli. Kolunu kaybeden adamın çevikliği kaybolmaz — değişen şey nasıl dövüştüğü. |
| **Sakatlıkta sınıf değişimi** (yeni satır) | — | **Uzuv kaybı sınıf seçimini yeniden açar**; kaybedilen uzuv bazı sınıfları imkânsız kılar, kalanlardan oyuncu seçer | Uzuv kaybı "sızıntı" olmaktan çıkıp **ikinci kariyer**e döner. Kader değil, daralan seçim. |
| Yetenek farkı | Talent çarpanı ✅ | **Talent + farklı başlangıç statları birlikte** | Pazarda iki eksen: şimdi güçlü olan mı, sonra güçlenecek olan mı. Geri sayım varken "yetiştirmeye vaktim var mı" sorusu doğar. |
| İsim | Havuz → Faz 5 chat 🟡 | **Aynı plan** | Yayınsız oynanış tam eşdeğer kalır. |
| Ölüm | Kalıcı ✅ | **Kalıcı + miras** | Devreden **yalnızca iki şey**: (1) **ekipmanı** — silahı ve zırhı dojo ambarına döner, **ama yalnızca dövüş kazanılırsa**: cesedi taşıyacak sağ kalan biri gerekir. Tek kişilik seferde ölen savaşçının ekipmanı da sahada kalır. Kalabalık sefere çıkmanın ölçülebilir bir getirisi olur; 1v1 sözleşmeler ekipman riski taşır. (2) **onuru ve unvanı** — dojo duvarına yazılır, kalıcı küçük onur artışı, sezon özetinde görünür. Devretmeyen: **sınıf** (450 altın) ve **yol** (20 antrenman günü) — ikisi de tamamen gider, ölüm ağır kalır. Gerekçe: bu turda savaşçı pahalılaştı (sınıf + yol + moral); saf permadeath oyuncuyu sahaya çıkmaktan kaçındırırdı — GDD §10'un kendi uyarısı artık bize karşı çalışıyordu. |
| Kalıcı sakatlık | Uzuv kaybı, taraflı cezalar ✅🔵 | **Ham cezalar aynen kalır** (kılıç kolu ×0.65, bacak ×0.55); telafi **sınıf değişimi** | Ceza hafifletilmez — kayıp gerçek kalır, ama çıkış yolu var. |
| Moral | Yok 🔵 | **Moral kaynağı eklenir**; İrade ile çift yönlü bağlı | İrade yüksek → moral yavaş düşer, yenilgiden sonra kolay kırılmaz. Moral düşük → İrade'ye dayanan kontroller (seppuku riski, panik eşiği) aleyhe kayar. Moral kısa vadeli keyif, İrade uzun vadeli dayanıklılık. |
| Azat etme | Yok ⚪ | **Emeklilik: usta olur** | Çok zafer alan ya da ağır sakatlanan savaşçı sahayı bırakıp eğitmen olur: antrenman hızına kalıcı bonus, günlük yiyecek yükü biter, bir daha sahaya çıkmaz. Kaybetmek yerine dönüştürme. |
| Öldürme / satma | Yok ⚪ | **Yalnızca onurlu çıkışlar**: seppuku, emeklilik, yolcu etme | Öğrenci mal değil — satılmaz, öldürülmez. "Sakat savaşçı sonsuza kadar yiyecek yiyor" sızıntısı emeklilikle kapanır. |

### Bölüm 2'nin getirdiği yeni işler
- **Sınıf sistemi**: sınıf listesi, açılış maliyeti, uzuv-sınıf uygunluk matrisi, Yol ile etkileşimi.
- **İrade statı** ve **moral kaynağı**: iki yeni sistem, birbirine bağlı; seppuku ve panik kontrollerine giriyor.
- **Üç davranış eğiliminin** ekranda gösterimi + yokai profilleri.
- **Miras**: ölen savaşçıdan ne devrolur, nasıl gösterilir.
- **Emeklilik / yolcu etme** akışı ve kadro tavanı yükseltme düğümü.
- GDD §4'ün "sınıf yok" gerekçesi ve §10'un "savaşçı tarafı sığ tutulur" gerekçesi **artık geçersiz** — ikisi de yeniden yazılmalı.

## Bölüm 3 — Eğitim ✔ tamamlandı

| Satır | Eski durum | Yeni karar | Not |
|---|---|---|---|
| Antrenman nasıl | Günlük tek talim 🔵 | **Aynı: tek talim seçimi** (Strikes / Guard / Footwork / Conditioning) | Kaydırıcı bir ayar, seçim bir karar. Gerçek zamanlı akışta "talim bloğu" olarak sürer. |
| Hız kolu | Soyut okul düğümleri ✅ | **Fiziksel tesisler** — dojoda görünen bina ve ekipman | Talim direği, taş, hamam gibi; dojonun büyüdüğü ekranda görülür. Soyut düğüm mantığı (kol içi sıra, peşin ödeme, geri satılmaz) korunur; değişen şey sunum ve yapım maliyeti. |
| Tavan | `FormsMaster` yükseltir ✅ | **Aynı: tesisle yükselen tavan** | Dövüşten gelen stat kazancı da aynı tavana çarpar — dojoya yatırım yapmadan savaşçı belli noktadan sonra büyüyemez. İki sistem birbirini kilitler. |
| Dövüşten öğrenme | Yok ⚪ | **Dövüş büyütür** (Domina modeli) — dövüş antrenmandan hızlı stat kazandırır | Tempo tersine döndü: artık "riske gir, güçlen". Mecburi dövüş sayacı ceza değil fırsat olur. Zafer ekranı stat kazancını gösterir. **Ölçümdeki "bağlayıcı kaynak kadro" bulgusu bu değişiklikle geçersizleşir — yeniden ölçülmeli.** |
| Zararsız eğitim maçı | Yok ⚪ | **Dojo içi talim maçı** — kendi savaşçıların birbiriyle | Gelir yok, ölüm yok; yara, stat kazancı ve moral etkisi var. Aynı çözümleyici kullanılır, yeni rakip içeriği gerekmez. Acemiyi ve sakatlanan savaşçının yeni sınıfını sınamanın güvenli yolu. |
| Otomatik eğitim | Talim seçili kalır ✅ | **Aynı** | Gerçek zamanlı akışta doğal: oyuncu başka işle uğraşırken dojo çalışır. Ayrı bir "otomatik mod" kutusu gerekmez. |

### Bölüm 3'ün getirdiği yeni işler
- **Dövüşten stat kazancı**: kazanç formülü, zafer ekranı gösterimi, tesis tavanıyla etkileşimi. Mevcut denge ölçümleri bu değişiklikle geçersiz — yeniden koşulmalı.
- **Dojo içi talim maçı**: eşleştirme ekranı, ölümsüz mod, moral ve yara sonuçları.
- **Fiziksel tesisler**: dokuz okul düğümünün görsel karşılığı; dojo ekranı artık büyüyen bir mekân.

## Bölüm 4 — Ekonomi ✔ tamamlandı

| Satır | Eski durum | Yeni karar | Not |
|---|---|---|---|
| Para | Tek: altın ✅ | **Aynı: tek para** | Her şey tek eksende ölçülür. |
| Stok kaynakları | Yiyecek / Su / İlaç ✅🔵 | **Aynı** ⏳ *kesinleşmedi* | Moral sistemi geldiği için sake (Domina'daki şarap) dördüncü stok olarak yeniden değerlendirilecek. Moral oturunca bakılır. |
| Başlangıç | 600 altın, ambar boş 🔵 | **Dolu ambarla başla** (Domina modeli) | 600 altın + yiyecek/su/ilaç. Geri sayım zaten baskı kuruyor; açılışın da boğması gerekmiyor. Kesin sayılar yeniden ölçülecek. |
| Savaşçı fiyatı / pazar | 150 altın, 10 aday ✅ | **Aynı** | Fiyat formülü artık iki ekseni birden yansıtacak (statlar + Talent), Bölüm 2'deki karara bağlı. |
| Personel | Yok 🔵 | **Personel sistemi eklenir** — her meslek hem kiralanabilir hem emekli savaşçıyla doldurulabilir | Günlük maaş + stok yer, istediğin gün kesersin. Emekli savaşçı maaş almaz. **Emekli savaşçı dövüş dışı rollerde zayıf** (talim/kata/silah ustasında kiralıktan iyi, hekim/demirci/ozanda yarı verim). 14 meslek taslağı çıkarıldı. |
| Tesis boş kalırsa | — (yeni) | **Yarı verimle çalışır** | İnşa yatırımı asla boşa gitmez; personel tam verime çıkarır. Bazı kapılar yine de personel ister (Ö-yoroi için demirci şart). |
| Zırh / onarım | Birim fiyat + onarım ✅ | **Kademeli zırh**: Deri → Lamel → Ö-yoroi | Kademe **koruma ↑ ağırlık ↑** dengesiyle çalışır (ağırlık staminayı yakar, saldırı döngüsünü uzatır — mevcut sistem). Üst kademeler **demirhane + demirci** ister, yoksa pazarda bulunmaz. Yıpranma/onarım ekonomisi korunur. |
| Günlük tüketim | Kişi başı sabit ✅ | **Aynı** | Kadro tavanı yükseltmenin gerçek bedeli bu: doğrusal artan gider. |
| Dövüş ödülü | Yalnızca altın 🔵 | **Altın + stok** (Domina modeli) | Yiyecek/su/ilaç da düşer. Ambar baskısı sefere çıkmanın ikinci sebebi olur; aç dojo sahaya çıkmak zorunda kalır. |
| "İyi dövüş" ödülü | Yok ⚪ | **Onur çarpanına gösteri girer** | Ayrı bir "kalabalık ödemesi" kalemi eklenmez; çekişmeli dövüş onuru hızlı yükseltir, onur zaten ödül çarpanı — dolaylı yoldan paraya döner. Mevcut sistem derinleşir, yeni sistem kurulmaz. |
| Kumar | Yok ⚪ | **Aynı: bahis yok** | Kumar iyi oynamayan oyuncuya kestirme sunar ve denge ölçümünü bozar. |
| Hırsızlık | Yok 🔵 | **Karşılıklı, ama olay olarak** | Biz ajan tutup çalmayız; ama onur yükseldikçe hırsızlık olayının olasılığı artar ("ünün kadar hırsız çeker"). Onur artık bir maliyet de taşır. |
| Pazarlık / indirim | Kalıcı düğüm indirimi ✅ | **Tesis kalıcı, personel çarpan** | Bina bir kez alınır, küçük indirimi kalıcı verir (×0.90); başına personel konursa tam indirim (×0.75) ama günlük maaş işler. Kriz anında personeli kesip binayı tutarsın — ekonomiye ilk kez bir **vites** girer. Emekli savaşçı maaş almadığı için uzun oyunun ödülü olur. |
| Kıtlık | Beş olay türü ✅ | ⏳ **kesinleşmedi** | Mevsimlik kıtlık (kuraklık/kış) ileride yeniden değerlendirilecek. |
| Olay sıklığı | Günde %15, hepsi eksiltir ✅ | **Olaylar karar sunsun** | Olay bir bildirim değil bir seçim olur: "aç köylüler kapıda — ver (onur +) / verme (stok korunur)". Chat oylamasına doğrudan bağlanır. Sıklık sayısı yeniden ölçülecek. |

### Terim değişikliği
"Okul düğümü" terimi emekliye ayrıldı. Bundan sonra: **Tesis** = kurulan fiziksel bina (peşin, kalıcı, geri satılmaz) · **Personel** = o binayı çalıştıran kişi (günlük maaş, kesilebilir; kiralık ya da emekli savaşçı).

### Meslek taslağı (14 rol)
Talim ustası · Kata ustası · Silah ustası · Hekim · Kırıkçı · Demirci · Kâhya · Simsar · Aracı · Ozan · Keşiş · Aşçı · Seyis · Kâhin.
İlk üçünde (talim / kata / silah ustası) emekli savaşçı kiralıktan **iyi**; kâhya / simsar / aracı / keşiş / seyis rollerinde **orta**; demirci ve ozanda emekli savaşçı çalışabilir ama **zayıf** (yarı verim).

⏳ *kesinleşmedi:* **hekim, kırıkçı, aşçı ve kâhin** rollerine emekli savaşçı hiç konamaz — bu dört meslek için **dışarıdan personel şart**. Demirci ve ozan bu kuralın dışında tutuldu (zayıf da olsa emekli savaşçı bakabilir). Karar kesinleşmedi, personel ekonomisi oturunca yeniden bakılacak.

### Bölüm 4'ün getirdiği yeni işler
- **Personel sistemi**: 14 meslek, günlük maaş, işe alma/çıkarma, emekli savaşçı yerleştirme, rol başına verim çarpanı.
- **Kademeli zırh**: üç kademe × altı yuva, demirhane kapısı, ağırlık dengesi.
- **Karar sunan olaylar**: olay başına seçenek metinleri ve sonuçları, chat oylaması bağlantısı.
- **Dövüş ödülüne stok**: ödül formülü yeniden kurulur.
- Tüm ekonomi sayıları (600 altın, 150 altın, ×0.45 ödül, %15 olay) **yeniden ölçülmeli** — dövüşün stat kazandırması ve gerçek zaman bu ölçümlerin varsayımlarını değiştirdi.

## Bölüm 5 — Dövüş ✔ tamamlandı (Rakip satırı hariç)

| Satır | Eski durum | Yeni karar | Not |
|---|---|---|---|
| Çözüm | Motordan bağımsız çekirdek 🔵 | **Aynı: motorsuz, seed'li deterministik çekirdek** ⏳ *yeniden bakılabilir* | Gerçek zaman, çekirdeğin sabit tik'inin (`TickSeconds` 0.05) görselleştirmede akıtılmasıdır: duraklama = `Step()` çağırmamak, 2x = tik başına iki adım, akıcılık = tikler arası interpolasyon. Değişken `dt` reddedildi: determinizm kare hızına bağlanır, sim ile oyun farklı sonuç verir. Motorda çözmek görsel kaliteyi vermez (onu görselleştirme verir), yalnızca öngörülemeyen fizik verir — otomatik ve chat'in etkilediği bir dövüşte bu haksızlık okunur, karşılığında ölçülebilirlik kaybedilir. **Kullanıcı bu satırı ileride yeniden açma hakkını saklı tuttu.** |
| Müdahale | Yok ✅ | **Aynı: tam otomatik** | Elle sürme de, dövüş içi emir de yok. Oyuncunun tüm kontrolü hazırlıkta (kadro, teçhizat, talim); dövüşteki tek kararı çekilme. |
| Tek müdahale | Tek tuş "Kaç" ✅ | **Aynı** | Koşullu açılan tek tuş, onur bedelli. QTE yok — beceri sınavı değil, karar. |
| Teslim eşiği | Otomatik eşik yok ✅ | **Aynı: otomatik teslim yok** | Savaşçı ölene kadar dövüşür; çekilme kararı oyuncunundur. İrade statı bu satıra bağlanmadı. |
| Teslim hakkı | Çekilme her zaman açık ⚪ | **Aynı: her sözleşmede açık** | `Surrender Allowed` gibi bir sözleşme alanı gelmez. Kural tek, bedel hep aynı: onur. |
| Süre sınırı | `BattleOutcome.TimeLimit` ✅ | **Süre sınırı kalkar** | Sayaç yok; dövüş biri düşene, çekilene ya da teslim olana kadar sürer. Uzun dövüşü yöneten şey artık duraklama/hızlandırma. |
| Blok | Savunma statından türeyen karar ✅ | **Stat + ekipman iki eksene ayrılır** | Savunma statı bloğun **sıklığını** verir (`MaxBlockChance` 0.45, taban yok — kilitli sayı korunur); silah bloğun **kalitesini**; zırh blok tutmayınca **ne kadarının emildiğini**. Ele takılan kalkan **yok** — samurayda iki el silaha gider, savunma zırha yazılıdır. Bunun yerine **ō-sode** omuz yuvasına girer: blok şansı vermez (pasif parça, hamle değil), etkisini büyütür, bedeli ağırlık. Kademeli zırh kararına (Deri → Lamel → Ō-yoroi) doğal oturur. Gerçek kalkan (**tate**) sahaya girer, kola değil: "siperli mevzi" saha özelliğinde mermiye karşı korur, yakın dövüşte işe yaramaz. Bedeli kabul edildi: kalkan bir bakışta okunur, ō-sode değil — silueti abartarak kapatılacak. |
| Hücum | Mesafe 320 / 0.40 / 0.75 sn ✅ | **Sayılar aynı, hücum görünür olur** | Birikme sırasında savaşçı ekranda işaretlenir; izleyici geleceği önceden görür. Chat için gerilim anı, çözümleyici için değişiklik yok. |
| Silah düşürme | Zırha vuruşta 0.05 taban, herkeste ✅ | **Taban herkeste kalır, uzmanlık büyütür** | Sınıf, Yol ya da silah türü (jitte/sai) şansı yükseltir; kimse sıfırlanmaz. Domina'nın "beceri yoksa hiç düşüremezsin" modeli reddedildi. Ölçülmüş bulgu korunur: kuralın bedelini düşme **yönü** belirliyor (rakibin arkası), mesafe değil. |
| Silah yakalama | Jitte/sai kilitleme ✅ (bizde fazladan) | **Aynı, sayılar kilitli** | Açık kalan iki ölçüm Faz 9'a: kilidin **takıma** değeri ve sayıca azken sai'nin yüksek yakalama hacminin karşılığı. Blok geldiğinden jitte'nin üstünlüğü 2.98 → 0.42 puana inmişti; o da yeniden ölçülecek. |
| Zehir | Doz/tik/ömür kurallarıyla ✅ (bizde fazladan) | **Aynı** | Herkese açık, onur bedeli yok, sözleşme yasağı yok. Ölçülmüş bulgu korunur: asıl ayar doz **tavanı**, ömrü değil. |
| Sersemletme | Künt silahların karşılığı ✅ (bizde fazladan) | **Aynı** | Künt silahın kimliği bloğu delmesi (`BlockStunShare` 0.75). Ō-sode bu dengeyi değiştirmez: zırh hasarı emer, sarsıntıyı emmez. |
| Mermi | `ThrownWeapon` ayrı yuvada, herkeste ✅ | **Atılan silah aynı + `yumi` (yay) bir sınıf olur** | Şuriken/tanto herkeste açık kalır. Yay **iki elli** silah olarak girer ve bir sınıfa bağlanır: menzilde üstün, yakında çaresiz. Boş el yuvasının kararını büyütür (yay = iki el), ō-sode'ye oka karşı gerçek bir iş verir, temaya tam oturur (samurayda yay birincil silahtı). |
| Zırh | Altı yuva, ağırlık, yıpranma, dağılma ✅ | **Mekanik aynı; ekrana `A / D / kg` gelir** | Domina'dan alınan tek şey gösterim: her parçada zırh değeri / dayanıklılık / ağırlık açıkça yazılır, oyuncu takası görerek yapar. Toplam ağırlığın saldırı döngüsüne etkisi de aynı ekranda okunur. |
| Uzuv kopması | Yaşayıp sakat kalma 🔵 | **Aynı** | Kopma bir beceri değil, herkeste açık; ölüm değil kader değiştiren olay. Blok bunu sıfırlar (`BlockDismembermentShare` 0) — Savunma statının verdiği tek kesin söz. Bölüm 2'nin uzuv-sınıf uygunluk matrisi buraya bağlı. |
| Sahadaki engel | Yok ⚪ | **Saha özelliği eklenir (canlı üçüncü taraf değil)** | Sis (isabet/menzil düşer), çamur (hız/kaçınma düşer, ağır zırh ekstra ceza), dar köprü (sayı üstünlüğü işlemez), gece (mermi zayıf), siperli mevzi (tate panoları). Sözleşme kartında yazılı gelir; mevcut çözümleyiciye çarpan olarak girer, yeni yapay zekâ gerekmez. |
| Rakip | Yokai (kappa, kitsune, tengu, oni, jorōgumo) 🔵 | ⏳ **kesinleşmedi** | Düşman havuzunun ne olacağı (yalnız yokai / yokai + insan / ağırlık insanda) karara bağlanmadı. Bestiary (#3) ile birlikte bakılacak. |

### Bölüm 5'in getirdiği yeni işler
- **Ō-sode ve blok ekseninin ayrılması**: `BlockDamageReduction`'ın zırh parçasından ölçeklenmesi, omuz yuvasının blokla ilişkisi, ağırlık dengesi.
- **`yumi` sınıfı**: iki elli menzilli silah, uçuş/menzil davranışı, yakın mesafede ceza, ok cephanesi ekonomisi.
- **Saha özellikleri**: beş saha türü, sözleşme kartında gösterimi, çözümleyiciye çarpan girişi.
- **Süre sınırının kaldırılması**: `BattleOutcome.TimeLimit` yolunun sökülmesi ve buna dayanan testlerin gözden geçirilmesi.
- **Görünür hücum ve `A/D/kg` zırh paneli**: sunum işi, çekirdeğe dokunmaz.
- Faz 9'a devreden ölçümler: takımda kilit değeri, sayıca azken sai, blok sonrası jitte/katana farkı.

## Bölüm 6 — Dövüş türleri ✔ tamamlandı

| Satır | Eski durum | Yeni karar | Not |
|---|---|---|---|
| Planlanmış dövüş | "Günün teklifi" — al ya da bırak ✅ | **Süreli teklif kuyruğu** | Tek günlük teklif yerine aynı anda birkaç teklif asılı durur, her birinin kendi son kullanma süresi vardır. Sefere çıkmak gerçek zamandan süre yer (yol + dövüş). Karar "hangisini alayım" değil, **"hangisine yetişirim"** olur — ayrık gün modeli kalktığı için teklifin de günlük olması anlamsızlaşmıştı. |
| Pit fight | Yok ⚪ | **Kör dövüş girer, bahis girmez** | Rakibin bilinmediği yüksek ödüllü sözleşme türü: hangi teçhizatı götüreceğini bilmeden karar verirsin. Bölüm 4'ün "kumar yok" kararı korunur — belirsizlik bir risk kararı olur, kestirme para değil. |
| Exhibition | Yok ⚪ | **Yalnızca dojo içi talim maçı** (Bölüm 3'te kabul edildi) | Dışarıya karşı ölümsüz gösteri dövüşü **açılmaz**. Riskin eşiği net kalır: dojonun içi güvenli, dışarı çıkan her dövüş ölümcül. Domina'nın exhibition'ı zaten "kasten kaybet" sömürüsünün kapısıydı. |
| Bölge şampiyonları | Kelle avı sözleşmeleri ✅🔵 | **Sabit adlı hikâye hedefleri** + kelle avı çerçevesi | Ana hikâye karakterleri **her oyunda aynı**: kimlik ve statlar sabit, ezberlenip hazırlanılabilir (Domina'daki gibi). Sıradan kelle avları arada üretilmeye devam eder. İleride yazılacak basit hikâye bu omurgaya oturur. |
| Yenilen hedef | — (yeni) | **Güçlenmez, büyür** | Ara hikâye dövüşünü kaybedersen hedefin statları **değişmez** (ezber korunur); yanına adam katılır — yenilen oni bir sürü toplar (yenilgi 1: +2 kappa, yenilgi 2: +4). Zorluk artar ama karşı hamle açık kalır: kalabalığa karşı kadro, saha ve teçhizat seçimi. Stat çarpanı reddedildi çünkü tavansız çarpan hedefi erişilemez yapıp koşuyu sessizce bitirir. |
| Gece baskını | — (yeni) | **Yenilgi tetikler** | Hikâye dövüşünü kaybetmek dojoyu hedef haline getirir: yenilen yokai birkaç gün içinde gece baskını yapabilir. Hazırlıksızsan ambar yağmalanır, revirdeki yaralılar ölür; hazırsan (nöbetçi savaşçı + duvar tesisi) dövüş başlar — ama kadro yorgun ve teçhizat yarımdır. Yenilgi dışarıda kalmaz, eve gelir. Mevcut kararlarla örtüşür: duvar/kapı bir tesis kolu, nöbetçi bir personel rolü, "nöbetçi koy / koyma" karar sunan bir olay. |
| Chariot race / Beast mode / Gravitas | Yok 🔵 | **Aynı: özel etkinlik yok** | Ayrı kural seti gerektiren mini oyunlar açılmaz. Çeşitlilik sözleşme türlerinden ve saha özelliklerinden gelir; her şey aynı çözümleyiciyi kullanır. |
| Final şampiyonası | Yok ⚪ | **Var — ve kaybetmek oyunu bitirir** | Kampanyanın sonu tek bir final dövüşüdür. **Final kaybı = game over**, kesin. Domina'dan ayrıldığımız yer burası: orada final kaybı yılın bitmesi (kayıp koşulu resmî değil, "en iyi gladyatörünü kaybedersen pratikte bitti" **[T]**), bizde koşunun bitmesi. Kaybetmek gerçek olmalı — oyun oyuncuyu kazanmaya taşımaz. |

### Bölüm 6'nın getirdiği yeni işler
- **Teklif kuyruğu**: aynı anda birden çok teklif, teklif başına süre, sefer süresinin gerçek zamandan düşmesi.
- **Kör sözleşme**: rakibin gizlendiği teklif türü ve ödül çarpanı.
- **Sabit hikâye hedefleri**: adlı yokai tanımları (kimlik + sabit stat), kelle avı sistemine bağlanması, hikâye ilerleme durumu.
- **Yenilgi sonrası büyüme**: hedefe eşlik eden sürünün yenilgi sayısına göre kurulması.
- **Gece baskını**: tetikleme kuralı, dojo savunması (nöbetçi personel + duvar tesisi), hazırlıksız kayıp tablosu (ambar, revir), baskın dövüşünün yorgun/yarım teçhizatlı başlaması.
- **Final dövüşü ve game over**: koşunun bitiş ekranı, kayıt akışının sonlanması.

## Bölüm 7 — Yönetim katmanı ✔ tamamlandı

| Satır | Eski durum | Yeni karar | Not |
|---|---|---|---|
| Yapı | Okul ağacı: 9 düğüm, üç kol 🔵 | **Tesis ağacı + personel; bazı mesleklerin kendi yükseltme kolu olur** | Üç kol × üç kademe tesis ağacı kalır (Bölüm 4'ün terim ayrımıyla: tesis = bina, personel = onu çalıştıran kişi). Domina'nın "her personelin kendi araştırma ağacı" modeli **bütünüyle** alınmaz — 14 ayrı ağaç ekonomi değil, tablo doldurmadır. Ama bir kısmı alınır: seçilecek birkaç meslek 2-3 kademelik kendi yükseltmesini taşır. **Hangi meslekler olacağı ayrı bir turda, 14 rol tek tek karara bağlanacak.** |
| Slot kısıtı | Düğüm alınınca kalıcı, slot yok 🔵 | **Sabit slot sayısı yok** | Tesis kalıcı ve geri satılmaz (GDD kararı korunur). Personelde doğal tavan = tesis sayısı, bina başına bir kişi; gerçek kısıt **günlük maaş**. Domina'nın "aynı anda 3/6 personel" sayısı yapay bir tavan; bizde kısıtı ekonominin kendisi koyar ve kriz anında personeli kesmek zaten Bölüm 4'te açılan vitestir. Kesilen personelin bonusu gider, binası kalır — Domina'daki Architect kuralıyla aynı yer. |
| Maliyet | Düğüm yalnızca altın 🔵 | **Tesis peşin altın; personel günlük maaş + kişi başı stok** | Araştırma/tur maliyeti ve ikinci kaynak (Domina'daki taş) girmez — tek para kararı korunur. Personel de yiyecek/su tüketir: kadro tavanı gibi, yönetimi büyütmenin bedeli de doğrusal artan gider olur. |
| Bina | Soyut düğümler 🔵 | **Fiziksel bina, sabit yerleşim** | Açılan tesis dojo ekranında görünür bina olur. Serbest yerleştirme / komşuluk bonusu **yok** — plan kurma oyunu açmıyoruz. Gece baskını kararı zaten duvar ve kapıyı fiziksel yapmıştı; tesislerin geri kalanının soyut kalması tutarsız olurdu. Sunum işi, çözümleyiciye dokunmaz. |
| İnşa süresi | Anında 🔵 | **Süre yer; personel meşgul olmaz** | Domina'nın kum saati alınır: altın ödendikten sonra tesis belli bir sürede kalkar, o süre sefere çıkma kararıyla yarışır. Domina'nın "o işi yapan personel o sürede başka iş yapamaz" kısmı **alınmaz** — personel bizde tesisi çalıştırmak için var, takip edilecek meşguliyet durumu eklemeye değmez. Ayrıca altınla hızlandırma yok. |
| NPC ilişkisi | Yok ⚪ | **Üç NPC ile başlanır; ilişki var, entrika yok** ⏳ *rakip dojo ve köy sonraya* | **Bölge beyi** (teklif kuyruğunun sahibi), **tüccar loncası** (pazar fiyatı, üst kademe zırhın satışta olması, kıtlıkta stok), **tapınak** (omamori arzı, cenaze töreniyle onur telafisi). Her biri için tek sayı, beş kademe: Düşman / Soğuk / Nötr / Hoşnut / Sadık; kayda yalnızca bu sayı yazılır. **Yükselten:** sözleşmeyi süresinde bitirmek (büyük), hediye (küçük ve azalan verimli — sadıklık işle kazanılır), olayda o tarafın lehine karar, onurun yükselmesi (hepsine birden, çok küçük). **Düşüren:** aldığı sözleşmede kaybetmek ya da çekilmek, teklifini süresi dolana kadar hiç almamak (birikir), aleyhine karar. **Alınmayan:** sır satma, şantaj, dövüş sonucu ayarlatma — otomatik dövüşün adaleti okunur kalmalı. Rakip dojo ve köy (ve ilişkilerin birbirini bozduğu karşı kutup modeli) şimdilik dışarıda; ekonomi ve personel oturunca bakılır. |
| Patronaj | Yok ⚪ | **Girmez** 🔵 | NPC bir savaşçının giderini üstlenmez. Domina'da bu, kadroyu iki kişiye düşürüp iki NPC'ye de patron oldurmakla sömürülüyordu; bizde günlük tüketim baskısı ekonominin omurgası (Bölüm 4) ve o baskıyı seyrelten bir muafiyet açılmayacak. |
| Kart sistemi | Yok 🔵 | **Omamori olarak girer** | Jupiter kutsamaları tema uyarlamasıyla alınır: **omamori (tapınak takısı)** hem savaşçıya hem personele takılır, sökülüp başkasına verilir, satılabilir. Tapınak ilişkisi arzını ve gücünü belirler — takı sistemi NPC katmanının karşılığıdır, başıboş bir ekonomi kalemi değil. Taşınabilirliğin sömürüye açık olduğu biliniyor (sefer öncesi hepsini bir savaşçıda toplamak); denge turunda ölçülecek. |

### Bölüm 7'nin getirdiği yeni işler
- **Meslek turu**: 14 rolün tek tek kararı — her rol ne verir, kendi yükseltme kolu var mı, emekli savaşçı verimi ne. Bölüm 4'ün açık bıraktığı "hekim/kırıkçı/aşçı/kâhin'e emekli savaşçı konamaz" satırı da burada kapanır.
- **İnşa süresi**: tesis başına süre, sürerken yarım çalışmama kuralı, ekranda kalan süre gösterimi.
- **Fiziksel dojo ekranı**: sabit yerleşimde tesis binalarının açılması, duvar/kapının bu ekranda görünmesi.
- **Personel stok tüketimi**: günlük tüketim formülüne personelin girmesi.
- **NPC ilişki sistemi**: üç taraf, beş kademe, yükselten/düşüren eylem tablosu, kademe başına etki (teklif kuyruğu kalitesi, pazar fiyatı ve stok, omamori arzı), kayda yazılması.
- **Omamori**: takı tanımları, savaşçı ve personel yuvası, taşınma/satış akışı, tapınak arzına bağlanması, denge ölçümü.