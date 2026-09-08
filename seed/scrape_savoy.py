"""
Extract the Savoy Cocktail Database (savoycocktaildatabase.com) into a structured JSON file.

Uses the site's public WordPress REST API (read-only, no auth) rather than scraping every
individual page — far fewer HTTP requests against the target server.

For each cocktail, captures: name, chapter/category, raw ingredient lines, a best-effort
parsed (quantity, unit, ingredient) breakdown per line, canonical ingredient names (from the
site's own tags), full instructions text, any historical/serving notes, and a best-effort
derived glass + method parsed out of the free-text instructions.
"""
import html
import json
import re
import time
import unicodedata

import requests
from bs4 import BeautifulSoup

BASE = "https://savoycocktaildatabase.com/wp-json/wp/v2"
HEADERS = {"User-Agent": "jigger-jot-data-import/1.0 (personal project; contact: argamboad@gmail.com)"}
PER_PAGE = 100
SLEEP_BETWEEN_REQUESTS = 0.3


def fetch_all(endpoint, params=None):
    params = dict(params or {})
    params["per_page"] = PER_PAGE
    items = []
    page = 1
    while True:
        params["page"] = page
        r = requests.get(f"{BASE}/{endpoint}", params=params, headers=HEADERS, timeout=30)
        r.raise_for_status()
        batch = r.json()
        if not batch:
            break
        items.extend(batch)
        total_pages = int(r.headers.get("X-WP-TotalPages", page))
        if page >= total_pages:
            break
        page += 1
        time.sleep(SLEEP_BETWEEN_REQUESTS)
    return items


def clean_text(s):
    s = html.unescape(s or "")
    s = unicodedata.normalize("NFKC", s)
    s = re.sub(r"\s+", " ", s).strip()
    return s


# --- ingredient-line parsing -------------------------------------------------

QTY_RE = r"(\d+(?:[-\s]\d+)?/\d+|\d+(?:\.\d+)?|a|an|one|two|three|four|five|six)"
UNIT_WORDS = (
    "dash(?:es)?|teaspoonful?s?|tablespoonful?s?|wine[- ]?glass(?:es)?(?:ful)?s?|"
    "liqueur glass(?:es)?(?:ful)?|glass(?:es)?(?:ful)?|pony(?:s|ies)?|jigger(?:s|ful)?|"
    "bottle(?:s|ful)?|lump(?:s)?|slice(?:s)?|piece(?:s)?|leaf|leaves|sprig(?:s)?|"
    "twist(?:s)?|drop(?:s)?|part(?:s)?|pint(?:s)?|quart(?:s)?|gill(?:s)?|"
    "cup(?:s|ful)?|barspoon(?:s|ful)?"
)

LEADING_UNIT_RE = re.compile(
    rf"^(?P<qty>{QTY_RE}(?:\s*(?:or|and|-|to)\s*{QTY_RE})?)\s+"
    rf"(?P<descr>(?:small|large|medium(?:\s*size(?:d)?)?))?\s*"
    rf"(?P<unit>(?:{UNIT_WORDS}))\s*(?:of\s+)?(?P<rest>.+)$",
    re.IGNORECASE,
)
LEADING_QTY_ONLY_RE = re.compile(rf"^(?P<qty>{QTY_RE})\s+(?P<rest>.+)$", re.IGNORECASE)
JUICE_OF_RE = re.compile(
    rf"^(?:the\s+)?juice\s+of\s+(?P<qty>{QTY_RE})\s+(?P<rest>.+)$", re.IGNORECASE
)
WHITE_OF_RE = re.compile(
    rf"^(?:the\s+)?white(?:s)?\s+of\s+(?P<qty>{QTY_RE})\s+(?P<rest>.+)$", re.IGNORECASE
)
YOLK_OF_RE = re.compile(
    rf"^(?:the\s+)?yolk(?:s)?\s+of\s+(?P<qty>{QTY_RE})\s+(?P<rest>.+)$", re.IGNORECASE
)


