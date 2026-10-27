"""Copy Design/design-data.json into index.html as the sandbox's built-in defaults.

Run from the repo root after changing Design/design-data.json:
    python Tools/ForageSandbox/embed-design.py
"""
import json
import os
import re

root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
design_path = os.path.join(root, "Design", "design-data.json")
page_path = os.path.join(root, "Tools", "ForageSandbox", "index.html")

design = open(design_path, encoding="utf-8").read().strip()
json.loads(design)  # fail loudly on broken JSON
# </script> inside the JSON would end the script block early.
design = design.replace("</", "<\\/")

page = open(page_path, encoding="utf-8").read()
pattern = re.compile(r'(<script type="application/json" id="seed-design">)(.*?)(</script>)', re.S)
if not pattern.search(page):
    raise SystemExit("Couldn't find the seed-design block in index.html")

page = pattern.sub(lambda m: m.group(1) + "\n" + design + "\n" + m.group(3), page, count=1)
open(page_path, "w", encoding="utf-8", newline="\n").write(page)
print(f"Embedded {design_path} into {page_path}")
