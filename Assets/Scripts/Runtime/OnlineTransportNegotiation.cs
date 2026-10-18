using System;

namespace DigitalTennis
{
    // Pure control policy; opening signaling alone is never P2P success.
    public sealed class OnlineTransportNegotiation
    {
        public const double AttemptSeconds=8;
        public uint Epoch {get;private set;}
        public bool LocalReady {get;set;}
        public bool PeerReady {get;set;}
        public bool Committed {get;private set;}
        public string Proposed {get;private set;}
        private bool host,forced;private double deadline;
        public void Begin(bool isHost,uint previousEpoch,double now,bool forceRelay)
        {
            if(previousEpoch==uint.MaxValue||double.IsNaN(now)||double.IsInfinity(now)||now<0)throw new ArgumentException("Negotiation context");
            host=isHost;forced=forceRelay;Epoch=previousEpoch+1;deadline=now+AttemptSeconds;LocalReady=PeerReady=Committed=false;Proposed=null;
        }
        public string Decide(double now)
        {
            if(!host||Committed||Proposed!=null)return null;
            if(!forced&&LocalReady&&PeerReady&&now<deadline)return Proposed="p2p";
            if(forced||now>=deadline)return Proposed="relay";
            return null;
        }
        public bool CanAcknowledge(string mode,uint epoch)=>!Committed&&epoch==Epoch&&(mode=="relay"||mode=="p2p"&&!forced&&LocalReady&&PeerReady);
        public bool Commit(string mode,uint epoch)
        {
            if(!CanAcknowledge(mode,epoch)||host&&Proposed!=mode)return false;
            Committed=true;return true;
        }
    }
}
