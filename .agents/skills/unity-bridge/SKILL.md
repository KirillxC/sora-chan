---
name: unity-bridge
description: >-
  Allows the agent to control, inspect, modify, and build scenes inside Unity Editor
  via the local AgentBridgeServer HTTP/JSON-RPC API (port 8765). Use this skill to
  import VRM models, auto-rig humanoid ragdolls, inspect GameObject hierarchies,
  modify component properties, toggle Play Mode, read console logs, and take screenshots.
---

# Unity Bridge Skill

This skill provides direct programmatic control over a running Unity Editor instance.
The agent communicates with the editor via `unity_ctl.py` which speaks HTTP/JSON-RPC
to `AgentBridgeServer.cs` listening on `http://127.0.0.1:8765` (or LAN IP specified via `UNITY_BRIDGE_URL`).

## Common CLI Commands

All commands are executed via:
```bash
python3 /home/kiriewka/all/ai-chan/.agents/skills/unity-bridge/scripts/unity_ctl.py <command> [args]
```

### 1. Diagnostic & Compilation
* `status` - Check if Unity Editor is connected, compiling, or in Play Mode.
* `logs` - View recent Unity Console logs (Errors, Warnings, Debug.Log) with stack traces.
* `refresh` - Trigger `AssetDatabase.Refresh()` and wait for compilation to complete.

### 2. Scene & Hierarchy Manipulation
* `hierarchy` - Dump the active scene GameObject tree as JSON.
* `inspect <GameObjectPath>` - View all components and SerializedProperty values on a GameObject.
* `set-prop <GameObjectPath> <ComponentType> <PropertyName> <JsonValue>` - Edit Inspector values.
* `invoke <TypeName> <MethodName>` - Execute any static C# method inside the Unity Editor.

### 3. Play Mode & Physics Simulation
* `play` - Enter Play Mode.
* `stop` - Exit Play Mode.
* `step` - Advance physics simulation by 1 frame.
* `screenshot [CameraName] [OutputPath]` - Capture a frame from SceneView or Game camera to inspect visual state.

### 4. Synthetic Action Injection (Automated Tests)
* `sim-throw --speed "0,4.5,6"` - Inject a physical ball throw towards the avatar.
* `sim-touch --zone "HeadTop" --gesture "Stroke" --duration 2.5` - Simulate touching a collider.

---

## Native Unity MCP Server (via Unity CLI)

In addition to `AgentBridgeServer`, the project has the official `com.unity.pipeline` package installed (`ai-chan-project/Packages/manifest.json`).
The Unity CLI MCP server is registered in Antigravity (`/home/kiriewka/.gemini/config/mcp_config.json`):
```json
"unity": {
  "command": "unity",
  "args": ["mcp", "--project-path", "/home/kiriewka/all/ai-chan/ai-chan-project"]
}
```
When Unity Editor is open, it exposes live tools dynamically via stdio MCP. You can also run commands via terminal using:
```bash
unity command --project-path /home/kiriewka/all/ai-chan/ai-chan-project
unity command eval "return UnityEngine.Application.unityVersion;"
```
