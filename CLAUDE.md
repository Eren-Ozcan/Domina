# Domina (çalışma adı — final isim henüz belirlenmedi)

Yokai/samuray temalı, dojo yönetimi + tam otomatik dövüş oyunu. Twitch/Kick chat
entegrasyonlu, Steam hedefli, Godot 4.

> ⚠️ Bu proje, `C:\Projects\Domina` klasöründe daha önce bulunan **tamamlanmış trivia
> oyunu Domina ile ilgisizdir**. "Domina" burada yalnızca çalışma klasörü adıdır.

## Tasarım ve plan — tek doğruluk kaynağı

Bu proje hakkında herhangi bir işe başlamadan önce oku:

- **`docs/GDD.md`** — kilitlenmiş tasarım kararları. Sonundaki "Açık Kararlar" tablosu
  neyin hâlâ karara bağlanmadığını gösterir.
- **`docs/ROADMAP.md`** — Faz 0-9 geliştirme planı, kabul kriterleri, riskler.
- **`docs/DESIGN-REFERENCES.md`** — kararların dışarıdan doğrulanabilir dayanakları
  (yerleşik tasarım pratiği, kaynak bağlantıları) ve kaynakların bizi çürüttüğü yerler.

Tasarım kararlarını yeniden tartışmaya açma — kullanıcı bunları uzun bir oturumda
madde madde kararlaştırdı. Değişiklik gerekiyorsa önce GDD'yi güncelle.

## Mimari kuralı (bozulmaması kritik)

Simülasyon çekirdeği **motora bağımlı olmayacak**. Dövüş çözümleyici animasyon
hakkında hiçbir şey bilmez — yalnızca **olay akışı** üretir; görselleştirme onu
tüketir. Rastgelelik **seed'li ve deterministik** olmalı.

Gerekçe: dövüş tam otomatik ve chat sonucu etkiliyor; denge ancak motor açmadan
on binlerce dövüş simüle edilerek yapılabilir. Bu ayrım bozulursa denge çalışması
imkânsızlaşır. Ayrıntı: `docs/ROADMAP.md` → "Temel İlke".

## Motor ikilisi

Godot çalıştırılabilir dosyası repoya girmez, `tools/` altında tutulur
(diğer projelerdeki düzenle aynı). `tools/` gitignore'ludur.

## Store / Pazarlama Görselleri

Store listing, feature graphic, ikon, ekran görüntüsü gibi pazarlama görselleri
**asla bu public repoya commit edilmez**. İki yerde tutulur:

1. Yerel, gitignore'lu kopya: `docs/store-assets-originals/`
2. Private yedek repo: `C:\Projects\pictures\<proje-klasörü>\` (private
   `Eren-Ozcan/pictures` reposunun yerel clone'u) — dosyalar oraya kopyalanır,
   o repoda commit + push edilir.

## Stüdyo geneli bilgiler

Bu oyuna özgü olmayan konular (Google hesabı, Play Console, Steam geliştirici hesabı,
yilkgames.com durumu) için tek doğruluk kaynağı `C:\Projects\pictures\STUDIO.md`.
Burada tekrarlanmaz.
