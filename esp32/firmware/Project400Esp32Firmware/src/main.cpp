#include <Arduino.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <Adafruit_PN532.h>
#include <qrcode.h>
#include <PubSubClient.h>
#include <mbedtls/md.h>
#include <mbedtls/base64.h>
#include <time.h>

const char* WIFI_SSID     = "tempHotspot";
const char* WIFI_PASSWORD = "Password123!";

const char* DEVICE_ID = "esp32-door-001";

const char* QR_BASE_URL = "https://www.eunanmurray.ie/q/";

const char* IOT_HUB_HOST = "project400-iothub-nwe.azure-devices.net";
const int   MQTT_PORT = 8883;
const char* DEVICE_KEY = "7O9yPXD0VNo5CYJCkQGa1pndhwfNjP7WiKaZWrZZii4=";

const int PIN_BUTTON = D1;
const int PIN_BUZZER = A3;
const int PIN_PIR    = D2;
const int PIN_LED    = LED_BUILTIN;

const int BUZZER_CHANNEL = 0;
const int BUZZER_RESOLUTION = 8;

const unsigned long AUTH_TIMEOUT     = 60000;
const unsigned long UNLOCK_DURATION  = 5000;
const unsigned long WIFI_RECONNECT_COOLDOWN = 30000;
const unsigned long MQTT_RECONNECT_INTERVAL = 5000;
const unsigned long SAS_TOKEN_LIFETIME = 86400;
const bool PIR_AUTO_TRIGGER = false;

#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, -1);
bool oledAvailable = false;

#define PN532_IRQ   (2)
#define PN532_RESET (3)
Adafruit_PN532 nfc(PN532_IRQ, PN532_RESET);
bool nfcAvailable = false;

unsigned long lastWiFiReconnectAttempt = 0;

enum State { IDLE, QR_DISPLAY, WAITING_AUTH, UNLOCKED, TIMEOUT_STATE };
State currentState = IDLE;

unsigned long stateStartTime = 0;
unsigned long lastLedToggle = 0;

bool ledState = false;
bool buttonPressed = false;
bool lastButtonState = HIGH;
bool lastPIRState = false;
String scannedCardUid = "";
String unlockShortCode = "";

WiFiClientSecure mqttWifiClient;
PubSubClient mqttClient(mqttWifiClient);

String sasToken = "";
unsigned long sasTokenExpiry = 0;
unsigned long lastMqttReconnectAttempt = 0;

volatile bool mqttUnlockReceived = false;
volatile bool mqttDenyReceived = false;
volatile bool mqttShowQrReceived = false;
String mqttUnlockCode = "";

void setupWiFi();
void setupDisplay();
void setupNFC();
void setupMqtt();
bool connectMqtt();
void maintainMqtt();
void mqttCallback(char* topic, byte* payload, unsigned int length);
String generateSasToken(const char* resourceUri, const char* key, unsigned long expiryEpoch);
String urlEncode(const String& str);
void drawCenteredText(const String& text, int y, int size = 1);
void displayQRCode(const String& url);
void handleQRDisplay();
void updateDisplay(const String&, const String& = "", const String& = "");
void beep(int ms, int freq = 800);
void beepSuccess();
void beepError();
void publishCardScan(const String& cardUid);
void handleIdle();
void handleWaitingAuth();
void handleUnlocked();
void handleTimeout();
void checkButton();
void checkPIR();
void checkNFC();

void setup() {
  Serial.begin(115200);
  delay(300);

  pinMode(PIN_BUTTON, INPUT_PULLUP);
  pinMode(PIN_PIR, INPUT);
  pinMode(PIN_LED, OUTPUT);

  ledcSetup(BUZZER_CHANNEL, 2000, BUZZER_RESOLUTION);
  ledcAttachPin(PIN_BUZZER, BUZZER_CHANNEL);
  ledcWrite(BUZZER_CHANNEL, 0);

  digitalWrite(PIN_LED, LOW);

  Wire.begin(SDA, SCL);
  Wire.setClock(400000);

  setupDisplay();
  setupNFC();
  setupWiFi();

  setupMqtt();
  connectMqtt();

  currentState = IDLE;
  stateStartTime = millis();
  updateDisplay("Smart Access", "Scan card", "to unlock");

  Serial.println("System ready");
}

