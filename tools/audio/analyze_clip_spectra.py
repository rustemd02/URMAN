#!/usr/bin/env python3
"""Per-clip spectral evidence for the URMAN Act I "dog + summer birds" report.

Reads 24 kHz mono PCM16 WAV clips and prints, for every clip:

  * duration, overall RMS/peak in dBFS;
  * the share of frame energy in the <1 kHz / 1-3 kHz / 3-8 kHz / >8 kHz
    bands, globally and gated to quiet frames (frames whose total energy is
    below the clip median, as requested for this audit);
  * a birdsong signature scan of exactly those quiet frames: short
    (~30-200 ms) narrow-band traces in 2-8 kHz with fast frequency
    modulation or harmonic stacks and little energy below 1 kHz, together
    with the contrast metrics that rule out broadband wind (flat spectrum),
    impulsive clicks (single-frame, broadband, no trace) and speech murmur
    (energy mainly <4 kHz, formant structure);
  * a verdict: CLEAN / BIRDSONG-LIKELY / BIRDSONG-CONFIRMED.

Nothing is written to disk; everything goes to stdout. Usage:

  python3 tools/audio/analyze_clip_spectra.py CLIP.wav [CLIP.wav ...]
  python3 tools/audio/analyze_clip_spectra.py --verbose CLIP.wav
  python3 tools/audio/analyze_clip_spectra.py --start 23.5 --end 28.6 BED.wav
  python3 tools/audio/analyze_clip_spectra.py --selftest

--start/--end select a time range of a single clip (used to inspect the
timed source tiles inside the mixed village beds). --selftest synthesises
known bird / wind / click signals in memory and checks the classifier.

Method: STFT with a 1024-sample Hann window (42.7 ms) and 256-sample hop
(10.7 ms) at 24 kHz -> 23.4 Hz bins. All decisions use only frame-power
ratios, so the tool is level-independent.
"""

from __future__ import annotations

import argparse
import sys
import wave
from pathlib import Path

import numpy as np

N_FFT = 1024
HOP = 256
BANDS = (("<1k", 0.0, 1000.0), ("1-3k", 1000.0, 3000.0), ("3-8k", 3000.0, 8000.0), (">8k", 8000.0, 12000.0))
BAND_39 = (3000.0, 8000.0)
BIRD_LO, BIRD_HI = 2000.0, 8000.0

# A candidate burst is "birdsong-like" when all of these hold. The isolation
# test rejects harmonics of a lower fundamental (bark/voice): a songbird trace
# at f usually has no strong sibling at f/2..f/5, a bark harmonic always does.
BIRD_CORE = dict(dom=(2000.0, 7800.0), dur=(0.03, 0.25), bw_max=500.0,
                 crest_min=10.0, iso_min=6.0, fm_min=250.0, span_min=400.0)
# Clip verdict thresholds: CONFIRMED needs repeated evidence (or one long fast
# sweep); LIKELY needs at least two traces or one unambiguous long sweep.
CONFIRMED_MIN = 3
LIKELY_MIN = 2


def load_wav(path: str, start: float | None = None, end: float | None = None) -> tuple[np.ndarray, int]:
    with wave.open(path, "rb") as w:
        channels, width, rate, total = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        if width != 2:
            raise SystemExit(f"{path}: only 16-bit PCM WAV supported (got {width * 8}-bit)")
        if start is not None:
            w.setpos(min(total, max(0, int(round(start * rate)))))
        if end is None:
            count = total - w.tell()
        else:
            count = max(0, min(total, int(round(end * rate))) - w.tell())
        raw = w.readframes(count)
    x = np.frombuffer(raw, dtype="<i2").astype(np.float64) / 32768.0
    if channels > 1:
        x = x.reshape(-1, channels).mean(axis=1)
    return x, rate


def spectrogram(x: np.ndarray, rate: int, n: int = N_FFT, hop: int = HOP) -> tuple[np.ndarray, np.ndarray]:
    win = np.hanning(n)
    if x.size < n:
        x = np.pad(x, (0, n - x.size))
    frames = np.lib.stride_tricks.sliding_window_view(x, n)[::hop]
    spec = np.abs(np.fft.rfft(frames * win, axis=1)) ** 2
    freq = np.fft.rfftfreq(n, 1.0 / rate)
    return spec.T, freq  # (bins, frames), (bins,)


def db(power: np.ndarray) -> np.ndarray:
    return 10.0 * np.log10(np.maximum(power, 1e-20))


