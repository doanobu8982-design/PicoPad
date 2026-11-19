# コントローラー画像アセット出典・ライセンス情報 (SOURCES.md)

本ディレクトリに配置されているコントローラー画像アセット（正面ビューおよび上面ビュー）の取得元、ライセンス、フォーマット詳細を記録するドキュメントです。

---

## 1. アセット一覧

| ファイル名 | 視点 / 内容 | フォーマット | 解像度 / ViewBox | ファイルサイズ |
| :--- | :--- | :--- | :--- | :--- |
| `controller_front.svg` | 正面ビュー（ABXY、十字キー、左右スティック、View/Menu、Guide） | SVG (ベクター) | viewBox: `0 140 1543.24 816.31` | 約 98 KB |
| `controller_top.svg` | 上面ビュー（LB/RBバンパー、LT/RTトリガー、ペアリングボタン） | SVG (ベクター) | viewBox: `150 0 1243.24 230` | 約 95 KB |
| `controller_full_layout.svg` | 全体ビュー（正面＋上面の完全版ベクター原典） | SVG (ベクター) | viewBox: `0 0 1543.24 956.31` | 約 184 KB |
| `controller_front.png` | 正面ビュー（透過背景・公式XB1_OG_base基準） | 透過 PNG | 1543 × 956 px | 約 177 KB |
| `controller_top.png` | 上面ビュー（透過背景） | 透過 PNG | 1243 × 230 px (幅500px以上) | 約 30 KB |
| `controller_full.png` | 全体ビュー（透過背景・原典画像） | 透過 PNG | 1543 × 956 px | 約 207 KB |

---

## 2. 取得元情報 (Sources)

* **プロジェクト名**: Gamepad-Asset-Pack / VSCView Gamepad Controller Theme
* **リポジトリURL**: [https://github.com/AL2009man/Gamepad-Asset-Pack](https://github.com/AL2009man/Gamepad-Asset-Pack)
* **原典ファイル**:
  * SVG: `Controller Asset Pack/Xbox Wireless Controller Images/Default Theme/Theme SVG/Xbox One Color/Xbox One Controller VSCView Black.svg`
  * PNG: `Controller Asset Pack/Xbox Wireless Controller Images/Default Theme/Template/Xbox One OG Controller/Black/Xbox One OG Controller Overlay.png`
* **作者 / 権利者**: AL2009man, Nielk1 (VSCView Project)

---

## 3. ライセンス (License)

* **ライセンス種別**: **MIT License**
* **利用条件**: 商用利用・私的利用・改変・再配布が許可されています。著作権表示および許諾表示を保持することで自由に使用可能です。

### ライセンス全文
```text
MIT License

Copyright (c) 2021 AL2009man

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 4. 素材のデザイン統一性と適合性

1. **同一シリーズ**: 正面ビューおよび上面ビューは、オープンソースの配信・入力可視化ツール「VSCView」用にAL2009man氏が一体的に制作した同一のアセットパックから取得・生成しています。
2. **デザインテイストの統一**:
   * **カラーリング**: マットブラック基調（ダークグレーの外郭線、濃灰のボタン・レバー、薄灰色のテキストラベル）。
   * **線の太さ・質感**: ベクター線画＋フラットデザインが完璧に一致。
   * **ラベル表記**: LT/RT（トリガー）、LB/RB（バンパー）、ABXY、十字キーのフォントとスタイルが揃っています。
3. **柔軟なフォーマット対応**:
   * WPFのベクター描画（SVG / XAML Path）に対応するSVGファイル（最優先要件）に加え、フォールバックや軽量プレビューとして直接Imageコントロールでバインド可能な高解像度透過PNG（1543px幅）を両方揃えています。

---

## 5. 個別ボタンパーツアセット (`Buttons/`)

UI上での直接発光・状態切り替え用に配置された、通常時（`_normal.png`）および押下発光時（`_pressed.png`）の個別透過PNGパーツ画像です（全17種・34ファイル）。

### 5.1. 取得元情報
* **プロジェクト名**: Gamepad-Asset-Pack (Xbox Wireless Controller / Default Theme)
* **リポジトリURL**: [https://github.com/AL2009man/Gamepad-Asset-Pack](https://github.com/AL2009man/Gamepad-Asset-Pack)
* **原典パス**:
  * 押下発光時: `Controller Asset Pack/Xbox Wireless Controller Images/Default Theme/Theme Assets/Xbox One Active Presses/`
  * 通常時: `Xbox One Controller VSCView Black.svg` および `controller_front.png` のベクター座標から同一シルエットで生成
* **ライセンス**: **MIT License**（上記原典と同一）

### 5.2. ボタンパーツ一覧（全17組・34ファイル）

| ボタン種別 | 通常時（未押下） | 押下発光時（アクティブ） | 解像度 | 状態表現 |
| :--- | :--- | :--- | :--- | :--- |
| **A ボタン** | `btn_a_normal.png` | `btn_a_pressed.png` | 86 × 80 px | マットダーク / ネオンシアン発光 |
| **B ボタン** | `btn_b_normal.png` | `btn_b_pressed.png` | 86 × 81 px | マットダーク / ネオンシアン発光 |
| **X ボタン** | `btn_x_normal.png` | `btn_x_pressed.png` | 90 × 82 px | マットダーク / ネオンシアン発光 |
| **Y ボタン** | `btn_y_normal.png` | `btn_y_pressed.png` | 89 × 83 px | マットダーク / ネオンシアン発光 |
| **十字キー 上** | `dpad_up_normal.png` | `dpad_up_pressed.png` | 99 × 100 px | ダーククロス / ネオンシアン発光 |
| **十字キー 下** | `dpad_down_normal.png` | `dpad_down_pressed.png` | 97 × 104 px | ダーククロス / ネオンシアン発光 |
| **十字キー 左** | `dpad_left_normal.png` | `dpad_left_pressed.png` | 127 × 89 px | ダーククロス / ネオンシアン発光 |
| **十字キー 右** | `dpad_right_normal.png` | `dpad_right_pressed.png` | 124 × 88 px | ダーククロス / ネオンシアン発光 |
| **LB（左バンパー）** | `btn_lb_normal.png` | `btn_lb_pressed.png` | 272 × 105 px | ショルダーカーブ / ネオンシアン発光 |
| **RB（右バンパー）** | `btn_rb_normal.png` | `btn_rb_pressed.png` | 274 × 105 px | ショルダーカーブ / ネオンシアン発光 |
| **LT（左トリガー）** | `btn_lt_normal.png` | `btn_lt_pressed.png` | 151 × 139 px | ダークトリガー / ネオンシアン発光 |
| **RT（右トリガー）** | `btn_rt_normal.png` | `btn_rt_pressed.png` | 153 × 139 px | ダークトリガー / ネオンシアン発光 |
| **L3（左スティック）** | `stick_l3_normal.png` | `stick_l3_pressed.png` | 150 × 141 px | スティックヘッド / ネオンシアン発光 |
| **R3（右スティック）** | `stick_r3_normal.png` | `stick_r3_pressed.png` | 152 × 144 px | スティックヘッド / ネオンシアン発光 |
| **Back（View）** | `btn_back_normal.png` | `btn_back_pressed.png` | 58 × 54 px | 二重四角アイコン / ネオンシアン発光 |
| **Start（Menu）** | `btn_start_normal.png` | `btn_start_pressed.png` | 58 × 54 px | ハンバーガーアイコン / ネオンシアン発光 |
| **Guide（Xbox Home）** | `btn_guide_normal.png` | `btn_guide_pressed.png` | 95 × 90 px | ホワイトエンブレム / ネオンシアン発光 |


