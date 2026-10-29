# Chill with You: Lo-Fi Story - Change of Pace - Costume Mod

A BepInEx mod for **Chill with You: Lo-Fi Story** that adds costume switching, hairstyle switching and permanent limited decorations to the "Change of Pace" decoration panel.

[English](#english) | [中文](#中文) | [日本語](#日本語)

---

## English

A BepInEx mod that extends the "Change of Pace" (気分転換) decoration panel with costume switching, hairstyle switching and persistent limited-time decorations.

### Features

- **Costume switching** with three request modes:
  - Once (session only, not saved)
  - One day (saved to today's record)
  - Forever (auto-applied on every launch)
- **Hairstyle switching**: Normal and hairpins A/B/C, following the game's daily rules (kept for the day, rotated daily). The beret follows the game default (Normal hairstyle, switching locked); the Halloween hat and future headwear without its own special style are unlocked, and the pre-hat hairstyle is restored when a special headwear is removed.
- **Permanent limited decorations** that stay equipped across launches and after stories:
  - Magical Princess beret (collab story)
  - Halloween hat / Halloween 2026
  - Christmas 2025 (santa hat + christmas props)
  - April Fools 2026 (sunglasses)
- Headwear temporarily replaces cat-ear headphones; removing it restores your saved headphones (including cat ears). Decorations in the same slot (head) are mutually exclusive, and limited-time events exclude each other; a single hat and the April Fools sunglasses can be used together.
- **Finale-aware**: while main episode 32 is pending, the mod follows the game's default presentation (no auto re-application, no hairstyle override) and resumes afterwards.
- **Future-proof discovery**: new hats are picked up from the game's `HatType` enum, new events from the scene; headwear rules are auto-detected at runtime (mutex + headphone swap) without a mod update.
- **Fault isolation**: every patch and every UI section is registered independently. If a game update breaks one patch, only that feature stops working - the rest keeps running.
- Trilingual UI (English / 中文 / 日本語) with live language switching, using the game's own language & font system.

### Installation

**Requires:** BepInEx 5.4.23.5+

- **First time (no BepInEx):** download the full package `ChangeOfPaceCostume_Full_v1.7.7.zip` from [Releases](https://github.com/fragule-hub/Chill-with-You-Lo-Fi-Story---Change-of-Pace---Costume-Mod/releases) and extract everything into the game folder (same level as `Chill With You.exe`).
- **BepInEx already installed / upgrading:** download `ChangeOfPaceCostume.zip` and copy `ChangeOfPaceCostume.dll` into `BepInEx\plugins\`, overwriting the old file. Your settings are kept automatically.

### Troubleshooting

If the mod features do not appear in-game:

1. Make sure `ChangeOfPaceCostume.dll` is in `BepInEx\plugins\`.
2. Close the game normally, then open `BepInEx\LogOutput.log` with a text editor.
3. Search for `Change of Pace - Costume`. A healthy log shows `[... v1.7.7] loaded.` and `[Inject] Complete: ...`; if it does not, send that part (or the whole file) to the author.

See `Mods/ChangeOfPaceCostume/InstallGuide.txt` for installation, uninstall and FAQ.

### Building from source

Open `Mods/ChangeOfPaceCostume/ChangeOfPaceCostume.csproj` - it references the game's managed assemblies and BepInEx from the local game folder; adjust `GameDir` in the `.csproj` to your install path, then `dotnet build -c Release`.

### License

[MIT](LICENSE)

---

## 中文

为 **Chill with You: Lo-Fi Story** 的"转换心情"（Change of Pace）装饰面板添加服装切换、发型切换与限定装饰常驻功能的 BepInEx Mod。

### 功能

- **服装切换**，支持三种请求模式：
  - 一次（仅本次会话，不保存）
  - 一日（保存到当日记录）
  - 一生（每次启动自动应用）
- **发型切换**：通常发型与发夹 A/B/C，遵循游戏当日规则（一天内保持、每日轮换）。贝雷帽遵循游戏默认（固定通常并锁定）；万圣节帽及未来没有专属发型的头部装饰会解锁，摘下有专属发型的装饰时恢复佩戴前的发型。
- **限定装饰常驻**，启动游戏与剧情结束后自动保持：
  - 魔法公主贝雷帽（联动剧情）
  - 万圣节帽子 / 万圣节 2026
  - 圣诞 2025（圣诞帽与圣诞物件）
  - 愚人节 2026（墨镜）
- 头部装饰会自动临时替换猫耳耳机；取消头部装饰后会恢复你原本保存的耳机（含猫耳耳机）。同部位（头部）装饰互斥，同时只能佩戴一个；限时活动装饰之间也互斥，但单顶帽子与愚人节墨镜可以同时使用。
- **最终话兼容**：主线第 32 话期间遵循游戏默认表现（不自动重戴、不覆盖强制发型），读完后自动恢复正常。
- **面向未来的自动适配**：新帽子从游戏的 `HatType` 枚举自动发现，新活动从场景自动发现；头部规则会在运行时自动识别（互斥 + 耳机替换），无需更新 Mod。
- **故障隔离**：每条补丁、每个 UI 区块独立注册。游戏更新导致某条补丁失效时，只有对应功能停止，其余功能照常工作。
- 界面三语（English / 中文 / 日本語）并支持游戏内即时切换语言，字体走游戏自身语言字体系统。

### 安装

**前置：** BepInEx 5.4.23.5+

- **首次安装（无 BepInEx）：** 从 [Releases](https://github.com/fragule-hub/Chill-with-You-Lo-Fi-Story---Change-of-Pace---Costume-Mod/releases) 下载完整包 `ChangeOfPaceCostume_Full_v1.7.7.zip`，将全部内容解压到游戏根目录（与 `Chill With You.exe` 同级）。
- **已有 BepInEx 或从旧版升级：** 下载 `ChangeOfPaceCostume.zip`，将 `ChangeOfPaceCostume.dll` 放入 `BepInEx\plugins\` 覆盖旧文件；原有设置自动沿用。

### 故障排查

若游戏内没有出现 Mod 功能：

1. 确认 `ChangeOfPaceCostume.dll` 位于 `BepInEx\plugins\`。
2. 正常关闭游戏，用记事本打开 `BepInEx\LogOutput.log`。
3. 搜索 `Change of Pace - Costume`。正常日志会有 `[... v1.7.7] loaded.` 与 `[Inject] Complete: ...`；若没有，请把这段日志（或整个文件）发给作者。

安装、卸载与常见问题见 `Mods/ChangeOfPaceCostume/InstallGuide.txt`。

### 从源码构建

打开 `Mods/ChangeOfPaceCostume/ChangeOfPaceCostume.csproj`——工程引用游戏托管程序集与 BepInEx（来自本地游戏目录）；按你的游戏路径修改 `.csproj` 中的 `GameDir`，然后执行 `dotnet build -c Release`。

### 许可

[MIT](LICENSE)

---

## 日本語

**Chill with You: Lo-Fi Story** の「気分転換」装飾パネルに衣装切替・髪型切替・限定装飾の常時装備を追加する BepInEx Mod です。

### 機能

- **衣装切替**、3 つのリクエストモード：
  - 一回（セッションのみ、保存しない）
  - 一日（当日記録に保存）
  - 一生（起動ごとに自動適用）
- **髪型切替**：通常とヘアピン A/B/C。ゲーム既定のルール（当日保持・毎日抽選）に従います。ベレー帽はゲーム既定（通常固定・切替ロック）、ハロウィンハットや専用髪型を持たない今後の頭装飾は解放され、専用髪型を外すと装着前の髪型へ戻ります。
- **限定装飾の常時装備**（起動・ストーリー後に自動維持）：
  - 魔法少女ベレー帽（コラボストーリー）
  - ハロウィンハット / ハロウィン2026
  - クリスマス2025（サンタ帽・クリスマスオブジェクト）
  - エイプリルフール2026（サングラス）
- 帽子系装飾は猫耳ヘッドホンを一時的に交換します。装飾を外すと保存されているヘッドホン（猫耳を含む）へ戻ります。同じ部位（頭）の装飾は互いに排他で、期間限定イベント装飾同士も排他です。単体の帽子とエイプリルフールのサングラスは同時に使えます。
- **最終話対応**：本編第 32 話の期間中はゲーム既定の演出に従い（自動再適用なし・髪型の上書きなし）、読了後に通常動作へ戻ります。
- **将来の装飾に自動対応**：新しい帽子は `HatType` 列挙から、新しいイベントはシーンから自動検出。帽子系ルール（排他・ヘッドホン交換）も実行時に自動判定するため、Mod の更新は不要です。
- **障害の分離**：パッチと UI セクションは個別に登録されます。ゲーム更新で 1 つのパッチが壊れても、その機能だけが停止し、他は動作を続けます。
- UI は三国語（English / 中文 / 日本語）対応、ゲーム内での言語切替にも追従し、フォントはゲームの言語フォントを使用。

### インストール

**前提：** BepInEx 5.4.23.5+

- **初回（BepInEx 未導入）：** [Releases](https://github.com/fragule-hub/Chill-with-You-Lo-Fi-Story---Change-of-Pace---Costume-Mod/releases) から完全版 `ChangeOfPaceCostume_Full_v1.7.7.zip` をダウンロードし、ゲームフォルダ（`Chill With You.exe` と同階層）へすべて展開。
- **BepInEx 導入済み・旧版からの更新：** `ChangeOfPaceCostume.zip` をダウンロードし、`ChangeOfPaceCostume.dll` を `BepInEx\plugins\` へ上書き配置。設定は自動で引き継がれます。

### トラブルシューティング

ゲーム内に Mod 機能が表示されない場合：

1. `ChangeOfPaceCostume.dll` が `BepInEx\plugins\` にあるか確認してください。
2. ゲームを正常終了し、`BepInEx\LogOutput.log` をメモ帳で開きます。
3. `Change of Pace - Costume` を検索します。正常なログには `[... v1.7.7] loaded.` と `[Inject] Complete: ...` があります。無い場合はその部分（またはファイル全体）を作者へ送ってください。

導入・アンインストール・FAQ は `Mods/ChangeOfPaceCostume/InstallGuide.txt` を参照。

### ソースからのビルド

`Mods/ChangeOfPaceCostume/ChangeOfPaceCostume.csproj` を開いてください。ゲームの Managed アセンブリと BepInEx をローカルのゲームフォルダから参照しています。`.csproj` の `GameDir` をゲームのインストール先に合わせ、`dotnet build -c Release` を実行します。

### ライセンス

[MIT](LICENSE)
