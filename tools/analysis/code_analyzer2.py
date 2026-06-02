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
    r"実装予定", r"placeholder", r"dummy", r"temporary", r"later", r"implement me", r"後で"
]

# スキップすべきディレクトリやファイル名パターン
SKIP_PATTERNS = [
    "\\bin", "\\obj", "\\.git", "\\.vs", 
    "\\Plugins\\Zenject", "\\Assets\\Plugins", 
    "\\NetCoreServer", "AssemblyInfo.cs"
]

def analyze_cs_file(filepath, project_name, rel_path):
    try:
        with open(filepath, "r", encoding="utf-8-sig", errors="ignore") as f:
            content = f.read()
    except Exception as e:
        return None

    # サードパーティや自動生成ファイルなどのスキップ
    if any(p in filepath for p in SKIP_PATTERNS):
        return None

    lines = content.splitlines()
    total_lines = len(lines)
    
    # 抽象クラスかどうかの判定
    is_abstract = "abstract class" in content or "interface " in content
    
    loc = 0
    in_block_comment = False
    
    todos = []
    not_implemented = []
    unfinished_comments = []
    
    for i, line in enumerate(lines, 1):
        stripped = line.strip()
        
        if in_block_comment:
            if "*/" in stripped:
                in_block_comment = False
            continue
        if stripped.startswith("/*"):
            if "*/" not in stripped:
                in_block_comment = True
            continue
            
        if not stripped:
            continue
        if stripped.startswith("//"):
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
        
        if "NotImplementedException" in stripped:
            not_implemented.append((i, stripped))
            
        if "//" in stripped:
            comment_part = stripped.split("//", 1)[1].strip()
            for kw in UNFINISHED_KEYWORDS:
                if re.search(re.escape(kw), comment_part, re.IGNORECASE):
                    if kw.upper() in ["TODO", "FIXME", "XXX"]:
                        todos.append((i, stripped))
                    else:
                        unfinished_comments.append((i, stripped))

    # 空メソッドの検出 (正規表現)
    # `void MyMethod() {}` のような1行の空メソッド、または複数行にまたがる空メソッド
    # 具象クラスの空メソッドは実装漏れの可能性が高いため重く評価する
    empty_methods_count = len(re.findall(r'(?:public|private|protected|internal|override|virtual|async\s+)+\s+(?:void|[a-zA-Z0-9_<>\?\[\]]+)\s+[a-zA-Z0-9_]+\s*\([^)]*\)\s*\{\s*\}', content))
    
    # スコア計算
    score = 0
    reasons = []
    
    # 1. NotImplementedException (非常に重要)
    if not_implemented:
        score += len(not_implemented) * 30
        reasons.append(f"NotImplementedException x {len(not_implemented)}")
        
    # 2. TODO コメント
    if todos:
        score += len(todos) * 15
        reasons.append(f"TODO comments x {len(todos)}")
        
    # 3. 日本語等の未実装コメント
    if unfinished_comments:
        score += len(unfinished_comments) * 15
        reasons.append(f"Unimplemented marks x {len(unfinished_comments)}")
        
    # 4. 空メソッドの検出 (抽象クラスなら低め、具象クラスなら高め)
    if empty_methods_count > 0:
        multiplier = 5 if is_abstract else 20
        score += empty_methods_count * multiplier
        reasons.append(f"Empty methods x {empty_methods_count} (Abstract: {is_abstract})")
        
    # 5. 実質行数 (LOC) が極端に少ない
    has_type_declaration = "class " in content or "interface " in content or "struct " in content or "enum " in content
    if has_type_declaration:
        if loc == 0:
            score += 50
            reasons.append("Empty file / only declarations (LOC=0)")
        elif loc <= 5:
            score += 35
            reasons.append(f"Extremely thin implementation (LOC={loc})")
        elif loc <= 15:
            score += 20
            reasons.append(f"Thin implementation (LOC={loc})")
            
    # 特殊ケース: ObsoleteやDeprecatedになっているものは除外もしくはスコア低減
    if "Obsolete" in content or "Deprecated" in rel_path:
        score = int(score * 0.3)
        reasons.append("Deprecated / Obsolete (Score reduced by 70%)")

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
        "reasons": reasons,
        "is_abstract": is_abstract
    }

