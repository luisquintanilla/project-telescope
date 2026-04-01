# MCP Proxy Instrumentation

Telescope can transparently capture all MCP (Model Context Protocol) server traffic without modifying the MCP server or the agent using it.

## Automatic Setup

The easiest way to instrument MCP servers:

```bash
# Instruments all known MCP configs (Copilot, Claude Desktop, Cursor, VS Code)
tele setup

# Verify what was changed
tele status

# Restore original configs when done
tele setup --undo
```

`tele setup` wraps each MCP server command with `tele proxy stdio`, transparently forwarding all JSON-RPC traffic while capturing it for observability.

## Manual Proxy

For specific MCP servers or custom configurations:

```bash
# Wrap any stdio-based MCP server
tele proxy stdio -- npx @modelcontextprotocol/server-filesystem /path

# Wrap a Python MCP server
tele proxy stdio -- python my_mcp_server.py

# Wrap the Aspire MCP server
tele proxy stdio -- aspire agent mcp
```

The proxy:
- Sits between the MCP client (agent) and server
- Forwards all JSON-RPC messages bidirectionally
- Captures tool calls, resource reads, and prompts
- Zero impact on protocol behavior

## What Gets Captured

When an MCP server is proxied, Telescope captures:

| Data | Event Type | Confidence |
|------|-----------|------------|
| Tool calls (name + arguments) | `ToolCallStarted` | 0.95 |
| Tool results | `ToolCallCompleted` | 0.95 |
| Resource reads | Logged as tool activity | 0.95 |
| Server connection | `McpServerConnected` | 0.95 |

The 0.95 confidence reflects live intercept capture — the highest fidelity source in Telescope.

## Use Cases

### Debug MCP tool failures
```bash
# Start proxy
tele proxy stdio -- my-mcp-server

# Run your agent — it calls tools through the proxy
# Check what happened
tele sessions list
tele turns list <session-id>
# See exact tool_input and tool_result for each MCP call
```

### Audit MCP server behavior
```bash
# Instrument all servers
tele setup

# Use your agent normally for a while
# Then inspect
tele sessions list
# See which MCP tools were called, how often, with what arguments
```

### Compare MCP servers
```bash
# Proxy two different implementations of the same MCP server
# Compare their tool call results for the same inputs
# Identify behavioral differences
```

## Notes

- The proxy only works with stdio transport (the most common MCP transport)
- HTTP/SSE-based MCP servers are not currently proxied
- The proxy adds negligible latency (passthrough with capture)
- All captured data stays local on your machine
