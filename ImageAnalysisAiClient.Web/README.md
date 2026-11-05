# Web版の分別索引

Web版はExcelの廃棄物索引から、廃棄物名と廃棄先を読み取ります。
Excelはリポジトリには含まれません。既定ではリポジトリ直下の `◆DNWA廃棄物種別検索_250804.xlsx` を参照します。別の場所に配置する場合はパスを設定してください。

```powershell
$env:WasteCatalog__ExcelPath = '<Excelの絶対パス>'
dotnet run --project .\ImageAnalysisAiClient.Web
```

`appsettings.json` の `WasteCatalog:ExcelPath` にも指定できます。相対パスはWebアプリのコンテンツルートを基準に解決します。判定ごとにExcelを読み込み、内容のハッシュが変わった場合だけ再解析するため、差し替えは次の判定に反映されます。キャッシュはアプリの再起動時に破棄されます。読み取れないExcelや索引の形式が異なるExcelはエラーになり、以前の結果は使用しません。

元の索引シートで隣接する「廃棄物名」「廃棄先」列を読み取ります。数式だけで構成された集計表は除外し、元の索引に数式と文字列が混在する場合はエラーにします。索引の注意点は使用せず、索引にない物体は判定不可として扱います。Azureのモデルを選択すると、索引の内容は画像とともにAzureに送信されます。

指定されたExcelを使うテストを実行する場合は、次のようにパスを設定します。このテストは索引の件数や代表的な項目を検証するため、Excelの更新時には期待値の確認が必要です。

```powershell
$env:WASTE_CATALOG_TEST_EXCEL = '<Excelの絶対パス>'
dotnet test .\ImageAnalysisAiClient.Web.Tests\ImageAnalysisAiClient.Web.Tests.csproj
```
