using System;
using UnityEngine;

namespace DigitalTennis
{
    public enum FrontEndStage { Logo, Title, Menu, Player, Opponent, Court, Versus, Playing, Pause, Results, Options, Controls, Lobby, Quit, Audio, InputControllers, KeyboardSettings, WirelessSettings }

    /// <summary>Menu navigation only; never owns tennis physics, scores or skill stock.</summary>
    public sealed class FrontEndModel
    {
        public FrontEndStage Stage { get; private set; } = FrontEndStage.Logo;
        public GameMode Mode { get; private set; } = GameMode.SinglePlayer;
        public int Player { get; private set; }
        public int Opponent { get; private set; } = 1;
        public int Court { get; private set; }
        public int Focus { get; private set; }
        private bool courtFromLobby;
        private FrontEndStage controllersReturn=FrontEndStage.Options;
        private FrontEndStage optionsReturn = FrontEndStage.Menu, controlsReturn = FrontEndStage.Menu;
        public bool ControllerDetail=>Stage==FrontEndStage.KeyboardSettings||Stage==FrontEndStage.WirelessSettings;
        public bool Selecting => Stage == FrontEndStage.Player || Stage == FrontEndStage.Opponent || Stage == FrontEndStage.Court;
        public bool ChooseMode(GameMode mode)
        {
            if (Stage != FrontEndStage.Menu || !Enum.IsDefined(typeof(GameMode), mode)) return false;
            Mode = mode; Stage = FrontEndStage.Player; Focus = Player; return true;
        }
        public bool Pick(int index)
        {
            if (!Selecting || !CharacterCatalog.ValidIndex(index)) return false;
            Focus = index; return true;
        }
        public void Move(int delta) { if (Selecting) Focus = ((Focus + delta) % 15 + 15) % 15; }
        public bool Confirm()
        {
            switch (Stage)
            {
                case FrontEndStage.Logo: Stage = FrontEndStage.Title; return true;
                case FrontEndStage.Title: Stage = FrontEndStage.Menu; return true;
                case FrontEndStage.Player:
                    Player = Focus; Stage = Mode == GameMode.Online ? FrontEndStage.Lobby : FrontEndStage.Opponent; Focus = Opponent; return true;
                case FrontEndStage.Opponent: Opponent = Focus; Stage = FrontEndStage.Court; Focus = Court; return true;
                case FrontEndStage.Court:
                    Court = Focus; Stage = courtFromLobby ? FrontEndStage.Lobby : FrontEndStage.Versus; courtFromLobby = false; return true;
                case FrontEndStage.Pause: Stage = FrontEndStage.Playing; return true;
                default: return false;
            }
        }
        public bool Back()
        {
            switch (Stage)
            {
                case FrontEndStage.Title: return false;
                case FrontEndStage.Menu: Stage = FrontEndStage.Title; break;
                case FrontEndStage.Player: Stage = FrontEndStage.Menu; break;
                case FrontEndStage.Opponent: Stage = FrontEndStage.Player; Focus = Player; break;
                case FrontEndStage.Court: Stage = courtFromLobby ? FrontEndStage.Lobby : FrontEndStage.Opponent; Focus = Opponent; courtFromLobby = false; break;
                case FrontEndStage.Lobby: Stage = FrontEndStage.Player; Focus = Player; break;
                case FrontEndStage.Options: Stage = optionsReturn; break;
                case FrontEndStage.Audio: Stage = FrontEndStage.Options; break;
                case FrontEndStage.InputControllers: Stage = controllersReturn; break;
                case FrontEndStage.KeyboardSettings:
                case FrontEndStage.WirelessSettings: Stage = FrontEndStage.InputControllers; break;
                case FrontEndStage.Controls: Stage = controlsReturn; break;
                case FrontEndStage.Pause: Stage = FrontEndStage.Playing; break;
                case FrontEndStage.Quit: Stage = FrontEndStage.Menu; break;
                default: return false;
            }
            return true;
        }
        public bool EditLobbyCourt()
        {
            if (Stage != FrontEndStage.Lobby) return false;
            courtFromLobby = true; Stage = FrontEndStage.Court; Focus = Court; return true;
        }
        public bool OnlineVersus(int near, int far, int court)
        {
            if (Stage != FrontEndStage.Lobby || !CharacterCatalog.ValidIndex(near) || !CharacterCatalog.ValidIndex(far) || !CharacterCatalog.ValidIndex(court)) return false;
            Player = near; Opponent = far; Court = court; Stage = FrontEndStage.Versus; return true;
        }
        public bool ReplayOpening(){if(Stage!=FrontEndStage.Title&&Stage!=FrontEndStage.Menu)return false;Stage=FrontEndStage.Logo;return true;}
        public bool StartPlay() { if (Stage != FrontEndStage.Versus) return false; Stage = FrontEndStage.Playing; return true; }
        public bool Pause() { if (Stage != FrontEndStage.Playing) return false; Stage = FrontEndStage.Pause; return true; }
        public bool Results() { if (Stage != FrontEndStage.Playing) return false; Stage = FrontEndStage.Results; return true; }
        public bool Rematch() { if (Stage != FrontEndStage.Results || Mode == GameMode.Online) return false; Stage = FrontEndStage.Versus; return true; }
        public bool OnlineRematch(){if(Stage!=FrontEndStage.Results||Mode!=GameMode.Online)return false;Stage=FrontEndStage.Versus;return true;}
        public void Menu() { courtFromLobby = false; Stage = FrontEndStage.Menu; }
        public void Options() { optionsReturn = Stage; Stage = FrontEndStage.Options; }
        public void AudioOptions() { Stage = FrontEndStage.Audio; }
        public void ControllerOptions(){controllersReturn=Stage;Stage=FrontEndStage.InputControllers;}
        public void OpenControllerDetail(InputDeviceKind kind){if(Stage==FrontEndStage.InputControllers)Stage=kind==InputDeviceKind.Keyboard?FrontEndStage.KeyboardSettings:FrontEndStage.WirelessSettings;}
        public void CloseControllerSettings(){if(ControllerDetail)Stage=controllersReturn;}
        public void Controls() { controlsReturn = Stage; Stage = FrontEndStage.Controls; }
        public void Quit() { if (Stage == FrontEndStage.Menu) Stage = FrontEndStage.Quit; }
    }

