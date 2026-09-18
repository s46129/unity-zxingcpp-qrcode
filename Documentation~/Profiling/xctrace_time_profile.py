"""Summarise an Instruments Time Profiler export of the webcam sample.

Usage:
  xcrun xctrace record --device <udid> --template 'Time Profiler' --time-limit 30s \
      --output run.trace --launch -- <bundle id>
  xcrun xctrace export --input run.trace \
      --xpath '/trace-toc/run[@number="1"]/data/table[@schema="time-profile"]' --output run.xml
  python3 xctrace_time_profile.py run.xml [skip-seconds]

Prints CPU time per thread, the main-thread cost of each step of the frame path,
and the ZXing decode cost on the worker threads. Samples are 1 ms each, so work
that finishes in less than a millisecond can go uncounted.
"""
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter

MAIN_THREAD_PATHS = [
    ("QRCodeSampleRunner.Update (all)", r"QRCodeSampleRunner_Update_"),
    ("  Gray8TextureReadback.TryRequest (blit + request)", r"Gray8TextureReadback_TryRequest_"),
    ("    Graphics.Blit", r"Graphics_Blit|Graphics::Blit|BlitMaterial"),
    ("    AsyncGPUReadback request", r"AsyncGPUReadback_Request|AsyncReadback.*Request"),
    ("  QRCodeScanner.CanAcceptFrame", r"QRCodeScanner_CanAcceptFrame_"),
    ("  AlignWebcamPreview", r"AlignWebcamPreview"),
    ("  UpdateTapToFocus", r"UpdateTapToFocus"),
    ("Readback callback -> OnReadbackFrameReady", r"OnReadbackFrameReady"),
    ("  RentReadbackFrame / CopyFrame (memcpy)", r"RentReadbackFrame|Gray8TextureReadback_CopyFrame"),
    ("  QRCodeScanner.TrySubmitFrame", r"QRCodeScanner_TrySubmitFrame_"),
    ("AsyncGPUReadback update (engine side)", r"AsyncReadback.*Update|AsyncGPUReadback.*Update|GfxAsyncReadback"),
    ("CPU path: RentWebcamFrame / GetPixels32", r"RentWebcamFrame|GetPixels32"),
    ("CPU path: Gray8RowOrder.FlipVertically", r"FlipVertically"),
    ("ZXing on the main thread (expect 0)", r"ZXing"),
    ("UI canvas rebuild / TextMesh Pro", r"CanvasUpdateRegistry|UI::Canvas|TMP_|TextMeshPro"),
    ("PlayerRender", r"PlayerRender"),
]


def load_rows(path):
    root = ET.parse(path).getroot()
    by_id = {}

    def resolve(element):
        if "ref" in element.attrib:
            return by_id[element.attrib["ref"]]
        if "id" in element.attrib:
            by_id[element.attrib["id"]] = element
        for child in element:
            resolve(child)
        return element

    rows = []
    for row in root.iter("row"):
        time = resolve(row.find("sample-time"))
        thread = resolve(row.find("thread"))
        weight = resolve(row.find("weight"))
        frames = []
        tagged = row.find("tagged-backtrace")
        if tagged is not None:
            backtrace = resolve(tagged).find("backtrace")
            if backtrace is not None:
                frames = [resolve(frame).attrib.get("name", "?") for frame in backtrace.findall("frame")]
        rows.append((int(time.text) / 1e9, thread.attrib["fmt"].split(" (")[0], int(weight.text) / 1e6, frames))
    return rows


def inclusive_ms(rows, pattern):
    regex = re.compile(pattern)
    return sum(weight for _, _, weight, frames in rows if any(regex.search(frame) for frame in frames))


def main():
    rows = load_rows(sys.argv[1])
    skip = float(sys.argv[2]) if len(sys.argv) > 2 else 3.0
    start = min(row[0] for row in rows)
    end = max(row[0] for row in rows)
    window = [row for row in rows if row[0] - start >= skip]
    wall = end - start - skip
    print(f"recording {end - start:.1f}s, analysed window {wall:.1f}s (first {skip:.0f}s skipped)")

    per_thread = Counter()
    for _, thread, weight, _ in window:
        per_thread[thread] += weight
    print("\n== CPU ms per thread ==")
    for thread, ms in per_thread.most_common(8):
        print(f"{ms:8.0f} ms  {ms / wall / 10:5.1f}% of one core  {thread}")

    main_rows = [row for row in window if row[1] == "Main Thread"]
    main_ms = sum(row[2] for row in main_rows)
    print(f"\n== Main thread: {main_ms:.0f} ms = {main_ms / wall / 10:.1f}% of one core; inclusive ms per call path ==")
    for label, pattern in MAIN_THREAD_PATHS:
        ms = inclusive_ms(main_rows, pattern)
        print(f"{ms:7.0f} ms  {ms / wall:6.2f} ms per second  {label}")

    decode_rows = [row for row in window if row[1] != "Main Thread" and any("ZXing" in f for f in row[3])]
    decode_ms = sum(row[2] for row in decode_rows)
    bursts = []
    for time, _, weight, _ in sorted(decode_rows):
        if bursts and time - bursts[-1][1] < 0.03:
            bursts[-1][1] = time
            bursts[-1][2] += weight
        else:
            bursts.append([time, time, weight])
    print(f"\n== ZXing decode on worker threads: {decode_ms:.0f} ms in {len(bursts)} sampled decodes, "
          f"avg {decode_ms / max(1, len(bursts)):.1f} ms CPU per sampled decode ==")
    leaves = Counter()
    for _, _, weight, frames in decode_rows:
        leaves[frames[0]] += weight
    for name, ms in leaves.most_common(5):
        print(f"{ms:6.0f} ms  {name[:100]}")


if __name__ == "__main__":
    main()
