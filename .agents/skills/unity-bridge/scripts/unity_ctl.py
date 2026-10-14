#!/usr/bin/env python3
import sys
import os
import json
import argparse
import urllib.request
import urllib.error

DEFAULT_URL = os.environ.get("UNITY_BRIDGE_URL", "http://127.0.0.1:8765")

def make_request(endpoint: str, payload: dict = None, timeout: float = 10.0):
    url = f"{DEFAULT_URL.rstrip('/')}/{endpoint.lstrip('/')}"
    headers = {"Content-Type": "application/json"}
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    req = urllib.request.Request(url, data=data, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            res_data = response.read().decode("utf-8")
            try:
                return json.loads(res_data)
            except json.JSONDecodeError:
                return {"raw": res_data}
    except urllib.error.URLError as e:
        print(f"[ERROR] Failed to connect to Unity Bridge at {url}: {e}", file=sys.stderr)
        return {"error": str(e), "connected": False}

def cmd_status(args):
    res = make_request("/api/status")
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_logs(args):
    res = make_request("/api/logs")
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_refresh(args):
    res = make_request("/api/refresh", {})
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_hierarchy(args):
    res = make_request("/api/hierarchy")
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_inspect(args):
    res = make_request("/api/inspect", {"path": args.path})
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_invoke(args):
    res = make_request("/api/invoke", {"type": args.type, "method": args.method, "args": args.args or []})
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_play(args):
    res = make_request("/api/playmode", {"action": "play"})
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_stop(args):
    res = make_request("/api/playmode", {"action": "stop"})
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_screenshot(args):
    res = make_request("/api/screenshot", {"camera": args.camera or "MainCamera", "path": args.output or "screenshot.png"})
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_sim_touch(args):
    payload = {
        "action": "touch",
        "zone": args.zone,
        "gesture": args.gesture,
        "duration": args.duration,
        "intensity": args.intensity
    }
    res = make_request("/api/simulate", payload)
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_sim_throw(args):
    coords = [float(x.strip()) for x in args.speed.split(",")]
    payload = {
        "action": "throw_ball",
        "velocity": coords
    }
    res = make_request("/api/simulate", payload)
    print(json.dumps(res, indent=2, ensure_ascii=False))

def main():
    parser = argparse.ArgumentParser(description="Unity Bridge CLI Controller")
    subparsers = parser.add_subparsers(dest="command", required=True)

    subparsers.add_parser("status", help="Get Unity Editor status")
    subparsers.add_parser("logs", help="Get Unity Console logs")
    subparsers.add_parser("refresh", help="Trigger AssetDatabase refresh")
    subparsers.add_parser("hierarchy", help="Get Scene hierarchy")

    p_inspect = subparsers.add_parser("inspect", help="Inspect GameObject components")
    p_inspect.add_argument("path", help="Hierarchy path to GameObject")

    p_invoke = subparsers.add_parser("invoke", help="Invoke static C# method")
    p_invoke.add_argument("type", help="Full C# Type name")
    p_invoke.add_argument("method", help="Method name")
    p_invoke.add_argument("args", nargs="*", help="Method arguments")

    subparsers.add_parser("play", help="Enter Play Mode")
    subparsers.add_parser("stop", help="Exit Play Mode")

    p_shot = subparsers.add_parser("screenshot", help="Capture Camera screenshot")
    p_shot.add_argument("--camera", default="MainCamera")
    p_shot.add_argument("--output", default="screenshot.png")

    p_touch = subparsers.add_parser("sim-touch", help="Simulate touching avatar")
    p_touch.add_argument("--zone", default="HeadTop")
    p_touch.add_argument("--gesture", default="Stroke")
    p_touch.add_argument("--duration", type=float, default=2.0)
    p_touch.add_argument("--intensity", type=float, default=0.5)

    p_throw = subparsers.add_parser("sim-throw", help="Simulate throwing ball")
    p_throw.add_argument("--speed", default="0,4.5,6")

    args = parser.parse_args()
    cmd_map = {
        "status": cmd_status,
        "logs": cmd_logs,
        "refresh": cmd_refresh,
        "hierarchy": cmd_hierarchy,
        "inspect": cmd_inspect,
        "invoke": cmd_invoke,
        "play": cmd_play,
        "stop": cmd_stop,
        "screenshot": cmd_screenshot,
        "sim-touch": cmd_sim_touch,
        "sim-throw": cmd_sim_throw
    }
    cmd_map[args.command](args)

if __name__ == "__main__":
    main()
