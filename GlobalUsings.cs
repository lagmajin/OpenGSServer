// グローバルusing - プロジェクト全体で共通使用する名前空間
// C# 10.0以降の機能

// OpenGSCore（共通ライブラリ）
global using OpenGSCore;

// .NET標準ライブラリ
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;

// JSON処理
global using Newtonsoft.Json.Linq;

// OpenGSCore型のエイリアス（明示的に区別が必要な場合）
global using CoreAbstractGameObject = OpenGSCore.AbstractGameObject;
global using CorePlayerInfo = OpenGSCore.PlayerInfo;

// 型の互換性レイヤー
// OpenGSCoreとOpenGSServerの橋渡し
