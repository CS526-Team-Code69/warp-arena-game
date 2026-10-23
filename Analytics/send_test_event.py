"""Send one edition="test" event to the analytics endpoint to check a deployment.

    python Analytics/send_test_event.py --url "https://script.google.com/macros/s/<DEPLOYMENT_ID>/exec"
    python Analytics/send_test_event.py --url "<web app URL>" --event run_start
    python Analytics/send_test_event.py --dry-run

The event is posted as text/plain JSON, like the game's navigator.sendBeacon call.
Test rows are kept in the sheet but left out of every chart.
"""

import argparse
import json
import secrets
import sys
import urllib.error
import urllib.request

EVENTS = ("run_end", "run_start")
FIELDS = (
    "schema", "event", "run_id", "session_id", "edition", "build", "outcome", "cause",
    "enemy", "elite", "stage", "wave", "run_seconds", "level", "kills", "starting_weapon",
)


def build_event(event="run_end"):
    """Return one valid test event with fresh random ids."""
    if event not in EVENTS:
        raise ValueError("event must be one of %s" % ", ".join(EVENTS))
    payload = {
        "schema": 1,
        "event": event,
        "run_id": secrets.token_hex(16),
        "session_id": secrets.token_hex(16),
        "edition": "test",
        "build": "0.0.0+test",
        "outcome": "",
        "cause": "",
        "enemy": "",
        "elite": False,
        "stage": "wave",
        "wave": 1,
        "run_seconds": 0.0,
        "level": 1,
        "kills": 0,
        "starting_weapon": "Rifle",
    }
    if event == "run_end":
        payload.update({
            "outcome": "defeat",
            "cause": "enemy",
            "enemy": "Grunt",
            "wave": 3,
            "run_seconds": 95.4,
            "level": 4,
            "kills": 57,
        })
    return payload


def build_request(url, payload):
    """POST request carrying the payload as text/plain JSON (no CORS preflight in a browser)."""
    body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    return urllib.request.Request(
        url, data=body, method="POST",
        headers={"Content-Type": "text/plain;charset=utf-8", "User-Agent": "warp-arena-analytics"},
    )


def send(url, payload, timeout=30):
    """Send the event and return (HTTP status, response body).

    Apps Script answers a POST with a 302 to a second URL; urllib follows it with a GET,
    which is what the web app expects.
    """
    try:
        with urllib.request.urlopen(build_request(url, payload), timeout=timeout) as response:
            return response.status, response.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode("utf-8", errors="replace")


def check_reply(status, body):
    """Return (passed, message). Code.gs answers every request with HTTP 200, so the JSON decides."""
    if not 200 <= status < 300:
        return False, "endpoint answered HTTP %d" % status
    try:
        reply = json.loads(body)
    except ValueError:
        return False, "reply was not JSON (check the deployment access is Anyone)"
    if not isinstance(reply, dict):
        return False, "reply was not a JSON object"
    if reply.get("ok") is not True:
        return False, "endpoint rejected the event: %s" % (reply.get("error") or "no error given")
    return True, "endpoint accepted the event"


def main(argv=None):
    parser = argparse.ArgumentParser(description="Send one edition='test' event to the analytics endpoint.")
    parser.add_argument("url_arg", nargs="?", metavar="URL", help="web app URL (same as --url)")
    parser.add_argument("--url", help="web app URL, ending in /exec")
    parser.add_argument("--event", choices=EVENTS, default="run_end", help="event to send (default: run_end)")
    parser.add_argument("--dry-run", action="store_true", help="print the event and send nothing")
    args = parser.parse_args(argv)

    payload = build_event(args.event)
    print(json.dumps(payload, indent=2))
    if args.dry_run:
        return 0

    url = args.url or args.url_arg
    if not url:
        parser.error("give the web app URL with --url")
    try:
        status, body = send(url, payload)
    except OSError as error:  # URLError, timeouts, connection resets
        print("error: %s" % getattr(error, "reason", error), file=sys.stderr)
        return 1
    print("HTTP %d" % status)
    print(body)
    passed, message = check_reply(status, body)
    if not passed:
        print("error: " + message, file=sys.stderr)
        return 1
    print(message)
    return 0


if __name__ == "__main__":
    sys.exit(main())
