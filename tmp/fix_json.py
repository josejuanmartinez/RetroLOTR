import os, re

files_to_fix = [
    'Assets/Resources/Cards/Modular/ActionsDeck.json',
    'Assets/Resources/Cards/Modular/GandalfTheWhite.json',
    'Assets/Resources/Cards/Modular/SarumanTheWhite.json',
    'Assets/Resources/Cards/Modular/ShadowOfTheEast.json',
    'Assets/Resources/Cards/Modular/SauronBase.json',
    'Assets/Resources/Cards/Modular/OfManyColours.json',
]

# Pattern: actionEffect string value (with escaped quotes handled) followed by optional whitespace and "type":
# We insert a comma between the closing quote and the whitespace before type
pattern = re.compile(r'("actionEffect":\s*"(?:[^"\\]|\\.)*")(\s*)("type":)')

for f in files_to_fix:
    with open(f, 'r', encoding='utf-8') as fh:
        content = fh.read()
    
    original = content
    content = pattern.sub(r'\1,\2\3', content)
    
    if content != original:
        with open(f, 'w', encoding='utf-8') as fh:
            fh.write(content)
        print(f'Fixed commas in {f}')
    else:
        print(f'No changes in {f}')

# Fix BOM in SarumanBase.json
saruman = 'Assets/Resources/Cards/Modular/SarumanBase.json'
with open(saruman, 'rb') as fh:
    raw = fh.read()
if raw.startswith(b'\xef\xbb\xbf'):
    with open(saruman, 'wb') as fh:
        fh.write(raw.lstrip(b'\xef\xbb\xbf'))
    print(f'Removed BOM from {saruman}')
else:
    print(f'No BOM in {saruman}')
