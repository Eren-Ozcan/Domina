using Domina.Core.Model;

namespace Domina.Core.Combat;

/// <summary>Bir savaşçının dövüş sırasındaki geçici durumu.</summary>
public enum CombatState
{
    /// <summary>Mesafe alıyor / bir sonraki saldırıyı bekliyor. Kesilebilir.</summary>
    Idle,

    /// <summary>Saldırıya kilitli. <b>Kesilemez</b> — kaçış komutu buffer'lanır.</summary>
    AttackWindup,

    /// <summary>Saldırı sonrası toparlanma. Kesilebilir.</summary>
    AttackRecovery,

    /// <summary>Fırlatma hamlesine kilitli. <b>Kesilemez</b>, tıpkı yakın dövüş vuruşu gibi.</summary>
    ThrowWindup,

    /// <summary>Fırlatma sonrası toparlanma. Kesilebilir.</summary>
    ThrowRecovery,

    /// <summary>
    /// Hücum öncesi birikme: savaşçı yerinde durup güç toplar ve <b>yediği ilk isabetle
    /// hücum dağılır</b>. Savunması normal oranıyla çalışmaya devam eder.
    /// </summary>
    /// <remarks>
    /// Hücumun bedeli burada ödenir — savunmayı kapatarak değil, <b>taahhüdü açıkta
    /// bırakarak</b>: savaşçı yerinden kıpırdamaz, ve yediği tek bir isabet hamleyi
    /// harcatır. Kaçınma hakkı elinden alınmaz; kaçınamadığı darbe hücumunu götürür.
    /// </remarks>
    ChargeWindup,

    /// <summary>
    /// Hedefe hücum ediyor: hızlanmış, taahhütlü. <b>Kesilemez</b> — ama savunması
    /// normal oranıyla sürer.
    /// </summary>
    Charging,

    /// <summary>
    /// Ağır bir darbeyle sersemledi: yürüyemez, vuramaz, <b>kaçınamaz</b>.
    /// </summary>
    /// <remarks>
    /// Künt silahın takasının diğer yarısı (docs/GDD.md §7). Kesilemez ama kendi süresi
    /// bitince savaşçı normal karar döngüsüne döner; buffer'lanmış kaçış komutu da
    /// orada işlenir — sersemletme komutu <b>yutmaz</b>, geciktirir.
    /// </remarks>
    Stunned,

    /// <summary>
    /// Silahı yakalandı: kilitli kaldığı süre boyunca yürüyemez, vuramaz, <b>kaçınamaz</b>.
    /// </summary>
    /// <remarks>
    /// Sersemletmeden ayrı bir durumdur, çünkü sebebi de görüntüsü de ayrıdır: sersemleyen
    /// savaşçı kendi ağırlığıyla sendeler, silahı yakalanan savaşçı <b>karşısındakine
    /// bağlı</b> durur. Tek durumda birleştirilseydi ne ekranda ayrışabilirlerdi ne de
    /// jitte'nin karşılığı ölçümde kendi sayacını taşıyabilirdi.
    /// </remarks>
    WeaponBound,

    /// <summary>Arenadan çıkıyor. Kaçınamaz, bloklayamaz.</summary>
    Retreating,

    /// <summary>Sağ olarak arenadan çıktı.</summary>
    Escaped,

    /// <summary>Öldü.</summary>
    Dead,
}

/// <summary>
/// Dövüşe katılan bir savaşçının çalışma zamanı hali.
/// </summary>
/// <remarks>
/// <see cref="Warrior"/> kalıcı hali tutar; burası yalnızca <b>bu dövüşe</b> ait
/// geçici durumdur. Dövüş bitince kalıcı sonuçlar (ölüm, sakatlık) savaşçıya işlenir.
/// </remarks>
internal sealed class Combatant(Warrior warrior, int team)
{
    public Warrior Warrior { get; } = warrior;

    public int Team { get; } = team;

    public WarriorId Id => Warrior.Id;

    public double Health { get; set; } = warrior.EffectiveStats.MaxHealth;

    public double Stamina { get; set; } = warrior.EffectiveStats.MaxStamina;

    public CombatState State { get; set; } = CombatState.Idle;

    /// <summary>Mevcut durumun bitmesine kalan süre.</summary>
    public double StateTimer { get; set; }

    /// <summary>
    /// Şu an vurmaya çalıştığı düşman. Ölene ya da kaçana kadar korunur
    /// (bkz. <c>Battle.FindTarget</c>).
    /// </summary>
    public Combatant? Target { get; set; }

    /// <summary>Arena düzlemindeki yeri.</summary>
    public ArenaPoint Position { get; set; }

    /// <summary>
    /// Baktığı yön: +1 sağa, -1 sola. Arkadan saldırı bunun üzerinden belirlenir.
    /// </summary>
    public int Facing { get; set; } = 1;

