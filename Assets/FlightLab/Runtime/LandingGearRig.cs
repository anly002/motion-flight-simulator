using UnityEngine;

namespace FlightLab
{
    [RequireComponent(typeof(Rigidbody), typeof(FlightInput))]
    public sealed class LandingGearRig : MonoBehaviour
    {
        public WheelCollider noseWheel;
        public WheelCollider[] mainWheels;
        public GameObject[] extendedVisuals;
        public GameObject[] retractedVisuals;
        public float maximumBrakeTorque = 100000f;
        public bool Deployed { get; private set; } = true;
        public bool IsGrounded => Deployed && ((noseWheel != null && noseWheel.isGrounded) ||
            (mainWheels != null && System.Array.Exists(mainWheels, w => w != null && w.isGrounded)));
        public bool ParkingBrake { get; private set; }
        private FlightInput controls;
        private Rigidbody body;

        private void Awake() { controls = GetComponent<FlightInput>(); body = GetComponent<Rigidbody>(); ResetGear(); }
        private void FixedUpdate()
        {
            if (controls.ConsumeGearToggle() && !IsGrounded) SetDeployed(!Deployed);
            float speed = body.linearVelocity.magnitude;
            ParkingBrake = Deployed && controls.Throttle < 0.01f && speed < 0.5f;
            float brake = controls.Brake || ParkingBrake ? maximumBrakeTorque : 0;
            ApplyWheel(noseWheel, brake);
            if (noseWheel != null)
                noseWheel.steerAngle = controls.Yaw * Mathf.Lerp(22f, 1f, Mathf.Clamp01(speed / 65f));
            if (mainWheels != null) foreach (WheelCollider wheel in mainWheels) ApplyWheel(wheel, brake);
            if (controls.Throttle > 0.01f) body.WakeUp();
        }
        private static void ApplyWheel(WheelCollider wheel, float brake)
        {
            if (wheel == null) return;
            // PhysX's zero-torque "sticky tire" constraint can cancel external jet
            // thrust. A negligible torque releases that constraint while rolling.
            // Actual propulsion is still applied by AirplanePhysics.AddForce.
            wheel.motorTorque = brake > 0 ? 0f : 0.01f;
            wheel.brakeTorque = brake;
        }
        public void ResetGear() { SetDeployed(true); ParkingBrake = true; }
        public void SetDeployed(bool deployed)
        {
            Deployed = deployed;
            if (noseWheel != null) noseWheel.enabled = deployed;
            if (mainWheels != null) foreach (var wheel in mainWheels) if (wheel != null) wheel.enabled = deployed;
            if (extendedVisuals != null) foreach (var item in extendedVisuals) if (item != null) item.SetActive(deployed);
            if (retractedVisuals != null) foreach (var item in retractedVisuals) if (item != null) item.SetActive(!deployed);
        }
    }
}
