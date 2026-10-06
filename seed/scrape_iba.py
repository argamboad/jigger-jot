"""
Extract the IBA official cocktail list (iba-world.com) into a structured JSON file.

The International Bartenders Association publishes the canon: 102 drinks in three groups of 34 —
The Unforgettables, The Contemporary, The New Era. This is the modern half of JiggerJot's seed
catalog, and the reason it exists: the 1930 Savoy has 868 recipes and, between them, zero tequila,
zero bourbon and one line of Campari. Every drink a person is likely to ask for by name today lives
in this list and in no public-domain book, because anything old enough to be free predates them.

What is taken and what is not. The pages carry a specification — name, category, ingredient lines
with metric amounts, method, garnish — and that is all this reads. Specifications are functional
facts; the surrounding prose, the videos and the photography are the site's own and are left alone.
Attribution to the IBA ships with the data (JJ-032).

Politeness: pages are enumerated from the site's own sitemap rather than by walking paginated HTML,
one request per drink with a pause between, identifying User-Agent. robots.txt disallows only
/wp-admin/ (checked 2026-09-09). There is no REST endpoint for the cocktail post type, which is why
this parses HTML at all — the Savoy extractor uses that site's REST API and makes far fewer requests.

    python seed/scrape_iba.py
"""
import collections
import html
import json
import re
import time
import unicodedata

import requests
from bs4 import BeautifulSoup

SITEMAP = "https://iba-world.com/wp-sitemap-posts-iba-cocktail-1.xml"
HEADERS = {"User-Agent": "jigger-jot-data-import/1.0 (personal project; contact: argamboad@gmail.com)"}
SLEEP_BETWEEN_REQUESTS = 0.5

# The headings the recipe sits under, in page order. Everything before the first and after the last
# is site chrome (navigation, "most viewed", the age gate) and is discarded.
SECTIONS = ("ingredients", "method", "garnish")

# The three official groups, normalised to the names the IBA publishes. Keyed by what the BREADCRUMB
# says, which is not what the nav menu says: the nav reads "The Contemporary", the breadcrumb reads
# "Contemporary Classics", and the Unforgettables breadcrumb lower-cases its own noun.
CATEGORIES = {
    "the unforgettables": "The Unforgettables",
    "unforgettables": "The Unforgettables",
    "contemporary classics": "Contemporary Classics",
    "the contemporary": "Contemporary Classics",
    "new era drinks": "New Era Drinks",
    "new era": "New Era Drinks",
    "the new era": "New Era Drinks",
}

# The list is three equal groups of 34 by construction, and has been since the 2026 revision. That
# makes it a free correctness check on the parse: any breadcrumb misread lands drinks in the wrong
# group and the counts stop being equal, which is how two separate parsing bugs were caught here.
EXPECTED_PER_CATEGORY = 34

# Amounts are metric on every page ("30 ml Gin"), which is the whole appeal of this source over a
# period book: no proportions to interpret and no house measures to guess at.
LINE_RE = re.compile(
    r"^(?P<amount>\d+(?:[.,]\d+)?(?:\s*/\s*\d+)?|\d+\s+\d+/\d+)?\s*"
    r"(?P<unit>ml|cl|oz|dash(?:es)?|drops?|bar\s?spoons?|teaspoons?|tsp|tablespoons?|tbsp|"
    r"splash(?:es)?|slices?|wedges?|sprigs?|leaves|leaf|cubes?|pieces?|drops?)?\s*"
    r"(?P<name>.+?)$",
    re.IGNORECASE,
)

# Glass is not its own field; it is named inside the method prose ("into chilled old fashioned
# glass"). Best-effort, and deliberately conservative — a wrong glass is worse than a missing one,
# since a null asks the curator a question and a wrong value answers it for them.
GLASS_RE = re.compile(
    r"\b((?:old[- ]fashioned|highball|collins|martini|cocktail|champagne\s+(?:flute|tulip)|flute|"
    r"wine|hurricane|julep|shot|rocks|coupe|sour|tumbler|goblet|mug|tiki|sling|toddy|punch)"
    r"(?:\s+\w+){0,2}?\s+(?:glass(?:es)?|cup|mug|tumbler|goblet))\b",
    re.IGNORECASE,
)

