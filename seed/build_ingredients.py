"""
Turn the raw ingredient vocabulary of both extractions into JiggerJot's curated ingredient catalog.

Reads  seed/savoy_cocktails.json, seed/iba_cocktails.json  (raw material)
       seed/ingredient_map.json                             (the curation: what each name IS)
Writes src/Infrastructure/Persistence/Seed/ingredients.json (shipped, embedded, seeded at startup)

The point of this script is the COVERAGE REPORT, not the file it writes. 395 distinct raw names come
out of the two books, and the failure mode of a job like this is silence: a name nobody mapped is a
recipe line that will not resolve in SEED-3, and if the build says nothing you find out months later
when a drink shows up missing an ingredient. So every raw name must be accounted for — mapped to a
canonical ingredient, or explicitly excluded with a reason — and this exits non-zero when one is not.

Normalisation happens HERE, at curation time, and not in the app. The shipped file is a plain list of
curated ingredients; the aliases and the tidying that got us there stay in this workspace, because
they are facts about two particular books rather than anything JiggerJot needs at runtime.

    python seed/build_ingredients.py
"""
import json
import pathlib
import re
import sys
import unicodedata

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCES = [ROOT / "seed/savoy_cocktails.json", ROOT / "seed/iba_cocktails.json"]
MAP_PATH = ROOT / "seed/ingredient_map.json"
OUT_PATH = ROOT / "src/Infrastructure/Persistence/Seed/ingredients.json"
LOOKUPS_PATH = ROOT / "src/Infrastructure/Persistence/Seed/lookups.json"

# Leading noise the books add to a name without changing what the thing IS. "Fresh lemon juice",
# "Freshly Squeezed Lemon Juice" and "Lemon juice" are one ingredient; carrying three aliases for
# every juice in the catalog would bury the curation in clerical work.
LEADING_NOISE = re.compile(
    r"^(?:"
    r"a\s+|an\s+|the\s+|of\s+|"
    r"fresh(?:ly)?\s+(?:squeezed\s+)?|fresh\s+squeezed\s+|"
    r"chilled\s+|raw\s+|whole\s+|strong\s+|hot\s+|cold\s+|good\s+|"
    r"top\s+(?:up\s+)?(?:with\s+)?|fill\s+up\s+with\s+|"
    r"splash\s+of\s+|dash\s+of\s+|dashes\s+of\s+|pinch\s+of\s+|"
    r"few\s+(?:drops?|dashes)\s+(?:of\s+)?|drops?\s+of\s+|"
    r"pcs?\s+|to\s+\d+\s+pcs\s+|\d+\s+"
    r")+",
    re.IGNORECASE,
)

# Trailing noise: parentheticals, footnote marks, and the "(optional)" the IBA uses for egg white.
TRAILING_NOISE = re.compile(r"\s*(?:\([^)]*\)|\*+|,.*)$")


def fold_accents(s):
    """cachaca == cachaça, kahlua == Kahlúa, creme == crème. The books disagree with each other and
    with themselves about accents, and the scrape's encoding adds its own noise, so identity here is
    the letters and nothing else. The CURATED names keep their accents - only the matching key loses
    them."""
    decomposed = unicodedata.normalize("NFKD", s)
    return "".join(c for c in decomposed if not unicodedata.combining(c))


def normalise(raw):
    """A raw book name reduced to the key the curation map is written against."""
    s = unicodedata.normalize("NFKC", raw or "")
    s = fold_accents(s.replace("’", "'").replace("‘", "'"))
    s = re.sub(r"\s+", " ", s).strip().lower()
    s = re.sub(r"^[^0-9a-z]+", "", s)   # a stray leading dash or bullet the scrape kept

    previous = None
    while previous != s:
        previous = s
        s = TRAILING_NOISE.sub("", s).strip()
        s = LEADING_NOISE.sub("", s).strip()

    return re.sub(r"\s+", " ", s).strip(" .,-")


def raw_names():
    """Every distinct ingredient name across both extractions, with how often it appears."""
    counts = {}
    for path in SOURCES:
        for cocktail in json.loads(path.read_text(encoding="utf-8"))["cocktails"]:
            for line in cocktail["ingredient_lines"]:
                name = (line.get("ingredient") or "").strip()
                if name:
                    counts[name] = counts.get(name, 0) + 1
    return counts


def load_curation():
    data = json.loads(MAP_PATH.read_text(encoding="utf-8"))
    ingredients = data["ingredients"]

    lookup = {}
    for item in ingredients:
        for key in [item["name"]] + item.get("aliases", []):
            k = normalise(key)
            if k in lookup and lookup[k]["name"] != item["name"]:
                raise SystemExit(
                    f"Alias '{key}' is claimed by both '{lookup[k]['name']}' and '{item['name']}'.")
            lookup[k] = item

    excluded = {normalise(k): v for k, v in data["excluded"].items()}
    return data, ingredients, lookup, excluded


def check_categories(ingredients):
    """Every ingredient must sit under a category that SEED-1 actually seeds."""
    lookups = json.loads(LOOKUPS_PATH.read_text(encoding="utf-8"))
    parents = {c["name"]: set(c["children"]) for c in lookups["ingredientCategories"]}

    problems = []
    for item in ingredients:
        parent, child = item["category"], item.get("subcategory")
        if parent not in parents:
            problems.append(f"{item['name']}: unknown category '{parent}'")
        elif child and child not in parents[parent]:
            problems.append(f"{item['name']}: '{child}' is not a subcategory of '{parent}'")
    return problems


def main():
    counts = raw_names()
    data, ingredients, lookup, excluded = load_curation()

    unmapped = []
    mapped_lines = excluded_lines = 0
    for name, n in counts.items():
        key = normalise(name)
        if key in lookup:
            mapped_lines += n
        elif key in excluded:
            excluded_lines += n
        else:
            unmapped.append((n, name, key))

    problems = check_categories(ingredients)

    total_lines = sum(counts.values())
    print(f"raw names        {len(counts)}")
    print(f"curated          {len(ingredients)} ingredients")
    print(f"lines mapped     {mapped_lines}/{total_lines}")
    print(f"lines excluded   {excluded_lines}/{total_lines}  (ice, water, parse artifacts)")

    if problems:
        print("\nCATEGORY PROBLEMS — an ingredient points at a category SEED-1 does not seed:")
        for p in problems:
            print(f"  {p}")

    if unmapped:
        print(f"\nUNMAPPED — {len(unmapped)} names, {sum(n for n, _, _ in unmapped)} lines. "
              f"Add each to ingredient_map.json as an ingredient, an alias, or an exclusion:")
        for n, name, key in sorted(unmapped, reverse=True):
            print(f"  {n:4}  {name!r}   (normalises to {key!r})")

    if problems or unmapped:
        return 1

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    shipped = {
        "$comment": data["$comment"],
        "ingredients": [
            {k: v for k, v in item.items() if k in ("name", "category", "subcategory")}
            for item in sorted(ingredients, key=lambda i: i["name"].lower())
        ],
    }
    with open(OUT_PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(shipped, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print(f"\nWrote {len(shipped['ingredients'])} ingredients to "
          f"{OUT_PATH.relative_to(ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
