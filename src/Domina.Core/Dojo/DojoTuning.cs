namespace Domina.Core.Dojo;

/// <summary>Gün döngüsünün ayarlanabilir sayıları.</summary>
/// <remarks>
/// Buradaki hiçbir sayı <b>kilitli değil</b>. Onur decay'i Açık Karar #8'e, iyileşme
/// hızı ve kaynak tüketimi Açık Karar #5'e bağlı; ikisi de ölçümle kapanacak. Varsayılanlar
/// döngüyü çalışır tutmak içindir, denge iddiası taşımaz.
/// </remarks>
public sealed record DojoTuning
{
    /// <summary>Bir günde eriyen revir günü sayısı.</summary>
    public int NaturalRecoveryPerDay { get; init; } = 1;

    /// <summary>
    /// Canının tamamına yakınını kaybederek dönen savaşçının yatacağı gün sayısı.
    /// </summary>
    public int RecoveryDaysAtFullDamage { get; init; } = 6;

    /// <summary>Kaybedilen her uzvun eklediği revir günü.</summary>
    /// <remarks>
    /// Uzuv kaybı zaten kalıcı ceza taşır (GDD §7); buradaki gün, kaybın <b>üstüne</b>
    /// gelen tedavi süresidir, cezanın kendisi değil.
    /// </remarks>
    public int RecoveryDaysPerLostLimb { get; init; } = 5;

    /// <summary>
    /// Bedava sayılan hasar payı — bunun altında kalan sıyrık gün yemez.
    /// </summary>
    /// <remarks>
    /// Eşik olmasaydı her dövüş bir gün revir demek olurdu ve gün döngüsünün asıl
    /// kararı ("bugün sefere mi, antrenmana mı") kendiliğinden ortadan kalkardı.
    /// </remarks>
    public double RecoveryFreeDamageShare { get; init; } = 0.25;

    /// <summary>
    /// Onurun nötre (<see cref="Model.HonorScale.Starting"/>) doğru günlük kayması.
    /// </summary>
    /// <remarks>
    /// GDD §6'nın gerekçesi: bir troll saldırısı kalıcı ceza olmamalı, yalnızca
    /// <b>sürekli</b> onursuzluk seppuku'ya götürmeli. Decay o sürekliliği zorunlu kılar.
    /// </remarks>
    public double HonorDecayPerDay { get; init; } = 0.5;
}
