using System;
using System.Collections.Generic;
using System.Text;
using OpenGSCore;

namespace OpenGSServer
{
    /// <summary>
    /// パワーアップアイテム：プレイヤーの攻撃力を上げる
    /// </summary>
    public class InstantStagePowerUpItem : AbstractFieldItem
    {
        public InstantStagePowerUpItem(float x, float y) : base(x, y)
        {
        }

        public override void OnPickedUp(PlayerGameObject player)
        {
            if (player?.Status == null)
            {
                return;
            }

            // パワーアップ効果を適用
            player.Status.Booster = Math.Min(player.Status.MaxBooster, player.Status.Booster + 20);
            destroyed = true;
        }
    }

    /// <summary>
    /// ディフェンスアイテム：プレイヤーの防御力を上げる
    /// </summary>
    public class InstantStageDefenceItem : AbstractFieldItem
    {
        public InstantStageDefenceItem(float x, float y) : base(x, y)
        {
        }

    public override void OnPickedUp(PlayerGameObject player)
    {
        if (player?.Status == null)
        {
            return;
        }

        // ディフェンス効果を適用
        player.Status.Hp = Math.Min(player.Status.MaxHp, player.Status.Hp + 100);
        destroyed = true;
    }
    }

    /// <summary>
    /// 手榴弾アイテム：追加の手榴弾を獲得
    /// </summary>
    public class InstantStageGranadeItem : AbstractFieldItem
    {
        public InstantStageGranadeItem(float x, float y) : base(x, y)
        {
        }

        public override void OnPickedUp(PlayerGameObject player)
        {
            if (player?.Status == null)
            {
                return;
            }

            // 手榴弾を追加（例：スコア加算）
            // ゲーム固有のアイテムカウントを増やす
            // player.GranadeCount++;
            destroyed = true;
        }
    }

    /// <summary>
    /// フレームスロワーアイテム：炎放射武器を獲得
    /// </summary>
    public class InstantFlameThrower : AbstractFieldItem
    {
        public InstantFlameThrower(float x, float y) : base(x, y)
        {
        }

        public override void OnPickedUp(PlayerGameObject player)
        {
            if (player?.Status == null)
            {
                return;
            }

            // フレームスロワー効果を適用
            // ブースターを回復（例）
            player.Status.Booster = player.Status.MaxBooster;
            destroyed = true;
        }
    }
}
