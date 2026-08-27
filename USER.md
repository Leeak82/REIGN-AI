# USER.md - User Model

Store stable user preferences and profile facts as directives that can guide future sessions.

Use one directive per entry:

```md
<!-- observed: YYYY-MM-DD | status: active -->

- Prefer concise progress updates during implementation work.
```

- Begin each directive with an imperative such as `Always`, `Never`, or `Prefer`.
- Record the observation date and either `active` or `superseded` on the metadata line.
- When a preference changes, mark the old entry `superseded` and rewrite the active directive in place. Never append a contradictory active directive.
- Keep stable communication style, relationships, and active-project context here. Put durable non-profile facts and decisions in `MEMORY.md`.

## Directives

<!-- observed: 2026-08-27 | status: active -->

- Always treat Jessica Collins as the service provider. Confirmed visits go on `j.collins2491@gmail.com` so she can see her schedule and provide QV, HH, or HR. Miss Reign is the SMS assistant. Customers text +19073001244.

## Related

- [Agent workspace](/concepts/agent-workspace)
