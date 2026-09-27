# ESP32 + MPU-6050 flight controller

The Raptor Valley scene defaults to **COM3, 115200 baud**. Upload the firmware below and select your board's actual COM port in Unity. The USB cable supplies power and carries sensor readings and the throttle-button state; no Wi-Fi or Bluetooth is required.

## Flying

1. Keep Arduino **Serial Monitor and Serial Plotter closed** while Unity is playing. Only one application can open COM3 at a time.
2. Open this repository's root folder as a project in Unity Hub, then choose **Flight Lab → Open Raptor Valley**. Press **Play**.
3. Hold the breadboard still in a comfortable neutral position for about **two seconds**. The upper-left panel should say **MOTION READY**.
4. Keep the sensor board approximately flat with its **+X arrow pointing forward, away from you**. Raise that front edge to pitch up; lower it to pitch down. Lower the right side to roll right.
5. Focus the Unity Game view, then hold your **breadboard button on D23** to accelerate. Release it to idle the engine. The HUD shows **Throttle button HELD / RELEASED**. At about **160 knots**, gently tilt the front up to take off. About **25 degrees** of tilt gives full control input.

| Control | Action |
|---|---|
| Tilt front up/down | Pitch up/down |
| Tilt right/left | Roll right/left |
| Hold breadboard button (D23) | Full throttle |
| Release breadboard button | Engine idle; the aircraft can still coast |
| Q / E | Yaw / ground steering |
| Space | Wheel brakes |
| C | Recalibrate neutral and gyro bias; hold still for about two seconds |
| M | Switch between motion and the original keyboard controls |
| G | Landing gear |
| R | Reset aircraft on runway |

With motion selected, W/S and A/D are replaced by the sensor, and the physical button replaces mouse throttle. Mouse clicks cannot apply throttle in this mode. Q/E and the other keys stay available. Press M to use the original keyboard/mouse controls instead. Recalibration, losing application focus, or missing sensor data temporarily idles the throttle and engages the wheel brakes. Disconnect detection uses a 0.3-second freshness window. Reconnection retries automatically and requires fresh stationary calibration.

## Throttle-button wiring

Connect a normally-open momentary button between **D23 / GPIO23** and **ESP32 GND**, using diagonally opposite legs for a standard four-legged pushbutton. The firmware enables the internal pull-up resistor, so no external resistor or button power wire is needed. It debounces press and release for 20 milliseconds. The button is hold-to-throttle, not a toggle.

If the HUD always says RELEASED when you press the button, check that its signal wire reaches D23 and its other terminal reaches ESP32 ground. If it always says HELD, check for an accidental D23-to-ground connection or two button legs that are already connected internally.

## Changing the feel or mounting direction

Stop Play mode, select **Aircraft - F-22 Raptor**, and find **Serial Motion Controller** in the Inspector. Change **Full Tilt Degrees**, **Dead Zone Degrees**, **Smoothing Seconds**, **Invert Pitch**, **Invert Roll**, or **Swap Axes**. Save the scene. If Windows assigns another COM number, update **Port Name** there. The defaults are 25° full tilt, 2° dead zone, and 0.06 seconds smoothing.

This version estimates pitch and roll from gyro and accelerometer readings. It does not use gyro yaw as an absolute heading, which would drift without a heading reference. Keep Q/E for yaw. Mount the sensor firmly so it moves with the breadboard.

## Firmware

`FlightController/FlightController.ino` contains the combined sensor and button firmware. Board: **ESP32 Dev Module** (`esp32:esp32:esp32`), tested chip ESP32-D0WD-V3. Sensor wiring is **SDA 21, SCL 22**, plus 3.3 V power and common ground; the button connects GPIO23 to ground. It detects sensor addresses 0x68 and 0x69.

The stream is 100 Hz. The MPU-6050 uses ±2g acceleration, ±250°/s gyro and its internal low-pass filter. Every line has a sequence number, microsecond timestamp, all six raw axes, a button value (0 released, 1 held), and a checksum:

`FL2,sequence,microseconds,ax,ay,az,gx,gy,gz,button*XX`

The updated Unity adapter requires FL2 firmware. Old FL1 sensor-only firmware and standalone sensor/button test sketches are rejected. To restore the combined controller later, stop Unity Play mode and upload `FlightController/FlightController.ino` using **ESP32 Dev Module** and your board's serial port.

Uploading a standalone sensor or button test sketch replaces the combined controller program. Upload `FlightController/FlightController.ino` again before using Unity.

Unity reads serial data on a background thread, rejects damaged packets, calibrates gyro bias while stationary, and combines gyro rotation with accelerometer correction. It then sends normalized pitch/roll and the throttle button to the same input component used by keyboard control. This serial implementation supports the Windows Editor and Windows player.

Developer checks: **Flight Lab → Check motion controller software**. Reports are written to `Library/FlightLabValidation/Motion`. The controller is attached to both the Raptor Valley scene and the F22-Player prefab.
