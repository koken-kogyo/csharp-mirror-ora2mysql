# [KMD004SC] EMマスタミラーリング  
- MirrorOra2MySQL  

## 概要  
- EMマスタの以下の参照テーブルの状態を調べ、MySQLデータベースへのミラーリングを行う  

## 参照テーブル  
| Table    | Name                      |  
| :------- | :------------------------ |  
| M0010    | 担当者マスタ              |  
| M0230    | 得意先管理マスタ          |  
| M0220    | 請求先マスタ              |  
| M0210    | 得意先マスタ              |  
| M0200    | 得意先名称マスタ          |  
| M0300    | 手配先名称マスタ          |  
| M0310    | 手配先マスタ              |  
| M0400    | 工程グループマスタ        |  
| M0410    | 工程マスタ                |  
| M0500    | 品目マスタ                |  
| M0520    | 品目構成マスタ            |  
| M0570    | 品目手順マスタ            |  
| M0510    | 品目手順詳細マスタ        |  

## 開発環境  
- C# .NET Framework v4.6  コンソールアプリケーション  

## 参照設定  
- DecryptPassword.dll  
- Oracle.ManagedDataAccess.dll  
- MySql.Data.dll  
  (\packages\MySql.Data.8.0.32.1\lib\net452)  

## 実行方法  
~~~  
       使用法 :: MirrorOra2MySQL 対象日数 [オプション]
       使用例 :: MirrorOra2MySQL 30 /C

     対象日数 :: 更新日付を検索対象とし、当日から過去遡りの日数
::
:: オプション :
::
           /C :: 追加更新件数のチェックのみ行う - 簡易表示（デフォルト）
           /D :: 追加更新件数のチェックのみ行う - 詳細表示
           /E :: データベースへの追加更新を行う
~~~  

## メンバー  
- y.watanabe  

## プロジェクト構成  
~~~  
./  
│  .gitignore                                  # ソース管理除外対象  
│  ActualProductCollation.sln                  # Visual Studio Solution ファイル  
│  README.md                                   # このファイル  
│  
├─ ActualProductCollation  
│  │  App.config                              # アプリケーション設定ファイル  
│  │  Common.cs                               # 共通設定ファイル  
│  │  FileAccess.cs                           # ファイルアクセス  
│  └  Program.cs                              # メイン関数  
│      
├─ packages  
│  │  DecryptPassword.dll                     #   
│  │  Oracle.ManagedDataAccess.dll            #   
│  └  MySql.Data.8.0.32.1  
│      
├─ settingfiles  
│      ConfigDB.xml                            # データベース定義ファイル  
│      
└─ specification  
        [KXXxxxXX] xxx 機能仕様書_Ver.1.0.0.0.xlsx  
        
~~~  

## 変更履歴  
- 2023.09.10  新規作成  y.watanabe  
