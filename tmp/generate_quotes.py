import json, glob, os, sys
from openai import OpenAI

DECK_DIR = 'Assets/Resources/Cards/Modular'
files = sorted(glob.glob(f'{DECK_DIR}/*.json'))
exclude = {'SharedBase.json', 'ActionsDeck.json', 'SpellsDeck.json', 'manifest.json'}

client = OpenAI()

def load_decks():
    decks = {}
    for f in files:
        basename = os.path.basename(f)
        if basename in exclude:
            continue
        with open(f, 'r', encoding='utf-8') as fh:
            decks[f] = json.load(fh)
    return decks

def find_missing_quotes(decks):
    missing = []
    for f, data in decks.items():
        for c in data.get('cards', []):
            if c.get('referenceDeckId') and c.get('referenceCardId'):
                continue
            quote = c.get('quote', '')
            if not quote or quote.strip() == '':
                missing.append((f, data['deckId'], c['cardId'], c['name'], c['type'], c.get('region',''), c.get('action',''), c.get('actionEffect','')))
    return missing

def build_prompt(batch):
    lines = [
        "You are writing brief, atmospheric quotes for a Lord of the Rings retro card game.",
        "Rules:",
        "- For PC cards: exactly one sentence, rooted in the specific place lore, no generic 'At {place}' openings.",
        "- For Land cards: one brief atmospheric sentence about the region.",
        "- For Character cards: a short line that fits the character's voice or legend (use Tolkien quotes or plausible original lines).",
        "- For Army cards: a short martial or atmospheric line about the troop type.",
        "- For Event/Action/Spell cards: a short atmospheric quote (NOT the effect text). Keep it separate from mechanics.",
        "- Keep all quotes concise (under 120 characters ideally).",
        "- Output ONLY valid JSON: an array of objects with fields: cardId, name, type, quote.",
        "",
        "Cards to quote:"
    ]
    for _, deck_id, card_id, name, ctype, region, action, effect in batch:
        extra = f" region={region}" if region else ""
        if effect:
            extra += f" effect=({effect[:80]})"
        lines.append(f"- cardId={card_id}, name={name}, type={ctype}{extra}")
    lines.append("")
    lines.append("JSON output:")
    return "\n".join(lines)

def generate_quotes(batch):
    prompt = build_prompt(batch)
    response = client.chat.completions.create(
        model="gpt-4o-mini",
        messages=[
            {"role": "system", "content": "You write concise, lore-rich card quotes for a Middle-earth card game. Output only the requested JSON."},
            {"role": "user", "content": prompt}
        ],
        temperature=0.8,
        max_tokens=2000,
    )
    text = response.choices[0].message.content.strip()
    # Strip markdown code fences if present
    if text.startswith("```"):
        text = text.split("\n", 1)[1]
        if text.endswith("```"):
            text = text.rsplit("\n", 1)[0]
    text = text.strip()
    return json.loads(text)

def main():
    decks = load_decks()
    missing = find_missing_quotes(decks)
    print(f"Total cards needing quotes: {len(missing)}")
    if not missing:
        return

    # Group by file
    by_file = {}
    for item in missing:
        f = item[0]
        by_file.setdefault(f, []).append(item)

    total_updated = 0
    for f, items in by_file.items():
        deck_id = items[0][1]
        print(f"\nProcessing {deck_id} ({len(items)} cards)...")
        # Batch in chunks of 20 to stay within token limits
        for i in range(0, len(items), 40):
            batch = items[i:i+20]
            try:
                results = generate_quotes(batch)
                # Build lookup by cardId
                id_to_quote = {}
                for r in results:
                    cid = r.get('cardId')
                    if cid is not None:
                        id_to_quote[cid] = r.get('quote', '')
                # Update cards
                for c in decks[f]['cards']:
                    cid = c.get('cardId')
                    if cid in id_to_quote:
                        c['quote'] = id_to_quote[cid]
                        total_updated += 1
                        print(f"  -> {c.get('name')} ({cid}): {c['quote'][:60]}...")
            except Exception as e:
                print(f"  ERROR on batch {i}-{i+20}: {e}")
                continue

        # Write file back
        with open(f, 'w', encoding='utf-8') as fh:
            json.dump(decks[f], fh, indent=4, ensure_ascii=False)
            fh.write('\n')
        print(f"  Wrote {f}")

    print(f"\nTotal quotes written: {total_updated}")

if __name__ == '__main__':
    main()