void loop() {
  maintainMqtt();

  checkNFC();
  checkButton();
  checkPIR();

  switch (currentState) {
    case IDLE:            handleIdle();           break;
    case QR_DISPLAY:      handleQRDisplay();      break;
    case WAITING_AUTH:    handleWaitingAuth();    break;
    case UNLOCKED:        handleUnlocked();       break;
    case TIMEOUT_STATE:   handleTimeout();        break;
  }

  if (WiFi.status() != WL_CONNECTED) {
    unsigned long now = millis();
    if (now - lastWiFiReconnectAttempt > WIFI_RECONNECT_COOLDOWN) {
      lastWiFiReconnectAttempt = now;
      Serial.println("WiFi disconnected, attempting reconnect...");
      WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    }
  }
}

void setupWiFi() {
  updateDisplay("Connecting WiFi", WIFI_SSID);

  WiFi.disconnect(true);
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(true);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  for (int i = 0; i < 40 && WiFi.status() != WL_CONNECTED; i++) {
    delay(500);
    Serial.print(".");
  }

  if (WiFi.status() == WL_CONNECTED) {
    IPAddress dns1(8, 8, 8, 8);
    IPAddress dns2(1, 1, 1, 1);
    WiFi.config(WiFi.localIP(), WiFi.gatewayIP(), WiFi.subnetMask(), dns1, dns2);
    Serial.println("\nWiFi connected, DNS set to 8.8.8.8 / 1.1.1.1");
    updateDisplay("WiFi Connected", WiFi.localIP().toString());
    lastWiFiReconnectAttempt = millis();

    configTime(0, 0, "pool.ntp.org", "time.nist.gov");
    Serial.print("Waiting for NTP time sync");
    time_t now = time(nullptr);
    int retries = 0;
    while (now < 1700000000 && retries < 20) {
      delay(500);
      Serial.print(".");
      now = time(nullptr);
      retries++;
    }
    Serial.printf("\nTime synced: %lu\n", (unsigned long)now);

    delay(500);
  } else {
    Serial.println("\nWiFi failed");
    updateDisplay("WiFi Failed");
  }
}

void setupDisplay() {
  oledAvailable = display.begin(SSD1306_SWITCHCAPVCC, 0x3C);

  if (!oledAvailable) {
    Serial.println("OLED not found");
    return;
  }

  // Increase display clock frequency to reduce flicker visible on phone cameras.
  // Command 0xD5 sets the clock divide ratio & oscillator frequency.
  // High nibble 0xF0 = max oscillator freq, low nibble 0x00 = divide ratio 1.
  // This makes the display scan fast enough that phone cameras won't see banding.
  display.ssd1306_command(SSD1306_SETDISPLAYCLOCKDIV);
  display.ssd1306_command(0xF0);

  display.clearDisplay();
  display.setTextColor(SSD1306_WHITE);
  drawCenteredText("Smart Access", 8, 2);
  drawCenteredText("Initializing...", 40);
  display.display();
}

void drawCenteredText(const String& text, int y, int size) {
  display.setTextSize(size);
  int16_t x1, y1;
  uint16_t w, h;
  display.getTextBounds(text.c_str(), 0, 0, &x1, &y1, &w, &h);
  display.setCursor((SCREEN_WIDTH - w) / 2, y);
  display.print(text);
}

void updateDisplay(const String& l1, const String& l2, const String& l3) {
  if (!oledAvailable) return;

  display.clearDisplay();
  display.setTextColor(SSD1306_WHITE);

  if (l3.length()) {
    drawCenteredText(l1, 8);
    drawCenteredText(l2, 28);
    drawCenteredText(l3, 48);
  } else if (l2.length()) {
    drawCenteredText(l1, 16);
    drawCenteredText(l2, 38);
  } else {
    drawCenteredText(l1, 24, 2);
  }

  display.display();
}

