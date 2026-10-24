"""Local checks for translate_course.py (no API calls):  python -I -m unittest tools/test_translate_course.py"""
import re
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import translate_course as tc  # noqa: E402


def fake_deepl(html):
    """Stand-in for DeepL: uppercases prose, leaves tags and translate="no" content alone, moves nothing."""
    parts = re.split(r'(<(?:code|span) translate="no">.*?</(?:code|span)>|<[^>]+>|&\w+;)', html)
    return "".join(p if p.startswith(("<", "&")) else p.upper() for p in parts)


def roundtrip(text):
    p = tc.prepare(text)
    return text if p is None else tc.restore(fake_deepl(p[0]), p[1])


C = '<code translate="no">'


class MarkdownProtection(unittest.TestCase):
    def test_code_urls_and_link_targets_survive(self):
        src = ("Die Klasse `Bruch` hat `Kuerze()`, siehe [Doku](https://learn.microsoft.com/de-de/x?a=1&b=2) "
               "und https://example.org/a_b. Test: ``a ` b``, **fett** und *kursiv* mit 3 < 4 & `List<int>`.")
        self.assertEqual(roundtrip(src),
                         "DIE KLASSE `Bruch` HAT `Kuerze()`, SIEHE [DOKU](https://learn.microsoft.com/de-de/x?a=1&b=2) "
                         "UND https://example.org/a_b. TEST: ``a ` b``, **FETT** UND *KURSIV* MIT 3 < 4 & `List<int>`.")

    def test_code_is_sent_untranslatable_and_unbilled(self):
        html, prot = tc.prepare("Rufe `Wort()` auf")
        self.assertEqual(html, f"Rufe {C}Wort()</code> auf")
        self.assertEqual(prot, [("`Wort()`", "Wort()")])
        self.assertEqual(tc.billable(html), len("Rufe  auf"))

    def test_code_only_units_are_not_sent(self):
        self.assertIsNone(tc.prepare("`FizzBuzzTests.Folge_bis_5`"))
        self.assertIsNone(tc.prepare("`a`, `b` (1)"))

    def test_lost_or_changed_code_is_an_error(self):
        html, prot = tc.prepare("Rufe `Wort()` auf")
        with self.assertRaises(tc.RestoreError):
            tc.restore(html.replace(f"{C}Wort()</code>", "Wort()"), prot)
        with self.assertRaises(tc.RestoreError):
            tc.restore(html.replace("Wort()", "Word()"), prot)

    def test_reordered_code_goes_back_by_content(self):
        html, prot = tc.prepare("`a` vor ``b`` und `a`")
        out = f"{C}b</code> after {C}a</code> and {C}a</code>"
        self.assertEqual(tc.restore(out, prot), "``b`` after `a` and `a`")

    def test_deepl_quirks_are_repaired(self):
        html, prot = tc.prepare("Mit `x` und **fett**")
        # DeepL sometimes wraps code in backticks of its own and pulls spaces into emphasis
        self.assertEqual(tc.restore(f"With `{C}x</code>` and <b>bold </b>.", prot), "With `x` and **bold** .")

    def test_glossary_terms_are_fixed(self):
        html, _ = tc.prepare("Zusatz: FizzBuzz")
        self.assertEqual(html, '<span translate="no">Extra:</span> FizzBuzz')
        self.assertEqual(roundtrip("Zusatzaufgabe mit ggT, kein Zusatz: hier. Zusatzaufgaben"),
                         "Optional exercise MIT GCD, KEIN ZUSATZ: HIER. Optional exercises")

    def test_misplaced_code_is_reported(self):
        class Fake:  # translate_texts' view of DeepL, answering with a misplaced code span
            dry_run, cache = False, {}

            def translate(self, htmls):
                self.cache.update({h: f"The num{C}b</code>ers" for h in htmls})
                return 0

        warnings = []
        result, _ = tc.translate_texts(["Die Zahlen `b`"], Fake(), warnings.append)
        self.assertEqual(result, {"Die Zahlen `b`": "The num`b`ers"})
        self.assertEqual(len(warnings), 1)

    def test_blocks(self):
        src = ("# Titel\n\nZeile eins\nzeile zwei.\n\n1. Punkt mit `x`\n   weiter\n   - Unterpunkt\n"
               "\n```csharp\nvar s = \"Hallo\"; // Kommentar\n```\n\n| A | `b` |\n|---|---|\n| eins | zwei |\n"
               "\n- `Tests.Nur_Code`\n")
        parts = tc.plan_markdown(src)
        out = tc.render_markdown(parts, roundtrip)
        self.assertEqual(out, "# TITEL\n\nZEILE EINS ZEILE ZWEI.\n\n1. PUNKT MIT `x` WEITER\n   - UNTERPUNKT\n"
                              "\n```csharp\nvar s = \"Hallo\"; // Kommentar\n```\n\n| A | `b` |\n|---|---|\n"
                              "| EINS | ZWEI |\n\n- `Tests.Nur_Code`\n")


