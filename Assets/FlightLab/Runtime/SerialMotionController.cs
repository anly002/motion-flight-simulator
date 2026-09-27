using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlightLab
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(FlightInput))]
    public sealed class SerialMotionController : MonoBehaviour
    {
        public string portName = "COM3";
        public int baudRate = 115200;
        [Range(10, 60)] public float fullTiltDegrees = 25;
        [Range(0, 8)] public float deadZoneDegrees = 2;
        [Range(0.01f, 0.3f)] public float smoothingSeconds = 0.06f;
        public bool invertPitch, invertRoll, swapAxes;
        public bool startWithMotion = true;
        public bool Ready => filter.Calibrated && Connected;
        public bool Connected => lastSampleAt > 0 && Now - lastSampleAt < 0.3;
        public float PitchDegrees => filter.PitchDegrees;
        public float RollDegrees => filter.RollDegrees;
        public bool ThrottlePressed => Connected && throttlePressed;
        public int SamplesReceived { get; private set; }
        public string Status => !motionEnabled ? "KEYBOARD  |  M: motion" :
            !Connected ? transportStatus : !filter.Calibrated ? "HOLD STILL  " + Mathf.Clamp(filter.CalibrationSamples * 100 / 150, 0, 100) + "%" :
            "MOTION READY  |  C: recenter  M: keyboard";
        public bool MotionEnabled => motionEnabled;
        private readonly MotionTiltFilter filter = new MotionTiltFilter();
        private readonly ConcurrentQueue<MotionSample> samples = new ConcurrentQueue<MotionSample>();
        private volatile string transportStatus = "CONNECTING";
        private volatile bool stop;
        private Thread reader;
        private FlightInput input;
        private double lastSampleAt;
        private bool motionEnabled;
        private bool throttlePressed;
        private float filteredPitch, filteredRoll;
        private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        private void OnEnable()
        {
            input = GetComponent<FlightInput>();
            motionEnabled = startWithMotion;
            if (motionEnabled) input.source = FlightInput.InputSource.Motion;
            lastSampleAt = 0; SamplesReceived = 0; throttlePressed = false; Recenter();
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
            { transportStatus = "USB serial currently supports Windows"; return; }
            stop = false;
            string port = portName; int baud = baudRate;
            reader = new Thread(() => ReadLoop(port, baud)) { IsBackground = true, Name = "FlightLab MPU6050" };
            reader.Start();
        }
        private void OnDisable()
        {
            stop = true;
            if (reader != null && reader.IsAlive) reader.Join(600);
            reader = null;
            while (samples.TryDequeue(out _)) { }
            if (input != null) input.ClearMotionInput();
        }
        public void Recenter()
        {
            filter.Recenter(); filteredPitch = filteredRoll = 0;
            input?.ClearMotionInput();
        }
        private void Update()
        {
            if (Application.isFocused)
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard keys = Keyboard.current;
                if (keys != null && keys.mKey.wasPressedThisFrame) ToggleMode();
                if (keys != null && keys.cKey.wasPressedThisFrame) Recenter();
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.M)) ToggleMode();
                if (Input.GetKeyDown(KeyCode.C)) Recenter();
#endif
            }
            int count = 0;
            while (count++ < 256 && samples.TryDequeue(out MotionSample sample))
            {
                // Old queued packets must not keep the controls alive after a disconnect.
                if (Now - sample.ReceivedAt > 0.25) continue;
                if (filter.Add(sample))
                {
                    lastSampleAt = sample.ReceivedAt;
                    throttlePressed = sample.ThrottlePressed;
                    SamplesReceived++;
                }
            }
            if (!motionEnabled) return;
            if (!Ready) { input.ClearMotionInput(); filteredPitch = filteredRoll = 0; return; }
            float pitch = swapAxes ? RollDegrees : PitchDegrees;
            float roll = swapAxes ? PitchDegrees : RollDegrees;
            pitch = MotionTiltFilter.Axis(pitch, fullTiltDegrees, deadZoneDegrees) * (invertPitch ? -1 : 1);
            roll = MotionTiltFilter.Axis(roll, fullTiltDegrees, deadZoneDegrees) * (invertRoll ? -1 : 1);
            float amount = 1 - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, smoothingSeconds));
            filteredPitch = Mathf.Lerp(filteredPitch, pitch, amount);
            filteredRoll = Mathf.Lerp(filteredRoll, roll, amount);
            input.SetMotionControls(filteredPitch, filteredRoll, throttlePressed);
        }
        private void ToggleMode()
        {
            // External is reserved for scripted checks or a separate future adapter.
            motionEnabled = !motionEnabled;
            input.source = motionEnabled ? FlightInput.InputSource.Motion : FlightInput.InputSource.Keyboard;
            input.ResetControls();
            if (motionEnabled) Recenter();
        }
        private void ReadLoop(string port, int baud)
        {
            while (!stop)
            {
                try
                {
                    transportStatus = "CONNECTING " + port;
                    using (var serial = new WindowsMotionSerial(port, baud))
                    {
                        transportStatus = "WAITING FOR SENSOR " + port;
                        byte[] bytes = new byte[512];
                        var line = new StringBuilder(128);
                        bool discard = false;
                        double lastValid = Now;
                        while (!stop)
                        {
                            int length = serial.Read(bytes);
                            for (int i = 0; i < length; i++)
                            {
                                char c = (char)bytes[i];
                                if (c == '\n')
                                {
                                    if (!discard && MotionSample.TryParse(line.ToString().TrimEnd('\r'), out MotionSample packet))
                                    {
                                        packet.ReceivedAt = Now; lastValid = packet.ReceivedAt;
                                        // Bound memory and latency if the editor pauses or loses focus.
                                        while (samples.Count >= 256) samples.TryDequeue(out _);
                                        samples.Enqueue(packet);
                                        transportStatus = "SENSOR CONNECTED " + port;
                                    }
                                    line.Clear(); discard = false;
                                }
                                else if (!discard && line.Length < 128) line.Append(c);
                                else discard = true;
                            }
                            if (Now - lastValid > 1) transportStatus = "NO SENSOR DATA  |  check USB / wiring";
                            if (Now - lastValid > 5) throw new TimeoutException("No valid FL2 packets. Upload the motion + button firmware.");
                        }
                    }
                }
                catch (Exception error) { transportStatus = "USB " + port + ": " + error.Message; }
                for (int i = 0; i < 20 && !stop; i++) Thread.Sleep(50);
            }
        }
    }
}
