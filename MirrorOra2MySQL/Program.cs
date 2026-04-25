using DecryptPassword;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Text;
using static Mysqlx.Expect.Open.Types.Condition.Types;

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
        private static bool isMaintenance = false;

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
            if (args.Length >= 2 && args[1].ToString().ToUpper() == "/E")
            {
                isUpdate = true;
            } else if (args.Length >= 2 && args[1].ToString().ToUpper() == "/D")
            {
                isDisp= true;
            }
            // メンテナンスモード
            if (args.Length >= 3 && args[2].ToString().ToUpper() == "/M")
            {
                isMaintenance = true;
            }
            // 
            DBOpen();
            if (isMaintenance) M0510_MaintenanceCopy_Bulk();
            S0820();
            M0010();
            M0200();
            M0230();
            M0220();
            M0210();
            M0330();
            M0300();
            M0310();
            M0400();
            M0410();
            M0500();
            M0520();
            M0570();
            M0510();
            M0600();
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
        private static string GetOracleConnectionString()
        {
            var dpc = new DecryptPasswordClass();
            dpc.DecryptPassword(dbconfig[0].EncPasswd, out string decPasswd);
            var host = dbconfig[0].Host;
            var userid = dbconfig[0].User;
            var password = decPasswd;
            var datasource = $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=KOKEN)))";
            Console.WriteLine($"Oracle[HOST:{host}/UserID:{userid}]");
            return $"User Id={userid};Password={password};Data Source={datasource}";
        }

        // MySQL 接続文字列作成
        private static string GetMySQLConnectionString()
        {
            var dpc = new DecryptPasswordClass();
            dpc.DecryptPassword(dbconfig[2].EncPasswd, out string decPasswd);
            var host = dbconfig[2].Host;
            var userid = dbconfig[2].User;
            var password = decPasswd;
            var database = dbconfig[2].Schema;
            var port = dbconfig[2].Port;
            Console.WriteLine($"MySQL [HOST:{host}/UserID:{userid}]");
            return $"Server={host};User ID={userid};Password={password};Database={database};Port={port};";
        }

        private static void DBOpen()
        {
            connOracle = new OracleConnection(GetOracleConnectionString());
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
            connMySQL = new MySqlConnection(GetMySQLConnectionString());
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
        //
        // NULL 安全比較（ObjectEquals）
        // Double / int / decimal / DateTime / string / NULL
        // 全部これで比較できます。
        //
        private static bool ObjectEquals(object a, object b)
        {
            if (a == DBNull.Value) a = null;
            if (b == DBNull.Value) b = null;

            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            // Oracle NUMBER(5,1) → double
            // MySQL decimal(5,1) → decimal
            // → 両方 decimal に寄せて比較
            if (IsNumeric(a) && IsNumeric(b))
            {
                decimal da = Convert.ToDecimal(a);
                decimal db = Convert.ToDecimal(b);
                return da == db;
            }

            // DateTime
            if (a is DateTime ta && b is DateTime tb)
                return ta == tb;

            // その他は文字列比較
            return a.ToString() == b.ToString();
        }

        private static bool IsNumeric(object value)
        {
            return value is sbyte || value is byte ||
                   value is short || value is ushort ||
                   value is int || value is uint ||
                   value is long || value is ulong ||
                   value is float || value is double ||
                   value is decimal;
        }
        // M0010 担当者マスタ（200件）
        private static void M0010()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0010 担当者マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0010", connOracle).ExecuteReader());

            // MySQL
            var dtMySQL = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0010", connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["TANCD"].ToString(), r => r);

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var tancd = oraRow["TANCD"].ToString();
                var key = tancd;

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    var newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {tancd}");
                    countInsert++;
                }
                else
                {
                    // 差分チェック（全列比較）
                    var oraTANNM = oraRow["TANNM"].ToString().Replace("_5", "");
                    var mysTANNM = myRow["TANNM"].ToString().Replace("_5", "");
                    if (mysTANNM != oraTANNM ||
                        myRow["PASSWD"].ToString() != oraRow["PASSWD"].ToString() ||
                        myRow["ATGCD"].ToString() != oraRow["ATGCD"].ToString() ||
                        myRow["INSTID"].ToString() != oraRow["INSTID"].ToString() ||
                        myRow["INSTDT"].ToString() != oraRow["INSTDT"].ToString() ||
                        myRow["UPDTID"].ToString() != oraRow["UPDTID"].ToString() ||
                        myRow["UPDTDT"].ToString() != oraRow["UPDTDT"].ToString()
                        )
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {tancd}");
                        countUpdate++;
                    }
                }
            }
            // 追加更新を実行
            if (countInsert + countUpdate > 0)
            {
                if (isUpdate) myDa.Update(dtMySQL);
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
        // S0820 カレンダーマスタ（当日-30日～で4万件位）
        private static void S0820()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("S0820 カレンダーマスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            var sqlOra = $"select * from S0820";
            if (!isMaintenance) sqlOra += " where YMD > SYSDATE - 365";
            dtOra.Load(new OracleCommand(sqlOra, connOracle).ExecuteReader());

            // MySQL
            var dtMySQL = new DataTable();
            var sqlMySQL = "select * from S0820";
            if (!isMaintenance) sqlMySQL += " where YMD > (CURRENT_DATE - interval 365 day)";
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => $"{r["CALTYP"]}_{r["YMD"]}", r => r);

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var caltyp = oraRow["CALTYP"].ToString();
                var ymd = oraRow["YMD"].ToString();
                var key = $"{oraRow["CALTYP"]}_{oraRow["YMD"]}";

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    DataRow newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow; // 追加した行も辞書に入れる
                    if (isDisp) Console.WriteLine($"Insert {caltyp} - {ymd}");
                    countInsert++;
                }
                else
                {
                    // 違えば UPDATE
                    if (myRow["WKKBN"].ToString() != oraRow["WKKBN"].ToString() ||
                        myRow["UPDTID"].ToString() != oraRow["UPDTID"].ToString() ||
                        myRow["UPDTDT"].ToString() != oraRow["UPDTDT"].ToString())
                        {
                            // UPDATE（全列コピー）
                            myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {caltyp} - {ymd}");
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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
        // M0230 得意先管理マスタ（100件）
        private static void M0230()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0230 得意先管理マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0230", connOracle).ExecuteReader());

            // MySQL を全件取得
            var dtMySQL = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0230", connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["TKCTLNO"].ToString(), r => r);

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var tkctlno = oraRow["TKCTLNO"].ToString();
                var key = tkctlno;

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    DataRow newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {tkctlno}");
                    countInsert++;
                }
                else
                {
                    // Double 比較（JUYMCNT）
                    var oraCnt = oraRow["JUYMCNT"].ToDoubleSafe();
                    var myCnt = myRow["JUYMCNT"].ToDoubleSafe();

                    // どれか1つでも違えば UPDATE
                    bool isDiff =
                        myRow["TKCTLNM"].ToString() != oraRow["TKCTLNM"].ToString() ||
                        myRow["JUYM"].ToString() != oraRow["JUYM"].ToString() ||
                        !myCnt.NearlyEquals(oraCnt) ||
                        myRow["BFLJUYM"].ToString() != oraRow["BFLJUYM"].ToString() ||
                        myRow["LJUYM"].ToString() != oraRow["LJUYM"].ToString() ||
                        myRow["LJUINDT"].ToString() != oraRow["LJUINDT"].ToString() ||
                        myRow["UPDTID"].ToString() != oraRow["UPDTID"].ToString() ||
                        myRow["UPDTDT"].ToString() != oraRow["UPDTDT"].ToString();

                    if (isDiff)
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {tkctlno}");
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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
        // M0220 請求先マスタ（300件）
        private static void M0220()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0220 請求先マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0220", connOracle).ExecuteReader());

            // MySQL
            var dtMySQL = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0220", connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["SKCD"].ToString(), r => r);

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var skcd = oraRow["SKCD"].ToString();
                var key = skcd;

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    DataRow newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {skcd}");
                    countInsert++;
                }
                else
                {
                    // どれか1つでも違えば UPDATE
                    bool isDiff =
                        myRow["NNDAYCD"].ToString() != oraRow["NNDAYCD"].ToString() ||
                        myRow["JKDAYCD"].ToString() != oraRow["JKDAYCD"].ToString() ||
                        myRow["KINKBN"].ToString() != oraRow["KINKBN"].ToString() ||
                        myRow["KINHASUKBN"].ToString() != oraRow["KINHASUKBN"].ToString() ||
                        myRow["TAXKBN"].ToString() != oraRow["TAXKBN"].ToString() ||
                        myRow["TAXHASUKBN"].ToString() != oraRow["TAXHASUKBN"].ToString() ||
                        myRow["SKDENKBN"].ToString() != oraRow["SKDENKBN"].ToString();

                    if (isDiff)
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {skcd}");
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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
        // M0210 得意先マスタ（300件）
        private static void M0210()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0210 得意先マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0210", connOracle).ExecuteReader());

            // MySQL
            var dtMySQL = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0210", connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["TKCD"].ToString(), r => r);

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var tkcd = oraRow["TKCD"].ToString();
                var key = tkcd;

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    var newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {tkcd}");
                    countInsert++;
                }
                else
                {
                    // DBNullあり Int 安全比較（LT）
                    var oraCnt = oraRow["LT"].ToIntNullable();
                    var myCnt = myRow["LT"].ToIntNullable();

                    // どれか1つでも違えば UPDATE
                    bool isDiff =
                        myRow["TRLT"].ToString() != oraRow["TRLT"].ToString() ||
                        myRow["TRTIME1"].ToString() != oraRow["TRTIME1"].ToString() ||
                        myRow["TRTIME2"].ToString() != oraRow["TRTIME2"].ToString() ||
                        myRow["SPDENKBN"].ToString() != oraRow["SPDENKBN"].ToString() ||
                        myRow["TKCTLNO"].ToString() != oraRow["TKCTLNO"].ToString() ||
                        myRow["CALTYP"].ToString() != oraRow["CALTYP"].ToString() ||
                        myRow["SKCD"].ToString() != oraRow["SKCD"].ToString() ||
                        myRow["PTKCD"].ToString() != oraRow["PTKCD"].ToString() ||
                        myRow["SPDAY"].ToString() != oraRow["SPDAY"].ToString() ||
                        myRow["STANCD"].ToString() != oraRow["STANCD"].ToString() ||
                        myRow["ETANCD"].ToString() != oraRow["ETANCD"].ToString() ||
                        myRow["YTANCD"].ToString() != oraRow["YTANCD"].ToString() ||
                        myRow["NJSEPKBN"].ToString() != oraRow["NJSEPKBN"].ToString() ||
                        myRow["ZENSEPDAY"].ToString() != oraRow["ZENSEPDAY"].ToString() ||
                        myRow["YGWKBN"].ToString() != oraRow["YGWKBN"].ToString() ||
                        myRow["UPDTID"].ToString() != oraRow["UPDTID"].ToString() ||
                        myRow["UPDTDT"].ToString() != oraRow["UPDTDT"].ToString() ||
                         !myCnt.IntEquals(oraCnt);

                    if (isDiff)
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {tkcd}");
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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
        // M0200 手配先名称マスタ（300件）
        private static void M0200()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0200 得意先名称マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0200", connOracle).ExecuteReader());

            // MySQL
            var dtMySQL = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0200", connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["TKCD"].ToString(), r => r);
            
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var tkcd = oraRow["TKCD"].ToString();
                var key = tkcd;

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    var newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {tkcd}");
                    countInsert++;
                }
                else
                {
                    // どれか1つでも違えば UPDATE
                    bool isDiff =
                        myRow["TKNM1"].ToString() != oraRow["TKNM1"].ToString() ||
                        myRow["TKNM2"].ToString() != oraRow["TKNM2"].ToString() ||
                        myRow["TKRNM"].ToString() != oraRow["TKRNM"].ToString() ||
                        myRow["TKTANNM"].ToString() != oraRow["TKTANNM"].ToString() ||
                        myRow["ZIP"].ToString() != oraRow["ZIP"].ToString() ||
                        myRow["ADD1"].ToString() != oraRow["ADD1"].ToString() ||
                        myRow["ADD2"].ToString() != oraRow["ADD2"].ToString() ||
                        myRow["TEL"].ToString() != oraRow["TEL"].ToString() ||
                        myRow["FAX"].ToString() != oraRow["FAX"].ToString() ||
                        myRow["MAIL"].ToString() != oraRow["MAIL"].ToString() ||
                        myRow["UPDTID"].ToString() != oraRow["UPDTID"].ToString() ||
                        myRow["UPDTDT"].ToString() != oraRow["UPDTDT"].ToString();
                    if (isDiff)
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {tkcd}");
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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
        // M0330 手配先管理マスタ（60件）
        private static void M0330()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0330 手配先管理マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0330", connOracle).ExecuteReader());

            // MySQL
            var dtMySQL = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0330", connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["ODCTLNO"].ToString(), r => r);

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var odctlno= oraRow["ODCTLNO"].ToString();
                var key = odctlno;

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    DataRow newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {odctlno}");
                    countInsert++;
                }
                else
                {
                    // どれか1つでも違えば UPDATE
                    bool isDiff =
                        myRow["ODCTLNM"].ToString() != oraRow["ODCTLNM"].ToString() ||
                        myRow["ODLT"].ToString() != oraRow["ODLT"].ToString() ||
                        myRow["KTDAY"].ToString() != oraRow["KTDAY"].ToString() ||
                        myRow["NJDAY"].ToString() != oraRow["NJDAY"].ToString() ||
                        myRow["CALTYP"].ToString() != oraRow["CALTYP"].ToString() ||
                        myRow["JITOKTDAY"].ToString() != oraRow["JITOKTDAY"].ToString() ||
                        myRow["UPDTID"].ToString() != oraRow["UPDTID"].ToString() ||
                        myRow["UPDTDT"].ToString() != oraRow["UPDTDT"].ToString();

                    if (isDiff)
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {odctlno}");
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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
        // M0300 手配先名称マスタ（1000件）
        private static void M0300()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0300 手配先名称マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0300", connOracle).ExecuteReader());

            // MySQL
            var dtMySQL = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0300", connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["ODCD"].ToString(), r => r);

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var odcd = oraRow["ODCD"].ToString();
                var key = odcd;

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    var newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {odcd}");
                    countInsert++;
                }
                else
                {
                    // どれか1つでも違えば UPDATE
                    bool isDiff =
                        myRow["ODNM1"].ToString() != oraRow["ODNM1"].ToString() ||
                        myRow["ODNM2"].ToString() != oraRow["ODNM2"].ToString() ||
                        myRow["ODRNM"].ToString() != oraRow["ODRNM"].ToString() ||
                        myRow["ODTANNM"].ToString() != oraRow["ODTANNM"].ToString() ||
                        myRow["ZIP"].ToString() != oraRow["ZIP"].ToString() ||
                        myRow["ADD1"].ToString() != oraRow["ADD1"].ToString() ||
                        myRow["ADD2"].ToString() != oraRow["ADD2"].ToString() ||
                        myRow["TEL"].ToString() != oraRow["TEL"].ToString() ||
                        myRow["FAX"].ToString() != oraRow["FAX"].ToString() ||
                        myRow["MAIL"].ToString() != oraRow["MAIL"].ToString() ||
                        myRow["IOKBN"].ToString() != oraRow["IOKBN"].ToString() ||
                        myRow["UPDTID"].ToString() != oraRow["UPDTID"].ToString() ||
                        myRow["UPDTDT"].ToString() != oraRow["UPDTDT"].ToString();

                    if (isDiff)
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {odcd}");
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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
        // M0310 手配先マスタ（1000件）
        private static void M0310()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0310 手配先マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0310", connOracle).ExecuteReader());
            var oraDict = dtOra.AsEnumerable()
                .ToDictionary(r => r["ODCD"].ToString(), r => r);

            // MySQL
            var dtMySQL = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0310", connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);
            var myDict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["ODCD"].ToString(), r => r);

            var countInsert = 0;
            var countUpdate = 0;
            int countDelete = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var odcd = oraRow["ODCD"].ToString();
                var key = odcd;

                if (!myDict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    var newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    myDict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {odcd}");
                    countInsert++;
                }
                else
                {
                    // どれか1つでも違えば UPDATE
                    bool isDiff =
                        myRow["ODGCD"].ToString() != oraRow["ODGCD"].ToString() ||
                        myRow["ODCTLNO"].ToString() != oraRow["ODCTLNO"].ToString() ||
                        myRow["SHCD"].ToString() != oraRow["SHCD"].ToString() ||
                        myRow["PODCD"].ToString() != oraRow["PODCD"].ToString() ||
                        myRow["SKOKBN"].ToString() != oraRow["SKOKBN"].ToString() ||
                        myRow["MKBN"].ToString() != oraRow["MKBN"].ToString() ||
                        myRow["YGWKTPNO"].ToString() != oraRow["YGWKTPNO"].ToString() ||
                        myRow["JODCDKBN"].ToString() != oraRow["JODCDKBN"].ToString() ||
                        myRow["UPDTID"].ToString() != oraRow["UPDTID"].ToString() ||
                        myRow["UPDTDT"].ToString() != oraRow["UPDTDT"].ToString();

                    if (isDiff)
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {odcd}");
                        countUpdate++;
                    }
                }
            }
            // DELETE（Oracle に無い ODCD）
            foreach (var kv in myDict)
            {
                if (!oraDict.ContainsKey(kv.Key))
                {
                    kv.Value.Delete();
                    if (isDisp) Console.WriteLine($"Delete {kv.Key}");
                    countDelete++;
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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
        // M0400 工程グループマスタ（40件）
        private static void M0400()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0400 工程グループマスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0400", connOracle).ExecuteReader());

            // MySQL
            var dtMySQL = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0400", connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["KTGCD"].ToString(), r => r);

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var ktgcd = oraRow["KTGCD"].ToString();
                var key = ktgcd;

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    DataRow newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {ktgcd}");
                    countInsert++;
                }
                else
                {
                    // どれか1つでも違えば UPDATE
                    bool isDiff =
                        myRow["KTGSEQ"].ToString() != oraRow["KTGSEQ"].ToString() ||
                        myRow["KTGNM"].ToString() != oraRow["KTGNM"].ToString() ||
                        myRow["KTGRNM"].ToString() != oraRow["KTGRNM"].ToString();

                    if (isDiff)
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {ktgcd}");
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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
        // M0410 工程マスタ（300件）
        private static void M0410()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0410 工程マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand($"select * from M0410", connOracle).ExecuteReader());

            // MySQL
            var dtMySQL = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0410", connMySQL);
            var buider = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["KTCD"].ToString(), r => r);

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var ktcd = oraRow["KTCD"].ToString();
                var key = ktcd;

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    var newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {ktcd}");
                    countInsert++;
                }
                else
                {
                    // Double 比較（KTPRICE）
                    var oraCnt = oraRow["KTPRICE"].ToDoubleSafe();
                    var myCnt = myRow["KTPRICE"].ToDoubleSafe();

                    // どれか1つでも違えば UPDATE
                    bool isDiff =
                        myRow["KTNM"].ToString() != oraRow["KTNM"].ToString() ||
                        myRow["KTGCD"].ToString() != oraRow["KTGCD"].ToString() ||
                        myRow["ODCD"].ToString() != oraRow["ODCD"].ToString() ||
                        myRow["SHINDO"].ToString() != oraRow["SHINDO"].ToString() ||
                        myRow["TENKAI"].ToString() != oraRow["TENKAI"].ToString() ||
                        myRow["ODRKBN"].ToString() != oraRow["ODRKBN"].ToString() ||
                        myRow["LOTKBN"].ToString() != oraRow["LOTKBN"].ToString() ||
                        myRow["ODANLT"].ToString() != oraRow["ODANLT"].ToString() ||
                        myRow["TRIALQTY"].ToString() != oraRow["TRIALQTY"].ToString() ||
                        myRow["UNITQTY"].ToString() != oraRow["UNITQTY"].ToString() ||
                        myRow["UNITNM"].ToString() != oraRow["UNITNM"].ToString() ||
                        myRow["HUNITNM"].ToString() != oraRow["HUNITNM"].ToString() ||
                        myRow["BFLT"].ToString() != oraRow["BFLT"].ToString() ||
                        myRow["AFLT"].ToString() != oraRow["AFLT"].ToString() ||
                        myRow["IDANLT"].ToString() != oraRow["IDANLT"].ToString() ||
                        myRow["ODRLT"].ToString() != oraRow["ODRLT"].ToString() ||
                        myRow["SAFELT"].ToString() != oraRow["SAFELT"].ToString() ||
                        myRow["MOLT"].ToString() != oraRow["MOLT"].ToString() ||
                        myRow["QCLT"].ToString() != oraRow["QCLT"].ToString() ||
                        myRow["YOLT"].ToString() != oraRow["YOLT"].ToString() ||
                        myRow["JIKBN"].ToString() != oraRow["JIKBN"].ToString() ||
                        myRow["QKSKBN"].ToString() != oraRow["QKSKBN"].ToString() ||
                        myRow["BUHIN"].ToString() != oraRow["BUHIN"].ToString() ||
                        myRow["CPKTCD"].ToString() != oraRow["CPKTCD"].ToString() ||
                        myRow["UPDTID"].ToString() != oraRow["UPDTID"].ToString() ||
                        myRow["UPDTDT"].ToString() != oraRow["UPDTDT"].ToString() ||
                        !myCnt.NearlyEquals(oraCnt);

                    if (isDiff)
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {ktcd}");
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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
        // M0500 品目マスタ（5万件）
        private static void M0500()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine($"M0500 品目マスタチェック開始 ({day}日間)");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle（直近更新分のみ）
            var dtOra = new DataTable();
            var sqlOra = "select * from M0500";
            if (!isMaintenance) sqlOra += " " + $"where UPDTDT > (SYSDATE - {day})";
            dtOra.Load(new OracleCommand(sqlOra, connOracle).ExecuteReader());
            var hmcdList = dtOra.AsEnumerable()
                .Select(r => r["HMCD"].ToString())
                .Distinct()
                .ToList();
            if (hmcdList.Count == 0)
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
                return;
            }

            // MySQL（直近リストから抽出）
            var dtMySQL = new DataTable();
            string sqlMySQL = "select * from M0500";
            if (!isMaintenance)
            {
                string inClause = string.Join(",", hmcdList.Select(x => $"'{x}'"));
                sqlMySQL += " " + $"where HMCD in ({inClause})";
            }
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            var builder = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(r => r["HMCD"].ToString(), r => r);

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var hmcd = oraRow["HMCD"].ToString();
                var key = hmcd;

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    var newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine("Insert " + hmcd);
                    countInsert++;
                } else {
                    // 差分チェック（全列比較）
                    bool isDiff = false;
                    for (int i = 0; i < dtOra.Columns.Count; i++)
                    {
                        var col = dtOra.Columns[i].ColumnName;
                        var oraVal = oraRow[col];
                        var myVal = myRow[col];
                        if (!ObjectEquals(oraVal, myVal))
                        {
                            isDiff = true;
                            break;
                        }
                    }
                    if (isDiff)
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine("Update " + hmcd);
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
                if (isDisp) Console.WriteLine(Common.MSG_SEPARATOR);
                Console.WriteLine("検査対象件数：" + String.Format("{0:#,0}", dtOra.Rows.Count) + " 件");
                Console.WriteLine("新規登録件数：" + String.Format("{0:#,0}", countInsert) + " 件");
                Console.WriteLine("　　更新件数：" + String.Format("{0:#,0}", countUpdate) + " 件");
            } else
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
            }
        }
        // M0520 品目構成マスタ（7万件）
        private static void M0520()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine($"M0520 品目構成マスタチェック開始 ({day}日間)");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle 直近に更新されたものをチェック
            var dtOra = new DataTable();
            var sqlOra = "select OYAHMCD, min(INSTDT) as MINDT, max(UPDTDT) as MAXDT from M0520 where OYAHMCD in " +
                $"(select OYAHMCD from M0520 where UPDTDT > SYSDATE - {day}) " +
                "group by OYAHMCD";
            dtOra.Load(new OracleCommand(sqlOra, connOracle).ExecuteReader());
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var oyahmcd = row["OYAHMCD"].ToString();
                var mindt = row["MINDT"].ToString();
                var maxdt = row["MAXDT"].ToString();

                // MySQL OYAHMCDを取得
                var dtMySQL = new DataTable();
                var sqlMySQL = $"select OYAHMCD, min(INSTDT) as MINDT, max(UPDTDT) as MAXDT from M0520 where OYAHMCD='{oyahmcd}' group by OYAHMCD";
                var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
                myDa.Fill(dtMySQL);
                bool insertFlg = false;
                if (dtMySQL.Rows.Count > 0)
                {
                    // 変更ありと判定された場合はMySQL側を一旦削除
                    if (mindt != dtMySQL.Rows[0]["MINDT"].ToString() ||
                        maxdt != dtMySQL.Rows[0]["MAXDT"].ToString())
                    {
                        var mpSQL = $"delete from m0520 where OYAHMCD='{oyahmcd}'";
                        using (MySqlCommand mpCmd = new MySqlCommand(mpSQL, connMySQL))
                        {
                            if (isDisp) Console.WriteLine($"Update {oyahmcd}");
                            if (isUpdate) mpCmd.ExecuteNonQuery();
                            countUpdate++;
                            insertFlg = true;
                        }
                    }
                }
                else
                {
                    if (isDisp) Console.WriteLine($"Insert {oyahmcd}");
                    countInsert++;
                    insertFlg = true;
                }
                if (insertFlg)
                {
                    // OracleのOYAHMCDを読み込んで挿入
                    var dtOraDetail = new DataTable();
                    dtOraDetail.Load(new OracleCommand($"select * from M0520 where OYAHMCD='{oyahmcd}'", connOracle).ExecuteReader());
                    // Bulk Insert用
                    List<string> m0520s = new List<string>();
                    foreach (DataRow r in dtOraDetail.Rows)
                    {
                        m0520s.Add(ImportMpM0520BulkData(r));
                    }
                    if (m0520s.Count > 0)
                    {
                        var BulkData = string.Join(",", m0520s.ToArray());
                        var sql = ImportMpM0520() + BulkData;
                        using (MySqlCommand mpCmd = new MySqlCommand(sql, connMySQL))
                        {
                            try
                            {
                                // 追加更新を実行
                                if (isUpdate) mpCmd.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                Console.Error.WriteLine($"M0520 Error Insert OYAHMCD={oyahmcd}");
                            }
                        }
                    }
                }
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
        private static string ImportMpM0520()
        {
            return "Insert into m0520 " +
                "(OYAHMCD,SEQ,BOMSEQ,KTCD,KOHMCD,BOMKBN,KOQTY,OYAQTY,VALDTF,VALDTT," +
                "INSTID,INSTDT,UPDTID,UPDTDT)" +
                " values ";
        }
        private static string ImportMpM0520BulkData(DataRow r)
        {
            return "("
                + "'" + r["OYAHMCD"].ToString() + "',"
                + r["SEQ"] + ","
                + r["BOMSEQ"] + ","
                + (r["KTCD"].ToString() == "" ? "null," : "'" + r["KTCD"].ToString() + "',")
                + "'" + r["KOHMCD"].ToString() + "',"
                + "'" + r["BOMKBN"].ToString() + "',"
                + r["KOQTY"] + ","
                + r["OYAQTY"] + ","
                + "'" + r["VALDTF"] + "',"
                + "'" + r["VALDTT"] + "',"
                + "'" + r["INSTID"].ToString() + "',"
                + "'" + r["INSTDT"] + "',"
                + "'" + r["UPDTID"].ToString() + "',"
                + "'" + r["UPDTDT"] + "'"
                + ")";
        }

        // M0570 品目手順マスタ（7万件）
        private static void M0570()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine($"M0570 品目手順マスタチェック開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle 全件
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0570", connOracle).ExecuteReader());
            var oraDict = dtOra.AsEnumerable()
                .ToDictionary(r => $"{r["HMCD"]}_{(DateTime)r["VALDTF"]:yyyyMMdd}", r => r);

            // MySQL 全件
            var dtMy = new DataTable();
            var myDa = new MySqlDataAdapter("select * from M0570", connMySQL);
            var builder = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMy);
            var myDict = dtMy.AsEnumerable()
                .ToDictionary(r => $"{r["HMCD"]}_{(DateTime)r["VALDTF"]:yyyyMMdd}", r => r);

            int countInsert = 0;
            int countUpdate = 0;
            int countDelete = 0;
            // INSERT / UPDATE
            foreach (var kv in oraDict)
            {
                var key = kv.Key;
                var oraRow = kv.Value;

                if (!myDict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    var newRow = dtMy.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMy.Rows.Add(newRow);
                    if (isDisp) Console.WriteLine("Insert " + key);
                    countInsert++;
                }
                else
                {
                    // 差分チェック（全列）
                    bool isDiff = false;

                    for (int i = 0; i < dtOra.Columns.Count; i++)
                    {
                        var col = dtOra.Columns[i].ColumnName;
                        if (!ObjectEquals(oraRow[col], myRow[col]))
                        {
                            isDiff = true;
                            break;
                        }
                    }

                    if (isDiff)
                    {
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine("Update " + key);
                        countUpdate++;
                    }
                }
            }
            // DELETE（Oracle に無い行）
            foreach (var kv in myDict)
            {
                if (!oraDict.ContainsKey(kv.Key))
                {
                    // 外部参照[FK_M0510_M0570_1]の為、M0510の実態を先に消してしまう
                    var hmcd = kv.Key.Split('_')[0];
                    var valdtf = kv.Key.Split('_')[1];
                    var mpSQL = $"delete from m0510 where HMCD='{hmcd}' and VALDTF='{valdtf}'";
                    using (MySqlCommand mpCmd = new MySqlCommand(mpSQL, connMySQL))
                    {
                        if (isUpdate) mpCmd.ExecuteNonQuery();
                    }
                    // DataTableから削除
                    kv.Value.Delete();
                    if (isDisp) Console.WriteLine("Delete " + hmcd + " - " + valdtf);
                    countDelete++;
                }
            }

            // 結果
            if (countInsert + countUpdate + countDelete > 0)
            {
                // 更新
                if (isUpdate) myDa.Update(dtMy);
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
        // M0510 品目手順詳細マスタ（19万件）
        private static void M0510()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine($"M0510 品目手順詳細マスタチェック開始 ({day}日間)");
            Console.WriteLine(Common.MSG_SEPARATOR);
            // Oracle 直近に更新されたものをチェック
            var dtOra = new DataTable();
            var sqlOra = $"select HMCD, VALDTF, min(INSTDT) as MINDT, MAX(UPDTDT) as MAXDT from M0510 where updtdt > SYSDATE - {day} group by HMCD, VALDTF";
            dtOra.Load(new OracleCommand(sqlOra, connOracle).ExecuteReader());
            // OracleRowを一件ずつループ
            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow row in dtOra.Rows)
            {
                var hmcd = row["HMCD"].ToString();
                var valdtf = row["VALDTF"].ToString();
                var mindt = row["MINDT"].ToString();
                var maxdt = row["MAXDT"].ToString();

                // MySQL HMCDを取得
                var dtMySQL = new DataTable();
                var sqlMySQL = $"select HMCD, VALDTF, min(INSTDT) as MINDT, max(UPDTDT) as MAXDT from m0510 where HMCD='{hmcd}' and VALDTF='{valdtf}'";
                var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
                myDa.Fill(dtMySQL);
                bool insertFlg = false;
                if (dtMySQL.Rows.Count > 0)
                {
                    // 変更ありと判定された場合はMySQL側を一旦削除
                    if (mindt != dtMySQL.Rows[0]["MINDT"].ToString() ||
                        maxdt != dtMySQL.Rows[0]["MAXDT"].ToString())
                    {
                        var mpSQL = $"delete from m0510 where HMCD='{hmcd}' and VALDTF='{valdtf}'";
                        using (MySqlCommand mpCmd = new MySqlCommand(mpSQL, connMySQL))
                        {
                            if (isDisp) Console.WriteLine($"Update {hmcd,-24} - {valdtf}");
                            if (isUpdate) mpCmd.ExecuteNonQuery();
                            countUpdate++;
                            insertFlg = true;
                        }
                    }
                }
                else
                {
                    if (isDisp) Console.WriteLine($"Insert {hmcd,-24} - {valdtf}");
                    countInsert++;
                    insertFlg = true;
                }
                if (insertFlg)
                {
                    // OracleのHMCDを読み込んで挿入
                    var dtOraDetail = new DataTable();
                    var sqlOraDetail = $"select * from M0510 where HMCD='{hmcd}' and VALDTF='{valdtf.Substring(0,10)}'";
                    dtOraDetail.Load(new OracleCommand(sqlOraDetail, connOracle).ExecuteReader());
                    // Bulk Insert用
                    List<string> m0510s = new List<string>();
                    foreach (DataRow r in dtOraDetail.Rows)
                    {
                        m0510s.Add(ImportMpM0510BulkData(r));
                    }
                    if (m0510s.Count > 0)
                    {
                        var BulkData = string.Join(",", m0510s.ToArray());
                        var sql = ImportMpM0510() + BulkData;
                        using (MySqlCommand mpCmd = new MySqlCommand(sql, connMySQL))
                        {
                            try
                            {
                                // 追加更新を実行
                                if (isUpdate) mpCmd.ExecuteNonQuery();
                            }
                            catch (Exception)
                            {
                                Console.Error.WriteLine($"M0510 Error Insert HMCD={hmcd} VALDTF={valdtf}");
                            }
                        }
                    }
                }
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
        private static string ImportMpM0510()
        {
            return "Insert into m0510 " +
                "(HMCD,VALDTF,KTSEQ,KTCD,ODCD,SHINDO,TENKAI,CARD,ODRKBN,LOTKBN," +
                "LOTQTY,ODANLT,BFLT,AFLT,IDANLT,ODRLT,SAFELT,MOLT,QCLT,YOLT," +
                "TRIALQTY,UNITQTY,UNITNM,HUNITNM,HQTY,KQTY,MCNO,TOOLNO,WKNOTE,WKCOMMENT," +
                "BOXCD,SAFEQTY,STKTKBN,EDKTKBN,YGWKBN,JIKBN,MKBN,INSTID,INSTDT,UPDTID," +
                "UPDTDT,HTKBN,THSSKBN)" +
                " values ";
        }
        private static string ImportMpM0510BulkData(DataRow r)
        {
            return "("
                + "'" + r["HMCD"].ToString() + "',"
                + "'" + r["VALDTF"] + "',"
                + r["KTSEQ"] + ","
                + "'" + r["KTCD"].ToString() + "',"
                + "'" + r["ODCD"].ToString() + "',"
                + "'" + r["SHINDO"].ToString() + "',"
                + "'" + r["TENKAI"].ToString() + "',"
                + "'" + r["CARD"].ToString() + "',"
                + "'" + r["ODRKBN"].ToString() + "',"
                + "'" + r["LOTKBN"].ToString() + "',"
                + r["LOTQTY"] + ","
                + r["ODANLT"] + ","
                + r["BFLT"] + ","
                + r["AFLT"] + ","
                + r["IDANLT"] + ","
                + r["ODRLT"] + ","
                + r["SAFELT"] + ","
                + r["MOLT"] + ","
                + r["QCLT"] + ","
                + r["YOLT"] + ","
                + r["TRIALQTY"] + ","
                + r["UNITQTY"] + ","
                + (r["UNITNM"].ToString() == "" ? "null," : "'" + r["UNITNM"].ToString() + "',")
                + (r["HUNITNM"].ToString() == "" ? "null," : "'" + r["HUNITNM"].ToString() + "',")
                + r["HQTY"] + ","
                + r["KQTY"] + ","
                + (r["MCNO"].ToString() == "" ? "null," : "'" + r["MCNO"].ToString() + "',")
                + (r["TOOLNO"].ToString() == "" ? "null," : "'" + r["TOOLNO"].ToString() + "',")
                + (r["WKNOTE"].ToString() == "" ? "null," : "'" + r["WKNOTE"].ToString() + "',")
                + (r["WKCOMMENT"].ToString() == "" ? "null," : "'" + r["WKCOMMENT"].ToString() + "',")
                + (r["BOXCD"].ToString() == "" ? "null," : "'" + r["BOXCD"].ToString() + "',")
                + r["SAFEQTY"] + ","
                + "'" + r["STKTKBN"].ToString() + "',"
                + "'" + r["EDKTKBN"].ToString() + "',"
                + "'" + r["YGWKBN"].ToString() + "',"
                + "'" + r["JIKBN"].ToString() + "',"
                + "'" + r["MKBN"].ToString() + "',"
                + (r["INSTID"].ToString() == "" ? "null," : "'" + r["INSTID"].ToString() + "',")
                + (r["INSTDT"].ToString() == "" ? "null," : "'" + r["INSTDT"].ToString() + "',")
                + (r["UPDTID"].ToString() == "" ? "null," : "'" + r["UPDTID"].ToString() + "',")
                + (r["UPDTDT"].ToString() == "" ? "null," : "'" + r["UPDTDT"].ToString() + "',")
                + "'" + r["HTKBN"].ToString() + "',"
                + "'" + r["THSSKBN"].ToString() + "'"
                + ")";
        }
        // M0510 緊急メンテナンス
        private static void M0510_MaintenanceCopy_Bulk()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine("M0510 緊急メンテナンス：Oracle → MySQL 全コピー（Bulk）開始");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle 全件取得
            var dtOra = new DataTable();
            dtOra.Load(new OracleCommand("select * from M0510", connOracle).ExecuteReader());

            // MySQL 側を TRUNCATE（超高速全削除）
            using (var cmd = new MySqlCommand("TRUNCATE TABLE M0510", connMySQL))
            {
                cmd.ExecuteNonQuery();
            }

            // Bulk Insert (バッチサイズ multi-row でまとめて高速投入）
            const int batchSize = 1000;
            int total = dtOra.Rows.Count;
            int processed = 0;

            while (processed < total)
            {
                var rows = dtOra.AsEnumerable()
                    .Skip(processed)
                    .Take(batchSize)
                    .ToList();
                // Bulk Insert用
                List<string> m0510s = new List<string>();
                foreach (DataRow r in rows)
                {
                    m0510s.Add(ImportMpM0510BulkData(r));
                }
                var BulkData = string.Join(",", m0510s.ToArray());
                var sql = ImportMpM0510() + BulkData;
                using (MySqlCommand mpCmd = new MySqlCommand(sql, connMySQL))
                {
                    mpCmd.ExecuteNonQuery();
                }
                processed += rows.Count;
                Console.WriteLine($"{processed:#,0}/{total:#,0} 件 INSERT 完了");
            }

            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine($"Oracle 件数：{dtOra.Rows.Count:#,0} 件");
            Console.WriteLine("MySQL へ multi-row INSERT 完了（高速）");
        }

        // M0600 受注品マスタ（6万件）
        private static void M0600()
        {
            Console.WriteLine(Common.MSG_SEPARATOR);
            Console.WriteLine($"M0600 受注品マスタチェック開始 ({day}日間)");
            Console.WriteLine(Common.MSG_SEPARATOR);

            // Oracle（直近更新分のみ）
            var dtOra = new DataTable();
            var sqlOra = "select * from M0600";
            if (!isMaintenance) sqlOra += " " + $"where UPDTDT > (SYSDATE - {day})";
            dtOra.Load(new OracleCommand(sqlOra, connOracle).ExecuteReader());
            var keyList = dtOra.AsEnumerable()
                .Select(r => new {
                    TKCD = r["TKCD"].ToString(),
                    TKHMCD = r["TKHMCD"].ToString()
                })
                .Distinct()
                .ToList();
            if (keyList.Count == 0)
            {
                Console.WriteLine("更新はありませんでした．".PadLeft(18));
                return;
            }

            // MySQL（直近リストから抽出）
            var dtMySQL = new DataTable();
            string sqlMySQL = "select * from M0600";
            if (!isMaintenance)
            {
                var whereList = keyList
                    .Select(k => $"(TKCD='{k.TKCD}' AND TKHMCD='{k.TKHMCD}')");
                var whereClause = string.Join(" OR ", whereList);
                sqlMySQL += " " + $"where {whereClause}";
            }
            var myDa = new MySqlDataAdapter(sqlMySQL, connMySQL);
            var builder = new MySqlCommandBuilder(myDa);
            myDa.Fill(dtMySQL);

            // MySQL → Dictionary（高速検索）
            var dict = dtMySQL.AsEnumerable()
                .ToDictionary(
                    r => $"{r["TKCD"]}_{r["TKHMCD"]}",
                    r => r
                );

            var countInsert = 0;
            var countUpdate = 0;
            foreach (DataRow oraRow in dtOra.Rows)
            {
                var tkcd = oraRow["TKCD"].ToString();
                var tkhmcd = oraRow["TKHMCD"].ToString();
                var key = $"{oraRow["TKCD"]}_{oraRow["TKHMCD"]}";

                if (!dict.TryGetValue(key, out DataRow myRow))
                {
                    // INSERT（全列コピー）
                    var newRow = dtMySQL.NewRow();
                    newRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                    dtMySQL.Rows.Add(newRow);
                    dict[key] = newRow;
                    if (isDisp) Console.WriteLine($"Insert {tkcd} - {tkhmcd}");
                    countInsert++;
                }
                else
                {
                    if (myRow["HMCD"].ToString() != oraRow["HMCD"].ToString() ||
                        myRow["TKLT"].ToString() != oraRow["TKLT"].ToString() ||
                        myRow["UPDTID"].ToString() != oraRow["UPDTID"].ToString() ||
                        myRow["UPDTDT"].ToString() != oraRow["UPDTDT"].ToString())
                    {
                        // UPDATE（全列コピー）
                        myRow.ItemArray = oraRow.ItemArray.Clone() as object[];
                        if (isDisp) Console.WriteLine($"Update {tkcd} - {tkhmcd}");
                        countUpdate++;
                    }
                }
            }
            // 結果
            if (countInsert + countUpdate > 0)
            {
                // 追加更新を実行
                if (isUpdate) myDa.Update(dtMySQL);
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

    }
}
