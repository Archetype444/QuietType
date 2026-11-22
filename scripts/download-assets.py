#!/usr/bin/env python3
"""Build-time only. Fetch immutable assets and verify their exact SHA-256."""
import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parent.parent

def main():
    manifest = json.loads((ROOT / 'assets/asset-manifest.json').read_text(encoding='utf-8'))
    for asset in manifest['assets']:
        destination = ROOT / asset['path']
        destination.parent.mkdir(parents=True, exist_ok=True)
        if destination.exists() and digest(destination) == asset['sha256']:
            print('Verified', asset['path'])
            continue
        temporary = destination.with_suffix(destination.suffix + '.download')
        try:
            request = urllib.request.Request(asset['url'], headers={'User-Agent': 'QuietType-build/1.0'})
            with urllib.request.urlopen(request, timeout=120) as source, temporary.open('wb') as output:
                while block := source.read(1024 * 1024):
                    output.write(block)
            if digest(temporary) != asset['sha256']:
                raise ValueError('SHA-256 mismatch: ' + asset['path'])
            temporary.replace(destination)
            print('Downloaded and verified', asset['path'])
        finally:
            temporary.unlink(missing_ok=True)

def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

if __name__ == '__main__':
    main()