def band_energy(spec: np.ndarray, freq: np.ndarray, lo: float, hi: float) -> np.ndarray:
    m = (freq >= lo) & (freq < min(hi, freq[-1] + 1e-9))
    return spec[m, :].sum(axis=0)


def band_shares(spec: np.ndarray, freq: np.ndarray, frames: np.ndarray | None) -> dict[str, float]:
    if frames is not None:
        spec = spec[:, frames]
    if spec.shape[1] == 0:
        return {name: 0.0 for name, _, _ in BANDS}
    total = spec.sum(axis=0) + 1e-30
    return {name: float(np.mean(band_energy(spec, freq, lo, hi) / total)) for name, lo, hi in BANDS}


def group_frames(index: np.ndarray, max_gap: int = 3) -> list[tuple[int, int]]:
    runs: list[tuple[int, int]] = []
    start = prev = None
    for i in index:
        if start is None:
            start = prev = int(i)
        elif i - prev <= max_gap:
            prev = int(i)
        else:
            runs.append((start, prev))
            start = prev = int(i)
    if start is not None:
        runs.append((start, prev))
    return runs


def scan_birds(spec: np.ndarray, freq: np.ndarray, quiet: np.ndarray, rate: int, hop: int = HOP,
               n: int = N_FFT, verbose: bool = False) -> dict:
    """Bird-signature scan. Candidates are detected on all frames (a clip whose
    loudest content is birdsong must still be caught); each event records how
    much of it lies inside the below-median quiet gate. Returns events + summary."""
    hb = (freq >= BIRD_LO) & (freq <= BIRD_HI)
    sdb = db(spec)
    # Spectral crest (peak minus median) inside 2-8 kHz, per frame.
    sub = sdb[hb, :]
    crest = sub.max(axis=0) - np.median(sub, axis=0)
    # Per-bin temporal median as the local baseline (robust to bursts).
    if quiet.any():
        base = np.median(spec[:, quiet], axis=1)
        q = quiet
    else:
        base = np.median(spec, axis=1)
        q = np.ones(spec.shape[1], dtype=bool)
    hbe = spec[hb, :].sum(axis=0)
    floor = max(float(np.median(hbe[q])), 1e-20)
    # Candidate frames: tonal crest in the 2-8 kHz band AND a high-band lift.
    lift = db(hbe) - db(np.full_like(hbe, floor))
    cand = (crest >= 9.0) & (lift >= 3.0)
    cand |= (crest >= 7.0) & (lift >= 6.0)
    runs = group_frames(np.flatnonzero(cand), max_gap=3)
    events = []
    for a, b in runs:
        nfr = b - a + 1
        dur = (nfr - 1) * hop / rate + n / rate
        if dur < 0.02 or dur > 0.35:
            continue
        seg = spec[:, a:b + 1]
        exc = np.maximum(seg.mean(axis=1) - base, 0.0)
        if exc[hb].max() <= 0.0:
            continue
        dom = float(freq[hb][np.argmax(exc[hb])])
        # Per-frame trace and per-frame -10 dB bandwidth around the local peak:
        # the bandwidth of a fast sweep must be judged frame by frame, not from
        # the whole-event mean spectrum (which smears the sweep out).
        trace, widths, crests = [], [], []
        for j in range(a, b + 1):
            near = (freq[hb] >= dom - 1200.0) & (freq[hb] <= dom + 1200.0)
            if not near.any():
                continue
            fj, sj = freq[hb][near], sdb[hb, j][near]
            pk = int(np.argmax(sj))
            trace.append(float(fj[pk]))
            crests.append(float(sj[pk] - np.median(sj)))
            above = np.flatnonzero(sj >= sj[pk] - 10.0)
            widths.append(float(fj[above[-1]] - fj[above[0]]) if above.size else 0.0)
        trace = np.array(trace)
        if trace.size == 0:
            continue
        fm = float(np.max(np.abs(np.diff(trace))) / (hop / rate) / 100.0) if trace.size > 1 else 0.0
        span = float(trace.max() - trace.min())
        bw = float(np.median(widths))
        # How much of what the event *adds* over the local baseline lies below
        # 1 kHz, whether the added high-band peak dominates added low-frequency
        # energy, and the isolation of the trace from f/2..f/5 siblings.
        low = float(exc[(freq >= 0.0) & (freq < 1000.0)].sum() / max(exc.sum(), 1e-30))
        exc_hi = float(exc[hb].max())
        exc_lo = float(exc[(freq >= 0.0) & (freq < 1000.0)].max()) if (freq < 1000.0).any() else 0.0
        dominance = float(db(np.array([max(exc_hi, 1e-20)]))[0] - db(np.array([max(exc_lo, 1e-20)]))[0])

        def peak_db(fc: float) -> float:
            m = (freq >= fc * 0.97) & (freq <= fc * 1.03)
            return float(db(np.array([exc[m].max()]))[0]) if m.any() else -400.0

        iso = peak_db(dom) - max(peak_db(dom / k) for k in (2, 3, 4, 5))
        # Harmonic stack: local maxima of the excess spectrum above +6 dB.
        peaks = []
        mm = exc[hb]
        for i in range(1, len(mm) - 1):
            if mm[i] > mm[i - 1] and mm[i] >= mm[i + 1] and db(mm[i] + 1e-20) >= 6.0:
                peaks.append(float(freq[hb][i]))
        peaks.sort()
        harm = 0
        if len(peaks) >= 2:
            top = peaks[-1]
            for p in peaks[:-1]:
                r = top / max(p, 1e-9)
                if abs(r - round(r)) < 0.12 and 2 <= round(r) <= 5:
                    harm = int(round(r))
                    break
        ev = dict(t0=float(a * hop / rate), dur=dur, dom=dom, bw=bw, fm=fm, span=span,
                  low=low, dominance=dominance, iso=iso, crest=float(np.median(crests)), harm=harm,
                  nfr=nfr, quiet=float(np.mean(q[a:b + 1])))
        ev["bird"] = (BIRD_CORE["dom"][0] <= dom <= BIRD_CORE["dom"][1]
                      and BIRD_CORE["dur"][0] <= dur <= BIRD_CORE["dur"][1]
                      and bw <= BIRD_CORE["bw_max"] and ev["crest"] >= BIRD_CORE["crest_min"]
                      and iso >= BIRD_CORE["iso_min"]
                      and (fm >= BIRD_CORE["fm_min"] or span >= BIRD_CORE["span_min"] or harm > 0))
        events.append(ev)
        if verbose:
            print(f"      event t={ev['t0']:.3f}s dur={dur * 1000:.0f}ms dom={dom:.0f}Hz bw={bw:.0f}Hz "
                  f"fm={fm:.0f}Hz/100ms span={span:.0f}Hz lowadd={low:.2f} iso={iso:.1f}dB "
                  f"crest={ev['crest']:.1f} harm={harm or '-'} quiet={ev['quiet']:.2f} "
                  f"{'BIRD' if ev['bird'] else 'other'}")
    birds = [e for e in events if e["bird"]]
    quiet_birds = [e for e in birds if e["quiet"] >= 0.5]
    med = lambda arr, key: float(np.median([e[key] for e in arr])) if arr else 0.0
    return dict(events=events, birds=birds, n_bird_quiet=len(quiet_birds),
                quiet_floor_db=float(db(np.array([floor]))[0]),
                quiet_hb_share=float(hbe[q].sum() / max(spec[:, q].sum(), 1e-30)),
                med_dur=med(birds, "dur"), med_dom=med(birds, "dom"),
                dom_min=float(min([e["dom"] for e in birds])) if birds else 0.0,
                dom_max=float(max([e["dom"] for e in birds])) if birds else 0.0,
                med_fm=med(birds, "fm"), med_low=med(birds, "low"))