void displayQRCode(const String& url) {
  if (!oledAvailable) {
    Serial.println("OLED not available, cannot display QR");
    return;
  }

  Serial.println("Generating QR code for: " + url);

  QRCode qrcode;
  uint8_t qrcodeData[qrcode_getBufferSize(3)];
  qrcode_initText(&qrcode, qrcodeData, 3, ECC_LOW, url.c_str());

  display.clearDisplay();

  int moduleSize = 2;
  int qrSize = qrcode.size * moduleSize;

  int qrOffsetX = 2;
  int qrOffsetY = (SCREEN_HEIGHT - qrSize) / 2;

  const int quiet = 2;
  display.fillRect(
    qrOffsetX - quiet,
    qrOffsetY - quiet,
    qrSize + 2 * quiet,
    qrSize + 2 * quiet,
    SSD1306_WHITE
  );

  for (int y = 0; y < qrcode.size; y++) {
    for (int x = 0; x < qrcode.size; x++) {
      if (qrcode_getModule(&qrcode, x, y)) {
        display.fillRect(
          qrOffsetX + x * moduleSize,
          qrOffsetY + y * moduleSize,
          moduleSize,
          moduleSize,
          SSD1306_BLACK
        );
      }
    }
  }

  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);

  int textX = qrOffsetX + qrSize + quiet + 4;
  display.setCursor(textX, 4);
  display.print("Scan to");
  display.setCursor(textX, 14);
  display.print("unlock");

  display.display();

  Serial.println("QR code displayed on OLED");
}

void beep(int ms, int freq) {
  ledcWriteTone(BUZZER_CHANNEL, freq);
  delay(ms);
  ledcWrite(BUZZER_CHANNEL, 0);
}

void beepSuccess() {
  beep(100); delay(100); beep(100);
}

void beepError() {
  beep(500, 300);
}

void checkButton() {
  bool state = digitalRead(PIN_BUTTON);

  if (lastButtonState == HIGH && state == LOW) {
    buttonPressed = true;
    Serial.println("Button pressed");
    delay(30);
  }
  lastButtonState = state;
}

void checkPIR() {
  if (currentState != IDLE) return;
  bool pirState = digitalRead(PIN_PIR) == HIGH;
  if (pirState && !lastPIRState) {
    Serial.println("Motion detected");
  }
  lastPIRState = pirState;
}

void setupNFC() {
  Serial.println("Initializing PN532...");
  updateDisplay("Init NFC...");

  nfc.begin();

  uint32_t versiondata = nfc.getFirmwareVersion();
  if (!versiondata) {
    Serial.println("PN532 not found");
    updateDisplay("NFC Error", "Reader not found");
    nfcAvailable = false;
    delay(2000);
    return;
  }

  Serial.print("Found PN532 chip v");
  Serial.print((versiondata >> 24) & 0xFF, HEX);
  Serial.print(".");
  Serial.println((versiondata >> 16) & 0xFF, HEX);

  nfc.SAMConfig();
  nfcAvailable = true;

  updateDisplay("NFC Ready");
  delay(1000);
}

void checkNFC() {
  if (!nfcAvailable || currentState != IDLE) return;

  uint8_t uid[] = { 0, 0, 0, 0, 0, 0, 0 };
  uint8_t uidLength;

  bool success = nfc.readPassiveTargetID(PN532_MIFARE_ISO14443A, uid, &uidLength, 100);

  if (success && uidLength > 0) {
    String cardUid = "";
    for (uint8_t i = 0; i < uidLength; i++) {
      if (cardUid.length() > 0) cardUid += ":";
      if (uid[i] < 0x10) cardUid += "0";
      cardUid += String(uid[i], HEX);
    }
    cardUid.toUpperCase();

    Serial.print("Card detected: ");
    Serial.println(cardUid);

    scannedCardUid = cardUid;

    delay(1000);
  }
}

