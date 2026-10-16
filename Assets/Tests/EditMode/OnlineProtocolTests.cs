using NUnit.Framework;
using UnityEngine;

namespace DigitalTennis.Tests
{
    public sealed class OnlineProtocolTests
    {
        [TestCase(0u, 0xffffffffu, true)]
        [TestCase(1u, 0xffffffffu, true)]
        [TestCase(8u, 8u, false)]
        [TestCase(7u, 8u, false)]
        [TestCase(0x80000000u, 0u, false)]
        public void SequenceComparisonHandlesWrapAndDuplicate(uint candidate,uint previous,bool expected)
            => Assert.That(Sequence32.IsNewer(candidate,previous),Is.EqualTo(expected));
        private static GameMessage Snapshot(uint tick)
        {
            return new GameMessage { type="snapshot", epoch=1, tick=tick, payload=new GamePayload {
                matchId="test", phase="Rally", serverId="host", serveSide="right", lastHitter="host", rallyId=1,shotId=2,
                points=new[]{0,0},score=MatchScoreState.Capture(new TennisMatchScore(CourtSide.Near,TennisMatchRules.Defaults)),ball=new BallState {pos=new[]{0f,1f,3f},vel=new[]{0f,0f,1f}},
                officials=new OfficialsState {callId=1, text="Play", runner=new[]{6f,0f,-.8f}},
                characters=CharacterSystemTests.States(), courtIndex=0, balanceRevision=1,gameplayHash=GameplayBalance.Defaults.Hash,gameplayValues=GameplayBalance.Defaults.Values,
                players=new[]{new PlayerState{id="host",pos=new[]{0f,.85f,-9f}},new PlayerState{id="guest",pos=new[]{0f,.85f,9f}}}
            }};
        }
        [Test]
        public void ContactAnchorRequiresAValidAcceptedSwingAndCannotContainNonFiniteCoordinates()
        {
            var message=Snapshot(10);var p=message.payload.players[0];p.swingId=1;p.stroke=1;p.swingContact=true;p.hasContactPoint=true;p.contactPoint=new[]{1f,1.2f,-8f};
            Assert.That(message.payload.IsSnapshotValid,Is.True);p.contactPoint[0]=float.NaN;Assert.That(message.payload.IsSnapshotValid,Is.False);
            p.contactPoint=new[]{1f,1f};Assert.That(message.payload.IsSnapshotValid,Is.False);
            p.contactPoint=new[]{1f,1f,-8f};p.swingContact=false;Assert.That(message.payload.IsSnapshotValid,Is.False);
        }
        [Test]
        public void NewMatchKeepsTickFencingAndDropsOldFrames()
        {
            var buffer=new SnapshotBuffer();var first=Snapshot(20);Assert.That(buffer.Push(first,1),Is.True);
            var next=Snapshot(21);next.payload.matchId="new-match";next.payload.rematchVotes=new[]{false,false};Assert.That(buffer.Push(next,2),Is.True);
            Assert.That(buffer.Latest.matchId,Is.EqualTo("new-match"));Assert.That(buffer.Push(first,3),Is.False);
            var malformed=Snapshot(22);malformed.payload.rematchVotes=new[]{true};Assert.That(buffer.Push(malformed,3),Is.False);
        }
        [Test]
        public void SnapshotBufferRejectsDuplicateOldMalformedAndNonFiniteState()
        {
            var buffer=new SnapshotBuffer();
            Assert.That(buffer.Push(Snapshot(0xffffffff),1),Is.True);
            Assert.That(buffer.Push(Snapshot(0),1.02),Is.True);
            Assert.That(buffer.Push(Snapshot(0),1.03),Is.False);
            Assert.That(buffer.Push(Snapshot(0xffffffff),1.03),Is.False);
            var bad=Snapshot(1);bad.payload.ball.pos[0]=float.NaN;
            Assert.That(buffer.Push(bad,1.03),Is.False);
            bad=Snapshot(1);bad.payload.points=new[]{-1,0};Assert.That(buffer.Push(bad,1.03),Is.False);
            bad=Snapshot(1);bad.payload.players[1].id="host";Assert.That(buffer.Push(bad,1.03),Is.False);
            Assert.That(buffer.Received,Is.EqualTo(2));
        }
        [Test]
        public void SnapshotValidatesOfficialsAndRecoveryState()
        {
            var s = Snapshot(1);
            s.payload.officials = new OfficialsState { callId = 2, call = (int)UmpireCall.Fault, text = "Fault - second serve",
                recovery = (int)RecoveryPhase.PickingUp, runner = new[] { 0f, 0f, -.35f }, completed = 0 };
            Assert.That(s.payload.IsSnapshotValid, Is.True);
            var copy = JsonUtility.FromJson<GameMessage>(JsonUtility.ToJson(s));
            Assert.That(copy.payload.officials.text, Is.EqualTo(s.payload.officials.text));
            s.payload.officials.runner[0] = float.NaN;
            Assert.That(s.payload.IsSnapshotValid, Is.False);
            s.payload.officials.runner[0] = 0;
            s.payload.officials.recovery = 42;
            Assert.That(s.payload.IsSnapshotValid, Is.False);
        }

