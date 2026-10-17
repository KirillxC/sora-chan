using System;
using UnityEngine;

namespace AiChan.Core
{
    public enum BodyZone
    {
        HeadTop,
        CheekLeft,
        CheekRight,
        Nose,
        Lips,
        ShoulderLeft,
        ShoulderRight,
        Chest,
        Back,
        Waist,
        UpperArmLeft,
        UpperArmRight,
        HandLeft,
        HandRight,
        LegLeft,
        LegRight
    }

    public enum TouchGesture
    {
        None,
        Stroke,     // Поглаживание
        Pat,        // Похлопывание
        Poke,       // Тычок
        Slap,       // Шлепок
        GrabHold,   // Захват/удержание за руку
        Pull,       // Тянет за собой
        Pinch       // Щипок
    }

    [Serializable]
    public struct SomatosensoryEvent
    {
        public BodyZone zone;
        public TouchGesture gesture;
        public float intensity;
        public float durationSec;
        public string contactSide;
        public long timestampMs;
    }

    [Serializable]
    public struct AffectiveState
    {
        public float valence;       // -1.0 .. 1.0 (настроение)
        public float arousal;       //  0.0 .. 1.0 (энергия/пульс)
        public float dominance;     // -1.0 .. 1.0 (уверенность/стеснительность)
        public float blush;         //  0.0 .. 1.0 (румянец)
        public float trust;         //  0.0 .. 1.0 (доверие)
        public float heartRateBpm;  //  60 .. 140 (пульс для хаптики в VR)
    }

    [Serializable]
    public struct MotorIntent
    {
        public string action;               // "idle", "look_at", "catch", "throw", "walk_to", "hold_user_hand", "poke_user", "step_back"
        public string targetId;             // "ball", "user_head", "user_hand_r", "hoop"
        public string reflexPreset;         // "playful", "annoyed", "shy", "competitive"
        public float effort;                // 0.0 .. 1.0
        public Vector3 target_coordinates;  // Look-at or navigation target point
        public string navigation_intent;    // "approach_player", "retreat", "circle", "stop"
        public string facial_expression;    // "joy", "angry", "sorrow", "fun", "surprised", "neutral", "pout"
        public float blush;                 // 0.0 .. 1.0
    }

    [Serializable]
    public struct CortexInboundPacket
    {
        public string event_type;           // "telemetry", "touch", "speech", "vision_tick", "boredom_tick"
        public string touch_zone;
        public string touch_gesture;
        public float touch_intensity;
        public string user_speech;
        public string eye_frame_jpeg_b64;
        public float player_distance;       // Distance to user / camera
        public string gaze_status;          // "direct_eye_contact", "looking_at_body", "looking_away"
        public AffectiveState affective_state;
    }

    [Serializable]
    public struct CortexOutboundResponse
    {
        public string speech;
        public string inner_thought;
        public MotorIntent motor_intent;
        public AffectiveState emotion_delta;
    }
}
