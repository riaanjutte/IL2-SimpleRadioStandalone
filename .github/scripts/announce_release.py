"""Post a published GitHub release to the configured Discord webhook."""

import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path


WEBSITE_URL = "https://srsforil2.com/"
MAX_MESSAGE_LENGTH = 1900


def previous_release_version(release, repository, token):
    headers = {"Accept": "application/vnd.github+json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(
        f"https://api.github.com/repos/{repository}/releases?per_page=100",
        headers=headers,
    )
    with urllib.request.urlopen(request, timeout=20) as response:
        releases = json.load(response)

    candidates = [
        item for item in releases
        if not item.get("draft")
        and item.get("tag_name") != release["tag_name"]
        and item.get("published_at")
        and release.get("published_at")
        and item["published_at"] < release["published_at"]
        and (release.get("prerelease") or not item.get("prerelease"))
    ]
    if not candidates:
        return None
    return max(candidates, key=lambda item: item["published_at"])["tag_name"].removeprefix("v")


def format_announcement(release, previous_version=None):
    title = release.get("name") or f"IL2-SRS {release['tag_name'].removeprefix('v')}"
    heading = f"Changes since {previous_version}" if previous_version else "Changes in this release"
    lines = [f"**{title} is now available**", f"Download: {WEBSITE_URL}", "", heading, ""]
    notes = []
    closing = []
    section = ""
    for raw_line in (release.get("body") or "").splitlines():
        line = raw_line.strip()
        if line.startswith("## ") and line[3:] == title:
            continue
        if line.startswith("### "):
            section = line[4:].lower()
            continue
        if not line:
            continue
        if section in ("compatibility", "updating"):
            closing.append(line.removeprefix("- "))
        elif line.startswith("- "):
            notes.append(line)
        else:
            notes.append(line)

    lines.extend(notes or ["- See the full release notes on GitHub."])
    if closing:
        lines.extend(["", " ".join(closing)])
    lines.extend(["", f"Full release notes: {release['html_url']}"])
    return "\n".join(lines)


def split_messages(message):
    chunks = []
    current = ""
    for line in message.splitlines(keepends=True):
        while len(line) > MAX_MESSAGE_LENGTH:
            split_at = line.rfind(" ", 0, MAX_MESSAGE_LENGTH)
            split_at = split_at if split_at > 0 else MAX_MESSAGE_LENGTH
            if current:
                chunks.append(current.rstrip())
                current = ""
            chunks.append(line[:split_at].rstrip())
            line = line[split_at:].lstrip()
        if len(current) + len(line) > MAX_MESSAGE_LENGTH:
            chunks.append(current.rstrip())
            current = ""
        current += line
    if current.strip():
        chunks.append(current.rstrip())
    return chunks


def post_message(webhook, content):
    payload = json.dumps({"content": content, "allowed_mentions": {"parse": []}}).encode("utf-8")
    request = urllib.request.Request(
        webhook,
        data=payload,
        headers={"Content-Type": "application/json", "User-Agent": "IL2-SRS-Release-Announcer"},
        method="POST",
    )
    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=20) as response:
                if response.status not in (200, 204):
                    raise RuntimeError(f"Discord returned HTTP {response.status}")
                return
        except urllib.error.HTTPError as error:
            if error.code != 429 or attempt == 2:
                raise RuntimeError(f"Discord returned HTTP {error.code}") from None
            retry_after = json.load(error).get("retry_after", 1)
            time.sleep(min(float(retry_after), 10))


def main():
    webhook = os.environ.get("DISCORD_RELEASE_WEBHOOK")
    if not webhook:
        raise RuntimeError("DISCORD_RELEASE_WEBHOOK repository secret is not configured")

    event_path = Path(os.environ["GITHUB_EVENT_PATH"])
    event = json.loads(event_path.read_text(encoding="utf-8"))
    release = event["release"]
    if release.get("draft"):
        raise RuntimeError("Refusing to announce a draft release")

    try:
        previous_version = previous_release_version(
            release, os.environ["GITHUB_REPOSITORY"], os.environ["GITHUB_TOKEN"]
        )
    except (OSError, ValueError, KeyError, TypeError):
        previous_version = None

    for message in split_messages(format_announcement(release, previous_version)):
        post_message(webhook, message)
    print(f"Announced {release['tag_name']} on Discord")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        # An HTTP exception can include the webhook URL, so never log its text.
        print(f"Release announcement failed: {type(exc).__name__}", file=sys.stderr)
        sys.exit(1)
