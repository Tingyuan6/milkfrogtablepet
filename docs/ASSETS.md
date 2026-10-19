# 素材与生成协议

## 来源

应用封面为用户提供的眼睛特写原图，保存在 `assets/source/app-cover.jpg`。直接沿用原图设计，转换为512×512 PNG和16/24/32/48/64/128/256像素ICO，用于Android桌面图标、应用首页标识及Windows程序/托盘图标。封面不参与桌宠角色动画。需要重新转换时，安装Pillow后运行 `python tools/prepare-branding.py`。

`assets/source/` 保存六个用户认可的原始输入：idle、sway、sit_1、sit_2、peek_left、bottom。正式端点不重画；`sourceSha256` 记录来源字节哈希。全部使用320×320角色坐标系，不把半身坐姿强行补腿或拉成全身高度。

`assets/source/bridges/` 的三张透明条带由内置 imagegen 参考相应端点生成，每张有三个中间关键姿态：蹲下到趴姿、抱臂到托腮、站立到左边探头。它们是新增过渡资源，不能替代或证明原端点完全一致。生成提示见同目录 README。

## 派生过程

V0.2 面部动画：`assets/source/faces.json` 标注六种稳定姿态的眼部椭圆、嘴巴位置与角度；`tools/prepare-face-assets.py` 将用户提供的 `andi.wav` 原样复制到运行资源，并每10ms分析20–30ms窗口的音量与高频变化，输出 `face.json`。Windows读取实际音频设备播放位置，Android读取MediaPlayer当前位置，驱动平滑开合与圆唇；口型停止后渐闭。原角色通过局部纹理采样、眼睑遮罩与带阴影的口腔在程序画布上连续补帧，保持透明轮廓与身体像素。闭眼80ms、停留25ms、睁眼135ms，三秒一个周期；交互或姿态过渡会重置待机计时。主观口型与眼睑自然度仍需结合设备和显示大小验收。

语音使用用户上传的完整两秒PCM WAV，本地播放。声音同步接口依据 [Windows音频播放位置](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-waveoutgetposition) 和 [Android MediaPlayer](https://developer.android.com/reference/android/media/MediaPlayer#getCurrentPosition())。原文件SHA256保存在运行清单中。

1. 通过 sharp 解码完整GIF画面，保留37帧及不均匀时长，共4390ms；不按源无限循环设置重复播放。
2. 归一化中间关键姿态，去除孤立/异常边缘颜色，按相同逻辑坐标对齐接触下沿。
3. 以轮廓和纵向标志对应建立几何中间状态，结合双向 Farneback 光流生成小幅补帧。大动作经过关键姿态，避免直接从两个相差很大的端点硬变形。
4. 导出静止眼部的25个方向变体；Windows 使用相同眼部范围实时平滑形变。原图底层与变形区域只出现一个瞳孔。
5. 生成左右镜像、角色范围、接触点、逐帧清单与过渡路径。核验反向路径、透明格式、原帧像素及眼部修改范围。

## manifest.json

- `version`、`canvas`：当前协议1与320×320画布。
- `poses`：资源ID对应相对PNG路径、有效范围、接触点、头部和拖动支点。
- `sway`：原帧ID数组、逐帧毫秒时长、总时长、单次策略。
- `clips`：起止资源ID、包含两端点的帧路径序列、持续时间、方法及人工审查状态。
- `eyes`、`belly`：眼部局部区域与腹部热区标注。

应用只解析本地资源协议，不依赖个人桌面目录。Windows使用PNG缓存，Android使用有上限的Bitmap缓存；派生资源可以重建。生成器与发布脚本按当前清单移除不再引用的旧派生PNG。

机器检查保证尺寸、像素保留与路由完整性；无法保证每张补帧解剖结构或运动观感自然。所有转场仍标记为原型美术资源。
