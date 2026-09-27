using System;
using System.Globalization;
using UnityEngine;

namespace FlightLab
{
    public struct MotionSample
    {
        public uint Sequence, Microseconds;
        public Vector3 Acceleration, Gyro;
        public bool ThrottlePressed;
        public double ReceivedAt;
        // FL2 adds a debounced throttle button to the six raw sensor axes and payload XOR.
        public static bool TryParse(string line, out MotionSample sample)
        {
            sample = default;
            if (string.IsNullOrEmpty(line) || line.Length > 128) return false;
            int star = line.IndexOf('*');
            if (star < 0 || star != line.Length - 3) return false;
            byte expected;
            if (!byte.TryParse(line.Substring(star + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out expected)) return false;
            byte checksum = 0;
            for (int i = 0; i < star; i++) checksum ^= (byte)line[i];
            if (checksum != expected) return false;
            string[] fields = line.Substring(0, star).Split(',');
            if (fields.Length != 10 || fields[0] != "FL2" ||
                !uint.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out sample.Sequence) ||
                !uint.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out sample.Microseconds)) return false;
            if (fields[9] != "0" && fields[9] != "1") return false;
            sample.ThrottlePressed = fields[9] == "1";
            short[] axes = new short[6];
            for (int i = 0; i < 6; i++)
                if (!short.TryParse(fields[i + 3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out axes[i])) return false;
            sample.Acceleration = new Vector3(axes[0], axes[1], axes[2]) / 16384f;
            sample.Gyro = new Vector3(axes[3], axes[4], axes[5]) / 131f;
            return sample.Acceleration.sqrMagnitude > 0.01f;
        }
    }

    // Estimate gravity in sensor coordinates. Gyro prediction handles quick movement;
    // accelerometer correction removes pitch/roll drift without requiring a magnetometer.
    public sealed class MotionTiltFilter
    {
        public bool Calibrated { get; private set; }
        public int CalibrationSamples { get; private set; }
        public float PitchDegrees { get; private set; }
        public float RollDegrees { get; private set; }
        public Vector3 Bias { get; private set; }
        private Vector3 gravity, previousAccel, sumAccel, sumGyro;
        private Vector3 neutralUp, forward, right;
        private float sumMagnitude, restingMagnitude = 1f;
        private uint previousMicros, previousSequence;
        private bool hasSample;
        public void Recenter()
        {
            Calibrated = false; CalibrationSamples = 0;
            sumAccel = sumGyro = Vector3.zero; sumMagnitude = 0; PitchDegrees = RollDegrees = 0;
            hasSample = false;
        }
        public bool Add(MotionSample sample)
        {
            float dt = hasSample ? unchecked(sample.Microseconds - previousMicros) * 0.000001f : 0.01f;
            if (hasSample && sample.Sequence == previousSequence) return false;
            if (hasSample && (dt <= 0 || dt > 0.2f)) Recenter();
            previousMicros = sample.Microseconds; previousSequence = sample.Sequence; hasSample = true;
            float magnitude = sample.Acceleration.magnitude;
            Vector3 measured = sample.Acceleration / magnitude;
            if (!Calibrated)
            {
                bool steady = magnitude > 0.65f && magnitude < 1.35f && sample.Gyro.magnitude < 8f &&
                    (CalibrationSamples == 0 || (Vector3.Angle(previousAccel, measured) < 1f && Vector3.Angle(sumAccel.normalized, measured) < 2.5f));
                previousAccel = measured;
                if (!steady) { CalibrationSamples = 0; sumMagnitude = 0; sumAccel = sumGyro = Vector3.zero; return true; }
                sumAccel += measured; sumGyro += sample.Gyro; sumMagnitude += magnitude; CalibrationSamples++;
                if (CalibrationSamples >= 150)
                {
                    Bias = sumGyro / CalibrationSamples;
                    restingMagnitude = sumMagnitude / CalibrationSamples;
                    gravity = neutralUp = sumAccel.normalized;
                    // Sensor +X marks the front of the controller. The flat PCB is normally +Z up.
                    forward = Vector3.ProjectOnPlane(Vector3.right, neutralUp).normalized;
                    if (forward.sqrMagnitude < 0.5f) forward = Vector3.ProjectOnPlane(Vector3.up, neutralUp).normalized;
                    right = Vector3.Cross(forward, neutralUp).normalized;
                    Calibrated = true;
                }
                return true;
            }
            Vector3 omega = (sample.Gyro - Bias) * Mathf.Deg2Rad;
            gravity = (gravity - Vector3.Cross(omega, gravity) * dt).normalized;
            if (Mathf.Abs(magnitude - restingMagnitude) < restingMagnitude * 0.2f)
                gravity = Vector3.Slerp(gravity, measured, 1f - Mathf.Exp(-dt / 0.45f)).normalized;
            // Front raised gives positive pitch; right side lowered gives positive roll.
            PitchDegrees = Mathf.Atan2(Vector3.Dot(gravity, forward), Vector3.Dot(gravity, neutralUp)) * Mathf.Rad2Deg;
            RollDegrees = -Mathf.Atan2(Vector3.Dot(gravity, right), Vector3.Dot(gravity, neutralUp)) * Mathf.Rad2Deg;
            return true;
        }
        public static float Axis(float degrees, float fullTilt, float deadZone)
        {
            float magnitude = Mathf.InverseLerp(deadZone, Mathf.Max(deadZone + 1f, fullTilt), Mathf.Abs(degrees));
            return Mathf.Sign(degrees) * magnitude;
        }
    }
}