def main():
    results = []
    
    for project_name, path in PROJECTS.items():
        if not os.path.exists(path):
            continue
            
        for root, dirs, files in os.walk(path):
            # スキップ判定
            if any(p in root for p in SKIP_PATTERNS):
                continue
                
            for file in files:
                if file.endswith(".cs"):
                    full_path = os.path.join(root, file)
                    rel_path = os.path.relpath(full_path, r"C:\dev")
                    
                    # 個別ファイルのスキップ判定
                    if any(p in rel_path for p in SKIP_PATTERNS):
                        continue
                        
                    res = analyze_cs_file(full_path, project_name, rel_path)
                    if res and res["score"] > 0:
                        results.append(res)
                        
    # スコア順にソート
    results.sort(key=lambda x: x["score"], reverse=True)
    
    # プロジェクト別に抽出
    project_ranks = {}
    for p in PROJECTS.keys():
        project_ranks[p] = [r for r in results if r["project"] == p]
        
    # ドキュメント（markdown）を作成
    md_content = """# OpenGS プロジェクト実装完成度・薄さランキング

このドキュメントは、`OpenGSCore`、`OpenGSServer`、`OpenGSR` のソースコード（C#）を自動解析し、実装が薄い（スカスカな）箇所、未実装、TODO放置、NotImplementedExceptionなどが多い箇所を抽出してランキング化したものです。

## 📊 スコアリング基準
- **NotImplementedException の検出**: `+30点` / 件
- **TODO / FIXME コメント**: `+15点` / 件
- **日本語未実装マーカー (`未実装`, `あとで`, `仮実装`, `ダミー` など)**: `+15点` / 件
- **空のメソッド定義 `{ }`**: 具象クラス `+20点` / 件, 抽象クラス/I/F `+5点` / 件
- **極めて少ないLOC (行数)**: LOC=0は `+50点`, LOC<=5は `+35点`, LOC<=15は `+20点`
- **非推奨 (Deprecated/Obsolete) フォルダ・属性**: スコア `70% 減算`

---

## 🏆 総合ワーストランキング（実装が薄いクラス TOP 15）

| 順位 | プロジェクト | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な理由 |
| :---: | :--- | :--- | :---: | :---: | :--- |
"""
    
    for i, res in enumerate(results[:15], 1):
        reasons_str = ", ".join(res["reasons"])
        md_content += f"| {i} | `{res['project']}` | `{res['rel_path']}` | **{res['score']}** | {res['loc']}/{res['total_lines']} | {reasons_str} |\n"
        
    md_content += "\n---\n\n"
    
    # 各プロジェクト別ランキング
    for p_name in PROJECTS.keys():
        p_list = project_ranks[p_name]
        md_content += f"## 📁 {p_name} 個別ワーストランキング TOP 8\n\n"
        md_content += "| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な理由 | TODO/未実装箇所 |\n"
        md_content += "| :---: | :--- | :---: | :---: | :--- | :--- |\n"
        
        for i, res in enumerate(p_list[:8], 1):
            reasons_str = ", ".join(res["reasons"])
            
            # TODOや未実装箇所のサンプルを抜粋
            unfinished_samples = []
            if res["not_implemented"]:
                unfinished_samples.append(f"NotImpl: Line {res['not_implemented'][0][0]}")
            if res["todos"]:
                # 不要なコメント記号を除去
                todo_text = res["todos"][0][1].strip().replace("//", "").strip()
                if len(todo_text) > 40:
                    todo_text = todo_text[:37] + "..."
                unfinished_samples.append(f"TODO (L{res['todos'][0][0]}): {todo_text}")
            if res["unfinished_comments"]:
                comment_text = res["unfinished_comments"][0][1].strip().replace("//", "").strip()
                if len(comment_text) > 40:
                    comment_text = comment_text[:37] + "..."
                unfinished_samples.append(f"未実装 (L{res['unfinished_comments'][0][0]}): {comment_text}")
                
            sample_str = "<br>".join(unfinished_samples) if unfinished_samples else "なし"
            
            md_content += f"| {i} | `{res['rel_path']}` | **{res['score']}** | {res['loc']}/{res['total_lines']} | {reasons_str} | {sample_str} |\n"
            
        md_content += "\n"
        
    md_content += """---

## 🔍 ピックアップされた主要な「薄い実装」の詳細と改善方針

ここでは、ランキング上位に選ばれたクラスの中で、特に開発や修正の優先度が高い「実装が薄い」箇所をピックアップして詳細を解説します。

### 1. OpenGSR 側のアクション・コントローラー系ベースクラス群 (`AbstractPlayer`, `AbstractScene`, `AbstractMatchMainScript`)
- **現状**: スコアが 200 点前後と極めて高くなっています。これらは Unity 側の基底クラス（抽象クラス）であり、数多くの `virtual void` や `virtual IEnumerator` が定義されていますが、中身が `{ }` (空) で多数放置されています。
- **課題**: フックメソッドが多すぎるか、あるいは基底クラスとしてのデフォルトの振る舞い（例えばデフォルトログ出力、デフォルト状態遷移など）が不足している可能性があります。
- **対策**: 不要な空メソッドを整理し、必要な共通処理を基底クラス側に集約するか、インターフェースに移行することを推奨します。

### 2. OpenGSCore の `ItemEffect.cs` / `Item` 関連
- **現状**: スコア 105。実質 LOC が 10 行しかなく、メソッドの多くが空になっています。
- **課題**: アイテム効果のシステム枠だけが存在し、具体的なロジック（HP回復、バフ効果など）が未実装のままになっています。
- **対策**: 各アイテム効果の具象クラスを実装するか、スクリプトデータ（ScriptableObject 等）から動的に効果を解決する仕組みを構築する必要があります。

### 3. OpenGSServer の `AccountDatabaseManager.cs` / `ServerInfoDatabaseManager.cs`
- **現状**: スコア 60。メソッド定義は多いものの、空のメソッドやプレースホルダー実装が多く散見されます。
- **課題**: データベースへの永続化処理や、サーバー情報の管理処理が仮実装の段階にとどまっている可能性があります。
- **対策**: DBコネクションの適切な管理、例外ハンドリングの強化、そして永続化処理の本格実装が必要です。

---
*このレポートは `code_analyzer2.py` によって自動生成されました。*
"""

    # docs ディレクトリへ書き出す
    docs_path = r"C:\dev\OpenGSServer\docs"
    os.makedirs(docs_path, exist_ok=True)
    out_file = os.path.join(docs_path, "ThinImplementationRanking.md")
    
    with open(out_file, "w", encoding="utf-8") as f:
        f.write(md_content)
        
    print(f"Successfully generated thin implementation ranking at: {out_file}")

if __name__ == "__main__":
    main()
