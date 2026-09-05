using Domina.Core.Model;

namespace Domina.Core.Campaign;

/// <summary>Günün karşılaşma teklifi — al ya da bırak.</summary>
/// <remarks>
/// <para>
/// GDD §10: günde <b>tek</b> teklif gelir, liste ya da harita ekranı yoktur. Girmek bir
/// gün yer (kaçılsa da), girilmezse gün dojo'da geçer.
/// </para>
/// <para>
/// Teklif düşman kadrosunun <b>tamamını</b> taşır ama arayüz onu göstermez: oyuncu yalnızca
/// <see cref="Threat"/> ve <see cref="Sighting"/> okur. Kadronun burada durmasının sebebi
/// teklifin kabul edildiğinde <b>aynı</b> dövüşü kurması — teklif anında bir kadro,
/// dövüş anında başka bir kadro üretilseydi tehdit işareti yalan söylerdi.
/// </para>
/// </remarks>
/// <param name="Day">Teklifin geçerli olduğu gün.</param>
/// <param name="Enemies">Karşıya çıkacak kadro.</param>
/// <param name="Threat">Girmeden önce okunabilen zorluk bandı.</param>
/// <param name="Sighting">Girmeden önce okunabilen kaba tanım ("üç kappa" gibi).</param>
/// <param name="RequiredPartySize">
/// Encounter tam bir sayı dayatıyorsa o sayı; dayatmıyorsa <c>null</c> (üst sınır yine 4).
/// </param>
public sealed record EncounterOffer(
    int Day,
    IReadOnlyList<Warrior> Enemies,
    ThreatBand Threat,
    string Sighting,
    int? RequiredPartySize = null)
{
    /// <summary>Ödülün ölçeklendiği ham büyüklük (bkz. <c>Quartermaster.PromisedReward</c>).</summary>
    public double EnemyHealth => Enemies.Sum(e => e.EffectiveStats.MaxHealth);

    /// <summary>Verilen büyüklükteki bir ekip bu teklife girebilir mi?</summary>
    public bool Accepts(int partySize) =>
        partySize > 0
        && partySize <= MaxPartySize
        && (RequiredPartySize is null || partySize == RequiredPartySize);

    /// <summary>Sefere gönderilebilecek azami savaşçı (GDD §10, Açık Karar #1).</summary>
    public const int MaxPartySize = 4;
}

/// <summary>Girmeden önce okunabilen kaba tehdit işareti.</summary>
/// <remarks>
/// GDD §10: tam kadro ve statlar <b>görünmez</b>. Seçim bilgili olsun ama sürpriz ölmesin
/// diye yalnızca bir bant okunur. Bant düşmanın gücünden değil, <b>oyuncunun kadrosundan
/// bağımsız</b> ham güçten çıkar — "senin için zor" demek, oyuncunun kendi kararını
/// oyunun eline vermek olurdu.
/// </remarks>
public enum ThreatBand
{
    /// <summary>Devriye işi.</summary>
    Faint,

    /// <summary>Sıradan gün.</summary>
    Rising,

    /// <summary>Kadro hazırlanmalı.</summary>
    Heavy,

    /// <summary>Ölüm riski yüksek.</summary>
    Dire,
}