def clip_verdict(birds: list[dict]) -> str:
    if len(birds) >= CONFIRMED_MIN:
        return "BIRDSONG-CONFIRMED"
    if len(birds) >= LIKELY_MIN or any(e["dur"] >= 0.10 and e["fm"] >= 400.0 for e in birds):
        return "BIRDSONG-LIKELY"
    return "CLEAN"


def analyze(path: str, start: float | None, end: float | None, verbose: bool) -> dict:
    x, rate = load_wav(path, start, end)
    if x.size == 0:
        raise SystemExit(f"{path}: empty range")
    rms = float(np.sqrt(np.mean(x * x)))
    peak = float(np.max(np.abs(x)))
    spec, freq = spectrogram(x, rate)
    energy = spec.sum(axis=0)
    med = float(np.median(energy))
    quiet = (energy <= med) & (energy > max(energy.max() * 1e-9, 1e-20))
    shares_all = band_shares(spec, freq, None)
    shares_q = band_shares(spec, freq, np.flatnonzero(quiet))
    scan = scan_birds(spec, freq, quiet, rate, verbose=verbose)
    n_bird = len(scan["birds"])
    verdict = clip_verdict(scan["birds"])
    return dict(path=path, dur=x.size / rate, rms_db=20 * np.log10(max(rms, 1e-9)),
                peak_db=20 * np.log10(max(peak, 1e-9)), shares_all=shares_all, shares_q=shares_q,
                quiet_floor_db=float(db(np.array([np.median(energy[quiet]) if quiet.any() else med]))[0]),
                scan=scan, n_events=len(scan["events"]), n_bird=n_bird, verdict=verdict, rate=rate)


