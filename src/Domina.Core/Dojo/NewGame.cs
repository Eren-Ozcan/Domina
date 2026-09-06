using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Core.Dojo;

/// <summary>Yeni bir oyunun ilk günü.</summary>
/// <remarks>
/// <para>
/// Başlangıç durumu <b>çekirdekte</b> kuruluyor, ekranda değil: aynı kuruluşu ölçüm
/// koşusu da, oyun da, test de kullanmalı. Ekranda kurulsaydı oynanan dojo ile
/// dengesi ölçülen dojo sessizce ayrışırdı.
/// </para>
/// <para>
/// Sayılar ölçülen kurulumla aynı (GDD §11): <b>600 altın</b>, ambar boş — ilk günün
/// yiyeceğini gün kapanışı satın alır — ve dört savaşçılık bir kadro. Kadro
/// büyüklüğü ölçümün <c>RosterTarget</c>'ıyla aynı; başka bir sayı, ölçülen ekonomiyi
/// oynanan oyundan koparırdı.
/// </para>
/// <para>
/// Başlangıç kadrosu pazarın kendi üreticisinden çekiliyor, elle yazılmış savaşçılardan
/// değil: aynı istatistik dağılımı hem tezgâhı hem ilk kadroyu beslesin diye. Akış
/// <b>ayrı bir tohumdan</b> türetiliyor, yoksa ilk gün tezgâhta duran adaylar kadronun
/// birebir kopyası olurdu.
/// </para>
/// </remarks>
public static class NewGame
{
    /// <summary>Başlangıç kasası — GDD §11.</summary>
    public const int StartingGold = 600;

    /// <summary>Başlangıç kadrosunun büyüklüğü.</summary>
    public const int StartingWarriors = 4;

    /// <summary>Başlangıç kadrosunu üreten akışı günün pazarından ayıran karıştırıcı.</summary>
    private const ulong RosterSalt = 0xA5A5_5A5A_C3C3_3C3C;

    /// <summary>Verilen tohumdan yeni bir dojo kurar.</summary>
    /// <param name="seed">Seferin tohumu; aynı tohum aynı başlangıcı verir.</param>
    /// <param name="tuning">Gün döngüsü ayarları; verilmezse varsayılan.</param>
    public static DojoState Create(ulong seed, DojoTuning? tuning = null)
    {
        DojoState dojo = new(tuning, seed: seed)
        {
            Resources = new Resources(Gold: StartingGold),
        };

        SeededRandom random = new(seed ^ RosterSalt);
        IReadOnlyList<RecruitOffer> stock = dojo.Market.Stock(
            random,
            WarriorStats.Recruit(),
            dojo.Economy.RecruitPrice);

        for (int i = 0; i < StartingWarriors && i < stock.Count; i++)
        {
            RecruitOffer offer = stock[i];
            string name = offer.Name;
            for (int suffix = 2; dojo.Roster.IsNameTaken(name); suffix++)
            {
                name = $"{offer.Name} {suffix}";
            }

            // Başlangıç kadrosu bedava: 600 altın ilk günün kararları için duruyor,
            // kadronun kendisi için değil.
            dojo.Roster.Recruit(name, offer.Stats, Weapon.Katana(), Armor.Light(), offer.Talent);
        }

        return dojo;
    }
}