    /// <summary>Bu tick'te ne kadar yol aldı — görselleştirme yürüme döngüsünü buradan sürer.</summary>
    public double SpeedThisTick { get; set; }

    /// <summary>Mevcut duruma girildiğindeki toplam süre.</summary>
    /// <remarks>
    /// Görselleştirme, animasyonu durumun neresinde olunduğuna göre sürer; bunun için
    /// kalan süre tek başına yetmez, toplam süre de gerekir
    /// (bkz. <see cref="CombatantSnapshot.StateProgress"/>).
    /// </remarks>
    public double StateDuration { get; private set; }

    /// <summary>Yeni bir duruma geçer ve sayaçları birlikte kurar.</summary>
    public void BeginState(CombatState state, double duration)
    {
        State = state;
        StateTimer = duration;
        StateDuration = duration;
    }

    /// <summary>Durumun tamamlanma oranı (0-1).</summary>
    public double StateProgress =>
        StateDuration <= 0 ? 1 : Math.Clamp(1 - (StateTimer / StateDuration), 0, 1);

    /// <summary>Oyuncu "çek" dedi mi? Buffer'lanmış olabilir.</summary>
    public bool RetreatRequested { get; set; }

    /// <summary>
    /// Ağır darbede hayatta kalmayı sağlayan koşul: oyuncu müdahale etmiş mi?
    /// Komut verilmişse (henüz kaçış başlamamış olsa bile) sayılır — tuşa basmak
    /// "zamanında müdahale" demektir (bkz. docs/GDD.md §7).
    /// </summary>
    public bool PlayerIntervened => RetreatRequested || State == CombatState.Retreating;

    /// <summary>Hâlâ dövüşe katılıyor mu?</summary>
    public bool IsActive => State is not (CombatState.Dead or CombatState.Escaped);

    /// <summary>
    /// Kaçınma/blok zarı atılabilir mi?
    /// </summary>
    /// <remarks>
    /// <b>Hücum savunmayı kapatmaz.</b> Koşan ya da güç toplayan savaşçı normal oranıyla
    /// kaçınır (docs/GDD.md §4). Sırtını dönüp kaçan dönmez — savunmasızlık kaçışa özgüdür.
    /// </remarks>
    public bool CanDefend => State is not (
        CombatState.Retreating or CombatState.Stunned or CombatState.WeaponBound);

    /// <summary>Kaçış komutu bu durumda anında işlenebilir mi?</summary>
    /// <remarks>
    /// <b>Hücum kaçış komutuyla kesilir.</b> Hücum kendi kararlarına karşı taahhütlüdür —
    /// savaşçı ondan vazgeçip başka bir hamle seçemez — ama oyuncunun "çek" komutu ayrı
    /// bir eksendir. Kesilemez olsaydı komut anında koşmakta olan savaşçı hücumu
    /// bitirmek, düşman hattına varmak ve oradan kaçmaya başlamak zorunda kalırdı;
    /// ölçüldü, bu <b>ilk temasta basmayı geç basmaktan ölümcül</b> yapıp docs/GDD.md
    /// §5'in merdivenini ters çeviriyordu. Kesilse bile hücumun bedeli ödenmiştir: yol
    /// boyunca yenen bedava vuruşlar geri gelmez ve hasar çarpanı harcanmaz.
    /// </remarks>
    public bool IsCancellable =>
        State is CombatState.Idle
            or CombatState.AttackRecovery
            or CombatState.ThrowRecovery
            or CombatState.Charging
            or CombatState.ChargeWindup;

    // ---- Hücum ----

    /// <summary>
    /// Hücumla varılan ilk vuruş henüz çözülmedi: hasar çarpanı bu vuruşta harcanır.
    /// </summary>
    public bool ChargeBonusPending { get; set; }

    /// <summary>Hücumun hedefe vardığı andaki hızı — varış vuruşunun sertliği buradan çıkar.</summary>
    public double ChargeImpactSpeed { get; set; }

    /// <summary>
    /// Hedefin karşı vuruşu tuttu: hücum varır ama <b>momentumunu kaybetmiş</b> olarak.
    /// </summary>
    /// <remarks>
    /// Varış vuruşu yine yapılır, hasar çarpanı kazanılmaz (docs/GDD.md §4). Ayrı bir
    /// bayrak gerekiyor çünkü karşı vuruş yolda, çarpanın kazanıldığı an ise varışta.
    /// </remarks>
    public bool ChargeMomentumBroken { get; set; }

    /// <summary>Mevcut hücumun başlangıcından bu yana geçen süre.</summary>
    public double ChargeSeconds { get; set; }

