# 禾境物業管理系統

使用 ASP.NET Core MVC、Razor Views、EF Core 與 SQLite 建立的繁體中文物業管理網站。第一階段提供資產、出租單位、承租戶、租約、水電表、抄表、應收帳款及收款管理，以及營運總覽。暫不提供登入。

**本機啟動**

需要 .NET 10 SDK；整合測試另需 Python 3，不需要額外 Python 套件。亦可直接使用 `make` 執行常用指令（輸入 `make help` 查看清單）。

```sh
# 使用 Makefile（推薦）
make restore
make run

# 或直接使用 .NET CLI
dotnet restore
dotnet run --launch-profile http
```

開啟 <http://localhost:5188>。首次在 Development 啟動時，會自動建立 SQLite 資料庫與範例資料，包括 6 處資產、32 個出租單位、21 份租約及近 6 個月帳款。範例資料不會重複匯入。

目前網站的名稱「禾境」為可替換的示範品牌。

**已完成的操作**

- 八個模組的新增、修改、查詢及刪除確認；已有關聯紀錄的資料限制刪除，帳務資料改用作廢保留紀錄。
- 所有列表以資料庫查詢分頁，每頁 10、20 或 50 筆。搜尋、狀態與排序條件會隨換頁保留。
- 以有效租約按月份產生租金應收，重複執行不會建立重複帳款；首末月以實際租用天數比例計租。
- 分次收款、餘額驗證、收款作廢、水電讀數差額計算及歷史帳款保護。
- 營運總覽：出租率、應收／實收、目前逾期未收、六個月趨勢及 90 天內到期租約。
- 圖表進場動畫、金額遞增、出租率圓環填入與動態重播；月份明細支援滑鼠、觸控及鍵盤，尊重系統減少動態設定。
- 桌面與行動版介面、欄位驗證、防偽權杖及編輯版本衝突檢查。
- Three.js 物業全景：建物選取、樓層展開、單位租約與欠款面板、搜尋及鍵盤清單操作；保留原有營運圖表與資料編輯流程。
- 工傷管理介面：95 個人體部位的 3D 點選與清單多選，支援正背面、左右側、骨架參考、部位搜尋及選取摘要。目前為當頁標記工具，尚未建立工傷個案儲存流程。

**資料位置與設定**

預設資料庫是 `App_Data/property.db`。資料庫、編譯結果及驗收截圖不納入 Git。

- `SeedDemoData=false`：關閉示範資料。只影響尚未初始化的資料，不會刪除現有內容。
- `ConnectionStrings:PropertyDb`：可改變 SQLite 路徑，例如 `Data Source=/absolute/path/property.db`。
- `ASPNETCORE_ENVIRONMENT=Development`：開發環境；示範資料僅在此環境建立。

目前以 `EnsureCreated` 建立第一版 schema，不會自動更新已存在資料庫的結構。未來修改結構時，必須加入 EF Core migration 與既有資料庫升級流程；不可刪除使用者資料庫來代替遷移。

無登入的版本預設只綁定 localhost。正式提供多人或外部使用前，需完成後續登入、權限、HTTPS、備份排程及部署設定。

**驗證**

```sh
# 執行所有測試（單元測試 + 端到端整合測試）
make test-all

# 或分開執行
make test      # xUnit 與前端 JS 單元測試
make smoke     # 端到端 HTTP 整合測試

# 亦可直接呼叫指令
dotnet build
dotnet test tests/PropertyManagement.Tests/PropertyManagement.Tests.csproj
python3 tests/smoke.py
```

整合測試會啟動獨立網站、隨機連接埠及臨時 SQLite 資料庫，結束後移除測試資料。涵蓋 CRUD、搜尋分頁、CSRF、版本衝突、帳務保護、重疊租約、比例計租、重複出帳、分次收款與水電計算。

**專案結構**

| 路徑 | 用途 |
| --- | --- |
| `Models/Domain.cs` | 八類實體、共通欄位與版本識別 |
| `Models/PropertySceneModel.cs` | 物業全景的建物與單位資料模型 |
| `Data/AppDbContext.cs` | 資料庫映射、精確數值與關聯限制 |
| `Data/SeedData.cs` | 開發環境示範資料 |
| `Services/Catalog.cs` | 各模組欄位、選單、狀態及畫面模型 |
| `Services/ModuleService.cs` | 資料維護、欄位驗證與分頁查詢 |
| `Services/Business.cs` | 租約、收款、水電與比例計租規則 |
| `Services/PropertySceneService.cs` | 空間狀態、樓層分組與逾期帳款彙整 |
| `Controllers/ManageController.cs` | CRUD 與按月出帳路由 |
| `Controllers/HomeController.cs` | 營運指標與趨勢資料 |
| `Views/Manage` | 清單、編輯及刪除確認 |
| `wwwroot/css/site.css` | 管理介面與響應式樣式 |
| `wwwroot/js/property-scene.js` | 3D 建物、樓層、單位互動及清單備援 |
| `wwwroot/vendor/three/` | 固定版本的 Three.js 0.186.1 與 MIT 授權，無外部 CDN 依賴 |
| `tests/smoke.py` | 獨立資料庫的 HTTP 整合驗收 |
| `docs/開發規範.md` | 完整產品決策、資料規則與後續範圍 |

**版本控制**

已初始化本機 Git。每個完整功能或修正以獨立 commit 保存，提交標題與內文均使用繁體中文，說明實作內容、業務規則與驗證結果。未建立遠端儲存庫，也未推送至外部服務。

**技術參考**

表單驗證與防偽處理依據 [ASP.NET Core MVC 驗證文件](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-mvc-app/validation?view=aspnetcore-10.0)；資料存取架構參考 [Microsoft EF Core 與 MVC 文件](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-mvc-app/adding-model?view=aspnetcore-10.0)。
