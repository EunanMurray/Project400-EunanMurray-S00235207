#include <Arduino.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include <PubSubClient.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <mbedtls/md.h>
#include <mbedtls/base64.h>
#include <time.h>
#include "esp_camera.h"

const char* WIFI_SSID     = "tempHotspot";
const char* WIFI_PASSWORD = "Password123!";

const char* DEVICE_ID          = "esp32-camera-001";
const char* PAIRED_DOOR_DEVICE = "esp32-door-001";

const char* API_BASE_URL   = "https://ca-project400-api.wittystone-4c147989.francecentral.azurecontainerapps.io";
const char* DEVICE_API_KEY = "prod-camera-key-change-this";

const char* IOT_HUB_HOST = "project400-iothub-nwe.azure-devices.net";
const int   MQTT_PORT = 8883;
const char* DEVICE_KEY = "H4Bo8gaCN0e1TvXWnwny40B3Q9Wav6PB4SeXy964s2c=";

const unsigned long CAPTURE_DELAY_MS        = 5000;
const unsigned long WIFI_RECONNECT_COOLDOWN = 30000;
const unsigned long MQTT_RECONNECT_INTERVAL = 5000;
const unsigned long SAS_TOKEN_LIFETIME      = 86400;
const unsigned long COOLDOWN_BETWEEN_CAPTURES = 10000;

#define PWDN_GPIO_NUM  -1
#define RESET_GPIO_NUM -1
#define XCLK_GPIO_NUM  10
#define SIOD_GPIO_NUM  40
#define SIOC_GPIO_NUM  39

#define Y9_GPIO_NUM    48
#define Y8_GPIO_NUM    11
#define Y7_GPIO_NUM    12
#define Y6_GPIO_NUM    14
#define Y5_GPIO_NUM    16
#define Y4_GPIO_NUM    18
#define Y3_GPIO_NUM    17
#define Y2_GPIO_NUM    15
#define VSYNC_GPIO_NUM 38
#define HREF_GPIO_NUM  47
#define PCLK_GPIO_NUM  13

#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, -1);
bool oledAvailable = false;

WiFiClientSecure mqttWifiClient;
PubSubClient mqttClient(mqttWifiClient);

String sasToken = "";
unsigned long sasTokenExpiry = 0;
unsigned long lastMqttReconnectAttempt = 0;
unsigned long lastWiFiReconnectAttempt = 0;
unsigned long lastCaptureTime = 0;

volatile bool doorUnlockReceived = false;
String lastCardUid = "";

void setupDisplay();
void drawCenteredText(const String& text, int y, int size = 1);
void displayStatus(const String& l1, const String& l2 = "", const String& l3 = "");
void setupWiFi();
void setupCamera();
void setupMqtt();
bool connectMqtt();
void maintainMqtt();
void mqttCallback(char* topic, byte* payload, unsigned int length);
String generateSasToken(const char* resourceUri, const char* key, unsigned long expiryEpoch);
String urlEncode(const String& str);
void captureAndAnalyze();
bool sendFrameToApi(uint8_t* jpegData, size_t jpegLen, const String& cardUid);

void setup() {
    Serial.begin(115200);
    delay(2000);

    Wire.begin();
    Wire.setClock(400000);
    setupDisplay();

    displayStatus("Initializing", "camera...");
    setupCamera();

    displayStatus("Connecting", "WiFi...");
    setupWiFi();

    displayStatus("Connecting", "IoT Hub...");
    setupMqtt();
    connectMqtt();

    displayStatus("System Ready", "Waiting for", "door events");
    Serial.println("Camera system ready");
}

void loop() {
    maintainMqtt();

    if (doorUnlockReceived) {
        doorUnlockReceived = false;

        unsigned long now = millis();
        if (now - lastCaptureTime < COOLDOWN_BETWEEN_CAPTURES) {
            Serial.println("Capture cooldown active, skipping");
            return;
        }

        displayStatus("Door Unlocked", "Capturing in", "5 seconds...");
        Serial.printf("Door unlock detected, waiting %lums...\n", CAPTURE_DELAY_MS);
        delay(CAPTURE_DELAY_MS);

        captureAndAnalyze();
        lastCaptureTime = millis();
    }

    if (WiFi.status() != WL_CONNECTED) {
        unsigned long now = millis();
        if (now - lastWiFiReconnectAttempt > WIFI_RECONNECT_COOLDOWN) {
            lastWiFiReconnectAttempt = now;
            displayStatus("WiFi Lost", "Reconnecting...");
            Serial.println("WiFi disconnected, reconnecting...");
            WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
        }
    }
}