    /// <summary>
    /// Geçen karar adımında ortada hücuma elverişli bir açıklık var mıydı?
    /// </summary>
    /// <remarks>
    /// Hücum kararı <b>açıklık doğduğu anda bir kez</b> verilir, açıklık sürdükçe her
    /// 0.2 saniyede bir yeniden değil (docs/GDD.md §4). Bu bayrak o "doğduğu an"ı
    /// yakalar. Yoksa hücum sıklığı, savaşçının açıklıkta ne kadar oyalandığına —
    /// yani <b>ters orantılı olarak kendi hızına</b> — bağlı kalırdı.
    /// </remarks>
    public bool SawChargeOpening { get; set; }

    /// <summary>
    /// Bu hücum boyunca hangi düşmanlar bedava vuruşunu kullandı.
    /// </summary>
    /// <remarks>
    /// Fırsat saldırısı hücum başına <b>düşman başına bir kez</b>: aksi halde yanından
    /// geçilen düşman her tick'te vurur ve hücum bir hamle değil bir infaz olurdu.
    /// </remarks>
    public HashSet<WarriorId> ChargeOpportunists { get; } = [];

    /// <summary>Hücumun kalktığı hedef.</summary>
    /// <remarks>
    /// Hücum <b>bu hedefe</b> taahhütlüdür (docs/GDD.md §4). Genel hedef seçiminden ayrı
    /// tutulur: hedef ölürse savaşçı koşarken en yakındakine nişan alamaz, hücum boşa
    /// gider. Aksi halde ıskalama dalı hiç işlemez ve hücum bedelsiz bir hamle olur.
    /// </remarks>
    public WarriorId? ChargeTarget { get; set; }

    /// <summary>Hücum sayaçlarını sıfırlar.</summary>
    public void ClearCharge()
    {
        ChargeBonusPending = false;
        ChargeImpactSpeed = 0;
        ChargeMomentumBroken = false;
        ChargeSeconds = 0;
        ChargeTarget = null;
        ChargeOpportunists.Clear();
    }

    /// <summary>Bekleyen hücum bonusunu okur ve harcar.</summary>
    public bool ConsumeChargeBonus()
    {
        if (!ChargeBonusPending)
        {
            return false;
        }

        ChargeBonusPending = false;
        return true;
    }

    /// <summary>Bu dövüşte kalan mermi. Kalıcı hale dokunulmaz, sayaç burada tutulur.</summary>
    public int ThrowsLeft { get; set; } = warrior.UsableThrown?.Ammo ?? 0;

    /// <summary>Atacak mermisi var mı?</summary>
    public bool CanThrow => ThrowsLeft > 0 && Warrior.UsableThrown is not null;

    // ---- Performans sayaçları (onur hesabını ve toplu simülasyonu besler) ----

    public int AttacksMade { get; set; }

    public int HitsLanded { get; set; }

    public int TimesHit { get; set; }

    public int DodgesPerformed { get; set; }

    public double DamageDealt { get; set; }

    public double DamageTaken { get; set; }

    public bool LostLimb { get; set; }

    /// <summary>Bu dövüşte kaç kez sersemledi.</summary>
    public int TimesStunned { get; set; }

    /// <summary>Kaç düşmanı sersemletti — künt silahın karşılığının ölçüldüğü sayaç.</summary>
    public int StunsInflicted { get; set; }

    /// <summary>Kaç kez gelen silahı yakaladı — jitte/sai'nin karşılığının sayacı.</summary>
    public int CatchesMade { get; set; }

    /// <summary>Kaç kez kendi silahı yakalanıp açıkta kaldı.</summary>
    public int TimesCaught { get; set; }

    /// <summary>Bu dövüşte kaç kez hücuma kalktı.</summary>
    public int ChargesStarted { get; set; }

    /// <summary>Kaç hücum hedefe vardı — başlayanların kaçının karşılığı alındı.</summary>
    public int ChargesConnected { get; set; }

    /// <summary>Hücumları sırasında yediği bedava vuruş sayısı — hücumun ödenen bedeli.</summary>
    public int ChargeOpportunitiesTaken { get; set; }

    /// <summary>Birikme aşamasında dağılan hücum sayısı.</summary>
    public int ChargesBroken { get; set; }

    /// <summary>Hücumların kalkış anlarının toplamı — ortalamayı çıkarmak için.</summary>
    public double ChargeStartSecondsSum { get; set; }

    /// <summary>Bu dövüşte en geç kalkılan hücumun anı.</summary>
    /// <remarks>
    /// Hücumun yalnızca açılış hamlesi mi olduğunu söyleyen sayı budur: dövüş 14 sn
    /// sürerken en geç kalkış 1 sn'deyse mesafe eşiği dövüşün geri kalanında hiç
    /// sağlanmıyor demektir.
    /// </remarks>
    public double LastChargeStartSeconds { get; set; }
}
