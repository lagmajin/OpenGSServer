import os
import re

PROJECTS = {
    "OpenGSCore": r"C:\dev\OpenGSCore",
    "OpenGSServer": r"C:\dev\OpenGSServer",
    "OpenGSR": r"C:\dev\OpenGSR"
}

UNFINISHED_KEYWORDS = [
    r"TODO", r"FIXME", r"XXX", 
    r"未実装", r"あとで", r"仮実装", r"一時的", r"プレースホルダー", r"ダミー", r"暫定",
    r"実装予定", r"placeholder", r"dummy", r"temporary", r"later", r"implement me", r"後で"
]

SKIP_PATTERNS = [
    "\\bin\\", "\\obj\\", "\\.git\\", "\\.vs\\", 
    "\\plugins\\zenject", "\\assets\\plugins\\", 
    "\\netcoreserver", "assemblyinfo.cs",
    "\\packages\\", "\\library\\", "unitask", 
    "\\temp\\", "\\logs\\", "assembly-csharp"
]

def analyze_cs_file(filepath, project_name, rel_path):
    try:
        content = None
        for enc in ["utf-8", "utf-8-sig", "cp932", "shift_jis"]:
            try:
                with open(filepath, "r", encoding=enc) as f:
                    content = f.read()
                break
            except Exception:
                continue
        if content is None:
            return None
    except Exception as e:
        return None

    filepath_lower = filepath.lower()
    if any(p in filepath_lower for p in SKIP_PATTERNS):
        return None

    lines = content.splitlines()
    total_lines = len(lines)
    
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

    # 空メソッドの検出
    empty_methods_count = len(re.findall(
        r'(?:public|private|protected|internal|override|virtual|async\s+)+\s+(?:[a-zA-Z0-9_<>\?\[\]\s]+)\s+[a-zA-Z0-9_]+\s*\([^)]*\)\s*\{\s*\}', 
        content
    ))
    
    # スコア計算
    score = 0
    reasons = []
    
    if not_implemented:
        score += len(not_implemented) * 35
        reasons.append(f"NotImplementedException x {len(not_implemented)}")
        
    if todos:
        score += len(todos) * 15
        reasons.append(f"TODO comments x {len(todos)}")
        
    if unfinished_comments:
        score += len(unfinished_comments) * 20
        reasons.append(f"Unfinished markers x {len(unfinished_comments)}")
        
    if empty_methods_count > 0:
        multiplier = 5 if is_abstract else 20
        score += empty_methods_count * multiplier
        reasons.append(f"Empty methods x {empty_methods_count} (Abstract: {is_abstract})")
        
    has_type_declaration = "class " in content or "interface " in content or "struct " in content or "enum " in content
    if has_type_declaration:
        if loc == 0:
            score += 60
            reasons.append("Empty file / only declarations (LOC=0)")
        elif loc <= 5:
            score += 45
            reasons.append(f"Extremely thin implementation (LOC={loc})")
        elif loc <= 15:
            score += 25
            reasons.append(f"Thin implementation (LOC={loc})")
            
    if "Obsolete" in content or "Deprecated" in rel_path or "deprecated" in rel_path.lower():
        score = int(score * 0.2)
        reasons.append("Deprecated / Obsolete (Score reduced by 80%)")

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

def load_implemented_files(ranking_path):
    implemented = set()
    implemented_section = ""
    
    if not os.path.exists(ranking_path):
        return implemented, implemented_section
        
    content = ""
    for enc in ["utf-8", "cp932", "shift_jis", "utf-8-sig"]:
        try:
            with open(ranking_path, "r", encoding=enc) as f:
                content = f.read()
            print(f"Successfully loaded ranking file using encoding: {enc}")
            break
        except Exception:
            continue
            
    if not content:
        return implemented, implemented_section

    # ファイル全体から ~~`パス`~~ または ~~パス~~ を一括抽出
    paths = re.findall(r'~~+`?([^`~\s]+?)`?~~+', content)
    for p in paths:
        normalized = p.replace("/", "\\").strip().lower()
        implemented.add(normalized)
        print(f"Detected implemented file to exclude: {p}")
        
    # 「実装済み」セクションをピンポイントで抽出する
    # 「実装済み」または「実裁」や「✅」を含む見出し行から、次の「##」または「---」まで
    match = re.search(r'(##\s+.*?(?:実装済み|実裁.*?み|✅).*?)(?=\n##|\n---|\Z)', content, re.DOTALL)
    if match:
        implemented_section = match.group(1).strip()
        print("Successfully extracted implemented section text.")
    else:
        print("Warning: Could not extract implemented section with regex.")
            
    return implemented, implemented_section

