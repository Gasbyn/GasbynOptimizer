# Gasbyn Optimizer v2.0.B9

## Fix

- Fixed PresentMon installation failure caused by keeping the downloaded EXE open while attempting to move it into place. The SHA-256 verification stream is now disposed before file installation begins.

## Security

- The installer still downloads from the official PresentMon GitHub release and verifies the published SHA-256 digest before installing the executable.
