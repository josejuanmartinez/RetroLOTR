with open('Assets/Resources/Cards/Modular/ActionsDeck.json', 'rb') as f:
    raw = f.read()

# Split by lines and show around line 247
lines = raw.split(b'\n')
for i, line in enumerate(lines[230:260], start=231):
    print(f'{i}: {line}')
