"""
Turn both raw extractions into JiggerJot's shared recipe catalog.

Reads  seed/savoy_cocktails.json, seed/iba_cocktails.json
       seed/ingredient_map.json                              (SEED-2's curation, reused verbatim)
Writes src/Infrastructure/Persistence/Seed/cocktails.json    (shipped, embedded, seeded at startup)

Like build_ingredients.py, the value here is the report rather than the file: every recipe line must
resolve to a curated ingredient, a known unit and a known glass and method, or be dropped for a
reason this script names out loud. A recipe that quietly loses a line is a recipe that quietly stops
being makeable.

Three judgements are worth knowing about before reading the code.

**Glass and method may be absent (JJ-034).** A quarter of the catalog states no glass or states one
that is not a glass type - the Savoy's "medium size glass" and bare "glass" are 165 recipes between
them. Those become null. Mapping them to something plausible would put a fact in the database that
nobody wrote down.

**Proportional amounts are stored as authored (JJ-007).** A 1930 recipe reading "2/3 Absinthe, 1/6
Gin" has no absolute volume in it, so the fraction is stored against the neutral `part` unit. The
tempting alternative - scaling to a common denominator so it reads "4 parts / 1 part" - preserves the
ratio exactly and is genuinely nicer to read, but it changes the stored number, and JJ-007 says the
stored number is what the author wrote. Rendering a decimal back as a fraction is a display problem.

**Four names appear in both books.** Champagne Cocktail, Gin Fizz, John Collins and Singapore Sling.
Both are kept. They are different drinks that share a name, the source column is what tells them
apart, and the cocktail name index is deliberately not unique.

    python seed/build_cocktails.py
"""
import collections
import json
import pathlib
import re
import sys
from fractions import Fraction

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from build_ingredients import normalise  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parents[1]
MAP_PATH = ROOT / "seed/ingredient_map.json"
LOOKUPS_PATH = ROOT / "src/Infrastructure/Persistence/Seed/lookups.json"
OUT_PATH = ROOT / "src/Infrastructure/Persistence/Seed/cocktails.json"


# ── the starter set ─────────────────────────────────────────────────────────────────────────────
# The extraction produces 969 recipes. That is the right eventual catalog and the wrong thing to
# develop against: every test assertion ends up being a claim about nine hundred rows rather than
# about behaviour, seeding costs seconds on every run, and a change to the data breaks tests that had
# nothing to do with it. So the shipped catalog is a small set by default, and the full one is a flag
# away:
#
#     python seed/build_cocktails.py --full
#
# The picks are not arbitrary. Between them they cover every SHAPE the model has to handle, which is
# what the tests actually need:
#
#   metric, absolute amounts ................. every IBA drink
#   proportional 1930 amounts ("2/3") ........ absinthe-special-cocktail
#   ...and whole ones ("4 Parts") ............ hawaiian-cocktail
#   unmeasured lines from a tag list ......... alfonso-cocktail
#   one name in two books .................... gin-fizz (IBA + Savoy)
#   one name twice in ONE book ............... mr-manhattan-cocktail + -2
#   no glass and no method recorded .......... martini-special-cocktail
#   an optional garnish line ................. mojito, old-fashioned
#   a substitution in play ................... white-lady (Cointreau ↔ Curaçao)
#   modern spirits Savoy never had ........... margarita, espresso-martini, cosmopolitan
#
# Enough drinks to page (20 per page) and few enough to reason about.
STARTER_SET = {
    "iba": [
        "negroni", "dry-martini", "white-lady", "daiquiri", "margarita", "espresso-martini",
        "mojito", "manhattan", "whiskey-sour", "cosmopolitan", "boulevardier", "last-word",
        "gin-fizz", "old-fashioned", "americano", "aviation", "sidecar", "mai-tai",
        "caipirinha", "paloma", "bloody-mary", "french-75", "sazerac", "tommys-margarita",
    ],
    "savoy": [
        "gin-fizz", "mr-manhattan-cocktail", "mr-manhattan-cocktail-2",
        "absinthe-special-cocktail", "alfonso-cocktail", "martini-special-cocktail",
        "hawaiian-cocktail",
    ],
}

FULL = "--full" in sys.argv

SOURCES = [
    {
        "key": "savoy",
        "path": ROOT / "seed/savoy_cocktails.json",
        "name": "The Savoy Cocktail Book",
        "year": 1930,
        "url": "https://savoycocktaildatabase.com/",
        "attribution": (
            "Recipes from The Savoy Cocktail Book (Harry Craddock, 1930), which entered the US public "
            "domain on 1 January 2026. Transcribed via savoycocktaildatabase.com."
        ),
    },
    {
        "key": "iba",
        "path": ROOT / "seed/iba_cocktails.json",
        "name": "IBA Official Cocktails",
        "year": None,
        "url": "https://iba-world.com/",
        "attribution": (
            "Specifications from the International Bartenders Association official cocktail list. "
            "Ingredients, measures and method only; the IBA's own writing is not reproduced."
        ),
    },
]

