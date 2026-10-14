# 禾境物業管理系統檢查入口

本檔提供找證據的入口；業務規則以專案 `AGENTS.md` 與 `docs/開發規範.md` 為準。開始時確認目前檔案、命令及實作，避免把歷史資訊當成事實。

## 依功能追蹤

- **啟動與部署**：從 `Program.cs`、專案檔、設定與部署檔案，追蹤身分驗證、全域授權、防偽、錯誤處理及環境分支。外部代理或身分服務資料未提供時列出證據缺口。
- **入口與綁定**：由 `Controllers/ManageController.cs`、`Controllers/HomeController.cs` 追到 `Services/ModuleService.cs` 及 `Services/Catalog.cs`。檢查讀寫權限、資料歸屬、允許欄位、HTTP 方法、防偽及並行版本。不能從單一組織的示範資料推論產品有多租戶隔離。
- **帳務完整性**：參照 `docs/開發規範.md`，追蹤 `Services/Business.cs` 的租約重疊、出帳、分次收款、已收款帳款鎖定、抄表差額及失敗回復。檢查重複與並行請求，區分一般計算錯誤與可濫用的安全影響。
- **資料與檔案**：檢查 `Data/AppDbContext.cs`、`Data/SeedData.cs`、資料庫路徑及靜態檔案根目錄。驗證約束、刪除限制、交易邊界、種子資料的啟用環境，以及資料庫是否可被公開下載。使用合成資料重現。
- **畫面與腳本**：檢查 `Views/` 與 `wwwroot/` 的不安全輸出、內嵌腳本資料、前端信任值、表單權杖及敏感資料暴露，追溯伺服器端控制。
- **依賴與建置**：核對套件、間接依賴、版本鎖定、來源與 CI。使用環境支援的依賴稽核命令；官方公告不可取得時記錄限制，不能杜撰 CVE 或宣稱沒有弱點。

## 本機驗證

依候選問題選擇必要驗證。檢核本身不要求修改產品或新增測試。

```sh
dotnet build
dotnet test tests/PropertyManagement.Tests/PropertyManagement.Tests.csproj
python3 tests/smoke.py
```

`tests/smoke.py` 設計為使用隨機連接埠與獨立暫存 SQLite 資料庫；執行前確認仍保持隔離。不得以刪除或覆寫 `App_Data/property.db` 建立測試環境。一般測試成功只代表既有斷言通過，不能代替權限或其他安全測試。

使用者要求修正時，依專案規範先取得失敗測試，再修改程式碼。涉及路由或畫面時同步處理 smoke 測試；完成後建置須零警告、零錯誤，並執行單元與整合測試。報告區分修正前重現、修正後驗證及未執行項目。
