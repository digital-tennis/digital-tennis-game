namespace DigitalTennis
{
    /// <summary>Presentation policy shared by offline and buffered guest state. No musical loops during a match.</summary>
    public static class TennisAudioPolicy
    {
        public static bool MusicAllowed(FrontEndStage stage)=>stage!=FrontEndStage.Playing&&stage!=FrontEndStage.Pause;
        public static bool CrowdAllowed(FrontEndStage stage,string phase)=>stage==FrontEndStage.Results||
            ((stage==FrontEndStage.Playing||stage==FrontEndStage.Pause)&&phase==nameof(MatchPhase.PointEnd));
    }
}
