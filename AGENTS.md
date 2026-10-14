# AGENTS.md — 禾境物業管理系統 AI Agent 開發指南

本文件為 AI 代理（Agents）在此專案工作時的最高指導原則與專案規範。所有程式碼變更皆須嚴格遵守以下規範。

---

## 1. 核心開發鐵則：測試驅動開發 (TDD)

**強制要求：以後所有程式碼變更（新功能、業務邏輯修改、錯誤修復），都必須「先寫測試，再寫程式碼」。**

### Red-Green-Refactor 循環
1. **Red（先寫失敗測試）**
   - 在撰寫任何產品程式碼（`Controllers/`、`Services/`、`Models/`、`Data/`、`Views/`）之前，**必須先在測試中新增或更新對應的測試案例**（優先於 `tests/PropertyManagement.Tests/` 撰寫 xUnit 單元測試，涉及端到端路由／畫面者同步更新 `tests/smoke.py`）。
   - 執行測試並確認新測試因功能尚未實作而**失敗（Red）**，確保測試確實能捕捉目標行為，而非空包彈。
2. **Green（撰寫最小可行實作）**
   - 撰寫剛好能讓測試通過的實作程式碼。
   - 建置並執行測試，確認新舊單元測試與整合測試**全數通過（Green）**。
3. **Refactor（重構與清理）**
   - 在不改變外部行為且維持測試全綠的前提下，重構程式碼、消除重複、強化可讀性與邊界防護。
   - 再次執行完整測試確認無回歸問題。

### 禁止事項
- **嚴禁**先寫完功能程式碼才補寫測試，或只改程式碼卻不寫測試。
- **嚴禁**為了讓測試通過而刪除、弱化既有測試斷言，或繞過驗證邏輯。
- 純文件更新或純視覺樣式微調除外，但若涉及 DOM 結構、表單欄位、路由、狀態或任何計算規則變動，一律適用 TDD 流程。

---

## 2. 建置、啟動與測試指令

- **技術棧**：.NET 10 (`net10.0`)、ASP.NET Core MVC、Razor Views、Entity Framework Core、SQLite、xUnit（C# 單元測試）、Python 3（HTTP 整合測試，無第三方套件依賴）。
- **本機建置**：
  ```sh
  dotnet build
  ```
  > 要求：**零警告、零錯誤**。
- **執行單元測試 (xUnit)**：
  ```sh
  dotnet test tests/PropertyManagement.Tests/PropertyManagement.Tests.csproj
  ```
  > 使用 SQLite In-Memory 資料庫測試業務規則、模組服務、控制器、資料轉換與種子資料。
- **執行端到端整合測試**：
  ```sh
  dotnet build && python3 tests/smoke.py
  ```
  > `tests/smoke.py` 會自動以隨機連接埠與獨立臨時 SQLite 資料庫啟動網站，執行端到端 HTTP 驗收後自動清理，不會污染 `App_Data/property.db`。
- **本機開發啟動**：
  ```sh
  dotnet run --launch-profile http
  ```
  預設位址為 `http://localhost:5188`。

---

## 3. 專案架構與職責分工

| 路徑 | 職責說明 |
| --- | --- |
| `Models/Domain.cs` | 八大核心實體（`Asset`、`Unit`、`Tenant`、`Lease`、`Meter`、`Reading`、`Invoice`、`Payment`）、共通欄位與並行版本識別 |
| `Models/DashboardModel.cs` | 首頁營運總覽之 ViewModel |
| `Data/AppDbContext.cs` | EF Core 資料庫映射、精確數值轉換（放大 100 倍整數儲存）、唯一索引與外鍵刪除限制 |
| `Data/SeedData.cs` | Development 環境首次啟動之示範資料初始化 |
| `Services/Catalog.cs` | 各模組欄位定義、下拉選單、狀態常數與畫面模型 |
| `Services/ModuleService.cs` | 通用 CRUD、白名單欄位綁定、後端驗證、關鍵字搜尋與 SQL `Skip`/`Take` 分頁 |
| `Services/Business.cs` | 租約重疊檢查、首末月比例計租、按月出帳、分次收款、水電抄表差額計費及歷史帳款鎖定規則 |
| `Controllers/ManageController.cs` | 八大模組之清單、新增、編輯、刪除確認及按月產生應收路由 |
| `Controllers/HomeController.cs` | 營運總覽指標、六個月收支趨勢、90 天到期租約與逾期未收統計 |
| `Views/` & `wwwroot/` | Razor 視圖、響應式樣式 (`wwwroot/css/site.css`) 與互動腳本 (`wwwroot/js/site.js`) |
| `tests/PropertyManagement.Tests/` | xUnit C# 單元測試專案（涵蓋 `Business`、`ModuleService`、`Catalog`、`Controllers`、`AppDbContext` 與 `SeedData`） |
| `tests/smoke.py` | 端到端 HTTP 整合測試腳本 |
| `docs/開發規範.md` | 48 項完整產品決策、資料規則與計算公式（修改業務邏輯前必讀） |

---

## 4. 關鍵業務與工程護欄

1. **計算與金額規則**
   - 所有業務計算必須放在 `Services/`，不得散落於 `Views/` 或前端腳本。
   - 租金、押金、應收與收款皆為**整數元**；面積、水電讀數與單價最多**兩位小數**。
   - SQLite 中所有 `decimal` 以放大 100 倍的整數儲存，避免浮點誤差；金額與計算上限為 `999,999,999`。
   - 首末月不足月租金公式：`月租 × 本月租用天數 ÷ 本月總天數`（起訖日皆計入，`MidpointRounding.AwayFromZero` 四捨五入至整數元）。
   - 水電費公式：`(本次讀數 − 前次讀數) × 本期單價`（`AwayFromZero` 四捨五入至整數元；讀數不得倒退）。
2. **資料完整性與帳務保護**
   - 已有關聯紀錄的主檔禁止實體刪除；帳務（應收、收款）與抄表紀錄不提供刪除，僅能透過「作廢」或「停用」管理。
   - 已有收款關聯的應收帳款鎖定金額與歸屬；收款紀錄不可改寫金額與歸屬，須作廢原筆後重開。
   - 同一出租單位在同一期間只能有一份有效租約，嚴禁租期重疊。
   - 修改已有資料庫結構時，必須建立遷移與升級流程，**嚴禁直接刪除使用者資料庫 (`App_Data/property.db`) 代替升級**。
3. **安全性與並行控制**
   - 所有寫入操作一律使用 `POST` 並驗證防偽權杖（Anti-forgery Token）；查詢使用 `GET`。
   - 編輯表單必須檢查並行版本（Concurrency Version），防止過期表單覆寫較新的修改。
   - 新欄位必須在 `Services/` 明確註冊允許綁定的欄位，禁止將未受控的表單內容直接映射至資料庫實體。

---

## 5. Git 版本控制與語言規範

- **語言**：所有介面文案、錯誤訊息、註解、文件與 Git Commit 訊息一律使用**繁體中文**（台灣用語）。
- **忽略檔案**：不得將 `App_Data/*.db`、建置產物（`bin/`、`obj/`）、密碼或臨時驗收截圖納入版控。
- **Commit 規範**：每完成一項獨立功能或修正，確認 `dotnet build` 與 `python3 tests/smoke.py` 全數通過後，以繁體中文撰寫清楚的提交標題與內文（涵蓋實作內容、業務規則與驗證結果）。
