using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using UnityEngine;
using AiChan.Core;
using AiChan.Limbic;
using AiChan.Thalamus;
using AiChan.Spinal;

namespace AiChan.Cortex
{
    /// <summary>
    /// Level 4: Cortex Intent Bridge.
    /// Manages non-blocking WebSocket connection between Unity and Python Brain Server (cortex_server.py).
    /// Emits high-level motor intentions to Level 3 (Limbic) and Level 1 (Spinal),
    /// and streams telemetry back to Python at 1-2 Hz without stalling the 90 FPS physics loop.
    /// Hardened against message fragmentation, disconnect race conditions, and ObjectDisposedException.
    /// </summary>
    public class CortexBridgeClient : MonoBehaviour
    {
        [Header("Connection")]
        public string serverUrl = "ws://127.0.0.1:8766/ws";
        public bool autoConnect = true;
        public float telemetryIntervalSec = 1.0f;

        [Header("References")]
        public ThalamusSensor thalamusSensor;
        public LimbicController limbicController;
        public ActiveRagdollController ragdollController;

        [Header("Status & Diagnostics")]
        [SerializeField] private bool _isConnected = false;
        [SerializeField] private string _lastReceivedAction = "none";
        [SerializeField] private string _lastThought = "";
        [SerializeField] private string _lastSpeech = "";
        [SerializeField] private string _lastNavigationIntent = "stop";
        [SerializeField] private MotorIntent _lastMotorIntent;
        [SerializeField] private int _receivedIntentsCount = 0;

        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;
        private Task _workerTask;
        private readonly ConcurrentQueue<string> _outboundQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<CortexOutboundResponse> _inboundQueue = new ConcurrentQueue<CortexOutboundResponse>();
        private float _lastTelemetryTime = -999f;

        public bool IsConnected => _isConnected;
        public string LastReceivedAction => _lastReceivedAction;
        public string LastThought => _lastThought;
        public string LastSpeech => _lastSpeech;
        public string LastNavigationIntent => _lastNavigationIntent;
        public MotorIntent LastMotorIntent => _lastMotorIntent;
        public int ReceivedIntentsCount => _receivedIntentsCount;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();

            if (autoConnect)
            {
                ConnectToCortex();
            }
        }

        public void EnsureInitialized()
        {
            if (thalamusSensor == null) thalamusSensor = GetComponent<ThalamusSensor>();
            if (limbicController == null) limbicController = GetComponent<LimbicController>();
            if (ragdollController == null) ragdollController = GetComponent<ActiveRagdollController>();
        }