METHOD_WORDS = (
    ("dry shake", "dry shake"), ("shake", "shake"), ("stir", "stir"), ("blend", "blend"),
    ("muddle", "muddle"), ("swizzle", "swizzle"), ("throw", "throw"), ("layer", "layer"),
    ("float", "layer"), ("build", "build"), ("pour all ingredients directly", "build"),
)


def clean_text(s):
    s = html.unescape(s or "")
    s = unicodedata.normalize("NFKC", s)
    return re.sub(r"\s+", " ", s).strip()


def fetch(url):
    r = requests.get(url, headers=HEADERS, timeout=30)
    r.raise_for_status()
    return r.text


def cocktail_urls():
    """Every official cocktail, from the site's own sitemap."""
    return re.findall(r"<loc>([^<]+)</loc>", fetch(SITEMAP))


# Everything from here down is site chrome - the "most viewed" rail, the footer, the age gate. The
# recipe's last section is Garnish and it runs to the end of the page, so without this cut a Negroni's
# garnish reads "half orange slice" followed by four unrelated drinks and a Singapore postal address.
CHROME_MARKER = "most viewed cocktails"


def visible_lines(page_html):
    """The page's visible text, one entry per element, with runs of repeats collapsed and the
    trailing site chrome cut off."""
    soup = BeautifulSoup(page_html, "html.parser")
    for tag in soup(["script", "style", "noscript"]):
        tag.decompose()

    lines = []
    for raw in soup.get_text("\n").split("\n"):
        text = clean_text(raw)
        if text and (not lines or lines[-1] != text):
            lines.append(text)

    cut = next((i for i, l in enumerate(lines) if l.strip().lower() == CHROME_MARKER), None)
    return lines[:cut] if cut is not None else lines


def carve_sections(lines):
    """Split the visible text into the ingredients / method / garnish blocks, by their headings."""
    marks = {}
    for i, line in enumerate(lines):
        key = line.strip().lower()
        if key in SECTIONS and key not in marks:
            marks[key] = i
    if "ingredients" not in marks:
        return None

    bounds = sorted(marks.items(), key=lambda kv: kv[1])
    sections = {}
    for n, (name, start) in enumerate(bounds):
        end = bounds[n + 1][1] if n + 1 < len(bounds) else len(lines)
        sections[name] = lines[start + 1:end]
    return sections


def parse_line(raw):
    """One ingredient line into (amount, unit, ingredient). Amount and unit may be absent —
    'Champagne' and 'Angostura bitters' are legitimate whole lines."""
    m = LINE_RE.match(raw)
    if not m:
        return {"raw": raw, "amount": None, "unit": None, "ingredient": raw}

    amount = m.group("amount")
    if amount:
        amount = amount.replace(",", ".").replace(" ", "")
    return {
        "raw": raw,
        "amount": amount,
        "unit": (m.group("unit") or None),
        "ingredient": clean_text(m.group("name")),
    }


def derive_method(method_text):
    lowered = method_text.lower()
    for needle, method in METHOD_WORDS:
        if needle in lowered:
            return method
    return None


def derive_glass(method_text, garnish_text):
    m = GLASS_RE.search(f"{method_text} {garnish_text}")
    return clean_text(m.group(1)).lower() if m else None