void setupDisplay() {
    oledAvailable = display.begin(SSD1306_SWITCHCAPVCC, 0x3C);

    if (!oledAvailable) {
        Serial.println("OLED not found");
        return;
    }

    display.clearDisplay();
    display.setTextColor(SSD1306_WHITE);
    drawCenteredText("Smart Access", 8, 2);
    drawCenteredText("Camera Module", 40);
    display.display();
    Serial.println("OLED initialized");
}

void drawCenteredText(const String& text, int y, int size) {
    display.setTextSize(size);
    int16_t x1, y1;
    uint16_t w, h;
    display.getTextBounds(text.c_str(), 0, 0, &x1, &y1, &w, &h);
    display.setCursor((SCREEN_WIDTH - w) / 2, y);
    display.print(text);
}

void displayStatus(const String& l1, const String& l2, const String& l3) {
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

void setupCamera() {
    Serial.println("Initializing camera...");

    camera_config_t config = {};
    config.ledc_channel = LEDC_CHANNEL_0;
    config.ledc_timer   = LEDC_TIMER_0;
    config.pin_d0       = Y2_GPIO_NUM;
    config.pin_d1       = Y3_GPIO_NUM;
    config.pin_d2       = Y4_GPIO_NUM;
    config.pin_d3       = Y5_GPIO_NUM;
    config.pin_d4       = Y6_GPIO_NUM;
    config.pin_d5       = Y7_GPIO_NUM;
    config.pin_d6       = Y8_GPIO_NUM;
    config.pin_d7       = Y9_GPIO_NUM;
    config.pin_xclk     = XCLK_GPIO_NUM;
    config.pin_pclk     = PCLK_GPIO_NUM;
    config.pin_vsync    = VSYNC_GPIO_NUM;
    config.pin_href     = HREF_GPIO_NUM;
    config.pin_sccb_sda = SIOD_GPIO_NUM;
    config.pin_sccb_scl = SIOC_GPIO_NUM;
    config.pin_pwdn     = PWDN_GPIO_NUM;
    config.pin_reset    = RESET_GPIO_NUM;
    config.xclk_freq_hz = 20000000;
    config.pixel_format = PIXFORMAT_JPEG;
    config.frame_size   = FRAMESIZE_QVGA;
    config.jpeg_quality = 15;
    config.fb_count     = 1;
    config.grab_mode    = CAMERA_GRAB_WHEN_EMPTY;
    config.fb_location  = CAMERA_FB_IN_PSRAM;

    if (!psramFound()) {
        config.fb_location = CAMERA_FB_IN_DRAM;
        Serial.println("Warning: No PSRAM, using DRAM");
    } else {
        Serial.printf("PSRAM found: %u bytes\n", ESP.getPsramSize());
    }

    esp_err_t err = esp_camera_init(&config);
    if (err != ESP_OK) {
        Serial.printf("Camera init failed: 0x%x\n", err);
        displayStatus("Camera FAILED", "Restarting...");
        delay(5000);
        ESP.restart();
    }

    sensor_t* sensor = esp_camera_sensor_get();
    if (sensor) {
        Serial.printf("Camera PID: 0x%02x\n", sensor->id.PID);
        sensor->set_brightness(sensor, 1);
        sensor->set_contrast(sensor, 1);
        sensor->set_saturation(sensor, 0);
        sensor->set_whitebal(sensor, 1);
        sensor->set_awb_gain(sensor, 1);
        sensor->set_wb_mode(sensor, 0);
        sensor->set_exposure_ctrl(sensor, 1);
        sensor->set_aec2(sensor, 1);
        sensor->set_gain_ctrl(sensor, 1);
        sensor->set_agc_gain(sensor, 0);
        sensor->set_gainceiling(sensor, (gainceiling_t)6);
    }

    displayStatus("Camera OK");
    Serial.println("Camera initialized");
}

void captureAndAnalyze() {
    displayStatus("Capturing", "frame...");
    Serial.println("Capturing frame...");

    camera_fb_t* fb = esp_camera_fb_get();
    if (fb) {
        esp_camera_fb_return(fb);
    }

    fb = esp_camera_fb_get();
    if (!fb) {
        Serial.println("Camera capture failed");
        displayStatus("Capture FAILED");
        delay(3000);
        displayStatus("System Ready", "Waiting for", "door events");
        return;
    }

    Serial.printf("Frame captured: %u bytes, %dx%d\n", fb->len, fb->width, fb->height);
    displayStatus("Analyzing...", String(fb->len) + " bytes");

    if (WiFi.status() == WL_CONNECTED) {
        Serial.println("Disconnecting MQTT to free TLS memory...");
        mqttClient.disconnect();
        mqttWifiClient.stop();
        delay(100);
        Serial.printf("Free heap before send: %u\n", ESP.getFreeHeap());

        bool success = sendFrameToApi(fb->buf, fb->len, lastCardUid);
        esp_camera_fb_return(fb);

        if (!success) {
            displayStatus("API Error", "Check logs");
            delay(3000);
        }

        displayStatus("System Ready", "Waiting for", "door events");
        Serial.println("Reconnecting MQTT...");
        connectMqtt();
    } else {
        Serial.println("WiFi not connected, cannot send frame");
        displayStatus("No WiFi", "Frame discarded");
        esp_camera_fb_return(fb);
        delay(3000);
        displayStatus("System Ready", "Waiting for", "door events");
    }
}

bool sendFrameToApi(uint8_t* jpegData, size_t jpegLen, const String& cardUid) {
    WiFiClientSecure client;
    client.setInsecure();
    client.setHandshakeTimeout(15);
    client.setTimeout(15000);
    String host = String(API_BASE_URL);
    host.replace("https://", "");
    int slashIdx = host.indexOf('/');
    if (slashIdx > 0) host = host.substring(0, slashIdx);

    Serial.printf("Connecting to %s:443...\n", host.c_str());
    Serial.printf("Free heap before TLS connect: %u\n", ESP.getFreeHeap());

    if (!client.connect(host.c_str(), 443)) {
        Serial.println("TLS connection failed");
        return false;
    }

    Serial.printf("Free heap after TLS connect: %u\n", ESP.getFreeHeap());

    String path = "/api/tailgate/analyze"
        "?cameraDeviceId=" + String(DEVICE_ID) +
        "&doorDeviceId=" + String(PAIRED_DOOR_DEVICE) +
        "&apiKey=" + String(DEVICE_API_KEY);

    if (cardUid.length() > 0) {
        path += "&lastCardUid=" + cardUid;
    }

    client.printf("POST %s HTTP/1.1\r\n", path.c_str());
    client.printf("Host: %s\r\n", host.c_str());
    client.print("Content-Type: image/jpeg\r\n");
    client.printf("Content-Length: %u\r\n", jpegLen);
    client.print("Connection: close\r\n\r\n");

    const size_t CHUNK = 256;
    size_t sent = 0;
    while (sent < jpegLen) {
        size_t toSend = min(CHUNK, jpegLen - sent);
        int written = client.write(jpegData + sent, toSend);

        if (written < 0) {
            Serial.printf("Write error at byte %u/%u\n", sent, jpegLen);
            client.stop();
            return false;
        }

        if (written == 0) {
            Serial.printf("Write returned 0 at byte %u/%u\n", sent, jpegLen);
            client.stop();
            return false;
        }

        sent += (size_t)written;
    }

    Serial.printf("Sent %u bytes, waiting for response...\n", sent);

    unsigned long start = millis();
    while (client.connected() && !client.available()) {
        if (millis() - start > 15000) {
            Serial.println("Response timeout");
            client.stop();
            return false;
        }
        delay(10);
    }

    String statusLine = client.readStringUntil('\n');
    int httpCode = 0;
    int spaceIdx = statusLine.indexOf(' ');
    if (spaceIdx > 0) {
        httpCode = statusLine.substring(spaceIdx + 1, spaceIdx + 4).toInt();
    }

    while (client.available()) {
        String headerLine = client.readStringUntil('\n');
        if (headerLine == "\r" || headerLine.length() <= 1) break;
    }

    String response = "";
    while (client.available()) {
        response += (char)client.read();
    }
    client.stop();

    Serial.printf("API response: %d\n", httpCode);

    if (httpCode == 200) {
        JsonDocument doc;
        DeserializationError error = deserializeJson(doc, response);
        if (!error) {
            bool isTailgating = doc["isTailgating"] | false;
            int peopleDetected = doc["peopleDetected"] | 0;
            double confidence = doc["confidence"] | 0.0;

            Serial.printf("Result: %d people, tailgating: %s, confidence: %.2f\n",
                peopleDetected, isTailgating ? "YES" : "no", confidence);

            if (isTailgating) {
                displayStatus("TAILGATING!", String(peopleDetected) + " people", "Conf: " + String(confidence * 100, 0) + "%");
            } else {
                displayStatus("All Clear", String(peopleDetected) + " person(s)", "Conf: " + String(confidence * 100, 0) + "%");
            }
            delay(3000);
            displayStatus("System Ready", "Waiting for", "door events");
        }
    } else {
        Serial.printf("Error response: %s\n", response.c_str());
        displayStatus("API Error", "HTTP " + String(httpCode));
        delay(3000);
        displayStatus("System Ready", "Waiting for", "door events");
    }

    return httpCode == 200;
}

void setupWiFi() {
    Serial.printf("Connecting to WiFi: %s\n", WIFI_SSID);

    WiFi.disconnect(true);
    WiFi.mode(WIFI_STA);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

    for (int i = 0; i < 40 && WiFi.status() != WL_CONNECTED; i++) {
        delay(500);
        Serial.print(".");
    }

    if (WiFi.status() == WL_CONNECTED) {
        Serial.printf("\nWiFi connected: %s\n", WiFi.localIP().toString().c_str());
        displayStatus("WiFi OK", WiFi.localIP().toString());

        configTime(0, 0, "pool.ntp.org", "time.nist.gov");
        Serial.print("Syncing NTP time");
        time_t now = time(nullptr);
        int retries = 0;
        while (now < 1700000000 && retries < 20) {
            delay(500);
            Serial.print(".");
            now = time(nullptr);
            retries++;
        }
        Serial.printf("\nTime synced: %lu\n", (unsigned long)now);

        lastWiFiReconnectAttempt = millis();
    } else {
        Serial.println("\nWiFi connection failed");
        displayStatus("WiFi FAILED", "Will retry...");
    }
}

void setupMqtt() {
    mqttWifiClient.setInsecure();
    mqttClient.setServer(IOT_HUB_HOST, MQTT_PORT);
    mqttClient.setCallback(mqttCallback);
    mqttClient.setKeepAlive(240);
    mqttClient.setBufferSize(512);
}

void mqttCallback(char* topic, byte* payload, unsigned int length) {
    JsonDocument doc;
    DeserializationError error = deserializeJson(doc, payload, length);
    if (error) {
        Serial.printf("JSON parse error: %s\n", error.c_str());
        return;
    }

    const char* command = doc["command"] | "";

    if (strcmp(command, "doorUnlocked") == 0) {
        const char* cardUid = doc["cardUid"] | "";
        lastCardUid = String(cardUid);
        doorUnlockReceived = true;
        Serial.printf("Door unlock event, card: %s\n", cardUid);
    }
}

bool connectMqtt() {
    if (mqttClient.connected()) return true;
    if (WiFi.status() != WL_CONNECTED) return false;

    time_t now = time(nullptr);
    if (now < 1700000000) return false;

    Serial.println("Connecting to Azure IoT Hub...");

    if (sasToken.isEmpty() || now >= (time_t)(sasTokenExpiry - 300)) {
        sasTokenExpiry = (unsigned long)now + SAS_TOKEN_LIFETIME;
        String resourceUri = String(IOT_HUB_HOST) + "/devices/" + String(DEVICE_ID);
        sasToken = generateSasToken(resourceUri.c_str(), DEVICE_KEY, sasTokenExpiry);
    }

    String mqttUsername = String(IOT_HUB_HOST) + "/" + String(DEVICE_ID)
                        + "/?api-version=2021-04-12";

    bool connected = mqttClient.connect(
        DEVICE_ID,
        mqttUsername.c_str(),
        sasToken.c_str()
    );

    if (connected) {
        Serial.println("MQTT connected");
        displayStatus("IoT Hub OK", "Listening...");
        String c2dTopic = "devices/" + String(DEVICE_ID) + "/messages/devicebound/#";
        mqttClient.subscribe(c2dTopic.c_str(), 1);
    } else {
        Serial.printf("MQTT connect failed, rc=%d\n", mqttClient.state());
        displayStatus("MQTT Failed", "rc=" + String(mqttClient.state()));
        mqttWifiClient.stop();
        if (mqttClient.state() == 5) sasToken = "";
    }

    return connected;
}

void maintainMqtt() {
    time_t now = time(nullptr);
    if (mqttClient.connected() && now >= (time_t)(sasTokenExpiry - 300)) {
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
    mbedtls_base64_decode(decodedKey, sizeof(decodedKey), &decodedKeyLen,
                          (const unsigned char*)key, keyLen);

    String encodedUri = urlEncode(String(resourceUri));
    String stringToSign = encodedUri + "\n" + String(expiryEpoch);

    unsigned char hmacResult[32];
    mbedtls_md_context_t ctx;
    mbedtls_md_init(&ctx);
    mbedtls_md_setup(&ctx, mbedtls_md_info_from_type(MBEDTLS_MD_SHA256), 1);
    mbedtls_md_hmac_starts(&ctx, decodedKey, decodedKeyLen);
    mbedtls_md_hmac_update(&ctx, (const unsigned char*)stringToSign.c_str(), stringToSign.length());
    mbedtls_md_hmac_finish(&ctx, hmacResult);
    mbedtls_md_free(&ctx);

    unsigned char base64Sig[68];
    size_t base64SigLen = 0;
    mbedtls_base64_encode(base64Sig, sizeof(base64Sig), &base64SigLen, hmacResult, 32);
    base64Sig[base64SigLen] = '\0';
    String signature = String((char*)base64Sig);

    return "SharedAccessSignature sr=" + encodedUri
         + "&sig=" + urlEncode(signature)
         + "&se=" + String(expiryEpoch);
}
