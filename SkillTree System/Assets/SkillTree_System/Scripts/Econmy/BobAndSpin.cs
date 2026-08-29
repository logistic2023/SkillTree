using UnityEngine;

public class BobAndSpin : MonoBehaviour
{
    public bool UsePositionBasedOffset = true;
    public float PositionBasedScale = 2.0f;

    public bool Bob = true;
    public float BobSpeed = 5.0f;
    public float BobHeight = 0.2f;

    public bool Spin = true;
    public float SpinSpeed = 180.0f;

    Transform myTransform;
    Vector3 startPosition;
    Quaternion startRotation;

    void Awake()
    {
        myTransform = transform;
        startPosition = myTransform.position;
        startRotation = myTransform.rotation;
    }

    public void InitBobAndSpin(Transform newPos)
    {
        startPosition = newPos.position;
        startRotation = newPos.rotation;
    }

    void Update()
    {
        float offset = (UsePositionBasedOffset) ? startPosition.z * PositionBasedScale + Time.time : Time.time;

        if (Bob)
        {
            myTransform.position = startPosition + Vector3.up * Mathf.Sin(offset * BobSpeed) * BobHeight;
        }

        if (Spin)
        {
            myTransform.rotation = startRotation * Quaternion.AngleAxis(offset * SpinSpeed, Vector3.up);
        }
    }
}