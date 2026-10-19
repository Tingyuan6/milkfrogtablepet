# 奶蛙 · milkfrogtablepet

<img src="assets/source/app-cover.jpg" alt="奶蛙应用封面" width="240">

this is a gift for me as my first project,also for the milk family

奶蛙 V0.3 是使用现有黄色角色素材的桌宠基础互动版，包含 Windows 程序与 Android 测试应用。眼睛跟随、点击晃动、拖动与停靠均在本地运行。智能服务目前保留独立接口，具体对话、记忆和工具功能等待后续需求。

## 下载与安装（从这里开始）

**先登录 GitHub。** 当前仓库是私有仓库，请使用 Tingyuan6 或已获授权的账号；看到 404 时先检查是否登录了正确账号。

| 你的设备 | 点击下载 |
| --- | --- |
| Windows 电脑 | [下载 Windows 版（ZIP）](https://github.com/Tingyuan6/milkfrogtablepet/releases/download/v0.3.0/Naifrog-Windows-v0.3.0.zip) |
| 安卓手机／平板 | [下载 Android 版（APK）](https://github.com/Tingyuan6/milkfrogtablepet/releases/download/v0.3.0/Naifrog-Android-v0.3.0.apk) |

也可以打开[下载页面 Releases](https://github.com/Tingyuan6/milkfrogtablepet/releases/tag/v0.3.0)，找到 **Assets**（下载文件列表），点击对应文件。电脑选名字带 **Windows**、后缀 **.zip** 的文件；安卓选名字带 **Android**、后缀 **.apk** 的文件。**Source code 是源码，普通使用无需下载。**

### Windows 电脑：下载 → 解压 → 打开

1. 用电脑浏览器登录 GitHub，点击上面的 **下载 Windows 版（ZIP）**。
2. 下载完成后，在浏览器的下载列表中点“在文件夹中显示”，或打开电脑的“下载”文件夹。
3. 找到 **Naifrog-Windows-v0.3.0.zip**，右键点击它，选择 **全部解压缩**，再点击 **解压**。
4. 打开解压出来的文件夹，再进入 **奶蛙V0.3.0-Windows** 文件夹。
5. 双击 **奶蛙.exe**，奶蛙就会出现在桌面上。保留同一文件夹里的 **assets**，不要单独移动 exe。
6. 退出时，右键点击奶蛙，选择 **退出**。

### 安卓手机／平板：下载 → 安装 → 打开

1. 用手机／平板的浏览器登录 GitHub，点击上面的 **下载 Android 版（APK）**。
2. 下载完成后，点击下载通知；也可以打开“文件管理”中的“下载”，找到 **Naifrog-Android-v0.3.0.apk**。
3. 点击 APK，选择 **安装**。
4. 如果提示当前浏览器或文件管理器没有安装权限，点击提示里的 **设置**，为这个应用开启 **允许来自此来源的应用**（各品牌文字可能不同），再返回继续安装。安装后可关闭这项权限。
5. 安装完成，点击 **打开**。先在应用内点击、拖动奶蛙，点击底部输入框体验键盘上方的坐姿。
6. 想让它出现在其他应用上方，点击 **开启桌面奶蛙**，按页面引导允许悬浮显示。跨应用键盘跟随可在应用中另行开启。

手机版目前支持 **Android**；iPhone／iPad 暂不支持。Android 为测试版，仍需真机验收。

## V0.3 嘴型与打开文件大笑

- 两端六种稳定姿态的“安迪”嘴型加入上下牙齿、粉色舌头和口腔阴影；跟随原两秒语音，闭嘴时仍使用原图。
- Windows：在桌面或Windows资源管理器中双击文件，或选中文件后按Enter，奶蛙进入从用户视频提取的透明大笑动作，并播放视频笑声。单击选中、打开文件夹、搜索框输入或其他应用中的双击不触发。
- 点击任意位置立即停止笑声，再补帧返回原姿态和位置；倒地时先沿原动作反向起身，最长约1.5秒。大笑在边缘触发时会暂时移到可见范围，结束后恢复。
- 右键奶蛙或托盘可关闭“打开文件时大笑”，也可选择“停止大笑”。识别只检查资源管理器项目和文件元数据，不读取文件内容，不保存文件名或键盘历史，不拦截正常点击和Enter操作。
- 文件触发目前针对Windows资源管理器/桌面的双击与Enter；第三方文件管理器、系统单击打开模式和右键菜单“打开”不在此版本的触发范围。Android这次同步嘴型，不监听其他应用打开文件。

## V0.2.2 安卓素材读取修复

修复 Windows SDK 打包时子目录使用反斜杠、导致 Android 找不到 `poses/idle.png` 的问题。图片、补帧和语音路径统一为 APK 标准正斜杠路径。签名后逐项核对全部415个素材的原始ZIP路径和文件内容；旧包中的错误路径会使构建失败，避免再次发布。新增7项打包回归验证。

旧的素材路径问题已包含在当前版本修复中；安卓可直接覆盖安装，无需卸载。V0.2.1的错误记录、权限返回续开和找回角色功能保留。仍需要在问题手机上确认显示与悬浮，详见[安卓排错步骤](docs/ANDROID-TROUBLESHOOTING.md)。

## V0.2 更新

- 点击嘴巴播放两秒“安迪”录音，口型跟随实际播放位置及语音起伏；再次点击从头播放，拖动或切换动作时停止。
- 站立、左右探头、底部趴姿和两种坐姿均支持靠近时的眼睛跟随；安卓也支持触摸或鼠标悬停。
- 停止操作三秒后眨眼，闭眼与睁眼间进行连续补帧；每三秒重复一次。

## 使用 Windows 版

解压完整便携包，双击其中的 **奶蛙.exe**。程序与 `assets` 文件夹需要放在一起。支持 Windows 10/11 x64、.NET Framework 4.8；本轮在 Windows 11、150% 缩放下验证。

- 鼠标靠近眼睛时，所有稳定姿态的眼睛平滑跟随；移远后回正。
- 点击嘴巴播放本地语音并同步开合口型；无操作时每三秒眨眼。
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
# 素材生成需要 Node 24、pnpm 11.25.0 和 Python 3（生成语音包络）
pnpm install --frozen-lockfile
pnpm run assets
pnpm run verify-assets

# Windows大笑素材；使用仓库中提供的原视频，固定依赖
python -m pip install -r tools/requirements-video.txt
python tools/prepare-laugh-assets.py
python tools/prepare-laugh-bridges.py
python tools/verify-laugh-assets.py

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

产物位于 `dist/android/奶蛙-0.3.0-测试版.apk`，使用本地生成的开发证书。私钥位于 `build/` 并已排除提交；升级同一测试安装时应保留自己的开发证书。`android/` 同时提供标准原生 Android 工程配置，直接 SDK 脚本在签名前规范化ZIP路径，并在签名后校验全部素材；构建失败时不要分发产物。

## 工程说明

- [实现结构与技术决策](docs/ARCHITECTURE.md)
- [状态、测试证据与待验收项](docs/STATUS.md)
- [素材协议与生成过程](docs/ASSETS.md)
- [原始 V0.1 需求方案](docs/PLAN-V0.1.md)

Windows 交互核心使用 C#，Android 交互核心使用 Java，通过同一素材协议与从实际清单导出的测试夹具保持规则一致。大幅姿态转场仍存在局部纹理和比例变化，属于需要继续精修的原型素材，不能视作最终美术验收。
