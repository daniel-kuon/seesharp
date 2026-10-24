"""Machine-translate the SeeSharp course content from German to English with DeepL.

    python -I tools/translate_course.py <course> --key-file deepl.key [--dry-run] [--only GLOB ...] [--max-chars N] [--ca-file BUNDLE]

* `modules/*/summary.de.md` and `modules/*/*/instructions.de.md` without an `.en.md` twin get one.
* `modules/*/module.yaml`, `modules/*/*/manifest.yaml` and `exams/*.yaml`: every locale map with a `de`
  value and no `en` value gets an `en` value, inserted by a line-based editor that leaves everything else
  byte for byte as it was.

Existing `.en.md` files and `en` keys are never touched, every file is written atomically, so an interrupted
run is resumed by running it again. `--dry-run` counts the characters that would be sent and calls nothing
but /v2/usage. Fenced code, inline code, URLs and link targets never reach DeepL as translatable text.
Standard library only; the key is read from `--key-file` and never printed.
"""
import argparse
import fnmatch
import html
import json
import os
import re
import ssl
import sys
import time
import urllib.error
import urllib.request
from collections import defaultdict
from pathlib import Path

API = "https://api-free.deepl.com/v2"
BATCH_TEXTS = 50          # DeepL limit per request
BATCH_CHARS = 30_000      # stay well below the 128 KiB request body limit

# Fixed course terms: German regex -> English. Sent as <span translate="no">English</span>, so DeepL builds the
# sentence around them (unsteered it produced "Addition: FizzBuzz", "Bonus question" and once "LCM" for ggT).
GLOSSARY = [
    (r"^Zusatz:", "Extra:"),                      # title prefix of optional exercises
    (r"\bZusatzaufgaben\b", "optional exercises"),
    (r"\bZusatzaufgabe\b", "optional exercise"),
    (r"\bggT\b", "GCD"),
]

# ---------------------------------------------------------------- inline Markdown <-> DeepL HTML

_INLINE = re.compile(
    r"(?P<code>(`+)(?P<cbody>.+?)(?<!`)\2(?!`))"     # code span, any backtick run length
    r"|(?P<img>!\[[^\]]*\]\([^)]*\))"                  # image: keep whole
    r"|\[(?P<ltext>[^\]]+)\]\((?P<ltarget>[^)]*)\)"   # link: translate text, keep target
    r"|(?P<auto><https?://[^>\s]+>)"                   # autolink
    r"|(?P<url>https?://[^\s<>()]+[^\s<>().,;:!?])"    # bare URL
)
_PH = re.compile(r"\ue000(\d+)\ue001")  # protected item placeholder
_LO = re.compile(r"\ue002(\d+)\ue003")  # link open placeholder
_LC = "\ue004"                           # link close placeholder
_CODE = '<code translate="no">'
_KEEP = '<span translate="no">'


def prepare(text):
    """Markdown inline text -> (html for DeepL, protected items), or None when there is no prose in it.
    A protected item is (original Markdown, text DeepL sees inside <code translate="no">, which it keeps verbatim
    and does not bill); a link is (target, None) and its text is translated inside <a i="N">."""
    prot = []

    def sub(m):
        if m.group("ltext") is not None:
            prot.append((m.group("ltarget"), None))
            inner = _INLINE.sub(sub, m.group("ltext"))
            return f"\ue002{len(prot) - 1}\ue003{inner}{_LC}"
        prot.append((m.group(0), m.group("cbody").strip() if m.group("code") else m.group(0)))
        return f"\ue000{len(prot) - 1}\ue001"

    s = _INLINE.sub(sub, text)
    if not re.search(r"[^\W\d_]", _PH.sub("", s)):
        return None
    s = html.escape(s, quote=False)
    for de, en in GLOSSARY:
        s = re.sub(de, _KEEP + en + "</span>", s)
    s = re.sub(r"\*\*(?=\S)(.+?)(?<=\S)\*\*", r"<b>\1</b>", s)
    s = re.sub(r"(?<!\w)__(?=\S)(.+?)(?<=\S)__(?!\w)", r"<b>\1</b>", s)
    s = re.sub(r"(?<![\w*])\*(?=[^\s*])(.+?)(?<=[^\s*])\*(?![\w*])", r"<i>\1</i>", s)
    s = re.sub(r"(?<!\w)_(?=\S)(.+?)(?<=\S)_(?!\w)", r"<i>\1</i>", s)
    s = _PH.sub(lambda m: _CODE + html.escape(prot[int(m.group(1))][1], quote=False) + "</code>", s)
    s = _LO.sub(lambda m: f'<a i="{m.group(1)}">', s).replace(_LC, "</a>")
    return s, prot


