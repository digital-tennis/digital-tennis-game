using System;
using System.Collections.Generic;
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
        public void NarrativesPreserveNamesAndLinkPersonalConflictsToExistingSkillsAndCourts()
        {
            string[] names={"한서윤","아오야마 렌","린웨","마야 브룩스","니콜라이 소콜로프","아라브 라오","레나 베버","테오 베넷","루시아 세라노","카미유 로랑","카이 모건","노르 더프리스","하파엘 코스타","살마 암라니","타보 은들로부"};
            Assert.That(CharacterCatalog.All.Select(p=>p.localName),Is.EqualTo(names));
            var incidents=new HashSet<string>();var lines=new HashSet<string>();
            foreach(var p in CharacterCatalog.All)
            {
                var n=p.narrative;Assert.That(n,Is.Not.Null,p.id);
                foreach(var field in typeof(CharacterNarrative).GetFields())
                    Assert.That(string.IsNullOrWhiteSpace((string)field.GetValue(n)),Is.False,p.id+" / "+field.Name);
                Assert.That(incidents.Add(n.formativeIncident),Is.True,"Repeated origin: "+p.id);
                Assert.That(lines.Add(n.voiceIdentity),Is.True,"Repeated voice: "+p.id);
                var related=CharacterCatalog.All.Single(other=>other.id==n.relationshipId);
                Assert.That(related.id,Is.Not.EqualTo(p.id));
                Assert.That(n.relationship,Does.Contain(related.localName),"Relationship must name a real roster peer.");
                Assert.That(n.skillIdentity,Does.Contain(p.skillName));
                Assert.That(n.courtIdentity,Does.Contain(p.courtName));
                Assert.That(n.musicIdentity,Does.Contain("메뉴·소개·결과"));
                Assert.That(n.Page(0),Does.Contain(n.formativeIncident).And.Contain(n.motivationStakes));
                Assert.That(n.Page(1),Does.Contain(n.flaw).And.Contain(n.growth).And.Contain(n.relationship));
                Assert.That(n.Page(2),Does.Contain(n.voiceIdentity).And.Contain(n.accessoryIdentity));
                Assert.Throws<ArgumentOutOfRangeException>(()=>n.Page(3));
            }
            // Guard the central KOR/JPN arc against swapping or reducing it to generic national tropes.
            Assert.That(CharacterCatalog.Get(0).narrative.formativeIncident,Does.Contain("휠체어").And.Contain("출입 동선"));
            Assert.That(CharacterCatalog.Get(1).narrative.formativeIncident,Does.Contain("습기").And.Contain("종이"));
            Assert.That(CharacterCatalog.Get(0).narrative.relationshipId,Is.EqualTo("JPN"));
            Assert.That(CharacterCatalog.Get(1).narrative.relationshipId,Is.EqualTo("KOR"));
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
