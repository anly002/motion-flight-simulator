using UnityEngine;

namespace FlightLab
{
    // A game-oriented fighter model. Parameters are tunable approximations, not certified F-22 data.
    [RequireComponent(typeof(Rigidbody), typeof(FlightInput))]
    public sealed class AirplanePhysics : MonoBehaviour
    {
        [Header("Aircraft and air")]
        [Min(1f)] public float massKg = 29000f;
        [Min(0.1f)] public float wingAreaM2 = 78f;
        [Min(0.01f)] public float airDensity = 1.225f;
        [Min(0f)] public float maximumThrustN = 220000f;
        public Vector3 windVelocity;
        public float zeroLiftDrag = 0.022f;
        public float inducedDragFactor = 0.065f;
        public AnimationCurve liftCoefficient = new AnimationCurve(
            new Keyframe(-90, 0), new Keyframe(-20, -0.65f), new Keyframe(-14, -1.1f),
            new Keyframe(0, 0.06f), new Keyframe(4, 0.5f), new Keyframe(10, 1.05f),
            new Keyframe(14, 1.3f), new Keyframe(22, 0.65f), new Keyframe(90, 0));
        [Header("Steering and stability")]
        public Vector3 controlTorqueNm = new Vector3(700000f, 240000f, 700000f);
        public Vector3 rateDamping = new Vector3(1000000f, 1600000f, 700000f);
        public Vector3 inertiaKgM2 = new Vector3(180000f, 550000f, 480000f);
        public float pitchStability = 4000000f;
        public float yawStability = 2000000f;
        public float trimAngleOfAttackDeg = 3f;
        public float referenceAirspeed = 100f;
        public float sideDragAreaM2 = 16f;
        [Header("Start and landing")]
        public float startingAirspeed = 0f;
        public LandingGearRig landingGear;
        public float rotationSpeed = 78f;

        public float Airspeed { get; private set; }
        public float AngleOfAttack { get; private set; }
        public bool Crashed { get; private set; }
        public bool Grounded => landingGear != null && landingGear.IsGrounded;
        public float HeightAboveGround { get; private set; }
        public Rigidbody Body => body;
        private Rigidbody body;
        private FlightInput controls;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            controls = GetComponent<FlightInput>();
            body.mass = massKg;
            body.useGravity = true;
            body.isKinematic = false;
            body.linearDamping = body.angularDamping = 0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 3f;
            body.solverIterations = 12;
            body.solverVelocityIterations = 8;
            body.centerOfMass = Vector3.zero;
            body.inertiaTensorRotation = Quaternion.identity;
            body.inertiaTensor = inertiaKgM2;
            spawnPosition = body.position;
            spawnRotation = body.rotation;
            ResetAircraft();
        }

        private void FixedUpdate()
        {
            if (controls.ConsumeReset()) { ResetAircraft(); return; }
            Vector3 airVelocity = body.linearVelocity - windVelocity;
            Airspeed = airVelocity.magnitude;
            Terrain terrain = Terrain.activeTerrain;
            float groundHeight = terrain == null ? 0f : terrain.SampleHeight(body.position) + terrain.transform.position.y;
            HeightAboveGround = Mathf.Max(0, body.position.y - groundHeight - 2.12f);
            if (Crashed) return;
            body.AddForce(transform.forward * controls.Throttle * maximumThrustN, ForceMode.Force);
            if (Airspeed < 0.5f) { AngleOfAttack = 0; return; }
            Vector3 local = transform.InverseTransformDirection(airVelocity);
            AngleOfAttack = -Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            float dynamicPressure = 0.5f * airDensity * Airspeed * Airspeed;
            bool forward = local.z > 0;
            float cl = forward ? liftCoefficient.Evaluate(AngleOfAttack) : 0;
            float fraction = Mathf.Clamp01(local.z / Airspeed);
            float cd = zeroLiftDrag + inducedDragFactor * cl * cl + 0.5f * (1f - fraction * fraction);
            if (landingGear != null && landingGear.Deployed) cd += 0.025f;
            Vector3 lift = Vector3.Cross(airVelocity, transform.right).normalized * (dynamicPressure * wingAreaM2 * cl);
            Vector3 drag = -airVelocity.normalized * (dynamicPressure * wingAreaM2 * cd);
            Vector3 sideDrag = -transform.right * (0.5f * airDensity * sideDragAreaM2 * local.x * Mathf.Abs(local.x));
            body.AddForce(lift + drag + sideDrag, ForceMode.Force);

            float authority = Mathf.Clamp(Airspeed * Airspeed / (referenceAirspeed * referenceAirspeed), 0, 3f);
            float alphaError = forward ? (AngleOfAttack - trimAngleOfAttackDeg) * Mathf.Deg2Rad : 0;
            float beta = Mathf.Atan2(local.x, Mathf.Abs(local.z) + 0.1f);
            Vector3 omega = transform.InverseTransformDirection(body.angularVelocity);
            Vector3 torque = new Vector3(-controls.Pitch * controlTorqueNm.x + pitchStability * alphaError,
                controls.Yaw * controlTorqueNm.y + yawStability * beta, -controls.Roll * controlTorqueNm.z);
            if (Grounded)
            {
                // Keep the aircraft settled during taxi; aerodynamic rotation becomes available near takeoff speed.
                torque.x *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(50f, rotationSpeed, Airspeed));
                torque.y = 0; // Nose-wheel steering controls heading on the ground.
                torque.z *= 0.1f;
            }
            torque -= Vector3.Scale(omega, rateDamping);
            body.AddRelativeTorque(torque * authority, ForceMode.Force);
        }

        public void ResetAircraft()
        {
            body.position = spawnPosition;
            body.rotation = spawnRotation;
            body.linearVelocity = startingAirspeed <= 0 ? Vector3.zero : spawnRotation * Vector3.forward * startingAirspeed;
            body.angularVelocity = Vector3.zero;
            controls.ResetControls();
            landingGear?.ResetGear();
            Crashed = false;
            Airspeed = 0;
            AngleOfAttack = 0;
            body.WakeUp();
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Wheel contacts are handled by WheelColliders; this detects significant hull/wing impacts.
            if (collision.relativeVelocity.magnitude > 4f) Crashed = true;
        }
    }
}
