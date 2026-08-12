using Domina.Core.Combat;
using Domina.Core.Model;
using Domina.Core.Rng;

namespace Domina.Presentation.Tests;

/// <summary>
/// Faz 2'nin kabul kriteri: <b>bir seed verildiğinde dövüş baştan sona izlenebiliyor ve
/// olaylar ile ekranda görünen birebir tutuyor</b> (uzuv kopan savaşçı ekranda da kopuk).
/// </summary>
/// <remarks>
/// Buradaki oynatma, <c>BattleArena._Process</c>'in motorsuz ikizidir: aynı sırayla
/// dövüşü adımlar, olayları tepkiye çevirir ve konumları hesaplar. Godot'suz koşabiliyor
/// olması sunum mantığının motordan ayrılmış olmasının kanıtı — ayrım bozulursa bu dosya
/// derlenmez.
/// </remarks>
public class ArenaPlaybackTests
{
    /// <summary>Oyuncunun tuşunun yerine geçen eşik: canı bu oranın altına düşen ekibi çeker.</summary>
    private const double _pullOutBelow = 0.45;

    private sealed record Playback(
        BattleResult Result,
        List<RigReaction> Reactions,
        Dictionary<WarriorId, ScenePoint> FinalPositions,
        Dictionary<WarriorId, CombatState> FinalStates);

    /// <summary>Bir dövüşü baştan sona "izler" ve ekranda olan biteni toplar.</summary>
    private static Playback Watch(long seed, bool pullOut = true)
    {
        BattleSetup setup = DemoRoster.Setup();
        var battle = new Battle(setup, new SeededRandom((ulong)seed));
        var choreography = new ArenaChoreography(new ArenaLayout());
        var reader = new ReactionReader();

        for (int i = 0; i < setup.PlayerSide.Count; i++)
        {
            choreography.Place(setup.PlayerSide[i].Id, Battle.PlayerTeam, i);
        }

        for (int i = 0; i < setup.EnemySide.Count; i++)
        {
            choreography.Place(setup.EnemySide[i].Id, Battle.EnemyTeam, i);
        }

        List<RigReaction> reactions = [];
        Dictionary<WarriorId, ScenePoint> positions = [];
        Dictionary<WarriorId, CombatState> states = [];
        bool commanded = false;

        while (true)
        {
            bool running = battle.Step();

            reactions.AddRange(reader.Drain(battle.Events));

            IReadOnlyList<CombatantSnapshot> snapshots = battle.Snapshots();
            foreach (CombatantSnapshot snapshot in snapshots)
            {
                positions[snapshot.Id] = choreography.PositionFor(snapshot, snapshots);
                states[snapshot.Id] = snapshot.State;

                // Oyuncunun tuşu: ekip yaralanınca çekiliyor. Uzuv kaybı yalnızca
                // zamanında müdahale edilen dövüşlerde oluşur (GDD §7).
                if (pullOut
                    && !commanded
                    && snapshot.Team == Battle.PlayerTeam
                    && snapshot.HealthFraction is > 0 and < _pullOutBelow)
                {
                    commanded = true;
                    battle.CommandRetreat();
                }
            }

            if (!running)
            {
                return new Playback(battle.Result!, reactions, positions, states);
            }
        }
    }

    /// <summary>
    /// Aranan durumu (ölüm, kaçış, uzuv kaybı) üreten ilk seed'i bulur.
    /// </summary>
    /// <remarks>
    /// Seed sabitlenmiyor: denge sayıları Faz 9'da ayarlanacak ve sabitlenmiş bir seed
    /// o gün sessizce anlamını yitirirdi — test yeşil kalır ama artık bir şey sınamaz.
    /// Aranarak bulunan seed her koşuda aynı, çünkü dövüş deterministik.
    /// </remarks>
    private static Playback WatchUntil(string looking, Func<Playback, bool> until)
    {
        for (long seed = 1; seed <= 400; seed++)
        {
            Playback playback = Watch(seed);

            if (until(playback))
            {
                return playback;
            }
        }

        throw new InvalidOperationException(
            $"400 seed içinde {looking} bulunamadı — denge sayıları mı değişti?");
    }

