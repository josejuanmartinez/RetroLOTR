import re
with open('Assets/Resources/Cards/Modular/SauronBase.json', 'r') as f:
    text = f.read()
idx = text.find('Full Moon')
print(repr(text[idx-50:idx+200]))

m = re.search(r'"actionEffect":\s*"[^"]*"', text)
print('Match:', m.group(0) if m else 'None')

# Let's try a more targeted fix: find all occurrences of actionEffect line not followed by comma before type
matches = list(re.finditer(r'"actionEffect":\s*"([^"]*)"\s*"type":', text))
print('Bad matches found:', len(matches))
for m in matches:
    print(repr(m.group(0)[:100]))
