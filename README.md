# Domina *(çalışma adı)*

Yokai/samuray temalı **dojo yönetimi + tam otomatik dövüş** oyunu.
Godot 4 · Steam · Twitch/Kick chat entegrasyonlu.

Bir dojo'nun sensei'sisin. Savaşçı yetiştirir, onları faz-faz ilerleyen seferlere
gönderirsin. Dövüşe müdahale edemezsin — tek istisna, bir savaşçıyı arenadan
**çekme** kararı. Ölüm kalıcıdır; ölümden dönenler uzuvlarını kaybedebilir ve
sakat kalarak savaşmaya devam eder.

Yayın yapıyorsan chat savaşçıların isimlerini belirler, dövüşleri **Bushi/Ronin**
diye yargılar (bu ödül ekonomisini etkiler) ve onursuz düşen bir savaşçının
**seppuku** oylamasında söz sahibi olur. Yayın yapmıyorsan aynı sistemleri bir
AI seyirci işletir — tek oyunculu mod mekanik olarak eşdeğerdir.

## Dokümanlar

| Dosya | İçerik |
|---|---|
| [`docs/GDD.md`](docs/GDD.md) | Tasarım kararları + açık kararlar tablosu |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Faz 0-9 geliştirme planı |
| [`docs/PROGRESS.md`](docs/PROGRESS.md) | Nerede kalındığının anlık fotoğrafı |
| [`CLAUDE.md`](CLAUDE.md) | Depo kuralları, mimari kısıtı, store-asset politikası |

## Durum

**Faz 2 — görselleştirme omurgası tamam.** Bir seed verildiğinde dövüş baştan sona
ekranda izleniyor: uzuvlar kopuyor, savaşçılar çekiliyor, pes etme tuşu çalışıyor.
Sanat **kasıtlı olarak geçici** (stickman) — görsel stil kararı verilmeden gerçek
sanata girilmiyor.

## Geliştirme

Godot çalıştırılabilir dosyası repoda tutulmaz; `tools/` altına indirilir
(`tools/` gitignore'ludur).

```
src/     simülasyon çekirdeği, chat adapter'ları, toplu simülasyon, Godot projesi
tests/   çekirdek testleri (motor açmadan koşar)
docs/    tasarım ve plan
tools/   Godot ikilisi (gitignore'lu)
```

### Toplu simülasyon

Denge çalışmasının aracı: Godot açmadan on binlerce dövüş koşturup ölüm, sakatlık
ve kazanma oranlarını verir.

```bash
dotnet run --project src/Domina.Sim -c Release -- --help
dotnet run --project src/Domina.Sim -c Release -- --scenario 3v3 --battles 10000 --policy below:0.3 --out sonuc.csv
```

`--policy` oyuncunun "çek" tuşunun yerine geçer; `never` ile `below:0.3` arasındaki
fark, uzuv kaybı mekaniğinin dengesini ölçmenin tek yoludur (uzuv kaybı yalnızca
zamanında müdahale edilen dövüşlerde oluşur).

### Oyunu çalıştırma

```bash
dotnet build src/Game/Domina.Game.csproj
tools/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64.exe --path src/Game -- --seed 81
```

`--seed` verilmezse varsayılan dövüş açılır. Toplu simülasyon ilginç bir dövüş
bildirdiğinde ("52 numaralı seed'de savaşçı kolunu kaybediyor") o dövüş burada
birebir izlenebilir — determinizmin pratik karşılığı budur.
