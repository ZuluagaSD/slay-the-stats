-- Player profiles (linked to Supabase auth.users)
CREATE TABLE public.profiles (
  id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
  display_name text,
  created_at timestamptz DEFAULT now(),
  updated_at timestamptz DEFAULT now()
);

-- Runs
CREATE TABLE public.runs (
  id text PRIMARY KEY,
  user_id uuid REFERENCES public.profiles(id) ON DELETE SET NULL,
  seed text,
  ascension int NOT NULL DEFAULT 0,
  win boolean NOT NULL DEFAULT false,
  abandoned boolean NOT NULL DEFAULT false,
  killed_by_encounter text,
  killed_by_event text,
  run_time_seconds float NOT NULL DEFAULT 0,
  total_floors int NOT NULL DEFAULT 0,
  event_count bigint NOT NULL DEFAULT 0,
  started_at timestamptz,
  ended_at timestamptz,
  created_at timestamptz DEFAULT now()
);

-- Run players (supports co-op: multiple players per run)
CREATE TABLE public.run_players (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  run_id text NOT NULL REFERENCES public.runs(id) ON DELETE CASCADE,
  net_id bigint NOT NULL,
  character text NOT NULL,
  UNIQUE(run_id, net_id)
);

-- Combat encounters within a run
CREATE TABLE public.combats (
  id text PRIMARY KEY,
  run_id text NOT NULL REFERENCES public.runs(id) ON DELETE CASCADE,
  encounter_id text,
  victory boolean,
  final_round int,
  started_at timestamptz,
  ended_at timestamptz
);

-- All events (the full JSONL data)
CREATE TABLE public.events (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  run_id text NOT NULL REFERENCES public.runs(id) ON DELETE CASCADE,
  seq bigint NOT NULL,
  type text NOT NULL,
  ts timestamptz NOT NULL,
  data jsonb NOT NULL DEFAULT '{}',
  UNIQUE(run_id, seq)
);

-- Deck snapshots (one per trigger point)
CREATE TABLE public.deck_snapshots (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  run_id text NOT NULL REFERENCES public.runs(id) ON DELETE CASCADE,
  trigger text NOT NULL,
  ts timestamptz NOT NULL,
  players jsonb NOT NULL
);

-- Indexes for common queries
CREATE INDEX idx_runs_user_id ON public.runs(user_id);
CREATE INDEX idx_run_players_character ON public.run_players(character);
CREATE INDEX idx_runs_win ON public.runs(win);
CREATE INDEX idx_events_run_id ON public.events(run_id);
CREATE INDEX idx_events_type ON public.events(type);
CREATE INDEX idx_combats_run_id ON public.combats(run_id);
CREATE INDEX idx_deck_snapshots_run_id ON public.deck_snapshots(run_id);
