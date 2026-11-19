# PicoPad

> **Windows キーボード入力を Raspberry Pi Pico（RP2040）経由で XInput ゲームパッドとして出力する、ハードウェア中継型ゲームパッドエミュレーター**

[![Release](https://img.shields.io/github/v/release/doanobu8982-design/PicoPad?label=Release)](https://github.com/doanobu8982-design/PicoPad/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

PicoPad は、格闘ゲームやアクションゲーム向けに設計された低遅延キーボード→ゲームパッド変換システムです。PC 側のソフト（`PicoPad.exe`）で Raw Input によるキー入力を取得し、USB シリアル経由で Raspberry Pi Pico へ送信することで、OS やゲーム側からは純粋な **Xbox 360 (XInput) ハードウェアコントローラー** として認識されます。

---

## 1. 主な特徴

* **2モード・ステルス設計**:
  * **通常モード（待機中）**: Pico が CDC シリアルポートとして待機し、ファームウェアの書き換えや設定待機を行います。
  * **ゲームモード（稼働中）**: XInput デバイス（Xbox 360 Controller）＋ 通信用インターフェースとして動作し、ゲーム側の入力競合や仮想ドライバ制限を回避します。
* **CPT 準拠ニュートラル SOCD クリーナー**:
  * 左右同時押し（`Left + Right`）および上下同時押し（`Up + Down`）をすべてニュートラル（未入力）へ調停します。
* **柔軟なプロファイル管理**:
  * タイトル別にプロファイルを作成・複製・リネーム・削除でき、D-Pad 出力 / 左スティック出力の切り替え（`IsStickMode`）にも対応しています。
* **複数キー同時割り当て・1キー複数ボタン割り当て**:
  * 1つのゲームパッドボタンに複数のキー（例: デフォルトの `Up` に `W` と `Space`）を登録でき、同じキーを複数ボタンへ割り当てることも可能です。
* **直感的なビジュアルキーコンフィグ**:
  * コントローラー画像上のボタン配置を見ながら、クリック操作だけでキーバインドの登録・解除・リアルタイム入力確認ができます。

---

## 2. 必要な環境

| 項目 | 要件 |
| :--- | :--- |
| **OS** | Windows 10 / Windows 11 (64bit / x64) |
| **ハードウェア** | Raspberry Pi Pico（RP2040）および USB ケーブル |
| **ランタイム** | **不要**（GitHub Releases 配布の ZIP 版は自己完結型のため、.NET ランタイムの事前インストールなしで動作します） |

---

## 3. セットアップ＆使い方（クイックスタート）

### Step 1: 配布 ZIP のダウンロードと展開
1. [GitHub Releases](https://github.com/doanobu8982-design/PicoPad/releases) から最新版の `PicoPad_vX.Y.Z.zip` をダウンロードします。
2. 任意のフォルダへ ZIP ファイルを解凍します（解凍先には `PicoPad.exe` と `gamepad_firmware.uf2` が同梱されています）。

### Step 2: Raspberry Pi Pico へのファームウェア書き込み
1. Raspberry Pi Pico 本体の **BOOTSEL ボタン** を押しながら、PC と USB ケーブルで接続します。
2. PC 上にリムーバブルドライブ **`RPI-RP2`** がマウントされたら、BOOTSEL ボタンを離します。
3. 同梱されている **`gamepad_firmware.uf2`** を `RPI-RP2` ドライブへドラッグ＆ドロップでコピーします。書き込み完了後、Pico は自動的に再起動します。

### Step 3: `PicoPad.exe` の起動と設定
1. 解凍したフォルダ内の `PicoPad.exe` を起動します。
   > [!NOTE]
   > Raw Input の安定取得および UIPI（User Interface Privilege Isolation）による入力遮断を回避するため、起動時に **管理者権限への昇格（UAC）** が要求されます。
2. 初回起動時は同じフォルダにデフォルト設定の `profiles.json` が自動生成されます（手動で用意する必要はありません）。
3. **キーバインド登録**:
   * 画面上の割り当てたいボタンを **左クリック** して待機状態（枠が金色表示）にし、割り当てたいキーボードのキーを押します。
   * 登録をクリアしたい場合は、対象ボタンを **右クリック** するか、待機中に **`Esc`** キーを押します。
4. **ゲームモードへの切り替え**:
   * Pico が接続されると、ボタン表示が「通常モード (待機中) → クリックでゲームモード起動」に変わります。クリックするとゲームモードへ移行し、XInput コントローラーとして動作します。

---

## 4. ソースからのビルド方法

### PC アプリ（`pc_app/`）
* **必要ツール**: [.NET 10 SDK](https://dotnet.microsoft.com/)（Windows x64）

```powershell
# 単体テストの実行（実機 Pico 未接続でも実行可能）
dotnet test pc_app/tests/GamepadEmulator.Tests.csproj -c Release

# 自己完結型ビルド（win-x64）
dotnet publish pc_app/src/GamepadEmulator.csproj -c Release -r win-x64 --self-contained true -o dist/publish/win-x64
```

### ファームウェア（`firmware/`）
* **必要ツール**: [Raspberry Pi Pico SDK 2.3.1](https://github.com/raspberrypi/pico-sdk)（TinyUSB サブモジュール含む）、CMake 3.13 以上、Ninja、Arm GNU Toolchain（`arm-none-eabi-gcc`）、`picotool`

```bash
cmake -S firmware -B firmware/build -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DPICO_BOARD=pico \
  -DPICO_PLATFORM=rp2040
cmake --build firmware/build -j
```
ビルドが完了すると `firmware/build/gamepad_firmware.uf2` が生成されます。

---

## 5. 注意事項・免責事項

* オンラインゲームや競技タイトル等での使用にあたっては、各タイトルの利用規約・大会ルールをご確認のうえ、**ご自身の責任においてご利用ください**。
* 本ソフトウェアおよびファームウェアの使用によって生じたいかなる損害についても、作者は責任を負いかねます。

---

## 6. ライセンスおよびサードパーティクレジット

* **本プロジェクト**: [MIT License](LICENSE)（`Copyright (c) 2026 doanobu8982-design`）
* **コントローラー画像アセット**:
  * 本アプリの UI に使用しているコントローラーおよびボタン画像アセットは、[AL2009man/Gamepad-Asset-Pack](https://github.com/AL2009man/Gamepad-Asset-Pack)（`AL2009man, Nielk1 / VSCView Project`、MIT License、`Copyright (c) 2021 AL2009man`）を使用しています。
  * 詳細は同梱の [SOURCES.md](pc_app/src/Resources/Images/SOURCES.md) をご参照ください。
