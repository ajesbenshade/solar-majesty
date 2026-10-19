"""
Keeps the string tables in Assets/Resources/Locale in sync with the code.

    python Tools/loc/extract.py              # update en.json and every translation, print a summary
    python Tools/loc/extract.py --report     # also write Docs/LOCALIZATION_STATUS.md
    python Tools/loc/extract.py --new fr Français   # start a table for a new language

What it does
  * Collects every literal passed to Loc.S("..."), Loc.F("...", ...) and Loc.T("key", "...") in
    Assets/Scripts/Runtime and Assets/Scripts/Systems (tests and editor code are skipped).
  * Writes en.json, the source catalog (translation = source).
  * Merges into every other <code>.json: new strings are added with an empty translation,
    existing translations are kept, and strings no longer in the code move to "obsolete"
    so a translator's work is never silently deleted.
  * --report also counts string literals that look player-facing but are not wrapped yet,
    per file, so migration progress can be measured.

The JSON layout matches Loc.cs (JsonUtility): {"code","name","strings":[{"en","key","tr"}],"obsolete":[...]}.
"""
import argparse
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCRIPTS = os.path.join(ROOT, "Assets", "Scripts")
SCAN_DIRS = [os.path.join(SCRIPTS, "Runtime"), os.path.join(SCRIPTS, "Systems")]
LOCALE = os.path.join(ROOT, "Assets", "Resources", "Locale")
REPORT = os.path.join(ROOT, "Docs", "LOCALIZATION_STATUS.md")

# A C# regular string literal: "..." with escapes.
LIT = r'"((?:[^"\\\n]|\\.)*)"'
CALL_S = re.compile(r'\bLoc\.(S|F)\(\s*' + LIT)
CALL_T = re.compile(r'\bLoc\.T\(\s*' + LIT + r'\s*,\s*' + LIT)
ANY_LIT = re.compile(r'(?<![@$\w])' + LIT)

ESCAPES = {'n': '\n', 't': '\t', '"': '"', '\\': '\\', "'": "'", '0': '\0', 'r': '\r'}


def unescape(s):
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c == '\\' and i + 1 < len(s):
            nxt = s[i + 1]
            if nxt == 'u' and i + 5 < len(s):
                out.append(chr(int(s[i + 2:i + 6], 16)))
                i += 6
                continue
            out.append(ESCAPES.get(nxt, nxt))
            i += 2
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def source_files():
    for d in SCAN_DIRS:
        for base, dirs, files in os.walk(d):
            if os.sep + "Tests" in base or os.sep + "Editor" in base:
                continue
            for f in files:
                if f.endswith(".cs"):
                    yield os.path.join(base, f)


def strip_comments(code):
    # Drop // and /* */ comments but keep string contents intact.
    out, i, n = [], 0, len(code)
    while i < n:
        if code.startswith("//", i):
            j = code.find("\n", i)
            i = n if j < 0 else j
        elif code.startswith("/*", i):
            j = code.find("*/", i + 2)
            i = n if j < 0 else j + 2
        elif code[i] == '"':
            j = i + 1
            while j < n and code[j] != '"':
                j += 2 if code[j] == '\\' else 1
            out.append(code[i:j + 1])
            i = j + 1
        else:
            out.append(code[i])
            i += 1
    return ''.join(out)


def collect():
    sources, keyed, where = {}, {}, {}
    for path in source_files():
        code = strip_comments(open(path, encoding="utf-8").read())
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        for m in CALL_S.finditer(code):
            text = unescape(m.group(2))
            if text.strip():
                sources.setdefault(text, rel)
        for m in CALL_T.finditer(code):
            keyed.setdefault(unescape(m.group(1)), unescape(m.group(2)))
            where.setdefault(unescape(m.group(1)), rel)
    return sources, keyed


# ---- unwrapped report ------------------------------------------------------------------------------

# Literals that are clearly not player-facing.
NOT_UI = re.compile(
    r'(Debug\.Log\w*|LogWarning|LogError|Shader\.Find|Resources\.Load\w*|GameObject\.Find|\.Find\(|'
    r'CompareTag|SetFloat|SetColor|SetInt|GetFloat|GetColor|HasProperty|PropertyToID|Tooltip|Header|'
    r'MenuItem|CreateAssetMenu|new GameObject|\.name\b|name =|Contains\(|StartsWith\(|EndsWith\(|IndexOf\(|'
    r'GamePrefs\.|PlayerPrefs\.|Key\b|key\b|Arg\w*\(|Path\.|\.json|\.png|SM_)')


