// WheelVisualSync.cs — Synchronises a visual wheel mesh with its WheelCollider pose.
// Attach to each visual wheel mesh GameObject and assign the corresponding WheelCollider.
using UnityEngine;

namespace AutoSim.Vehicle
{
    /// <summary>
    /// Copies the world-space position and rotation from a <see cref="WheelCollider"/>
    /// to this transform every frame, keeping the visual wheel mesh in sync with physics.
    /// </summary>
    [AddComponentMenu("AutoSim/Vehicle/Wheel Visual Sync")]
    public class WheelVisualSync : MonoBehaviour
    {
        [Tooltip("The WheelCollider that drives this visual wheel.")]
        [SerializeField] private WheelCollider targetWheel;

        /// <summary>Public property to configure target wheel in editor or runtime.</summary>
        public WheelCollider TargetWheel
        {
            get => targetWheel;
            set => targetWheel = value;
        }

        private void Update()
        {
            if (targetWheel == null)
                return;

            targetWheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
            transform.position = position;
            transform.rotation = rotation;
        }
    }
}
