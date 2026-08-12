# Geliştirme Yol Haritası

> Tasarım kararları için `GDD.md`. Bu dosya **nasıl inşa edileceğini** anlatır.
> Efor tahminleri **görecelidir** (S/M/L/XL), takvim değil.

## Temel İlke: Çekirdek Önce, Motor Sonra

En kritik mimari karar: **simülasyon çekirdeği Godot'a hiç bağımlı olmayacak.**

```
src/
  Core/          → saf C# sınıf kütüphanesi (Godot referansı YOK)
                   statlar, dövüş çözümleyici, onur, ekonomi, yaralanma, RNG, save modeli
  Chat/          → saf C# (Godot referansı YOK)
                   Twitch/Kick adapter'ları, komut ayrıştırma, oylama motoru
  Game/          → Godot 4 projesi — Core ve Chat'i ProjectReference ile kullanır
                   sahneler, animasyon, UI, ses, Steam
tests/
  Core.Tests/    → xUnit — Godot açmadan çalışır
  Chat.Tests/    → xUnit — sahte (fake) chat akışıyla
```

**Neden:** Dövüş tam otomatik ve chat sonucu etkiliyor. Bu tür bir sistem ancak
**motor açmadan, saniyeler içinde binlerce dövüş simüle edilerek** dengelenebilir.
UI'a bağlı bir çekirdekle denge çalışması imkânsız hale gelir.

