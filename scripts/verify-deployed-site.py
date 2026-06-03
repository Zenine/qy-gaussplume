#!/usr/bin/env python3
"""Verify a deployed Meridian/VitePress GitHub Pages site.

This runs after `actions/deploy-pages` so workflow success means the
published URL is reachable and still contains the SEO/GEO assets that
Meridian promises.
"""

from __future__ import annotations

import argparse
import re
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from typing import Callable
from urllib.parse import urljoin


HTML_PATHS = ("", "quick-start.html", "faq.html", "en/", "ja/", "zh-TW/")
STATIC_PATHS = ("robots.txt", "sitemap.xml", "llms.txt", "llms-full.txt", "og.png")
PLACEHOLDER_RE = re.compile(r"\{\{[^{}\s]+(?:_[^{}\s]+)*\}\}")


def join_url(base_url: str, path: str) -> str:
    base = base_url.rstrip("/") + "/"
    return urljoin(base, path.lstrip("/"))


class Fail:
    def __init__(self) -> None:
        self.errors: list[str] = []

    def add(self, message: str) -> None:
        self.errors.append(message)

    def report(self) -> int:
        if not self.errors:
            print("\n\033[32m[PASS]\033[0m deployed site checks passed")
            return 0
        print(f"\n\033[31m[FAIL]\033[0m {len(self.errors)} deployed site issue(s):")
        for error in self.errors:
            print(f"  - {error}")
        return 1


@dataclass
class Response:
    url: str
    status: int
    body: bytes
    content_type: str

    @property
    def text(self) -> str:
        return self.body.decode("utf-8", errors="replace")


def default_fetcher(url: str, timeout: int = 10):
    req = urllib.request.Request(url, headers={"User-Agent": "meridian-post-deploy-check/1.0"})
    return urllib.request.urlopen(req, timeout=timeout)


class DeployedSiteVerifier:
    def __init__(
        self,
        base_url: str,
        fetcher: Callable[[str, int], object] = default_fetcher,
        retries: int = 3,
        retry_delay: float = 1.0,
    ) -> None:
        self.base_url = base_url.rstrip("/") + "/"
        self.fetcher = fetcher
        self.retries = retries
        self.retry_delay = retry_delay

    def run(self, fail: Fail) -> None:
        responses: dict[str, Response] = {}
        for path in (*HTML_PATHS, *STATIC_PATHS):
            response = self.fetch(path, fail)
            if response is not None:
                responses[path] = response

        root = responses.get("")
        if root:
            self.check_root_metadata(root, fail)
        for path in HTML_PATHS:
            response = responses.get(path)
            if response:
                self.check_html_page(path or "/", response, fail)
        for path in STATIC_PATHS:
            response = responses.get(path)
            if response:
                self.check_static_asset(path, response, fail)

    def fetch(self, path: str, fail: Fail) -> Response | None:
        url = join_url(self.base_url, path)
        last_error = ""
        for attempt in range(1, self.retries + 1):
            try:
                with self.fetcher(url, 10) as raw:
                    status = getattr(raw, "status", 200)
                    body = raw.read()
                    content_type = self.header(raw, "content-type")
                    if status != 200:
                        fail.add(f"{url} returned HTTP {status}")
                    return Response(url=url, status=status, body=body, content_type=content_type)
            except urllib.error.HTTPError as exc:
                fail.add(f"{url} returned HTTP {exc.code}")
                return None
            except Exception as exc:  # network flake, DNS, TLS, timeout
                last_error = str(exc)
                if attempt < self.retries:
                    time.sleep(self.retry_delay)
        fail.add(f"{url} could not be fetched after {self.retries} attempts: {last_error}")
        return None

    @staticmethod
    def header(raw: object, name: str) -> str:
        headers = getattr(raw, "headers", {})
        if hasattr(headers, "get"):
            return headers.get(name, headers.get(name.title(), "")) or ""
        return ""

    def check_root_metadata(self, response: Response, fail: Fail) -> None:
        html = response.text
        required = [
            ("og:title", r'property=["\']og:title["\']'),
            ("twitter:card", r'name=["\']twitter:card["\']'),
            ("canonical", r'rel=["\']canonical["\']'),
            ("JSON-LD", r'application/ld\+json'),
            ("llms.txt discovery", r'title=["\']llms\.txt["\']'),
        ]
        for label, pattern in required:
            if not re.search(pattern, html):
                fail.add(f"{response.url} missing {label}")

    def check_html_page(self, label: str, response: Response, fail: Fail) -> None:
        text = response.text
        if response.status != 200:
            return
        if "<html" not in text.lower():
            fail.add(f"{response.url} does not look like an HTML document")
        if PLACEHOLDER_RE.search(text):
            fail.add(f"{response.url} contains unreplaced template placeholder")

    def check_static_asset(self, path: str, response: Response, fail: Fail) -> None:
        text = response.text
        if response.status != 200:
            return
        if path == "robots.txt" and "sitemap" not in text.lower():
            fail.add(f"{response.url} robots.txt does not reference sitemap.xml")
        if path == "sitemap.xml" and "<urlset" not in text:
            fail.add(f"{response.url} does not look like a sitemap")
        if path == "llms.txt" and not text.strip().startswith("#"):
            fail.add(f"{response.url} llms.txt should start with a Markdown heading")
        if path == "llms-full.txt" and len(text.strip()) < 500:
            fail.add(f"{response.url} llms-full.txt is unexpectedly short")
        if path == "og.png" and "image" not in response.content_type.lower():
            fail.add(f"{response.url} should be served as an image, got {response.content_type!r}")


def run() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("base_url", help="Published Pages URL from actions/deploy-pages")
    args = parser.parse_args()

    fail = Fail()
    DeployedSiteVerifier(args.base_url).run(fail)
    return fail.report()


if __name__ == "__main__":
    sys.exit(run())
