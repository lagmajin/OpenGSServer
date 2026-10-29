import re
import os

workspace_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ranking_path = os.path.join(workspace_root, "docs", "ThinImplementationRanking.md")

content = ""
for enc in ["utf-8", "cp932", "shift_jis", "utf-8-sig"]:
    try:
        with open(ranking_path, "r", encoding=enc) as f:
            content = f.read()
        print(f"Loaded using {enc}")
        break
    except Exception as e:
        print(f"Failed {enc}: {e}")

print("Content length:", len(content))

# 打ち消し線の周りをダンプ
matches = re.findall(r'~~.*?~~', content)
print(f"Found {len(matches)} matching tilde pairs:")
for m in matches[:10]:
    print(repr(m))

# 単純に '~~' を含む行を表示
lines = content.splitlines()
t_lines = [l for l in lines if "~~" in l]
print(f"Total lines with '~~': {len(t_lines)}")
for tl in t_lines[:10]:
    print(repr(tl))