String urlEncode(const String& str) {
  String encoded = "";
  for (unsigned int i = 0; i < str.length(); i++) {
    char c = str.charAt(i);
    if (isAlphaNumeric(c) || c == '-' || c == '_' || c == '.' || c == '~') {
      encoded += c;
    } else {
      char buf[4];
      snprintf(buf, sizeof(buf), "%%%02X", (unsigned char)c);
      encoded += buf;
    }
  }
  return encoded;
}

String generateSasToken(const char* resourceUri, const char* key, unsigned long expiryEpoch) {
  size_t keyLen = strlen(key);
  unsigned char decodedKey[64];
  size_t decodedKeyLen = 0;
  int decRet = mbedtls_base64_decode(decodedKey, sizeof(decodedKey), &decodedKeyLen,
                        (const unsigned char*)key, keyLen);
  Serial.printf("[SAS] Key decode: ret=%d, keyLen=%u, decodedLen=%u\n",
                decRet, (unsigned)keyLen, (unsigned)decodedKeyLen);

  String encodedUri = urlEncode(String(resourceUri));
  String stringToSign = encodedUri + "\n" + String(expiryEpoch);
  Serial.printf("[SAS] String to sign: %s\n", stringToSign.c_str());

  unsigned char hmacResult[32];
  mbedtls_md_context_t ctx;
  mbedtls_md_init(&ctx);
  int setupRet = mbedtls_md_setup(&ctx, mbedtls_md_info_from_type(MBEDTLS_MD_SHA256), 1);
  mbedtls_md_hmac_starts(&ctx, decodedKey, decodedKeyLen);
  mbedtls_md_hmac_update(&ctx, (const unsigned char*)stringToSign.c_str(), stringToSign.length());
  mbedtls_md_hmac_finish(&ctx, hmacResult);
  mbedtls_md_free(&ctx);
  Serial.printf("[SAS] HMAC setup ret=%d\n", setupRet);

  unsigned char base64Sig[68];
  size_t base64SigLen = 0;
  int encRet = mbedtls_base64_encode(base64Sig, sizeof(base64Sig), &base64SigLen, hmacResult, 32);
  base64Sig[base64SigLen] = '\0';
  String signature = String((char*)base64Sig);
  Serial.printf("[SAS] Sig encode: ret=%d, len=%u, sig=%s\n",
                encRet, (unsigned)base64SigLen, signature.c_str());

  String token = "SharedAccessSignature sr=" + encodedUri
               + "&sig=" + urlEncode(signature)
               + "&se=" + String(expiryEpoch);

  Serial.printf("[SAS] Full token (%u chars): %s\n", token.length(), token.c_str());

  return token;
}

void mqttCallback(char* topic, byte* payload, unsigned int length) {
  Serial.printf("MQTT message on topic: %s\n", topic);
  Serial.printf("Payload (%u bytes): ", length);
  for (unsigned int i = 0; i < length; i++) {
    Serial.print((char)payload[i]);
  }
  Serial.println();

  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, payload, length);

  if (error) {
    Serial.printf("MQTT JSON parse error: %s\n", error.c_str());
    return;
  }

  const char* command = doc["command"] | "";
  Serial.printf("MQTT command received: %s\n", command);

  if (strcmp(command, "unlock") == 0) {
    mqttUnlockReceived = true;
    Serial.println(">>> UNLOCK command from IoT Hub");
  } else if (strcmp(command, "deny") == 0) {
    mqttDenyReceived = true;
    Serial.println(">>> DENY command from IoT Hub");
  } else if (strcmp(command, "showQr") == 0) {
    const char* code = doc["unlockCode"] | "";
    if (strlen(code) > 0) {
      mqttUnlockCode = String(code);
      mqttShowQrReceived = true;
      Serial.printf(">>> SHOWQR command from IoT Hub, code: %s\n", code);
    } else {
      Serial.println(">>> SHOWQR command received but no unlockCode");
    }
  }
}