    /// <summary>
    /// Kabul kriterinin özü: bilançoda uzvunu kaybettiği yazan savaşçı ekranda da
    /// kopmuş olmalı, üstelik <b>aynı uzuv</b>.
    /// </summary>
    [Fact]
    public void WhatTheSummaryReportsIsWhatTheScreenShows()
    {
        Playback playback = WatchUntil(
            "uzuv kaybı",
            p => p.Reactions.Exists(r => r.Kind == RigReactionKind.Dismember));

        foreach (WarriorBattleSummary summary in playback.Result.Summaries)
        {
            List<RigReaction> severed = playback.Reactions
                .FindAll(r => r.Warrior == summary.Id && r.Kind == RigReactionKind.Dismember);

            if (!summary.LostLimb)
            {
                Assert.Empty(severed);
                continue;
            }

            Assert.Equal(summary.LostPart, Assert.Single(severed).Part);
        }
    }

    /// <summary>Aynı seed aynı dövüşü verir — ekranda görünen de dahil.</summary>
    [Fact]
    public void TheSameSeedIsWatchedTheSameWayTwice()
    {
        Playback first = Watch(20260806);
        Playback second = Watch(20260806);

        Assert.Equal(first.Result.Outcome, second.Result.Outcome);
        Assert.Equal(first.Result.ElapsedSeconds, second.Result.ElapsedSeconds, 6);
        Assert.Equal(first.Reactions, second.Reactions);
        Assert.Equal(first.FinalPositions, second.FinalPositions);
    }

    /// <summary>Ölen savaşçı düştüğü yerde kalır; hattına geri ışınlanmaz.</summary>
    [Fact]
    public void TheDeadRestWhereTheyFellInsideTheFrame()
    {
        var layout = new ArenaLayout();
        Playback playback = WatchUntil("ölü", p => p.FinalStates.ContainsValue(CombatState.Dead));

        foreach ((WarriorId id, CombatState state) in playback.FinalStates)
        {
            if (state != CombatState.Dead)
            {
                continue;
            }

            // Ceset ne kaçış yoluna savrulur ne de kadrajın dışına düşer.
            ScenePoint spot = playback.FinalPositions[id];
            Assert.InRange(spot.X, 0, layout.Width);
            Assert.Equal(layout.GroundY, spot.Y);
        }
    }

    /// <summary>Arenadan sağ çıkan savaşçı gizlenmeden önce kadrajı gerçekten terk eder.</summary>
    [Fact]
    public void TheEscapedLeaveTheFrame()
    {
        var layout = new ArenaLayout();
        Playback playback = WatchUntil("kaçan", p => p.FinalStates.ContainsValue(CombatState.Escaped));

        foreach ((WarriorId id, CombatState state) in playback.FinalStates)
        {
            if (state != CombatState.Escaped)
            {
                continue;
            }

            float x = playback.FinalPositions[id].X;
            Assert.True(x < 0 || x > layout.Width, $"{id} kadrajın içinde kayboldu: {x}");
        }
    }

    /// <summary>
    /// Fırsat saldırısı her kaçışın bedelidir; ekranda karşılığı olmayan bir bedel
    /// oyuncuya "tuşa bastım, sonra canım gitti" olarak görünür.
    /// </summary>
    [Fact]
    public void EveryOpportunityAttackReachesTheScreen()
    {
        Playback playback = WatchUntil("kaçış", p => p.Reactions.Exists(
            r => r.Kind == RigReactionKind.OpportunitySwing));

        int swings = playback.Reactions.Count(r => r.Kind == RigReactionKind.OpportunitySwing);

        // Her savaşçı en fazla bir kez kaçmaya başlar, dolayısıyla bedava vuruş da
        // savaşçı başına en fazla bir tanedir.
        Assert.InRange(swings, 1, playback.Result.Summaries.Count(s => s.Team == Battle.PlayerTeam));
    }

    /// <summary>Müdahale edilmeyen dövüşte uzuv kaybı olmaz, yalnızca ölüm olur (GDD §7).</summary>
    [Fact]
    public void NobodyIsMaimedWhenTheButtonIsNeverPressed()
    {
        for (long seed = 1; seed <= 40; seed++)
        {
            Playback playback = Watch(seed, pullOut: false);

            Assert.DoesNotContain(playback.Reactions, r => r.Kind == RigReactionKind.Dismember);
        }
    }
}
