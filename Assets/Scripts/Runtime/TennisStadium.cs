using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Cosmetic stadium. Reads match state; never owns colliders, physics or gameplay.</summary>
    public sealed class TennisStadium : MonoBehaviour
    {
        private Renderer[] seats, trims, apron;
        private TextMesh[] boards, venueNames;
        private TennisMatchController controller;
        [SerializeField] private int spectatorCount, seatCount;
        private MaterialPropertyBlock colors;
        private float refreshAt;
        private string lastScore;
        public int SpectatorCount => spectatorCount;
        public int SeatCount => seatCount;
        private void Awake()
        {
            controller=UnityEngine.Object.FindFirstObjectByType<TennisMatchController>();
            seats=new[]{transform.Find("Individual Stadium Seats").GetComponent<Renderer>()};
            trims=new[]{transform.Find("National Accent Bands").GetComponent<Renderer>()};
            apron=new[]{transform.Find("Outer Plaza").GetComponent<Renderer>()};
            boards=new[]{transform.Find("Live Score -1").GetComponent<TextMesh>(),transform.Find("Live Score 1").GetComponent<TextMesh>()};
            venueNames=new[]{transform.Find("Venue Name -1").GetComponent<TextMesh>(),transform.Find("Venue Name 1").GetComponent<TextMesh>()};
        }
        public void Configure(CharacterProfile profile)
        {
            Tint(seats,Color.Lerp(CharacterProfile.ColorOf(profile.secondary),new Color(.12f,.2f,.26f),.45f));
            Tint(trims,Color.Lerp(CharacterProfile.ColorOf(profile.accent),Color.white,.12f));
            Tint(apron,Color.Lerp(CharacterProfile.ColorOf(profile.sky),new Color(.12f,.19f,.19f),.68f));
            foreach(var label in venueNames)label.text=profile.id+"  /  "+profile.courtCode+"\nDIGITAL TENNIS  ·  WORLD TOUR";
            lastScore=null;
        }
        private void Tint(Renderer[] renderers,Color color)
        {
            if(colors==null)colors=new MaterialPropertyBlock();
            colors.SetColor("_Color",color);
            foreach(var r in renderers)r.SetPropertyBlock(colors);
        }
        private void LateUpdate()
        {
            if(controller?.Match==null||Time.unscaledTime<refreshAt)return;
            refreshAt=Time.unscaledTime+.15f;
            var session=controller.GetComponent<OnlineTennisSession>();
            var state=controller.Ball.IsAuthoritative?null:session?.PresentedState;
            if(!controller.Ball.IsAuthoritative&&state==null)return;
            var tally=state?.score??MatchScoreState.Capture(controller.Match.Progress);
            var points=state?.points??new[]{controller.Match.Score.NearPoints,controller.Match.Score.FarPoints};
            string a=CharacterCatalog.Get(controller.Characters.Player(CourtSide.Near).Index).id;
            string b=CharacterCatalog.Get(controller.Characters.Player(CourtSide.Far).Index).id;
            string text=a+"   "+tally.PointDisplay(points,0)+" : "+tally.PointDisplay(points,1)+"   "+b+
                "\nSETS "+tally.sets[0]+" : "+tally.sets[1]+"    GAMES "+tally.games[0]+" : "+tally.games[1];
            if(text==lastScore)return;lastScore=text;
            foreach(var label in boards)label.text=text;
        }
    }
}