void setupMqtt() {
  mqttWifiClient.setInsecure();
  mqttClient.setServer(IOT_HUB_HOST, MQTT_PORT);
  mqttClient.setCallback(mqttCallback);
  mqttClient.setKeepAlive(240);
  mqttClient.setBufferSize(512);

  Serial.println("MQTT configured for Azure IoT Hub");
}

bool connectMqtt() {
  if (mqttClient.connected()) return true;
  if (WiFi.status() != WL_CONNECTED) return false;

  time_t now = time(nullptr);
  if (now < 1700000000) {
    Serial.printf("NTP not synced yet (time=%lu), skipping MQTT connect\n", (unsigned long)now);
    return false;
  }

  Serial.println("Connecting to Azure IoT Hub via MQTT...");
  Serial.printf("Free heap before MQTT connect: %u bytes\n", ESP.getFreeHeap());
  Serial.printf("Current epoch time: %lu\n", (unsigned long)now);

  if (sasToken.isEmpty() || now >= (time_t)(sasTokenExpiry - 300)) {
    sasTokenExpiry = (unsigned long)now + SAS_TOKEN_LIFETIME;
    String resourceUri = String(IOT_HUB_HOST) + "/devices/" + String(DEVICE_ID);
    sasToken = generateSasToken(resourceUri.c_str(), DEVICE_KEY, sasTokenExpiry);
    Serial.printf("SAS token generated (length=%u), expires at: %lu\n",
                  sasToken.length(), sasTokenExpiry);
  }

  String mqttUsername = String(IOT_HUB_HOST) + "/" + String(DEVICE_ID)
                      + "/?api-version=2021-04-12";

  Serial.printf("MQTT username: %s\n", mqttUsername.c_str());

  bool connected = mqttClient.connect(
    DEVICE_ID,
    mqttUsername.c_str(),
    sasToken.c_str()
  );

  if (connected) {
    Serial.println("MQTT connected to Azure IoT Hub!");

    String c2dTopic = "devices/" + String(DEVICE_ID) + "/messages/devicebound/#";
    bool subOk = mqttClient.subscribe(c2dTopic.c_str(), 1);
    Serial.printf("Subscribed to C2D topic: %s (result: %s)\n",
                  c2dTopic.c_str(), subOk ? "OK" : "FAILED");

    Serial.printf("Free heap after MQTT connect: %u bytes\n", ESP.getFreeHeap());
  } else {
    int rc = mqttClient.state();
    Serial.printf("MQTT connect failed, rc=%d\n", rc);

    mqttWifiClient.stop();
    Serial.printf("MQTT TLS released. Free heap: %u bytes\n", ESP.getFreeHeap());

    if (rc == 5) {
      sasToken = "";
      Serial.println("Will regenerate SAS token on next attempt");
    }
  }

  return connected;
}

void maintainMqtt() {
  time_t now = time(nullptr);
  if (mqttClient.connected() && now >= (time_t)(sasTokenExpiry - 300)) {
    Serial.println("SAS token expiring, reconnecting MQTT...");
    mqttClient.disconnect();
    sasToken = "";
  }

  if (!mqttClient.connected()) {
    unsigned long nowMs = millis();
    if (nowMs - lastMqttReconnectAttempt > MQTT_RECONNECT_INTERVAL) {
      lastMqttReconnectAttempt = nowMs;
      connectMqtt();
    }
  } else {
    mqttClient.loop();
  }
}

void publishCardScan(const String& cardUid) {
  if (!mqttClient.connected()) {
    Serial.println("MQTT not connected, cannot publish card scan");
    return;
  }

  String d2cTopic = "devices/" + String(DEVICE_ID) + "/messages/events/";

  JsonDocument doc;
  doc["messageType"] = "cardScan";
  doc["cardUid"] = cardUid;

  char payload[128];
  serializeJson(doc, payload, sizeof(payload));

  bool published = mqttClient.publish(d2cTopic.c_str(), payload);

  if (published) {
    Serial.printf("D2C card scan published: %s\n", payload);
  } else {
    Serial.println("Failed to publish D2C card scan");
  }
}

