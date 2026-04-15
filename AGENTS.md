# VariableBlade_Measure (Beckhoff TwinCAT 3) — Agent Notes

本仓库是 TwinCAT 3 工程（Visual Studio / TcXaeShell 解决方案 + System Manager 配置 + PLC 程序）。

## 入口与关键文件
- 解决方案入口：`VariableBlade_Measure.sln`
- 系统/IO 配置（EtherCAT 树、设备、PDO 等）：`VariableBlade_Measure/VariableBlade_Measure.tsproj`
- PLC 工程：`VariableBlade_Measure/main/main.plcproj`
  - PLC 程序/POU：`VariableBlade_Measure/main/POUs/*.TcPOU`
  - 全局变量：`VariableBlade_Measure/main/GVLs/*.TcGVL`

## 上位机（C# / ADS）
- 上位机解决方案：`PcHost/PcHost/PcHost.sln`
  - 控制台：`PcHost/PcHost/PcHostConsole/PcHostConsole.csproj`
  - 可复用核心库（供后续 WPF/WinForms 共用）：`PcHost/PcHost/PcHost.Core/PcHost.Core.csproj`
- ADS 库：NuGet `Beckhoff.TwinCAT.Ads`
  - 仅安装 NuGet 不等于能连通：PC 侧仍需配置 ADS Router/Route（到目标 PLC 的 AMS Route）。
  - 运行时依赖：请从 `PcHostConsole/bin/Release` 目录运行（确保 `TwinCAT.Ads*.dll` 在同目录）。
- 目标 PLC 的 AmsNetId 示例：`5.132.153.117.1.1`（CX-849975），PLC 默认端口：`851`。

## 高速采集（500–2000Hz）建议
- 不建议 PC 以 0.5–2ms 周期做“逐点 ADS Read”：Windows 调度抖动 + ADS 往返开销会导致不稳定。
- 推荐：PLC 内部高速任务采集 → 写入环形缓冲（带 `t_us/t_ms` 时间戳）→ PC 以较低频率批量拉取/订阅（如 50–200Hz）并在本地还原为 2kHz 数据流。

## TwinCAT I/O Mapping：变量“看不到”的常见原因
- TwinCAT I/O Mapping 的 `Link To...` 列表里经常只显示带 **显式 I/O 地址占位** 的变量。
- 需要映射 EtherCAT PDO 的 PLC 变量建议写成：
  - 输入：`... AT %I* : <Type>;`
  - 输出：`... AT %Q* : <Type>;`
  - 例：`EL3742_Ch2_Sample0_Raw AT %I*: INT;`

## Demo：EL3742 Ch2 Sample0 读取
- PLC 侧：`VariableBlade_Measure/main/GVLs/GVL_PcDemo.TcGVL`
- TwinCAT I/O Mapping：`EL3742 -> Ch2 Sample 0 -> Ch2 Value` 链接到 `GVL_PcDemo.EL3742_Ch2_Sample0_Raw`
- PC 侧读取示例：`PcHostConsole.exe --ams <AmsNetId> read-i16 GVL_PcDemo.EL3742_Ch2_Sample0_RawCopy`

## Demo：EL6022 RS-485 自收自发（Ch1 <-> Ch2）
- PLC I/O 映射占位：`VariableBlade_Measure/main/GVLs/GVL_EL6022_IO.TcGVL`
- Demo 交互变量：`VariableBlade_Measure/main/GVLs/GVL_Rs485Demo.TcGVL`
  - Demo 由 `GVL_Rs485Demo.Enable` 控制
  - 当 `GVL_NimServo.Enable=TRUE` 时，MAIN 会强制禁用 loopback（避免与正式外设驱动抢占同一 EL6022 通道）

