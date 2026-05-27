using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

public class hitbox : MonoBehaviour
{
    private XRBodyTransformer transformer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
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
        if(other.gameObject.name == "sitdown")
        {
            var colliderCenter = other.bounds.center;//collider‚Ìcenter
            var offset = new Vector3(0f, -0.4f, -0.9f);
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

            //transform.position = colliderCenter + offset;
            //xr.transform.rotation = Quaternion.Euler(0f,180f,0f);
        }
    }
}
