using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>A format is frozen when a match starts. Rule constants are not character balance.</summary>
    public sealed class TennisMatchRules
    {
        public int BestOfSets { get; }
        public int SetsToWin => BestOfSets / 2 + 1;
        public int GamesPerSet { get; }
        public int TieBreakAt { get; }
        public int TieBreakPoints { get; }
        public static TennisMatchRules Defaults => Parse(File.ReadAllText(System.IO.Path.Combine(Application.streamingAssetsPath,"Balance","rules.csv")));

        public TennisMatchRules(int bestOfSets, int gamesPerSet, int tieBreakAt, int tieBreakPoints)
        {
            if ((bestOfSets != 3 && bestOfSets != 5) || (gamesPerSet != 4 && gamesPerSet != 6) ||
                (tieBreakAt != gamesPerSet && !(gamesPerSet == 4 && tieBreakAt == 3)) ||
                (tieBreakPoints != 7 && tieBreakPoints != 10)) throw new FormatException("Invalid tennis match format.");
            BestOfSets=bestOfSets;GamesPerSet=gamesPerSet;TieBreakAt=tieBreakAt;TieBreakPoints=tieBreakPoints;
        }
        public int[] Values => new[]{BestOfSets,GamesPerSet,TieBreakAt,TieBreakPoints};
        public static TennisMatchRules FromValues(int[] v)
        {
            if(v==null||v.Length!=4)throw new FormatException("Missing tennis match format.");
            return new TennisMatchRules(v[0],v[1],v[2],v[3]);
        }
        public static TennisMatchRules Parse(string text)
        {
            var rows=CharacterBalanceCsv.ReadRows(text.TrimStart('\ufeff'));
            if(rows.Count!=5||string.Join(",",rows[0])!="key,value,description")throw new FormatException("rules.csv: invalid header / row count.");
            var data=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var row in rows.Skip(1))
            {
                if(row.Count!=3||data.ContainsKey(row[0])||!int.TryParse(row[1],NumberStyles.None,CultureInfo.InvariantCulture,out var value))
                    throw new FormatException("rules.csv: duplicate / non-integer value.");
                data.Add(row[0],value);
            }
            int Get(string key) => data.TryGetValue(key,out var v)?v:throw new FormatException("rules.csv: missing "+key);
            return new TennisMatchRules(Get("best_of_sets"),Get("games_per_set"),Get("tie_break_at"),Get("tie_break_points"));
        }
    }

    public sealed class TennisRulesStore
    {
        public string Path { get; }
        public string Status { get; private set; }
        public TennisMatchRules NextMatch { get; private set; }
        public TennisRulesStore(string path,string defaults)
        {
            Path=path;NextMatch=TennisMatchRules.Parse(defaults);
            try { if(!File.Exists(path)){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));File.WriteAllText(path,defaults);} Reload(); }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){Status="기본 경기 규칙 사용: "+e.Message;}
        }
        public bool Reload()
        {
            try {var candidate=TennisMatchRules.Parse(File.ReadAllText(Path));NextMatch=candidate;Status="규칙 CSV 읽기 완료 · 새 경기부터 적용";return true;}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is FormatException){Status="규칙 적용 실패 · 이전 설정 유지: "+e.Message;return false;}
        }
        public static TennisRulesStore Open()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-dt-rules-file");
            string path=i>=0&&i+1<args.Length?args[i+1]:System.IO.Path.Combine(Application.persistentDataPath,"Balance","rules.csv");
            return new TennisRulesStore(path,File.ReadAllText(System.IO.Path.Combine(Application.streamingAssetsPath,"Balance","rules.csv")));
        }
    }

    [Serializable] public sealed class CompletedTennisSet
    {
        public int nearGames,farGames,nearTieBreak,farTieBreak;
        public CourtSide Winner => nearGames>farGames?CourtSide.Near:CourtSide.Far;
        public string Display(bool reverse=false) => (reverse?farGames:nearGames)+"–"+(reverse?nearGames:farGames)+
            (nearTieBreak+farTieBreak>0?" (TB "+(reverse?farTieBreak:nearTieBreak)+"–"+(reverse?nearTieBreak:farTieBreak)+")":"");
    }

    /// <summary>Pure singles match scoring, service order and end changes. Player IDs never swap.</summary>
    public sealed class TennisMatchScore
    {
        private uint lastAwardedRally;
        private CourtSide gameServer;
        private bool changeEndsPending;
        private readonly List<CompletedTennisSet> history=new List<CompletedTennisSet>();
        public TennisMatchRules Rules { get; }
        public TennisScore Points { get; private set; }
        public int NearGames { get; private set; }
        public int FarGames { get; private set; }
        public int NearSets { get; private set; }
        public int FarSets { get; private set; }
        public long NearTotalPoints { get; private set; }
        public long FarTotalPoints { get; private set; }
        public long TotalPoints => NearTotalPoints+FarTotalPoints;
        public bool TieBreak { get; private set; }
        public bool GameComplete { get; private set; }
        public bool SetComplete { get; private set; }
        public CourtSide? Winner { get; private set; }
        public CourtSide? LastGameWinner { get; private set; }
        public int EndChanges { get; private set; }
        public CourtSide GameServer => gameServer;
        public bool EndsChanged => EndChanges%2!=0;
        public CompletedTennisSet[] CompletedSets => history.Select(s=>new CompletedTennisSet {
            nearGames=s.nearGames,farGames=s.farGames,nearTieBreak=s.nearTieBreak,farTieBreak=s.farTieBreak }).ToArray();
        public CourtSide Server => !TieBreak||Points.TotalPoints==0||((Points.TotalPoints+1)/2)%2==0?gameServer:TennisMatch.Opponent(gameServer);
        public CourtSide EndOf(CourtSide player) => EndsChanged?TennisMatch.Opponent(player):player;
        public string Display(CourtSide player) => TieBreak?(player==CourtSide.Near?Points.NearPoints:Points.FarPoints).ToString(CultureInfo.InvariantCulture):Points.Display(player);
        public string Summary => "세트 "+NearSets+"–"+FarSets+"  |  "+string.Join(" / ",history.Select(s=>s.Display()));

        public TennisMatchScore(CourtSide firstServer,TennisMatchRules rules,TennisScore initial=default)
        {
            TennisScore.RequirePlayer(firstServer);Rules=rules??throw new ArgumentNullException(nameof(rules));
            if(initial.Winner.HasValue)throw new ArgumentException("Initial game is already complete.");
            gameServer=firstServer;Points=initial;NearTotalPoints=initial.NearPoints;FarTotalPoints=initial.FarPoints;
        }
        public bool TryAward(uint rallyId,CourtSide player)
        {
            if(rallyId==0)throw new ArgumentOutOfRangeException(nameof(rallyId));
            TennisScore.RequirePlayer(player);
            if(Winner.HasValue||GameComplete||rallyId<=lastAwardedRally)return false;
            lastAwardedRally=rallyId;
            Points=TieBreak?new TennisScore(checked(Points.NearPoints+(player==CourtSide.Near?1:0)),checked(Points.FarPoints+(player==CourtSide.Far?1:0))):Points.Award(player);
            if(player==CourtSide.Near)NearTotalPoints++;else FarTotalPoints++;
            bool won=TieBreak?Math.Max(Points.NearPoints,Points.FarPoints)>=Rules.TieBreakPoints&&Math.Abs((long)Points.NearPoints-Points.FarPoints)>=2:Points.Winner.HasValue;
            if(!won){if(TieBreak&&Points.TotalPoints%6==0)changeEndsPending=true;return true;}
            LastGameWinner=player;GameComplete=true;
            if(player==CourtSide.Near)NearGames++;else FarGames++;
            changeEndsPending=(NearGames+FarGames)%2!=0;
            SetComplete=TieBreak||(Math.Max(NearGames,FarGames)>=Rules.GamesPerSet&&Math.Abs(NearGames-FarGames)>=2);
            if(SetComplete)
            {
                history.Add(new CompletedTennisSet{nearGames=NearGames,farGames=FarGames,nearTieBreak=TieBreak?Points.NearPoints:0,farTieBreak=TieBreak?Points.FarPoints:0});
                if(player==CourtSide.Near)NearSets++;else FarSets++;
                if(Math.Max(NearSets,FarSets)==Rules.SetsToWin)Winner=player;
            }
            return true;
        }
        public void BeginNextPoint()
        {
            if(Winner.HasValue)return;
            if(changeEndsPending){EndChanges++;changeEndsPending=false;}
            if(!GameComplete)return;
            // A tie-break counts as one game: its first server receives in the next set.
            gameServer=TennisMatch.Opponent(gameServer);Points=default;
            if(SetComplete){NearGames=FarGames=0;}
            TieBreak=NearGames==Rules.TieBreakAt&&FarGames==Rules.TieBreakAt;
            GameComplete=SetComplete=false;LastGameWinner=null;
        }
    }
}
