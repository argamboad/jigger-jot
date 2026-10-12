"""
Turn both raw extractions into JiggerJot's shared recipe catalog.

Reads  seed/savoy_cocktails.json, seed/iba_cocktails.json
       seed/ingredient_map.json                              (SEED-2's curation, reused verbatim)
Writes src/Infrastructure/Persistence/Seed/cocktails.json    (shipped, embedded, seeded at startup)

Like build_ingredients.py, the value here is the report rather than the file: every recipe line must
resolve to a curated ingredient, a known unit and a known glass and method, or be dropped for a
reason this script names out loud. A recipe that quietly loses a line is a recipe that quietly stops
being makeable.

The shipped catalog is CURATED (JJ-043): one recipe per drink. Four judgements are worth knowing
about before reading the code.

**The IBA list wins where both books have a drink.** It is the accepted modern spec. The Savoy adds
the drinks the IBA does not have. Which Savoy recipe is "the same drink" is decided by hand, drink by
drink, in seed/overlap.json - a name match would delete real drinks (Corpse Reviver No. 1 is not the
IBA #2) - and the build fails on any name-match candidate that file does not settle.

**A glass is required, period (JJ-043, retiring JJ-034's "optional").** Every IBA drink ships, and
every one ends up with a glass: from the source's glass, from its own instructions where they name one
(GLASS_FROM_TEXT, each phrase checked to occur), or, for five drinks whose source names none, from a
curator's call that is labelled as one (GLASS_ASSIGNED). A Savoy recipe ships only if its source
names a glass; "medium size glass" and bare "glass" are not glasses, and nothing is guessed for it.

**A Savoy recipe must be a usable spec to ship.** A glass, a method, at least two required lines,
and an amount on every required line. That leaves out the prose recipes recovered from tag lists and
the book's how-to-make-a-Cobbler entries. The excluded list, with reasons, is written to
seed/savoy_excluded.txt so a reviewer can read what did not ship.

**Proportional amounts are stored as authored (JJ-007).** A 1930 recipe reading "2/3 Absinthe, 1/6
Gin" has no absolute volume in it, so the fraction is stored against the neutral `part` unit; the
seeder turns it into ounces (JJ-041).

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


OVERLAP_PATH = ROOT / "seed/overlap.json"
EXCLUDED_PATH = ROOT / "seed/savoy_excluded.txt"


# ── glass the source names in its prose (JJ-043) ────────────────────────────────────────────────
# The IBA site has no glass field; scrape_iba.py reads the glass out of the method text and is
# deliberately conservative about it, so fifteen drinks came back with none. Ten of them name their
# glass plainly in words the regex did not take. The phrase is kept beside the answer and the build
# fails if it stops occurring in the recipe's instructions, so every row here stays checkable.
GLASS_FROM_TEXT = {
    ("iba", "chartreuse-swizzle"): ("tall glass", "Highball glass"),
    ("iba", "dons-special-daiquiri"): ("footed copo glass", "Goblet"),
    ("iba", "french-75"): ("Champagne flute", "Champagne flute"),
    ("iba", "grand-margarita"): ("rock glass", "Rocks glass"),
    ("iba", "irish-coffee"): ("Irish coffee glass", "Irish coffee mug"),
    ("iba", "john-collins"): ("highball", "Highball glass"),
    ("iba", "missionarys-downfall"): ("Coppa grande", "Goblet"),
    ("iba", "pisco-punch"): ("large goblet", "Goblet"),
    ("iba", "planters-punch"): ("small tumbler", "Rocks glass"),
    ("iba", "three-dots-and-a-dash"): ("footed copo glass", "Goblet"),
}

# ── glass the source does not name: the curator's call (JJ-043, maintainer 2026-10-09) ────────────
# These five say "the glass", "a large glass", "a large Champagne glass" (flute or coupe?) or nothing,
# and the IBA site has nothing more. Dropping the Mojito and the Piña Colada from a cocktail app to
# keep a rule pure was the worse trade, so each gets the glass it is conventionally served in - and
# the reason is written here, so this is never mistaken for something the source said.
GLASS_ASSIGNED = {
    ("iba", "canchanchara"): ("Rocks glass", "source names none; served short over cracked ice"),
    ("iba", "champagne-cocktail"): ("Champagne flute", "source says 'large Champagne glass'"),
    ("iba", "kir"): ("Wine glass", "source says 'glass'; white wine topped up"),
    ("iba", "mojito"): ("Highball glass", "source says 'the glass'; a tall drink topped with soda"),
    ("iba", "pina-colada"): ("Hurricane glass", "source says 'a large glass'"),
}

# ── names (JJ-043, #179) ────────────────────────────────────────────────────────────────────────
# The Savoy titles every entry "X Cocktail"; that is the book's convention, not the drink's name, and
# it made the same drink look like a near-duplicate of the IBA's ("Dry Martini" / "Dry Martini
# Cocktail"). The word goes - unless what is left would be an ingredient or a bare word that is not a
# drink's name on its own ("Coffee", "Brandy", "Perfect").
KEEP_COCKTAIL = {
    "champagne", "coffee", "bacardi", "soda", "white", "silver", "perfect", "classic", "ideal",
    "club", "irish", "russian", "canadian", "chinese", "colonial", "imperial", "oriental",
    "parisian", "tropical", "london", "spring", "morning", "breakfast", "health", "opening",
    "prohibition", "victory", "liberty", "fancy", "dream", "elixir", "ship", "wax", "whip",
    "thunder", "virgin", "cape", "doctor", "president", "derby", "turf", "empire", "jewel",
    "kina", "melon", "picon", "cinzano", "xeres", "sloeberry", "‘flu",
}

# Where the book reuses a title for a different recipe, the second needs a name of its own; the
# qualifier comes from the book (the chapter it appears in), not from us.
NAME_OVERRIDE = {
    ("savoy", "mr-manhattan-cocktail-2"): "Mr. Manhattan (Prohibition)",
}

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
    "wine cup": "Wine glass", "goblet glass": "Goblet",
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
# AUTHORING-3: the write form suggests roles with the SAME rule, in Core (RecipeRoles.cs). Change the
# two together — SeedRolesParityTests holds every line of the emitted file to the Core copy.
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


def assign_roles(lines, source_key):
    """Base is the first spirit in the drink; every later spirit is a modifier (JJ-010)."""
    seen_spirit = False
    for line in lines:
        category = line.pop("category")
        if category in SPIRIT_CATEGORIES:
            line["role"] = "Modifier" if seen_spirit else "Base"
            seen_spirit = True
        else:
            line["role"] = ROLE_BY_CATEGORY.get(category, "Other")
        # The IBA lists its garnish in a field of its own (it reaches the instructions), so every line
        # of an IBA spec is something the drink is MADE of: the Mojito's mint, the Caipirinha's lime,
        # the Bellini's peach purée. Read by category they would be garnishes, optional, and a Mojito
        # would be makeable without mint (JJ-043). The Savoy has no garnish field - its peels, slices
        # and sprigs sit among the lines - so the category rule stands there.
        if source_key == "iba" and line["role"] == "Garnish":
            line["role"] = "Other"
        # A garnish never blocks makeability (JJ-009); everything else does until told otherwise.
        line["isRequired"] = line["role"] != "Garnish"


def name_key(name):
    """What makes two titles 'the same drink' to a reader: no qualifiers, no 'Cocktail'/'The', no
    punctuation or digits. Used only to FIND overlap candidates; the verdict is overlap.json's."""
    n = re.sub(r"\(.*?\)", "", name.lower())
    n = re.sub(r"\b(cocktail|the)\b", "", n)
    return re.sub(r"[^a-z]", "", n)


