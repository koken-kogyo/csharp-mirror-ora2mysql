using System;
using System.Linq;
using Oracle.ManagedDataAccess.Client;
using MySql.Data.MySqlClient;
using System.Data;
using DecryptPassword;

namespace MirrorOra2MySQL
{
    internal class Program
    {
        private static DBConfigData[] dbconfig = new DBConfigData[3];
        private static OracleConnection connOracle = null;
        private static MySqlConnection connMySQL = null;
        private static int day = 0;
        private static bool isUpdate = false;
        private static bool isDisp = false;

        static void Main(string[] args)
        {
            // 定義ファイル読み取り
            dbconfig = FileAccess.ReserializeDBConfigFile();

            // パラメータチェック
            if (args.Length == 0)
            {
                Console.Error.WriteLine(Common.MSG_HOWTOUSE);
                Console.ReadKey();
                Environment.Exit(1);
            }
            if (Int32.TryParse(args[0], out int _day) == false)
            {
                Console.Error.WriteLine(Common.ERR_NOT_NUMERIC);
                if (AssemblyState.IsDebug) Console.ReadKey();
                Environment.Exit(1);
            } else {
                day = _day;
            }
            // 実行モード
            if (args.Length == 2 && args[1].ToString().ToUpper() == "/E")
            {
                isUpdate = true;
            } else if (args.Length == 2 && args[1].ToString().ToUpper() == "/D")
            {
                isDisp= true;
            }
            // 
            DBOpen();
            M0010();
            M0230();
            M0220();
            M0210();
            M0200();
            M0300();
            M0310();
            M0400();
            M0410();
            M0500();
            M0520();
            M0570();
            M0510();
            connOracle.Close();
            connMySQL.Close();
            if (AssemblyState.IsDebug)
            {
                Console.WriteLine("なにかキーを押してください");
                Console.ReadKey();
            }
            Environment.Exit(0);
        }

