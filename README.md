# Chill with You: Lo-Fi Story - Change of Pace - Costume Mod

[English](#english) | [中文](#中文) | [日本語](#日本語)

---

## English

A BepInEx mod that adds costume switching to the "Change of Pace" decoration panel in **Chill with You: Lo-Fi Story**.

### Features

**v1.0 - Costume Switching**
- Adds a "Costume" section to the decoration panel
- One-click costume switching with visual feedback (active costume highlighted in blue)
- Auto-discovers all costume enum values (supports future game updates)

**v1.1 - Costume Request Options**
- Three persistence modes for costume changes:
  - **"Just this once~"**: Applies only for this session, not saved
  - **"Just today~"**: Saves to the game's daily costume change record
  - **"Forever?!"**: Saved permanently via PlayerPrefs, auto-applied on every launch
- Trilingual support: English, 中文, 日本語

### Installation

**Prerequisites:**
- BepInEx 5.4.23.5 or higher

**Steps:**
1. Download `ChangeOfPaceCostume.zip` from [Releases](https://github.com/fragule-hub/Chill-with-You-Lo-Fi-Story---Change-of-Pace---Costume-Mod/releases)
2. Extract to get `ChangeOfPaceCostume.dll`
3. Place `ChangeOfPaceCostume.dll` into `BepInEx\plugins\` folder
4. Launch the game

**Verification:**
- Open `BepInEx\LogOutput.log` and confirm: `Change of Pace - Costume loaded.`
- Click the "Change of Pace" button, scroll to the bottom to see the Costume section

### Notes

- The mod only modifies runtime state, it does not affect core save data
- The "Forever?!" option persists via PlayerPrefs and survives mod uninstallation
- If game updates add new costume enum values, the mod auto-discovers them

---

## 中文

一个 BepInEx Mod，为 **Chill with You: Lo-Fi Story** 游戏的"转转你的"装饰面板添加服装切换功能。

### 功能特性

**v1.0 - 服装切换**
- 在装饰面板中添加"Costume"栏目
- 一键切换服装，带视觉反馈（当前服装高亮显示为蓝色）
- 自动发现所有服装枚举值（支持未来游戏更新）

**v1.1 - 换装请求选项**
- 三种换装持久化模式：
  - **"只这一次哦~"**：仅本次会话生效，不保存
  - **"只有今天哦~"**：保存到游戏当日换装记录
  - **"一辈子？！"**：永久保存，每次启动自动应用
- 三语支持：English、中文、日本語

### 安装说明

**前提条件：**
- BepInEx 5.4.23.5 或更高版本

**安装步骤：**
1. 从 [Releases](https://github.com/fragule-hub/Chill-with-You-Lo-Fi-Story---Change-of-Pace---Costume-Mod/releases) 下载 `ChangeOfPaceCostume.zip`
2. 解压得到 `ChangeOfPaceCostume.dll`
3. 将 `ChangeOfPaceCostume.dll` 放入 `BepInEx\plugins\` 文件夹
4. 启动游戏

**验证：**
- 打开 `BepInEx\LogOutput.log`，确认看到：`Change of Pace - Costume loaded.`
- 点击"转转你的"按钮，滚动到底部查看 Costume 服装栏目

### 说明

- Mod 仅修改运行时状态，不影响游戏存档核心数据
- "一辈子？！"选项通过 PlayerPrefs 保存，卸载 Mod 后数据仍保留
- 游戏更新后如果新增服装，Mod 会自动发现并显示

---

## 日本語

**Chill with You: Lo-Fi Story** の「転々あなたの」装飾パネルにコスチューム切替機能を追加する BepInEx Mod です。

### 機能

**v1.0 - コスチューム切替**
- 装飾パネルに「Costume」セクションを追加
- ワンクリックでコスチューム切替、ビジュアルフィードバック付き（現在のコスチュームを青色でハイライト）
- すべてのコスチューム列挙値を自動検出（今後のゲームアップデートに対応）

**v1.1 - コスチュームリクエストオプション**
- コスチューム変更の3つの永続化モード：
  - **「今回だけね~」**：今回のセッションのみ適用、保存しない
  - **「今日だけね~」**：ゲームの当日コスチューム変更記録に保存
  - **「一生？！」**：PlayerPrefs で永続保存、毎回起動時に自動適用
- 3か国語対応：English、中文、日本語

### インストール方法

**前提条件：**
- BepInEx 5.4.23.5 以上が必要です

**手順：**
1. [Releases](https://github.com/fragule-hub/Chill-with-You-Lo-Fi-Story---Change-of-Pace---Costume-Mod/releases) から `ChangeOfPaceCostume.zip` をダウンロード
2. 解凍して `ChangeOfPaceCostume.dll` を取得
3. `ChangeOfPaceCostume.dll` を `BepInEx\plugins\` フォルダに配置
4. ゲームを起動

**確認方法：**
- `BepInEx\LogOutput.log` を開き、次のメッセージを確認：`Change of Pace - Costume loaded.`
- 「転々あなたの」ボタンをクリックし、一番下にスクロールして Costume セクションを表示

### 補足

- Mod はランタイム状態のみを変更し、コアセーブデータには影響しません
- 「一生？！」オプションは PlayerPrefs で保存され、Mod をアンインストール後もデータが残ります
- ゲーム更新で新しいコスチュームが追加された場合、Mod が自動検出して表示します

---

## License

MIT License - See [LICENSE](LICENSE) for details.
