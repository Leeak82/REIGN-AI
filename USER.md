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

<!-- observed: 2026-08-27 | status: superseded -->

- Named a private person as the service provider in public copy.

<!-- observed: 2026-08-27 | status: active -->

- Always use Miss Reign as the public name. Never put a legal name in customer SMS, public pages, calendar event text, or dashboard copy. Customers text +19073001244. Booked visits sync to the configured Google Calendar account.

## Related

- [Agent workspace](/concepts/agent-workspace)
