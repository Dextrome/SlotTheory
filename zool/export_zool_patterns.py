"""
export_zool_patterns.py
Parses "rock n zool.mod" (ProTracker format) and exports all pattern/note data
to zool_patterns.json for easy reference without re-parsing the binary.

Period → MIDI formula: MIDI = 60 + 12 * log2(428 / period)
  Period 428 = C4 = MIDI 60 (Amiga 8287 Hz at 3,546,895 / 428 = 8287 Hz)

Sample-rate correction (for synthesis at equal temperament):
  chugalug  sample rate 8468 Hz → freq *= 8287 / 8468
  coolbass2 sample rate 8051 Hz → freq *= 8287 / 8051
"""

import struct
import math
import json

MOD_PATH = "rock n zool.mod"
OUT_PATH = "zool_patterns.json"

# ProTracker period table (octave 1–3, notes C through B)
# Used for cross-referencing; we compute MIDI directly from period.
AMIGA_CLOCK = 3_546_895
REF_PERIOD   = 428       # period 428 = MIDI 60 (C4 / C-5 in OpenMPT convention)

# Known sample names and their actual sample rates (from OpenMPT / extra0.png)
SAMPLE_RATES = {
    "chugalug":  8468,
    "coolbass2": 8051,
}
DEFAULT_RATE = 8287  # standard samples

NOTE_NAMES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]


def period_to_midi(period: int) -> float:
    """Convert Amiga period to MIDI note number (float). Period 428 = MIDI 60."""
    if period <= 0:
        return None
    return 60.0 + 12.0 * math.log2(REF_PERIOD / period)