        [Test]
        public void WireEnvelopeRoundTripsTypedNestedMessage()
        {
            var envelope=new WireEnvelope<RoomRequest>{type="game.relay",requestId="u1",payload=new RoomRequest{epoch=1,message=Snapshot(5)}};
            string json=JsonUtility.ToJson(envelope);
            var parsed=JsonUtility.FromJson<WireEnvelope<RoomRequest>>(json);
            Assert.That(parsed.v,Is.EqualTo(1));Assert.That(parsed.payload.message.payload.IsSnapshotValid,Is.True);
            Assert.That(parsed.payload.message.tick,Is.EqualTo(5));
            var response="{\"v\":1,\"type\":\"relay.message\",\"requestId\":null,\"payload\":{\"from\":\"host\",\"epoch\":1,\"message\":"+JsonUtility.ToJson(Snapshot(6))+"}}";
            Assert.That(JsonUtility.FromJson<WireEnvelope<ServerMessagePayload>>(response).payload.message.tick,Is.EqualTo(6));
        }
        [Test]
        public void CosmeticHistoryRoundTripsAndRejectsMalformedOrUnboundedRecords()
        {
            var snapshot=Snapshot(1);var journal=new TennisEffectJournal();journal.Add(TennisEffectKind.Hit,CourtSide.Near,0,StrokeKind.Smash,Vector3.one,Vector3.forward,0);
            snapshot.payload.effectSequence=journal.Sequence;snapshot.payload.effects=journal.Capture(.1f);
            var copy=JsonUtility.FromJson<GameMessage>(JsonUtility.ToJson(snapshot));Assert.That(copy.payload.IsSnapshotValid,Is.True);
            copy.payload.effects[0].age=float.NaN;Assert.That(copy.payload.IsSnapshotValid,Is.False);
            copy.payload.effects=new TennisEffectEvent[17];Assert.That(copy.payload.IsSnapshotValid,Is.False);
        }
        [Test]
        public void MatchSnapshotsRejectPrematureWinnersAndInconsistentSets()
        {
            var state=Snapshot(1).payload;Assert.That(state.IsSnapshotValid,Is.True);
            state.winner="host";Assert.That(state.IsSnapshotValid,Is.False);state.winner=null;
            state.score=null;Assert.That(state.IsSnapshotValid,Is.False);
            state.score=MatchScoreState.Capture(new TennisMatchScore(CourtSide.Near,TennisMatchRules.Defaults));
            state.score.sets[0]=1;Assert.That(state.IsSnapshotValid,Is.False);state.score.sets[0]=0;
            state.score.games=new[]{6,6};Assert.That(state.IsSnapshotValid,Is.False);
            state.score.tieBreak=true;Assert.That(state.IsSnapshotValid,Is.True);
            state.score.rules=new[]{3,6,5,7};Assert.That(state.IsSnapshotValid,Is.False);
        }

        [Test]
        public void CompleteMatchScoreRoundTripsWithHistoryAndEndChanges()
        {
            var score=new TennisMatchScore(CourtSide.Near,TennisMatchRules.Defaults);uint rally=0;
            while(!score.Winner.HasValue){score.TryAward(++rally,CourtSide.Near);score.BeginNextPoint();}
            var state=Snapshot(1).payload;state.phase="MatchEnd";state.winner="host";state.points=new[]{4,0};
            state.score=MatchScoreState.Capture(score);state.serverId=score.Server==CourtSide.Near?"host":"guest";
            var copy=JsonUtility.FromJson<GamePayload>(JsonUtility.ToJson(state));
            Assert.That(copy.IsSnapshotValid,Is.True);Assert.That(copy.score.sets,Is.EqualTo(new[]{2,0}));
            Assert.That(copy.score.completedSets[1].Display(),Is.EqualTo("6–0"));Assert.That(copy.score.endChanges,Is.EqualTo(6));
            copy.score.completedSets[0].nearGames=7;copy.score.completedSets[0].farGames=4;
            Assert.That(copy.IsSnapshotValid,Is.False);
        }
    }
}
