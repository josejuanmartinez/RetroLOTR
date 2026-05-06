import re

text = '"actionEffect": "foo \\"bar\\" baz"\n            "type":'
pattern = r'"actionEffect":\s*"[^"]*"'
m = re.search(pattern, text)
print('Match:', repr(m.group(0)) if m else 'None')

# Better pattern that handles escaped quotes inside the string
pattern2 = r'"actionEffect":\s*"((?:[^"\\]|\\.)*)"'
m2 = re.search(pattern2, text)
print('Match2:', repr(m2.group(0)) if m2 else 'None')
