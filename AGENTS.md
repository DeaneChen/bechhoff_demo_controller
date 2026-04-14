# VariableBlade_Measure (Beckhoff TwinCAT 3) — Agent Notes

本仓库是 TwinCAT 3 工程（Visual Studio / TcXaeShell 方案 + System Manager 配置 + PLC 工程）。

## 入口与关键文件

- 方案入口：`VariableBlade_Measure.sln`
- 系统/IO 配置（EtherCAT 树、设备、PDO 等）：`VariableBlade_Measure/VariableBlade_Measure.tsproj`
- PLC 工程：`VariableBlade_Measure/main/main.plcproj`
  - 任务：`VariableBlade_Measure/main/PlcTask.TcTTO`
  - 程序/POU：`VariableBlade_Measure/main/POUs/*.TcPOU`

## 上位机（C#）

- 上位机解决方案位置：`PcHost/PcHost/PcHost.sln`
  - 控制台：`PcHost/PcHost/PcHostConsole/PcHostConsole.csproj`
  - 可复用核心库（供后续WPF/WinForms共用）：`PcHost/PcHost/PcHost.Core/PcHost.Core.csproj`
- ADS 通信库：`Beckhoff.TwinCAT.Ads`（NuGet，当前在 `PcHost.Core` 引用）
  - 仅装 NuGet 不等于能通讯：PC 侧仍需要有 ADS Router/路由配置（TwinCAT 或 Beckhoff 的 ADS Runtime/Router），并且要添加到目标（PLC）设备的 AMS Route。
  - 直连 169.254.x.x（link-local）时，目标 AMS NetId 通常形如 `169.254.231.128.1.1`（以实际为准）。
  - 本工程 `VariableBlade_Measure.tsproj` 的 `TargetNetId` 目前为 `5.132.153.117.1.1`（对应你选择的目标 CX-849975）。
  - `PcHostConsole` 采用 SDK-style `csproj`，Release 输出目录会自动带上 `TwinCAT.Ads*.dll` 等运行时依赖；请从 `PcHostConsole/bin/Release` 目录运行可执行文件。

## 高频采集注意事项（500~2000Hz）

- 不建议 PC 以 0.5~2ms 周期“逐点 ADS Read”取样：Windows 调度抖动 + ADS 往返开销会导致不稳定。
- 建议方案：PLC 内部高速任务采集 → 写入环形缓冲（带 `t_us/t_ms` 时间戳）→ PC 以较低频率批量拉取/订阅（例如 50~200Hz）并在本地落盘还原为 2kHz 数据流。

## Demo：EL3742 Ch2 Sample0 读取

- PLC 侧已添加示例 GVL：`VariableBlade_Measure/main/GVLs/GVL_PcDemo.TcGVL`
  - 需要在 TwinCAT I/O Mapping 中把 `EL3742 -> Ch2 Sample 0 -> Ch2 Value` 链接到 `GVL_PcDemo.EL3742_Ch2_Sample0_Raw`
  - 如果 I/O Mapping 的变量选择窗口里“看不到 PLC 变量”，可将待映射变量声明为显式 I/O 地址占位：输入用 `AT %I*`、输出用 `AT %Q*`（例如 `EL3742_Ch2_Sample0_Raw AT %I*: INT;`），再进行 Link。
- PLC 侧示例逻辑在：`VariableBlade_Measure/main/POUs/MAIN.TcPOU`
- PC 侧可用：`PcHostConsole.exe --ams <AmsNetId> read-i16 GVL_PcDemo.EL3742_Ch2_Sample0_RawCopy`

## Demo：EL6022 RS-485 自收自发（Ch1↔Ch2）

- PLC 侧 I/O 映射占位：`VariableBlade_Measure/main/GVLs/GVL_EL6022_IO.TcGVL`
  - 需要把 EL6022 的以下过程数据映射到 GVL（输入用 `AT %I*`、输出用 `AT %Q*`）：
    - `COM Inputs Channel 1`：`Status (WORD)` → `GVL_EL6022_IO.EL6022_Ch1_Status`；`Data In 0..21` → `GVL_EL6022_IO.EL6022_Ch1_DataIn[0..21]`
    - `COM Outputs Channel 1`：`Ctrl (WORD)` → `GVL_EL6022_IO.EL6022_Ch1_Ctrl`；`Data Out 0..21` → `GVL_EL6022_IO.EL6022_Ch1_DataOut[0..21]`
    - `COM Inputs Channel 2`：同理映射到 `Ch2_*`
    - `COM Outputs Channel 2`：同理映射到 `Ch2_*`
- PLC 侧 PC 交互变量：`VariableBlade_Measure/main/GVLs/GVL_Rs485Demo.TcGVL`
- `Status/Ctrl` 位定义（按 EL6022 PDO 常见约定）：
  - `WORD` 低字节：位标志；高字节：长度（bytes）
  - `Ctrl` bit0=`Transmit request`，bit1=`Receive accepted`，bit2=`Init request`，bit3=`Send continuous`，高字节=`Output length`
  - `Status` bit0=`Transmit accepted`，bit1=`Receive request`，bit2=`Init accepted`，bit3=`Buffer full`，bit4=`Parity error`，bit5=`Framing error`，bit6=`Overrun error`，高字节=`Input length`
- 上位机回环测试：`PcHostConsole.exe --ams <AmsNetId> el6022-loopback --tx-ch 1 --hex \"01 02 03\" --timeout-ms 2000`

## TwinCAT ST 兼容性注意事项（本工程）

- 在当前 TwinCAT/XAE 编译器设置下，避免使用以下写法：
  - 命名参数形式：`SHL(IN := ..., N := ...)` / `SHR(IN := ..., N := ...)`
  - “类型名当函数”的强转：`WORD(x)` / `USINT(x)`
- 推荐使用更通用写法：
  - 移位：`SHL(x, 8)` / `SHR(x, 8)`（2 个操作数）
  - 类型转换：`USINT_TO_WORD(...)`、`WORD_TO_USINT(...)` 等显式转换函数

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
