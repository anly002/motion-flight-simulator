using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlightLab
{
    // Keyboard, USB motion hardware and scripted controls share this interface.
    public sealed class FlightInput : MonoBehaviour
    {
        public enum InputSource { Keyboard, External, Motion }
        public InputSource source = InputSource.Keyboard;
        [Range(0f, 1f)] public float startingThrottle = 0f;
        [Min(0.05f)] public float externalTimeout = 0.3f;

        public float Pitch { get; private set; } // Positive = nose up.
        public float Roll { get; private set; }  // Positive = right wing down.
        public float Yaw { get; private set; }   // Positive = nose right.
        public float Throttle { get; private set; }
        public bool Brake { get; private set; }
        public bool ExternalSignalLost { get; private set; }
        private float lastExternalTime = float.NegativeInfinity;
        private bool resetRequested;
        private bool gearRequested;
        private float motionPitch, motionRoll;
        private bool motionThrottlePressed;
        private float lastMotionTime = float.NegativeInfinity;
        public bool MotionReady => Time.realtimeSinceStartup - lastMotionTime <= externalTimeout;

        private void Awake() { ResetControls(); }

        private void Update()
        {
            if (source == InputSource.External)
            {
                ExternalSignalLost = Time.realtimeSinceStartup - lastExternalTime > externalTimeout;
                if (ExternalSignalLost) { Pitch = Roll = Yaw = Throttle = 0f; Brake = true; }
                return;
            }

            ExternalSignalLost = false;
            if (!Application.isFocused) { Pitch = Roll = Yaw = Throttle = 0f; Brake = true; return; }
            ReadKeyboardAndMouse();
            ApplyMotionInput();
        }

        private void ApplyMotionInput()
        {
            if (source == InputSource.Motion)
            {
                ExternalSignalLost = !MotionReady;
                if (MotionReady) { Pitch = motionPitch; Roll = motionRoll; Throttle = motionThrottlePressed ? 1f : 0f; }
                else { Pitch = Roll = Yaw = Throttle = 0; Brake = true; }
            }
        }

        private void ReadKeyboardAndMouse()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            Throttle = mouse != null && mouse.leftButton.isPressed && PointerAllowsThrottle(mouse.position.ReadValue()) ? 1f : 0f;
            Keyboard keys = Keyboard.current;
            if (keys == null) { Pitch = Roll = Yaw = 0f; Brake = false; return; }
            Pitch = Axis(keys.wKey.isPressed, keys.sKey.isPressed);
            Roll = Axis(keys.aKey.isPressed, keys.dKey.isPressed);
            Yaw = Axis(keys.qKey.isPressed, keys.eKey.isPressed);
            if (keys.rKey.wasPressedThisFrame) { RequestReset(); }
            Brake = keys.spaceKey.isPressed;
            if (keys.gKey.wasPressedThisFrame) gearRequested = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            Throttle = Input.GetMouseButton(0) && PointerAllowsThrottle(Input.mousePosition) ? 1f : 0f;
            Pitch = Axis(Input.GetKey(KeyCode.W), Input.GetKey(KeyCode.S));
            Roll = Axis(Input.GetKey(KeyCode.A), Input.GetKey(KeyCode.D));
            Yaw = Axis(Input.GetKey(KeyCode.Q), Input.GetKey(KeyCode.E));
            if (Input.GetKeyDown(KeyCode.R)) { RequestReset(); }
            Brake = Input.GetKey(KeyCode.Space);
            if (Input.GetKeyDown(KeyCode.G)) gearRequested = true;
#endif
        }

        private static bool PointerAllowsThrottle(Vector2 position)
        {
            if (position.x < 0 || position.x > Screen.width || position.y < 0 || position.y > Screen.height) return false;
            float scale = Mathf.Clamp(Screen.height / 900f, 0.8f, 1.5f);
            Vector2 guiPosition = new Vector2(position.x, Screen.height - position.y) / scale;
            // Clicking the on-screen restart button must never also apply engine power.
            return !new Rect(Screen.width / scale - 154, 22, 132, 30).Contains(guiPosition);
        }

        // Call from your Unity input adapter on the main thread, once each Update.
        // Read the USB axes in the adapter; keep sensor/USB details out of flight physics.
        public void SetExternalControls(float pitch, float roll, float yaw, float throttle, bool brake = false)
        {
            source = InputSource.External;
            Pitch = Sanitize(pitch, -1f, 1f);
            Roll = Sanitize(roll, -1f, 1f);
            Yaw = Sanitize(yaw, -1f, 1f);
            Throttle = Sanitize(throttle, 0f, 1f);
            Brake = brake;
            lastExternalTime = Time.realtimeSinceStartup;
            ExternalSignalLost = false;
        }

        // Motion supplies pitch, roll and the physical throttle; keyboard yaw/brakes remain.
        public void SetMotionControls(float pitch, float roll, bool throttlePressed)
        {
            motionPitch = Sanitize(pitch, -1, 1); motionRoll = Sanitize(roll, -1, 1);
            motionThrottlePressed = throttlePressed;
            lastMotionTime = Time.realtimeSinceStartup;
        }
        public void ClearMotionInput()
        {
            motionPitch = motionRoll = 0;
            motionThrottlePressed = false;
            lastMotionTime = float.NegativeInfinity;
        }

        public void RequestReset() { resetRequested = true; }
        public bool ConsumeGearToggle() { bool value = gearRequested; gearRequested = false; return value; }
        public bool ConsumeReset()
        {
            bool requested = resetRequested;
            resetRequested = false;
            return requested;
        }

        public void ResetControls()
        {
            Pitch = Roll = Yaw = 0f;
            Throttle = startingThrottle;
            lastExternalTime = float.NegativeInfinity;
            resetRequested = false;
            gearRequested = false;
            Brake = false;
            ClearMotionInput();
        }

        private static float Axis(bool negative, bool positive)
        { return (positive ? 1f : 0f) - (negative ? 1f : 0f); }

        private static float Sanitize(float value, float min, float max)
        { return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, min, max); }
    }
}