# ── glass ───────────────────────────────────────────────────────────────────────────────────────
# Left of the arrow is what the books say. Anything not here resolves to null, on purpose.
GLASS = {
    "cocktail glass": "Cocktail glass", "cocktail glasses": "Cocktail glass",
    "martini cocktail glass": "Cocktail glass",
    "coupe glass": "Coupe",
    "old fashioned glass": "Rocks glass", "old-fashioned glass": "Rocks glass",
    "rocks glass": "Rocks glass",
    "highball glass": "Highball glass", "highball glasses": "Highball glass",
    "long tumbler": "Highball glass", "large tumbler": "Highball glass",
    "long glass": "Highball glass", "tumbler glass": "Highball glass",
    "collins glass": "Collins glass",
    "flute glass": "Champagne flute",
    "wine glass": "Wine glass", "large wine glass": "Wine glass", "small wine glass": "Wine glass",
    "medium-size wine glass": "Wine glass", "medium sized wine glass": "Wine glass",
    "wine cup": "Wine glass", "goblet glass": "Wine glass",
    "sherry glass": "Sherry glass",
    "liqueur glass": "Liqueur glass",
    "hurricane glass": "Hurricane glass",
    "tiki glass": "Tiki mug",
    "julep stainless steel cup": "Julep cup", "julep cocktail cup": "Julep cup",
}

# Named here so the report can separate "the book said nothing" from "the book said something we
# refuse to guess at" — two different data-quality stories, and only the second is ours to improve.
GLASS_TOO_VAGUE = {
    "glass", "glasses", "medium size glass", "medium-size glass", "medium-sized glass",
    "small glass", "large glass", "tumbler", "small tumbler", "small bar glass", "large bar glass",
    "cup", "mugs",
}

METHOD = {
    "shake": "Shake", "dry shake": "Dry shake", "stir": "Stir", "build": "Build",
    "muddle": "Muddle", "blend": "Blend", "swizzle": "Swizzle", "throw": "Throw",
    "layer": "Layer", "float": "Layer", "boil": "Heat", "dissolve": "Build",
}

UNIT = {
    "ml": "ml", "cl": "cl", "l": "l", "oz": "oz",
    "dash": "dash", "dashes": "dash", "drop": "drop", "drops": "drop",
    "bar spoon": "barspoon", "bar spoons": "barspoon", "barspoon": "barspoon",
    "teaspoon": "tsp", "teaspoons": "tsp", "tsp": "tsp",
    "teaspoonful": "tsp", "teaspoonfuls": "tsp",
    "tablespoon": "tbsp", "tablespoons": "tbsp", "tbsp": "tbsp", "tablespoonful": "tbsp",
    "splash": "splash", "pinch": "pinch",
    "cube": "cube", "lump": "cube", "lumps": "cube", "small lump": "cube", "small lumps": "cube",
    "piece": "piece", "pieces": "piece", "small piece": "piece", "small pieces": "piece",
    "slice": "slice", "slices": "slice", "wedge": "wedge", "wedges": "wedge",
    "sprig": "sprig", "sprigs": "sprig", "leaf": "leaf", "leaves": "leaf",
    "part": "part", "parts": "part",
    "pint": "pint", "quart": "quart", "cup": "cup", "gill": "gill", "pony": "oz",
    "glass": "glass", "glasses": "glass", "small glass": "glass", "large glass": "glass",
    "wineglass": "wineglass", "wineglasses": "wineglass", "wine glass": "wineglass",
    "wineglassful": "wineglass", "large wineglass": "wineglass",
    "liqueur glass": "liqueur glass", "liqueur glasses": "liqueur glass",
    "bottle": "bottle",
}

# Not units at all: "juice of 1 Lemon" and "white of 1 Egg" are sentence fragments, and the
# ingredient they qualify is already Lemon Juice or Egg White. The amount survives, the word does not.
NOT_UNITS = {"juice of", "white of", "yolk of"}

# Role is derived from what the ingredient IS, never tagged by hand (JJ-014 in spirit). Spirits are
# resolved separately below, because the FIRST spirit in a drink is its base and the rest are not.
ROLE_BY_CATEGORY = {
    "Bitters": "Bitters",
    "Juice": "Juice",
    "Syrup": "Syrup",
    "Soda and mixer": "Mixer",
    "Garnish": "Garnish",
    "Fruit": "Garnish",
    "Herb and spice": "Garnish",
    "Vermouth": "Modifier",
    "Fortified wine": "Modifier",
    "Liqueur": "Modifier",
    "Amaro and bitter": "Modifier",
    "Wine": "Modifier",
    "Beer and cider": "Modifier",
}
SPIRIT_CATEGORIES = {
    "Gin", "Whisky", "Rum", "Agave", "Brandy", "Vodka", "Absinthe and pastis", "Other spirits",
}