def looks_player_facing(text):
    if len(text) < 3 or not re.search(r'[A-Za-z]{2}', text):
        return False
    if re.fullmatch(r'[\w./\-:]+', text) and ' ' not in text and not text.isupper():
        return False  # identifiers, paths, keys
    if text.startswith(('_', '#', 'SM_', 'Dress_', 'Bld_', 'Universal Render', 'Hidden/')):
        return False
    return True


def unwrapped_report():
    rows = []
    for path in source_files():
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        count = 0
        for line in strip_comments(open(path, encoding="utf-8").read()).splitlines():
            if "Loc." in line or NOT_UI.search(line):
                continue
            for m in ANY_LIT.finditer(line):
                if looks_player_facing(unescape(m.group(1))):
                    count += 1
        if count:
            rows.append((count, rel))
    rows.sort(reverse=True)
    return rows


# ---- tables -------------------------------------------------------------------------------------------

def load(path):
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
        f.write("\n")


def build_entries(sources, keyed, old):
    tr_src, tr_key = {}, {}
    if old:
        for e in old.get("strings", []) + old.get("obsolete", []):
            if e.get("en"):
                tr_src[e["en"]] = e.get("tr", "")
            elif e.get("key"):
                tr_key[e["key"]] = e.get("tr", "")
    entries = [{"en": s, "key": "", "tr": tr_src.get(s, "")} for s in sorted(sources)]
    entries += [{"en": "", "key": k, "tr": tr_key.get(k, ""), "note": keyed[k]} for k in sorted(keyed)]
    live_src, live_key = set(sources), set(keyed)
    obsolete = [{"en": s, "key": "", "tr": t} for s, t in tr_src.items() if s not in live_src and t]
    obsolete += [{"en": "", "key": k, "tr": t} for k, t in tr_key.items() if k not in live_key and t]
    return entries, obsolete


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--new", nargs=2, metavar=("CODE", "NAME"))
    args = ap.parse_args()

    sources, keyed = collect()

    # English source catalog: translation = source, so it doubles as a reference file.
    en = {"code": "en", "name": "English",
          "strings": [{"en": s, "key": "", "tr": s} for s in sorted(sources)] +
                     [{"en": "", "key": k, "tr": keyed[k]} for k in sorted(keyed)]}
    save(os.path.join(LOCALE, "en.json"), en)

    if args.new:
        code, name = args.new
        path = os.path.join(LOCALE, code + ".json")
        if not os.path.exists(path):
            save(path, {"code": code, "name": name, "strings": [], "obsolete": []})

    summary = []
    for f in sorted(os.listdir(LOCALE)) if os.path.isdir(LOCALE) else []:
        if not f.endswith(".json") or f == "en.json":
            continue
        path = os.path.join(LOCALE, f)
        old = load(path)
        entries, obsolete = build_entries(sources, keyed, old)
        data = {"code": old.get("code", f[:-5]), "name": old.get("name", f[:-5]),
                "strings": entries, "obsolete": obsolete}
        save(path, data)
        done = sum(1 for e in entries if e["tr"])
        summary.append((data["code"], data["name"], done, len(entries), len(obsolete)))

    total = len(sources) + len(keyed)
    print(f"[loc] {total} strings in code ({len(sources)} source, {len(keyed)} keyed) -> en.json")
    for code, name, done, n, obs in summary:
        pct = 100.0 * done / n if n else 0.0
        print(f"[loc] {code} ({name}): {done}/{n} translated ({pct:.0f}%), {obs} obsolete")

    if args.report:
        rows = unwrapped_report()
        unwrapped = sum(c for c, _ in rows)
        lines = [
            "# Localization status",
            "",
            "Generated by `python Tools/loc/extract.py --report`. Do not edit by hand.",
            "",
            f"- **Wrapped strings** (in the tables): {total}",
            f"- **Likely player-facing literals not wrapped yet**: {unwrapped} (heuristic; includes some false positives)",
            "",
            "| Languages | Translated |",
            "|---|---|",
        ]
        lines += [f"| {name} (`{code}`) | {done}/{n} |" for code, name, done, n, _ in summary] or ["| (none yet) | |"]
        lines += ["", "## Unwrapped literals by file", "", "| File | Count |", "|---|---|"]
        lines += [f"| `{rel}` | {c} |" for c, rel in rows]
        os.makedirs(os.path.dirname(REPORT), exist_ok=True)
        with open(REPORT, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lines) + "\n")
        print(f"[loc] {unwrapped} likely unwrapped literals; report -> {os.path.relpath(REPORT, ROOT)}")


if __name__ == "__main__":
    sys.exit(main())
