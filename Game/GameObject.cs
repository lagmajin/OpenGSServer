using Newtonsoft.Json.Linq;
using OpenGSCore; // OpenGSCoreを使用
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace OpenGSServer
{
    // ===============================================
    // 注意: このファイルは段階的に削除予定
    // OpenGSCore.AbstractGameObjectを使用してください
    // ===============================================

    // OpenGSCoreのenumを使用
    // public enum EGameObjectType は削除（OpenGSCore.eGameObjectType を使用）

    // ISyncable は削除（OpenGSCore.AbstractGameObjectで代替）

    // ===============================================
    // 後方互換性のためのエイリアス
    // 新規コードでは OpenGSCore.AbstractGameObject を直接使用してください
    // ===============================================
    [Obsolete("Use OpenGSCore.AbstractGameObject instead", false)]
    public abstract class AbstractGameObject : OpenGSCore.AbstractGameObject
    {
        // OpenGSCore.AbstractGameObjectを継承
        // このクラスは後方互換性のためのみ存在します
        
        protected AbstractGameObject(float x, float y)
        {
            SetPos(x, y);
        }

        // 追加のコンストラクタ
        protected AbstractGameObject()
        {
        }
    }
}
