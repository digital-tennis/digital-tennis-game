using System;
using UnityEngine;

namespace DigitalTennis
{
    /// <summary>Rendering perspective only. Network coordinates and authoritative physics remain in court space.</summary>
    public sealed class LocalCourtView : IDisposable
    {
        private readonly Camera camera;
        private readonly Vector3 nearPosition;
        private readonly Quaternion nearRotation;
        public CourtSide LocalPlayer { get; private set; } = CourtSide.Near;
        public Camera Camera => camera;
        public CourtSide Opponent => TennisMatch.Opponent(LocalPlayer);

        public LocalCourtView(Camera camera)
        {
            this.camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            nearPosition = camera.transform.position; nearRotation = camera.transform.rotation;
        }
        public void SetPlayer(CourtSide player)
        {
            TennisScore.RequirePlayer(player); LocalPlayer = player;
            if (camera == null) return;
            Quaternion turn = player == CourtSide.Far ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            // Apply against the original pose, so repeated joins/resumes cannot accumulate rotations.
            camera.transform.SetPositionAndRotation(turn * nearPosition, turn * nearRotation);
        }
        public Vector3 Project(Vector3 worldPosition) => camera.WorldToViewportPoint(worldPosition);
        public void Dispose() { if (camera != null) camera.transform.SetPositionAndRotation(nearPosition, nearRotation); }
    }
}
