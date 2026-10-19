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
  * Also collects English that has to stay unwrapped in the code (see "Catalog rules" below).
  * --report also counts string literals that look player-facing but are not wrapped yet,
    per file, so migration progress can be measured. Literals collected by a catalog rule count
    as wrapped.

Catalog rules
  Some English cannot sit inside a Loc call where it is written: static catalog data built once
  (before the player picks a language), constants that are also ids or test anchors, and lines
  that are voice-keyed by their exact English text (baked clips, Tools/audio/render_voices.py).
  That English stays as it is in the code, the type exposes a translated accessor
  (e.g. TechDef.DisplayNameText => Loc.S(DisplayName)), and the extractor finds the literals by
  where they sit. Every rule is scoped to named files (paths relative to Assets/Scripts), so a
  field called Name elsewhere is never swept up:
  * FIELD_RULES   literal assigned to a listed field: `Title = "..."`, `s.displayName = "..."`.
  * CALL_RULES    literal positional argument of a listed constructor/helper call, by index
                  (0-based; "3+" = that index and every later one, for params arrays).
  * DICT_RULES    value of a `{ Key, "..." }` collection-initializer entry.
  * RETURN_RULES  literal returned (`return "..."` or `=> "..."`) inside a listed method;
                  "*" = anywhere in the file.
  * CONST_RULES   `const string` whose name matches the pattern (voice-keyed Jex lines).
  * Any `const string X = "..."` whose name is passed to Loc.S / Loc.F anywhere
    (`Loc.S(X)`, `Loc.S(Type.X)`) is collected too.
  NOT_SHOWN lists designer-note fields that never reach the screen; the report skips them.

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


# ---- catalog rules (see the module docstring) ------------------------------------------------------

FIELD_RULES = {
    "Systems/ShopCatalog.cs": ["DisplayName", "Description"],
    "Systems/Achievements.cs": ["Title", "Description"],
    "Systems/MajestyAnalog.cs": ["CompactName", "Toy"],
    "Systems/World/CelestialBodyCatalog.cs":
        ["DisplayName", "Briefing", "ArrivalLog", "VictoryLog", "FailLog", "EndlessLog"],
    "Systems/RobotGuildCatalog.cs":
        ["HallName", "ShortName", "Motto", "Wants", "Ignores", "CatalogLine", "BenefitName", "BenefitBlurb"],
    "Systems/OrbitalPowers.cs": ["displayName", "description"],
    "Systems/ClassAbilities.cs": ["displayName"],
    "Systems/SpecialistPersonality.cs": ["displayName", "description"],
}

CALL_RULES = {
    "Systems/TechCatalog.cs": {"new TechDef": [1, 2]},
    "Systems/FlagDecreeIds.cs": {"new FlagDecree": [3, 4]},
    "Systems/AdvisorToastCatalog.cs": {"new AdvisorToast": [3]},
    "Systems/CampaignCutsceneCatalog.cs": {"new CampaignCutscene": [1, "3+"]},
    "Systems/ClassAbilities.cs": {"Strike": [2], "Summon": [2], "Buff": [2]},
    "Systems/FaunaAfflictions.cs": {"A": [1]},
}

DICT_RULES = {"Systems/AdvisorToastCatalog.cs"}

RETURN_RULES = {
    "Systems/JexCatalog.cs": ["Line"],
    "Systems/CompactJex.cs": ["*"],
    "Systems/FaunaNames.cs": ["Display"],
    "Systems/SpecialistIdentity.cs": ["RankFor"],
    "Systems/FlagOrdersParser.cs": ["ShortName"],
    "Systems/SpecialistFlavor.cs": ["FlagShort"],
}

CONST_RULES = {
    "Systems/OverseerRules.cs": r"Jex\w+",
    "Systems/MarketMath.cs": r"Jex\w+",
}

NOT_SHOWN = {
    "Systems/MajestyAnalog.cs": ["MajestyLoop"],
    "Systems/World/PlanetArchitecture.Belt.cs": ["Name", "Rationale"],
    "Systems/World/PlanetArchitecture.Earth.cs": ["Name", "Rationale"],
    "Systems/World/PlanetArchitecture.Europa.cs": ["Name", "Rationale"],
    "Systems/World/PlanetArchitecture.Luna.cs": ["Name", "Rationale"],
    "Systems/World/PlanetArchitecture.Mars.cs": ["Name", "Rationale"],
}

LIT_RUN = r'"(?:[^"\\\n]|\\.)*"(?:\s*\+\s*"(?:[^"\\\n]|\\.)*")*'  # "a" or "a" + "b" + ...
CONST_DECL = re.compile(r'\bconst\s+string\s+(\w+)\s*=\s*(' + LIT_RUN + r')\s*;')
LOC_IDENT = re.compile(r'\bLoc\.(?:S|F)\(\s*(?:[\w.]+\.)?([A-Za-z_]\w*)\s*[,)]')
PLAIN_LIT = re.compile(r'(?<![@$\w])' + LIT)


def _run_text(run):
    """Text of a literal run ("a" + "b"), or None if it is not only literals."""
    parts = re.findall(LIT, run)
    if not parts or re.sub(LIT, "", run).replace("+", "").strip():
        return None
    return "".join(unescape(p) for p in parts)


def _rel(path):
    return os.path.relpath(path, os.path.join(ROOT, "Assets", "Scripts")).replace("\\", "/")