class RestoreError(Exception):
    pass


def restore(out, prot):
    """DeepL HTML -> Markdown, putting every protected item back exactly as it was.
    Code elements are matched by content (DeepL may reorder them), the first unused item wins; backticks DeepL
    sometimes adds around them are dropped."""
    seen = set()

    def code(m):
        content = html.unescape(m.group(1))
        k = next((k for k, (_, shown) in enumerate(prot) if shown == content and k not in seen), None)
        if k is None:
            raise RestoreError(out)
        seen.add(k)
        return f"\ue000{k}\ue001"

    def link(m):
        seen.add(int(m.group(1)))
        return f"[{m.group(2)}](\ue002{m.group(1)}\ue003)"

    s = re.sub("`?" + re.escape(_CODE) + "(.*?)</code>`?", code, out, flags=re.S)
    s = re.sub(re.escape(_KEEP) + "(.*?)</span>",  # glossary term; capitalised when it starts a sentence
               lambda m: m.group(1)[:1].upper() + m.group(1)[1:]
               if re.search(r"(^|[.!?]\s+)$", re.sub(r"<[^>]+>", "", m.string[:m.start()])) else m.group(1),
               s, flags=re.S)
    s = re.sub(r'<a i="(\d+)">(.*?)</a>', link, s, flags=re.S)
    for tag, md in (("b", "**"), ("i", "*")):  # whitespace inside an emphasis would break it
        s = re.sub(rf"<{tag}>(\s*)(.*?)(\s*)</{tag}>", lambda m: m.group(1) + md + m.group(2) + md + m.group(3),
                   s, flags=re.S)
    if seen != set(range(len(prot))) or re.search(r"<[a-z/][^>]*>", s):
        raise RestoreError(out)
    expand = lambda m: prot[int(m.group(1))][0]  # noqa: E731
    return _LO.sub(expand, _PH.sub(expand, html.unescape(s)))


# ---------------------------------------------------------------- Markdown blocks

_FENCE = re.compile(r"^\s*(`{3,}|~{3,})")
_HEADING = re.compile(r"^(\s{0,3}#{1,6}\s+)(.*?)(\s+#+)?\s*$")
_ITEM = re.compile(r"^(\s*(?:[-*+]|\d+[.)])\s+)(.*)$")
_QUOTE = re.compile(r"^(\s*>\s?)(.*)$")
_HR = re.compile(r"^\s{0,3}([-*_])(\s*\1){2,}\s*$")
_TABLE = re.compile(r"^\s*\|.*\|\s*$")
_TABLE_SEP = re.compile(r"^[\s|:-]+$")


def _block_start(line):
    return bool(_FENCE.match(line) or _HEADING.match(line) or _ITEM.match(line) or _HR.match(line)
                or _TABLE.match(line) or _QUOTE.match(line) or line.lstrip().startswith("<"))


def _cells(row):
    """Split a table row on unescaped pipes outside code spans."""
    cells, cur, tick = [], "", False
    for i, ch in enumerate(row):
        if ch == "`":
            tick = not tick
        if ch == "|" and not tick and (i == 0 or row[i - 1] != "\\"):
            cells.append(cur)
            cur = ""
        else:
            cur += ch
    cells.append(cur)
    return cells[1:-1]  # drop what is outside the outer pipes