def shipped_name(source_key, slug, name, ingredient_keys):
    """The name a household reads. The IBA's names are already plain; the Savoy loses its suffix."""
    override = NAME_OVERRIDE.get((source_key, slug))
    if override:
        return override
    if source_key != "savoy":
        return name
    # "X Cocktail", "X Cocktail (No. 1)", "X (Dry) Cocktail". A "*" is the book's footnote mark, and a
    # title in quotes ("“Old Pal” Cocktail") is the book's typography, not part of the name.
    name = name.replace("*", "")
    stripped = re.sub(r"\s+Cocktail(?=\s*(\(|$))", "", name).strip()
    stem = re.sub(r"\s*\(.*?\)\s*", " ", stripped).strip()
    if stripped == name or not stem:
        return name
    # "Devil’s Cocktail" is the Devil's; "Devil’s" alone is a fragment.
    if (normalise(stem) in ingredient_keys or stem.lower() in KEEP_COCKTAIL
            or stem.endswith(("’s", "'s"))):
        return name
    quoted = re.fullmatch(r"“([^”]+)”(.*)", stripped)
    return f"{quoted.group(1)}{quoted.group(2)}" if quoted else stripped


def resolve_glass(source_key, c, report):
    """The glass, or None. Never a guess: the source's field, its own words, or a labelled call."""
    key = (source_key, c["slug"])
    if key in GLASS_ASSIGNED:
        report["glass_assigned"] += 1
        return GLASS_ASSIGNED[key][0]
    if key in GLASS_FROM_TEXT:
        phrase, glass = GLASS_FROM_TEXT[key]
        if phrase.lower() not in (c.get("instructions") or "").lower():
            report["stale_glass_text"].append(f"{source_key}:{c['slug']} no longer says {phrase!r}")
        report["glass_from_text"] += 1
        return glass

    glass_raw = (c.get("glass") or "").strip().lower()
    glass = GLASS.get(glass_raw)
    if glass is None:
        report["no_glass" if not glass_raw else
               "vague_glass" if glass_raw in GLASS_TOO_VAGUE else "unknown_glass"] += 1
    return glass