def _call_args(code, open_paren):
    """Split the argument list starting at code[open_paren] == '('. Returns [(start, text)]."""
    args, depth, i, start = [], 0, open_paren + 1, open_paren + 1
    n = len(code)
    while i < n:
        c = code[i]
        if c == '"':
            i += 1
            while i < n and code[i] != '"':
                i += 2 if code[i] == '\\' else 1
        elif c in "([{":
            depth += 1
        elif c in ")]}":
            if depth == 0:
                args.append((start, code[start:i]))
                return args
            depth -= 1
        elif c == ',' and depth == 0:
            args.append((start, code[start:i]))
            start = i + 1
        i += 1
    return args


def _method_body(code, name):
    """(start, end) spans of every method called `name` (block or expression body)."""
    spans = []
    for m in re.finditer(r'\b' + re.escape(name) + r'\s*\([^;{]*?\)\s*(\{|=>)', code):
        if m.group(1) == "=>":
            end = code.find(";", m.end())
            spans.append((m.end(), end if end >= 0 else len(code)))
            continue
        depth, i = 0, m.end() - 1
        while i < len(code):
            if code[i] == '"':
                i += 1
                while i < len(code) and code[i] != '"':
                    i += 2 if code[i] == '\\' else 1
            elif code[i] == '{':
                depth += 1
            elif code[i] == '}':
                depth -= 1
                if depth == 0:
                    break
            i += 1
        spans.append((m.end(), i))
    return spans


def catalog_literals(rel, code, loc_idents):
    """[(offset, text)] for every literal a catalog rule collects in this (comment-stripped) file."""
    found = []
    for field in FIELD_RULES.get(rel, []):
        for m in re.finditer(r'(?<![\w.])(?:\w+\.)?' + re.escape(field) + r'\s*=\s*(' + LIT_RUN + ')', code):
            text = _run_text(m.group(1))
            if text is not None:
                found.append((m.start(1), text))
    for callee, indexes in CALL_RULES.get(rel, {}).items():
        for m in re.finditer(r'(?<![\w.])' + re.escape(callee) + r'\s*\(', code):
            args = _call_args(code, m.end() - 1)
            for k, (start, arg) in enumerate(args):
                hit = any(k == ix if isinstance(ix, int) else k >= int(str(ix).rstrip("+")) for ix in indexes)
                if not hit:
                    continue
                arg_text = re.sub(r'^\s*\w+\s*:(?!:)', "", arg)  # named argument
                text = _run_text(arg_text.strip())
                if text is not None:
                    found.append((start + arg.index('"'), text))
    if rel in DICT_RULES:
        for m in re.finditer(r'\{\s*[\w.]+\s*,\s*(' + LIT_RUN + r')\s*\}', code):
            text = _run_text(m.group(1))
            if text is not None:
                found.append((m.start(1), text))
    for name in RETURN_RULES.get(rel, []):
        spans = [(0, len(code))] if name == "*" else _method_body(code, name)
        for a, b in spans:
            for m in re.finditer(r'(?:\breturn|=>)\s*(' + LIT_RUN + r')\s*[;,\n}]', code[a:b]):
                text = _run_text(m.group(1))
                if text is not None:
                    found.append((a + m.start(1), text))
    pattern = CONST_RULES.get(rel)
    for m in CONST_DECL.finditer(code):
        if m.group(1) in loc_idents or (pattern and re.fullmatch(pattern, m.group(1))):
            text = _run_text(m.group(2))
            if text is not None:
                found.append((m.start(2), text))
    return found


def not_shown_literals(rel, code):
    found = []
    for field in NOT_SHOWN.get(rel, []):
        for m in re.finditer(r'(?<![\w.])(?:\w+\.)?' + re.escape(field) + r'\s*=\s*(' + LIT_RUN + ')', code):
            found.append(m.start(1))
    return found


def _stripped_files():
    for path in source_files():
        yield path, _rel(path), strip_comments(open(path, encoding="utf-8").read())


def loc_identifiers():
    names = set()
    for _, _, code in _stripped_files():
        names.update(m.group(1) for m in LOC_IDENT.finditer(code))
    return names


def collect():
    sources, keyed, where = {}, {}, {}
    idents = loc_identifiers()
    for path, short, code in _stripped_files():
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        for m in CALL_S.finditer(code):
            text = unescape(m.group(2))
            if text.strip():
                sources.setdefault(text, rel)
        for m in CALL_T.finditer(code):
            keyed.setdefault(unescape(m.group(1)), unescape(m.group(2)))
            where.setdefault(unescape(m.group(1)), rel)
        for _, text in catalog_literals(short, code, idents):
            if text.strip():
                sources.setdefault(text, rel)
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
    idents = loc_identifiers()
    for path, short, code in _stripped_files():
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        # Literal offsets a catalog rule already collects (or that are designer notes).
        covered = {off for off, _ in catalog_literals(short, code, idents)}
        covered.update(not_shown_literals(short, code))
        covered.update(m.start(2) - 1 for m in CALL_S.finditer(code))  # Loc.S(\n "..." on the next line
        count = 0
        offset = 0
        for line in code.split("\n"):
            line_start, offset = offset, offset + len(line) + 1
            if "Loc." in line or NOT_UI.search(line):
                continue
            for m in ANY_LIT.finditer(line):
                if line_start + m.start() in covered:
                    continue
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
