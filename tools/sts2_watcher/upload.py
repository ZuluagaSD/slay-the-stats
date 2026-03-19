"""Upload local JSONL run data to Supabase."""
import json
import os
import sys
import urllib.request
import urllib.error

SUPABASE_URL = os.environ.get("SUPABASE_URL", "https://sskibxdluttejitksnkr.supabase.co")
SERVICE_KEY = os.environ.get("SUPABASE_SERVICE_KEY", "")

if not SERVICE_KEY:
    print("Error: Set SUPABASE_SERVICE_KEY environment variable")
    print("  PowerShell: $env:SUPABASE_SERVICE_KEY = 'your-key-here'")
    sys.exit(1)

HEADERS = {
    "apikey": SERVICE_KEY,
    "Authorization": f"Bearer {SERVICE_KEY}",
    "Content-Type": "application/json",
    "Prefer": "return=minimal",
}


def post(table, data):
    """Insert data into a Supabase table."""
    url = f"{SUPABASE_URL}/rest/v1/{table}"
    body = json.dumps(data).encode()
    req = urllib.request.Request(url, data=body, headers=HEADERS, method="POST")
    try:
        urllib.request.urlopen(req)
    except urllib.error.HTTPError as e:
        err = e.read().decode()
        # Ignore duplicate key errors (already uploaded)
        if "duplicate key" in err or "23505" in err:
            return False
        print(f"  Error inserting into {table}: {err}")
        return False
    return True


def upsert(table, data):
    """Upsert data into a Supabase table."""
    url = f"{SUPABASE_URL}/rest/v1/{table}"
    body = json.dumps(data).encode()
    headers = {**HEADERS, "Prefer": "resolution=merge-duplicates,return=minimal"}
    req = urllib.request.Request(url, data=body, headers=headers, method="POST")
    try:
        urllib.request.urlopen(req)
    except urllib.error.HTTPError as e:
        print(f"  Error upserting into {table}: {e.read().decode()}")
        return False
    return True


def upload_run(run_dir):
    """Upload a single run directory to Supabase."""
    meta_path = os.path.join(run_dir, "meta.json")
    events_path = os.path.join(run_dir, "events.jsonl")

    if not os.path.exists(events_path):
        print(f"  Skipping {run_dir} — no events.jsonl")
        return

    # Read meta if available
    meta = None
    if os.path.exists(meta_path):
        with open(meta_path) as f:
            meta = json.load(f)

    # Read all events
    events = []
    with open(events_path) as f:
        for line in f:
            line = line.strip()
            if line:
                events.append(json.loads(line))

    if not events:
        print(f"  Skipping {run_dir} — empty events")
        return

    run_id = events[0].get("runId", os.path.basename(run_dir))
    print(f"  Uploading run {run_id} ({len(events)} events)...")

    # Insert run record
    run_data = {
        "id": run_id,
        "seed": meta.get("seed") if meta else None,
        "ascension": meta.get("ascension", 0) if meta else 0,
        "win": meta.get("win", False) if meta else False,
        "abandoned": meta.get("abandoned", False) if meta else False,
        "killed_by_encounter": meta.get("killedByEncounter") if meta else None,
        "killed_by_event": meta.get("killedByEvent") if meta else None,
        "run_time_seconds": meta.get("runTime", 0) if meta else 0,
        "total_floors": meta.get("totalFloors", 0) if meta else 0,
        "event_count": len(events),
        "started_at": meta.get("startedAt") if meta else events[0].get("ts"),
        "ended_at": meta.get("endedAt") if meta else events[-1].get("ts"),
    }

    if not upsert("runs", run_data):
        return

    # Insert run players from meta
    if meta and meta.get("players"):
        for p in meta["players"]:
            upsert("run_players", {
                "run_id": run_id,
                "net_id": p["netId"],
                "character": p["character"],
            })

    # Process events — extract combats and deck snapshots, insert all events
    combats = {}
    deck_snapshots = []
    event_rows = []

    for evt in events:
        etype = evt.get("type", "")
        data = evt.get("data", {})
        ts = evt.get("ts")

        # Collect event row
        event_rows.append({
            "run_id": run_id,
            "seq": evt.get("seq"),
            "type": etype,
            "ts": ts,
            "data": data,
        })

        # Track combats
        if etype == "combat_start":
            combat_id = data.get("combatId", "")
            combats[combat_id] = {
                "id": f"{run_id}_{combat_id}",
                "run_id": run_id,
                "encounter_id": data.get("encounterId"),
                "started_at": ts,
            }
        elif etype == "combat_end":
            combat_id = data.get("combatId", "")
            if combat_id in combats:
                combats[combat_id]["victory"] = data.get("victory")
                combats[combat_id]["final_round"] = data.get("finalRound")
                combats[combat_id]["ended_at"] = ts

        # Track deck snapshots
        elif etype == "deck_snapshot":
            deck_snapshots.append({
                "run_id": run_id,
                "trigger": data.get("trigger", "unknown"),
                "ts": ts,
                "players": data.get("players", []),
            })

    # Batch insert events (in chunks of 500)
    for i in range(0, len(event_rows), 500):
        chunk = event_rows[i:i+500]
        if not post("events", chunk):
            print(f"  Warning: some events in chunk {i//500} may have failed")

    # Insert combats
    for combat in combats.values():
        upsert("combats", combat)

    # Insert deck snapshots
    for snap in deck_snapshots:
        post("deck_snapshots", snap)

    print(f"  Done: {len(events)} events, {len(combats)} combats, {len(deck_snapshots)} snapshots")


def main():
    base_dir = os.path.join(
        os.environ.get("LOCALAPPDATA", ""),
        "SlayTheStats", "watcher", "runs"
    )

    if not os.path.exists(base_dir):
        print(f"No runs directory found at {base_dir}")
        sys.exit(1)

    run_dirs = sorted(
        [os.path.join(base_dir, d) for d in os.listdir(base_dir)
         if os.path.isdir(os.path.join(base_dir, d))]
    )

    print(f"Found {len(run_dirs)} runs to upload")

    for run_dir in run_dirs:
        upload_run(run_dir)

    print("\nUpload complete!")


if __name__ == "__main__":
    main()
