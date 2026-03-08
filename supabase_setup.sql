-- Slot Theory — Supabase Leaderboard Setup
-- Run this in the Supabase SQL Editor (Dashboard → SQL Editor → New query)
--
-- Design: every run is stored as a separate row (no upsert, no dedup).
-- The leaderboard shows all runs ordered by score — same player can appear
-- multiple times. Use supabase_migration_all_runs.sql on an existing database.

-- ── Table ─────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS public.scores (
    id                  bigserial       PRIMARY KEY,
    player_id           text            NOT NULL,
    player_name         text            NOT NULL DEFAULT '',
    map_id              text            NOT NULL,
    difficulty          text            NOT NULL,    -- "normal" | "hard"
    score               bigint          NOT NULL,
    won                 boolean         NOT NULL DEFAULT false,
    wave_reached        integer         NOT NULL DEFAULT 0,
    lives_remaining     integer         NOT NULL DEFAULT 0,
    play_time_seconds   float           NOT NULL DEFAULT 0,
    game_version        text            NOT NULL DEFAULT '',
    build_code          text            NOT NULL DEFAULT '',  -- packed slot ints, comma-separated
    build_name          text            NOT NULL DEFAULT '',  -- generated once at run end
    submitted_at        timestamptz     NOT NULL DEFAULT now()
    -- No UNIQUE constraint: every run gets its own row
);

-- Index for leaderboard fetch (filter by map+difficulty, sort by score desc)
CREATE INDEX IF NOT EXISTS scores_leaderboard_idx ON public.scores (map_id, difficulty, score DESC);

-- ── RPC: submit_score (always insert) ─────────────────────────────────────────
-- Every run is stored as a new row regardless of previous scores.
-- Returns JSON: { "rank": <int> }
-- Rank = position of this run among all runs on the same map/difficulty.

CREATE OR REPLACE FUNCTION public.submit_score(
    p_player_id         text,
    p_player_name       text,
    p_map_id            text,
    p_difficulty        text,
    p_score             bigint,
    p_won               boolean,
    p_wave_reached      integer,
    p_lives_remaining   integer,
    p_play_time_seconds float,
    p_game_version      text,
    p_build_code        text,
    p_build_name        text DEFAULT ''
) RETURNS json
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
    v_rank bigint;
BEGIN
    INSERT INTO public.scores (
        player_id, player_name, map_id, difficulty,
        score, won, wave_reached, lives_remaining,
        play_time_seconds, game_version, build_code, build_name, submitted_at
    ) VALUES (
        p_player_id, p_player_name, p_map_id, p_difficulty,
        p_score, p_won, p_wave_reached, p_lives_remaining,
        p_play_time_seconds, p_game_version, p_build_code, p_build_name, now()
    );

    -- Rank = number of runs with a strictly higher score + 1
    SELECT COUNT(*) + 1 INTO v_rank
    FROM public.scores
    WHERE map_id    = p_map_id
      AND difficulty = p_difficulty
      AND score > p_score;

    RETURN json_build_object('rank', v_rank);
END;
$$;

-- ── Row Level Security ────────────────────────────────────────────────────────
-- Public read access; writes only through the RPC (SECURITY DEFINER).

ALTER TABLE public.scores ENABLE ROW LEVEL SECURITY;

CREATE POLICY "scores_public_read"
    ON public.scores FOR SELECT USING (true);

-- Grant anon role access to read scores and execute the RPC
GRANT SELECT ON public.scores TO anon;
GRANT EXECUTE ON FUNCTION public.submit_score TO anon;