        public void ConnectToCortex()
        {
            if (_isConnected || (_webSocket != null && _webSocket.State == WebSocketState.Open)) return;
            if (_workerTask != null && !_workerTask.IsCompleted && (_cts == null || !_cts.IsCancellationRequested)) return;

            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => WebSocketWorker(_cts.Token));
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            if (_webSocket != null)
            {
                try
                {
                    _webSocket.Abort();
                    _webSocket.Dispose();
                }
                catch { }
                _webSocket = null;
            }
            _isConnected = false;
        }

        private async Task WebSocketWorker(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using (_webSocket = new ClientWebSocket())
                    {
                        var uri = new Uri(serverUrl);
                        await _webSocket.ConnectAsync(uri, ct);
                        _isConnected = true;
                        Debug.Log($"[CortexBridge] Connected to Python Cortex at {serverUrl}");

                        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                        {
                            var receiveTask = ReceiveLoop(_webSocket, linkedCts.Token);
                            var sendTask = SendLoop(_webSocket, linkedCts.Token);

                            var completed = await Task.WhenAny(receiveTask, sendTask);
                            linkedCts.Cancel();
                            await Task.WhenAll(receiveTask.ContinueWith(_ => { }), sendTask.ContinueWith(_ => { }));
                            if (completed.IsFaulted && completed.Exception != null)
                            {
                                throw completed.Exception.InnerException ?? completed.Exception;
                            }
                        }
                    }
                    await Task.Delay(1000, ct).ContinueWith(_ => { });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    _isConnected = false;
                    await Task.Delay(2000, ct).ContinueWith(_ => { });
                }
                finally
                {
                    _isConnected = false;
                }
            }
        }

        private async Task ReceiveLoop(ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[8192];
            using (var ms = new MemoryStream())
            {
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                            return;
                        }
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage && !ct.IsCancellationRequested);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string json = Encoding.UTF8.GetString(ms.ToArray());
                        try
                        {
                            var response = JsonUtility.FromJson<CortexOutboundResponse>(json);
                            while (_inboundQueue.Count > 10)
                            {
                                _inboundQueue.TryDequeue(out _);
                            }
                            _inboundQueue.Enqueue(response);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[CortexBridge] Error deserializing inbound intent: {ex.Message}");
                        }
                    }
                }
            }
        }

        private async Task SendLoop(ClientWebSocket ws, CancellationToken ct)
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                while (_outboundQueue.TryDequeue(out var msg))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(msg);
                    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
                }
                await Task.Delay(50, ct);
            }
        }

        public void Step()
        {
            EnsureInitialized();

            // 1. Process received responses on main Unity thread
            while (_inboundQueue.TryDequeue(out var response))
            {
                ApplyCortexResponse(response);
            }

            // 2. Stream touch immediately or telemetry at decoupled 1-2 Hz rate
            bool hasPendingTouch = thalamusSensor != null && thalamusSensor.HasPendingTouch;
            float currentTime = Time.realtimeSinceStartup;
            if (hasPendingTouch || _lastTelemetryTime < 0f || (currentTime - _lastTelemetryTime >= telemetryIntervalSec))
            {
                _lastTelemetryTime = currentTime;
                EmitTelemetry();
            }
        }

        private void Update()
        {
            Step();
        }

        public void ApplyCortexResponse(CortexOutboundResponse resp)
        {
            _receivedIntentsCount++;
            _lastMotorIntent = resp.motor_intent;
            if (!string.IsNullOrEmpty(resp.motor_intent.action)) _lastReceivedAction = resp.motor_intent.action;
            if (resp.inner_thought != null) _lastThought = resp.inner_thought;
            if (resp.speech != null) _lastSpeech = resp.speech;
            if (!string.IsNullOrEmpty(resp.motor_intent.navigation_intent)) _lastNavigationIntent = resp.motor_intent.navigation_intent;

            // Apply emotional delta to Limbic
            if (limbicController != null)
            {
                limbicController.ApplyEmotionDelta(resp.emotion_delta);

                if (!string.IsNullOrEmpty(resp.motor_intent.facial_expression))
                {
                    limbicController.SetEmotion(resp.motor_intent.facial_expression, resp.motor_intent.blush);
                }
                else if (resp.motor_intent.blush > 0f)
                {
                    limbicController.SetBlush(resp.motor_intent.blush);
                }

                // If look_at action or target coordinates specified
                if (resp.motor_intent.action == "look_at" || resp.motor_intent.target_coordinates.sqrMagnitude > 0.001f)
                {
                    limbicController.SetLookAtTarget(resp.motor_intent.target_coordinates);
                }
            }

            Debug.Log($"[CortexBridge] Received MotorIntent: action='{resp.motor_intent.action}', nav='{resp.motor_intent.navigation_intent}', speech='{resp.speech}', thought='{resp.inner_thought}'");
        }

        public void EmitTelemetry()
        {
            _lastTelemetryTime = Time.realtimeSinceStartup;
            var packet = new CortexInboundPacket
            {
                event_type = "telemetry",
                player_distance = thalamusSensor != null ? thalamusSensor.PlayerDistance : 2.0f,
                gaze_status = thalamusSensor != null ? thalamusSensor.GazeStatus : "looking_away",
                affective_state = limbicController != null ? limbicController.CurrentState : default
            };

            // Check if there was any fresh touch
            if (thalamusSensor != null && thalamusSensor.TryPopRecentTouch(out var zone, out var gesture, out var intensity))
            {
                packet.event_type = "touch";
                packet.touch_zone = zone;
                packet.touch_gesture = gesture;
                packet.touch_intensity = intensity;
            }

            string json = JsonUtility.ToJson(packet);

            // Cap outbound queue to prevent unbounded buffer bloat during disconnects
            while (_outboundQueue.Count > 10)
            {
                _outboundQueue.TryDequeue(out _);
            }
            _outboundQueue.Enqueue(json);
        }

        public void SendPacketDirect(CortexInboundPacket packet)
        {
            string json = JsonUtility.ToJson(packet);
            while (_outboundQueue.Count > 10)
            {
                _outboundQueue.TryDequeue(out _);
            }
            _outboundQueue.Enqueue(json);
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }
    }
}
