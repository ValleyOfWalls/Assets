using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    private Transform _target;
    public Vector3 offset = new Vector3(0, 3, -5);
    public float smoothSpeed = 0.125f;

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    void LateUpdate()
    {
        if (_target == null)
            return;

        Vector3 desiredPosition = _target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        transform.LookAt(_target);
    }
}