        // Oracle 接続文字列
        private static string getOracleConnectionString()
        {
            var dpc = new DecryptPasswordClass();
            dpc.DecryptPassword(dbconfig[0].EncPasswd, out string decPasswd);
            var host = dbconfig[0].Host;        // "192.168.3.197";
            var userid = dbconfig[0].User;      // "KOKEN_5";
            var password = decPasswd;           //
            var datasource = $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=KTEST)))";
            return $"User Id={userid};Password={password};Data Source={datasource}";
        }

        // MySQL 接続文字列作成
        private static string getMySQLConnectionString()
        {
            var dpc = new DecryptPasswordClass();
            dpc.DecryptPassword(dbconfig[2].EncPasswd, out string decPasswd);
            var host = dbconfig[2].Host;        // "localhost" or "192.168.96.199" or "192.168.3.197"
            var userid = dbconfig[2].User;      // "koken_1"
            var password = decPasswd;           // 
            var database = dbconfig[2].Schema;  // "koken_1"
            var port = dbconfig[2].Port;        // 3306 or 53306
            return $"Server={host};User ID={userid};Password={password};Database={database};Port={port};";
        }

        private static void DBOpen()
        {
            connOracle = new OracleConnection(getOracleConnectionString());
            try
            {
                connOracle.Open();
                //Console.WriteLine("Oracleデータベース接続確認");
            }
            catch (Exception ex) 
            {
                Console.WriteLine("Oracleデータベースへ接続できませんでした．\r\n" + ex.Message.ToString());
                Environment.Exit(1);
            }
            connMySQL = new MySqlConnection(getMySQLConnectionString());
            try
            {
                connMySQL.Open();
                //Console.WriteLine("MySQLデータベース接続確認");
            }
            catch (Exception ex)
            {
                Console.WriteLine("MySQLデータベースへ接続できませんでした．\r\n" + ex.Message.ToString());
                Environment.Exit(1);
            }
        }
        // M0010 担当者マスタ
        private static void M0010()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0010 担当者マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0010";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL TANCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select TANCD from M0010";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var tancd = row["TANCD"].ToString();
                var sql = $" select * from m0010 where TANCD='{tancd}'";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"TANCD='{tancd}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {tancd}");
                    countInsert++;
                }
                else
                {
                    var mysTANNM = dtUpdate.Rows[0]["TANNM"].ToString().Replace("_5", "");
                    var oraTANNM = row["TANNM"].ToString().Replace("_5", "");
                    if (oraTANNM != mysTANNM ||
                        row["PASSWD"].ToString() != dtUpdate.Rows[0]["PASSWD"].ToString() ||
                        row["ATGCD"].ToString() != dtUpdate.Rows[0]["ATGCD"].ToString()
                        )
                    {
                        dtUpdate.Rows[0]["TANNM"] = row["TANNM"];
                        dtUpdate.Rows[0]["PASSWD"] = row["PASSWD"];
                        dtUpdate.Rows[0]["ATGCD"] = row["ATGCD"];
                        dtUpdate.Rows[0]["UPDTID"] = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        if (isDisp) Console.WriteLine($"Update {tancd}");
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0230 得意先管理マスタ
        private static void M0230()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0230 得意先管理マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0230";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL TKCTLNOを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select TKCTLNO from M0230";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var tkctlno = row["TKCTLNO"].ToString();
                var sql = $" select * from m0230 where TKCTLNO='{tkctlno}'";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"TKCTLNO='{tkctlno}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {tkctlno}");
                    countInsert++;
                }
                else
                {
                    // row["SETULEN"]==DBNull.Value を キャスト出来ない ToString()だと""になる
                    var mysJUYMCNTstr = dtUpdate.Rows[0]["JUYMCNT"].ToString();
                    var mysJUYMCNT = Double.Parse(mysJUYMCNTstr == "" ? "0" : mysJUYMCNTstr);
                    var oraJUYMCNTstr = row["JUYMCNT"].ToString();
                    var oraJUYMCNT = Double.Parse(oraJUYMCNTstr == "" ? "0" : oraJUYMCNTstr);
                    if (row["TKCTLNM"].ToString() != dtUpdate.Rows[0]["TKCTLNM"].ToString() ||
                        row["JUYM"].ToString() != dtUpdate.Rows[0]["JUYM"].ToString() ||
                        oraJUYMCNT != mysJUYMCNT ||
                        row["BFLJUYM"].ToString() != dtUpdate.Rows[0]["BFLJUYM"].ToString() ||
                        row["LJUYM"].ToString() != dtUpdate.Rows[0]["LJUYM"].ToString() ||
                        row["LJUINDT"].ToString() != dtUpdate.Rows[0]["LJUINDT"].ToString()
                        )
                    {
                        dtUpdate.Rows[0]["TKCTLNM"] = row["TKCTLNM"];
                        dtUpdate.Rows[0]["JUYM"] = row["JUYM"];
                        dtUpdate.Rows[0]["JUYMCNT"] = row["JUYMCNT"];
                        dtUpdate.Rows[0]["BFLJUYM"] = row["BFLJUYM"];
                        dtUpdate.Rows[0]["LJUYM"] = row["LJUYM"];
                        dtUpdate.Rows[0]["LJUINDT"] = row["LJUINDT"];
                        dtUpdate.Rows[0]["UPDTID"] = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        if (isDisp) Console.WriteLine($"Update {tkctlno}");
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0220 請求先マスタ
        private static void M0220()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0220 請求先マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0220";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL SKCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select SKCD from M0220";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var skcd = row["SKCD"].ToString();
                var sql = $" select * from m0220 where SKCD='{skcd}'";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"SKCD='{skcd}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {skcd}");
                    countInsert++;
                }
                else
                {
                    if (row["NNDAYCD"].ToString() != dtUpdate.Rows[0]["NNDAYCD"].ToString() ||
                        row["JKDAYCD"].ToString() != dtUpdate.Rows[0]["JKDAYCD"].ToString() ||
                        row["KINKBN"].ToString() != dtUpdate.Rows[0]["KINKBN"].ToString() ||
                        row["KINHASUKBN"].ToString() != dtUpdate.Rows[0]["KINHASUKBN"].ToString() ||
                        row["TAXKBN"].ToString() != dtUpdate.Rows[0]["TAXKBN"].ToString() ||
                        row["TAXHASUKBN"].ToString() != dtUpdate.Rows[0]["TAXHASUKBN"].ToString() ||
                        row["SKDENKBN"].ToString() != dtUpdate.Rows[0]["SKDENKBN"].ToString()
                        )
                    {
                        dtUpdate.Rows[0]["NNDAYCD"] = row["NNDAYCD"];
                        dtUpdate.Rows[0]["JKDAYCD"] = row["JKDAYCD"];
                        dtUpdate.Rows[0]["KINKBN"] = row["KINKBN"];
                        dtUpdate.Rows[0]["KINHASUKBN"] = row["KINHASUKBN"];
                        dtUpdate.Rows[0]["TAXKBN"] = row["TAXKBN"];
                        dtUpdate.Rows[0]["TAXHASUKBN"] = row["TAXHASUKBN"];
                        dtUpdate.Rows[0]["SKDENKBN"] = row["SKDENKBN"];
                        dtUpdate.Rows[0]["UPDTID"] = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        if (isDisp) Console.WriteLine($"Update {skcd}");
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0210 得意先マスタ
        private static void M0210()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0210 得意先マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0210";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL TKCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select TKCD from M0210";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var tkcd = row["TKCD"].ToString();
                var sql = $" select * from m0210 where TKCD='{tkcd}'";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"TKCD='{tkcd}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {tkcd}");
                    countInsert++;
                }
                else
                {
                    // row["SETULEN"]==DBNull.Value を キャスト出来ない ToString()だと""になる
                    var mysLTstr = dtUpdate.Rows[0]["LT"].ToString();
                    var mysLT = Double.Parse(mysLTstr == "" ? "0" : mysLTstr);
                    var oraLTstr = row["LT"].ToString();
                    var oraLT = Double.Parse(oraLTstr == "" ? "0" : oraLTstr);
                    if (row["TRLT"].ToString() != dtUpdate.Rows[0]["TRLT"].ToString() ||
                        row["TRTIME1"].ToString() != dtUpdate.Rows[0]["TRTIME1"].ToString() ||
                        row["TRTIME2"].ToString() != dtUpdate.Rows[0]["TRTIME2"].ToString() ||
                        row["SPDENKBN"].ToString() != dtUpdate.Rows[0]["SPDENKBN"].ToString() ||
                        row["TKCTLNO"].ToString() != dtUpdate.Rows[0]["TKCTLNO"].ToString() ||
                        row["CALTYP"].ToString() != dtUpdate.Rows[0]["CALTYP"].ToString() ||
                        row["SKCD"].ToString() != dtUpdate.Rows[0]["SKCD"].ToString() ||
                        row["PTKCD"].ToString() != dtUpdate.Rows[0]["PTKCD"].ToString() ||
                        row["SPDAY"].ToString() != dtUpdate.Rows[0]["SPDAY"].ToString() ||
                        row["STANCD"].ToString() != dtUpdate.Rows[0]["STANCD"].ToString() ||
                        row["ETANCD"].ToString() != dtUpdate.Rows[0]["ETANCD"].ToString() ||
                        row["YTANCD"].ToString() != dtUpdate.Rows[0]["YTANCD"].ToString() ||
                        row["NJSEPKBN"].ToString() != dtUpdate.Rows[0]["NJSEPKBN"].ToString() ||
                        row["ZENSEPDAY"].ToString() != dtUpdate.Rows[0]["ZENSEPDAY"].ToString() ||
                        row["YGWKBN"].ToString() != dtUpdate.Rows[0]["YGWKBN"].ToString() ||
                        oraLT != mysLT
                        )
                    {
                        dtUpdate.Rows[0]["TRLT"] = row["TRLT"];
                        dtUpdate.Rows[0]["TRTIME1"] = row["TRTIME1"];
                        dtUpdate.Rows[0]["TRTIME2"] = row["TRTIME2"];
                        dtUpdate.Rows[0]["SPDENKBN"] = row["SPDENKBN"];
                        dtUpdate.Rows[0]["TKCTLNO"] = row["TKCTLNO"];
                        dtUpdate.Rows[0]["CALTYP"] = row["CALTYP"];
                        dtUpdate.Rows[0]["SKCD"] = row["SKCD"];
                        dtUpdate.Rows[0]["PTKCD"] = row["PTKCD"];
                        dtUpdate.Rows[0]["SPDAY"] = row["SPDAY"];
                        dtUpdate.Rows[0]["STANCD"] = row["STANCD"];
                        dtUpdate.Rows[0]["ETANCD"] = row["ETANCD"];
                        dtUpdate.Rows[0]["YTANCD"] = row["YTANCD"];
                        dtUpdate.Rows[0]["NJSEPKBN"] = row["NJSEPKBN"];
                        dtUpdate.Rows[0]["ZENSEPDAY"] = row["ZENSEPDAY"];
                        dtUpdate.Rows[0]["YGWKBN"] = row["YGWKBN"];
                        dtUpdate.Rows[0]["UPDTID"] = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        dtUpdate.Rows[0]["LT"] = row["LT"];
                        if (isDisp) Console.WriteLine($"Update {tkcd}");
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0200 手配先名称マスタ
        private static void M0200()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0200 得意先名称マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0200";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL TKCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select TKCD from M0200";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var tkcd = row["TKCD"].ToString();
                var sql = $" select * from m0200 where TKCD='{tkcd}'";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"TKCD='{tkcd}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {tkcd}");
                    countInsert++;
                }
                else
                {
                    if (row["TKNM1"].ToString() != dtUpdate.Rows[0]["TKNM1"].ToString() ||
                        row["TKNM2"].ToString() != dtUpdate.Rows[0]["TKNM2"].ToString() ||
                        row["TKRNM"].ToString() != dtUpdate.Rows[0]["TKRNM"].ToString() ||
                        row["TKTANNM"].ToString() != dtUpdate.Rows[0]["TKTANNM"].ToString() ||
                        row["ZIP"].ToString() != dtUpdate.Rows[0]["ZIP"].ToString() ||
                        row["ADD1"].ToString() != dtUpdate.Rows[0]["ADD1"].ToString() ||
                        row["ADD2"].ToString() != dtUpdate.Rows[0]["ADD2"].ToString() ||
                        row["TEL"].ToString() != dtUpdate.Rows[0]["TEL"].ToString() ||
                        row["FAX"].ToString() != dtUpdate.Rows[0]["FAX"].ToString() ||
                        row["MAIL"].ToString() != dtUpdate.Rows[0]["MAIL"].ToString()
                        )
                    {
                        dtUpdate.Rows[0]["TKNM1"] = row["TKNM1"];
                        dtUpdate.Rows[0]["TKNM2"] = row["TKNM2"];
                        dtUpdate.Rows[0]["TKRNM"] = row["TKRNM"];
                        dtUpdate.Rows[0]["TKTANNM"] = row["TKTANNM"];
                        dtUpdate.Rows[0]["ZIP"] = row["ZIP"];
                        dtUpdate.Rows[0]["ADD1"] = row["ADD1"];
                        dtUpdate.Rows[0]["ADD2"] = row["ADD2"];
                        dtUpdate.Rows[0]["TEL"] = row["TEL"];
                        dtUpdate.Rows[0]["FAX"] = row["FAX"];
                        dtUpdate.Rows[0]["MAIL"] = row["MAIL"];
                        dtUpdate.Rows[0]["UPDTID"] = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        if (isDisp) Console.WriteLine($"Update {tkcd}");
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0300 手配先名称マスタ
        private static void M0300()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0300 手配先名称マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0300";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL ODCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select ODCD from M0300";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var odcd = row["ODCD"].ToString();
                var sql = $" select * from m0300 where ODCD='{odcd}'";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"ODCD='{odcd}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {odcd}");
                    countInsert++;
                }
                else
                {
                    if (row["ODNM1"].ToString() != dtUpdate.Rows[0]["ODNM1"].ToString() ||
                        row["ODNM2"].ToString() != dtUpdate.Rows[0]["ODNM2"].ToString() ||
                        row["ODRNM"].ToString() != dtUpdate.Rows[0]["ODRNM"].ToString() ||
                        row["ODTANNM"].ToString() != dtUpdate.Rows[0]["ODTANNM"].ToString() ||
                        row["ZIP"].ToString() != dtUpdate.Rows[0]["ZIP"].ToString() ||
                        row["ADD1"].ToString() != dtUpdate.Rows[0]["ADD1"].ToString() ||
                        row["ADD2"].ToString() != dtUpdate.Rows[0]["ADD2"].ToString() ||
                        row["TEL"].ToString() != dtUpdate.Rows[0]["TEL"].ToString() ||
                        row["FAX"].ToString() != dtUpdate.Rows[0]["FAX"].ToString() ||
                        row["MAIL"].ToString() != dtUpdate.Rows[0]["MAIL"].ToString() ||
                        row["IOKBN"].ToString() != dtUpdate.Rows[0]["IOKBN"].ToString()
                        )
                    {
                        dtUpdate.Rows[0]["ODNM1"] = row["ODNM1"];
                        dtUpdate.Rows[0]["ODNM2"] = row["ODNM2"];
                        dtUpdate.Rows[0]["ODRNM"] = row["ODRNM"];
                        dtUpdate.Rows[0]["ODTANNM"] = row["ODTANNM"];
                        dtUpdate.Rows[0]["ZIP"] = row["ZIP"];
                        dtUpdate.Rows[0]["ADD1"] = row["ADD1"];
                        dtUpdate.Rows[0]["ADD2"] = row["ADD2"];
                        dtUpdate.Rows[0]["TEL"] = row["TEL"];
                        dtUpdate.Rows[0]["FAX"] = row["FAX"];
                        dtUpdate.Rows[0]["MAIL"] = row["MAIL"];
                        dtUpdate.Rows[0]["IOKBN"] = row["IOKBN"];
                        dtUpdate.Rows[0]["UPDTID"] = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        if (isDisp) Console.WriteLine($"Update {odcd}");
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0310 手配先マスタ
        private static void M0310()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0310 手配先マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0310";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL ODCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select ODCD from M0310";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var odcd = row["ODCD"].ToString();
                var sql = $" select * from m0310 where ODCD='{odcd}'";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"ODCD='{odcd}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {odcd}");
                    countInsert++;
                }
                else
                {
                    if (row["ODGCD"].ToString() != dtUpdate.Rows[0]["ODGCD"].ToString() ||
                        row["ODCTLNO"].ToString() != dtUpdate.Rows[0]["ODCTLNO"].ToString() ||
                        row["SHCD"].ToString() != dtUpdate.Rows[0]["SHCD"].ToString() ||
                        row["PODCD"].ToString() != dtUpdate.Rows[0]["PODCD"].ToString() ||
                        row["SKOKBN"].ToString() != dtUpdate.Rows[0]["SKOKBN"].ToString() ||
                        row["MKBN"].ToString() != dtUpdate.Rows[0]["MKBN"].ToString() ||
                        row["YGWKTPNO"].ToString() != dtUpdate.Rows[0]["YGWKTPNO"].ToString() ||
                        row["JODCDKBN"].ToString() != dtUpdate.Rows[0]["JODCDKBN"].ToString() 
                        )
                    {
                        dtUpdate.Rows[0]["ODGCD"] = row["ODGCD"];
                        dtUpdate.Rows[0]["ODCTLNO"] = row["ODCTLNO"];
                        dtUpdate.Rows[0]["SHCD"] = row["SHCD"];
                        dtUpdate.Rows[0]["PODCD"] = row["PODCD"];
                        dtUpdate.Rows[0]["SKOKBN"] = row["SKOKBN"];
                        dtUpdate.Rows[0]["MKBN"] = row["MKBN"];
                        dtUpdate.Rows[0]["YGWKTPNO"] = row["YGWKTPNO"];
                        dtUpdate.Rows[0]["JODCDKBN"] = row["JODCDKBN"];
                        dtUpdate.Rows[0]["UPDTID"] = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        if (isDisp) Console.WriteLine($"Update {odcd}");
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0400 工程グループマスタ
        private static void M0400()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0400 工程グループマスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0400";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL KTGCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select KTGCD from M0400";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var ktgcd = row["KTGCD"].ToString();
                var sql = $" select * from m0400 where KTGCD='{ktgcd}'";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"KTGCD='{ktgcd}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {ktgcd}");
                    countInsert++;
                }
                else
                {
                    if (row["KTGSEQ"].ToString() != dtUpdate.Rows[0]["KTGSEQ"].ToString() ||
                        row["KTGNM"].ToString() != dtUpdate.Rows[0]["KTGNM"].ToString() ||
                        row["KTGRNM"].ToString() != dtUpdate.Rows[0]["KTGRNM"].ToString()
                        )
                    {
                        dtUpdate.Rows[0]["KTGSEQ"] = row["KTGSEQ"];
                        dtUpdate.Rows[0]["KTGNM"] = row["KTGNM"];
                        dtUpdate.Rows[0]["KTGRNM"] = row["KTGRNM"];
                        dtUpdate.Rows[0]["UPDTID"] = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        if (isDisp) Console.WriteLine($"Update {ktgcd}");
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0410 工程マスタ
        private static void M0410()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0410 工程マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0410";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL KTCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select KTCD from M0410";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var ktcd = row["KTCD"].ToString();
                var sql = $" select * from m0410 where KTCD='{ktcd}'";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"KTCD='{ktcd}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {ktcd}");
                    countInsert++;
                }
                else
                {
                    // row["SETULEN"]==DBNull.Value を キャスト出来ない ToString()だと""になる
                    var mysKTPRICEstr = dtUpdate.Rows[0]["KTPRICE"].ToString();
                    var mysKTPRICE = Double.Parse(mysKTPRICEstr == "" ? "0" : mysKTPRICEstr);
                    var oraKTPRICEstr = row["KTPRICE"].ToString();
                    var oraKTPRICE = Double.Parse(oraKTPRICEstr == "" ? "0" : oraKTPRICEstr);
                    if (row["KTNM"].ToString() != dtUpdate.Rows[0]["KTNM"].ToString() ||
                        row["KTGCD"].ToString() != dtUpdate.Rows[0]["KTGCD"].ToString() ||
                        row["ODCD"].ToString() != dtUpdate.Rows[0]["ODCD"].ToString() ||
                        row["SHINDO"].ToString() != dtUpdate.Rows[0]["SHINDO"].ToString() ||
                        row["TENKAI"].ToString() != dtUpdate.Rows[0]["TENKAI"].ToString() ||
                        row["ODRKBN"].ToString() != dtUpdate.Rows[0]["ODRKBN"].ToString() ||
                        row["LOTKBN"].ToString() != dtUpdate.Rows[0]["LOTKBN"].ToString() ||
                        row["ODANLT"].ToString() != dtUpdate.Rows[0]["ODANLT"].ToString() ||
                        row["TRIALQTY"].ToString() != dtUpdate.Rows[0]["TRIALQTY"].ToString() ||
                        row["UNITQTY"].ToString() != dtUpdate.Rows[0]["UNITQTY"].ToString() ||
                        row["UNITNM"].ToString() != dtUpdate.Rows[0]["UNITNM"].ToString() ||
                        row["HUNITNM"].ToString() != dtUpdate.Rows[0]["HUNITNM"].ToString() ||
                        row["BFLT"].ToString() != dtUpdate.Rows[0]["BFLT"].ToString() ||
                        row["AFLT"].ToString() != dtUpdate.Rows[0]["AFLT"].ToString() ||
                        row["IDANLT"].ToString() != dtUpdate.Rows[0]["IDANLT"].ToString() ||
                        row["ODRLT"].ToString() != dtUpdate.Rows[0]["ODRLT"].ToString() ||
                        row["SAFELT"].ToString() != dtUpdate.Rows[0]["SAFELT"].ToString() ||
                        row["MOLT"].ToString() != dtUpdate.Rows[0]["MOLT"].ToString() ||
                        row["QCLT"].ToString() != dtUpdate.Rows[0]["QCLT"].ToString() ||
                        row["YOLT"].ToString() != dtUpdate.Rows[0]["YOLT"].ToString() ||
                        row["JIKBN"].ToString() != dtUpdate.Rows[0]["JIKBN"].ToString() ||
                        row["QKSKBN"].ToString() != dtUpdate.Rows[0]["QKSKBN"].ToString() ||
                        row["BUHIN"].ToString() != dtUpdate.Rows[0]["BUHIN"].ToString() ||
                        row["CPKTCD"].ToString() != dtUpdate.Rows[0]["CPKTCD"].ToString() ||
                        oraKTPRICE != mysKTPRICE
                        )
                    {
                        dtUpdate.Rows[0]["KTNM"] = row["KTNM"];
                        dtUpdate.Rows[0]["KTGCD"] = row["KTGCD"];
                        dtUpdate.Rows[0]["ODCD"] = row["ODCD"];
                        dtUpdate.Rows[0]["SHINDO"] = row["SHINDO"];
                        dtUpdate.Rows[0]["TENKAI"] = row["TENKAI"];
                        dtUpdate.Rows[0]["ODRKBN"] = row["ODRKBN"];
                        dtUpdate.Rows[0]["LOTKBN"] = row["LOTKBN"];
                        dtUpdate.Rows[0]["ODANLT"] = row["ODANLT"];
                        dtUpdate.Rows[0]["TRIALQTY"] = row["TRIALQTY"];
                        dtUpdate.Rows[0]["UNITQTY"] = row["UNITQTY"];
                        dtUpdate.Rows[0]["UNITNM"] = row["UNITNM"];
                        dtUpdate.Rows[0]["HUNITNM"] = row["HUNITNM"];
                        dtUpdate.Rows[0]["BFLT"] = row["BFLT"];
                        dtUpdate.Rows[0]["AFLT"] = row["AFLT"];
                        dtUpdate.Rows[0]["IDANLT"] = row["IDANLT"];
                        dtUpdate.Rows[0]["ODRLT"] = row["ODRLT"];
                        dtUpdate.Rows[0]["SAFELT"] = row["SAFELT"];
                        dtUpdate.Rows[0]["MOLT"] = row["MOLT"];
                        dtUpdate.Rows[0]["QCLT"] = row["QCLT"];
                        dtUpdate.Rows[0]["YOLT"] = row["YOLT"];
                        dtUpdate.Rows[0]["JIKBN"] = row["JIKBN"];
                        dtUpdate.Rows[0]["QKSKBN"] = row["QKSKBN"];
                        dtUpdate.Rows[0]["UPDTID"] = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        dtUpdate.Rows[0]["BUHIN"] = row["BUHIN"];
                        dtUpdate.Rows[0]["CPKTCD"] = row["CPKTCD"];
                        dtUpdate.Rows[0]["KTPRICE"] = row["KTPRICE"];
                        if (isDisp) Console.WriteLine($"Update {ktcd}");
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0500 品目マスタ
        private static void M0500()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine($"M0500 品目マスタチェック開始 ({day}日間)");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle 直近一週間に更新されたものを次項でチェック
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0500 where updtdt > SYSDATE - {day}"; // and hmcd = '1A7530-48630-1'";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL HMCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select HMCD from M0500";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var hmcd = row["HMCD"].ToString();
                var sql = $" select * from m0500 where hmcd='{hmcd}' ";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"HMCD='{hmcd}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine("Insert " + hmcd);
                    countInsert++;
                } else {
                    // row["SETULEN"]==DBNull.Value を キャスト出来ない ToString()だと""になる
                    var mysSKWEIGHTstr = dtUpdate.Rows[0]["SKWEIGHT"].ToString();
                    var mysSOODstr = dtUpdate.Rows[0]["SOOD"].ToString(); 
                    var mysSOTCstr = dtUpdate.Rows[0]["SOTC"].ToString(); 
                    var mysSOLENstr = dtUpdate.Rows[0]["SOLEN"].ToString();
                    var mysWEIGHTstr = dtUpdate.Rows[0]["WEIGHT"].ToString();
                    var mysSETULENstr = dtUpdate.Rows[0]["SETULEN"].ToString();
                    var mysSPOU1str = dtUpdate.Rows[0]["SPOU1"].ToString();
                    var mysSPOU2str = dtUpdate.Rows[0]["SPOU2"].ToString();
                    var mysSPOU3str = dtUpdate.Rows[0]["SPOU3"].ToString();
                    var mysSKWEIGHT = Double.Parse(mysSKWEIGHTstr == "" ? "0" : mysSKWEIGHTstr);
                    var mysSOOD = Double.Parse(mysSOODstr == "" ? "0" : mysSOODstr);
                    var mysSOTC = Double.Parse(mysSOTCstr == "" ? "0" : mysSOTCstr);
                    var mysSOLEN = Double.Parse(mysSOLENstr == "" ? "0" : mysSOLENstr);
                    var mysWEIGHT = Double.Parse(mysWEIGHTstr == "" ? "0" : mysWEIGHTstr);
                    var mysSETULEN = Double.Parse(mysSETULENstr == "" ? "0" : mysSETULENstr);
                    var mysSPOU1 = Double.Parse(mysSPOU1str == "" ? "0" : mysSPOU1str);
                    var mysSPOU2 = Double.Parse(mysSPOU2str == "" ? "0" : mysSPOU2str);
                    var mysSPOU3 = Double.Parse(mysSPOU3str == "" ? "0" : mysSPOU3str);
                    var oraSKWEIGHTstr = row["SKWEIGHT"].ToString();
                    var oraSOODstr = row["SOOD"].ToString();
                    var oraSOTCstr = row["SOTC"].ToString();
                    var oraSOLENstr = row["SOLEN"].ToString();
                    var oraWEIGHTstr = row["WEIGHT"].ToString();
                    var oraSETULENstr = row["SETULEN"].ToString();
                    var oraSPOU1str = row["SPOU1"].ToString();
                    var oraSPOU2str = row["SPOU2"].ToString();
                    var oraSPOU3str = row["SPOU3"].ToString();
                    var oraSKWEIGHT = Double.Parse(oraSKWEIGHTstr == "" ? "0" : oraSKWEIGHTstr);
                    var oraSOOD = Double.Parse(oraSOODstr == "" ? "0" : oraSOODstr);
                    var oraSOTC = Double.Parse(oraSOTCstr == "" ? "0" : oraSOTCstr);
                    var oraSOLEN = Double.Parse(oraSOLENstr == "" ? "0" : oraSOLENstr);
                    var oraWEIGHT = Double.Parse(oraWEIGHTstr == "" ? "0" : oraWEIGHTstr);
                    var oraSETULEN = Double.Parse(oraSETULENstr == "" ? "0" : oraSETULENstr);
                    var oraSPOU1 = Double.Parse(oraSPOU1str == "" ? "0" : oraSPOU1str);
                    var oraSPOU2 = Double.Parse(oraSPOU2str == "" ? "0" : oraSPOU2str);
                    var oraSPOU3 = Double.Parse(oraSPOU3str == "" ? "0" : oraSPOU3str);
                    // 変更判定
                    if (row["HMNM"].ToString()      != dtUpdate.Rows[0]["HMNM"].ToString() ||
                        row["HMRNM"].ToString()     != dtUpdate.Rows[0]["HMRNM"].ToString() ||
                        row["HMTYPE"].ToString()    != dtUpdate.Rows[0]["HMTYPE"].ToString() ||
                        row["BOMKBN"].ToString()    != dtUpdate.Rows[0]["BOMKBN"].ToString() ||
                        row["PROCESSKBN"].ToString()!= dtUpdate.Rows[0]["PROCESSKBN"].ToString() ||
                        row["MAKER"].ToString()     != dtUpdate.Rows[0]["MAKER"].ToString() ||
                        row["HMKIND"].ToString()    != dtUpdate.Rows[0]["HMKIND"].ToString() ||
                        row["MODEL"].ToString()     != dtUpdate.Rows[0]["MODEL"].ToString() ||
                        row["ZUBAN"].ToString()     != dtUpdate.Rows[0]["ZUBAN"].ToString() ||
                        row["HTKBN"].ToString()     != dtUpdate.Rows[0]["HTKBN"].ToString() ||
                        row["KZAIKBN"].ToString()   != dtUpdate.Rows[0]["KZAIKBN"].ToString() ||
                        row["ODRKBN"].ToString()    != dtUpdate.Rows[0]["ODRKBN"].ToString() ||
                        row["MODEL"].ToString()     != dtUpdate.Rows[0]["MODEL"].ToString() ||
                        row["ODCD1"].ToString()     != dtUpdate.Rows[0]["ODCD1"].ToString() ||
                        row["ODCD2"].ToString()     != dtUpdate.Rows[0]["ODCD2"].ToString() ||
                        row["BUCD"].ToString()      != dtUpdate.Rows[0]["BUCD"].ToString() ||
                        row["BOXCD"].ToString()     != dtUpdate.Rows[0]["BOXCD"].ToString() ||
                        row["BOXQTY"].ToString()    != dtUpdate.Rows[0]["BOXQTY"].ToString() ||
                        row["UKCD"].ToString()      != dtUpdate.Rows[0]["UKCD"].ToString() ||
                        row["TENKAI"].ToString()    != dtUpdate.Rows[0]["TENKAI"].ToString() ||
                        row["SHIJI"].ToString()     != dtUpdate.Rows[0]["SHIJI"].ToString() ||
                        row["LOTKBN"].ToString()    != dtUpdate.Rows[0]["LOTKBN"].ToString() ||
                        row["LOTQTY"].ToString()    != dtUpdate.Rows[0]["LOTQTY"].ToString() ||
                        row["TRIALQTY"].ToString()  != dtUpdate.Rows[0]["TRIALQTY"].ToString() ||
                        row["CUTLT"].ToString()     != dtUpdate.Rows[0]["CUTLT"].ToString() ||
                        row["FIXLT"].ToString()     != dtUpdate.Rows[0]["FIXLT"].ToString() ||
                        row["HENLT"].ToString()     != dtUpdate.Rows[0]["HENLT"].ToString() ||
                        row["TKCD"].ToString()      != dtUpdate.Rows[0]["TKCD"].ToString() ||
                        row["QCNOTE"].ToString()    != dtUpdate.Rows[0]["QCNOTE"].ToString() ||
                        row["NOTE"].ToString()      != dtUpdate.Rows[0]["NOTE"].ToString() ||
                        row["SAFEQTY"].ToString()   != dtUpdate.Rows[0]["SAFEQTY"].ToString() ||
                        row["NJSEPKBN"].ToString()  != dtUpdate.Rows[0]["NJSEPKBN"].ToString() ||
                        row["WKNOTE"].ToString()    != dtUpdate.Rows[0]["WKNOTE"].ToString() ||
                        row["WKCOMMENT"].ToString() != dtUpdate.Rows[0]["WKCOMMENT"].ToString() ||
                        row["UKICD"].ToString()     != dtUpdate.Rows[0]["UKICD"].ToString() ||
                        row["YGWKBN"].ToString()    != dtUpdate.Rows[0]["YGWKBN"].ToString() ||
                        row["SKBOXCD"].ToString()   != dtUpdate.Rows[0]["SKBOXCD"].ToString() ||
                        row["SKBOXQTY"].ToString()  != dtUpdate.Rows[0]["SKBOXQTY"].ToString() ||
                        row["SKBUCD"].ToString()    != dtUpdate.Rows[0]["SKBUCD"].ToString() ||
                        row["SKHIASU"].ToString()   != dtUpdate.Rows[0]["SKHIASU"].ToString() ||
                        row["SKNIS"].ToString()     != dtUpdate.Rows[0]["SKNIS"].ToString() ||
                        oraSKWEIGHT != mysSKWEIGHT ||
                        row["SKTNOTE1"].ToString()  != dtUpdate.Rows[0]["SKTNOTE1"].ToString() ||
                        row["SKTNOTE2"].ToString()  != dtUpdate.Rows[0]["SKTNOTE2"].ToString() ||
                        row["SKNOTE"].ToString()    != dtUpdate.Rows[0]["SKNOTE"].ToString() ||
                        oraSOOD != mysSOOD ||
                        oraSOTC != mysSOTC ||
                        oraSOLEN != mysSOLEN ||
                        oraWEIGHT != mysWEIGHT ||
                        row["ZAINM"].ToString() != dtUpdate.Rows[0]["ZAINM"].ToString() ||
                        row["KJNM"].ToString() != dtUpdate.Rows[0]["KJNM"].ToString() ||
                        oraSETULEN != mysSETULEN ||
                        oraSPOU1 != mysSPOU1 ||
                        oraSPOU2 != mysSPOU2 ||
                        oraSPOU3 != mysSPOU3 ||
                        row["WEIGHTKBN"].ToString() != dtUpdate.Rows[0]["WEIGHTKBN"].ToString()
                        )
                    {
                        // 楽しようと思ったが・・・
                        // dtUpdate.Clear();
                        // dtUpdate.ImportRow(row);
                        // dtUpdate.AcceptChanges();
                        // dtUpdate.Rows[0].SetModified();
                        // row.ItemArray.CopyTo(dtUpdate.Rows[0].ItemArray, 0);
                        dtUpdate.Rows[0]["HMNM"]        = row["HMNM"];
                        dtUpdate.Rows[0]["HMRNM"]       = row["HMRNM"];
                        dtUpdate.Rows[0]["HMTYPE"]      = row["HMTYPE"];
                        dtUpdate.Rows[0]["BOMKBN"]      = row["BOMKBN"];
                        dtUpdate.Rows[0]["PROCESSKBN"]  = row["PROCESSKBN"];
                        dtUpdate.Rows[0]["MAKER"]       = row["MAKER"];
                        dtUpdate.Rows[0]["HMKIND"]      = row["HMKIND"];
                        dtUpdate.Rows[0]["MODEL"]       = row["MODEL"];
                        dtUpdate.Rows[0]["ZUBAN"]       = row["ZUBAN"];
                        dtUpdate.Rows[0]["HTKBN"]       = row["HTKBN"];
                        dtUpdate.Rows[0]["KZAIKBN"]     = row["KZAIKBN"];
                        dtUpdate.Rows[0]["ODRKBN"]      = row["ODRKBN"];
                        dtUpdate.Rows[0]["MODEL"]       = row["MODEL"];
                        dtUpdate.Rows[0]["ODCD1"]       = row["ODCD1"];
                        dtUpdate.Rows[0]["ODCD2"]       = row["ODCD2"];
                        dtUpdate.Rows[0]["BUCD"]        = row["BUCD"];
                        dtUpdate.Rows[0]["BOXCD"]       = row["BOXCD"];
                        dtUpdate.Rows[0]["BOXQTY"]      = row["BOXQTY"];
                        dtUpdate.Rows[0]["UKCD"]        = row["UKCD"];
                        dtUpdate.Rows[0]["TENKAI"]      = row["TENKAI"];
                        dtUpdate.Rows[0]["SHIJI"]       = row["SHIJI"];
                        dtUpdate.Rows[0]["LOTKBN"]      = row["LOTKBN"];
                        dtUpdate.Rows[0]["LOTQTY"]      = row["LOTQTY"];
                        dtUpdate.Rows[0]["TRIALQTY"]    = row["TRIALQTY"];
                        dtUpdate.Rows[0]["CUTLT"]       = row["CUTLT"];
                        dtUpdate.Rows[0]["FIXLT"]       = row["FIXLT"];
                        dtUpdate.Rows[0]["HENLT"]       = row["HENLT"];
                        dtUpdate.Rows[0]["TKCD"]        = row["TKCD"];
                        dtUpdate.Rows[0]["QCNOTE"]      = row["QCNOTE"];
                        dtUpdate.Rows[0]["NOTE"]        = row["NOTE"];
                        dtUpdate.Rows[0]["SAFEQTY"]     = row["SAFEQTY"];
                        dtUpdate.Rows[0]["NJSEPKBN"]    = row["NJSEPKBN"];
                        dtUpdate.Rows[0]["WKNOTE"]      = row["WKNOTE"];
                        dtUpdate.Rows[0]["WKCOMMENT"]   = row["WKCOMMENT"];
                        dtUpdate.Rows[0]["UKICD"]       = row["UKICD"];
                        dtUpdate.Rows[0]["YGWKBN"]      = row["YGWKBN"];
                        dtUpdate.Rows[0]["SKBOXCD"]     = row["SKBOXCD"];
                        dtUpdate.Rows[0]["SKBOXQTY"]    = row["SKBOXQTY"];
                        dtUpdate.Rows[0]["SKBUCD"]      = row["SKBUCD"];
                        dtUpdate.Rows[0]["SKHIASU"]     = row["SKHIASU"];
                        dtUpdate.Rows[0]["SKNIS"]       = row["SKNIS"];
                        dtUpdate.Rows[0]["SKWEIGHT"]    = row["SKWEIGHT"];
                        dtUpdate.Rows[0]["SKTNOTE1"]    = row["SKTNOTE1"];
                        dtUpdate.Rows[0]["SKTNOTE2"]    = row["SKTNOTE2"];
                        dtUpdate.Rows[0]["SKNOTE"]      = row["SKNOTE"];
                        dtUpdate.Rows[0]["SOOD"]        = row["SOOD"];
                        dtUpdate.Rows[0]["SOTC"]        = row["SOTC"];
                        dtUpdate.Rows[0]["SOLEN"]       = row["SOLEN"];
                        dtUpdate.Rows[0]["WEIGHT"]      = row["WEIGHT"];
                        dtUpdate.Rows[0]["ZAINM"]       = row["ZAINM"];
                        dtUpdate.Rows[0]["KJNM"]        = row["KJNM"];
                        dtUpdate.Rows[0]["SETULEN"]     = row["SETULEN"];
                        dtUpdate.Rows[0]["SPOU1"]       = row["SPOU1"];
                        dtUpdate.Rows[0]["SPOU2"]       = row["SPOU2"];
                        dtUpdate.Rows[0]["SPOU3"]       = row["SPOU3"];
                        dtUpdate.Rows[0]["UPDTID"]      = "11014";
                        dtUpdate.Rows[0]["UPDTDT"]      = DateTime.Now.ToString();
                        dtUpdate.Rows[0]["WEIGHTKBN"]   = row["WEIGHTKBN"];
                        if (isDisp) Console.WriteLine("Update " + hmcd);
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            } else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0520 品目構成マスタ
        private static void M0520()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine($"M0520 品目構成マスタチェック開始 ({day}日間)");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle 直近一週間に更新されたものを次項でチェック
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0520 where updtdt > SYSDATE - {day}";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL HMCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select OYAHMCD, SEQ from M0520";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var oyahmcd = row["OYAHMCD"].ToString();
                var seq = row["SEQ"].ToString();
                var sql = $" select * from m0520 where OYAHMCD='{oyahmcd}' and SEQ={seq} ";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"OYAHMCD='{oyahmcd}' and SEQ={seq}").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {oyahmcd} - {seq}");
                    countInsert++;
                }
                else
                {
                    if (row["BOMSEQ"].ToString()    != dtUpdate.Rows[0]["BOMSEQ"].ToString() ||
                        row["KTCD"].ToString()      != dtUpdate.Rows[0]["KTCD"].ToString() ||
                        row["KOHMCD"].ToString()    != dtUpdate.Rows[0]["KOHMCD"].ToString() ||
                        row["BOMKBN"].ToString()    != dtUpdate.Rows[0]["BOMKBN"].ToString() ||
                        row["KOQTY"].ToString()     != dtUpdate.Rows[0]["KOQTY"].ToString() ||
                        row["OYAQTY"].ToString()    != dtUpdate.Rows[0]["OYAQTY"].ToString() ||
                        row["VALDTF"].ToString()    != dtUpdate.Rows[0]["VALDTF"].ToString() ||
                        row["VALDTT"].ToString()    != dtUpdate.Rows[0]["VALDTT"].ToString() 
                        )
                    {
                        dtUpdate.Rows[0]["BOMSEQ"]  = row["BOMSEQ"];
                        dtUpdate.Rows[0]["KTCD"]    = row["KTCD"];
                        dtUpdate.Rows[0]["KOHMCD"]  = row["KOHMCD"];
                        dtUpdate.Rows[0]["BOMKBN"]  = row["BOMKBN"];
                        dtUpdate.Rows[0]["KOQTY"]   = row["KOQTY"];
                        dtUpdate.Rows[0]["OYAQTY"]  = row["OYAQTY"];
                        dtUpdate.Rows[0]["VALDTF"]  = row["VALDTF"];
                        dtUpdate.Rows[0]["VALDTT"]  = row["VALDTT"];
                        dtUpdate.Rows[0]["UPDTID"]  = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        if (isDisp) Console.WriteLine($"Update {oyahmcd} - {seq}");
                        countUpdate++;
                    }
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0570 品目手順マスタ
        private static void M0570()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine($"M0570 品目手順マスタチェック開始 ({day}日間)");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle 直近一週間に更新されたものを次項でチェック
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0570 where updtdt > SYSDATE - {day}";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL HMCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select HMCD, VALDTF from M0570";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var hmcd = row["HMCD"].ToString();
                var valdtf = row["VALDTF"].ToString();
                var sql = $" select * from m0570 where HMCD='{hmcd}' and VALDTF='{valdtf}'";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"HMCD='{hmcd}' and VALDTF='{valdtf}'").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) Console.WriteLine($"Insert {hmcd} - {valdtf}");
                    countInsert++;
                }
                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0510 品目手順詳細マスタ
        private static void M0510()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine($"M0510 品目手順詳細マスタチェック開始 ({day}日間)");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle 直近一週間に更新されたものを次項でチェック
            var dtOra = new DataTable();
            var sqlOra = $"select * from M0510 where updtdt > SYSDATE - {day}";
            var oracleCommand = new OracleCommand(sqlOra);
            oracleCommand.Connection = connOracle;
            OracleDataReader oracleReader = oracleCommand.ExecuteReader();
            dtOra.Load(oracleReader);
            // MySQL HMCDを全件取得
            var dtMySQL = new DataTable();
            var sqlMySQL = "select HMCD, VALDTF, KTSEQ from M0510";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            myDa.Fill(dtMySQL);
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            var countDelete = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var hmcd = row["HMCD"].ToString();
                var valdtf = row["VALDTF"].ToString();
                var ktseq = row["KTSEQ"].ToString();
                var ktcd = row["KTCD"].ToString();
                var sql = $" select * from m0510 where HMCD='{hmcd}' and VALDTF='{valdtf}' and KTSEQ={ktseq} ";
                var adapter = new MySqlDataAdapter();
                adapter.SelectCommand = new MySqlCommand(sql, connMySQL);
                var buider = new MySqlCommandBuilder(adapter);
                var dtUpdate = new DataTable();
                adapter.Fill(dtUpdate);

                if (dtMySQL.Select($"HMCD='{hmcd}' and VALDTF='{valdtf}' and KTSEQ={ktseq}").Count() == 0)
                {
                    dtUpdate.ImportRow(row);
                    dtUpdate.Rows[0].SetAdded();
                    if (isDisp) 
                        Console.WriteLine($"Insert {hmcd.PadRight(24)} - {valdtf} - {ktseq}");
                    countInsert++;
                }
                else
                {
                    if (row["KTCD"].ToString() != dtUpdate.Rows[0]["KTCD"].ToString() ||
                        row["ODCD"].ToString() != dtUpdate.Rows[0]["ODCD"].ToString() ||
                        row["SHINDO"].ToString() != dtUpdate.Rows[0]["SHINDO"].ToString() ||
                        row["TENKAI"].ToString() != dtUpdate.Rows[0]["TENKAI"].ToString() ||
                        row["CARD"].ToString() != dtUpdate.Rows[0]["CARD"].ToString() ||
                        row["ODRKBN"].ToString() != dtUpdate.Rows[0]["ODRKBN"].ToString() ||
                        row["LOTKBN"].ToString() != dtUpdate.Rows[0]["LOTKBN"].ToString() ||
                        row["LOTQTY"].ToString() != dtUpdate.Rows[0]["LOTQTY"].ToString() ||
                        row["ODANLT"].ToString() != dtUpdate.Rows[0]["ODANLT"].ToString() ||
                        row["BFLT"].ToString() != dtUpdate.Rows[0]["BFLT"].ToString() ||
                        row["AFLT"].ToString() != dtUpdate.Rows[0]["AFLT"].ToString() ||
                        row["IDANLT"].ToString() != dtUpdate.Rows[0]["IDANLT"].ToString() ||
                        row["ODRLT"].ToString() != dtUpdate.Rows[0]["ODRLT"].ToString() ||
                        row["SAFELT"].ToString() != dtUpdate.Rows[0]["SAFELT"].ToString() ||
                        row["MOLT"].ToString() != dtUpdate.Rows[0]["MOLT"].ToString() ||
                        row["QCLT"].ToString() != dtUpdate.Rows[0]["QCLT"].ToString() ||
                        row["YOLT"].ToString() != dtUpdate.Rows[0]["YOLT"].ToString() ||
                        row["TRIALQTY"].ToString() != dtUpdate.Rows[0]["TRIALQTY"].ToString() ||
                        row["UNITQTY"].ToString() != dtUpdate.Rows[0]["UNITQTY"].ToString() ||
                        row["UNITNM"].ToString() != dtUpdate.Rows[0]["UNITNM"].ToString() ||
                        row["HUNITNM"].ToString() != dtUpdate.Rows[0]["HUNITNM"].ToString() ||
                        float.Parse(row["HQTY"].ToString()) != float.Parse(dtUpdate.Rows[0]["HQTY"].ToString()) ||
                        row["KQTY"].ToString() != dtUpdate.Rows[0]["KQTY"].ToString() ||
                        row["MCNO"].ToString() != dtUpdate.Rows[0]["MCNO"].ToString() ||
                        row["TOOLNO"].ToString() != dtUpdate.Rows[0]["TOOLNO"].ToString() ||
                        row["WKNOTE"].ToString() != dtUpdate.Rows[0]["WKNOTE"].ToString() ||
                        row["WKCOMMENT"].ToString() != dtUpdate.Rows[0]["WKCOMMENT"].ToString() ||
                        row["BOXCD"].ToString() != dtUpdate.Rows[0]["BOXCD"].ToString() ||
                        row["SAFEQTY"].ToString() != dtUpdate.Rows[0]["SAFEQTY"].ToString() ||
                        row["STKTKBN"].ToString() != dtUpdate.Rows[0]["STKTKBN"].ToString() ||
                        row["EDKTKBN"].ToString() != dtUpdate.Rows[0]["EDKTKBN"].ToString() ||
                        row["YGWKBN"].ToString() != dtUpdate.Rows[0]["YGWKBN"].ToString() ||
                        row["JIKBN"].ToString() != dtUpdate.Rows[0]["JIKBN"].ToString() ||
                        row["MKBN"].ToString() != dtUpdate.Rows[0]["MKBN"].ToString() ||
                        row["HTKBN"].ToString() != dtUpdate.Rows[0]["HTKBN"].ToString() ||
                        row["THSSKBN"].ToString() != dtUpdate.Rows[0]["THSSKBN"].ToString()
                        )
                    {
                        dtUpdate.Rows[0]["KTCD"] = row["KTCD"];
                        dtUpdate.Rows[0]["ODCD"] = row["ODCD"];
                        dtUpdate.Rows[0]["SHINDO"] = row["SHINDO"];
                        dtUpdate.Rows[0]["TENKAI"] = row["TENKAI"];
                        dtUpdate.Rows[0]["CARD"] = row["CARD"];
                        dtUpdate.Rows[0]["ODRKBN"] = row["ODRKBN"];
                        dtUpdate.Rows[0]["LOTKBN"] = row["LOTKBN"];
                        dtUpdate.Rows[0]["LOTQTY"] = row["LOTQTY"];
                        dtUpdate.Rows[0]["ODANLT"] = row["ODANLT"];
                        dtUpdate.Rows[0]["BFLT"] = row["BFLT"];
                        dtUpdate.Rows[0]["AFLT"] = row["AFLT"];
                        dtUpdate.Rows[0]["IDANLT"] = row["IDANLT"];
                        dtUpdate.Rows[0]["ODRLT"] = row["ODRLT"];
                        dtUpdate.Rows[0]["SAFELT"] = row["SAFELT"];
                        dtUpdate.Rows[0]["MOLT"] = row["MOLT"];
                        dtUpdate.Rows[0]["QCLT"] = row["QCLT"];
                        dtUpdate.Rows[0]["YOLT"] = row["YOLT"];
                        dtUpdate.Rows[0]["TRIALQTY"] = row["TRIALQTY"];
                        dtUpdate.Rows[0]["UNITQTY"] = row["UNITQTY"];
                        dtUpdate.Rows[0]["UNITNM"] = row["UNITNM"];
                        dtUpdate.Rows[0]["HUNITNM"] = row["HUNITNM"];
                        dtUpdate.Rows[0]["HQTY"] = row["HQTY"];
                        dtUpdate.Rows[0]["KQTY"] = row["KQTY"];
                        dtUpdate.Rows[0]["MCNO"] = row["MCNO"];
                        dtUpdate.Rows[0]["TOOLNO"] = row["TOOLNO"];
                        dtUpdate.Rows[0]["WKNOTE"] = row["WKNOTE"];
                        dtUpdate.Rows[0]["WKCOMMENT"] = row["WKCOMMENT"];
                        dtUpdate.Rows[0]["BOXCD"] = row["BOXCD"];
                        dtUpdate.Rows[0]["SAFEQTY"] = row["SAFEQTY"];
                        dtUpdate.Rows[0]["STKTKBN"] = row["STKTKBN"];
                        dtUpdate.Rows[0]["EDKTKBN"] = row["EDKTKBN"];
                        dtUpdate.Rows[0]["YGWKBN"] = row["YGWKBN"];
                        dtUpdate.Rows[0]["JIKBN"] = row["JIKBN"];
                        dtUpdate.Rows[0]["MKBN"] = row["MKBN"];
                        dtUpdate.Rows[0]["UPDTID"] = "11014";
                        dtUpdate.Rows[0]["UPDTDT"] = DateTime.Now.ToString();
                        dtUpdate.Rows[0]["HTKBN"] = row["HTKBN"];
                        dtUpdate.Rows[0]["THSSKBN"] = row["THSSKBN"];
                        if (isDisp) 
                            Console.WriteLine($"Update {hmcd.PadRight(24)} - {valdtf} - {ktseq}");
                        countUpdate++;

                        // 以下の対策 2023-09-12 y.w
                        // 手順詳細マスタはよく削除されることが判明
                        // Duplicate entry '94-1103-2007-01-01 00:00:00-TRWH' for key 'm0510.UQ_M0510_1'
                        // 手順10, 20, 30 ある場合の途中の 20 が消されると UNIQE KEYが被る
                        // ⇒ ①データベース制約にKTSEQを追加して対処
                        // ⇒ ②削除明細を検索しあれば削除
                        var sqlDel = $" select * from m0510 where HMCD='{hmcd}' and VALDTF='{valdtf}' and KTCD='{ktcd}' and KTSEQ!={ktseq}";
                        var adapterDel = new MySqlDataAdapter();
                        adapterDel.SelectCommand = new MySqlCommand(sqlDel, connMySQL);
                        var buiderDel = new MySqlCommandBuilder(adapterDel);
                        var dtDelete = new DataTable();
                        adapterDel.Fill(dtDelete);
                        if (dtDelete.Rows.Count != 0)
                        {
                            if (isDisp)
                                Console.WriteLine($"Delete {hmcd.PadRight(24)} - {valdtf} - {ktseq}");
                            dtDelete.Rows[0].Delete();
                            if (isUpdate) adapter.Update(dtDelete);
                            countDelete++;
                        }
                    }
                }

                // 追加更新を実行
                if (isUpdate) adapter.Update(dtUpdate);

            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
                Console.WriteLine("　　削除件数：" + String.Format("{0:#,0}", countDelete) + " 件");
            }
            else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }

    }
}
