using UnityEngine;
using UnityEngine.InputSystem;

namespace Projects.Scripts.Controller
{
    public class ControllerManager : MonoBehaviour
    {
        [SerializeField] private Camera mainCam;
        [SerializeField] private GameObject leftHandPrefab, rightHandPrefab;
        [SerializeField] private float moveSpeed, rotateSpeed;

        [SerializeField] private InputActionReference
            leftHandPosAction,
            leftHandRotAction,
            leftHandTriggerAction,
            rightHandTriggerAction,
            rightHandPosAction,
            rightHandRotAction;

        private HandInputInfo _leftHandInfo, _rightHandInfo;

        private Transform _leftHandObject, _rightHandObject;

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

            if (leftHandTriggerAction.action.WasPressedThisFrame()) Debug.Log("左が押されたよ");
            if (rightHandTriggerAction.action.WasPressedThisFrame()) Debug.Log("右が押されたよ");
        }

        private void UpdateHand(Transform hand, HandInputInfo info)
        {
            hand.localRotation = Quaternion.Slerp(hand.localRotation, info.rotation, rotateSpeed * Time.deltaTime);
            hand.localPosition = Vector3.Lerp(hand.localPosition, info.position, moveSpeed * Time.deltaTime);
        }

        private class HandInputInfo
        {
            public Vector3 position;
            public Quaternion rotation;
        }
    }
}