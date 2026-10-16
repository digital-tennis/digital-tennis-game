namespace DigitalTennis
{
    /// <summary>Both players must consent to the same completed match. Old requests cannot start another round.</summary>
    public sealed class OnlineRematchConsent
    {
        public string MatchId {get;private set;}
        public bool Host {get;private set;}
        public bool Guest {get;private set;}
        private bool consumed;
        public void Reset(string id){MatchId=id;Host=Guest=consumed=false;}
        public void ClearVotes(){Host=Guest=false;}
        public bool Vote(CourtSide player,string matchId,bool wants,bool completed)
        {
            if(!completed||consumed||matchId!=MatchId||string.IsNullOrEmpty(matchId))return false;
            if(player==CourtSide.Near)Host=wants;else if(player==CourtSide.Far)Guest=wants;else return false;
            return true;
        }
        public bool Consume()
        {if(consumed||!Host||!Guest)return false;consumed=true;return true;}
        public bool[] Capture()=>new[]{Host,Guest};
    }
}