def edit(yaml_text, tr=lambda s: "EN:" + s):
    lines = yaml_text.splitlines(keepends=True)
    jobs = tc.plan_yaml(lines)
    return "".join(tc.apply_yaml(lines, jobs, tr))


class YamlInsertion(unittest.TestCase):
    def test_flow_maps(self):
        src = ('title: { de: "Zusatz: \\"x\\"" }  # Kommentar\n'
               "    options: [{ de: \"a, b\" }, { de: 'it''s' }, { de: plain }, { de: \"x\", en: \"y\" }]\n"
               'openFile: "a{b}.cs"\n')
        self.assertEqual(edit(src),
                         'title: { de: "Zusatz: \\"x\\"", en: "EN:Zusatz: \\"x\\"" }  # Kommentar\n'
                         "    options: [{ de: \"a, b\", en: \"EN:a, b\" }, { de: 'it''s', en: \"EN:it's\" }, "
                         "{ de: plain, en: \"EN:plain\" }, { de: \"x\", en: \"y\" }]\n"
                         'openFile: "a{b}.cs"\n')

    def test_block_maps_and_list_items(self):
        src = ("title:\n  de: Titel  # c\nprompt:\n  en: \"done\"\n  de: \"schon da\"\n"
               "options:\n  - de: \"eins\"\n  - de: 'zwei'\n    en: two\n  - de: drei\n")
        self.assertEqual(edit(src),
                         "title:\n  de: Titel  # c\n  en: \"EN:Titel\"\nprompt:\n  en: \"done\"\n  de: \"schon da\"\n"
                         "options:\n  - de: \"eins\"\n    en: \"EN:eins\"\n  - de: 'zwei'\n    en: two\n"
                         "  - de: drei\n    en: \"EN:drei\"\n")

    def test_block_scalar_and_crlf(self):
        src = "prompt:\r\n  de: |\r\n    Zeile 1\r\n    Zeile 2\r\n  x: 1\r\n"
        self.assertEqual(edit(src),
                         "prompt:\r\n  de: |\r\n    Zeile 1\r\n    Zeile 2\r\n  en: \"EN:Zeile 1\\nZeile 2\"\r\n  x: 1\r\n")

    def test_multiline_quoted(self):
        src = 'p:\n  de: "lang\n    weiter"\nq: 1\n'
        self.assertEqual(edit(src), 'p:\n  de: "lang\n    weiter"\n  en: "EN:lang weiter"\nq: 1\n')

    def test_idempotent(self):
        once = edit('title: { de: "A" }\nt:\n  de: B\n')
        self.assertEqual(edit(once), once)

    def test_quote_escapes(self):
        self.assertEqual(tc.yaml_quote('a "b" \\ c\nd\te\x01'), '"a \\"b\\" \\\\ c\\nd\\te\\x01"')


class DroppingClient:
    """Fake DeepL that 'translates' by identity but drops the tail of any text with more than 4 code spans,
    the way the real one sometimes does with long, code-heavy units."""
    dry_run = False

    def __init__(self):
        self.cache = {}

    def translate(self, htmls):
        for h in htmls:
            self.cache[h] = h[: h.index("</code>", h.index("</code>") + 1) + 7] if h.count("<code") > 4 else h
        return 0


class Fallback(unittest.TestCase):
    def test_dropped_text_is_retried_in_smaller_pieces_instead_of_aborting(self):
        line = "Tests: `Eins`, `Zwei`, `Drei`, `Vier`, `Fuenf`, `Sechs`."
        result, _ = tc.translate_texts([line], DroppingClient())
        self.assertEqual(result[line], line)  # identity "translation", every code span back in place

    def test_sentences_never_split_inside_code(self):
        self.assertEqual(tc.split_sentences("A `x. Y` b. C."), ["A `x. Y` b.", "C."])


if __name__ == "__main__":
    unittest.main()