void handleIdle() {
  mqttUnlockReceived = false;
  mqttDenyReceived = false;
  mqttShowQrReceived = false;
  mqttUnlockCode = "";

  digitalWrite(PIN_LED, LOW);

  if (!scannedCardUid.isEmpty()) {
    String cardUid = scannedCardUid;
    scannedCardUid = "";

    updateDisplay("Card Detected", "Processing...");
    beep(100);

    publishCardScan(cardUid);

    currentState = WAITING_AUTH;
    stateStartTime = millis();
  }

  if (buttonPressed) {
    buttonPressed = false;
    scannedCardUid = "TEST-CARD-001";
  }
}

void handleQRDisplay() {
  unsigned long now = millis();

  if (now - lastLedToggle > 300) {
    ledState = !ledState;
    digitalWrite(PIN_LED, ledState);
    lastLedToggle = now;
  }

  if (now - stateStartTime > AUTH_TIMEOUT) {
    currentState = TIMEOUT_STATE;
    stateStartTime = now;
    unlockShortCode = "";
    updateDisplay("Timed Out", "Try again");
    beepError();
    return;
  }

  if (mqttUnlockReceived) {
    mqttUnlockReceived = false;
    mqttDenyReceived = false;
    currentState = UNLOCKED;
    stateStartTime = now;
    unlockShortCode = "";
    updateDisplay("ACCESS", "GRANTED");
    beepSuccess();
    digitalWrite(PIN_LED, HIGH);
    return;
  }

  if (mqttDenyReceived) {
    mqttDenyReceived = false;
    currentState = TIMEOUT_STATE;
    stateStartTime = now;
    unlockShortCode = "";
    updateDisplay("ACCESS", "DENIED");
    beepError();
    return;
  }
}

void handleWaitingAuth() {
  unsigned long now = millis();

  if (now - lastLedToggle > 500) {
    ledState = !ledState;
    digitalWrite(PIN_LED, ledState);
    lastLedToggle = now;
  }

  if (now - stateStartTime > AUTH_TIMEOUT) {
    currentState = TIMEOUT_STATE;
    stateStartTime = now;
    unlockShortCode = "";
    updateDisplay("Timed Out", "Try again");
    beepError();
    return;
  }

  if (mqttShowQrReceived) {
    mqttShowQrReceived = false;
    unlockShortCode = mqttUnlockCode;
    mqttUnlockCode = "";

    if (!unlockShortCode.isEmpty()) {
      String url = String(QR_BASE_URL) + unlockShortCode;
      displayQRCode(url);
      currentState = QR_DISPLAY;
      stateStartTime = millis();
    } else {
      updateDisplay("Error", "No unlock code");
      beepError();
      delay(2000);
      currentState = IDLE;
      updateDisplay("Smart Access", "Scan card", "to unlock");
    }
    return;
  }

  if (mqttDenyReceived) {
    mqttDenyReceived = false;
    currentState = TIMEOUT_STATE;
    stateStartTime = now;
    updateDisplay("ACCESS", "DENIED");
    beepError();
    return;
  }

  if (mqttUnlockReceived) {
    mqttUnlockReceived = false;
    mqttDenyReceived = false;
    currentState = UNLOCKED;
    stateStartTime = now;
    updateDisplay("ACCESS", "GRANTED");
    beepSuccess();
    digitalWrite(PIN_LED, HIGH);
    return;
  }
}

void handleUnlocked() {
  if (millis() - stateStartTime > UNLOCK_DURATION) {
    currentState = IDLE;
    updateDisplay("Smart Access", "Scan card", "to unlock");
    digitalWrite(PIN_LED, LOW);
  }
}

void handleTimeout() {
  if (millis() - stateStartTime > 3000) {
    currentState = IDLE;
    updateDisplay("Smart Access", "Scan card", "to unlock");
  }
}
