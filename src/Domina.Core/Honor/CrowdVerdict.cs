namespace Domina.Core.Honor;

/// <summary>Chat'in bir dövüşe verdiği tepkinin sayımı.</summary>
/// <param name="Bushi">"Gerçek bir savaşçı gibi dövüştü" diyenler.</param>
/// <param name="Ronin">"Onursuz" diyenler.</param>
/// <remarks>
/// <para>
/// Ham sayı değil <b>oran</b> kullanılır. 5 kişilik bir chat'te 3 ronin ile 5000
/// kişilik bir chat'te 3 ronin aynı şey değildir; oran, küçük ve büyük yayınlar
/// arasında adaleti sağlar.
/// </para>
/// <para>
/// Çarpanın alt/üst sınırı olması da spam'in sonucu uçlara çekmesini engeller
/// (bkz. docs/GDD.md §6).
/// </para>
/// </remarks>
public readonly record struct CrowdVerdict(int Bushi, int Ronin)
{
    public static CrowdVerdict Silent => new(0, 0);

    public int Total => Bushi + Ronin;

    /// <summary>Tek bir oy bile geldiyse gerçek oy sayılır — asgari katılım eşiği yok.</summary>
    public bool HasVotes => Total > 0;

    /// <summary>Oy yoksa nötr (0.5).</summary>
    public double BushiRatio => Total == 0 ? 0.5 : (double)Bushi / Total;

    /// <summary>Chat'in çoğunluğu affetmekten yana mı?</summary>
    public bool FavorsMercy => Bushi > Ronin;

    /// <summary>Dövüş ödülüne uygulanacak altın çarpanı.</summary>
    public double RewardMultiplier(HonorTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);

        double span = tuning.MaxRewardMultiplier - tuning.MinRewardMultiplier;
        return Math.Clamp(
            tuning.MinRewardMultiplier + (BushiRatio * span),
            tuning.MinRewardMultiplier,
            tuning.MaxRewardMultiplier);
    }

    public CrowdVerdict WithBushi(int count = 1) => this with { Bushi = Bushi + count };

    public CrowdVerdict WithRonin(int count = 1) => this with { Ronin = Ronin + count };
}
