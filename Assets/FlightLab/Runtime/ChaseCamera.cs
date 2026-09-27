using UnityEngine;

namespace FlightLab
{
    public sealed class ChaseCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 33f;
        public float height = 10f;
        public float lookAhead = 12f;
        public float smoothing = 6f;
        private Vector3 lastHeading = Vector3.forward;
        private bool initialized;

        private void LateUpdate()
        {
            if (target == null) { return; }
            Vector3 heading = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (heading.sqrMagnitude > 0.01f) { lastHeading = heading.normalized; }
            Vector3 desiredPosition = target.position - lastHeading * distance + Vector3.up * height;
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            if (!initialized || Vector3.Distance(transform.position, desiredPosition) > 150f)
            { transform.position = desiredPosition; initialized = true; }
            else { transform.position = Vector3.Lerp(transform.position, desiredPosition, t); }
            Quaternion desiredRotation = Quaternion.LookRotation(
                target.position + target.forward * lookAhead - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }
    }
}