def load_ingredient_lookup():
    data = json.loads(MAP_PATH.read_text(encoding="utf-8"))
    lookup = {}
    for item in data["ingredients"]:
        for key in [item["name"]] + item.get("aliases", []):
            lookup[normalise(key)] = item
    excluded = {normalise(k) for k in data["excluded"]}
    return lookup, excluded


def parse_amount(raw):
    """'2/3' -> Fraction(2,3); '1.5' -> Fraction(3,2); anything unreadable -> None."""
    if raw is None:
        return None
    text = str(raw).strip().replace(",", ".")
    if not text:
        return None
    try:
        if " " in text and "/" in text:                 # "1 1/2"
            whole, frac = text.split(" ", 1)
            return Fraction(whole) + Fraction(frac)
        return Fraction(text)
    except (ValueError, ZeroDivisionError):
        return None


def resolve_line(line, lookup, excluded, report):
    """One raw line into (ingredient, amount, unit) — or None when it should not become a row."""
    name = (line.get("ingredient") or "").strip()
    key = normalise(name)

    if not key:
        report["blank_lines"] += 1
        return None
    if key in excluded:
        report["excluded_lines"] += 1
        return None
    ingredient = lookup.get(key)
    if ingredient is None:
        report["unresolved"][name] += 1
        return None

    raw_unit = (line.get("unit") or "").strip().lower()
    amount = parse_amount(line.get("quantity") if "quantity" in line else line.get("amount"))

    if raw_unit in NOT_UNITS:
        unit = None
    elif raw_unit:
        unit = UNIT.get(raw_unit)
        if unit is None:
            report["unknown_units"][raw_unit] += 1
            return None
    elif amount is not None and amount.denominator != 1:
        # A bare fraction with no unit is the Savoy's proportional style: two thirds OF THE DRINK.
        unit = "part"
    else:
        unit = None

    if amount is None:
        unit = None                                     # a unit with nothing to measure says nothing

    return {
        "ingredient": ingredient["name"],
        "category": ingredient["category"],
        "amount": None if amount is None else round(float(amount), 4),
        "unit": unit,
    }


def assign_roles(lines):
    """Base is the first spirit in the drink; every later spirit is a modifier (JJ-010)."""
    seen_spirit = False
    for line in lines:
        category = line.pop("category")
        if category in SPIRIT_CATEGORIES:
            line["role"] = "Modifier" if seen_spirit else "Base"
            seen_spirit = True
        else:
            line["role"] = ROLE_BY_CATEGORY.get(category, "Other")
        # A garnish never blocks makeability (JJ-009); everything else does until told otherwise.
        line["isRequired"] = line["role"] != "Garnish"


def build(source, lookup, excluded, report):
    raw = json.loads(source["path"].read_text(encoding="utf-8"))["cocktails"]

    if not FULL:
        keep = set(STARTER_SET[source["key"]])
        raw = [c for c in raw if c["slug"] in keep]
        missing = keep - {c["slug"] for c in raw}
        if missing:
            # A slug that stopped existing would silently shrink the catalog and take a test's
            # premise with it, so say so rather than quietly emitting fewer rows.
            report["missing_starters"].extend(f"{source['key']}:{m}" for m in sorted(missing))

    out = []

    for c in raw:
        # 60 Savoy recipes are written as prose - "Put on the fire in a saucepan one quart of Ale" -
        # so the line parser found nothing in them. The site's own ingredient TAGS did, and those are
        # enough: makeability is a question about which ingredients a drink needs, not how much of
        # each (JJ-003), so these recipes work fully with unmeasured lines and their quantities stay
        # readable in the instructions. Dropping 60 real cocktails to avoid an empty amount column
        # would be the worse trade.
        raw_lines = c["ingredient_lines"]
        if not raw_lines and c.get("ingredients"):
            raw_lines = [{"ingredient": tag, "quantity": None, "unit": None}
                         for tag in c["ingredients"]]
            report["from_tags"] += 1

        lines = []
        for raw_line in raw_lines:
            resolved = resolve_line(raw_line, lookup, excluded, report)
            if resolved:
                resolved["displayOrder"] = len(lines)
                lines.append(resolved)

        if not lines:
            report["empty"].append(f"{source['key']}:{c['name']}")
            continue

        assign_roles(lines)

        glass_raw = (c.get("glass") or "").strip().lower()
        glass = GLASS.get(glass_raw)
        if glass is None:
            report["no_glass" if not glass_raw else
                   "vague_glass" if glass_raw in GLASS_TOO_VAGUE else "unknown_glass"] += 1

        method = METHOD.get((c.get("method") or "").strip().lower())
        if method is None:
            report["no_method"] += 1

        instructions = (c.get("instructions") or "").strip() or None
        garnish = (c.get("garnish") or "").strip() or None
        if garnish:
            instructions = f"{instructions} {garnish}".strip() if instructions else garnish

        out.append({
            # The source's own slug, not the name, is what identifies a recipe. The Savoy has
            # "Mr. Manhattan Cocktail" twice - once in the main chapter and once among the
            # Prohibition cocktails, with mint and sugar the second time. Keyed on the name, the two
            # would collide and one would vanish; keyed on the slug they are what they are, two
            # recipes that happen to share a title.
            "slug": c["slug"],
            "name": c["name"],
            "source": source["name"],
            "glassType": glass,
            "method": method,
            "servingType": "FullDrink",
            "instructions": instructions,
            "lines": lines,
        })

    return out


