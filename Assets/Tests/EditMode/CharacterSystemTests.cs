using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class CharacterSystemTests
    {
        public static string Defaults => File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Balance/characters.csv"));
        public static CharacterState[] States()
        {
            var b = CharacterBalanceCsv.Parse(Defaults);
            return new[] { new CharacterAbility(0, b["KOR"]).Capture(), new CharacterAbility(1, b["JPN"]).Capture() };
        }
        [Test]
        public void FifteenAdultOriginalCharactersHaveDistinctSkillsCourtsAndEqualInitialBudgets()
        {
            var profiles = CharacterCatalog.All; var balances = CharacterBalanceCsv.Parse(Defaults);
            Assert.That(profiles.Length, Is.EqualTo(15));
            Assert.That(profiles.Select(p => p.id), Is.EqualTo(CharacterCatalog.Ids));
            Assert.That(profiles.Select(p => p.skillName).Distinct().Count(), Is.EqualTo(15));
            Assert.That(profiles.Select(p => p.courtCode).Distinct().Count(), Is.EqualTo(15));
            Assert.That(profiles.Select(p => p.style).Distinct().Count(), Is.EqualTo(15));
            foreach (var p in profiles)
            {
                Assert.That(p.age, Is.InRange(21, 50));
                Assert.That(string.IsNullOrWhiteSpace(p.heritage + p.persona + p.costume), Is.False);
                Assert.That(balances[p.id].Ratings.Sum(), Is.EqualTo(420));
            }
        }
        [Test]
        public void GaugeCapsAtThreeAndCannotStoreAHiddenFourthCharge()
        {
            var m = new SkillMeter(); m.Gain(250);
            Assert.That(m.Stock, Is.EqualTo(2)); Assert.That(m.Gauge, Is.EqualTo(50));
            m.Gain(5000); Assert.That(m.Stock, Is.EqualTo(3)); Assert.That(m.Gauge, Is.EqualTo(100));
            m.Gain(50); m.Gain(float.NaN);
            Assert.That(m.Spend(), Is.True); Assert.That(m.Stock, Is.EqualTo(2)); Assert.That(m.Gauge, Is.Zero);
            m.Gain(99); Assert.That(m.Stock, Is.EqualTo(2)); m.Gain(1);
            Assert.That(m.Stock, Is.EqualTo(3)); Assert.That(m.Gauge, Is.EqualTo(100));
            m.Spend(); m.Spend(); m.Spend(); Assert.That(m.Spend(), Is.False);
        }
        [Test]
        public void PartialGaugeSurvivesSpendingAndInvalidGainsDoNothing()
        {
            var m = new SkillMeter(); m.Gain(140); m.Spend();
            Assert.That(m.Gauge, Is.EqualTo(40)); m.Gain(-10); m.Gain(float.PositiveInfinity);
            Assert.That(m.Gauge, Is.EqualTo(40));
        }
        [Test]
        public void EverySkillSpendsOneHasASeparateCinematicAndCannotStackItsOwnBuff()
        {
            var data = CharacterBalanceCsv.Parse(Defaults);
            for (int i = 0; i < 15; i++)
            {
                var a = new CharacterAbility(i, data[CharacterCatalog.Ids[i]]);
                Assert.That(a.Activate(), Is.False);
                for (int hit = 0; hit < 20; hit++) a.Returned(15);
                Assert.That(a.Meter.Stock, Is.EqualTo(3)); Assert.That(a.Activate(), Is.True);
                Assert.That(a.Meter.Stock, Is.EqualTo(2)); Assert.That(a.Cinematic, Is.True); Assert.That(a.Buff, Is.False);
                Assert.That(a.Activate(), Is.False);
                a.Tick(CharacterAbility.CinematicSeconds, false);
                Assert.That(a.Buff, Is.True); Assert.That(a.Capture().IsValid, Is.True);
                Assert.That(a.Activate(), Is.False);
                a.Tick(10, false); Assert.That(a.Remaining, Is.Zero);
                Assert.That(a.Activate(), Is.True);
            }
        }
        [Test]
        public void CsvReloadIsAtomicAndWorksWithoutBuilding()
        {
            string directory = Path.Combine(Path.GetTempPath(), "tennis-balance-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "characters.csv");
            try
            {
                var store = new CharacterBalanceStore(path, Defaults); uint revision = store.Revision;
                File.WriteAllText(path, Defaults.Replace("KOR,70,70", "KOR,88,70"));
                Assert.That(store.Reload(), Is.True); Assert.That(store[0].serve, Is.EqualTo(88)); Assert.That(store.Revision, Is.GreaterThan(revision));
                revision = store.Revision;
                File.WriteAllText(path, Defaults.Replace("KOR,70,70", "KOR,NaN,70"));
                Assert.That(store.Reload(), Is.False); Assert.That(store[0].serve, Is.EqualTo(88)); Assert.That(store.Revision, Is.EqualTo(revision));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        [Test]
        public void CsvSupportsBomQuotesCrLfAndInvariantNumbersButRejectsBadShapeAndBounds()
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                string quoted = "\ufeff" + Defaults.Replace("KOR,", "\"KOR\",");
                Assert.That(CharacterBalanceCsv.Parse(quoted)["KOR"].shotMultiplier, Is.EqualTo(1.10f));
                foreach (var bad in new[] { Defaults.Replace("KOR,", "JPN,"), Defaults.Replace("KOR,70,", "KOR,101,"), Defaults + "EXTRA\n", "\"" + Defaults })
                    Assert.Throws<FormatException>(() => CharacterBalanceCsv.Parse(bad));
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }
        [Test]
        public void SixRatingsHaveObservableGameplayEffectsAndSmashTravelsDownward()
        {
            var d = CharacterBalanceCsv.Parse(Defaults);
            var baseline = new CharacterAbility(0, d["KOR"]); var quick = new CharacterAbility(11, d["NLD"]);
            Assert.That(quick.MoveSpeed, Is.GreaterThan(baseline.MoveSpeed));
            Assert.That(new CharacterAbility(4, d["RUS"]).Reach, Is.GreaterThan(baseline.Reach));
            Assert.That(new CharacterAbility(3, d["USA"]).ServeSpeed, Is.GreaterThan(baseline.ServeSpeed));
            Assert.That(new CharacterAbility(8, d["ESP"]).ShotMultiplier(StrokeKind.Smash), Is.GreaterThan(baseline.ShotMultiplier(StrokeKind.Smash)));
            Assert.That(new CharacterAbility(7, d["GBR"]).ShotMultiplier(StrokeKind.Volley), Is.GreaterThan(baseline.ShotMultiplier(StrokeKind.Volley)));
            var durable = new CharacterAbility(5, d["IND"]); baseline.Moved(20); durable.Moved(20);
            Assert.That(durable.Energy, Is.GreaterThan(baseline.Energy));
            var input = new RacketInput(CourtSide.Near, StrokeKind.Smash, 1, 1, .5f, 0, 0, 1, 1, 1);
            var velocity = RacketShot.CharacterVelocity(input, new Vector3(0, 2.2f, -5), 9.81f, .12f, 1);
            Assert.That(velocity.y, Is.LessThan(0)); Assert.That(velocity.z, Is.GreaterThan(0));
        }
        [Test]
        public void VolleyCannotReturnServeBeforeBounceAndSmashRequiresOverheadContact()
        {
            var m = new TennisMatch(); m.TryServe(CourtSide.Near);
            var ball = new BallSimulation(); ball.Launch(new Vector3(0, 1, 5), Vector3.forward, m.CurrentShot);
            var gate = new RacketInputGate();
            RacketInput I(StrokeKind s, uint id) => new RacketInput(CourtSide.Far, s, 1, id, .35f, 0, 0, 1, m.RallyId, m.CurrentShot.ShotId);
            Assert.That(gate.Evaluate(I(StrokeKind.Volley, 1), 1, m, ball, new Vector3(0, .85f, 5), true), Is.Not.EqualTo(SwingResult.Hit));
            m.HandleBallEvent(new BallEvent(m.CurrentShot, 1, BallEventKind.Bounce, new Vector3(-1, 0, 4), 1, true));
            Assert.That(gate.Evaluate(I(StrokeKind.Smash, 2), 1, m, ball, new Vector3(0, .85f, 5), true), Is.EqualTo(SwingResult.Height));
            m.TryReturn(CourtSide.Far);
            ball.Launch(new Vector3(0, 1, -5), Vector3.back, m.CurrentShot);
            var volley = new RacketInput(CourtSide.Near, StrokeKind.Volley, 1, 3, .35f, 0, 0, 1, m.RallyId, m.CurrentShot.ShotId);
            Assert.That(gate.Evaluate(volley, 1, m, ball, new Vector3(0, .85f, -5), true), Is.EqualTo(SwingResult.Hit));
        }

        [Test]
        public void MalformedRemoteStocksAndBalanceCannotEnterTheSnapshotBuffer()
        {
            var state = States()[0]; Assert.That(state.IsValid, Is.True);
            state.stock = 3; state.gauge = 99; Assert.That(state.IsValid, Is.False);
            state.gauge = 100; Assert.That(state.IsValid, Is.True);
            state.balance.speed = float.NaN; Assert.That(state.IsValid, Is.False);
        }
    }
}
