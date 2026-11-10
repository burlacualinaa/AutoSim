// SimpleFollowCamera.cs — Smooth chase camera for testing vehicle driving in the Editor.
using UnityEngine;

namespace AutoSim.Vehicle
{
    /// <summary>
    /// Smooth follow camera that tracks the vehicle from behind and above.
    /// Ideal for editor play-mode testing.
    /// </summary>
    [AddComponentMenu("AutoSim/Vehicle/Simple Follow Camera")]
    public class SimpleFollowCamera : MonoBehaviour
    {
        [Tooltip("Target transform to follow (typically the Vehicle root).")]
        [SerializeField] private Transform target;

        [Tooltip("Offset relative to vehicle orientation (X=side, Y=height, Z=distance behind).")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, -6f);

        [Tooltip("How smoothly the camera follows position.")]
        [SerializeField] private float positionDamping = 5f;

        [Tooltip("How smoothly the camera rotates toward vehicle forward.")]
        [SerializeField] private float rotationDamping = 5f;

        [Tooltip("Look-at target height offset above vehicle origin.")]
        [SerializeField] private float lookAtHeight = 1.2f;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void Awake()
        {
#if UNITY_WSA && !UNITY_EDITOR
            // Always disable chase camera on HoloLens 2 device so it does not interfere with XR head tracking
            enabled = false;
#endif
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            // Compute wanted position based on target's current rotation and offset
            Vector3 wantedPosition = target.TransformPoint(offset);

            // Smooth position
            transform.position = Vector3.Lerp(transform.position, wantedPosition, positionDamping * Time.deltaTime);

            // Smooth look-at rotation
            Vector3 lookTarget = target.position + Vector3.up * lookAtHeight;
            Vector3 lookDirection = lookTarget - transform.position;

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion wantedRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, wantedRotation, rotationDamping * Time.deltaTime);
            }
        }
    }
}
