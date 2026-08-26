# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| OpenAPI document build & serialization | Backend | Build OpenApiDocument, JSON/YAML output, provider abstraction |
| Azure Functions integration | Functions | HttpTrigger, isolated worker v4 wiring, DI registration, sample host |
| API surface & architecture | Lead | Public extension methods, options types, DI strategy, trade-offs |
| Code review | Lead | Review PRs, check quality, suggest improvements |
| Testing | Tester | Unit tests, integration tests, OpenAPI validation, edge cases |
| Scope & priorities | Lead | What to build next, trade-offs, decisions |
| Session logging | Scribe | Automatic — never needs routing |
| RAI review | Rai | Content safety, credential detection, ethical review |
| Verification / Devil''s Advocate | Fact Checker | Verify claims, package/API existence, pre-mortem |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Lead |
| `squad:{name}` | Pick up issue and complete the work | Named member |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member''s label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts to coordinator** answered directly.
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." to fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied, route to that member. The Lead handles all `squad` (base label) triage.
