using System;

namespace OpenGSServer.Utility
{
    // Timeクラスは削除（.NET標準のTimeSpanを使用してください）
    // 例: TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(30)).TotalSeconds

    /// <summary>
    /// Ping計算ユーティリティ
    /// </summary>
    public class Ping
    {
        /// <summary>
        /// 2つのタイムスタンプからPingを計算
        /// </summary>
        public static int CalcPing(int m1, int m2)
        {
            return Math.Abs(m1 - m2);
        }
    }
}