def plan_markdown(text):
    """Split Markdown into parts: a str is kept verbatim, (prefix, text) is a translatable unit,
    ("row", indent, [cells]) is a table row. Paragraph and list item lines are joined into one unit."""
    lines = text.split("\n")
    parts, i = [], 0
    while i < len(lines):
        line = lines[i]
        fence = _FENCE.match(line)
        if fence:
            mark = fence.group(1)
            parts.append(line)
            i += 1
            while i < len(lines):
                parts.append(lines[i])
                i += 1
                if lines[i - 1].strip().startswith(mark[0] * len(mark)) and not lines[i - 1].strip().strip(mark[0]):
                    break
            continue
        if not line.strip() or _HR.match(line) or line.lstrip().startswith("<"):
            parts.append(line)
            i += 1
            continue
        if _TABLE.match(line):
            if _TABLE_SEP.match(line):
                parts.append(line)
            else:
                parts.append(("row", line[:len(line) - len(line.lstrip())], [c.strip() for c in _cells(line.strip())]))
            i += 1
            continue
        m = _HEADING.match(line)
        if m:
            parts.append((m.group(1), m.group(2), m.group(3) or ""))
            i += 1
            continue
        m = _ITEM.match(line) or _QUOTE.match(line)
        prefix, body = (m.group(1), [m.group(2)]) if m else (line[:len(line) - len(line.lstrip())], [line.strip()])
        quote = bool(m) and m.re is _QUOTE
        i += 1
        while i < len(lines) and lines[i].strip():
            nxt = lines[i]
            if quote and _QUOTE.match(nxt):
                body.append(_QUOTE.match(nxt).group(2))
            elif _block_start(nxt):
                break
            else:
                body.append(nxt.strip())
            i += 1
        parts.append((prefix, " ".join(body), ""))
    return parts


def units_of(parts):
    for p in parts:
        if isinstance(p, tuple):
            if p[0] == "row":
                yield from p[2]
            else:
                yield p[1]


def render_markdown(parts, tr):
    out = []
    for p in parts:
        if isinstance(p, str):
            out.append(p)
        elif p[0] == "row":
            out.append(p[1] + "| " + " | ".join(tr(c) for c in p[2]) + " |")
        else:
            out.append(p[0] + tr(p[1]) + p[2])
    return "\n".join(out)


# ---------------------------------------------------------------- YAML locale maps

_DQ_ESC = {"0": "\0", "a": "\a", "b": "\b", "t": "\t", "\t": "\t", "n": "\n", "v": "\v", "f": "\f", "r": "\r",
           "e": "\x1b", " ": " ", '"': '"', "/": "/", "\\": "\\", "N": "\x85", "_": "\xa0", "L": " ",
           "P": " "}


def _dq_decode(body):
    def esc(m):
        e = m.group(1)
        if e[0] in "xuU":
            return chr(int(e[1:], 16))
        return _DQ_ESC[e]
    body = re.sub(r"[ \t]*\n[ \t]*", " ", body)  # folded line breaks inside a quoted scalar
    return re.sub(r"\\(x[0-9a-fA-F]{2}|u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|.)", esc, body)


def yaml_quote(s):
    """A double-quoted YAML scalar for any string."""
    out = []
    for ch in s:
        if ch == "\\":
            out.append("\\\\")
        elif ch == '"':
            out.append('\\"')
        elif ch == "\n":
            out.append("\\n")
        elif ch == "\t":
            out.append("\\t")
        elif ord(ch) < 0x20 or ord(ch) == 0x7f:
            out.append(f"\\x{ord(ch):02x}")
        else:
            out.append(ch)
    return '"' + "".join(out) + '"'


def _scan_quoted(s, i):
    """s[i] is a quote; return (decoded value, index after the closing quote) or None if unclosed."""
    q = s[i]
    j = i + 1
    while j < len(s):
        if q == '"' and s[j] == "\\":
            j += 2
            continue
        if s[j] == q:
            if q == "'" and s[j + 1:j + 2] == "'":
                j += 2
                continue
            body = s[i + 1:j]
            val = _dq_decode(body) if q == '"' else re.sub(r"[ \t]*\n[ \t]*", " ", body).replace("''", "'")
            return val, j + 1
        j += 1
    return None


def _flow_map(line, i):
    """line[i] == '{'. Return (end index, {key: (value, value end index)}) or None if not parseable here."""
    entries = {}
    j = i + 1
    while True:
        while j < len(line) and line[j] in " \t":
            j += 1
        if j >= len(line):
            return None
        if line[j] == "}":
            return j + 1, entries
        m = re.match(r"([A-Za-z_][\w-]*)\s*:\s+", line[j:])
        if not m:
            return None
        key = m.group(1)
        j += m.end()
        if line[j:j + 1] in ('"', "'"):
            r = _scan_quoted(line, j)
            if not r:
                return None
            val, j = r
        elif line[j:j + 1] in ("{", "["):
            return None
        else:
            m = re.match(r"[^,}]*", line[j:])
            val = m.group(0).strip()
            j += len(m.group(0).rstrip())
        entries[key] = (val, j)
        while j < len(line) and line[j] in " \t":
            j += 1
        if line[j:j + 1] == ",":
            j += 1
        elif line[j:j + 1] != "}":
            return None


