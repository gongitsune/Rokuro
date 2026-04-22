using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerManager : MonoBehaviour
{
    [SerializeField]
    private InputActionReference
        leftPositionInput,
        rightPositionInput;

    [SerializeField] private Transform leftHandObject, rightHandObject;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        leftPositionInput.action.performed += ev =>
        {
            var pos = ev.ReadValue<Vector3>();
            leftHandObject.position = pos;
        };
        rightPositionInput.action.performed += ev =>
        {
            var pos = ev.ReadValue<Vector3>();
            rightHandObject.position = pos;
        };
    }

    // Update is called once per frame
    private void Update()
    {

    }
}
