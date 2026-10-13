# Gasbyn Optimizer v2.0.B8

## Fixes

- Retry update ZIP downloads when GitHub temporarily returns HTTP 404, 429, or 5xx responses.
- If GitHub still returns 404 after retries, show a clearer message and open the release page for inspection.
- Publish the release ZIP once from the release-branch workflow instead of running a second upload workflow after publication, reducing the risk of a stale or temporarily unavailable asset URL.

## Notes

- The updater continues to validate the downloaded ZIP using GitHub's published SHA-256 digest before applying it.
- This release has been build-validated by GitHub Actions; the in-app update flow should still be confirmed on a real B6 installation.
