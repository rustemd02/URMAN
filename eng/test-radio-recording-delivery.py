#!/usr/bin/env python3
"""Isolated validator/rollback checks. Generated test tones never enter the game.

The artificial manifest deliberately models a supplied declaration. A passing
test checks parser and transaction behaviour, not actual rights or performance.
"""

import copy
import importlib.util
import io
import json
import math
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import wave

sys.dont_write_bytecode = True
SPEC = importlib.util.spec_from_file_location("radio_delivery", Path(__file__).with_name("apply-act1-radio-recordings.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def tone(seconds=1, channels=1, level=.12):
    output = io.BytesIO()
    with wave.open(output, "wb") as stream:
        stream.setnchannels(channels)
        stream.setsampwidth(2)
        stream.setframerate(44100)
        samples = bytearray()
        for frame in range(round(seconds * 44100)):
            samples.extend(struct.pack("<h", round(math.sin(frame * 2 * math.pi * 440 / 44100) * level * 32767)) * channels)
        stream.writeframes(samples)
    return output.getvalue()


class RadioDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="urman-radio-validator-fixtures-")
        self.addCleanup(self.temporary.cleanup)
        root = Path(self.temporary.name).resolve()
        self.project, self.delivery = root / "project-fixture", root / "delivery-fixture"
        self.delivery.mkdir()
        (self.project / MODULE.STATION.parent).mkdir(parents=True)
        self.station = {"schemaVersion": 1, "stationId": "avyl-fm", "languageReview": "pending", "segments": [
            {"id": "spoken", "kind": "ident", "language": "tt", "transcript": "Сынау.",
             "durationSeconds": 10, "rightsStatus": "pending-recording"},
            {"id": "music-one", "kind": "music", "language": "zxx", "transcript": "Not supplied.",
             "durationSeconds": 150, "rightsStatus": "pending-recording"}]}
        (self.project / MODULE.STATION).write_bytes(MODULE.encoded(self.station))
        wav = tone()
        evidence = b"TEST ONLY artificial permission declaration; not a real performance or music source."
        (self.delivery / "prepared.wav").write_bytes(wav)
        (self.delivery / "original.wav").write_bytes(wav)
        (self.delivery / "rights.txt").write_bytes(evidence)
        self.record = {"segmentId": "spoken", "origin": "human-recording", "file": "prepared.wav",
            "sha256": MODULE.digest(wav), "sourceFile": "original.wav", "sourceSha256": MODULE.digest(wav),
            "sourceURL": "https://example.invalid/explicit-test-fixture", "durationSeconds": 1,
            "language": "tt", "transcript": "Сынау.", "performer": "TEST ONLY", "credit": "TEST ONLY",
            "processing": ["test-only generated waveform, never production delivery"], "rights": [{
                "covers": ["recording", "performance"], "licenseId": "CC0-1.0",
                "licenseURL": MODULE.LICENSES["CC0-1.0"], "copyrightHolder": "TEST ONLY",
                "sourceURL": "https://example.invalid/test-declaration", "evidenceFile": "rights.txt",
                "evidenceSha256": MODULE.digest(evidence)}]}
        self.manifest = {"schemaVersion": 1, "stationId": "avyl-fm", "recordings": [self.record]}
        self.write_manifest()

    def write_manifest(self):
        (self.delivery / "provenance.json").write_bytes(MODULE.encoded(self.manifest))

    def prepare(self, complete=False):
        self.write_manifest()
        return MODULE.prepare_delivery(self.delivery, self.project, complete)

    def test_partial_install_is_idempotent_preserves_ids_and_source(self):
        before = (self.project / MODULE.STATION).read_bytes()
        plan = self.prepare()
        self.assertEqual(before, (self.project / MODULE.STATION).read_bytes())
        self.assertEqual(plan["report"]["remainingSegmentIds"], ["music-one"])
        MODULE.apply_delivery(plan)
        station = json.loads((self.project / MODULE.STATION).read_bytes())
        self.assertEqual([row["id"] for row in station["segments"]], ["spoken", "music-one"])
        self.assertEqual(station["segments"][0]["durationSeconds"], 1)
        self.assertEqual(station["languageReview"], "pending")
        self.assertTrue((self.project / plan["archive"] / "original.wav").is_file())
        installed = {str(path.relative_to(self.project)): path.read_bytes() for path in self.project.rglob("*") if path.is_file()}
        MODULE.apply_delivery(self.prepare())
        self.assertEqual(installed, {str(path.relative_to(self.project)): path.read_bytes() for path in self.project.rglob("*") if path.is_file()})

    def test_partial_cannot_be_reported_complete(self):
        with self.assertRaisesRegex(MODULE.DeliveryError, "missing recordings"):
            self.prepare(complete=True)

    def test_explicit_recording_replaces_preview_metadata_without_losing_history(self):
        row = self.station["segments"][0]
        row.update(rightsStatus="licensed-synthetic-preview", origin="synthetic-preview",
                   acceptance="preview-pending-listening-and-language-review", modelLicense="MIT",
                   authorNote="preserve this authored field")
        (self.project / MODULE.STATION).write_bytes(MODULE.encoded(self.station))
        old_source = self.project / "assets/source/audio/preview-history.json"
        old_source.parent.mkdir(parents=True)
        old_source.write_bytes(b'{"origin":"synthetic-preview","testOnly":true}\n')
        previous_history = old_source.read_bytes()
        MODULE.apply_delivery(self.prepare())
        installed = (self.project / MODULE.STATION).read_bytes()
        actual = json.loads(installed)["segments"][0]
        self.assertEqual(actual["id"], "spoken")
        self.assertEqual(actual["origin"], "human-recording")
        self.assertEqual(actual["acceptance"], "recording-delivered; listening-and-language-review-pending")
        self.assertNotIn("modelLicense", actual)
        self.assertNotIn("repeatsSegmentId", actual)
        self.assertEqual(actual["authorNote"], "preserve this authored field")
        MODULE.apply_delivery(self.prepare())
        self.assertEqual((self.project / MODULE.STATION).read_bytes(), installed)
        self.assertEqual(old_source.read_bytes(), previous_history)

    def test_explicit_music_delivery_replaces_repeat_identity(self):
        row = self.station["segments"][1]
        row.update(rightsStatus="licensed-recording-repeat", origin="recorded-music-repeat",
                   acceptance="reuses-existing-approved-recording", repeatsSegmentId="prior-recording",
                   authorNote="keep programme placement")
        (self.project / MODULE.STATION).write_bytes(MODULE.encoded(self.station))
        wav = tone(seconds=10)
        for name in ("original.wav", "prepared.wav"):
            (self.delivery / name).write_bytes(wav)
        self.record.update(segmentId="music-one", origin="recorded-music", language="zxx",
                           durationSeconds=10, sha256=MODULE.digest(wav), sourceSha256=MODULE.digest(wav),
                           instrumental=True, title="TEST ONLY independent recording", transcript="TEST ONLY instrumental.")
        self.record["rights"][0]["covers"] += ["composition", "arrangement"]
        MODULE.apply_delivery(self.prepare())
        installed = (self.project / MODULE.STATION).read_bytes()
        actual = json.loads(installed)["segments"][1]
        self.assertEqual(actual["origin"], "recorded-music")
        self.assertNotIn("repeatsSegmentId", actual)
        self.assertEqual(actual["authorNote"], "keep programme placement")
        self.assertEqual(actual["streamPath"], "res://assets/audio/act1/radio/music-one.wav")
        MODULE.apply_delivery(self.prepare())
        self.assertEqual((self.project / MODULE.STATION).read_bytes(), installed)

    def test_old_approved_flag_cannot_hide_missing_or_mutated_audio(self):
        row = self.station["segments"][1]
        row.update(rightsStatus="approved", streamPath="res://assets/audio/act1/radio/music-one.wav",
                   sha256="0" * 64, durationSeconds=10, provenance="assets/source/audio/act1/radio/absent/provenance.json#music-one")
        (self.project / MODULE.STATION).write_bytes(MODULE.encoded(self.station))
        with self.assertRaisesRegex(MODULE.DeliveryError, "existing approved recording is absent"):
            self.prepare(complete=True)
        runtime = self.project / MODULE.RUNTIME / "music-one.wav"
        runtime.parent.mkdir(parents=True)
        runtime.write_bytes(tone(seconds=10))
        with self.assertRaisesRegex(MODULE.DeliveryError, "existing approved recording hash differs"):
            self.prepare(complete=True)
        row["sha256"] = MODULE.digest(runtime.read_bytes())
        (self.project / MODULE.STATION).write_bytes(MODULE.encoded(self.station))
        with self.assertRaisesRegex(MODULE.DeliveryError, "provenance is absent"):
            self.prepare(complete=True)

    def test_mutated_sample_and_evidence_hashes_are_rejected(self):
        for filename in ("prepared.wav", "rights.txt"):
            with self.subTest(filename=filename):
                path = self.delivery / filename
                before = path.read_bytes()
                path.write_bytes(before + b"changed")
                with self.assertRaisesRegex(MODULE.DeliveryError, "hash differs"):
                    self.prepare()
                path.write_bytes(before)

    def test_score_licence_cannot_approve_a_recording(self):
        self.record["rights"][0]["covers"] = ["composition", "lyrics"]
        with self.assertRaisesRegex(MODULE.DeliveryError, "performance, recording"):
            self.prepare()

    def test_synthesis_is_not_accepted_as_a_performed_recording(self):
        self.record["origin"] = "licensed-synthetic-speech"
        with self.assertRaisesRegex(MODULE.DeliveryError, "fixtures and synthesis"):
            self.prepare()

    def test_path_escape_and_unsupported_licence_are_rejected(self):
        original = self.record["file"]
        self.record["file"] = "../outside.wav"
        with self.assertRaisesRegex(MODULE.DeliveryError, "escapes"):
            self.prepare()
        self.record["file"] = original
        self.record["rights"][0]["licenseId"] = "CC-BY-NC-4.0"
        with self.assertRaisesRegex(MODULE.DeliveryError, "unsupported"):
            self.prepare()

    def test_transcript_and_duration_must_match(self):
        self.record["transcript"] = "Different recorded words."
        with self.assertRaisesRegex(MODULE.DeliveryError, "spoken transcript"):
            self.prepare()
        self.record["transcript"] = "Сынау."
        self.record["durationSeconds"] = 1.2
        with self.assertRaisesRegex(MODULE.DeliveryError, "decoded sample count"):
            self.prepare()

    def test_real_audio_container_is_required(self):
        for data, reason in ((b"<html>not sound</html>", "RIFF/WAVE"),
                             (tone(level=0), "silent"), (tone(level=.99), "peak"),
                             (tone()[:-1], "RIFF length")):
            with self.subTest(reason=reason):
                with self.assertRaisesRegex(MODULE.DeliveryError, reason):
                    MODULE.inspect_wav(data, "ident")

    def test_duplicate_keys_are_rejected(self):
        with self.assertRaisesRegex(MODULE.DeliveryError, "duplicate JSON key"):
            MODULE.strict_json(b'{"rightsStatus":"pending","rightsStatus":"approved"}')

    def test_extensible_pcm_is_decoded_but_float_subtype_is_rejected(self):
        original = tone()
        fmt = original[20:36]
        extension = struct.pack("<HHI", 22, 16, 4) + bytes.fromhex("0100000000001000800000aa00389b71")
        extensible_fmt = struct.pack("<H", 65534) + fmt[2:] + extension
        body = b"WAVEfmt " + struct.pack("<I", 40) + extensible_fmt + original[36:]
        extensible = b"RIFF" + struct.pack("<I", len(body)) + body
        self.assertEqual(MODULE.inspect_wav(extensible, "ident")["frames"], 44100)
        changed = bytearray(extensible)
        changed[44] = 3
        with self.assertRaisesRegex(MODULE.DeliveryError, "subtype"):
            MODULE.inspect_wav(bytes(changed), "ident")

    def test_changed_station_is_preserved(self):
        plan = self.prepare()
        new_bytes = (self.project / MODULE.STATION).read_bytes() + b" "
        (self.project / MODULE.STATION).write_bytes(new_bytes)
        with self.assertRaisesRegex(MODULE.DeliveryError, "station changed"):
            MODULE.apply_delivery(plan)
        self.assertEqual((self.project / MODULE.STATION).read_bytes(), new_bytes)
        self.assertFalse((self.project / MODULE.RUNTIME).exists())

    def test_preparation_preserves_signed_samples_and_exact_timeline(self):
        source = self.delivery / "decode-fixture.wav"
        destination = self.delivery / "prepared-fixture.wav"
        source.write_bytes(tone(seconds=10, channels=2, level=.9))
        result = subprocess.run([sys.executable, "-B", str(Path(__file__).with_name("prepare-radio-wav.py")),
                                 str(source), str(destination), "--peak-db", "-3"],
                                capture_output=True, text=True, check=False)
        self.assertEqual(result.returncode, 0, result.stderr)
        original_fmt, original_pcm = MODULE.read_wav_pcm(source.read_bytes())
        prepared_fmt, prepared_pcm = MODULE.read_wav_pcm(destination.read_bytes())
        self.assertEqual(original_fmt, prepared_fmt)
        self.assertEqual(len(original_pcm), len(prepared_pcm))
        processing = json.loads(destination.with_suffix(".wav.processing.json").read_bytes())
        gain = processing["linearGain"]
        for offset in (2, 400, 4002, 40004):
            expected = round(struct.unpack_from("<h", original_pcm, offset)[0] * gain)
            self.assertEqual(struct.unpack_from("<h", prepared_pcm, offset)[0], expected)
        self.assertAlmostEqual(processing["audio"]["peakDbfs"], -3, places=3)
        self.assertEqual(processing["audio"]["frames"], 441000)

    def test_failed_install_restores_previous_files_and_modes(self):
        target = self.project / MODULE.RUNTIME / "spoken.wav"
        target.parent.mkdir(parents=True)
        target.write_bytes(b"previous runtime recording")
        target.chmod(0o640)
        before = (self.project / MODULE.STATION).read_bytes()
        plan = self.prepare()
        actual = MODULE.atomic_write
        failed = False

        def fail_once(path, data, mode=None):
            nonlocal failed
            if path == self.project / MODULE.STATION and not failed:
                failed = True
                raise OSError("intentional transaction fixture")
            actual(path, data, mode)

        with mock.patch.object(MODULE, "atomic_write", side_effect=fail_once):
            with self.assertRaisesRegex(OSError, "intentional"):
                MODULE.apply_delivery(plan)
        self.assertEqual(target.read_bytes(), b"previous runtime recording")
        self.assertEqual(target.stat().st_mode & 0o777, 0o640)
        self.assertEqual((self.project / MODULE.STATION).read_bytes(), before)
        self.assertFalse(any(path.is_file() for path in (self.project / MODULE.SOURCES).rglob("*")))
        self.assertFalse((self.project / MODULE.STATION.parent / ".radio-install.lock").exists())


if __name__ == "__main__":
    unittest.main(verbosity=2)