def midi_to_name(midi_f: float) -> str:
    """Convert float MIDI to note name like 'A5' or 'G#6'."""
    midi = round(midi_f)
    octave = (midi // 12) - 1
    note   = NOTE_NAMES[midi % 12]
    return f"{note}{octave}"


def midi_freq(midi: float, sample_name: str = "") -> float:
    """Concert pitch frequency at equal temperament, with optional sample-rate correction."""
    freq = 440.0 * (2.0 ** ((midi - 69) / 12.0))
    rate = SAMPLE_RATES.get(sample_name.lower().strip(), DEFAULT_RATE)
    if rate != DEFAULT_RATE:
        freq *= DEFAULT_RATE / rate
    return freq


def parse_mod(path: str) -> dict:
    with open(path, "rb") as f:
        data = f.read()

    # Song name (bytes 0–19)
    song_name = data[:20].rstrip(b"\x00").decode("ascii", errors="replace")

    # Sample headers (31 samples, each 30 bytes, starting at byte 20)
    samples = []
    for i in range(31):
        base = 20 + i * 30
        name_raw = data[base:base+22].rstrip(b"\x00").decode("ascii", errors="replace")
        length    = struct.unpack_from(">H", data, base + 22)[0] * 2
        finetune  = data[base + 24] & 0x0F
        volume    = data[base + 25]
        loop_start = struct.unpack_from(">H", data, base + 26)[0] * 2
        loop_len   = struct.unpack_from(">H", data, base + 28)[0] * 2
        rate       = SAMPLE_RATES.get(name_raw.lower().strip(), DEFAULT_RATE)
        samples.append({
            "index":     i + 1,
            "name":      name_raw,
            "length":    length,
            "finetune":  finetune,
            "volume":    volume,
            "loop_start": loop_start,
            "loop_len":   loop_len,
            "sample_rate": rate,
        })

    # Song length and pattern order table (bytes 950–981)
    song_length  = data[950]
    pattern_order = list(data[952:952 + 128])
    num_patterns  = max(pattern_order[:song_length]) + 1

    # Pattern data starts at byte 1084
    pattern_offset = 1084
    patterns = []
    for pat_idx in range(num_patterns):
        rows = []
        for row in range(64):
            channels = []
            for ch in range(4):
                offset = pattern_offset + pat_idx * 64 * 4 * 4 + row * 4 * 4 + ch * 4
                b0, b1, b2, b3 = data[offset], data[offset+1], data[offset+2], data[offset+3]
                sample_num = (b0 & 0xF0) | (b2 >> 4)
                period     = ((b0 & 0x0F) << 8) | b1
                effect     = b2 & 0x0F
                param      = b3

                cell = {"sample": sample_num, "period": period, "effect": effect, "param": param}

                if period > 0:
                    midi_f = period_to_midi(period)
                    midi_i = round(midi_f)
                    sname  = samples[sample_num - 1]["name"] if 1 <= sample_num <= 31 else ""
                    cell["midi"]          = midi_i
                    cell["midi_exact"]    = round(midi_f, 3)
                    cell["note"]          = midi_to_name(midi_f)
                    cell["sample_name"]   = sname
                    cell["freq_standard"] = round(440.0 * (2.0 ** ((midi_f - 69) / 12.0)), 2)
                    cell["freq_corrected"]= round(midi_freq(midi_f, sname), 2)

                channels.append(cell)
            rows.append(channels)
        patterns.append(rows)

    return {
        "song_name":     song_name,
        "song_length":   song_length,
        "pattern_order": pattern_order[:song_length],
        "num_patterns":  num_patterns,
        "samples":       samples,
        "patterns":      patterns,
    }


def build_summary(parsed: dict) -> dict:
    """Build a human-readable summary: which notes play on which channels per pattern."""
    order   = parsed["pattern_order"]
    samples = parsed["samples"]
    summary = {}

    for seq_idx, pat_idx in enumerate(order):
        rows = parsed["patterns"][pat_idx]
        channels_summary = {0: [], 1: [], 2: [], 3: []}
        for row_idx, channels in enumerate(rows):
            for ch_idx, cell in enumerate(channels):
                if cell["period"] > 0:
                    sname = cell.get("sample_name", "")
                    entry = {
                        "row":   row_idx,
                        "note":  cell["note"],
                        "midi":  cell["midi"],
                        "sample": cell["sample"],
                        "sample_name": sname,
                        "freq_corrected": cell.get("freq_corrected"),
                    }
                    channels_summary[ch_idx].append(entry)

        summary[f"seq{seq_idx:02d}_pat{pat_idx:02d}"] = {
            "sequence_index": seq_idx,
            "pattern_index":  pat_idx,
            "ch0": channels_summary[0],
            "ch1": channels_summary[1],
            "ch2": channels_summary[2],
            "ch3": channels_summary[3],
        }

    return summary


def main():
    print(f"Parsing {MOD_PATH} ...")
    parsed = parse_mod(MOD_PATH)

    print(f"Song: {parsed['song_name']}")
    print(f"Patterns in order: {parsed['pattern_order']}")
    print(f"Total unique patterns: {parsed['num_patterns']}")
    print()

    # Print samples table
    print("=== SAMPLES ===")
    for s in parsed["samples"]:
        if s["length"] > 0:
            print(f"  [{s['index']:2d}] {s['name']:<22s}  len={s['length']:6d}  vol={s['volume']:3d}  rate={s['sample_rate']} Hz")
    print()

    # Print per-pattern summary for the patterns used in the song order
    summary = build_summary(parsed)
    print("=== PATTERN NOTES (used patterns only) ===")
    for key, pat_data in summary.items():
        print(f"\n{key}:")
        for ch_idx in range(4):
            ch_key = f"ch{ch_idx}"
            events = pat_data[ch_key]
            if events:
                notes_str = "  ".join(
                    f"r{e['row']:02d}:{e['note']}({e['midi']})"
                    + (f"[{e['sample_name'].split()[0]}]" if e['sample_name'] else "")
                    for e in events
                )
                print(f"  Ch{ch_idx}: {notes_str}")

    # Build output dict
    output = {
        "_readme": (
            "Generated by export_zool_patterns.py from 'rock n zool.mod'. "
            "Period→MIDI: MIDI=60+12*log2(428/period). "
            "freq_corrected applies sample-rate tuning (chugalug*8287/8468, coolbass2*8287/8051). "
            "Channels: 0=melody/lead, 1=chug/rhythm, 2=bass, 3=extra (check per pattern)."
        ),
        "song_name":     parsed["song_name"],
        "pattern_order": parsed["pattern_order"],
        "num_patterns":  parsed["num_patterns"],
        "samples":       parsed["samples"],
        "pattern_summary": summary,
        "patterns_raw":  parsed["patterns"],
    }

    with open(OUT_PATH, "w") as f:
        json.dump(output, f, indent=2)

    print(f"\nWrote {OUT_PATH}")


if __name__ == "__main__":
    main()
