using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

public class hitbox : MonoBehaviour
{
    private XRBodyTransformer transformer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private DynamicMoveProvider moveProvider;
    [SerializeField] private SnapTurnProvider snapTurnProvider;
    private int collision = 0;//collider‚É“–‚½‚Á‚½‰ñ”
    void Start()
    {
        transformer = GetComponentInChildren<XRBodyTransformer>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.name == "sitdown"&&collision==0)
        {
            collision += 1;
            var colliderCenter = other.bounds.center;//collider‚Ìcenter
            var offset = new Vector3(0f, -0.1f, -0.75f);
            var translateData = new XRBodyGroundPosition
            {
                targetPosition = colliderCenter + offset
            };
            var rotateData = new XRBodyYawRotation
            {
                angleDelta = 180
            };

            transformer.QueueTransformation(translateData);
            transformer.QueueTransformation(rotateData);

            moveProvider.moveSpeed = 0f; //ƒJƒƒ‰‚ÌˆÚ“®‘¬“x‚ğ0‚ÉŒÅ’è
            snapTurnProvider.turnAmount = 0f;
            moveProvider.useGravity = false;
        }
    }
}
