# 奶蛙 · milkfrogtablepet

this is a gift for me as my first project,also for the milk family

奶蛙 V0.1 是使用现有黄色角色素材的桌宠基础互动版，包含 Windows 程序与 Android 测试应用。眼睛跟随、点击晃动、拖动与停靠均在本地运行。智能服务目前保留独立接口，具体对话、记忆和工具功能等待后续需求。

## 使用 Windows 版

解压完整便携包，双击其中的 **奶蛙.exe**。程序与 `assets` 文件夹需要放在一起。支持 Windows 10/11 x64、.NET Framework 4.8；本轮在 Windows 11、150% 缩放下验证。

- 鼠标靠近时，眼睛平滑跟随；移远后回正。
- 在自由站立状态点击腹部，晃动一遍后回到静止；动作中再点一次会退出。
- 拖到底边切换趴姿，拖到左、右边切换探头；右侧自动镜像。
- 从边缘拖回中间恢复站立。拖动松手不会触发晃动。
- 右键奶蛙或托盘图标，可恢复站立、调整大小、关闭视线跟随或退出。

位置、大小保存在程序旁的 `data/settings.json`，删除该设置文件可恢复默认位置。没有自动开机启动，也不需要模型服务或 API 密钥。

## 使用 Android 测试版

安装测试 APK，先在应用内拖动和点击奶蛙。点击底部输入框打开真正的软件键盘：奶蛙切到坐姿一，点击角色切到坐姿二，再次点击返回；收起键盘恢复先前位置。

“开启桌面奶蛙”会引导授权悬浮显示。可选的“跟随其他应用的键盘”会引导开启奶蛙键盘跟随服务，该服务仅使用输入法窗口类型和矩形，不读取输入文字或节点内容。可在应用或常驻通知中收起奶蛙。

**Android 已通过编译、签名及纯逻辑测试，尚未通过真机界面验收。** 不同输入法、浮动/分体键盘和厂商后台策略需要分别验证。悬浮窗口按角色范围收紧，但透明角落仍可能拦截触摸。详见 [状态与验收](docs/STATUS.md)。

## 从源码构建

Windows 工程不需要 .NET SDK。GitHub 仓库保存来源素材和生成工具，先生成派生资源；完整源码素材 ZIP 已包含派生资源，可跳过这一步。

```powershell
# Node 24 与 pnpm 11.25.0 为本轮实际验证版本
pnpm install --frozen-lockfile
pnpm run assets
pnpm run verify-assets

./build-windows.ps1 -Test
# 可选：短暂显示原生测试窗口；不会移动或注入物理鼠标
./build-windows.ps1 -NativeTest
```

生成程序位于 `dist/windows/奶蛙.exe`。

Android 使用 JDK 17、SDK Platform 35 和 Build Tools 35.0.0；无需 Gradle即可运行构建脚本：

```powershell
./build-android.ps1 -Jdk '你的JDK目录' -Platform '你的SDK/platforms/android-35' -BuildTools '你的SDK/build-tools/35.0.0'
./test-java.ps1 -Jdk '你的JDK目录'
```

产物位于 `dist/android/奶蛙-0.1.0-测试版.apk`，使用本地生成的开发证书。私钥位于 `build/` 并已排除提交；升级同一测试安装时应保留自己的开发证书。`android/` 同时提供标准原生 Android 工程配置，当前直接 SDK 脚本构建路径已验证。

## 工程说明

- [实现结构与技术决策](docs/ARCHITECTURE.md)
- [状态、测试证据与待验收项](docs/STATUS.md)
- [素材协议与生成过程](docs/ASSETS.md)
- [原始 V0.1 需求方案](docs/PLAN-V0.1.md)

Windows 交互核心使用 C#，Android 交互核心使用 Java，通过同一素材协议与从实际清单导出的测试夹具保持规则一致。大幅姿态转场仍存在局部纹理和比例变化，属于需要继续精修的原型素材，不能视作最终美术验收。
