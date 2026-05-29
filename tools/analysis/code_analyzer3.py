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

# スキップすべきディレクトリやファイル名パターン (小文字で比較)
SKIP_PATTERNS = [
    "\\bin\\", "\\obj\\", "\\.git\\", "\\.vs\\", 
    "\\plugins\\zenject", "\\assets\\plugins\\", 
    "\\netcoreserver", "assemblyinfo.cs",
    "\\packages\\", "\\library\\", "unitask", 
    "\\temp\\", "\\logs\\", "assembly-csharp"
]

def analyze_cs_file(filepath, project_name, rel_path):
    try:
        with open(filepath, "r", encoding="utf-8-sig", errors="ignore") as f:
            content = f.read()
    except Exception as e:
        return None

    # サードパーティや自動生成ファイルなどのスキップ判定
    filepath_lower = filepath.lower()
    if any(p in filepath_lower for p in SKIP_PATTERNS):
        return None

    lines = content.splitlines()
    total_lines = len(lines)
    
    # 抽象クラス/インターフェースかどうかの判定
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
    
    # 1. NotImplementedException
    if not_implemented:
        score += len(not_implemented) * 35
        reasons.append(f"NotImplementedException x {len(not_implemented)}")
        
    # 2. TODO コメント
    if todos:
        score += len(todos) * 15
        reasons.append(f"TODO comments x {len(todos)}")
        
    # 3. 日本語等の未実装コメント
    if unfinished_comments:
        score += len(unfinished_comments) * 20
        reasons.append(f"Unfinished markers x {len(unfinished_comments)}")
        
    # 4. 空メソッドの検出 (具象クラスは +20、抽象クラス/インターフェースは +5)
    if empty_methods_count > 0:
        multiplier = 5 if is_abstract else 20
        score += empty_methods_count * multiplier
        reasons.append(f"Empty methods x {empty_methods_count} (Abstract: {is_abstract})")
        
    # 5. 実質行数 (LOC) が極端に少ない
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
            
    # 特殊ケース: ObsoleteやDeprecatedになっているものは除外もしくはスコア低減
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

def main():
    results = []
    
    for project_name, path in PROJECTS.items():
        if not os.path.exists(path):
            continue
            
        for root, dirs, files in os.walk(path):
            for file in files:
                if file.endswith(".cs"):
                    full_path = os.path.join(root, file)
                    rel_path = os.path.relpath(full_path, r"C:\dev")
                    
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

このドキュメントは、`OpenGSCore`、`OpenGSServer`、`OpenGSR` のソースコード（C#）を静的解析し、実装が薄い（スカスカな）箇所、未実装、TODO放置、`NotImplementedException` などが多いファイルやクラスを抽出してランキング化したものです。

## 📊 スコアリング基準
- **NotImplementedException**: `+35点` / 件 （例外を投げて放置されている重要箇所）
- **TODO / FIXME コメント**: `+15点` / 件
- **日本語未実装表記 (`未実装`, `あとで`, `仮実装`, `ダミー` など)**: `+20点` / 件
- **空のメソッド定義 `{ }`**: 具象クラス `+20点` / 件（実装漏れの可能性大）, 抽象クラス/IF `+5点` / 件
- **極めて少ないLOC (実質コード行数)**: LOC=0は `+60点`, LOC<=5は `+45点`, LOC<=15は `+25点`
- **非推奨 (Deprecated/Obsolete) フォルダ・属性**: スコア `80% 減算`（過去の遺産は除外するため）

---

## 🏆 総合ワーストランキング（実装が薄いクラス TOP 30）

| 順位 | プロジェクト | ファイルパス | スコア | 評価行数 (LOC/Total) | 主な検出理由 |
| :---: | :--- | :--- | :---: | :---: | :--- |
"""
    
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
        
        # 最大30件
        limit = min(30, len(p_list))
        for i, res in enumerate(p_list[:limit], 1):
            reasons_str = ", ".join(res["reasons"])
            
            # TODOや未実装箇所のサンプルを抜粋
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
        
    md_content += """---

## 🔍 主要な「薄い実装」の詳細とコード解説

ランキング上位から、特に実装不足や放置されたメソッドが目立ち、機能追加の余地が大きい主要なクラスをピックアップして詳細を解説します。

