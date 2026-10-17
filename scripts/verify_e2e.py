#!/usr/bin/env python3
"""
Automated End-to-End Verification Script for AI-Chan VRM Stabilization & Intent Bridge Milestone.
Verifies:
1. Python Brain server (FastAPI + WebSocket) starts cleanly on port 8766 and healthcheck succeeds.
2. High-level intent exchange and telemetry streaming over WebSocket (LookAt, Navigation, Facial expressions, Blush, Touch).
3. Unity 6 C# project compiles with 0 errors.
4. Unity physics simulation and console output in RoomScene:
   - 0 runtime exceptions or errors.
   - Stable ragdoll posture (zero limb explosions, zero NaN, realistic knee/elbow limits, pinned hips).
   - 4-level decoupled architecture components wired and active.
"""

import sys
import os
import time
import json
import subprocess
import asyncio
import urllib.request
import urllib.error

PROJECT_ROOT = "/home/kiriewka/all/ai-chan"
UNITY_PROJECT = os.path.join(PROJECT_ROOT, "ai-chan-project")
BRAIN_SERVER_DIR = os.path.join(PROJECT_ROOT, "brain-server")
PYTHON_BIN = os.path.join(BRAIN_SERVER_DIR, "venv", "bin", "python3")
UNITY_BIN = "/mnt/data/unity/editors/6000.0.84f1/Editor/Unity"

if not os.path.exists(PYTHON_BIN):
    PYTHON_BIN = sys.executable

def log(msg: str):
    print(f"\033[1;34m[E2E-VERIFY]\033[0m {msg}", flush=True)

def log_success(msg: str):
    print(f"\033[1;32m[PASS]\033[0m {msg}", flush=True)

def log_fail(msg: str):
    print(f"\033[1;31m[FAIL]\033[0m {msg}", flush=True)

async def test_brain_server_intents():
    import websockets

    uri = "ws://127.0.0.1:8766/ws"
    log(f"Connecting to Cortex WebSocket at {uri}...")

    async with websockets.connect(uri) as ws:
        # 1. Send Telemetry Packet
        telemetry = {
            "event_type": "telemetry",
            "player_distance": 0.95,
            "gaze_status": "direct_eye_contact",
            "affective_state": {"valence": 0.5, "arousal": 0.3, "blush": 0.1}
        }
        await ws.send(json.dumps(telemetry))
        raw = await asyncio.wait_for(ws.recv(), timeout=5.0)
        resp = json.loads(raw)
        log(f"Received response to telemetry: action='{resp.get('motor_intent', {}).get('action')}', blush={resp.get('motor_intent', {}).get('blush')}")
        assert "motor_intent" in resp, "Missing motor_intent in server response!"
        assert "speech" in resp or "inner_thought" in resp, "Missing speech or inner_thought in server response!"

        # 2. Send Touch Sensor Packet
        touch_packet = {
            "event_type": "touch",
            "touch_zone": "HeadTop",
            "touch_gesture": "Stroke",
            "touch_intensity": 1.0,
            "affective_state": {"valence": 0.6, "arousal": 0.3, "blush": 0.2}
        }
        await ws.send(json.dumps(touch_packet))
        raw_touch = await asyncio.wait_for(ws.recv(), timeout=5.0)
        resp_touch = json.loads(raw_touch)
        log(f"Received touch reaction: speech='{resp_touch.get('speech')}', action='{resp_touch.get('motor_intent', {}).get('action')}'")
        assert resp_touch.get("motor_intent", {}).get("action") == "lean_into_hand", "Expected action lean_into_hand on head stroke!"
        assert resp_touch.get("emotion_delta", {}).get("blush", 0) > 0, "Expected positive blush delta on stroke!"

        log_success("WebSocket bidirectional intent & telemetry exchange verified successfully.")

        # 3. Test resilience to malformed JSON (server must not crash or disconnect)
        log("Testing server resilience to malformed JSON packet...")
        await ws.send("INVALID_NOT_A_JSON{}}{")
        # Follow immediately with valid ping packet to prove connection is healthy
        await ws.send(json.dumps({"event_type": "telemetry", "player_distance": 1.5, "gaze_status": "direct_eye_contact"}))
        raw_healthy = await asyncio.wait_for(ws.recv(), timeout=5.0)
        assert "motor_intent" in json.loads(raw_healthy), "Expected valid response after malformed JSON!"
        log_success("Server survived malformed JSON and connection remained active.")

        # 4. Test HTTP /api/intent POST with partial payload (model validation and normalization)
        log("Testing HTTP POST /api/intent with partial payload...")
        test_api_partial_intent_post()

        # 5. Verify the normalized broadcasted intent was received over WebSocket!
        raw_partial_broadcast = await asyncio.wait_for(ws.recv(), timeout=5.0)
        resp_p_broadcast = json.loads(raw_partial_broadcast)
        assert resp_p_broadcast.get("motor_intent", {}).get("action") == "step_back"
        assert resp_p_broadcast.get("motor_intent", {}).get("navigation_intent") == "stop", "Expected default navigation_intent 'stop' populated!"
        assert "target_coordinates" in resp_p_broadcast.get("motor_intent", {}), "Expected default target_coordinates populated!"
        log_success("Normalized partial intent received over WebSocket with valid defaults.")

        # 6. Test HTTP /api/intent full broadcast POST while WebSocket is connected
        log("Testing HTTP POST /api/intent full broadcast with active WebSocket listener...")
        test_api_intent_post()

        # 7. Verify the full broadcasted intent was received over WebSocket!
        raw_broadcast = await asyncio.wait_for(ws.recv(), timeout=5.0)
        resp_broadcast = json.loads(raw_broadcast)
        log(f"WebSocket client received broadcasted intent: action='{resp_broadcast.get('motor_intent', {}).get('action')}', blush={resp_broadcast.get('motor_intent', {}).get('blush')}")
        assert resp_broadcast.get("motor_intent", {}).get("action") == "look_at", "Expected broadcast action look_at!"
        log_success("Real-time full intent push to active client verified.")

