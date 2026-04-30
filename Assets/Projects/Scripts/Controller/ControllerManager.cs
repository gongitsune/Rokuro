using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerManager : MonoBehaviour
{
    [SerializeField] private Camera mainCam;
    [SerializeField] private GameObject leftHandPrefab, rightHandPrefab;
    [SerializeField] private float moveSpeed, rotateSpeed;
    [SerializeField]
    InputActionReference
        leftHandPosAction,
        leftHandRotAction,
        leftHandTriggerAction,
        rightHandTriggerAction,
        rightHandPosAction,
        rightHandRotAction;

    private Transform _leftHandObject, _rightHandObject;
    private HandInputInfo _leftHandInfo, _rightHandInfo;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        _leftHandObject = Instantiate(leftHandPrefab, transform).transform;
        _rightHandObject = Instantiate(rightHandPrefab, transform).transform;
        _leftHandInfo = new HandInputInfo();
        _rightHandInfo = new HandInputInfo();
    }

    // Update is called once per frame
    private void Update()
    {
        _leftHandInfo.position = leftHandPosAction.action.ReadValue<Vector3>();
        _leftHandInfo.rotation = leftHandRotAction.action.ReadValue<Quaternion>();
        _rightHandInfo.position = rightHandPosAction.action.ReadValue<Vector3>();
        _rightHandInfo.rotation = rightHandRotAction.action.ReadValue<Quaternion>();
        UpdateHand(_leftHandObject, _leftHandInfo);
        UpdateHand(_rightHandObject, _rightHandInfo);

        if (leftHandTriggerAction.action.WasPressedThisFrame())
        {
            Debug.Log("ç∂Ç™âüÇ≥ÇÍÇΩÇÊ");
        }
        if (rightHandTriggerAction.action.WasPressedThisFrame())
        {
            Debug.Log("âEÇ™âüÇ≥ÇÍÇΩÇÊ");
        }
    }

    private void UpdateHand(Transform hand, HandInputInfo info)
    {
        hand.SetLocalPositionAndRotation(
            Vector3.Lerp(hand.position, info.position, moveSpeed * Time.deltaTime),
            Quaternion.Slerp(hand.rotation, info.rotation, rotateSpeed * Time.deltaTime)
        );
    }

    private class HandInputInfo
    {
        public Vector3 position;
        public Quaternion rotation;
    }
}
