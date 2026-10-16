using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Country-inspired visual themes; lines, bounds, net and ball physics stay identical.</summary>
    public sealed class NationalCourtView : MonoBehaviour
    {
        [SerializeField] private Renderer court, surround;
        [SerializeField] private Transform[] scenery;
        [SerializeField] private LineRenderer[] outlines;
        private TennisStadium stadium;
        private Material courtMaterial, surroundMaterial;
        private readonly System.Collections.Generic.List<Material> propMaterials = new System.Collections.Generic.List<Material>();
        public int Theme { get; private set; }
        public void Configure(CharacterProfile p)
        {
            Theme = p.style;
            if (courtMaterial == null) { courtMaterial = court.material; surroundMaterial = surround.material; }
            courtMaterial.color = CharacterProfile.ColorOf(p.courtColor);
            surroundMaterial.color = Color.Lerp(CharacterProfile.ColorOf(p.sky), Color.black, .25f);
            GetComponent<CourtAtmosphere>()?.Configure(p,courtMaterial);
            Camera.main.backgroundColor = Color.Lerp(CharacterProfile.ColorOf(p.sky), Color.black, .6f);
            if(stadium==null)stadium=UnityEngine.Object.FindFirstObjectByType<TennisStadium>();
            if(stadium!=null)
            {
                // The earlier abstract perimeter was a placeholder, replaced by a complete venue.
                foreach(var prop in scenery)prop.gameObject.SetActive(false);
                foreach(var line in outlines)line.enabled=false;
                stadium.Configure(p);return;
            }
            // A unique perimeter rhythm for each court. All props remain outside the run-off area.
            for (int i = 0; i < scenery.Length; i++)
            {
                float x = -8 + i * 2;
                float height = .9f + ((i * (p.style + 3) + p.style) % 5) * .45f;
                scenery[i].localPosition = new Vector3(x, height / 2, 16f + (i % 2) * .5f);
                scenery[i].localScale = new Vector3(1.4f, height, .7f + p.style % 3 * .2f);
                scenery[i].localRotation = Quaternion.Euler(0, (p.style % 3 - 1) * 12, p.style % 4 == 0 ? (i % 2 == 0 ? 12 : -12) : 0);
                if (propMaterials.Count <= i) propMaterials.Add(scenery[i].GetComponent<Renderer>().material);
                propMaterials[i].color = CharacterProfile.ColorOf(i % 3 == 0 ? p.accent : p.secondary);
            }
            // Abstract, original silhouettes rather than replicas of sacred or civic landmarks.
            for (int i = 0; i < outlines.Length; i++)
            {
                var points = Motif(p.style); var line = outlines[i];
                line.startColor = line.endColor = CharacterProfile.ColorOf(p.accent);
                line.positionCount = points.Length;
                for (int j = 0; j < points.Length; j++) line.SetPosition(j, new Vector3(points[j].x * 1.45f + (i - 2) * 3.8f, points[j].y * 1.6f + 2, 17));
            }
        }
        private static Vector2[] Motif(int style)
        {
            float[] xy;
            switch (style)
            {
                case 0: xy = new float[] {-1,0,-1,1,-1.3f,1.1f,-.7f,1.2f,0,1.6f,.7f,1.2f,1.3f,1.1f,1,1,1,0}; break; // curved roof
                case 1: xy = new float[] {-1,0,0,1.6f,.35f,.6f,1,0,-1,0,.35f,.6f,-.25f,.6f,0,1.6f}; break; // paper folds
                case 2: xy = new float[] {-1,0,-1,1.4f,-.6f,1.7f,.6f,1.7f,1,1.4f,1,0,.55f,0,.55f,1,-.55f,1,-.55f,0}; break; // lane gateway
                case 3: xy = new float[] {-1,0,-1,1.3f,-.5f,1.3f,-.5f,.7f,0,.7f,0,2,.3f,2,.3f,1,1,1,1,0}; break;
                case 4: xy = new float[] {-1,0,-1,1,0,1.6f,1,1,1,0,.5f,0,.5f,.8f,0,1.1f,-.5f,.8f,-.5f,0}; break;
                case 5: xy = new float[] {-1,0,-1,1.6f,1,1.6f,1,0,-1,0,1,1.6f,-1,1.6f,1,0,0,.8f,-1,0}; break; // textile lattice
                case 6: xy = new float[] {-1,0,-1,1.5f,.2f,1.5f,.2f,.7f,1,.7f,1,0,-1,0,-1,.7f,.2f,.7f,.2f,0}; break;
                case 7: xy = new float[] {-.6f,0,-.6f,1.5f,0,1.9f,.6f,1.5f,.6f,0,-.6f,0,-.6f,1.1f,.6f,1.1f,0,1.1f,0,1.55f}; break;
                case 8: xy = new float[] {-1,0,-.8f,1,-.4f,1.5f,0,1.7f,.4f,1.5f,.8f,1,1,0,0,.2f,-.8f,1,0,.2f,0,1.7f,0,.2f,.8f,1}; break;
                case 9: xy = new float[] {-1,0,0,1.8f,1,0,-1,0,-.4f,.8f,.4f,.8f,1,0}; break;
                case 10: xy = new float[] {-1,0,-.7f,.5f,-.3f,.8f,.2f,.8f,.7f,1.4f,1,1,.8f,.5f,.3f,.2f,-.4f,.2f,-1,0}; break;
                case 11: xy = new float[] {-1,0,-1,1,-.6f,1,-.6f,1.3f,-.3f,1.3f,-.3f,1.6f,.3f,1.6f,.3f,1.3f,.6f,1.3f,.6f,1,1,1,1,0}; break;
                case 12: xy = new float[] {-1,.4f,-.75f,.8f,-.5f,1,-.25f,.8f,0,.4f,.25f,0,.5f,-.2f,.75f,0,1,.4f}; break;
                case 13: xy = new float[] {0,1.8f,.35f,1.25f,1,1,.45f,.55f,.6f,-.1f,0,.2f,-.6f,-.1f,-.45f,.55f,-1,1,-.35f,1.25f,0,1.8f}; break;
                default: xy = new float[] {-1,0,-.8f,.7f,-.4f,.8f,-.2f,1.4f,.7f,1.4f,1,.4f,1.3f,0}; break;
            }
            var points = new Vector2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return points;
        }
        private void OnDestroy()
        {
            if (courtMaterial != null) Destroy(courtMaterial); if (surroundMaterial != null) Destroy(surroundMaterial);
            foreach (var material in propMaterials) if (material != null) Destroy(material);
        }
    }
}
