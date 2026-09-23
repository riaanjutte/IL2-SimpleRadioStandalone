import unittest

from announce_release import format_announcement, split_messages


class ReleaseAnnouncementTests(unittest.TestCase):
    def test_stable_release_matches_the_existing_plain_message_style(self):
        release = {
            "name": "IL2-SRS 1.0.4.12",
            "tag_name": "v1.0.4.12",
            "html_url": "https://github.com/example/releases/tag/v1.0.4.12",
            "body": "## IL2-SRS 1.0.4.12\n\n### Fixed\n\n- Audio recovery",
            "prerelease": False,
            "assets": [{"name": "IL2-SRS-AutoUpdater.exe", "browser_download_url": "https://example/updater.exe"}],
        }
        message = format_announcement(release, "1.0.4.11")

        self.assertIn("**IL2-SRS 1.0.4.12 is now available**", message)
        self.assertIn("Download: https://srsforil2.com/", message)
        self.assertIn("Changes since 1.0.4.11", message)
        self.assertIn("- Audio recovery", message)
        self.assertNotIn("### Fixed", message)
        self.assertIn(release["html_url"], message)

    def test_beta_uses_fallback_changes_heading(self):
        release = {
            "tag_name": "v1.0.4.13-beta.1",
            "html_url": "https://github.com/example/releases/tag/v1.0.4.13-beta.1",
            "prerelease": True,
        }
        message = format_announcement(release)

        self.assertIn("Changes in this release", message)
        self.assertIn(release["html_url"], message)

    def test_long_release_notes_are_split_without_losing_text(self):
        release = {
            "tag_name": "v1.0.4.13",
            "html_url": "https://github.com/example/releases/tag/v1.0.4.13",
            "body": "A long note\n" * 1000,
        }
        message = format_announcement(release)
        chunks = split_messages(message)

        self.assertGreater(len(chunks), 1)
        self.assertTrue(all(len(chunk) <= 2000 for chunk in chunks))
        self.assertEqual(message.split(), " ".join(chunks).split())


if __name__ == "__main__":
    unittest.main()
