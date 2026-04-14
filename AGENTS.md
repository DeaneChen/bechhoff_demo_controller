# VariableBlade_Measure (Beckhoff TwinCAT 3) — Agent Notes

本仓库是 TwinCAT 3 工程（Visual Studio / TcXaeShell 方案 + System Manager 配置 + PLC 工程）。

## 入口与关键文件

- 方案入口：`VariableBlade_Measure.sln`
- 系统/IO 配置（EtherCAT 树、设备、PDO 等）：`VariableBlade_Measure/VariableBlade_Measure.tsproj`
- PLC 工程：`VariableBlade_Measure/main/main.plcproj`
  - 任务：`VariableBlade_Measure/main/PlcTask.TcTTO`
  - 程序/POU：`VariableBlade_Measure/main/POUs/*.TcPOU`

## 修改约定（重要）

- 尽量不要手工编辑 `VariableBlade_Measure/_Boot/**` 与 `VariableBlade_Measure/_Config/**`：
  - 这些通常是从工程导出/生成的目标配置或部署产物，应该由 TwinCAT/XAE 生成。
- PLC 逻辑优先改 `.TcPOU`（IEC 61131-3，常见为 ST），需要新增全局变量/数据类型时：
  - GVL：`VariableBlade_Measure/main/GVLs/*.TcGVL`
  - DUT：`VariableBlade_Measure/main/DUTs/*.TcDUT`
- IO 端子/模块是否“插入到工程里”，以 `VariableBlade_Measure.tsproj` 的 EtherCAT `<Box>` / `Type="ELxxxx..."` / `Desc="ELxxxx"` 记录为准。

## 快速自检（离线）

- 查 EtherCAT 端子型号：在 `VariableBlade_Measure.tsproj` 里搜索 `Desc="EL` 或 `Type="EL`.
- 查是否包含某端子（例如 EL6022）：全工程搜索 `EL6022` 是否命中。

## 工具提示

- 在本环境里 `rg`（ripgrep）可能无法执行；优先用 PowerShell 的 `Select-String` 进行全文搜索。