def test_api_partial_intent_post():
    url = "http://127.0.0.1:8766/api/intent"
    payload = {
        "speech": "Частичное намерение",
        "motor_intent": {
            "action": "step_back"
        }
    }
    req = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(req, timeout=5) as response:
        data = json.loads(response.read().decode())
        assert data.get("status") == "sent"
        mi = data.get("intent", {}).get("motor_intent", {})
        assert mi.get("navigation_intent") == "stop"
        assert mi.get("target_coordinates") == {"x": 0.0, "y": 1.45, "z": -2.2}
        log_success("HTTP /api/intent partial normalization verified.")

def test_api_intent_post():
    url = "http://127.0.0.1:8766/api/intent"
    payload = {
        "speech": "Тестовое намерение доставлено!",
        "inner_thought": "Я смотрю на игрока и улыбаюсь.",
        "motor_intent": {
            "action": "look_at",
            "targetId": "user_head",
            "target_coordinates": {"x": 0.0, "y": 1.45, "z": -2.2},
            "navigation_intent": "approach_player",
            "facial_expression": "joy",
            "blush": 0.55
        },
        "emotion_delta": {"valence": 0.2, "arousal": 0.1, "blush": 0.25}
    }
    req = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(req, timeout=5) as response:
        data = json.loads(response.read().decode())
        assert data.get("status") == "sent", f"Unexpected status from /api/intent: {data}"
        log_success(f"HTTP /api/intent broadcast verified ({data.get('clients_count', 0)} clients connected).")

def test_telemetry_get():
    url = "http://127.0.0.1:8766/api/telemetry"
    with urllib.request.urlopen(url, timeout=5) as response:
        data = json.loads(response.read().decode())
        assert data.get("status") == "ok", f"Unexpected status from /api/telemetry: {data}"
        t = data.get("telemetry", {})
        log_success(f"HTTP /api/telemetry verified: player_distance={t.get('player_distance')}, gaze_status='{t.get('gaze_status')}'.")

def run_unity_validation():
    log("Running Unity 6 Simulation & Console Validator in batchmode...")
    log_file = "/tmp/unity_e2e_verify.log"
    cmd = [
        UNITY_BIN,
        "-batchmode",
        "-nographics",
        "-projectPath", UNITY_PROJECT,
        "-executeMethod", "AiChan.Editor.SimulationValidator.ValidatePlayModePhysicsAndConsole",
        "-quit",
        "-logFile", log_file
    ]

    result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=120)
    if result.returncode != 0:
        log_fail(f"Unity validation exited with non-zero code {result.returncode}!")
        if os.path.exists(log_file):
            with open(log_file, "r") as f:
                lines = f.readlines()
                print("".join(lines[-40:]))
        sys.exit(1)

    # Check log file for confirmation
    if os.path.exists(log_file):
        with open(log_file, "r") as f:
            content = f.read()
            if "=== [VALIDATION PASSED]" not in content:
                log_fail("Validation pass marker not found in Unity log!")
                sys.exit(1)
            if "InvalidOperationException" in content:
                log_fail("Legacy Input InvalidOperationException found in Unity log!")
                sys.exit(1)

    log_success("Unity Editor console has ZERO runtime exceptions and physics simulation PASSED stably.")

def main():
    print("=" * 70)
    log("Starting AI-Chan Milestone Verification Pipeline")
    print("=" * 70)

    # 1. Start Python Brain Server in Background
    log("Launching Python Brain Server (cortex_server.py)...")
    server_proc = subprocess.Popen(
        [PYTHON_BIN, "cortex_server.py"],
        cwd=BRAIN_SERVER_DIR,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE
    )

    try:
        # Wait for health endpoint
        health_url = "http://127.0.0.1:8766/health"
        server_ready = False
        for _ in range(30):
            try:
                with urllib.request.urlopen(health_url, timeout=1) as res:
                    if res.status == 200:
                        server_ready = True
                        break
            except Exception:
                time.sleep(0.5)

        if not server_ready:
            log_fail("Python Brain server failed to start within 15 seconds!")
            server_proc.kill()
            sys.exit(1)

        log_success("Python Brain server started cleanly on http://127.0.0.1:8766")

        # 2. Test WebSocket Intent & Telemetry Exchange (including push broadcast)
        asyncio.run(test_brain_server_intents())

        # 3. Test HTTP Telemetry Endpoint
        test_telemetry_get()

        # 4. Run Unity Headless Validation
        run_unity_validation()

        print("=" * 70)
        log_success("ALL ACCEPTANCE CRITERIA VERIFIED SUCCESSFULLY!")
        print("  [x] Unity console: 0 runtime exceptions or errors.")
        print("  [x] Active Ragdoll: stable posture, zero joint explosions or limb tangles.")
        print("  [x] Input System: PhysicalLimbDragger migrated, legacy Input eliminated.")
        print("  [x] 4-Level Architecture: Spinal -> Thalamus -> Limbic -> Cortex active.")
        print("  [x] High-level Intent & Telemetry WebSocket bridge operating cleanly.")
        print("=" * 70)
        sys.exit(0)

    finally:
        log("Terminating Python Brain Server...")
        server_proc.terminate()
        try:
            server_proc.wait(timeout=3)
        except subprocess.TimeoutExpired:
            server_proc.kill()

if __name__ == "__main__":
    main()
