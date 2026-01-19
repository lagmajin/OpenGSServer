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

    public interface ISyncable
    {
        JObject ToJson();
        bool HasChanged();
        void SaveSyncState();
    }

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

    /// <summary>
    /// 通常の手榴弾（参考実装）
    /// OpenGSCoreのToJsonメソッドはJObjectではなくstring返すため、オーバーライドしない
    /// </summary>
    class NormalGranade : OpenGSCore.AbstractGameObject
    {
        public NormalGranade(float x, float y, float time = 0.0f)
        {
            SetPos(x, y);
        }

        // OpenGSCoreのToJsonはstring返すため、別メソッドとして実装
        public JObject ToJObject()
        {
            return new JObject
            {
                ["Type"] = "Grenade",
                ["X"] = Posx,
                ["Y"] = Posy
            };
        }
    }
}
