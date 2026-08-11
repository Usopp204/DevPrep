# Tooling Follow-ups

Deferred items about the development harness itself, not the application. Recorded so they are not lost between batches.

---

## T-01 — `PreToolUse` hook is the only real enforcement of the protected-directory rule

**Status:** deferred by decision, 2026-08-11. Not scheduled.

### The gap

`.claude/settings.json` denies `Edit(...)` on five teammate-owned service directories:

```
Edit(source/AuthAPI/**)      Edit(source/PaymentAPI/**)     Edit(source/UserAPI/**)
Edit(source/NewsAPI/**)      Edit(source/NotificationAPI/**)
```

**These rules constrain the editor tool only. They do not constrain shell writes.** During Batch A, twelve `Dockerfile.dockerignore` files were created with `cp`, five of them into protected directories, and the guardrail did not fire — the writes landed and were committed before anyone noticed. The gap was found in Batch B when an `Edit` on the same path was correctly refused.

### Why it cannot be fixed with deny rules

`Edit(...)` patterns match **file paths**. `Bash(...)` patterns match the **command string** — the permission layer has no idea which files a shell command will touch. There is no path-based Bash write rule to express.

Enumerating commands does not work. A rule like `Bash(cp *source/AuthAPI*)` is evaded by every one of: `sed -i`, `tee`, `>` redirection, `mv`, `install`, `python -c`, a heredoc, or simply `cd source/AuthAPI` first and using a relative path. A partial rule is worse than none, because it looks protective and is not.

### The only real fix

A `PreToolUse` hook. It receives the tool name and its input before execution and can block:

- for `Write` / `Edit` / `NotebookEdit` — inspect the resolved `file_path`
- for `Bash` — inspect the command string, resolve candidate paths, and reject writes into protected directories

Sketch of the policy it would encode:

```
if tool is Write|Edit and file_path is under a protected dir
        and basename not in {Dockerfile, Dockerfile.dockerignore, .dockerignore}:
    block
if tool is Bash and command mentions a protected dir
        and command contains a write verb (cp, mv, sed -i, tee, >, >>, install, dd):
    block
```

The Bash half stays heuristic — a determined bypass is always possible — but it would have caught the Batch A `cp`, which is the actual failure that occurred.

### Why it was deferred

The current arrangement is a deliberate lift-per-batch ritual: when a batch genuinely needs to write into a protected directory, the owner backs up `settings.json`, lifts exactly those rules, the work is done, and the file is restored and checksum-verified immediately. **Every lift is therefore a conscious decision by a human**, which is the property that matters most. The hook would automate enforcement but also make the boundary invisible again.

Revisit if either becomes true:
1. the lift ritual starts being skipped or batched together for convenience, or
2. more than one person begins running agent sessions against this repo.

### Rejected alternative, recorded so it is not re-proposed

Narrowing the deny rules to business-logic file patterns (`Edit(source/AuthAPI/**/*.cs)` and similar) so that `Dockerfile` edits are permitted by omission. **Rejected because it is opt-out:** any new file type in those directories — a `.razor`, a `.sql`, a `.props` — becomes editable by default without anyone deciding it should be. That is the same failure shape as the Edit-vs-Bash gap it was meant to fix.

---

## T-02 — `dotnet run` and `docker compose up` removed from the allow list

**Status:** done, 2026-08-11. Recorded for context.

Windows Application Control blocks .NET processes spawned by Claude Code (`Win32Exception 4551`; see `CLAUDE.md`). Allowing these commands could only ever produce a confusing failure, so they were removed from `permissions.allow` rather than relying on per-session instructions.

`Bash(dotnet build*)` is retained — building is safe and useful. Running services remains the developer's own terminal.