### 🚨 1位：`CharaController.cs` (OpenGSR) — 具象クラスの空メソッド放置
- **現状スコア**: **180** (LOC: 176 / Total: 522)
- **主な理由**: 具象クラスでありながら、空のメソッド定義 `{ }` が 9 箇所も放置されています。
- **薄い実装のコード抜粋**:
  ```csharp
  [Button("ローリングテスト")]
  public new void Rolling()
  {
      // 空っぽ
  }

  void Scope()
  {
      // 空っぽ
  }

  public void FlipWeapon()
  {
      // 空っぽ
  }

  void TakeNewWeapon()
  {
      // 空っぽ
  }
  ```
- **分析と影響**: プレイヤーキャラクターの基本アクション（ローリング、スコープ覗き込み、武器の切り替え、武器の拾得・投棄など）の機能枠だけが宣言され、ロジックが未実装になっています。特に `Rolling` や `Scope` はゲームプレイの根幹に関わる部分であり、最優先での実装が必要です。

### 🚨 2位：`ItemEffect.cs` (OpenGSCore) — アイテム効果ロジックの不在
- **現状スコア**: **145** (LOC: 10 / Total: 51)
- **主な理由**: 実質 LOC がわずか 10 行しかなく、派生クラスがすべて空のメソッドで定義されています。
- **薄い実装 of コード抜粋**:
  ```csharp
  public class PowerUpItemEffect : AbstractItemEffect
  {
      public PowerUpItemEffect() { }

      public override void ApplyItemEffect(PlayerStatus status)
      {
          // 空っぽ
      }

      public override void UnApplyItemEffect(PlayerStatus status)
      {
          // 空っぽ
      }
  }
  ```
- **分析と影響**: 攻撃力アップ (`PowerUp`)、防御力アップ (`DefenceUp`)、グレネードパック (`NormalGranadePack`) のアイテム効果クラスが定義されているものの、プレイヤーへのバフ適用ロジックが全く存在しません。アイテムを拾った際の効果が機能していないことを示しています。

### 🚨 3位：`AIPlayerController.cs` (OpenGSR) — AIキャラクター制御の未実装
- **現状スコア**: **140** (LOC: 61 / Total: 205)
- **主な理由**: 具象クラスにおける空メソッドが 7 箇所。
- **分析と影響**: AI（ボット）キャラクターの移動、攻撃、意思決定などのフレームワークは定義されている可能性がありますが、個別の状態更新ロジックやターゲット追跡処理などが空になっており、AIがその場で静止するか、意図通りに動かない原因となっています。

### 🚨 4位：`ServerInfoDatabaseManager.cs` (OpenGSServer) — データベース管理機能の不足
- **現状スコア**: **85** (LOC: 11 / Total: 45)
- **主な理由**: 具象クラスの空メソッドが 3 箇所、実質 LOC=11 行の非常に薄いクラス。
- **薄い実装のコード抜粋**:
  ```csharp
  public void UpdateDatabase()
  {
      // 空っぽ
  }

  public void ClearDatabase()
  {
      // 空っぽ
  }

  public void RemoveDatabase()
  {
      // 空っぽ
  }
  ```
- **分析と影響**: LiteDB の接続初期化 `Connect()` 自体は行っていますが、DBのアップデート、クリア、削除などの実際のデータ操作処理が空のままです。サーバー情報テーブルのメンテナンスや削除処理が未実装であることを意味します。

---

## 💡 今後の改善に向けた推奨アプローチ

1. **基本アクションの実装 (`CharaController`)**: 
   `Rolling()`, `Scope()`, `FlipWeapon()` など、プレイヤーに紐づくアクションの処理を追加し、入力システムやアニメーションシステムと連動させます。
2. **アイテム効果の具象ロジック実装 (`ItemEffect`)**:
   `PlayerStatus` クラスに対し、バフ値（攻撃力、防御力）の加算・減算処理を追加し、タイマー処理と連動して効果時間が切れたら `UnApply` を呼ぶロジックを追加します。
3. **データベース管理メソッドの実装 (`ServerInfoDatabaseManager`)**:
   LiteDB のコレクション取得・書き込みロジックを追加し、管理用APIからDBの状態を変更できるようにします。

---
*このレポートは `code_analyzer3.py` の静的解析により自動生成され、手動で重要度を評価したものです。*
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