## 正式外设：NiMotion 无刷伺服（Modbus RTU over EL6022 / RS-485）
- 约束：电机的 Modbus/伺服控制命令 **全部在 PLC 上实现**；PC 只做宏观指令（切模式/使能/目标值）与数据获取（ADS）。
- 寄存器映射来源：`Sdk/NimServoSDK-MM-bin-linux-x64/bin/Modbus.db`
- PLC 侧实现位置：
  - 接口/遥测：`VariableBlade_Measure/main/GVLs/GVL_NimServo.TcGVL`
  - Modbus Master：`VariableBlade_Measure/main/POUs/FB_ModbusRtuMaster.TcPOU`
  - EL6022 端口：`VariableBlade_Measure/main/POUs/FB_EL6022_Port.TcPOU`
  - 伺服控制：`VariableBlade_Measure/main/POUs/FB_NimServo.TcPOU`

### 关键前置：控制模式必须是 CIA402
- 参数：`I2002-1 (H00B1, 控制模式选择)` 必须为 `0 = CIA402`。
- 默认值可能是 `4`（NiMotion 开环等模式），会导致 6040/6041 状态机不按预期工作。
- 代码中提供 `AutoSetCia402Mode`：Enable 后会先读 H00B1，必要时写 0 并回读确认。

### 电机“不转”的高概率原因：DI 使能信号未满足
- DI1 默认功能：`I2003-3 (H00D5) = 1 使能`
- DI1 默认逻辑：`I2003-4 (H00D6) = 0 低电平有效`
- 若 DI1 没有按该逻辑被拉到有效电平，驱动通常会停在 `StatusWord` 的 **Switch on disabled**（常见观测值如 `0x1260`）。
- 实际接线前请先读回 `H00D5..H00DC`（DI1..DI4 功能/逻辑），确认到底哪一路被配置成了“使能(1)”以及其有效电平。
- 诊断寄存器：
  - `I200B-5 (H01E2) 输入信号(DI信号)`：DI 监视位
  - `I200B-6 (H01E3) 输出信号(DO信号)`：DO 监视位（可用于确认抱闸/报警等输出状态）
  - `I200B-21 (H01F7) 母线电压值`、`I200B-22 (H01F8) 模块温度值`：确认驱动上电/电源侧是否正常
  - `I200B-1 (H01DE) 驱动器内部状态`：0=未准备好、1=准备好、8=速度闭环控制等（详见 Modbus.db 的 selects）
  - `I200B-24 (H01FA) FPGA给出的系统状态` / `I200B-25 (H01FB) FPGA给出的系统故障` / `I200B-27 (H01FD) 故障记录`：用于定位“Not ready”的更底层原因
  - `I1001 (H0000) 错误寄存器` / `I1003-0 (H0001) 当前报警`
- 注意：DI 功能/逻辑等参数在 `Modbus.db` 里 `EffectTime=2`，通常意味着需要上电/重启后生效（请以厂家手册为准）。

### 调试技巧：驱动器内部“强制 DI/DO”（不等于安全输入）
- `I200D-7 (H0250) DIDO强制输入输出使能`
- `I200D-8 (H0251) DI强制输入给定`（通常 bit0=DI1、bit1=DI2…，以实测/手册为准）
- 这类“强制输入”一般只能用于调试逻辑，**不保证**能绕过 STO/急停等安全链路，且不建议作为正式方案。

### 32-bit 量的字序（word order）风险
- 例如 `60FF/606C/6064` 为 32-bit（2 个寄存器），高低字顺序可能与预期相反。
- 工程内提供 `WordSwap32` 开关用于适配。

### VM 模式注意：目标速度不是 60FF
- NimServo SDK 的 VM 示例（`test_vm.c` / `test_vm.py`）使用的是 VM 专用寄存器，而不是 CiA402 的 `60FF`：
  - `I6042 (H0382) VM模式的目标速度`（16-bit, rpm）
  - `I6044 (H0384) VM模式下实际速度`（16-bit, rpm）
  - `I6046-1/2 (H0385/H0387) VM模式速度最小/最大值`（32-bit）
  - `I6048-1 (H0389) VM模式加速度`、`I6049-1 (H038C) VM模式减速度`（32-bit）
- 本工程 PLC 的 `FB_NimServo` 在 `DesiredMode=2 (VM)` 时会写 `H0382`，并读取 `H0382..H0384` 作为 VM 遥测。

