using System;
using System.IO;
using System.Xml.Serialization;
using DecryptPassword;

namespace MirrorOra2MySQL
{
    /// <summary>
    /// ファイル アクセス クラス
    /// </summary>
    class FileAccess
    {
        /// <summary>
        /// データベース設定ファイルの逆シリアライズ
        /// </summary>
        /// <param name="cmn">共通クラス</param>
        /// <returns></returns>
        public static DBConfigData[] ReserializeDBConfigFile()
        {
            // 設定ファイル名
            var BaseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');          // 実行ファイルのあるディレクトリ
            var DbConfFilePath = Path.Combine(BaseDir, Common.DB_CONFIG_FILE);      // データベース設定データは実行ファイルと同一ディレクトリ
            string fileName = DbConfFilePath;

            // XmlSerializerオブジェクトを作成
            XmlSerializer serializer = new XmlSerializer(typeof(DBConfigData[]));

            // 読み込むファイルを開く
            // 逆シリアライズ化
            using (StreamReader sr = new StreamReader(fileName, new System.Text.UTF8Encoding(false)))
            {
                // XMLファイルから読み込み、逆シリアル化する
                DBConfigData[] obj = (DBConfigData[])serializer.Deserialize(sr);

                // ファイルを閉じる
                sr.Close();

                return obj;
            }
        }


    }
}