def _indent(line):
    return len(line) - len(line.lstrip(" "))


_BLOCK_DE = re.compile(r"^(\s*)(- )?de:(?:\s+(.*?))?\s*$")


def plan_yaml(lines, warn=lambda msg: None):
    """Find locale maps with `de` and no `en`. `lines` keep their line endings.
    Returns jobs: ("flow", line, column, de) inserts `, en: …` at column; ("block", after_line, indent, de)
    inserts an `en:` line after line index after_line."""
    jobs = []
    i = 0
    while i < len(lines):
        line = lines[i].rstrip("\r\n")
        m = _BLOCK_DE.match(line)
        if m:
            end = _block_de(lines, i, m, jobs, warn)
            i = end + 1
            continue
        j, quote_ok = 0, True
        while j < len(line):
            ch = line[j]
            if ch == "#" and (j == 0 or line[j - 1] in " \t"):
                break
            if ch in "\"'" and quote_ok:
                r = _scan_quoted(line, j)
                if not r:
                    break
                j = r[1]
                continue
            if ch == "{":
                r = _flow_map(line, j)
                if r is None:
                    if re.search(r"\bde\s*:", line[j:]):
                        warn(f"line {i + 1}: locale map not on one line or not parseable, skipped")
                    break
                end, entries = r
                if "de" in entries and "en" not in entries:
                    jobs.append(("flow", i, entries["de"][1], entries["de"][0]))
                j = end
                continue
            quote_ok = ch in " \t[{,:"
            j += 1
        i += 1
    return jobs


def _block_de(lines, i, m, jobs, warn):
    """Handle a `de:` key of a block map at line i; return the last line index of its value."""
    col = len(m.group(1)) + (2 if m.group(2) else 0)
    raw = m.group(3)
    end = i
    if raw is None or raw.startswith("#"):
        return i  # no scalar value: not a locale string
    if raw[0] in "|>":
        body = []
        k = i + 1
        while k < len(lines) and (not lines[k].strip() or _indent(lines[k]) > col):
            body.append(lines[k].rstrip("\r\n"))
            k += 1
        while body and not body[-1].strip():
            body.pop()
        end = i + len(body)
        ind = min((_indent(b) for b in body if b.strip()), default=0)
        body = [b[ind:] for b in body]
        if raw[0] == "|":
            value = "\n".join(body)
        else:
            value = "\n".join(" ".join(p.split("\n")) for p in "\n".join(body).split("\n\n"))
    elif raw[0] in "\"'":
        text = lines[i].rstrip("\r\n")
        start = m.start(3)
        r = _scan_quoted(text, start)
        while r is None and end + 1 < len(lines):
            end += 1
            text += "\n" + lines[end].rstrip("\r\n")
            r = _scan_quoted(text, start)
        if r is None:
            warn(f"line {i + 1}: unclosed quoted de value, skipped")
            return end
        value = r[0]
    else:
        value = re.sub(r"\s+#.*$", "", raw)
        k = i + 1
        while k < len(lines) and lines[k].strip() and _indent(lines[k]) > col:
            value += " " + lines[k].strip()
            k += 1
        end = k - 1
    # siblings of the same map: same key column; an item start `- ` sits two columns left
    keys = []
    if not m.group(2):
        for k in range(i - 1, -1, -1):
            s = lines[k].rstrip("\r\n")
            if not s.strip() or s.lstrip().startswith("#") or _indent(s) > col:
                continue
            if _indent(s) == col and not s.lstrip().startswith("- "):
                keys.append(s.strip().split(":")[0])
                continue
            if _indent(s) == col - 2 and s.lstrip().startswith("- "):
                keys.append(s.strip()[2:].split(":")[0])
            break
    for k in range(end + 1, len(lines)):
        s = lines[k].rstrip("\r\n")
        if not s.strip() or s.lstrip().startswith("#") or _indent(s) > col:
            continue
        if _indent(s) == col and not s.lstrip().startswith("- "):
            keys.append(s.strip().split(":")[0])
            continue
        break
    if "en" not in keys:
        jobs.append(("block", end, " " * col, value))
    return end


