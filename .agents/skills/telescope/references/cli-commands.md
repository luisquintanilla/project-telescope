# CLI Commands Reference

## Service Management

```bash
tele service start              # Start the Telescope service (background daemon)
tele status                     # Service overview: running state, connected collectors, recent sessions
```

## Querying Sessions

```bash
tele sessions list              # List all recent sessions
tele sessions list --json       # Machine-readable JSON output
tele sessions get <session-id>  # Full details for one session (metadata, turns, duration)
```

A session represents one conversation or task execution by an AI agent. Each session has:
- A unique session ID
- The agent that created it
- Start/end timestamps
- Working directory and git context (branch, repo)

## Querying Turns

```bash
tele turns list <session-id>        # List turns in a session
tele turns list <session-id> --json # Machine-readable output
```

A turn is one LLM request/response cycle within a session. Each turn includes:
- Turn index (sequential within the session)
- User message content
- Assistant response content
- Tool calls (started + completed with arguments and results)
- Token usage (input, output, total)
- Duration in milliseconds
- Status (completed, error, etc.)

## Querying Agents

```bash
tele agents list                # List all known agents
tele collectors list            # List loaded collectors with status
```

## MCP Proxy

```bash
tele setup                      # Auto-instrument MCP configs for Copilot, Claude, Cursor, VS Code
tele setup --undo               # Restore original MCP configs
tele proxy stdio -- <command>   # Manually proxy a single MCP server
```

## Output Formats

Most commands accept `--json` for machine-readable output. When using the telescope skill to debug, prefer `--json` so the output can be parsed programmatically.

## Tips

- Use `tele status` first to confirm the service is running
- Sessions are ordered by most recent first
- Turn indices start at 0 and increment with each LLM call
- Tool call events appear as nested entries within turns
- Token counts may be null if the model doesn't report them