def main():
    ranking_path = r"C:\dev\OpenGSServer\docs\ThinImplementationRanking.md"
    implemented_files, implemented_section_text = load_implemented_files(ranking_path)
    
    print(f"Total unique implemented files excluded: {len(implemented_files)}")
    
    results = []
    
    for project_name, path in PROJECTS.items():
        if not os.path.exists(path):
            continue
            
        for root, dirs, files in os.walk(path):
            for file in files:
                if file.endswith(".cs"):
                    full_path = os.path.join(root, file)
                    rel_path = os.path.relpath(full_path, r"C:\dev")
                    
                    rel_path_norm = rel_path.replace("/", "\\").strip().lower()
                    if rel_path_norm in implemented_files:
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
        
    if not implemented_section_text:
        implemented_section_text = """## ✅ 実装済み

この一覧は、すでに消し込み済み（実装完了）として扱える主な項目です。ここに `~~ファイルパス~~` を追加すると、自動的にランキングから除外されます。
"""

    md_template = """# OpenGS プロジェクト実装完成度・薄さランキング

このドキュメントは、`OpenGSCore`、`OpenGSServer`、`OpenGSR` のソースコード（C#）を静的解析し、実装が薄い（スカスカな）箇所、未実装、TODO放置、`NotImplementedException` などが多いファイルやクラスを抽出してランキング化したものです。

## 📊 スコアリング基準
- **NotImplementedException**: `+35点` / 件 （例外を投げて放置されている重要箇所）
- **TODO / FIXME コメント**: `+15点` / 件
- **日本語未実装表記 (`未実装`, `あとで`, `仮実装`, `ダミー` など)**: `+20点` / 件
- **空のメソッド定義 `{ }`**: 具象クラス `+20点` / 件（実装漏れの可能性大）, 抽象クラス/IF `+5点` / 件
- **極めて少ないLOC (実質コード行数)**: LOC=0は `+60点`, LOC<=5は `+45点`, LOC<=15は `+25点`
- **非推奨 (Deprecated/Obsolete) フォルダ・属性**: スコア `80% 減算`（過去の遺産は除外するため）

---

[IMPLEMENTED_SECTION_PLACEHOLDER]

---

## 🏆 総合ワーストランキング（未実装・薄いクラス TOP 30）

| 順位 | プロジェクト | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 |
| :---: | :--- | :--- | :---: | :---: | :--- |
"""

    md_content = md_template.replace("[IMPLEMENTED_SECTION_PLACEHOLDER]", implemented_section_text)
    
    # 総合ランキング TOP 30
    for i, res in enumerate(results[:30], 1):
        reasons_str = ", ".join(res["reasons"])
        md_content += f"| {i} | `{res['project']}` | `{res['rel_path']}` | **{res['score']}** | {res['loc']}/{res['total_lines']} | {reasons_str} |\n"
        
    md_content += "\n---\n\n"
    
    # 各プロジェクト別ランキング TOP 30
    for p_name in PROJECTS.keys():
        p_list = project_ranks[p_name]
        md_content += f"## 📁 {p_name} 個別ワーストランキング TOP 30\n\n"
        md_content += "| 順位 | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 | TODO/未実装箇所の抜粋 |\n"
        md_content += "| :---: | :--- | :---: | :---: | :--- | :--- |\n"
        
        limit = min(30, len(p_list))
        for i, res in enumerate(p_list[:limit], 1):
            reasons_str = ", ".join(res["reasons"])
            
            unfinished_samples = []
            if res["not_implemented"]:
                unfinished_samples.append(f"NotImpl (L{res['not_implemented'][0][0]})")
            if res["todos"]:
                todo_text = res["todos"][0][1].strip().replace("//", "").strip()
                if len(todo_text) > 40:
                    todo_text = todo_text[:37] + "..."
                unfinished_samples.append(f"TODO (L{res['todos'][0][0]}): `{todo_text}`")
            if res["unfinished_comments"]:
                comment_text = res["unfinished_comments"][0][1].strip().replace("//", "").strip()
                if len(comment_text) > 40:
                    comment_text = comment_text[:37] + "..."
                unfinished_samples.append(f"未実装 (L{res['unfinished_comments'][0][0]}): `{comment_text}`")
                
            sample_str = "<br>".join(unfinished_samples) if unfinished_samples else "なし"
            
            md_content += f"| {i} | `{res['rel_path']}` | **{res['score']}** | {res['loc']}/{res['total_lines']} | {reasons_str} | {sample_str} |\n"
            
        md_content += "\n"
        
    # ピックアップする上位のクラスを特定
    top_picks = results[:4]
    
    md_content += """---

## 🔍 主要な「薄い実装」の詳細とコード解説

ランキング上位から、特に実装不足や放置されたメソッドが目立ち、機能追加の余地が大きい主要なクラスをピックアップして詳細を解説します。
"""

    for idx, res in enumerate(top_picks, 1):
        md_content += f"\n### 🚨 {idx}位：`{res['file_name']}` ({res['project']}) — 実装補強が必要な箇所\n"
        md_content += f"- **現状スコア**: **{res['score']}** (LOC: {res['loc']} / Total: {res['total_lines']})\n"
        md_content += f"- **ファイルパス**: `{res['rel_path']}`\n"
        md_content += f"- **主な検出理由**: {', '.join(res['reasons'])}\n"
        
        try:
            full_path = os.path.join(r"C:\dev", res["rel_path"])
            with open(full_path, "r", encoding="utf-8-sig", errors="ignore") as f:
                file_lines = f.readlines()
            
            samples = []
            for i, line in enumerate(file_lines, 1):
                stripped = line.strip()
                if "NotImplementedException" in stripped or "TODO" in stripped or "未実装" in stripped:
                    samples.append(f"  {i}: {stripped}")
                if len(samples) >= 4:
                    break
                    
            if samples:
                md_content += "- **未実装・TODOのコード抜粋**:\n"
                md_content += "  ```csharp\n"
                md_content += "\n".join(samples) + "\n"
                md_content += "  ```\n"
        except Exception as e:
            pass
            
        md_content += "- **分析と影響**: このモジュールはクラス宣言やインターフェースの整合性は保たれていますが、実際の動作に必要なロジックが不足しているか、ダミーデータ・例外を返す仮の状態になっています。本番環境の機能連携に支障をきたすため、優先的に本実装を行う必要があります。\n"

    md_content += """
---

## 💡 今後の改善に向けた推奨アプローチ

1. **実装完了ファイルの消し込み**:
   実装が完了したファイルは、ドキュメント冒頭の「## ✅ 実装済み」セクションに `~~ファイルパス~~` の形式で追加してください。次回スキャン時にランキングから自動で除外されます。
2. **上位クラスからの順次本実装**:
   現在ランキングの上位に入っているクラスは、いずれも機能フレームワークは構築済みで「中身のロジック」だけが空になっている状態です。メソッドの引数と戻り値の設計に沿って、必要な同期処理やデータ処理を追加してください。

---
*このレポートは `update_ranking.py` の静的解析により自動更新されました。*
"""

    docs_path = r"C:\dev\OpenGSServer\docs"
    os.makedirs(docs_path, exist_ok=True)
    out_file = os.path.join(docs_path, "ThinImplementationRanking.md")
    
    with open(out_file, "w", encoding="utf-8") as f:
        f.write(md_content)
        
    print(f"Successfully updated thin implementation ranking at: {out_file}")

if __name__ == "__main__":
    main()
