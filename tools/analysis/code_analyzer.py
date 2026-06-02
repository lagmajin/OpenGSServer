import os
import re

PROJECTS = {
    "OpenGSCore": r"C:\dev\OpenGSCore",
    "OpenGSServer": r"C:\dev\OpenGSServer",
    "OpenGSR": r"C:\dev\OpenGSR"
}

# 未実装を示すキーワード (日本語・英語)
UNFINISHED_KEYWORDS = [
    r"TODO", r"FIXME", r"XXX", 
    r"未実装", r"あとで", r"仮実装", r"一時的", r"プレースホルダー", r"ダミー", r"暫定",
    r"実装予定", r"placeholder", r"dummy", r"temporary", r"later", r"implement me"
]

def analyze_cs_file(filepath, project_name, rel_path):
    try:
        with open(filepath, "r", encoding="utf-8-sig", errors="ignore") as f:
            content = f.read()
    except Exception as e:
        return None

    lines = content.splitlines()
    total_lines = len(lines)
    
    # コメント・空白を除いた実質行数 (LOC) のカウント
    loc = 0
    in_block_comment = False
    
    todos = []
    not_implemented = []
    unfinished_comments = []
    
    # 空メソッドの検出用簡易アプローチ
    # `void MethodName() {}` のような1行の空メソッド、または複数行にまたがる空メソッド
    # 簡易的に `{}` (改行や空白のみ含む) を検出する
    empty_brackets_pattern = re.compile(r'\{\s*\}')
    
    for i, line in enumerate(lines, 1):
        stripped = line.strip()
        
        # ブロックコメントの判定
        if in_block_comment:
            if "*/" in stripped:
                in_block_comment = False
            continue
        if stripped.startswith("/*"):
            if "*/" not in stripped:
                in_block_comment = True
            continue
            
        # 空行や単一コメント、using文、名前空間宣言、中括弧のみの行はLOCに含めない
        if not stripped:
            continue
        if stripped.startswith("//"):
            # コメント内のキーワードチェック
            comment_text = stripped[2:].strip()
            for kw in UNFINISHED_KEYWORDS:
                if re.search(re.escape(kw), comment_text, re.IGNORECASE):
                    if kw.upper() in ["TODO", "FIXME", "XXX"]:
                        todos.append((i, stripped))
                    else:
                        unfinished_comments.append((i, stripped))
            continue
            
        if stripped.startswith("using ") or stripped.startswith("namespace ") or stripped == "{" or stripped == "}":
            continue
            
        loc += 1
        
        # NotImplementedException の検出
        if "NotImplementedException" in stripped:
            not_implemented.append((i, stripped))
            
        # インラインコメントのキーワードチェック
        if "//" in stripped:
            comment_part = stripped.split("//", 1)[1].strip()
            for kw in UNFINISHED_KEYWORDS:
                if re.search(re.escape(kw), comment_part, re.IGNORECASE):
                    if kw.upper() in ["TODO", "FIXME", "XXX"]:
                        todos.append((i, stripped))
                    else:
                        unfinished_comments.append((i, stripped))

    # 空メソッドのカウント
    # 簡易的に、メソッド定義らしきものの直後に {} が続いているものをカウント
    # 例: void MyMethod() { } または void MyMethod() \n { \n }
    # より正確には、正規表現で `(void|[A-Za-z0-9_<>]+)\s+[A-Za-z0-9_]+\s*\([^)]*\)\s*\{\s*\}`
    # 複数行にまたがる空括弧もカバーする
    empty_methods_count = len(re.findall(r'(?:public|private|protected|internal|override|virtual|async\s+)+\s+(?:void|[a-zA-Z0-9_<>\?\[\]]+)\s+[a-zA-Z0-9_]+\s*\([^)]*\)\s*\{\s*\}', content))
    
    # 複数行の空メソッド `{\s*}` の簡易検出 (メソッドシグネチャの後に続くもの)
    # クラス定義 `class Foo {}` も引っかかる可能性があるため、メソッド定義に限定
    method_sig_pattern = r'(?:public|private|protected|internal|override|virtual|async\s+)+\s+(?:void|[a-zA-Z0-9_<>\?\[\]]+)\s+[a-zA-Z0-9_]+\s*\([^)]*\)\s*\{\s*\}'
    
    # スコア計算
    score = 0
    reasons = []
    
    # 1. NotImplementedException
    if not_implemented:
        score += len(not_implemented) * 20
        reasons.append(f"NotImplementedException x {len(not_implemented)}")
        
    # 2. TODO コメント
    if todos:
        score += len(todos) * 10
        reasons.append(f"TODOコメント x {len(todos)}")
        
    # 3. 日本語等の未実装コメント
    if unfinished_comments:
        score += len(unfinished_comments) * 12
        reasons.append(f"未実装表記コメント x {len(unfinished_comments)}")
        
    # 4. 空メソッドの検出
    if empty_methods_count > 0:
        score += empty_methods_count * 15
        reasons.append(f"空のメソッド定義 x {empty_methods_count}")
        
    # 5. 実質行数 (LOC) が極端に少ない
    # クラスやインターフェースが含まれており、かつ LOC が 15行以下
    has_type_declaration = "class " in content or "interface " in content or "struct " in content or "enum " in content
    if has_type_declaration:
        if loc == 0:
            score += 40
            reasons.append("完全な空ファイル / 宣言のみ (LOC=0)")
        elif loc <= 5:
            score += 30
            reasons.append(f"極めて薄い実装 (LOC={loc})")
        elif loc <= 15:
            score += 15
            reasons.append(f"薄い実装 (LOC={loc})")
            
    # 実装がほぼ存在しないインターフェースやクラスのボーナス
    # (例: メンバが全くない、あるいは1つしかないなど)
    
    return {
        "project": project_name,
        "rel_path": rel_path,
        "file_name": os.path.basename(filepath),
        "loc": loc,
        "total_lines": total_lines,
        "todos": todos,
        "not_implemented": not_implemented,
        "empty_methods_count": empty_methods_count,
        "unfinished_comments": unfinished_comments,
        "score": score,
        "reasons": reasons
    }

def main():
    results = []
    
    for project_name, path in PROJECTS.items():
        if not os.path.exists(path):
            print(f"Directory not found: {path}")
            continue
            
        for root, dirs, files in os.walk(path):
            # obj, bin, .git などのフォルダはスキップ
            if any(p in root for p in ["\\bin", "\\obj", "\\.git", "\\.vs", "\\Packages", "\\Library"]):
                continue
                
            for file in files:
                if file.endswith(".cs"):
                    full_path = os.path.join(root, file)
                    rel_path = os.path.relpath(full_path, r"C:\dev")
                    res = analyze_cs_file(full_path, project_name, rel_path)
                    if res and res["score"] > 0:
                        results.append(res)
                        
    # スコア順にソート
    results.sort(key=lambda x: x["score"], reverse=True)
    
    # 結果の表示 (上位50件)
    print(f"Total analyzed thin files: {len(results)}")
    print("--- TOP 30 THIN IMPLEMENTATIONS ---")
    for i, res in enumerate(results[:30], 1):
        print(f"Rank {i}: [{res['project']}] {res['rel_path']} (Score: {res['score']})")
        print(f"  LOC: {res['loc']} / Total: {res['total_lines']}")
        print(f"  Reasons: {', '.join(res['reasons'])}")
        if res['todos']:
            print(f"  TODOs: {res['todos'][:2]}")
        if res['not_implemented']:
            print(f"  NotImplemented: {res['not_implemented'][:2]}")
        print()

if __name__ == "__main__":
    main()
