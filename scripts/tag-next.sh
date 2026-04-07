#!/usr/bin/env bash

set -euo pipefail

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  printf 'error: run this script from inside a git repository\n' >&2
  exit 1
fi

if ! git remote get-url origin >/dev/null 2>&1; then
  printf 'error: git remote "origin" is not configured\n' >&2
  exit 1
fi

git fetch origin --tags --quiet

latest_tag="$(
  git tag --list 'v[0-9]*.[0-9]*.[0-9]*' --sort=-version:refname | sed -n '1p'
)"

if [[ -z "${latest_tag}" ]]; then
  next_tag="v0.1.0"
else
  version="${latest_tag#v}"
  IFS='.' read -r major minor patch <<<"${version}"
  next_patch=$((patch + 1))
  next_tag="v${major}.${minor}.${next_patch}"
fi

if git rev-parse "${next_tag}" >/dev/null 2>&1; then
  printf 'error: tag %s already exists locally\n' "${next_tag}" >&2
  exit 1
fi

git tag "${next_tag}"
git push origin "${next_tag}"

printf 'latest tag: %s\n' "${latest_tag:-<none>}"
printf 'created and pushed: %s\n' "${next_tag}"
