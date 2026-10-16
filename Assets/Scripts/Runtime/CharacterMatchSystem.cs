using System;
using System.IO;
using UnityEngine;

namespace DigitalTennis
{
    public sealed class CharacterMatchSystem : MonoBehaviour
    {
        [SerializeField] private CharacterPresentation nearView, farView;
        [SerializeField] private NationalCourtView courtView;
        private TennisMatchController controller;
        private CharacterAbility[] players;
        private bool menu;
        private float originalFov;
        public CharacterBalanceStore Store { get; private set; }
        public int CourtIndex { get; private set; }
        public uint BalanceRevision { get; private set; }
        public bool Cinematic => players != null && (players[0].Cinematic || players[1].Cinematic);
        public int Activations { get; private set; }
        public CharacterAbility Player(CourtSide side) => players[(int)side];
        public bool CanSelect
        {
            get
            {
                if (!controller.Ball.IsAuthoritative)
                {
                    var remote = GetComponent<OnlineTennisSession>().RemoteState;
                    return remote != null && remote.phase == "WaitServe" && remote.shotId == 0;
                }
                return controller.Match.Phase == MatchPhase.WaitServe && controller.Match.CurrentShot.ShotId == 0 && !controller.Recovery.Active && !Cinematic;
            }
        }
        public bool CanReload => controller.Ball.IsAuthoritative && controller.Match.Phase == MatchPhase.WaitServe && !controller.Recovery.Active && (!controller.IsPaused || controller.Mode != GameMode.Online) && !Cinematic && players[0].Remaining == 0 && players[1].Remaining == 0;
        public void Initialize(TennisMatchController owner)
        {
            controller = owner; originalFov = owner.View.Camera.fieldOfView;
            Store = owner.Balance.Characters;
            players = new[] { new CharacterAbility(0, Store[0], controller.Tuning), new CharacterAbility(1, Store[1], controller.Tuning) };
            BalanceRevision = controller.Balance.Revision;
            courtView.Configure(CharacterCatalog.Get(0));
        }
        public void ResetGame()
        {
            if (players == null) return;
            for (int i = 0; i < 2; i++) players[i] = new CharacterAbility(players[i].Index, Store[players[i].Index], controller.Tuning);
            BalanceRevision=controller.Balance.Revision;
            Activations = 0;
            controller.Ball.IsPaused = controller.IsPaused;
        }
        public bool Select(CourtSide side, int index)
        {
            if (!controller.Ball.IsAuthoritative || !CanSelect || !CharacterCatalog.ValidIndex(index)) return false;
            players[(int)side] = new CharacterAbility(index, Store[index], controller.Tuning); return true;
        }
        public bool SelectCourt(int index)
        {
            if (!controller.Ball.IsAuthoritative || !CanSelect || !CharacterCatalog.ValidIndex(index)) return false;
            CourtIndex = index; courtView.Configure(CharacterCatalog.Get(index)); return true;
        }
        public bool Reload()
        {
            if(controller.Ball.IsAuthoritative)controller.RulesStore.Reload();
            if (!CanReload) { controller.Balance.Refuse(); return false; }
            if (!controller.Balance.Reload()) return false;
            foreach (var p in players) p.Rebalance(Store[p.Index], controller.Tuning);
            BalanceRevision = controller.Balance.Revision;
            controller.ApplyBalance(); return true;
        }
        public bool Activate(CourtSide side)
        {
            if (!controller.Ball.IsAuthoritative || controller.IsPaused || controller.Recovery.Active || Cinematic ||
                (controller.Match.Phase != MatchPhase.WaitServe && controller.Match.Phase != MatchPhase.InFlight && controller.Match.Phase != MatchPhase.Rally)) return false;
            if (!Player(side).Activate()) return false;
            controller.Effects?.Record(TennisEffectKind.Skill,side,controller.Effects.Avatar(side),Vector3.up);
            controller.Ball.IsPaused = true; Activations++;
            Debug.Log($"CHARACTER_SKILL {side} {CharacterCatalog.Get(Player(side).Index).id} activation={Player(side).ActivationId}");
            return true;
        }
        public void Tick(float dt)
        {
            bool wasCinematic = Cinematic;
            bool idle = controller.Match.Phase == MatchPhase.WaitServe || controller.Match.Phase == MatchPhase.PointEnd;
            // Both players' effect timers stop during either cinematic. No global Time.timeScale changes.
            foreach (var p in players) if (!wasCinematic || p.Cinematic) p.Tick(dt, idle);
            controller.Ball.IsPaused = controller.IsPaused || Cinematic;
        }
        public CharacterState[] Capture() => new[] { players[0].Capture(), players[1].Capture() };
        public void Present(CharacterState[] states, int court, uint revision)
        {
            if (controller.Ball.IsAuthoritative || states == null) return;
            for (int i = 0; i < 2; i++) { players[i].Rebalance(states[i].balance,controller.Tuning);players[i].Present(states[i]); }
            if (CourtIndex != court) { CourtIndex = court; courtView.Configure(CharacterCatalog.Get(court)); }
            BalanceRevision = revision;
        }
        public void ToggleMenu() => menu = !menu;
        private void LateUpdate()
        {
            if (players == null) return;
            nearView.Present(Player(CourtSide.Near).Capture()); farView.Present(Player(CourtSide.Far).Capture());
            float age = players[0].Cinematic ? players[0].CinematicAge : players[1].CinematicAge;
            controller.View.Camera.fieldOfView = Cinematic && !(FrontEndController.Active?.ReduceMotion ?? false) ? originalFov - 9 * Mathf.Sin(age / CharacterAbility.CinematicSeconds * Mathf.PI) : originalFov;
        }
        private void OnDestroy() { if (controller?.View?.Camera != null) controller.View.Camera.fieldOfView = originalFov; }
        private void OnGUI()
        {
            if (FrontEndController.HideLegacyHud) return;
            if (players == null) return;
            CourtSide own = controller.View.LocalPlayer;
            DrawMeter(16, Screen.height - 166, own);
            DrawMeter(Screen.width - 294, Screen.height - 166, TennisMatch.Opponent(own));
            if (GUI.Button(new Rect(16, Screen.height - 42, 278, 27), "Characters / courts [C]")) ToggleMenu();
            if (Cinematic)
            {
                var p = players[0].Cinematic ? players[0] : players[1]; var profile = CharacterCatalog.Get(p.Index);
                GUI.Box(new Rect(0, 0, Screen.width, 62), "");
                GUI.Label(new Rect(0, 9, Screen.width, 48), profile.name + "  /  " + profile.skillName,
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 26 });
            }
            if (!menu) return;
            var box = new Rect((Screen.width - 800) / 2, 80, 800, 540);
            GUI.Box(box, "WORLD ROSTER - 15 original players / six ratings (1-100)");
            GUI.Label(new Rect(box.x + 18, box.y + 28, 760, 24), "Choose before the first serve. In online, each player chooses their own character; host chooses court.");
            for (int i = 0; i < 15; i++)
            {
                var profile = CharacterCatalog.Get(i); float x = box.x + 18 + (i % 5) * 154, y = box.y + 62 + (i / 5) * 60;
                bool old = GUI.enabled; GUI.enabled = old && CanSelect;
                if (GUI.Button(new Rect(x, y, 146, 27), profile.id + " / " + profile.name.Split(' ')[0]))
                    controller.InputAdapter.Press(PlayerAction.SelectCharacter, false, i);
                GUI.enabled = old && CanSelect && controller.Ball.IsAuthoritative;
                if (GUI.Button(new Rect(x, y + 29, 70, 22), "Opponent")) Select(TennisMatch.Opponent(own), i);
                if (GUI.Button(new Rect(x + 75, y + 29, 70, 22), "Court")) SelectCourt(i);
                GUI.enabled = old;
            }
            var selected = Player(own); var persona = CharacterCatalog.Get(selected.Index);
            GUI.Label(new Rect(box.x + 18, box.y + 249, 764, 25), $"{persona.name} / {persona.id} / age {persona.age} / {persona.gender} / {persona.heritage}");
            GUI.Label(new Rect(box.x + 18, box.y + 278, 764, 38), persona.persona + "\n" + persona.costume);
            string[] labels = { "Serve", "Smash", "Volley", "Defense", "Stamina", "Speed" };
            for (int i = 0; i < 6; i++) GUI.Label(new Rect(box.x + 18 + (i % 3) * 255, box.y + 325 + (i / 3) * 28, 250, 25), labels[i] + "  " + selected.Balance.Ratings[i].ToString("0"));
            GUI.Label(new Rect(box.x + 18, box.y + 386, 764, 25), $"Court {CharacterCatalog.Get(CourtIndex).courtCode} / {CharacterCatalog.Get(CourtIndex).courtName} / {CharacterCatalog.Get(CourtIndex).surface} (visual)");
            GUI.Label(new Rect(box.x + 18, box.y + 414, 764, 25), "CSV: " + Store.Path);
            bool enabled = GUI.enabled; GUI.enabled = enabled && CanReload;
            if (GUI.Button(new Rect(box.x + 18, box.y + 450, 230, 28), "Reload balance CSV [F5]")) Reload();
            GUI.enabled = enabled;
            GUI.Label(new Rect(box.x + 260, box.y + 450, 520, 40), Store.Status);
            if (GUI.Button(new Rect(box.x + 18, box.y + 499, 764, 27), "Close")) menu = false;
        }
        private void DrawMeter(float x, float y, CourtSide side)
        {
            var p = Player(side); var profile = CharacterCatalog.Get(p.Index);
            GUI.Box(new Rect(x, y, 278, 116), profile.id + " / " + profile.name);
            GUI.Label(new Rect(x + 10, y + 25, 260, 22), $"Skill {p.Meter.Stock}/3 | Gauge {p.Meter.Gauge:0}% | Energy {p.Energy:0}");
            GUI.Label(new Rect(x + 10, y + 48, 260, 22), profile.skillName);
            bool before = GUI.enabled;
            GUI.enabled = before && side == controller.View.LocalPlayer && p.Meter.Stock > 0 && !Cinematic && p.Remaining == 0 && !controller.IsPaused;
            if (GUI.Button(new Rect(x + 10, y + 77, 258, 27), p.Buff ? $"Active {p.Remaining:F1}s" : "Use skill [K]")) controller.InputAdapter.Press(PlayerAction.Skill);
            GUI.enabled = before;
        }
    }
}
