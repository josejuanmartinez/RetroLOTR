import os, re

def fix_file(path):
    with open(path, 'r', encoding='utf-8') as fh:
        content = fh.read()
    
    original = content
    # Fix missing comma after actionEffect before any next key
    pattern = re.compile(r'("actionEffect":\s*"(?:[^"\\]|\\.)*")(\s*)("\w+":)')
    content = pattern.sub(r'\1,\2\3', content)
    
    if content != original:
        with open(path, 'w', encoding='utf-8') as fh:
            fh.write(content)
        print(f'Fixed: {path}')
        return True
    return False

base = 'Assets/Resources/Cards/Modular'
fixed_count = 0
for name in os.listdir(base):
    if name.endswith('.json'):
        path = os.path.join(base, name)
        if fix_file(path):
            fixed_count += 1

print(f'\nTotal files fixed: {fixed_count}')
