"""Checks the frontmatter of every .claude/skills/*/SKILL.md: valid YAML, a name equal to the folder, a description.
An invalid frontmatter (an unquoted ": " in the description, for example) keeps Claude Code from loading the skill properly.
Usage: python3 tools/ci/check_skills.py   (needs PyYAML: apt install python3-yaml)"""
import pathlib
import sys

import yaml

errors = 0
skills = sorted(pathlib.Path(".claude/skills").glob("*/SKILL.md"))
for path in skills:
    text = path.read_text(encoding="utf-8")
    parts = text.split("---", 2)
    if not text.startswith("---") or len(parts) < 3:
        print(f"{path}: no frontmatter between --- lines")
        errors += 1
        continue
    try:
        meta = yaml.safe_load(parts[1]) or {}
    except yaml.YAMLError as e:
        print(f"{path}: frontmatter is not valid YAML: {e}".replace("\n", " "))
        errors += 1
        continue
    if meta.get("name") != path.parent.name:
        print(f"{path}: name {meta.get('name')!r} differs from the folder {path.parent.name!r}")
        errors += 1
    if not str(meta.get("description") or "").strip():
        print(f"{path}: description missing")
        errors += 1
print(f"{len(skills)} skills, {errors} problem(s)")
sys.exit(1 if errors or not skills else 0)