def shortfalls(recipe):
    """Why a Savoy recipe is not a usable spec - empty when it ships (JJ-043, #178)."""
    required = [line for line in recipe["lines"] if line["isRequired"]]
    reasons = []
    if recipe["glassType"] is None:
        reasons.append("no glass")
    if recipe["method"] is None:
        reasons.append("no method")
    if len(required) < 2:
        reasons.append("fewer than two required lines")
    if any(line["amount"] is None and line["unit"] is None for line in required):
        reasons.append("a required line without an amount")
    return reasons


def build(source, lookup, excluded, report):
    raw = json.loads(source["path"].read_text(encoding="utf-8"))["cocktails"]
    ingredient_keys = set(lookup)
    out = []

    for c in raw:
        # 60 Savoy recipes are written as prose - "Put on the fire in a saucepan one quart of Ale" -
        # so the line parser found nothing in them, and the site's ingredient TAGS stand in. They
        # carry no amounts, so the shipping bar leaves them out; they are kept this far so the
        # report can say so.
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

        assign_roles(lines, source["key"])

        glass = resolve_glass(source["key"], c, report)

        method = METHOD.get((c.get("method") or "").strip().lower())
        if method is None:
            report["no_method"] += 1

        instructions = (c.get("instructions") or "").strip() or None
        garnish = (c.get("garnish") or "").strip() or None
        if garnish and garnish.upper() != "N/A":
            instructions = f"{instructions} {garnish}".strip() if instructions else garnish

        out.append({
            # The source's own slug, not the name, is what identifies a recipe (its id derives from
            # source + slug), so a renamed title keeps its row.
            "slug": c["slug"],
            "name": shipped_name(source["key"], c["slug"], c["name"], ingredient_keys),
            "source": source["name"],
            "glassType": glass,
            "method": method,
            "servingType": "FullDrink",
            "instructions": instructions,
            "lines": lines,
            "_key": name_key(c["name"]),
        })

    return out


def check_overlap(iba, savoy):
    """Every name-match candidate is settled in overlap.json, and every entry there is real."""
    data = json.loads(OVERLAP_PATH.read_text(encoding="utf-8"))
    superseded, distinct = data["superseded"], data["distinct"]
    iba_slugs = {c["slug"] for c in iba}
    savoy_slugs = {c["slug"] for c in savoy}
    iba_keys = {c["_key"] for c in iba}
    problems = []

    for slug in sorted(set(superseded) & set(distinct)):
        problems.append(f"{slug} is both superseded and distinct")
    for slug in sorted(set(superseded) | set(distinct)):
        if slug not in savoy_slugs:
            problems.append(f"{slug} is not a Savoy recipe")
    for slug, entry in sorted(superseded.items()):
        if entry["by"] not in iba_slugs:
            problems.append(f"{slug} is superseded by {entry['by']!r}, which is not an IBA recipe")
    for c in savoy:
        if c["_key"] in iba_keys and c["slug"] not in superseded and c["slug"] not in distinct:
            problems.append(f"{c['slug']} ({c['name']}) looks like an IBA drink and is not reviewed")
    return set(superseded), problems


