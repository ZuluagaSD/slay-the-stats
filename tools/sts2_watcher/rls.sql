-- Enable RLS on all tables
ALTER TABLE public.profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.runs ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.run_players ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.combats ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.events ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.deck_snapshots ENABLE ROW LEVEL SECURITY;

-- Profiles: users can read/update their own profile
CREATE POLICY "Users can view own profile"
  ON public.profiles FOR SELECT
  USING (auth.uid() = id);

CREATE POLICY "Users can update own profile"
  ON public.profiles FOR UPDATE
  USING (auth.uid() = id);

CREATE POLICY "Users can insert own profile"
  ON public.profiles FOR INSERT
  WITH CHECK (auth.uid() = id);

-- Runs: users can see their own runs, anyone can see public stats (no user_id filter for aggregates)
CREATE POLICY "Users can view own runs"
  ON public.runs FOR SELECT
  USING (user_id = auth.uid() OR user_id IS NULL);

CREATE POLICY "Users can insert own runs"
  ON public.runs FOR INSERT
  WITH CHECK (user_id = auth.uid() OR user_id IS NULL);

CREATE POLICY "Users can update own runs"
  ON public.runs FOR UPDATE
  USING (user_id = auth.uid());

-- Service role can do everything (for the watcher upload)
CREATE POLICY "Service role full access on runs"
  ON public.runs FOR ALL
  USING (auth.role() = 'service_role');

CREATE POLICY "Service role full access on events"
  ON public.events FOR ALL
  USING (auth.role() = 'service_role');

CREATE POLICY "Service role full access on combats"
  ON public.combats FOR ALL
  USING (auth.role() = 'service_role');

CREATE POLICY "Service role full access on run_players"
  ON public.run_players FOR ALL
  USING (auth.role() = 'service_role');

CREATE POLICY "Service role full access on deck_snapshots"
  ON public.deck_snapshots FOR ALL
  USING (auth.role() = 'service_role');

CREATE POLICY "Service role full access on profiles"
  ON public.profiles FOR ALL
  USING (auth.role() = 'service_role');

-- Run players: visible if the run is visible
CREATE POLICY "View run players for accessible runs"
  ON public.run_players FOR SELECT
  USING (EXISTS (
    SELECT 1 FROM public.runs WHERE runs.id = run_players.run_id
    AND (runs.user_id = auth.uid() OR runs.user_id IS NULL)
  ));

-- Events: visible if the run is visible
CREATE POLICY "View events for accessible runs"
  ON public.events FOR SELECT
  USING (EXISTS (
    SELECT 1 FROM public.runs WHERE runs.id = events.run_id
    AND (runs.user_id = auth.uid() OR runs.user_id IS NULL)
  ));

-- Combats: visible if the run is visible
CREATE POLICY "View combats for accessible runs"
  ON public.combats FOR SELECT
  USING (EXISTS (
    SELECT 1 FROM public.runs WHERE runs.id = combats.run_id
    AND (runs.user_id = auth.uid() OR runs.user_id IS NULL)
  ));

-- Deck snapshots: visible if the run is visible
CREATE POLICY "View deck snapshots for accessible runs"
  ON public.deck_snapshots FOR SELECT
  USING (EXISTS (
    SELECT 1 FROM public.runs WHERE runs.id = deck_snapshots.run_id
    AND (runs.user_id = auth.uid() OR runs.user_id IS NULL)
  ));

-- Auto-create profile on signup
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS trigger AS $$
BEGIN
  INSERT INTO public.profiles (id, display_name)
  VALUES (new.id, COALESCE(new.raw_user_meta_data->>'display_name', split_part(new.email, '@', 1)));
  RETURN new;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE OR REPLACE TRIGGER on_auth_user_created
  AFTER INSERT ON auth.users
  FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();
