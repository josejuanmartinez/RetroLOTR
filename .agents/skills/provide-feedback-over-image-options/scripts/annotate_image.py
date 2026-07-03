"""Open a copy of an image in MS Paint for the user to annotate, then wait.

Used by the provide-feedback-over-image-options skill. The script copies the
source image to %TEMP% (the original asset is never modified), launches the
editor on the copy, and blocks until the user saves and closes. It then prints
`ANNOTATED: <path>` (saved) or `UNCHANGED: <path>` (closed without saving) so
the caller can read the annotated copy back.
"""

import argparse
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

# Windows 11 Paint is a Store app: launching "mspaint" may return immediately
# after handing off to the packaged process, so we also poll running processes.
PAINT_IMAGE_NAMES = {"mspaint.exe", "paintapp.exe", "paint.exe"}
POLL_SECONDS = 2.0


def paint_process_running() -> bool:
    result = subprocess.run(["tasklist", "/fo", "csv", "/nh"], capture_output=True, text=True)
    for line in result.stdout.splitlines():
        name = line.split('","')[0].strip('"').lower()
        if name in PAINT_IMAGE_NAMES:
            return True
    return False


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Open a temp copy of an image in an editor and wait for the user to save and close."
    )
    parser.add_argument("image", help="Image to collect feedback on (a temp copy is edited, never this file)")
    parser.add_argument("--editor", default="mspaint", help="Editor executable (default: mspaint)")
    parser.add_argument("--timeout", type=float, default=1800,
                        help="Max seconds to wait for the editor session (default: 1800)")
    parser.add_argument("--out", default="", help="Path for the editable copy (default: temp file in %%TEMP%%)")
    args = parser.parse_args()

    src = Path(args.image)
    if not src.is_file():
        print(f"ERROR: not a file: {args.image}", file=sys.stderr)
        return 1

    if args.out:
        work = Path(args.out)
    else:
        work = Path(os.environ.get("TEMP", "/tmp")) / f"feedback_{time.strftime('%Y%m%d_%H%M%S')}_{src.name}"
    work.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, work)  # copy2 preserves the source mtime, so any save is detectable
    baseline_mtime = work.stat().st_mtime

    print(f"Source: {src}")
    print(f"Editing copy: {work}")
    print("Waiting for the user to save and close the editor...", flush=True)

    started = time.monotonic()

    def timed_out() -> bool:
        return time.monotonic() - started > args.timeout

    proc = subprocess.Popen([args.editor, str(work)])
    while proc.poll() is None and not timed_out():
        time.sleep(POLL_SECONDS)

    if not timed_out():
        # Store-app handoff: the launcher exits at once while the real Paint
        # process lives on. Keep waiting until no Paint process remains.
        time.sleep(POLL_SECONDS)
        while paint_process_running() and not timed_out():
            time.sleep(POLL_SECONDS)

    saved = work.stat().st_mtime > baseline_mtime
    if timed_out() and not saved:
        print(f"TIMEOUT: no save detected after {args.timeout:.0f}s ({work})", file=sys.stderr)
        return 3
    if saved:
        print(f"ANNOTATED: {work}")
        return 0
    print(f"UNCHANGED: {work} (editor closed without saving)")
    return 2


if __name__ == "__main__":
    sys.exit(main())
