import re

f = 'Assets/Resources/Cards/Modular/ActionsDeck.json'
with open(f, 'r', encoding='utf-8') as fh:
    content = fh.read()

original = content
# Fix missing comma after actionEffect before any next key
pattern = re.compile(r'("actionEffect":\s*"(?:[^"\\]|\\.)*")(\s*)("\w+":)')
content = pattern.sub(r'\1,\2\3', content)

if content != original:
    with open(f, 'w', encoding='utf-8') as fh:
        fh.write(content)
    print(f'Fixed commas in {f}')
else:
    print(f'No changes in {f}')
