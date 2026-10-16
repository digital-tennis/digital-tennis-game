using System;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Read-only evidence for the explicit standalone validation run. Never moves an object or changes a rule.</summary>
    [Serializable]
    public sealed class OnlineViewValidation
    {
        public string localPlayer;
        public float[] cameraPosition;
        public int frames, viewErrors, worldPoseChecks, worldPoseErrors, strokeChecks, strokeErrors;
        public int opponentRightMoves, opponentLeftMoves, mirroredMovementErrors;
        public int opponentForehands, opponentBackhands, localForehands, localBackhands;
        private bool hasPrevious;
        private Vector3 previousOpponent, previousView;
        private uint previousRally;

        public void Sample(TennisMatchController c, OnlineTennisSession session)
        {
            if (!session.Playing || c.View == null) return;
            var state = session.IsHost ? GamePayload.Capture(c, "view-proof", 0) : session.PresentedState;
            if (state == null) return;
            var view = c.View;
            localPlayer = view.LocalPlayer.ToString(); cameraPosition = GamePayload.Pack(view.Camera.transform.position);
            var own = view.LocalPlayer == CourtSide.Near ? c.NearAvatar : c.FarAvatar;
            var opponent = view.LocalPlayer == CourtSide.Near ? c.FarAvatar : c.NearAvatar;
            var ownPos = own.transform.position; var opponentPos = opponent.transform.position;
            frames++;
            // Test the actual camera at the actual rendered player position: a purely lateral movement must reverse.
            float facing = view.LocalEnd == CourtSide.Near ? 1 : -1;
            float ownRight = view.Project(ownPos + Vector3.right * facing).x - view.Project(ownPos).x;
            float opponentRight = view.Project(opponentPos - Vector3.right * facing).x - view.Project(opponentPos).x;
            if (ownRight <= 0 || opponentRight >= 0 || view.Project(ownPos).y >= view.Project(opponentPos).y ||
                view.LocalPlayer != (session.IsHost ? CourtSide.Near : CourtSide.Far)) viewErrors++;
            if (!session.IsHost)
            {
                worldPoseChecks++;
                if (!session.PresentationAligned) worldPoseErrors++;
                CheckStroke(c.NearAvatar, state.players[0]); CheckStroke(c.FarAvatar, state.players[1]);
            }
            Vector3 cameraPoint = view.Camera.transform.InverseTransformPoint(opponentPos);
            if (hasPrevious && state.rallyId == previousRally && (state.phase == "InFlight" || state.phase == "Rally"))
            {
                float opponentOwnDx = (opponentPos.x - previousOpponent.x) * -facing;
                float viewerDx = cameraPoint.x - previousView.x;
                if (opponentOwnDx > .001f) { opponentRightMoves++; if (viewerDx >= 0) mirroredMovementErrors++; }
                else if (opponentOwnDx < -.001f) { opponentLeftMoves++; if (viewerDx <= 0) mirroredMovementErrors++; }
            }
            hasPrevious = true; previousRally = state.rallyId; previousOpponent = opponentPos; previousView = cameraPoint;
            opponentForehands = opponent.RemoteForehands; opponentBackhands = opponent.RemoteBackhands;
            localForehands = own.RemoteForehands; localBackhands = own.RemoteBackhands;
        }
        private void CheckStroke(RacketAvatar avatar, PlayerState state)
        {
            if (state.swingId == 0) return;
            strokeChecks++;
            if (avatar.SwingId != state.swingId || (int)avatar.LastStroke != state.stroke) strokeErrors++;
        }
        public bool Passed(bool host) => frames > 30 && viewErrors == 0 && mirroredMovementErrors == 0 &&
            opponentRightMoves > 0 && opponentLeftMoves > 0 && (host ||
            (worldPoseChecks > 0 && worldPoseErrors == 0 && strokeChecks > 0 && strokeErrors == 0 &&
             opponentForehands > 0 && opponentBackhands > 0 && localForehands > 0 && localBackhands > 0));
    }
}