def main():
    lookup, excluded = load_ingredient_lookup()
    report = {
        "excluded_lines": 0, "blank_lines": 0, "from_tags": 0, "missing_starters": [],
        "unresolved": collections.Counter(),
        "unknown_units": collections.Counter(), "empty": [],
        "no_glass": 0, "vague_glass": 0, "unknown_glass": 0, "no_method": 0,
    }

    cocktails = []
    for source in SOURCES:
        cocktails.extend(build(source, lookup, excluded, report))

    keys = collections.Counter((c["source"], c["slug"]) for c in cocktails)
    collisions = [k for k, n in keys.items() if n > 1]
    if collisions:
        print(f"SLUG COLLISIONS within a source — recipe identity is derived from these, so two rows "
              f"would become one: {collisions}")
        return 1

    if report["missing_starters"]:
        print("STARTER SLUGS NOT FOUND in the extraction — the set names recipes that no longer "
              f"exist: {', '.join(report['missing_starters'])}")
        return 1

    total_lines = sum(len(c["lines"]) for c in cocktails)
    print(f"catalog          {'FULL' if FULL else 'starter set'}")
    print(f"cocktails        {len(cocktails)}")
    print(f"recipe lines     {total_lines}")
    print(f"lines dropped    {report['excluded_lines']} (ice, water, the deliberate exclusions)"
          f" + {report['blank_lines']} the scrape left blank")
    print(f"from tag lists   {report['from_tags']} recipes whose prose defeated the line parser")
    print(f"glass: null      {report['no_glass']} unstated + {report['vague_glass']} too vague to map")
    print(f"method: null     {report['no_method']}")

    if report["unknown_glass"]:
        print(f"\nUNKNOWN GLASS on {report['unknown_glass']} recipes — a glass name that is neither "
              f"mapped nor listed as too vague. Decide which it is.")
    if report["unresolved"]:
        print(f"\nUNRESOLVED INGREDIENTS — {len(report['unresolved'])} names. SEED-2's map should "
              f"already cover every one of these:")
        for name, n in report["unresolved"].most_common():
            print(f"  {n:4}  {name!r}")
    if report["unknown_units"]:
        print(f"\nUNKNOWN UNITS — add to UNIT or NOT_UNITS:")
        for unit, n in report["unknown_units"].most_common():
            print(f"  {n:4}  {unit!r}")
    if report["empty"]:
        print(f"\nRECIPES WITH NO USABLE LINES ({len(report['empty'])}), dropped: "
              f"{', '.join(report['empty'][:10])}")

    if report["unresolved"] or report["unknown_units"] or report["unknown_glass"]:
        return 1

    shipped = {
        "$comment": [
            "The shared recipe catalog (JJ-012), built by seed/build_cocktails.py from the two",
            "extractions in seed/. Do not hand-edit: the next build overwrites it.",
            "",
            "Glass and method are null where the source did not say, or said something that is not a",
            "glass (JJ-034). Amounts are as authored, so a proportional 1930 recipe carries fractions",
            "against the neutral 'part' unit rather than invented millilitres (JJ-007).",
            "",
            "This is the STARTER SET, not the whole extraction: a small catalog chosen to cover every",
            "shape the model handles, so that slice work is about behaviour rather than about nine",
            "hundred rows. `python seed/build_cocktails.py --full` emits all of them.",
        ],
        "sources": [
            {k: v for k, v in s.items() if k in ("name", "year", "url", "attribution")}
            for s in SOURCES
        ],
        "cocktails": sorted(cocktails, key=lambda c: (c["name"].lower(), c["source"], c["slug"])),
    }
    with open(OUT_PATH, "w", encoding="utf-8", newline="\n") as f:
        json.dump(shipped, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print(f"\nWrote {len(cocktails)} cocktails to {OUT_PATH.relative_to(ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
