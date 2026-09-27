// Flight Lab USB motion controller — ESP32 Dev Module, MPU-6050.
// Existing wiring: SDA GPIO21, SCL GPIO22, 3.3V and common GND.
// Throttle: normally-open pushbutton between GPIO23 (D23) and GND.
// USB serial: 115200 baud, 100 packets/s. No Wi-Fi or Bluetooth needed.
#include <Arduino.h>
#include <Wire.h>

constexpr int SDA_PIN = 21;
constexpr int SCL_PIN = 22;
constexpr int THROTTLE_PIN = 23;
constexpr uint32_t BUTTON_DEBOUNCE_MS = 20;
bool throttlePressed = false, buttonCandidate = false;
uint32_t buttonChangedAt = 0;
uint8_t sensorAddress = 0x68;
uint32_t sequence = 0, nextSample = 0, lastAttempt = 0;
bool sensorReady = false;

bool writeRegister(uint8_t reg, uint8_t value) {
  Wire.beginTransmission(sensorAddress);
  Wire.write(reg); Wire.write(value);
  return Wire.endTransmission() == 0;
}

bool readRegisters(uint8_t reg, uint8_t *buffer, uint8_t count) {
  Wire.beginTransmission(sensorAddress); Wire.write(reg);
  if (Wire.endTransmission(false) != 0) return false;
  if (Wire.requestFrom(sensorAddress, count, true) != count) return false;
  for (uint8_t i = 0; i < count; ++i) buffer[i] = Wire.read();
  return true;
}

bool initializeSensor() {
  for (uint8_t address : {uint8_t(0x68), uint8_t(0x69)}) {
    sensorAddress = address;
    uint8_t identity = 0;
    if (readRegisters(0x75, &identity, 1) && (identity & 0x7E) == 0x68) {
      // PLL clock, ~42 Hz gyro filter, 100 Hz sampling, +/-250 deg/s, +/-2g.
      if (!writeRegister(0x6B, 0x01) || !writeRegister(0x1A, 0x03) ||
          !writeRegister(0x19, 9) || !writeRegister(0x1B, 0) || !writeRegister(0x1C, 0)) return false;
      delay(100);
      return true;
    }
  }
  return false;
}

int16_t signedWord(const uint8_t *bytes) {
  return int16_t((uint16_t(bytes[0]) << 8) | bytes[1]);
}

void updateThrottleButton() {
  bool pressed = digitalRead(THROTTLE_PIN) == LOW;
  uint32_t now = millis();
  if (pressed != buttonCandidate) {
    buttonCandidate = pressed;
    buttonChangedAt = now;
  }
  if (now - buttonChangedAt >= BUTTON_DEBOUNCE_MS) throttlePressed = buttonCandidate;
}

void setup() {
  pinMode(THROTTLE_PIN, INPUT_PULLUP);
  Serial.begin(115200);
  Wire.begin(SDA_PIN, SCL_PIN); Wire.setClock(100000); Wire.setTimeOut(25);
  delay(300);
  Serial.println("# Flight Lab FL2 motion + D23 throttle; hold controller still for Unity calibration.");
  sensorReady = initializeSensor();
  nextSample = micros();
}

void loop() {
  updateThrottleButton();
  if (!sensorReady) {
    if (millis() - lastAttempt >= 1000) {
      lastAttempt = millis(); sensorReady = initializeSensor();
      if (!sensorReady) Serial.println("# MPU6050 unavailable: check power, GND, SDA21 and SCL22.");
      nextSample = micros();
    }
    delay(1); return;
  }
  uint32_t now = micros();
  if (int32_t(now - nextSample) < 0) { delay(1); return; }
  nextSample += 10000;
  if (int32_t(now - nextSample) > 20000) nextSample = now + 10000;
  uint8_t bytes[14];
  if (!readRegisters(0x3B, bytes, sizeof(bytes))) { sensorReady = false; return; }
  char payload[112];
  snprintf(payload, sizeof(payload), "FL2,%lu,%lu,%d,%d,%d,%d,%d,%d,%d",
    (unsigned long)sequence++, (unsigned long)now,
    signedWord(bytes), signedWord(bytes + 2), signedWord(bytes + 4),
    signedWord(bytes + 8), signedWord(bytes + 10), signedWord(bytes + 12), throttlePressed ? 1 : 0);
  uint8_t checksum = 0;
  for (const char *p = payload; *p; ++p) checksum ^= uint8_t(*p);
  Serial.printf("%s*%02X\n", payload, checksum);
}