def apply_yaml(lines, jobs, tr):
    """Apply jobs bottom-up / right-to-left so earlier positions stay valid."""
    lines = list(lines)
    for job in sorted(jobs, key=lambda j: (j[1], j[2] if j[0] == "flow" else 10 ** 9), reverse=True):
        kind, idx, pos, de = job
        en = yaml_quote(tr(de))
        if kind == "flow":
            lines[idx] = lines[idx][:pos] + ", en: " + en + lines[idx][pos:]
        else:
            nl = lines[idx][len(lines[idx].rstrip("\r\n")):] or "\n"
            if not lines[idx].endswith(("\n", "\r")):
                lines[idx] += nl
            lines.insert(idx + 1, pos + "en: " + en + nl)
    return lines


# ---------------------------------------------------------------- DeepL

class BudgetExceeded(Exception):
    pass


def billable(h):
    """What DeepL bills for one text: tags and translate="no" content are free (matched /v2/usage exactly)."""
    h = re.sub(r'<(code|span) translate="no">.*?</\1>', "", h, flags=re.S)
    return len(html.unescape(re.sub(r"<[^>]+>", "", h)))


class DeepL:
    def __init__(self, key, dry_run, max_chars, ca_file=None):
        self.key, self.dry_run, self.max_chars = key, dry_run, max_chars
        self.tls = ssl.create_default_context(cafile=ca_file)  # verification always on
        self.sent = 0  # billed characters
        self.cache = {}

    def _call(self, path, payload=None):
        data = json.dumps(payload).encode() if payload is not None else None
        req = urllib.request.Request(API + path, data=data, headers={
            "Authorization": "DeepL-Auth-Key " + self.key, "Content-Type": "application/json"})
        for attempt in range(6):
            try:
                with urllib.request.urlopen(req, timeout=120, context=self.tls) as r:
                    return json.load(r)
            except urllib.error.HTTPError as e:
                if e.code in (429, 500, 502, 503, 504) and attempt < 5:
                    time.sleep(2 ** attempt)
                    continue
                if e.code == 456:
                    raise BudgetExceeded("DeepL quota exceeded (456)") from None
                raise SystemExit(f"DeepL {path}: HTTP {e.code} {e.read()[:300]!r}") from None

    def usage(self):
        u = self._call("/usage")
        return u["character_count"], u["character_limit"]

    def translate(self, htmls):
        """Translate distinct HTML strings (cached); returns the billed characters for them."""
        todo = list(dict.fromkeys(h for h in htmls if h not in self.cache))
        batches, cur = [], []
        for h in todo:
            if cur and (len(cur) == BATCH_TEXTS or sum(map(len, cur)) + len(h) > BATCH_CHARS):
                batches.append(cur)
                cur = []
            cur.append(h)
        if cur:
            batches.append(cur)
        billed = 0
        for b in batches:
            n = sum(map(billable, b))
            if self.max_chars is not None and self.sent + n > self.max_chars:
                raise BudgetExceeded(f"--max-chars {self.max_chars} would be exceeded ({self.sent} + {n})")
            if self.dry_run:
                out = b
            else:
                out = [t["text"] for t in self._call("/translate", {
                    "text": b, "source_lang": "DE", "target_lang": "EN-US",
                    "tag_handling": "html", "preserve_formatting": True})["translations"]]
            self.cache.update(zip(b, out))
            self.sent += n
            billed += n
        return billed


_GLUED = re.compile(r"[^\W\d_]`[^`]+`[^\W\d_]")  # code inside a word: DeepL misplaced a code span


def translate_texts(texts, client, warn=lambda msg: None):
    """Markdown inline texts -> {text: English}; returns (mapping, billed characters)."""
    prepared = {t: prepare(t) for t in set(texts)}
    sent = client.translate([p[0] for p in prepared.values() if p])
    result = {}
    for t, p in prepared.items():
        if p is None or client.dry_run:
            result[t] = t
            continue
        try:
            result[t] = restore(client.cache[p[0]], p[1])
        except RestoreError as e:
            raise SystemExit(f"could not restore protected items in DeepL output:\n  sent: {p[0]}\n  got:  {e}")
        if len(_GLUED.findall(result[t])) > len(_GLUED.findall(t)):
            warn(f"check code placement: {_GLUED.search(result[t]).group(0)!r} in {result[t][:80]!r}…")
    return result, sent


