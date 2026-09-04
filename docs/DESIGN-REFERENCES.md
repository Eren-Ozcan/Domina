# Tasarım Dayanakları

Bu dosya, `docs/GDD.md`'deki kararların **dışarıdan doğrulanabilir** dayanaklarını tutar:
hangi karar hangi yerleşik tasarım pratiğine yaslanıyor, nerede o pratikten ayrılıyoruz ve
ayrılmanın gerekçesi ne.

**Neden ayrı dosya:** GDD kararı söyler, burası kararın *neden savunulabilir* olduğunu.
İkisini karıştırmak GDD'yi okunmaz yapar.

**Uyarı — kaynakların ağırlığı eşit değil.** Aşağıda üç tür kaynak var: (1) tasarımcıların
kendi konuşma/yazıları, (2) hakemli olmayan ama yerleşik sektör yazısı, (3) topluluk
wiki'leri ve forum ölçümleri. Üçüncüsü bir oyunun *ne yaptığını* gösterir, *neden* yaptığını
değil — sayıları oradan almayız, yalnızca kalıbı okuruz.

---

## 1. Hücumun tetiği: fırsat değerlendirmesi, sabit eşik değil

**Bizim kuralımız (GDD §4):** savaşçı sabit bir mesafeye bakmaz; her düşman için "bana
vurabilir hale gelmesi ne kadar sürer" hesabını yapar ve birikmesini tamamlayacak boşluk
varsa hücumu düşünür.

**Dayanak — fayda tabanlı (utility) yapay zekâ.** Dave Mark'ın GDC AI Summit'te anlattığı
yaklaşımın çekirdeği tam bu: ajan sabit eşiklere/ağaçlara değil, **o anki duruma göre
puanlanan seçeneklere** bakar ve en iyisini seçer. Yöntemin tasarımcı açısından asıl değeri,
kuralın doğal dilde ifade edilebilmesi — "ateş altındaysan önce siper ara" gibi. Bizimki de
öyle okunuyor: *"kimse bana vuramıyorsa ve toparlanacak vaktim varsa hücumu dene."*

**Nerede ayrılıyoruz:** IAUS sürekli bir puan üretir; bizimki **evet/hayır bir uygunluk
kapısı** + Saldırganlıkla ölçeklenen bir zar. Yani fayda sisteminin tamamı değil, "durumu
sorgula" ilkesi alınmış. Puanlamaya geçmek için sebep yok: hücumun rakibi yok, tek soru
yapılıp yapılmayacağı.

**Ne doğrulamıyor:** eşiğin *sayısını* bu kaynak vermiyor. Bizim sayımız ölçümden geliyor
(GDD §4 tablosu) ve zaten formülden türüyor.

