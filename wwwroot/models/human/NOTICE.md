# 人體模型來源

## 體表模型（body.glb）

- 來源：MakeHuman Community，`makehuman/data/3dobjs/base.obj`。
- 專案：https://github.com/makehumancommunity/makehuman
- 來源版本：`a8bc2d54ff0ac92e78ff71431b1023eda42bf482`。
- 授權：CC0 1.0，完整內容見 `LICENSE.ASSETS.md`。本專案僅採用模型與骨架資料，未引入 MakeHuman 程式碼。
- 處理：依來源 `default.mhskel` 與 `default_weights.mhw` 調整手臂姿勢，移除輔助網格，保留眼球，細分平滑一次，縮放後輸出 GLB。
- 用途：工傷部位選取示意。圖面與骨架參考不作為醫療診斷依據。


## 骨架參考模型（skeleton.glb）

- 本專案以 `tools/build_skeleton.py` 建立的原創簡化模型，與上列 MakeHuman 體表模型使用同一座標。
- 包含頭顱與下顎、十二對肋骨、二十四節頸胸腰椎、骨盆、肩胛、長骨及手足小骨。
- 建立指令：`blender --background --python tools/build_skeleton.py`（需另外安裝 Blender；網站執行時不需要）。
- 結構參考：[OpenStax 胸廓](https://openstax.org/books/anatomy-and-physiology-2e/pages/7-4-the-thoracic-cage)、[脊柱](https://openstax.org/books/anatomy-and-physiology-2e/pages/7-3-the-vertebral-column)、[上肢骨](https://openstax.org/books/anatomy-and-physiology-2e/pages/8-2-bones-of-the-upper-limb)。未複製其圖片或模型。