def tr_value(value, client_map):
    """A YAML value is Markdown too (quiz texts are rendered as Markdown); single-line values stay one unit."""
    return render_markdown(plan_markdown(value), client_map.__getitem__)


# ---------------------------------------------------------------- files

def read(path):
    raw = path.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    return raw.decode("utf-8-sig"), bom


def write_atomic(path, text, bom):
    tmp = path.with_name(path.name + ".tmp")
    tmp.write_bytes((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))
    os.replace(tmp, path)


def targets(course):
    md = sorted(course.glob("modules/*/summary.de.md")) + sorted(course.glob("modules/*/*/instructions.de.md"))
    ym = (sorted(course.glob("modules/*/module.yaml")) + sorted(course.glob("modules/*/*/manifest.yaml"))
          + sorted(course.glob("exams/*.yaml")))
    return md, ym


def kind_of(rel):
    return "summary.md" if rel.endswith("summary.de.md") else "instructions.md" if rel.endswith(".md") \
        else "exam.yaml" if rel.startswith("exams/") else rel.rsplit("/", 1)[-1]


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("course", type=Path)
    ap.add_argument("--key-file", type=Path, required=True)
    ap.add_argument("--dry-run", action="store_true", help="count characters, translate nothing")
    ap.add_argument("--only", action="append", help="glob on the course-relative path (repeatable)")
    ap.add_argument("--max-chars", type=int, help="stop before sending more than this many characters")
    ap.add_argument("--ca-file", default=os.environ.get("SSL_CERT_FILE"),
                    help="CA bundle for TLS, if the Windows store fails (e.g. Git's mingw64/etc/ssl/certs/ca-bundle.crt)")
    a = ap.parse_args(argv)

    key = a.key_file.read_text(encoding="utf-8").strip()
    client = DeepL(key, a.dry_run, a.max_chars, a.ca_file)
    used, limit = client.usage()
    print(f"DeepL usage before: {used:,} / {limit:,}")

    md, ym = targets(a.course)
    by_kind, by_module = defaultdict(int), defaultdict(int)
    files_done = 0

    def wanted(p):
        rel = p.relative_to(a.course).as_posix()
        return rel if not a.only or any(fnmatch.fnmatch(rel, g) for g in a.only) else None

    def warner(rel):
        return lambda msg: print(f"warning: {rel}: {msg}", file=sys.stderr)

    def tally(rel, n):
        by_kind[kind_of(rel)] += n
        by_module[rel.split("/")[1] if rel.startswith("modules/") else "exams"] += n

    try:
        for p in md:
            rel = wanted(p)
            out = p.with_name(p.name.replace(".de.md", ".en.md"))
            if not rel or out.exists():
                continue
            text, bom = read(p)
            parts = plan_markdown(text)
            mapping, n = translate_texts(list(units_of(parts)), client, warner(rel))
            tally(rel, n)
            if not a.dry_run:
                write_atomic(out, render_markdown(parts, mapping.__getitem__), bom)
                files_done += 1
                print(f"wrote {out.relative_to(a.course).as_posix()} ({n:,} chars)")
        for p in ym:
            rel = wanted(p)
            if not rel:
                continue
            text, bom = read(p)
            lines = text.splitlines(keepends=True)
            jobs = plan_yaml(lines, warner(rel))
            if not jobs:
                continue
            units = [u for j in jobs for u in units_of(plan_markdown(j[3]))]
            mapping, n = translate_texts(units, client, warner(rel))
            tally(rel, n)
            if not a.dry_run:
                write_atomic(p, "".join(apply_yaml(lines, jobs, lambda v: tr_value(v, mapping))), bom)
                files_done += 1
                print(f"updated {rel}: {len(jobs)} en values ({n:,} chars)")
    except BudgetExceeded as e:
        print(f"stopped: {e}. Files written so far are complete; rerun to resume.", file=sys.stderr)
        return 3
    finally:
        if a.dry_run:
            print("\nBilled characters that would be sent (distinct texts):")
            for k in sorted(by_kind):
                print(f"  {k:<44}{by_kind[k]:>10,}")
            print("  by module:")
            for k in sorted(by_module):
                print(f"    {k:<42}{by_module[k]:>10,}")
        print(f"total billed characters {'counted' if a.dry_run else 'sent'}: {client.sent:,}"
              + ("" if a.dry_run else f", {files_done} files written"))
        used, limit = client.usage()
        print(f"DeepL usage after: {used:,} / {limit:,}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
