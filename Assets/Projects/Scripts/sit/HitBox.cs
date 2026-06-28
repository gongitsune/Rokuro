using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace Projects.Scripts.sit
{
    public class HitBox : MonoBehaviour
    {
        [SerializeField] private DynamicMoveProvider moveProvider;
        [SerializeField] private GravityProvider gravityProvider;
        [SerializeField] private SnapTurnProvider snapTurnProvider;

        private int _collision;
        private XRBodyTransformer _transformer;

        private void Start()
        {
            _transformer = GetComponentInChildren<XRBodyTransformer>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("SitDown") || _collision != 0) return;

            _collision += 1;
            var colliderCenter = other.bounds.center;
            var offset = new Vector3(0f, -0.1f, -0.75f);
            var translateData = new XRBodyGroundPosition
            {
                targetPosition = colliderCenter + offset
            };
            var rotateData = new XRBodyYawRotation
            {
                angleDelta = 180
            };

            _transformer.QueueTransformation(translateData);
            _transformer.QueueTransformation(rotateData);

            moveProvider.moveSpeed = 0f;
            snapTurnProvider.turnAmount = 0f;
            gravityProvider.useGravity = false;
        }
    }
}