using System;

namespace MirrorOra2MySQL
{
    class Common
    {
        // プログラムタイトル
        public static readonly string PROGRAM_TITLE = "[KMD004SC] EMマスタミラーリング";
        public static readonly string PROGRAM_NAME = "MirrorOra2MySQL";
        public static readonly string PROGRAM_VERSION = "230911.01";

        // 定義情報
        public static readonly string DB_CONFIG_FILE = "ConfigDB.xml";

        // メッセージ定義
        public static readonly string MSG_HOWTOUSE = @"
-------------------------------------------------------------------------------
   MirrorOra2MySQL     ::     EMマスタ の ミラーリング
-------------------------------------------------------------------------------

       使用法 :: MirrorOra2MySQL 対象日数 [オプション]
       使用例 :: MirrorOra2MySQL 30 /C

     対象日数 :: 更新日付を検索対象とし、当日から過去遡りの日数
::
:: オプション :
::
           /C :: 追加更新件数のチェックのみ行う - 簡易表示（デフォルト）
           /D :: 追加更新件数のチェックのみ行う - 詳細表示
           /E :: データベースへの追加更新を行う

 対象テーブル :: M0010 担当者マスタ
              :: M0230 M0220 M0210 M0200
              ::   得意先管理マスタ 請求先マスタ 得意先マスタ 得意先名称マスタ
              :: M0300 手配先名称マスタ
              :: M0310 手配先マスタ
              :: M0400 工程グループマスタ
              :: M0410 工程マスタ
              :: M0500 M0520 M0570 M0510
              ::   品目マスタ 品目構成マスタ 品目手順マスタ 品目手順詳細マスタ
";
        public static readonly string MSG_SEPARATOR = 
"-------------------------------------------------------------------------------";

        // エラーメッセージ定義
        public static readonly string ERR_NOT_NUMERIC = "数値を入力してください．";

        public static readonly string MSG_DATABESE_CONFIG_NOT_EXSIST = "データベース設定ファイルが存在しません\n設定ファイルを配置しアプリを再起動してください";
        public static readonly string MSG_FILE_CONFIG_NOT_EXSIST = "ファイル設定ファイルが存在しません\n設定ファイルを配置しアプリを再起動してください";
        public static readonly string MSG_DATABESE_CONNECTION_FAILURE = "データベースへの接続に失敗しました";
        public static readonly string MSG_DATABESE_CLOSE_FAILURE = "データベースへの切断に失敗しました";
        public static readonly string MSG_KM8420_REFRESH_FAILURE = "データベースの更新に失敗しました";

        public static readonly string MSG_PROGRAM_ERROR = "プログラムの想定エラーが発生しました";

    }
    /// <summary>
    /// データベース設定データ クラス
    /// </summary>
    public class DBConfigData
    {
        // プロパティ
        public string User { get; set; }        // ユーザー ID
        public string EncPasswd { get; set; }   // 暗号化パスワード ([KCM002SF] パスワード暗号化アプリ で暗号化した文字列)
        public string Protocol { get; set; }    // 通信プロトコル
        public string Host { get; set; }        // ホスト名または IPv4 アドレス
        public int Port { get; set; }           // ポート番号
        public string ServiceName { get; set; } // サービス名
        public string Schema { get; set; }      // スキーマ
        public string CharSet { get; set; }     // 文字セット
    }


    internal static class AssemblyState
    {
        public const bool IsDebug =
#if DEBUG
        true;
#else
        false;
#endif
    }

}
