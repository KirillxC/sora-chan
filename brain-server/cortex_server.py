import asyncio
import json
import time
import base64
from typing import Optional, Dict, Any, List
from fastapi import FastAPI, WebSocket, WebSocketDisconnect, Body
from pydantic import BaseModel
from memory_manager import MemoryManager

app = FastAPI(title="AI-Chan Cortex Server")
memory = MemoryManager()

active_connections: List[WebSocket] = []
last_telemetry: Dict[str, Any] = {
    "player_distance": 2.0,
    "gaze_status": "looking_away",
    "touch_triggers": [],
    "last_update": 0
}

class SensorPacket(BaseModel):
    event_type: str  # "telemetry", "touch", "speech", "vision_tick", "boredom_tick"
    touch_zone: Optional[str] = None
    touch_gesture: Optional[str] = None
    touch_intensity: Optional[float] = 0.0
    user_speech: Optional[str] = None
    eye_frame_jpeg_b64: Optional[str] = None
    player_distance: Optional[float] = 2.0
    gaze_status: Optional[str] = "looking_away"
    affective_state: Optional[Dict[str, float]] = None

class MotorIntentPayload(BaseModel):
    action: str = "idle"
    targetId: Optional[str] = "none"
    reflexPreset: Optional[str] = "playful"
    effort: Optional[float] = 1.0
    target_coordinates: Optional[Dict[str, float]] = {"x": 0.0, "y": 1.45, "z": -2.2}
    navigation_intent: Optional[str] = "stop"
    facial_expression: Optional[str] = "neutral"
    blush: Optional[float] = 0.0

class MotorIntentResponse(BaseModel):
    speech: Optional[str] = None
    inner_thought: str = ""
    motor_intent: Dict[str, Any]
    emotion_delta: Dict[str, float] = {"valence": 0.0, "arousal": 0.0, "blush": 0.0}

class InnerMonologueEngine:
    def __init__(self):
        self.last_thought: str = "Стою в комнате, осматриваюсь..."
        self.last_thought_time = time.time()

    def generate_thought(self, context_snippet: str, current_mood: Dict[str, float]) -> str:
        arousal = current_mood.get("arousal", 0.3)
        blush = current_mood.get("blush", 0.0)
        valence = current_mood.get("valence", 0.5)

        if blush > 0.5:
            thought = "У меня пылают щеки... Зачем он так близко подошел? Надо попытаться казаться невозмутимой."
        elif arousal > 0.7:
            thought = "Ого, как быстро всё происходит! Нужно поймать этот мяч во что бы то ни стало!"
        elif valence < 0.2:
            thought = "Ну вот, опять он надо мной подшучивает... Не буду показывать, что мне обидно."
        else:
            thought = "В комнате тихо. Он смотрит в мою сторону. Интересно, о чем он думает?"

        self.last_thought = thought
        self.last_thought_time = time.time()
        return thought

monologue = InnerMonologueEngine()

