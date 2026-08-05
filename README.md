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
| [`CLAUDE.md`](CLAUDE.md) | Depo kuralları, mimari kısıtı, store-asset politikası |

## Durum

**Faz 0 — İskele.** Henüz oynanabilir bir yapı yok.

## Geliştirme

Godot çalıştırılabilir dosyası repoda tutulmaz; `tools/` altına indirilir
(`tools/` gitignore'ludur).

```
src/     simülasyon çekirdeği, chat adapter'ları, Godot projesi
tests/   çekirdek testleri (motor açmadan koşar)
docs/    tasarım ve plan
tools/   Godot ikilisi (gitignore'lu)
```