**Deterministik RNG (Faz 1'de kurulur, sonradan eklenemez):** Her dövüş bir `seed`
ile başlar. Aynı seed + aynı girdiler = aynı sonuç. Bu üç şeyi sağlar:
1. Bug tekrar üretilebilir ("şu seed'de savaşçı yanlış öldü")
2. Denge testi otomatikleşir (10.000 dövüş toplu simülasyon)
3. Dövüş tekrarı/replay özelliği ileride bedava gelir

---

## Faz 0 — İskele · S

**Hedef:** Boş ama sağlam bir temel.

- [x] `git init`, `.gitignore` (Godot + .NET + `docs/store-assets-originals/`)
- [x] Godot 4.x projesi + C# (.NET) kurulumu, çalıştığının doğrulanması
- [x] Yukarıdaki `src/` + `tests/` klasör yapısı, `.csproj` referansları
- [x] xUnit kurulumu, `dotnet test` çalışıyor
- [x] `dotnet format` / analyzer kuralları
- [x] README + `docs/` (GDD, ROADMAP zaten var)
- [x] GitHub Actions: push'ta build + test

**Kabul:** `dotnet test` yeşil, boş Godot sahnesi açılıyor, CI geçiyor.

---

## Faz 1 — Simülasyon Çekirdeği (Görselsiz) · L

**Hedef:** Godot açmadan, konsolda tam bir dövüş simüle edilebiliyor.

### 1.1 Veri modeli
- [x] `Warrior`: HP, Saldırganlık, Savunma, Kaçınma, Güç, Stamina, Onur
- [x] `Injury` / `Disability`: kol, bacak, göz — kalıcı stat modifikatörleri
- [x] `Weapon`: kesici/künt ayrımı, hasar, el sayısı (tek/çift)
- [x] `Armor`: savunma değeri, uzuv kaybı riski azaltıcı
- [x] Kimlik: her savaşçının **benzersiz ID**'si var; isim ayrı alan
      (aynı isim farklı zamanlarda yeniden kullanılabilir — GDD §6)

### 1.2 Deterministik RNG
- [x] `IRandomSource` arayüzü + seed'li implementasyon
- [x] **Kural:** Core içinde `System.Random` doğrudan kullanılmaz, hep bu arayüz
- [x] Test için sahte (scripted) RNG implementasyonu

### 1.3 Dövüş çözümleyici
- [x] Vuruşma çözümü: Saldırganlık → Kaçınma zarı → Savunma → hasar (GDD §4)
- [x] Stamina tüketimi ve düşük stamina cezaları
- [x] Uzuv kaybı riski: darbe/maxHP oranı × silah tipi × zırh (GDD §7)
- [x] Sonuç ağacı: hafif / ağır+pes / ağır+müdahalesiz (ölüm)
- [x] 1v1, 2vX, 3vX çoklu savaşçı desteği
- [x] Dövüş **olay akışı (event stream)** üretir — görselleştirme bunu tüketecek
      (`Vurdu`, `Kaçındı`, `UzuvKopti`, `Öldü`, `KaçışBaşladı`...)

> **Kritik:** Çözümleyici animasyon hakkında hiçbir şey bilmez. Sadece olay üretir.
> Faz 2'deki görselleştirme bu olayları oynatır. Bu ayrım bozulursa denge testi ölür.

### 1.4 Pes etme mantığı
- [x] Kesilebilir/kesilemez durum makinesi (GDD §5)
- [x] Komut buffer'lama (saldırıya kilitliyken bekletme)
- [x] Kaçış penceresi: kaçınma/blok devre dışı, rakibe fırsat saldırısı

### 1.5 Onur motoru
- [x] 0-100 ölçek, decay, dövüş performansından hesaplama
- [x] `bushiOrani` → `odulCarpani` clamp(0.5, 1.5)
- [x] Seppuku eşiği tespiti + **kuyruk** (aktif dövüş varken bekletme)
- [x] 15 dakikalık af cooldown'ı

### 1.6 Toplu simülasyon aracı
- [x] CLI: N dövüşü seed aralığında koştur, ölüm/sakatlık/kazanma oranlarını CSV'ye yaz
- [x] Bu araç tüm denge çalışmasının temeli — **Faz 1'de yapılmazsa Faz 9'da acı çekilir**
- [x] `--policy` ile oyuncunun "çek" tuşunun yerine geçen kaçış politikası
      (uzuv kaybı yalnızca müdahale edilen dövüşlerde oluştuğu için şart)

**Kabul:** ✅ *(2026-08-06)*
- [x] `dotnet test` ile 3v3 dövüş baştan sona simüle ediliyor
- [x] Aynı seed 100 kez aynı sonucu veriyor (determinizm testi)
- [x] 10.000 dövüş < 10 saniyede koşuyor — ölçüldü: **~1 sn** (Release), ~2 sn (Debug)
- [x] Uzuv kaybı, pes etme, onur, seppuku kuyruğu için birim testler yeşil (112 test)

**Risk:** Denge sayıları bu fazda tutmayacak — normal. Amaç **çalışan ve ölçülebilir**
bir sistem, dengeli bir sistem değil.

---

## Faz 2 — Dövüş Görselleştirme · M

**Hedef:** Faz 1'in olay akışı ekranda izlenebilir bir dövüşe dönüşüyor.

### 2.1 Stil-bağımsız omurga — ✅ *(2026-08-12)*
- [x] Savaşçı sahne yapısı, **modüler uzuvlar** — 15 parçalı cutout rig, kopma
      noktaları omuz ve kalça (GDD §2, §7)
- [x] Animasyon durum makinesi: bekle → yaklaş → saldır → tepki → tekrar
- [x] Faz 1 olaylarını animasyona bağlama (event → görsel tepki) — **saldırının üç
      sonucu ayırt edilebiliyor**: isabet sarsar, ıska savurur, kaçınma yana kaçar;
      fırsat saldırısının ayrı bir vuruşu var
- [x] Kesme pencereleri (cancel window) animasyon zamanlamasıyla eşleştirme
- [x] **Uzuv kopma:** uzvun rig'den ayrılması, kan VFX, kalıcı model değişimi
- [x] Sakat animasyon setleri: topallama (kaçarken de), tek elli duruş
- [x] Ölüm animasyonu (yığılma — gore geçici sanatla anlamsız, sanatla gelecek);
      ceset düştüğü yerde kalıyor
- [x] **Pes etme tuşu** UI'ı — **tek tuş, ekibin tamamını çeker** (GDD §5); tuş kaç
      savaşçının etkileneceğini ve kaçının vuruşa kilitli olduğunu önceden gösterir,
      basıldıktan sonra da hangisinin beklediği panelde okunuyor
- [x] `-- --seed N` ile toplu simülasyonun bildirdiği dövüşü birebir izleme
      (`--speed N` ile hızlandırılabiliyor)
- [x] Sunum mantığı motordan ayrıldı (`Domina.Presentation`) ve **57 testle**
      kapsandı — Faz 2 artık repodaki diğer fazlar gibi doğrulanıyor

> **Rig'de Skeleton2D + Bone2D kullanılmadı.** Bone2D iskeleti mesh *deformasyonu*
> içindir; bizim ihtiyacımız uzvu deforme etmek değil **koparmak**. Düz `Node2D`
> hiyerarşisinde kopma, düğümü zincirinden ayırmaktır — GDD §2'nin tarif ettiği şeyin
> tam karşılığı, üstelik daha az parça. Skeleton2D ileride IK gerekirse eklenebilir.

> **Sunum mantığı Godot'un içinde değil.** Kim nerede durur, hangi olay hangi tepkiyi
> doğurur, kemikler hangi açıyı alır, tuşta ne yazar — hepsi `Domina.Presentation`
> içinde, motora bağımsız. Gerekçe çekirdektekiyle aynı: motor açmadan test edilemeyen
> karar hiç test edilmez. `src/Game` artık yalnızca düğüm kurup gelen açıyı uyguluyor.

### 2.2 Sanat ve cila — ⬜ stil kararına bağlı
- [ ] Görsel stil kararı (GDD Açık Karar #6) — **bu verilmeden ilerlenmez**
- [ ] Kemiklere gerçek sanat varlıklarının asılması
- [ ] Kamera, arena sahnesi, vuruş efektleri, ses
- [ ] Ölüm/bitiriş (gore) animasyonları
- [ ] Zırh kademelerinin görsel karşılığı (4 kademe × ~10 parça)

**Kabul:** Bir seed verildiğinde dövüş baştan sona izlenebiliyor; olaylar ile ekranda
görünen birebir tutuyor (uzuv kopan savaşçı ekranda da kopuk).
✅ *Omurga için doğrulandı ve artık **testle bağlandı**: `ArenaPlaybackTests` dövüşü
arenayla aynı sırayla oynatıp bilançodaki uzuv kaybının ekrandaki kopmayla — üstelik
aynı uzuvla — eşleştiğini sınıyor. Motorla karşılaştırma da tutuyor: seed 20260806
arenada da motorsuz oynatmada da PlayerVictory / 15,2 sn, seed 81 ikisinde de
PlayerDefeat / 32,0 sn.*

**Risk:** Hâlâ projenin en maliyetli görsel parçası, ama **2D cutout kararıyla
XL'den M'e indi** — 3D modüler dismemberment riski ortadan kalktı. Omurga bittiğine
göre kalan riskin tamamı 2.2'de: sanat üretimi ve stil kararı.

**Ön koşul:** Görsel **stil** kararı (GDD Açık Karar #6 — piksel mi mürekkep mi)
bu faza başlamadan verilmeli. Teknik yol belli, stil değil.

---

## Faz 3 — Dojo / Meta Katman · L

**Hedef:** Dövüşler arası oyun.

- [ ] Roster ekranı: savaşçılar, statlar, yaralar, onur, isim düzenleme
- [ ] **İsim düzenleme** (chat'ten gelen veya üretilen ismi değiştirme — GDD §8)
- [ ] Antrenman alanları + antrenman süresi/etkisi
- [ ] **Savaşçı skill tree'si (basit)**
- [ ] **Okul + Eğitmen skill tree'si (derin)** — antrenman hızı, tesis kilitleri,
      ekonomi bonusları, revir iyileştirmeleri
- [ ] Revir/hekim: iyileşme süresi, ilaç kaynağıyla hızlandırma
- [ ] Ekonomi: altın, yiyecek/su, ilaç; alım-satım; rastgele olaylar
- [ ] Gün döngüsü
- [ ] Recruit akışı (isim havuzu bağlantısı Faz 5'te takılacak — şimdilik yerel havuz)
- [ ] **Kayıt sistemi:** versiyonlu, merge-on-load, try/catch (GDD §2)

**Kabul:** Bir savaşçı işe alınıp eğitilebiliyor, yaralanıp iyileşebiliyor, oyun
kapatılıp açıldığında her şey yerinde.

---

## Faz 4 — Sefer ve Bestiary · L

**Hedef:** Faz-faz ilerleyen seferler ve yokai düşmanlar.

- [ ] Sefer yapısı: faz zinciri, zorluk eğrisi, ödül dağılımı
- [ ] **Pes etme seferi bitirir** (GDD §5, §10): "kaç" denen odadan sonrası iptal,
      ekip dojo'ya döner, o seferin ödülü alınmaz. Önceki odalarda toplanan
      ganimetin akıbeti hâlâ açık (GDD Açık Karar #9)
- [ ] Harita/ilerleme ekranı (Açık Karar #2: düz mü node-map mi)
- [ ] Parti seçimi: 1-3 savaşçı (Açık Karar #1)
- [ ] Yokai davranış/AI profilleri — her yokai farklı dövüş kalıbı
- [ ] Boss encounter'ları

### Bestiary adayları
| Yokai | Rol / karakter |
|---|---|
| **Oni** | Ağır, yüksek hasar, yavaş — tank/bruiser |
| **Kappa** | Küçük, çevik, sürü halinde |
| **Tengu** | Hızlı, yüksek kaçınma, hit-and-run |
| **Kitsune** | Aldatma/illüzyon — sahte hedefler |
| **Yuki-onna** | Yavaşlatma/dondurma, stamina baskısı |
| **Jorōgumo** | Örümcek — hareket kısıtlama |
| **Nue** | Karma yaratık — mini boss |
| **Gashadokuro** | Dev iskelet — **boss** |
| **Shuten-dōji** | Oni kralı — **boss** |
| **Yamata-no-Orochi** | Sekiz başlı yılan — **final boss** |

**Kabul:** Baştan sona bir sefer oynanabiliyor, ölüm/kayıp/ödül dojo'ya doğru yansıyor.

---

## Faz 5 — Chat Entegrasyonu · L

**Hedef:** Twitch ve Kick bağlanıyor, tüm chat mekanikleri çalışıyor.

### 5.1 Adapter katmanı (önce bu)
- [ ] `IChatSource` arayüzü — platform-bağımsız
- [ ] Ortak iç olaylar: `MesajGeldi(kullanıcı, metin)`, `BağışGeldi(kullanıcı, miktar)`
- [ ] **`FakeChatSource`** — test ve geliştirme için sahte chat (gerçek bağlantı olmadan
      tüm mekanikler test edilebilir; bu olmadan chat özellikleri geliştirilemez)
- [ ] Thread-safety: chat asenkron gelir, oyun döngüsüne **kuyrukla** aktarılır

### 5.2 Komut motoru
- [ ] İsim havuzu: **varsayılan herkes dahil**, `!no` → çıkış, `!join` → öncelik
- [ ] Kullanıcı adı filtresi (küfür/uygunsuz)
- [ ] `!bushi` / `!ronin` → aktif dövüş
- [ ] `!bushi-<isim>` / `!ronin-<isim>` → hedefli, küçük etki, bulunamazsa **sessiz**
- [ ] Seppuku oylaması: 60 sn pencere, kullanıcı başına tek oy, kuyruk
- [ ] "Kahraman Çağır": bağış → garantili roster girişi, miktar→seviye, **üst sınırlı**

### 5.3 Twitch
- [ ] Chat: IRC-over-WebSocket (`wss://irc-ws.chat.twitch.tv`)
- [ ] **Spike:** Cheer/Bits'in PRIVMSG `bits` tag'inden okunabildiğini **doğrula**.
      Doğruysa Bits için ayrı OAuth/EventSub gerekmez — büyük sadeleşme.
      Doğru değilse EventSub WebSocket + OAuth akışı gerekir.
- [ ] Bağlantı kopma/yeniden bağlanma, rate limit

### 5.4 Kick
- [ ] **Spike (öncelikli):** Kick'in güncel resmî API'sinde chat okuma ve **Kicks**
      (bağış) olaylarının nasıl alındığını doğrula — OAuth kapsamları, webhook mu
      websocket mi, rate limit.
      → Kick API'si Twitch'e göre çok daha yeni; **burada belirsizlik var, erken doğrula.**
- [ ] Adapter implementasyonu
- [ ] Kicks → aynı `BağışGeldi` olayına normalize etme

**Kabul:** `FakeChatSource` ile tüm mekanikler test ediliyor; gerçek bir Twitch
kanalına bağlanıp `!join`, `!bushi`, seppuku oylaması ve Bits akışı uçtan uca çalışıyor.

**Risk:** Kick API'si en büyük bilinmeyen. Adapter katmanı sayesinde Kick gecikse bile
oyun Twitch ile çıkabilir — **Kick'i sürüm engeli yapma.**

---

## Faz 6 — AI Seyirci · M

**Hedef:** Tek oyunculu mod, yayın moduyla mekanik olarak eşdeğer.

- [ ] Dövüş performans sinyallerinden bushi/ronin oranı üretimi (GDD §9)
- [ ] Seppuku oylamasında onur-ağırlıklı olasılıksal karar
- [ ] **Sıfır oy fallback'i:** gerçek chat var ama hiç oy gelmediyse de AI karar verir
- [ ] Japon isim havuzu (`!join` havuzu boşken devreye girer)

**Kabul:** Chat olmadan tam bir kampanya oynanabiliyor; hiçbir sistem "kapalı" değil.

---

## Faz 7 — Steam Entegrasyonu · M

- [ ] **Spike:** Godot 4 + C# için Steamworks yolu seç
      (GodotSteam GDExtension vs Steamworks.NET vs Facepunch.Steamworks) —
      C# uyumunu **kod yazmadan önce** doğrula
- [ ] Steamworks başlatma, Steam ID, overlay
- [ ] Achievements (uzuv kaybı, seppuku, boss'lar, hayatta kalma serileri)
- [ ] Cloud save
- [ ] İçerik derecelendirme anketi (gore/dismemberment beyanı)
- [ ] Mağaza sayfası, kapsül görselleri, fragman
- [ ] Build pipeline (Steam depot upload)

> **Not (global CLAUDE.md kuralı):** Mağaza/pazarlama görselleri **public repoya
> commit edilmez** — yerel `docs/store-assets-originals/` (gitignore'lu) +
> private `Eren-Ozcan/pictures` reposunda `pictures/<proje>/` altına.

---

## Faz 8 — Cila · M

- [ ] Lokalizasyon altyapısı (TR/EN) — hardcoded metin bırakma
- [ ] Ses tasarımı, müzik
- [ ] Ayarlar, tuş atama, erişilebilirlik (gore filtresi dahil)
- [ ] Onboarding/tutorial — otomatik dövüş oyunu olduğu için "neyi kontrol ettiğim"
      net anlatılmalı
- [ ] Yayıncı modu ayarları ekranı: kanal adı, komut açma/kapama, gore seviyesi

---

## Faz 9 — Denge ve Çıkış · L

- [ ] Faz 1'deki toplu simülasyon aracıyla denge geçişleri
- [ ] Onur eşikleri, decay hızı, hedefli komut katsayısı (Açık Karar #8)
- [ ] Griefing testi: kötü niyetli chat senaryolarını simüle et
- [ ] Kapalı playtest → yayıncı playtest'i (chat mekanikleri **ancak gerçek yayında**
      test edilebilir — bunu erken planla)
- [ ] Steam Next Fest / demo
- [ ] Early Access veya tam çıkış kararı
- [ ] **Çıkış öncesi isim çakışması kontrolü tekrarı** (bkz. hafıza notu)

---

## Kritik Sıralama Kuralları

1. **Faz 1 çekirdeği görselden önce** — tersi olursa denge çalışması imkânsızlaşır
2. **Deterministik RNG Faz 1'de** — sonradan eklenemez, her yere sızmış olur
3. **`FakeChatSource` gerçek API'lerden önce** — yoksa her test için canlı yayın gerekir
4. **Kick spike'ı erken** — en büyük teknik bilinmeyen
5. **Görsel stil kararı Faz 2'den önce** — teknik yol (2D cutout) belli, stil değil

## Bağımlılık Zinciri

```
Faz 0 ─→ Faz 1 ─┬─→ Faz 2 (görsel)     ─┐
                ├─→ Faz 3 (dojo)        ─┼─→ Faz 4 ─→ Faz 9
                └─→ Faz 5 (chat) ─→ Faz 6┘
                                    Faz 7 ─┘
```
Faz 2, 3 ve 5 birbirinden bağımsız — Faz 1 bittikten sonra istenen sırayla ilerlenebilir.
