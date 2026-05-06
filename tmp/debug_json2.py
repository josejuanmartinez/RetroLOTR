with open('Assets/Resources/Cards/Modular/SauronBase.json', 'rb') as f:
    raw = f.read()

# Find "actionEffect" in bytes
needle = b'"actionEffect"'
idx = raw.find(needle)
print('First occurrence bytes:', raw[idx:idx+200])
print('---')
# Show hex around the sprite name part
idx2 = raw.find(b'Full Moon')
print('Around Full Moon:', raw[idx2-30:idx2+250])
