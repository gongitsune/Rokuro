using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerManager : MonoBehaviour
{
    [SerializeField] private Transform leftHandObject, rightHandObject;
    [SerializeField] private float moveSpeed, rotateSpeed;
    [SerializeField]
    InputActionReference
        leftHandPosAction,
        leftHandRotAction,
        rightHandPosAction,
        rightHandRotAction;


    private HandInputInfo _leftHandInfo, _rightHandInfo;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        _leftHandInfo = new HandInputInfo();
        _rightHandInfo = new HandInputInfo();
    }

    // Update is called once per frame
    private void Update()
    {
        _leftHandInfo.position = leftHandPosAction.action.ReadValue<Vector3>();
        _leftHandInfo.rotation = leftHandRotAction.action.ReadValue < Quaternion>();
        _rightHandInfo.position = rightHandPosAction.action.ReadValue<Vector3>();
        _rightHandInfo.rotation = rightHandRotAction.action.ReadValue<Quaternion>();
        UpdateHand(leftHandObject, _leftHandInfo);
        UpdateHand(rightHandObject, _rightHandInfo);
    }

    private void UpdateHand(Transform hand, HandInputInfo info)
    {
        hand.SetPositionAndRotation(
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