    public static class WorldRoster
    {
        // Cities in the authored court profiles: true anchors, with separate visual callouts for dense regions.
        private static readonly Vector2[] Cities = {
            new Vector2(126.978f,37.566f),new Vector2(135.768f,35.011f),new Vector2(121.474f,31.230f),new Vector2(-74.006f,40.713f),
            new Vector2(30.335f,59.934f),new Vector2(75.787f,26.913f),new Vector2(13.405f,52.520f),new Vector2(-.128f,51.507f),
            new Vector2(-5.984f,37.389f),new Vector2(2.352f,48.857f),new Vector2(144.963f,-37.814f),new Vector2(4.904f,52.367f),
            new Vector2(-43.173f,-22.907f),new Vector2(-7.982f,31.630f),new Vector2(18.424f,-33.925f) };
        public static readonly Vector2[] Callouts = {
            new Vector2(-15,-46),new Vector2(54,8),new Vector2(-68,28),new Vector2(-20,-30),new Vector2(110,-25),
            new Vector2(15,32),new Vector2(68,-44),new Vector2(-100,-60),new Vector2(-94,30),new Vector2(-52,-5),
            new Vector2(0,27),new Vector2(5,-86),new Vector2(0,30),new Vector2(-75,58),new Vector2(0,25) };
        public static Vector2 Position(int index)
        {
            if (!CharacterCatalog.ValidIndex(index)) throw new ArgumentOutOfRangeException(nameof(index));
            var c = Cities[index]; return new Vector2((c.x + 180) / 360, (c.y + 60) / 145);
        }
    }
}
