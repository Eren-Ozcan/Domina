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
