#!/usr/bin/env python3
"""Minimal MCP-over-HTTP client for the Unity "MCP for Unity" bridge.

Lets this agent drive a live Unity Editor without the Hermes MCP layer being loaded in
the current session (tools only register at session start).

  python3 unity_mcp.py schemas [out.json]        dump every tool schema
  python3 unity_mcp.py call <tool> '<json args>' call one tool
  python3 unity_mcp.py res <uri>                 read an MCP resource
  python3 unity_mcp.py state                     editor readiness shortcut

Env: UNITY_MCP_URL (default http://127.0.0.1:8080/mcp)
"""
import json
import os
import sys
import urllib.error
import urllib.request

URL = os.environ.get("UNITY_MCP_URL", "http://127.0.0.1:8080/mcp")
PLAIN = "--plain" in sys.argv


def _post(payload, session=None, timeout=180):
    data = json.dumps(payload).encode()
    headers = {"Content-Type": "application/json",
               "Accept": "application/json, text/event-stream"}
    if session:
        headers["Mcp-Session-Id"] = session
    req = urllib.request.Request(URL, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, dict(r.headers), r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, dict(e.headers), e.read().decode()


def _msgs(body):
    out = []
    for line in body.splitlines():
        if line.startswith("data: "):
            try:
                out.append(json.loads(line[6:]))
            except ValueError:
                pass
    if not out:
        try:
            out.append(json.loads(body))
        except ValueError:
            pass
    return out


def handshake():
    st, hdrs, body = _post({"jsonrpc": "2.0", "id": 1, "method": "initialize",
                            "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                                       "clientInfo": {"name": "hermes-agent", "version": "1"}}})
    sid = hdrs.get("Mcp-Session-Id") or hdrs.get("mcp-session-id")
    info = {}
    for m in _msgs(body):
        info = m.get("result", {}).get("serverInfo", info)
    if st != 200 or not sid:
        raise SystemExit("handshake failed: HTTP %s session=%s body=%s" % (st, sid, body[:300]))
    _post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    return sid, info


def unwrap(msg):
    """Return the useful payload of a tools/call or resources/read result."""
    res = msg.get("result", msg)
    if isinstance(res, dict) and "structuredContent" in res:
        return res["structuredContent"]
    content = res.get("content") if isinstance(res, dict) else None
    if content:
        texts = [c.get("text", "") for c in content if c.get("type") == "text"]
        joined = "\n".join(texts)
        try:
            return json.loads(joined)
        except ValueError:
            return joined
    return res


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return
    cmd = sys.argv[1]
    sid, info = handshake()

    if cmd == "schemas":
        st, _, body = _post({"jsonrpc": "2.0", "id": 2, "method": "tools/list"}, sid)
        tools = _msgs(body)[0]["result"]["tools"]
        out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), "unity_tools.json")
        with open(out, "w") as f:
            json.dump(tools, f, indent=1)
        print("server=%s tools=%d -> %s" % (info.get("version"), len(tools), out))

    elif cmd == "call":
        name, args = sys.argv[2], json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}
        st, _, body = _post({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                             "params": {"name": name, "arguments": args}}, sid)
        msgs = _msgs(body)
        if not msgs:
            print("HTTP %s: %s" % (st, body[:400]))
            return
        payload = unwrap(msgs[0])
        print(json.dumps(payload, indent=None if PLAIN else 1)[:6000])

    elif cmd == "res":
        st, _, body = _post({"jsonrpc": "2.0", "id": 4, "method": "resources/read",
                             "params": {"uri": sys.argv[2]}}, sid)
        msgs = _msgs(body)
        print(json.dumps(unwrap(msgs[0]), indent=1)[:4000] if msgs else "HTTP %s %s" % (st, body[:300]))

    elif cmd == "state":
        st, _, body = _post({"jsonrpc": "2.0", "id": 5, "method": "resources/read",
                             "params": {"uri": "mcpforunity://editor/state"}}, sid)
        msgs = _msgs(body)
        print(json.dumps(unwrap(msgs[0]), indent=1)[:2000] if msgs else "HTTP %s %s" % (st, body[:300]))

    else:
        print(__doc__)


if __name__ == "__main__":
    main()