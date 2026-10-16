namespace DigitalTennis
{
    public enum TennisMusicMood{Normal,Deuce,Pressure}
    public static class TennisMusicSituation
    {
        public static bool MatchPoint(MatchScoreState state,int[] points)
        {
            if(state.gameComplete||state.setComplete)return false;
            var rules=TennisMatchRules.FromValues(state.rules);
            for(int player=0;player<2;player++)
            {
                bool pointWinsGame=state.tieBreak?points[player]>=rules.TieBreakPoints-1&&points[player]>points[1-player]:points[player]>=3&&points[player]>points[1-player];
                bool gameWinsSet=state.tieBreak||state.games[player]>=rules.GamesPerSet-1&&state.games[player]>state.games[1-player];
                if(pointWinsGame&&gameWinsSet&&state.sets[player]==rules.SetsToWin-1)return true;
            }
            return false;
        }
        public static TennisMusicMood Classify(MatchScoreState state,int[] points)
        {
            if(MatchPoint(state,points))return TennisMusicMood.Pressure;
            if(!state.tieBreak&&points[0]>=3&&points[0]==points[1])return TennisMusicMood.Deuce;
            int receiver=state.gameServer=="host"?1:0;
            bool breakPoint=!state.tieBreak&&points[receiver]>=3&&points[receiver]>points[1-receiver];
            return state.tieBreak||breakPoint?TennisMusicMood.Pressure:TennisMusicMood.Normal;
        }
    }
}