def strip_trailing_ingredient_noise(rest):
    return rest.strip().rstrip(".").strip()


def match_tag_in_text(text, tag_names_sorted):
    low = text.lower()
    for tag in tag_names_sorted:
        if tag.lower() in low:
            return tag
    return None


def parse_ingredient_line(raw, tag_names_sorted):
    line = {"raw": raw, "quantity": None, "unit": None, "ingredient": None}

    m = JUICE_OF_RE.match(raw)
    if m:
        fruit = strip_trailing_ingredient_noise(m.group("rest"))
        line["quantity"] = m.group("qty")
        line["unit"] = "juice of"
        line["ingredient"] = match_tag_in_text(fruit, tag_names_sorted) or f"{fruit} Juice"
        return line

    m = WHITE_OF_RE.match(raw)
    if m:
        line["quantity"] = m.group("qty")
        line["unit"] = "white of"
        line["ingredient"] = match_tag_in_text(raw, tag_names_sorted) or "Egg White"
        return line

    m = YOLK_OF_RE.match(raw)
    if m:
        line["quantity"] = m.group("qty")
        line["unit"] = "yolk of"
        line["ingredient"] = match_tag_in_text(raw, tag_names_sorted) or "Egg Yolk"
        return line

    m = LEADING_UNIT_RE.match(raw)
    if m:
        line["quantity"] = m.group("qty").strip()
        descr = m.group("descr")
        unit = clean_text(m.group("unit"))
        line["unit"] = clean_text(f"{descr} {unit}") if descr else unit
        rest = strip_trailing_ingredient_noise(m.group("rest"))
        line["ingredient"] = match_tag_in_text(rest, tag_names_sorted) or rest
        return line

    m = LEADING_QTY_ONLY_RE.match(raw)
    if m:
        line["quantity"] = m.group("qty").strip()
        rest = strip_trailing_ingredient_noise(m.group("rest"))
        line["ingredient"] = match_tag_in_text(rest, tag_names_sorted) or rest
        return line

    line["ingredient"] = match_tag_in_text(raw, tag_names_sorted)
    return line


# --- method / glass extraction from free-text instructions -------------------

METHOD_PATTERNS = [
    ("shake", r"\bshake(?:n|s)?\b"),
    ("stir", r"\bstir(?:red|s)?\b"),
    ("muddle", r"\bmuddle(?:d)?\b"),
    ("blend", r"\bblend(?:ed)?\b"),
    ("build", r"\bbuild(?:ing)?\b|\bpour(?:ed)? directly\b"),
    ("float", r"\bfloat(?:ed|ing)?\b"),
    ("layer", r"\blayer(?:ed)?\b"),
    ("boil", r"\bboil(?:ed|ing)?\b"),
    ("dissolve", r"\bdissolve(?:d)?\b"),
]

GLASS_RE = re.compile(
    r"\b((?:small|medium|large|long|old[- ]fashioned|cocktail|highball|sherry|claret|"
    r"champagne|wine|punch|liqueur|beer|silver|copper|pousse[- ]caf[eé]|bar|hot|"
    r"medium[- ]size(?:d)?)\s+)*"
    r"(glass(?:es)?|tumblers?|goblets?|flutes?|mugs?|steins?|cups?)\b",
    re.IGNORECASE,
)


def extract_method(text):
    low = text.lower()
    for name, pattern in METHOD_PATTERNS:
        if re.search(pattern, low):
            return name
    return None


def extract_glass(text):
    m = GLASS_RE.search(text)
    if not m:
        return None
    return clean_text(m.group(0)).lower()


YIELD_RE = re.compile(r"^\(\s*(\d+)\s+people\s*\)$", re.IGNORECASE)