### EL6022 限制：Modbus 0x10 一次最多写 6 个寄存器
- EL6022 过程数据一帧只有 22 字节，因此 Modbus RTU `0x10`（写多个寄存器）请求帧会受限：
  - 帧长度 `= 7 + ByteCount + 2`，而 `ByteCount = 2 * 寄存器数`
  - 为了不超出 22 字节，本工程限制 `ByteCount <= 12`（最多 6 个寄存器）
- 例如 VM 配置（速度 min/max、加减速等）需要分两次写入（先 `H0385..H0388`，再 `H0389..H038E`）。

### 幂等启动建议（避免“有时不转”）
- 建议让 `GVL_NimServo.Enable` 长期开启（仅用于打开PLC侧通讯与轮询），用 `GVL_NimServo.PowerEnable` 作为“运行/停机”开关。
- 本工程在 `PowerEnable` 上升沿会强制重发 ControlWord/速度；在 `PowerEnable` 下降沿（VM 模式）会写一次 `H0382=0` 确保停机，再进入下一次启动。
- VM 模式写速度有一个 `200ms` 启动延时（对齐 SDK `power_on` 后的延时），并且当驱动内部状态从“未准备好(H01DE=0)”跳到非 0 时会再强制重发一次 ControlWord/速度，提高复现稳定性。
- VM 模式下写 `H0382` 会在 `Cia402State=Operation enabled` 后才发送（避免驱动未就绪时写入被吞/状态不一致）。
- 重要：早期版本如果你在驱动未到 `Operation enabled` 前就写 `TargetVelocity`，可能会因为“速度写入待处理”逻辑阻塞后续轮询，导致 `StatusWord` 不再更新、状态机卡在 `ControlWord=0x0006`，表现为“偶发不转/重启后不转”。当前版本已修复：当条件不满足时不再 `RETURN`，会继续轮询推进状态机。
- Modbus 帧间隔：`FB_ModbusRtuMaster` 增加 `MinGap`（默认 `5ms`）用于限制相邻两帧的最小静默时间，避免 back-to-back 过快导致某些设备偶发不响应；如仍不稳定可尝试增大到 `10ms` 或 `20ms`。

### 上位机（ADS）推荐启动顺序（VM 模式）
- “一次性初始化”（通常只做一次）：
  - `write-u8  GVL_NimServo.Channel 1`（或 2）
  - `write-u8  GVL_NimServo.SlaveId 1`
  - `write-u8  GVL_NimServo.DesiredMode 2`
  - （可选）写 VM 参数：`VmSpeedMaxRpm/VmAccelRpm/VmDecelRpm`
- “每次运行/停机”：
  1) `write-bool GVL_NimServo.Enable true`（建议常开）
  2) 等待 `read-bool GVL_NimServo.CommOk` 与 `read-bool GVL_NimServo.ControlModeOk` 都为 `true`
  3) `write-bool GVL_NimServo.PowerEnable true`（上升沿触发启动流程）
  4) 等待 `read-u8 GVL_NimServo.Cia402State` 变为 `5`（Operation enabled）
  5) `write-i32 GVL_NimServo.TargetVelocity 200`（rpm，VM 模式写入 `H0382`；可随后改 400/500）
  6) 停机：`write-i32 GVL_NimServo.TargetVelocity 0`，再 `write-bool GVL_NimServo.PowerEnable false`

## TwinCAT ST 兼容性注意事项（本工程）
- 避免使用：
  - 命名参数：`SHL(IN := ..., N := ...)` / `SHR(IN := ..., N := ...)`
  - “类型名当函数”的强转：`WORD(x)` / `USINT(x)`
- 推荐：
  - `SHL(x, 8)` / `SHR(x, 8)`
  - `USINT_TO_WORD(...)`、`WORD_TO_USINT(...)` 等显式转换函数

## 修改约定（重要）
- 尽量不要手工编辑 `VariableBlade_Measure/_Boot/**` 或 `VariableBlade_Measure/_Config/**`（通常为导出/生成产物，建议由 TwinCAT/XAE 生成）。