> [Architecture Tricks: Managing Behaviors in Time, Space, and Depth (GDC 2013)](https://www.gdcvault.com/play/1018040/Architecture-Tricks-Managing-Behaviors-in) ·
> [IAUS — Intrinsic Algorithm](https://www.gameai.com/iaus.php) ·
> [Utility system (genel bakış)](https://en.wikipedia.org/wiki/Utility_system)

---

## 2. Birikme süresi: 0.75 sn okunabilir mi

**Bizim kuralımız:** hücum 0.75 sn yerinde birikir; bu sürede savaşçı yerinden kıpırdamaz
ve yediği ilk isabetle hamle dağılır (savunması normal oranıyla sürer — bkz. §3).

**Dayanak — insan tepki süresi.** Basit görsel uyarana tepki **200-300 ms** bandında; 60
fps'de 12-18 kare. Yaygın tasarım kılavuzları pratikte **~0.25 sn**'lik bir pay üzerinden
düşünmeyi öneriyor. Telegraf yazısının ortak sonucu: işaret ile darbe arasındaki süre
oyuncunun algılayıp cevap verebileceği kadar uzun, dövüşü ağırlaştırmayacak kadar kısa
olmalı — ve **tek bir doğru sayı yoktur**, beklenen cevabın süresine bağlıdır.

**Bizim için anlamı:** 0.75 sn, tepki tabanının **~3 katı**. Yani birikme, izleyicinin
"toparlanıyor, koşacak" diye okuyabileceği rahat bir pencere. Bizde oyuncunun refleksle
cevap vermesi gerekmiyor (dövüş tam otomatik), o yüzden alt sınır bize dar gelmiyor —
gereken şey *okunabilirlik*, ve 0.75 sn onun fazlasıyla üstünde.

**Dürüst sınır:** bu, 0.75'in **doğru** sayı olduğunu kanıtlamaz; yalnızca *okunamayacak
kadar kısa olmadığını* gösterir. Sayının kendisi ölçümden geldi (dağılma oranı eğrisinin
dizi).

> [Reaction Time and Game Design](https://www.retrogamedeconstructionzone.com/2020/05/reaction-time-and-game-design.html) ·
> [How to Design Enemy Attack Telegraphs](https://bugnet.io/blog/how-to-design-enemy-attack-telegraphs) ·
> [Keys to Combat Design: Anatomy of an Attack](https://gdkeys.com/keys-to-combat-design-1-anatomy-of-an-attack/)

---

## 3. Taahhüt: hız kazanılır, yavaşlayınca kaybedilir

**Dayanak — Mount & Blade, couched lance.** Mızrağı yatırmak **belli bir at hızının
üstünde** olmayı gerektirir (Bannerlord'da eşik ~44 hız değeri); hız yetmiyorken mızrak
yukarıda durur, yeterli hıza ulaşınca **görünür biçimde** koltuk altına iner. Tek vuruştan
sonra ya da **yeterince yavaşlayınca** taahhüt bozulur. **Hasar hıza bağlıdır** — hızlı at
daha çok vurur. Ayrıca couched vuruş normal yön bloklamasını **görmezden gelir**.

**Ne doğruluyor:**
- Hücumun **görünür bir hazırlık durumu** olması (bizde birikme; onlarda mızrağın inmesi)
- Taahhüdün **koşullara bağlı bozulması**

**Nerede bilerek ayrılıyoruz:** M&B'de couched vuruş savunmayı (yön bloklamasını) devre dışı
bırakır. Bizde bir süre benzeri vardı — hücum eden kaçınamıyordu — ve **kaldırıldı**. Ölçüm
sebebi gösterdi: savunmasızlık, hücumu *kimin yaptığına* göre asimetrik bir ceza üretiyordu
(GDD §4, "Neden hücum savunmayı kapatmıyor"). M&B'de bunu taşıyabilen şey, oyuncunun
kontrolündeki tek bir vuruş olması; bizde hücum otomatik ve iki taraf da yapıyor.

**Uygulandı — ve iki adımda tuttu.** M&B'nin "hasar hıza bağlıdır" kalıbı alındı: varış
vuruşunun çarpanı artık `1 + (varış hızı ÷ azami yürüme hızı) × oran`. **İlk ölçümde denge
beklentisi doğrulanmadı:** `Speed` ekseni hâlâ atıldı (3v3 zaferi Hız 0'da %83.9, Hız 100'de
%84.7). Sebep kalıbın kendisi değil, bizim tarafımızdaki ikinci bağdı — hücum zarı karar adımı
başına atıldığı için hızlı savaşçı fırsat penceresinden çabuk geçiyor ve **daha seyrek** hücum
ediyordu (2.20 → 1.20). Kalıbın verdiği artışı bizim kendi örnekleme biçimimiz yiyordu.

Zar fırsat başına bir kez atılınca sıklık hızdan koptu ve kalıp beklendiği gibi çalıştı:
zafer Hız 0'da **%83.6**, Hız 100'de **%87.0** (GDD §4, "Fırsat başına tek zar").

Ders: kaynak bir **kalıbı** doğrular, o kalıbın senin sisteminde ne yapacağını değil. M&B'de
hız hücumun tek değişkeni; bizde hıza bağlı ikinci bir kanal daha vardı ve ters işaretliydi.
Kalıbı ölçüp düz çıkması onu çürütmez — önce kendi sistemindeki karşı kanalı ara.

> [Couched lance damage (Mount & Blade Wiki)](https://mountandblade.fandom.com/wiki/Couched_lance_damage) ·
> [Bannerlord couch lance rehberi](https://gamerempire.net/mount-blade-2-bannerlord-how-to-couch-lance/)

---

## 4. "Yol boyunca fırsat saldırısı" — D&D bu kuralı iki kez daralttı

**Bizim mevcut kuralımız (GDD §4):** hücum eden savaşçının menzilinden geçtiği her düşman
ona bir kez bedava vuruş yapar. Ölçüm: hücum başına **0.35** vuruş — neredeyse hiç, ve
işlediği kadarı silah menzili farkının yan ürünü.

**Dayanak — D&D'nin fırsat saldırısı (attack of opportunity).** Kural elli yıldır masada
denenmiş ve **daraltıla daraltıla** bugünkü haline gelmiş:

| Sürüm | Tetik | Sayı sınırı |
|---|---|---|
| 3.x | Tehdit edilen alanda hareket/eylem — geniş tetik | Combat Reflexes ile **artırılabilir** |
| 5e | Yalnızca **menzilinden çıkarsan** | Tur başına **tek** tepki (reaction) |

5e'de düşmanın etrafında dönmek serbesttir; ancak menzilini terk edince provoke edersin.
Ve **Disengage** eylemiyle tamamen kaçınılabilir. Gerekçe olarak öne çıkan iki şey: tepkiyi
bir **karar** haline getirmek (oyuncu sırası gelmeden de dövüşe bağlı kalır) ve dövüşü
hızlı tutmak.

**Bizim için anlamı — bu, kullanıcının önerdiği yönü doğruluyor.** Yanından geçilen herkesin
dönüp vurması 3.x'in terk edilmiş geniş tetiği. Yerleşik pratik şunu söylüyor:

1. Tetik **"yanından geçti" değil, "seninle temastayken menzilini terk etti"** olmalı.
2. Düşman başına **sert bir üst sınır** olmalı (bizde zaten hücum başına bir kez).
3. Kaçınılabilir bir yolu olmalı (bizde: fırsat kuralı zaten hücumu kalabalıkken engelliyor).

Bu, GDD §5'teki kaçış penceresiyle de aynı kalıp — "taahhüde girerken menzilindekilere
borçlanırsın". Yani yeni bir kavram değil, var olanın hücuma uzanması.

> [Opportunity Attacks in D&D 5e (Arcane Eye)](https://arcaneeye.com/mechanic-overview/opportunity-attack-5e/) ·
> [Why Opportunity Attacks Matter in 5e](https://screenrant.com/dnd-5e-attack-opportunity-rules-good/)

---

## 5. Varıştaki açı — Total War bizimle çelişiyor

**Tartışılan öneri:** hücumun hedefi (kafa kafaya gelen) **düşük** oranla karşılık verir,
yandan geçilen savaşçılar **yüksek** oranla vurur; çünkü hücum edenin böğrünü görürler.

**Kaynak tersini söylüyor.** Total War serisinde hücumun karşılığı doğrudan **cepheye**
bağlı: *charge defence* özelliğine sahip mızraklı birlik, **sabit durur (braced) ve
cepheden hücuma uğrarsa** düşmanın charge bonusunu **tamamen iptal eder**. Yandan ya da
arkadan gelen hücum ise bonusu iptal **etmez** — birlik tam hasarı yer.

Yani sektörün en çok denenmiş hücum modelinde **kafa kafaya gelmek hücum edenin en kötü
açısıdır**, en iyisi değil. Gerekçesi sezgisel: sana bakan düşman, sana **hazır** olandır.

**Ama koşulu var, ve asıl ders orada.** TW'de bunu yapan her birlik değil; **doğru silah +
doğru duruş** gerekir. Mızrak cepheden hücumu durdurur, kılıç durduramaz.

**Bizim sistemimize düşen sonuç:** açıyı değil **silahı** ölçüt yapmak. Bizde zaten menzil
farkı var (katana 100, çift elli silahlar 150). Doğal kural:

> Hücumun hedefi, **hücum edenden uzun menzilli bir silah taşıyorsa** varışta bir karşılama
> vuruşu kazanır. Kısa silahlı hedef momentumu durduramaz.

Bu, üç şeyi birden yapar: kullanıcının "herkes dönüp vurmasın" itirazını korur (yalnızca
hedef, yalnızca koşulu sağlıyorsa), TW'nin doğrulanmış cephe kuralını alır, ve **yeni bir
sayı doğurmaz** — mevcut menzil değerlerini kullanır. Ayrıca `Weapon.Reach`'e ikinci bir
tasarım işi verir: uzun silah artık yalnızca "önce vurur" değil, "hücumu karşılar".

**Karara bağlandı (2026-09-02) — ve iki kaynak da kısmen haklı çıktı.** Kullanıcının açı
modeli alındı: hedef karşılık verebilir ama **kesin değil**, zara bağlı (0.6); yoldan
geçilen düşman vuruşunu kesin alır. Silah menzilini ölçüt yapan TW kuralı **alınmadı** —
onun yerine TW'nin asıl fikri, *bracing hücum bonusunu iptal eder*, olduğu gibi taşındı:
hedefin karşılığı tuttuğunda **momentum söner**, varış vuruşu hasar çarpanını kazanmaz.
Böylece nadir karşılığın ağırlığı olur ve yine **yeni bir sayı doğmaz**.

Ölçüm bir de sürpriz verdi: bu oran hücumun bedelinden fazlasını taşıyor. Hedefin topladığı
karşılıklar, **sayıca azalan tarafın başlıca geliri**; kısıldığında kalabalık tarafın
avantajı katlanıyor ve §5'in kaçış vaadi çöküyor. 0.6, tüm kilitli vaatleri ayakta tutan en
düşük değer olduğu için seçildi (tablo: GDD §4, "Hedefin karşılığı neyi taşıyor").

**Dürüst not:** TW'nin 13 sn'lik charge bonus süresi ve %20'lik bracing bonusu bize
taşınamaz — onlar dakikalarca süren birlik ölçekli savaşlar; bizim dövüşler 14 sn ve bonus
tek bir vuruşa biniyor. Kalıbı alıyoruz, sayıyı değil. Kaynaklar topluluk wiki'si ve forum
ölçümü, resmî tasarım belgesi değil.

> [Charge Bonus (Total War: Warhammer Wiki)](https://totalwarwarhammer.fandom.com/wiki/Charge_Bonus) ·
> [Charge Defence vs. Large](https://totalwarwarhammer.fandom.com/wiki/Charge_Defence_vs._Large)

---

## 6. Hücum bir "karar" mı — ve kimin kararı

**Ölçülen gerçek (güncel):** hücum artık her zaman doğru hamle **değil**. Düelloda nötr
(%66.7 → %66.0), donanımlı veteran için **zararlı** (%98.3 → %96.1), 3v3'te ölçülü bir
kazanç (%81.6 → %84.2). Savunmasızlık kuralı kaldırılmadan önce `veteran` dışında her
senaryoda yarıyordu.

**Dayanak — Sid Meier, "interesting decisions".** Ölçüt şudur: oyuncu seçeneklerden hep
aynısını seçiyorsa ya da seçim rastgeleyse, orada **ilginç bir karar yoktur**. Meier'in
saydığı karar türleri: kişiselleştirme, **takas (trade-off)**, ve kısa vade-uzun vade
gerilimi.

**Bu ölçütü bize uygularken bir düzeltme gerekiyor.** Bizde dövüş tam otomatik; oyuncu
hücuma karar vermiyor. O yüzden ölçüt **dövüş anına değil, dojo katmanına** uygulanmalı:
hücum, oyuncunun *hazırlıkta* verdiği kararların (Saldırganlık antrenmanı, `Speed`, silah
menzili, zırh) sahadaki karşılığı olmalı. Kararın ilginç olması için hazırlık ekseninde bir
takas gerekir.

**Şu anki durum:** Saldırganlık hücum sıklığını belirliyor ve hamlenin **gerçek bir bedeli
var** — iki senaryoda katkısı sıfır ya da eksi. Takas ölçütü sağlanıyor. Eksik kalan iki
eksen: `Speed` atıl (bkz. §3) ve silah menzili hücumda yalnızca kazara rol oynuyor
(bkz. §5). İkisi de bağlanırsa hücum, üç hazırlık ekseninin birden okunduğu yer olur.

> [GDC 2012: Sid Meier on interesting decisions](https://www.gamedeveloper.com/design/gdc-2012-sid-meier-on-how-to-see-games-as-sets-of-interesting-decisions) ·
> [Interesting Decisions (GDC Vault)](https://www.gdcvault.com/play/1015756/interesting)

---

## Hangi sayı nereden geliyor

Ticari oyunlar denge sabitlerini yayımlamaz. Bu yüzden **kaynaklardan sayı almıyoruz**;
kaynaklar kalıbı ve gerekçeyi veriyor, sayılar `Domina.Sim` ölçümünden çıkıyor.

| Sayı | Kaynağı |
|---|---|
| Birikme 0.75 sn | **Ölçüm** (dağılma eğrisinin dizi). Kaynaklar yalnızca *okunamayacak kadar kısa olmadığını* doğruluyor: tepki tabanı 200-300 ms |
| Saldırganlık eğrisi 0.12-0.45 | **Ölçüm** — dövüş başına 1.71 tamamlanmış hücum |
| Hasar çarpanı 1.5 | **Ölçüm** — 1.25-1.5 bandı oyuncu ölümünü en aza indiriyor |
| Hız çarpanı 1.6 | **Sunum kararı** — ölçümde denge etkisi yok |
| Süre sınırı 4.0 sn | **Emniyet supabı** — ölçümde hiç dolmuyor |
| Fırsat saldırısı: düşman başına bir kez | **Kalıp doğrulanmış** — D&D 5e tur başına tek tepki |
| Gereken mesafe | **Türetiliyor:** `düşmanın menzili + hızı × birikme` |

---

## Bu dosyaya ne eklenir

Yeni bir tasarım kararı GDD'ye girerken, dışarıda denenmiş bir karşılığı varsa buraya bir
bölüm açılır: **ne yaptığımız, kaynağın ne dediği, nerede ayrıldığımız ve neden.** Kaynağın
bizi çürüttüğü yerler (§5 gibi) **silinmez** — asıl değeri olan kayıt odur.
