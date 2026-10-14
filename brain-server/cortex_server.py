import asyncio
import json
import time
import base64
from typing import Optional, Dict, Any
from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from pydantic import BaseModel
from memory_manager import MemoryManager

app = FastAPI(title="AI-Chan Cortex Server")
memory = MemoryManager()

class SensorPacket(BaseModel):
    event_type: str  # "touch", "speech", "vision_tick", "boredom_tick"
    touch_zone: Optional[str] = None
    touch_gesture: Optional[str] = None
    touch_intensity: Optional[float] = 0.0
    user_speech: Optional[str] = None
    eye_frame_jpeg_b64: Optional[str] = None
    affective_state: Optional[Dict[str, float]] = None

class MotorIntentResponse(BaseModel):
    speech: Optional[str] = None
    inner_thought: str
    motor_intent: Dict[str, Any]
    emotion_delta: Dict[str, float]

class InnerMonologueEngine:
    def __init__(self):
        self.last_thought: str = "Стою в комнате, осматриваюсь..."
        self.last_thought_time = time.time()

    def generate_thought(self, context_snippet: str, current_mood: Dict[str, float]) -> str:
        # Heuristic / Local model thought stream generator
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
            thought = f"В комнате тихо. Он смотрит в мою сторону. Интересно, о чем он думает?"

        self.last_thought = thought
        self.last_thought_time = time.time()
        return thought

monologue = InnerMonologueEngine()

@app.websocket("/ws")
async def websocket_endpoint(websocket: WebSocket):
    await websocket.accept()
    print("[Cortex] Unity connected via WebSocket")

    try:
        while True:
            raw_msg = await websocket.receive_text()
            data = json.loads(raw_msg)
            event_type = data.get("event_type", "unknown")

            # Extract sensory payload
            current_mood = data.get("affective_state", {"valence": 0.5, "arousal": 0.3, "blush": 0.0})
            eye_b64 = data.get("eye_frame_jpeg_b64")

            # 1. Update Inner Monologue
            thought = monologue.generate_thought(str(data), current_mood)
            memory.add_event("ai_thought", thought)

            # 2. Decision logic: Respond or silent reaction
            response = MotorIntentResponse(
                speech=None,
                inner_thought=thought,
                motor_intent={"action": "idle", "target": "none"},
                emotion_delta={"valence": 0.0, "arousal": 0.0, "blush": 0.0}
            )

            if event_type == "touch":
                zone = data.get("touch_zone", "HeadTop")
                gesture = data.get("touch_gesture", "Stroke")
                memory.add_event("touch_sense", f"Игрок прикоснулся к {zone} жестом {gesture}")

                if gesture == "Stroke" and zone in ["HeadTop", "Hair"]:
                    response.speech = "М-м... щекотно вообще-то... но приятно."
                    response.motor_intent = {"action": "lean_into_hand", "target": "user_hand", "effort": 0.5}
                    response.emotion_delta = {"blush": 0.3, "valence": 0.2}
                elif gesture in ["Poke", "Slap"]:
                    response.speech = "Ай! Ты чего дерешься?!"
                    response.motor_intent = {"action": "step_back", "target": "user", "effort": 0.8}
                    response.emotion_delta = {"valence": -0.3, "arousal": 0.4}

            elif event_type == "ball_incoming":
                # Ballistic reflex triggers locally at 90Hz, but cortex reacts with voice
                response.speech = "О, пас! Ловлю!"
                response.motor_intent = {"action": "catch", "target": "ball", "effort": 0.9}
                response.emotion_delta = {"arousal": 0.3}

            elif event_type == "user_speech":
                speech_text = data.get("user_speech", "")
                memory.add_event("user", speech_text)
                # LLM response (placeholder for live cloud API call)
                response.speech = f"Ты сказал '{speech_text}'? Я тебя отлично слышу!"
                response.motor_intent = {"action": "look_at_user_face", "target": "user_head"}

            await websocket.send_text(response.model_dump_json())

    except WebSocketDisconnect:
        print("[Cortex] Unity disconnected")
    except Exception as e:
        print(f"[Cortex] WebSocket error: {e}")

@app.get("/health")
def health():
    return {"status": "ok", "last_thought": monologue.last_thought}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8766)
