#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AiChan.Editor
{
    [InitializeOnLoad]
    public static class AgentBridgeServer
    {
        private const int PORT = 8765;
        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static bool _isRunning = false;
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private static readonly List<LogEntry> _recentLogs = new List<LogEntry>();
        private const int MAX_LOGS = 100;

        [Serializable]
        public struct LogEntry
        {
            public string type;
            public string message;
            public string stackTrace;
            public string timestamp;
        }

        static AgentBridgeServer()
        {
            Application.logMessageReceived += OnLogMessageReceived;
            EditorApplication.update += OnEditorUpdate;
            StartServer();
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            lock (_recentLogs)
            {
                if (_recentLogs.Count >= MAX_LOGS)
                    _recentLogs.RemoveAt(0);

                _recentLogs.Add(new LogEntry
                {
                    type = type.ToString(),
                    message = condition,
                    stackTrace = stackTrace,
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
        }

        private static void OnEditorUpdate()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AgentBridge] Error executing queued action: {ex}");
                }
            }
        }

        public static void StartServer()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{PORT}/");
                _listener.Start();
                _isRunning = true;

                _listenerThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "AgentBridgeServerThread"
                };
                _listenerThread.Start();
                Debug.Log($"[AgentBridge] Server listening on port {PORT}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AgentBridge] Could not start on http://*:{PORT}/, falling back to localhost: {ex.Message}");
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{PORT}/");
                    _listener.Start();
                    _isRunning = true;
                    _listenerThread = new Thread(ListenLoop) { IsBackground = true };
                    _listenerThread.Start();
                    Debug.Log($"[AgentBridge] Server listening on http://127.0.0.1:{PORT}/");
                }
                catch (Exception e2)
                {
                    Debug.LogError($"[AgentBridge] Failed to start server: {e2}");
                }
            }
        }

        private static void ListenLoop()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem((state) => HandleRequest(context));
                }
                catch (HttpListenerException) { break; }
                catch (Exception ex)
                {
                    Debug.LogError($"[AgentBridge] Listener error: {ex}");
                }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = (int)HttpStatusCode.OK;
                res.Close();
                return;
            }

            string path = req.Url.AbsolutePath.ToLowerInvariant();
            string body = "";
            if (req.HasEntityBody)
            {
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                body = reader.ReadToEnd();
            }

            var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();

            _mainThreadQueue.Enqueue(() =>
            {
                try
                {
                    string jsonResponse = ProcessEndpoint(path, body);
                    tcs.SetResult(jsonResponse);
                }
                catch (Exception ex)
                {
                    tcs.SetResult(JsonUtility.ToJson(new ErrorResponse { error = ex.ToString() }));
                }
            });

            tcs.Task.Wait(5000);
            string responseText = tcs.Task.IsCompleted ? tcs.Task.Result : "{\"error\": \"Request timed out on main thread\"}";

            byte[] buffer = Encoding.UTF8.GetBytes(responseText);
            res.ContentType = "application/json";
            res.ContentLength64 = buffer.Length;
            res.OutputStream.Write(buffer, 0, buffer.Length);
            res.OutputStream.Close();
        }

        private static string ProcessEndpoint(string path, string body)
        {
            switch (path)
            {
                case "/api/status":
                    return JsonUtility.ToJson(new StatusResponse
                    {
                        isPlaying = EditorApplication.isPlaying,
                        isPaused = EditorApplication.isPaused,
                        isCompiling = EditorApplication.isCompiling,
                        activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                    });

                case "/api/logs":
                    lock (_recentLogs)
                    {
                        return "{\"logs\":" + JsonList(_recentLogs) + "}";
                    }

                case "/api/refresh":
                    AssetDatabase.Refresh();
                    return "{\"status\": \"refresh_triggered\"}";

                case "/api/playmode":
                    if (body.Contains("\"play\""))
                    {
                        EditorApplication.isPlaying = true;
                    }
                    else if (body.Contains("\"stop\""))
                    {
                        EditorApplication.isPlaying = false;
                    }
                    return "{\"status\": \"ok\", \"isPlaying\": " + (EditorApplication.isPlaying ? "true" : "false") + "}";

                case "/api/screenshot":
                    return CaptureScreenshot(body);

                case "/api/invoke":
                    return InvokeMethod(body);

                default:
                    return "{\"error\": \"unknown_endpoint\", \"path\": \"" + path + "\"}";
            }
        }

        private static string CaptureScreenshot(string body)
        {
            string filename = "screenshot.png";
            string path = Path.Combine(Application.dataPath, "..", filename);
            ScreenCapture.CaptureScreenshot(path);
            return "{\"status\": \"captured\", \"file\": \"" + path.Replace("\\", "/") + "\"}";
        }

        private static string InvokeMethod(string body)
        {
            try
            {
                var req = JsonUtility.FromJson<InvokeRequest>(body);
                Type type = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(req.type);
                    if (type != null) break;
                }

                if (type == null)
                    return "{\"error\": \"type_not_found\", \"type\": \"" + req.type + "\"}";

                var method = type.GetMethod(req.method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (method == null)
                    return "{\"error\": \"method_not_found\", \"method\": \"" + req.method + "\"}";

                object result = method.Invoke(null, null);
                return "{\"status\": \"invoked\", \"result\": \"" + (result != null ? result.ToString() : "null") + "\"}";
            }
            catch (Exception ex)
            {
                return "{\"error\": \"" + ex.Message.Replace("\"", "'") + "\"}";
            }
        }

        private static string JsonList(List<LogEntry> logs)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < logs.Count; i++)
            {
                sb.Append(JsonUtility.ToJson(logs[i]));
                if (i < logs.Count - 1) sb.Append(",");
            }
            sb.Append("]");
            return sb.ToString();
        }

        [Serializable]
        private struct StatusResponse
        {
            public bool isPlaying;
            public bool isPaused;
            public bool isCompiling;
            public string activeScene;
        }

        [Serializable]
        private struct ErrorResponse { public string error; }

        [Serializable]
        private struct InvokeRequest { public string type; public string method; }
    }
}
#endif
