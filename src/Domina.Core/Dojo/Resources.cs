namespace Domina.Core.Dojo;

/// <summary>Dojo'nun kasası ve ambarı.</summary>
/// <remarks>
/// <para>
/// Kaynak <b>türleri</b> GDD §11'den gelir (altın, yiyecek/su, ilaç); <b>sayıları</b>
/// gelmez — fiyatlar, günlük tüketim ve başlangıç stoğu Açık Karar #5'te duruyor.
/// Bu yüzden burada tek bir sabit yok: tür bir değer taşıyıcısı, aritmetiği ve
/// "yeter mi" sorusu kilitli sayı beklemeden yazılabilir.
/// </para>
/// <para>
/// Sayılar tam sayı: kaynak sayılabilir bir şeydir, kesirli altın bir tarafın
/// yuvarlamasıyla kaybolur ve kayıt/yükleme arasında birebir eşleşmez.
/// </para>
/// </remarks>
public readonly record struct Resources(int Gold = 0, int Food = 0, int Water = 0, int Medicine = 0)
{
    public static Resources Empty { get; }

    public static Resources operator +(Resources a, Resources b) => new(
        a.Gold + b.Gold,
        a.Food + b.Food,
        a.Water + b.Water,
        a.Medicine + b.Medicine);

    public static Resources operator -(Resources a, Resources b) => new(
        a.Gold - b.Gold,
        a.Food - b.Food,
        a.Water - b.Water,
        a.Medicine - b.Medicine);

    /// <summary>Verilen gideri karşılayabilir mi?</summary>
    public bool Covers(Resources cost) =>
        Gold >= cost.Gold && Food >= cost.Food && Water >= cost.Water && Medicine >= cost.Medicine;

    /// <summary>Herhangi bir kalem eksiye düşmüş mü?</summary>
    public bool AnyNegative => Gold < 0 || Food < 0 || Water < 0 || Medicine < 0;

    /// <summary>Eksileri sıfıra çeker — açık verilen kalemleri ayrıca bildirmek çağıranın işi.</summary>
    public Resources ClampedToZero() => new(
        Math.Max(0, Gold),
        Math.Max(0, Food),
        Math.Max(0, Water),
        Math.Max(0, Medicine));
}
