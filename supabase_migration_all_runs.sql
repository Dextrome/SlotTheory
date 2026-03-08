-- Slot Theory — Migration: switch from keep-best to all-runs leaderboard
-- Run this in Supabase SQL Editor on your EXISTING database.
--
-- What this does:
--   1. Drops the UNIQUE (player_id, map_id, difficulty) constraint so every
--      run can be stored as a separate row.
--   2. Replaces the submit_score RPC with an always-insert version.
--
-- Existing rows are preserved unchanged.

-- ── 1. Drop the unique constraint ────────────────────────────────────────────

ALTER TABLE public.scores
    DROP CONSTRAINT IF EXISTS scores_player_id_map_id_difficulty_key;

-- ── 2. Replace the RPC with an always-insert version ─────────────────────────

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
    p_build_code        text
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
        play_time_seconds, game_version, build_code, submitted_at
    ) VALUES (
        p_player_id, p_player_name, p_map_id, p_difficulty,
        p_score, p_won, p_wave_reached, p_lives_remaining,
        p_play_time_seconds, p_game_version, p_build_code, now()
    );

    SELECT COUNT(*) + 1 INTO v_rank
    FROM public.scores
    WHERE map_id    = p_map_id
      AND difficulty = p_difficulty
      AND score > p_score;

    RETURN json_build_object('rank', v_rank);
END;
$$;