HEADER = (f"{'clip':34} {'dur':>7} {'rms dB':>7} {'peak dB':>8} {'3-8k all':>9} {'3-8k quiet':>10} "
          f"{'quiet flr':>9} {'<1k q':>6} {'1-3k q':>7} {'>8k q':>6} {'evts':>4} {'bird':>4}  verdict")


def fmt_row(res: dict) -> str:
    name = Path(res["path"]).name + (f" [{res.get('range', '')}]" if res.get("range") else "")
    a, q = res["shares_all"], res["shares_q"]
    return (f"{name:34} {res['dur']:6.2f}s {res['rms_db']:7.1f} {res['peak_db']:8.1f} "
            f"{a['3-8k']:9.3f} {q['3-8k']:10.3f} {res['quiet_floor_db']:8.1f} "
            f"{q['<1k']:6.3f} {q['1-3k']:7.3f} {q['>8k']:6.3f} {res['n_events']:4d} {res['n_bird']:4d}  {res['verdict']}")


def selftest() -> int:
    """Synthesise known signals and check the classifier."""
    rate = 24000
    t = np.arange(int(2.0 * rate)) / rate
    rng = np.random.default_rng(7)
    wind = rng.normal(0, 0.02, t.size)
    clicks = wind.copy()
    for c in range(1, 6):
        i = int(c * 0.3 * rate)
        clicks[i:i + 24] += rng.normal(0, 1.0, 24) * np.hanning(24)
    chirps = wind.copy()
    for c in range(5):
        t0 = 0.2 + c * 0.35
        i = int(t0 * rate)
        dur = int(0.08 * rate)
        tt = np.arange(dur) / rate
        f = 3600 + 1700 * tt / 0.08
        chirps[i:i + dur] += 0.3 * np.sin(2 * np.pi * np.cumsum(f) / rate) * np.hanning(dur)
    ok = True
    for label, sig, expect in (("synthetic wind", wind, "CLEAN"),
                               ("synthetic clicks", clicks, "CLEAN"),
                               ("synthetic chirps", chirps, "BIRDSONG-CONFIRMED")):
        spec, freq = spectrogram(sig, rate)
        energy = spec.sum(axis=0)
        quiet = energy <= np.median(energy)
        scan = scan_birds(spec, freq, quiet, rate)
        verdict = clip_verdict(scan["birds"])
        mark = "ok" if verdict == expect else "FAIL"
        if mark == "FAIL":
            ok = False
        print(f"selftest {label:18} -> {verdict:20} (expected {expect:16}) {mark}")
    return 0 if ok else 1


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="Per-clip birdsong evidence for URMAN Act I audio.")
    ap.add_argument("clips", nargs="*", help="WAV files to analyse")
    ap.add_argument("--start", type=float, default=None, help="range start in seconds (single clip)")
    ap.add_argument("--end", type=float, default=None, help="range end in seconds (single clip)")
    ap.add_argument("--verbose", action="store_true", help="print every burst event of the quiet-window scan")
    ap.add_argument("--selftest", action="store_true", help="run synthetic signal checks")
    args = ap.parse_args(argv)
    if args.selftest:
        return selftest()
    if not args.clips:
        ap.print_help()
        return 2
    if (args.start is not None or args.end is not None) and len(args.clips) != 1:
        raise SystemExit("--start/--end need exactly one clip")
    print(HEADER)
    results = []
    for clip in args.clips:
        if args.verbose:
            print(f"   {clip}")
        res = analyze(clip, args.start, args.end, args.verbose)
        if args.start is not None or args.end is not None:
            res["range"] = f"{args.start if args.start is not None else 0:.1f}-{args.end if args.end is not None else res['dur']:.1f}s"
        results.append(res)
        print(fmt_row(res))
        s = res["scan"]
        if res["n_bird"]:
            print(f"      bird bursts: n={res['n_bird']} (quiet-window {s['n_bird_quiet']}) median dur={s['med_dur'] * 1000:.0f}ms "
                  f"dom={s['dom_min']:.0f}-{s['dom_max']:.0f}Hz (med {s['med_dom']:.0f}Hz) "
                  f"med fm={s['med_fm']:.0f}Hz/100ms med lowadd={s['med_low']:.2f}")
            print(f"      quiet-window high-band share={s['quiet_hb_share']:.3f} quiet floor={s['quiet_floor_db']:.1f} dBFS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
