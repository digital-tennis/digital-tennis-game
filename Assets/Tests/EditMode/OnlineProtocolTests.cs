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
                points=new[]{0,0}, ball=new BallState {pos=new[]{0f,1f,3f},vel=new[]{0f,0f,1f}},
                players=new[]{new PlayerState{id="host",pos=new[]{0f,.85f,-9f}},new PlayerState{id="guest",pos=new[]{0f,.85f,9f}}}
            }};
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
    }
}