def parse_content(content_html, tag_names_sorted):
    soup = BeautifulSoup(content_html, "html.parser")

    ingredient_lines = []
    for li in soup.select("ul.wp-block-list li"):
        text = clean_text(li.get_text(" "))
        if text:
            ingredient_lines.append(text)

    instructions_parts = []
    notes_parts = []
    yield_text = None
    for p in soup.select("p.wp-block-paragraph"):
        text = clean_text(p.get_text(" "))
        if not text:
            continue
        ym = YIELD_RE.match(text)
        if ym:
            yield_text = text
            continue
        is_note = p.find("em") is not None and len(p.find_all(string=True, recursive=False)) == 0
        # a paragraph whose entire visible text is wrapped in <em> is treated as a historical note
        only_em = all(
            (child.name == "em") for child in p.find_all(recursive=False)
        ) if p.find_all(recursive=False) else False
        if only_em and p.find("em"):
            notes_parts.append(text)
        else:
            instructions_parts.append(text)

    instructions = " ".join(instructions_parts).strip()
    notes = " ".join(notes_parts).strip() or None

    parsed_ingredients = [parse_ingredient_line(line, tag_names_sorted) for line in ingredient_lines]

    return {
        "ingredient_lines": parsed_ingredients,
        "instructions": instructions or None,
        "notes": notes,
        "yield": yield_text,
        "method": extract_method(instructions) if instructions else None,
        "glass": extract_glass(instructions) if instructions else None,
    }


def main():
    print("Fetching categories...")
    categories = {c["id"]: clean_text(c["name"]) for c in fetch_all("categories")}
    print(f"  {len(categories)} categories")

    print("Fetching tags (ingredient names)...")
    tags = {t["id"]: clean_text(t["name"]) for t in fetch_all("tags")}
    print(f"  {len(tags)} tags")
    all_tag_names_sorted = sorted(set(tags.values()), key=len, reverse=True)

    print("Fetching posts (this is the slow part, ~9 requests of 100 each)...")
    posts = fetch_all("posts", params={"_fields": "id,slug,link,title,content,categories,tags"})
    print(f"  {len(posts)} posts")

    cocktails = []
    unmatched_ingredient_count = 0
    total_ingredient_count = 0
    no_glass = 0
    no_method = 0

    for post in posts:
        name = clean_text(post["title"]["rendered"])
        post_tag_names = [tags.get(t) for t in post.get("tags", []) if tags.get(t)]
        # sort this post's own tags longest-first for the most specific substring match
        local_tag_names_sorted = sorted(set(post_tag_names), key=len, reverse=True) or all_tag_names_sorted

        parsed = parse_content(post["content"]["rendered"], local_tag_names_sorted)

        for line in parsed["ingredient_lines"]:
            total_ingredient_count += 1
            if not line["ingredient"]:
                unmatched_ingredient_count += 1
        if not parsed["glass"]:
            no_glass += 1
        if not parsed["method"]:
            no_method += 1

        cat_names = [categories.get(c) for c in post.get("categories", []) if categories.get(c)]

        cocktails.append(
            {
                "id": post["id"],
                "name": name,
                "slug": post["slug"],
                "url": post["link"],
                "categories": cat_names,
                "ingredients": post_tag_names,
                "ingredient_lines": parsed["ingredient_lines"],
                "instructions": parsed["instructions"],
                "method": parsed["method"],
                "glass": parsed["glass"],
                "notes": parsed["notes"],
                "yield": parsed["yield"],
            }
        )

    cocktails.sort(key=lambda c: c["name"].lower())

    out = {
        "source": "https://savoycocktaildatabase.com/",
        "source_book": "The 1930 Savoy Cocktail Book",
        "extracted_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "count": len(cocktails),
        "cocktails": cocktails,
    }

    out_path = "savoy_cocktails.json"
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)

    print()
    print(f"Wrote {len(cocktails)} cocktails to {out_path}")
    print(f"Glass detected for {len(cocktails) - no_glass}/{len(cocktails)} cocktails")
    print(f"Method detected for {len(cocktails) - no_method}/{len(cocktails)} cocktails")
    print(
        f"Ingredient lines matched to a canonical tag name: "
        f"{total_ingredient_count - unmatched_ingredient_count}/{total_ingredient_count}"
    )


if __name__ == "__main__":
    main()
