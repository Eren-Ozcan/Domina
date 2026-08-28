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
    /// Hedefe hücum ediyor: hızlanmış, taahhütlü. <b>Kesilemez</b>, kaçınamaz, bloklayamaz.
    /// </summary>
    Charging,

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
    /// Kaçınma ve blok yalnızca çekilmiyor <b>ve hücum etmiyorken</b> mümkündür.
    /// </summary>
    /// <remarks>
    /// Hücumun bedeli budur: hızın karşılığında savunmayı bırakırsın (docs/GDD.md §4).
    /// </remarks>
    public bool CanDefend => State is not (CombatState.Retreating or CombatState.Charging);

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
            or CombatState.Charging;

    // ---- Hücum ----

    /// <summary>
    /// Hücumla varılan ilk vuruş henüz çözülmedi: hasar çarpanı bu vuruşta harcanır.
    /// </summary>
    public bool ChargeBonusPending { get; set; }

    /// <summary>Mevcut hücumun başlangıcından bu yana geçen süre.</summary>
    public double ChargeSeconds { get; set; }

    /// <summary>
    /// Bu hücum boyunca hangi düşmanlar bedava vuruşunu kullandı.
    /// </summary>
    /// <remarks>
    /// Fırsat saldırısı hücum başına <b>düşman başına bir kez</b>: aksi halde yanından
    /// geçilen düşman her tick'te vurur ve hücum bir hamle değil bir infaz olurdu.
    /// </remarks>
    public HashSet<WarriorId> ChargeOpportunists { get; } = [];

    /// <summary>Hücum sayaçlarını sıfırlar.</summary>
    public void ClearCharge()
    {
        ChargeBonusPending = false;
        ChargeSeconds = 0;
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
}