@app.websocket("/ws")
async def websocket_endpoint(websocket: WebSocket):
    await websocket.accept()
    active_connections.append(websocket)
    print("[Cortex] Unity connected via WebSocket")

    try:
        while True:
            raw_msg = await websocket.receive_text()
            try:
                data = json.loads(raw_msg)
            except Exception as e:
                print(f"[Cortex] Malformed JSON received: {e}")
                continue
            event_type = data.get("event_type", "unknown")

            current_mood = data.get("affective_state", {"valence": 0.5, "arousal": 0.3, "blush": 0.0})
            player_dist = data.get("player_distance", 2.0)
            gaze_stat = data.get("gaze_status", "looking_away")

            # Update telemetry store
            last_telemetry["player_distance"] = player_dist
            last_telemetry["gaze_status"] = gaze_stat
            last_telemetry["affective_state"] = current_mood
            last_telemetry["last_update"] = time.time()

            # 1. Update Inner Monologue
            thought = monologue.generate_thought(str(data), current_mood)
            memory.add_event("ai_thought", thought)

            # 2. Decision logic
            response = MotorIntentResponse(
                speech=None,
                inner_thought=thought,
                motor_intent={
                    "action": "idle",
                    "targetId": "none",
                    "target_coordinates": {"x": 0.0, "y": 1.45, "z": -2.2},
                    "navigation_intent": "stop",
                    "facial_expression": "neutral",
                    "blush": 0.0
                },
                emotion_delta={"valence": 0.0, "arousal": 0.0, "blush": 0.0}
            )

            if event_type == "telemetry":
                if gaze_stat == "direct_eye_contact" and player_dist < 1.2:
                    response.speech = "Ты так близко подошел... и смотришь прямо в глаза."
                    response.motor_intent = {
                        "action": "look_at",
                        "targetId": "user_head",
                        "target_coordinates": {"x": 0.0, "y": 1.45, "z": -2.2},
                        "navigation_intent": "approach_player",
                        "facial_expression": "joy",
                        "blush": 0.6
                    }
                    response.emotion_delta = {"blush": 0.3, "valence": 0.2, "arousal": 0.2}
                else:
                    response.motor_intent = {
                        "action": "look_at",
                        "targetId": "user_head",
                        "target_coordinates": {"x": 0.0, "y": 1.45, "z": -2.2},
                        "navigation_intent": "approach_player",
                        "facial_expression": "neutral",
                        "blush": 0.1
                    }

            elif event_type == "touch":
                zone = data.get("touch_zone", "HeadTop")
                gesture = data.get("touch_gesture", "Stroke")
                memory.add_event("touch_sense", f"Игрок прикоснулся к {zone} жестом {gesture}")
                last_telemetry["touch_triggers"].append({"zone": zone, "gesture": gesture, "time": time.time()})
                if len(last_telemetry["touch_triggers"]) > 50:
                    last_telemetry["touch_triggers"] = last_telemetry["touch_triggers"][-50:]

                if gesture == "Stroke" and zone in ["HeadTop", "Hair"]:
                    response.speech = "М-м... щекотно вообще-то... но приятно."
                    response.motor_intent = {
                        "action": "lean_into_hand",
                        "targetId": "user_hand",
                        "target_coordinates": {"x": 0.0, "y": 1.45, "z": -2.2},
                        "navigation_intent": "stop",
                        "facial_expression": "joy",
                        "blush": 0.4
                    }
                    response.emotion_delta = {"blush": 0.3, "valence": 0.3}
                elif gesture in ["Poke", "Slap"]:
                    response.speech = "Ай! Ты чего дерешься?!"
                    response.motor_intent = {
                        "action": "step_back",
                        "targetId": "user",
                        "target_coordinates": {"x": 0.0, "y": 1.45, "z": -2.2},
                        "navigation_intent": "retreat",
                        "facial_expression": "angry",
                        "blush": 0.1
                    }
                    response.emotion_delta = {"valence": -0.3, "arousal": 0.4}

            elif event_type == "user_speech":
                speech_text = data.get("user_speech", "")
                memory.add_event("user", speech_text)
                response.speech = f"Ты сказал '{speech_text}'? Я тебя отлично слышу!"
                response.motor_intent = {
                    "action": "look_at",
                    "targetId": "user_head",
                    "target_coordinates": {"x": 0.0, "y": 1.45, "z": -2.2},
                    "navigation_intent": "approach_player",
                    "facial_expression": "joy",
                    "blush": 0.2
                }

            await websocket.send_text(response.model_dump_json())

    except WebSocketDisconnect:
        print("[Cortex] Unity disconnected")
    except Exception as e:
        print(f"[Cortex] WebSocket error: {e}")
    finally:
        if websocket in active_connections:
            active_connections.remove(websocket)

@app.post("/api/intent")
async def send_manual_intent(intent: Dict[str, Any] = Body(...)):
    """Send arbitrary high-level intent to connected Unity clients."""
    if not active_connections:
        return {"status": "error", "message": "No Unity client connected"}

    raw_motor = intent.get("motor_intent", {})
    if isinstance(raw_motor, dict) and raw_motor:
        try:
            validated_motor = MotorIntentPayload(**raw_motor).model_dump()
        except Exception:
            validated_motor = MotorIntentPayload().model_dump()
    else:
        validated_motor = MotorIntentPayload(
            action="look_at",
            targetId="user_head",
            target_coordinates={"x": 0.0, "y": 1.45, "z": -2.2},
            navigation_intent="approach_player",
            facial_expression="joy",
            blush=0.5
        ).model_dump()

    payload = {
        "speech": intent.get("speech", None),
        "inner_thought": intent.get("inner_thought", monologue.last_thought),
        "motor_intent": validated_motor,
        "emotion_delta": intent.get("emotion_delta", {"valence": 0.1, "arousal": 0.1, "blush": 0.2})
    }

    msg = json.dumps(payload)
    for ws in list(active_connections):
        try:
            await ws.send_text(msg)
        except Exception:
            if ws in active_connections:
                active_connections.remove(ws)

    return {"status": "sent", "clients_count": len(active_connections), "intent": payload}

@app.get("/api/telemetry")
def get_telemetry():
    return {
        "status": "ok",
        "connected_clients": len(active_connections),
        "telemetry": last_telemetry
    }

@app.get("/health")
def health():
    return {
        "status": "ok",
        "last_thought": monologue.last_thought,
        "connected_clients": len(active_connections)
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8766)