def parse_cocktail(url, page_html):
    lines = visible_lines(page_html)
    sections = carve_sections(lines)
    if not sections:
        return None

    slug = url.rstrip("/").rsplit("/", 1)[-1]

    # Name and category come from the BREADCRUMB, read structurally: it is the last
    # "<name> / <category> / <name>" run before "Ingredients", so the category sits between the final
    # pair of separators. Matching on the text instead does not work, and both ways of getting that
    # wrong were tried first: the nav menu higher up lists all three groups verbatim, so taking the
    # first "The ..." line labelled every drink an Unforgettable, and taking the LAST one labelled
    # every Contemporary Classic a New Era drink - because that breadcrumb reads "Contemporary
    # Classics", with no "The", and the fallback landed on the nav's last entry.
    head = lines[:next(i for i, l in enumerate(lines) if l.strip().lower() == "ingredients")]
    separators = [i for i, l in enumerate(head) if l.strip() == "/"]

    category = name = None
    if len(separators) >= 2:
        first, second = separators[-2], separators[-1]
        if second - first == 2:                       # "/ <category> /"
            category = CATEGORIES.get(head[first + 1].lower(), head[first + 1])
            name = head[first - 1] if first > 0 else None

    if not name:
        name = slug.replace("-", " ").title()

    ingredient_lines = [parse_line(l) for l in sections.get("ingredients", []) if l != "/"]
    method_text = " ".join(sections.get("method", []))
    garnish_text = " ".join(sections.get("garnish", []))

    return {
        "slug": slug,
        "name": name,
        "url": url,
        "category": category,
        "ingredient_lines": ingredient_lines,
        "ingredients": sorted({l["ingredient"] for l in ingredient_lines if l["ingredient"]}),
        "instructions": method_text or None,
        "garnish": garnish_text or None,
        "method": derive_method(method_text),
        "glass": derive_glass(method_text, garnish_text),
    }


def main():
    urls = cocktail_urls()
    print(f"{len(urls)} official cocktails listed in the sitemap")

    cocktails, skipped = [], []
    for n, url in enumerate(urls, 1):
        try:
            parsed = parse_cocktail(url, fetch(url))
        except Exception as e:  # one bad page must not lose the other hundred
            print(f"  [{n:3}/{len(urls)}] FAILED {url}: {type(e).__name__}: {e}")
            skipped.append(url)
            continue

        if parsed is None:
            print(f"  [{n:3}/{len(urls)}] no recipe section: {url}")
            skipped.append(url)
            continue

        cocktails.append(parsed)
        print(f"  [{n:3}/{len(urls)}] {parsed['name']} — {len(parsed['ingredient_lines'])} lines")
        time.sleep(SLEEP_BETWEEN_REQUESTS)

    cocktails.sort(key=lambda c: c["name"].lower())

    out = {
        "source": "https://iba-world.com/",
        "source_name": "IBA Official Cocktails",
        "source_note": (
            "Specifications only (name, category, ingredient lines, method, garnish). Prose, video "
            "and photography are the IBA's and are not reproduced. Credit the International "
            "Bartenders Association wherever these recipes are shown (JJ-032)."
        ),
        "extracted_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "count": len(cocktails),
        "cocktails": cocktails,
    }

    with open("seed/iba_cocktails.json", "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)

    no_method = sum(1 for c in cocktails if not c["method"])
    no_glass = sum(1 for c in cocktails if not c["glass"])
    unamounted = sum(1 for c in cocktails for l in c["ingredient_lines"] if not l["amount"])
    total_lines = sum(len(c["ingredient_lines"]) for c in cocktails)

    grouped = collections.Counter(c["category"] for c in cocktails)

    print()
    print(f"Wrote {len(cocktails)} cocktails to seed/iba_cocktails.json")
    for category, n in sorted(grouped.items(), key=lambda kv: str(kv[0])):
        flag = "" if n == EXPECTED_PER_CATEGORY else f"  <- expected {EXPECTED_PER_CATEGORY}"
        print(f"  {str(category):<22} {n}{flag}")
    if sorted(grouped.values()) != [EXPECTED_PER_CATEGORY] * 3:
        print("  WARNING: the three official groups are not 34 each - the breadcrumb parse is off.")
    print(f"Method detected for {len(cocktails) - no_method}/{len(cocktails)}")
    print(f"Glass detected for  {len(cocktails) - no_glass}/{len(cocktails)}")
    print(f"Lines with an amount: {total_lines - unamounted}/{total_lines}")
    if skipped:
        print(f"Skipped {len(skipped)}: {', '.join(skipped)}")


if __name__ == "__main__":
    main()
