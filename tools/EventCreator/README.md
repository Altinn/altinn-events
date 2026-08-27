# EventCreator

Console tool for inspecting App instances/events and, when needed, manually registering a CloudEvent for an App instance — e.g. when an app failed to emit an event itself, or an event subscriber needs an event resent.

## Before generating anything, analyze

Don't assume what's missing — check it:

- Is the instance archived (`Status.IsArchived`), and does it have `CompleteConfirmations` set? That combination generally means the process is done and all parties have received what they need. Some apps have custom process steps and event types, or archive via a different flow entirely — check what other archived instances of the *same app* actually went through before assuming a "standard" event list.
- Confirm the app is actually set up to produce events itself — don't generate events for one that isn't.

The interactive menu's "Analyze app instance" and "Compare with similar archived instances" actions (below) exist to make this check quick.

## Configuration

Four settings are needed, via user secrets (from the project directory):

```
dotnet user-secrets set "GeneralSettings:SourceBaseAddress" <app host base address>
dotnet user-secrets set "StorageDbSettings:ConnectionString" <postgresql connection string>
dotnet user-secrets set "EventsDbSettings:ConnectionString" <postgresql connection string>
dotnet user-secrets set "QueueStorageSettings:ConnectionString" <storage account connection string>
```

## Running

```
dotnet run
```

launches the interactive menu. Passing `-b`/`--batch` instead reads instance GUIDs from `instances.txt` (one raw GUID per line, no quotes or commas) and sends a hardcoded event type for each, logging progress to `log.txt` — the event type for batch mode is set directly in `Program.cs`. It is preferred to use the interactive menu instead.

### Interactive menu

1. **Analyze app instance** — fetches an instance from Storage and prints its status (created, last changed, current process step, archived), completion confirmations, and its recorded events from the Events DB.
2. **Compare with similar archived instances** — given an instance, finds other **archived** instances of the same app and prints their event sequences and confirmation status next to the target's, so you can see what a normal/healthy run of that specific app looks like and spot which event is missing. Also reports how many of the compared instances had completion confirmations, as a signal for whether this app's process typically confirms before archiving. Only considers instances archived at least N days ago (configurable, default 1) — a recently archived instance may show "Confirmed: No" simply because the confirming third party hasn't acted yet, not because this app's process never confirms.
3. **Generate event for instance** — sends a CloudEvent of a chosen type (default `app.instance.process.completed`) onto the events-registration queue for an instance, in the same shape the platform itself produces.
4. **Exit**

The instance GUID you last worked with is remembered and offered as the default for the next prompt — press Enter to reuse it.

### Why events are ordered by `sequenceno`, not `registeredtime`

`events.events.registeredtime` is a `timestamptz`, but several events registered within the same transaction can end up with the exact same value (Postgres freezes `now()` for the duration of a transaction), so `registeredtime` alone gives no reliable tie-break — and ties aren't guaranteed to come back in a stable order. `sequenceno` (the table's `BIGSERIAL` primary key) is strictly monotonic and always reflects true insertion order, so the tool sorts by that instead, and displays timestamps with millisecond precision alongside it.