def main():
    lookup, excluded = load_ingredient_lookup()
    report = {
        "excluded_lines": 0, "blank_lines": 0, "from_tags": 0,
        "unresolved": collections.Counter(),
        "unknown_units": collections.Counter(), "empty": [],
        "no_glass": 0, "vague_glass": 0, "unknown_glass": 0, "no_method": 0,
        "glass_from_text": 0, "glass_assigned": 0, "stale_glass_text": [],
    }

    built = {source["key"]: build(source, lookup, excluded, report) for source in SOURCES}
    iba, savoy = built["iba"], built["savoy"]

    superseded, overlap_problems = check_overlap(iba, savoy)
    if overlap_problems:
        print("OVERLAP NOT SETTLED - review these in seed/overlap.json:")
        for problem in overlap_problems:
            print(f"  {problem}")
        return 1

    iba_without_glass = [c["slug"] for c in iba if c["glassType"] is None]
    if iba_without_glass:
        print(f"IBA DRINKS WITHOUT A GLASS - every IBA drink ships, so each needs one in "
              f"GLASS_FROM_TEXT or GLASS_ASSIGNED: {', '.join(iba_without_glass)}")
        return 1
    if report["stale_glass_text"]:
        print("GLASS_FROM_TEXT PHRASES NOT FOUND in the instructions they were read from:")
        for line in report["stale_glass_text"]:
            print(f"  {line}")
        return 1

    savoy_shipped, savoy_excluded = [], []
    why_counts = collections.Counter()
    for c in savoy:
        if c["slug"] in superseded:
            continue
        reasons = shortfalls(c)
        if reasons:
            savoy_excluded.append((c["slug"], c["name"], reasons))
            why_counts.update(reasons)
        else:
            savoy_shipped.append(c)

    cocktails = iba + savoy_shipped

    keys = collections.Counter((c["source"], c["slug"]) for c in cocktails)
    collisions = [k for k, n in keys.items() if n > 1]
    if collisions:
        print(f"SLUG COLLISIONS within a source — recipe identity is derived from these, so two rows "
              f"would become one: {collisions}")
        return 1

    names = collections.defaultdict(list)
    for c in cocktails:
        names[c["name"].casefold()].append(f"{c['source']}:{c['slug']}")
    repeated = {n: s for n, s in names.items() if len(s) > 1}
    if repeated:
        print("REPEATED NAMES - one recipe per drink (JJ-043); settle the overlap or give the "
              "recipe a name of its own in NAME_OVERRIDE:")
        for name, slugs in sorted(repeated.items()):
            print(f"  {name!r}: {', '.join(slugs)}")
        return 1

    total_lines = sum(len(c["lines"]) for c in cocktails)
    print(f"cocktails        {len(cocktails)} ({len(iba)} IBA + {len(savoy_shipped)} Savoy)")
    print(f"recipe lines     {total_lines}")
    print(f"Savoy extracted  {len(savoy)}: {len(superseded)} superseded by the IBA, "
          f"{len(savoy_excluded)} below the bar, {len(savoy_shipped)} shipped")
    for reason, n in why_counts.most_common():
        print(f"  below the bar  {n:4}  {reason}")
    print(f"glass            {report['glass_from_text']} read from the IBA's own words, "
          f"{report['glass_assigned']} assigned by the curator")
    print(f"lines dropped    {report['excluded_lines']} (ice, water, the deliberate exclusions)"
          f" + {report['blank_lines']} the scrape left blank")

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

    if report["unresolved"] or report["unknown_units"] or report["unknown_glass"]:
        return 1

    with open(EXCLUDED_PATH, "w", encoding="utf-8", newline="\n") as f:
        f.write("# Savoy recipes that do not ship (JJ-043), written by seed/build_cocktails.py.\n"
                "# The bar: a glass, a method, two or more required lines, an amount on every one.\n"
                "# Superseded drinks are in seed/overlap.json, not here.\n\n")
        for slug, name, reasons in sorted(savoy_excluded, key=lambda x: x[1].lower()):
            f.write(f"{slug}\t{name}\t{'; '.join(reasons)}\n")

    for c in cocktails:
        del c["_key"]

    shipped = {
        "$comment": [
            "The shared recipe catalog (JJ-012), built by seed/build_cocktails.py from the two",
            "extractions in seed/. Do not hand-edit: the next build overwrites it.",
            "",
            "Curated, one recipe per drink (JJ-043): the IBA official list whole, plus the Savoy",
            "recipes the IBA does not have that are usable specs. Every recipe has a glass. Amounts",
            "are as the books wrote them, so a proportional 1930 recipe carries fractions against the",
            "'part' unit; the seeder stores every volume in ounces (JJ-041).",
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
    print(f"Wrote {len(savoy_excluded)} exclusions to {EXCLUDED_PATH.relative_to(ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
