# Referans oyun: Domina — ekran ve arayüz dökümü

`REFERENCE-DOMINA.md` **sistemleri** anlatır; bu dosya **ekranları**: hangi bilgi nerede
duruyor, hangi kontrol neye benziyor, oyuncu bir kararı hangi kutuya bakarak veriyor.

Kaynak: "Domina Beginners Guide To Starting Right PLUS Tips & Tricks (2018 Edition)"
videosundan 84 kare (1280×720). Her şey **görüldüğü kadarıyla** yazıldı; okunamayan yerler
"belirsiz" diye geçiyor. Oyun 2018 sürümü.

> **Neden var:** bizde dört ekran var (gün, kadro, pazar, okul) ve hepsi düz `VBoxContainer`
> listesi. Referansın arayüzü **yirmi yaşında bir tür**ün yerleşik çözümlerini taşıyor;
> hangisini alacağımıza bakmadan önce ne olduğunu görmek gerekiyor.

---

## 0. Sürekli duran üst çubuk

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Coin: 935 ●  Water: 400 ◆  Food: 800 ◆  Wine: 80 ◆     Next Battle: 3     │
│                                                         Days Left: 364    │
│                                                                     [II]  │
└──────────────────────────────────────────────────────────────────────────┘
```

- Solda dört kaynak: **etiket + sayı + küçük renkli ikon**. Bar yok, yüzde yok, hep tam
  sayı. Hiçbir panel bu çubuğun üstüne binmiyor.
- Sağda iki satır: `Next Battle` (mecburi dövüşe kalan gün) ve `Days Left` (yılın sonu).
- En sağda küçük bir simge oyunun akıp akmadığını gösteriyor; duraklatınca ekranın
  ortasında büyük **PAUSED** yazısı beliriyor **ve bütün dünya mavimsi-gri bir filtreye
  giriyor** — yani "duraklatıldı" bilgisi tek bir yazıya değil, ekranın rengine bağlı.

**Eksik olan:** hiçbir kaynağın **eğilimi** yok. "800 yiyecek" yazıyor ama günde kaç
eriyor, kaç geliyor yazmıyor; onu görmek için Architect panelini açman gerekiyor. Kıtlık
oyunun ana baskısıyken bu bilgi HUD'da yok.

## 1. Ana ekran: avlu

```
┌──────────────────────────────────────────────────────────────────────────┐
│ [üst çubuk]                                                               │
│  ▯▯▯▯▯▯▯ revak / bina cephesi — personel burada yürüyor                  │
│                                                                           │
│            açık kum avlusu — gladyatörler burada talim ediyor            │
│            (birimin altında ince yeşil bar + ad etiketi)                 │
│            üstünde metin: "Granius is Strength Training"                 │
│                                                                           │
│  ─── parmaklık ───                                                        │
│  NPC'ler (Legate kırmızı cübbe, rahibe beyaz) ve eşya nesneleri:         │
│  örs, evrak masası, kuyu, "Map of Games" tabelası                        │
└──────────────────────────────────────────────────────────────────────────┘
```

- Ekran bir **menü değil, diyorama**: sabit kamera, canlı bir avlu. Bütün paneller bunun
  **üstüne** açılıyor; hiçbiri tam ekran değil, arkada dünya görünmeye ve oynamaya devam
  ediyor.
- **Dünyanın kendisi menü:** personeli ve NPC'yi açmak için figürüne tıklıyorsun, pazarı
  açmak için masaya. Ayrı bir gezinme çubuğu **yok**.
- Birimin durumu tek bir birleşik "birim kartı" ile anlatılıyor: **portre + ad + renkli can
  barı**. Aynı kart avluda, dövüş HUD'ında ve dövüş sözleşmesinde birebir aynı.
- Köleler **çıplak ve yeşil şortlu**; gladyatörler zırhlı. Kim ne, etiket okumadan
  anlaşılıyor.
- Bir birimin ne yaptığı **üstünde yazan düz metinle** anlatılıyor ("X is Strength
  Training"). İkon yok, ilerleme çubuğu yok.

## 2. Panellerin ortak iskeleti

Bütün personel panelleri **aynı** kalıp:

```
┌──────────────────────────────────────────┐
│ <Ad> Info                                 │
│  [Düğme]        [Düğme]                   │
│  [Düğme]        [Düğme]                   │
│  [Düğme]        [Düğme]                   │
│                                            │
│ [Fire Employee]              [ Close ]     │
└──────────────────────────────────────────┘
```

- Bordo, benekli parşömen zemin; başlık sol üstte; **iki (bazen üç) sütunlu düz metin
  düğmesi ızgarası**; sol altta **Fire Employee**, sağ altta **Close** (kırmızı odak
  çerçevesiyle).
- **İkon yok.** Architect paneli, Bard paneli ve rahip paneli — başlığı okumazsan
  birbirinden ayırt edilemiyor. Tutarlılık kazanılmış, **tanınabilirlik kaybedilmiş**.
- Bir araştırma sürerken **bütün ızgara soluklaşıyor** — tek tek değil, panel bütün olarak
  kilitleniyor.

### Fare üstüne gelince: iki parçalı bilgi

- Düğmenin **üstünde** maliyet rozeti: `-30 altın · -30 su · -15 taş` gibi.
- Düğmenin **altında** ipucu kutusu: bir cümlelik açıklama.
- **Çakışma önleme yok:** rozet üstteki düğmenin yazısını kesiyor, ipucu alttaki düğmeleri
  örtüyor. Bu, bütün oyunun en sık tekrar eden arayüz kusuru.

## 3. Gladyatör paneli (en yoğun ekran)

```
┌────────────────────────────────────────────────────────────────────────┐
│ Gladiator Info                                                          │
│ Vettius of Melitensium      Weight: 91kg    ┌─PRIMARY──────┐           │
│ Temperament  -[===|===]+    Total: 124kg    │ Basic Pugio  │  ┌──────┐ │
│              "Satisfied"                     │ A:+7 D:+7 1kg│  │ tam  │ │
│ Health [======yeşil======] [Heal]           │ [==bar==]    │  │ boy  │ │
│        145/145                               └──────────────┘  │portre│ │
│                                              ┌─SECONDARY────┐  │      │ │
│ Training Balance   Level  Points             │ (varsa)      │  │THRAEX│ │
│  Agility  ─●────    13     62                └──────────────┘  └──────┘ │
│  Weapon   ─●────    14     34                                            │
│  Defense  ─●────    13     49                                            │
│  Strength ─●────    10   145HP MAX                                       │
│  Meditate ●─────   100     [Train]                                       │
│  Aggro:79  Turtle:21  Evasive:56  Stamina:50                            │
│  Victories: 3, Losses: 1                                                 │
│ [Reward Wine 1▲▼][Reward Coin 1▲▼][Award Private Room (kapalı)]        │
│              [Put to Death][Grant Freedom][Sell] [<][>] [Close]         │
└────────────────────────────────────────────────────────────────────────┘
```

Aynı anda ekranda **25-30 ayrı sayı** var. Öğrenilecek üç şey:

1. **Eğitim iki katmanlı gösteriliyor:** her stat için hem bir **kaydırıcı** (vaktin ne
   kadarını yiyor) hem **Level** hem **Points**. Yani oyuncu "neyi eğitiyorum" ile "ne
   kadar ilerledim"i aynı satırda görüyor.
2. **Türetilmiş sayılar açıkta:** Aggro / Turtle / Evasive / Stamina düz sayı olarak
   yazıyor. Davranış gizli değil.
3. **`<` `>` okları var** — panel kapanmadan kadroda gezilebiliyor. Kadro ekranı ile
   savaşçı ekranı **aynı ekran**.

**Kusurlar:** `Put to Death` — geri dönüşü olmayan bir eylem — `Sell` ve `Close` ile
**birebir aynı** görünüyor; hiçbir yerde kırmızı "tehlike" rengi yok. `Award Private Room`
kapalı ama **neden kapalı olduğu yazmıyor** (Architect'in özel oda binasını gerektiriyor).
Beş kaydırıcı için **tek** `Train` düğmesi var, hangisini eğittiği belirsiz.

## 4. Doctore: beceri ağacı

Tek "tam genişlik" panel. ~30 düğüm, iki küme, aralarında **dikey bağlantı çizgileri** (ön
koşul). Altta sol köşede `Fire Employee`, sağda **`Enable Automatic Gladiator Training`
onay kutusu** ve `Close`.

- Sıradan bir düğüm ~20-70 altın; **sınıf açan düğüm 400-500 altın ve 16-17 tur**. Ama
  **düğmenin boyu ve yazı tipi aynı** — "bu büyük bir karar" bilgisi yalnızca sayıda.
- Alınmış / alınabilir / kilitli düğümler **neredeyse aynı soluklukta** görünüyor. Ağaç
  "nerede kaldım" sorusuna bakışta cevap vermiyor.
- İpucu kutusu büyük olduğunda arkasındaki düğümleri tamamen örtüyor.

## 5. Pazar

```
┌───────────────────────────────────┐
│ City Market                        │
│  [maliyet önizleme şeridi]         │
│ ┌───────┐ ┌───────┐ ┌───────┐      │
│ │x10 🍎 │ │ x0 🍯 │ │ x5 ◆ │      │
│ │Buy Food│ │Buy Wine│ │Buy Water│  │
│ │Buy All │ │Buy All │ │Buy All │   │
│ └───────┘ └───────┘ └───────┘      │
│              Sell                   │
│ [Sell Food][Sell Wine][Sell Water]  │  (üçü de kapalı)
│ [Attend Pit Fight] [Hire Employees] │
│                          [ Close ]  │
└───────────────────────────────────┘
```

- Pazar aynı zamanda bir **kavşak**: pit dövüşü ve personel alımı buradan açılıyor.
- Stok, ürünün üstünde küçük bir **rozet** (`x10`). Fiyat düğmede **yazmıyor** — üstteki
  önizleme şeridinde çıkıyor.
- Alma hücrelerinde ikon var, satma hücrelerinde yok; iki sıra birbirine benzemiyor.

## 6. Legate / Magistrate paneli

```
┌───────────────────────────────────────┐
│ Legate Germanicus Terentius            │
│ Temperament  -[====|===]+  "Satisfied" │
│ Bribery: [2 ▲▼]        [ Send Wine ]   │
│ [ Suggest Gladiator Patronage ]        │
│ [ Purchase Gladiators ]                │
│ [ Arrange Exhibition Match ]  (kapalı) │
│ [ Sell Secret <Magistrate> ]  (kapalı) │
│ [ Blackmail <Legate Secret> ] (kapalı) │
│                              [ Close ] │
└───────────────────────────────────────┘
```

- Başlık **rolün değil kişinin adı** — personel panelleri "X Info" derken NPC paneli
  kişiselleşiyor.
- **Temperament çubuğu gladyatör panelindekiyle aynı widget.** Yani "moral" ve "ilişki"
  oyunda tek bir kavram olarak gösteriliyor. Bu, arayüzün en zarif kararı.
- Sayaç + düğme ikilisi (`Bribery: 2 ▲▼` + `Send Wine`) oyunun en anlaşılır kontrolü.
- Üç seçenek kapalı ve **hiçbiri neden kapalı olduğunu söylemiyor**.

## 7. Dövüş sözleşmesi

```
┌──────────────────────────────────────────────────────────────────────┐
│ Arena Battle   Host: The Emperor        Game Type: Championship       │
│ Victory Reward: 131● 166🍎 155◆ 22🍯 2 Slaves   "A battle to the      │
│ Participation Cost: 11● 2🍎 1◆ 6 gün             death against Ancus" │
│ Surrender Allowed: No                   Obstacles: Tigers             │
│                                                                        │
│ Pick Your Gladiators                    Opponent Gladiators           │
│ (Mind Control Not Researched)           ┌──────────────┐              │
│ ┌────────────────────┐        vs        │ portre  AI   │              │
│ │  (seçim kutusu)     │                  │ Ancus...     │              │
│ └────────────────────┘                  │ [can barı]   │              │
│ [Pick Gladiators]  Selected/MAX: 0/3                                  │
│                        [Reject Terms]        [Accept Terms]           │
└──────────────────────────────────────────────────────────────────────┘
```

- Simetrik: solda **senin maliyetin**, sağda **rakip ve şartlar**, ortada "vs".
- Oyunun **en ikonlu** ekranı: ödül ve maliyet satırları ikon+sayı, çünkü bu satırlar sık
  tekrar ediyor.
- `Selected/MAX: 0/3` canlı sayaç ve kimse seçilmeden kapalı duran `Accept Terms` —
  oyunun en temiz kapı mantığı.
- `(Mind Control Not Researched)` parantezi: kilit **kararın önünde**, ama nereden
  açılacağı yazmıyor.

## 8. Dövüş ekranı

```
┌──────────────────────────────────────────────────────────────┐
│                            [Tullus 141/141][Vettius 24/145]   │
│                                              2:39             │
│         toz + kan parçacıkları; savaşçılar bulutun içinde    │
│                                                                │
│   ekranın alt %40'ı: kalabalık                                │
│                                        [Ancus 106/550]        │
└──────────────────────────────────────────────────────────────┘
```

- Üstte senin savaşçıların (portre + can barı + **sayı**), altında **geri sayan süre**;
  sağ altta rakip.
- Yetenek tuşu, bekleme süresi, komut çubuğu **yok**. İzleniyor.
- **Okunabilirlik zayıf:** savaşçılar toz efektinin içinde kayboluyor; kimin kazandığı
  ancak köşedeki barlardan anlaşılıyor. Arena "bilgi" değil "atmosfer" veriyor.

## 9. Zafer ekranı ve kartlar

- **VICTORY** başlığı; altında her savaşçı için **"AI Training MAX"** kartı ve dövüşten
  kazanılan eğitim (`Agility +13 · Weapon +12 · Strength +8 · Defense +4`).
- Ödüller ikon+sayı olarak sıralanıyor; **köleler portre olarak**, kartlar kart olarak,
  ve ayrı bir kalem: **`73` Crowd Favour**.
- **Kart eli** ekranın altında duruyor: krem/parşömen, altın köşe süslü kartlar — oyunun
  geri kalanının bordo panellerinden **bilerek ayrılan** tek görsel dil. Kartta **maliyet
  yok** (bunlar satın alınmıyor, veriliyor).
- Kart **sürüklenip** sol üstteki küçük portre yuvalarına (Doctore, Educator, gladyatör)
  bırakılıyor. Altta **SORT** ve **DISCARD**.
- Kusur: sürükleme hedefleri çok küçük ve hareketli kalabalığın üstünde duruyor.

## 10. Tören ekranları

- `PREPARE FOR BATTLE` ve `SELECT GLADIATOR CLASS` — **tek** yerde büyük, gösterişli yazı
  tipi kullanılıyor. Geri kalan her yer aynı küçük piksel yazı tipi. Gösteri yazısı
  **kıtlıkla** harcanıyor; bu yüzden işe yarıyor.
- Patron bildirimi tuşsuz: sadece metin ve "Press any key".

---

## Bizim dört ekranımız için çıkarımlar

Referansın işe yarayan kararları:

1. **Tek birim kartı, her yerde aynı.** Portre + ad + can barı; avluda, dövüşte,
   sözleşmede. Bizde savaşçı üç ekranda üç farklı satır biçiminde yazılıyor — birleştirmeye
   değer.
2. **Aynı widget iki kavram için.** Temperament çubuğu hem gladyatörün moralini hem
   NPC ilişkisini gösteriyor. Bizde onur ve (gelecekteki) chat ilişkisi aynı görsel dili
   paylaşabilir.
3. **Kararın maliyeti kararın yanında.** Yükseltme ekranı yükseltmenin **ve** düşürmenin
   fiyatını aynı anda gösteriyor; sözleşme ekranı ödülü ve gün bedelini yan yana koyuyor.
   Bizim gün ekranımız ödülü yazıyor ama **bedeli** yazmıyor.
4. **Canlı sayaç + kapalı onay tuşu** (`Selected/MAX: 2/3`, kapalı `Accept Terms`). Bizde
   zaten var (`PartyVerdict`), ama sayacı ekranda göstermiyoruz.
5. **Kadroda panel içinden gezinme** (`<` `>`). Bizde her savaşçı için listeye dönmek
   gerekiyor.

Kopyalanmayacaklar:

1. **İkonsuz, birbirinin aynı paneller.** Dört ekranımız zaten birbirine benziyor; referans
   bunun sonunu gösteriyor.
2. **Tehlikeli eylemin sıradan görünmesi.** `Put to Death` ile `Close` aynı düğme. Bizde
   seppuku ve azat benzeri kararlar gelirse ayrışmalı.
3. **Sebepsiz kapalı seçenek.** Kapalı tuş neden kapalı olduğunu söylemeli — bizim
   `RefusalText` bunu doğru yapıyor, korunmalı.
4. **Çakışan ipucu ve maliyet rozetleri.**
5. **Eğilimi olmayan kaynak göstergesi.** Bizim gün ekranı da bugün sadece stok yazıyor;
   "günlük tüketim" satırı eklenmeli.
6. **Bilgi vermeyen dövüş görüntüsü.** Bizim arena da izleniyor; toz altında kaybolan
   dövüş yerine kimin kazandığı okunabilir kalmalı.

---

*Kaynak kareler: `scratchpad/domina-ref/frames` ve `.../ui` (oturumluk; repoya girmez).*
