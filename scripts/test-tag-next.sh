#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_SCRIPT="${SCRIPT_DIR}/tag-next.sh"

TMP_DIR="$(mktemp -d "${SCRIPT_DIR}/tag-next-test.XXXXXX")"
trap 'rm -rf "${TMP_DIR}"' EXIT

REMOTE_REPO="${TMP_DIR}/remote.git"
WORK_REPO="${TMP_DIR}/work"

git init --bare "${REMOTE_REPO}" >/dev/null

git init -b main "${WORK_REPO}" >/dev/null
cd "${WORK_REPO}"
git remote add origin "${REMOTE_REPO}"
printf 'seed\n' >README.md
git add README.md
git -c user.name='Test User' -c user.email='test@example.com' commit -m "seed repo" >/dev/null
git push origin HEAD:main >/dev/null
git tag v0.1.1
git tag v0.1.2
git push origin v0.1.1 v0.1.2 >/dev/null

"${TARGET_SCRIPT}" >/tmp/tag-next-output.txt

LOCAL_TAGS="$(git tag --list 'v*' --sort=version:refname)"
REMOTE_TAGS="$(git ls-remote --tags --refs origin 'v*' | awk -F/ '{print $3}' | sort -V)"

if ! printf '%s\n' "${LOCAL_TAGS}" | awk '$0 == "v0.1.3" { found = 1 } END { exit(found ? 0 : 1) }'; then
  printf 'expected local tag v0.1.3, got:\n%s\n' "${LOCAL_TAGS}" >&2
  exit 1
fi

if ! printf '%s\n' "${REMOTE_TAGS}" | awk '$0 == "v0.1.3" { found = 1 } END { exit(found ? 0 : 1) }'; then
  printf 'expected remote tag v0.1.3, got:\n%s\n' "${REMOTE_TAGS}" >&2
  exit 1
fi

printf 'ok: created and pushed v0.1.3\n'
