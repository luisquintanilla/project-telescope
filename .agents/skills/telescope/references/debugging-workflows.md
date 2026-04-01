# Debugging Workflows

Systematic patterns for using Telescope traces to diagnose AI agent issues.

## Workflow 1: Agent Returns Wrong Results

**Symptom:** The agent gives an incorrect answer despite having the right tools.

**Investigation:**

```bash
# 1. Find the session
tele sessions list

# 2. Inspect turns
tele turns list <session-id>

# 3. Look for ToolCallCompleted events
#    - Is the tool returning the expected data?
#    - Are the arguments correct?
```

**What to look for:**
- `ToolCallStarted` — check `tool_input` (what the LLM asked for)
- `ToolCallCompleted` — check `tool_result` (what came back)
- If the tool result is wrong for valid input → bug in the tool implementation
- If the tool input is wrong → LLM misunderstood the tool description

**Common root causes:**
- Typos in tool matching logic (e.g., string comparisons)
- Case sensitivity issues in input parsing
- Missing cases in switch/match expressions
- Tool returns default/fallback value instead of actual data

## Workflow 2: Agent Calls the Wrong Tool

**Symptom:** The agent selects an unexpected tool for the task.

**Investigation:**

```bash
tele turns list <session-id>
# Look at ToolCallStarted events — which tool was selected?
```

**What to look for:**
- Which tool was called vs which should have been called
- The tool's `[Description]` — is it specific enough?
- Are multiple tools with overlapping descriptions confusing the LLM?
- Is the expected tool even registered in `ChatOptions.Tools`?

**Common fixes:**
- Make tool descriptions more specific and distinct
- Add negative examples ("do NOT use this for...")
- Remove overlapping tools
- Verify the tool is registered in the pipeline

## Workflow 3: Excessive Token Usage

**Symptom:** Agent burns through tokens disproportionate to the task complexity.

**Investigation:**

```bash
tele turns list <session-id>
# Check per-turn token counts in TurnCompleted events
```

**What to look for:**
- Which turns consumed the most tokens?
- Are there repeated tool calls (retry loops)?
- Is the context growing unbounded?
- `ContextWindowSnapshot` events showing near-capacity usage
- `ContextPruned` events indicating the agent had to trim context

**Common fixes:**
- Truncate large tool outputs before returning
- Implement conversation windowing (sliding window of recent turns)
- Reduce system prompt size
- Use streaming to detect and stop runaway responses

## Workflow 4: Agent Errors

**Symptom:** Agent encounters errors during execution.

**Investigation:**

```bash
tele sessions list
# Find sessions with errors

tele turns list <session-id>
# Look for ErrorOccurred and TurnCompleted with status="error"
```

**What to look for:**
- `ErrorOccurred` events — check `category` and `message`
- `RetryAttempted` events — is the agent retrying excessively?
- `TurnCompleted` with `status: "error"` — which turn failed?
- Stack traces or error messages in the event data

**Common categories:**
- `chat_completion` — LLM API call failed
- `tool_execution` — tool threw an exception
- `transport` — network or pipe connection issue
- `rate_limit` — API rate limit exceeded

## Workflow 5: Tool Call Chains and Loops

**Symptom:** Agent makes multiple tool calls when one should suffice, or gets stuck in a loop.

**Investigation:**

```bash
tele turns list <session-id>
# Count ToolCallStarted events per turn
# Look for patterns: same tool called repeatedly with same/similar args
```

**What to look for:**
- Same tool called multiple times with identical arguments
- Tool call → wrong result → retry with slightly different args → repeat
- Circular dependencies between tools
- FunctionInvokingChatClient retry loop exceeding max iterations

**Common fixes:**
- Set `MaximumIterationsPerRequest` on `FunctionInvokingChatClient`
- Add argument validation to tools
- Make tool error messages more informative so the LLM can self-correct
- Consider combining related tools into one

## General Tips

- Always start with `tele status` to verify the service is running
- Use `--json` output for precise parsing of event data
- Compare working sessions against broken sessions to spot differences
- Look at the full turn sequence, not just individual events
- Cross-reference Telescope events with the agent's source code
- Tool call events are the most valuable for debugging — they show exactly what the LLM decided and what happened
