# XNB Exporter Pro v2.0

**Улучшенный конвертер XNB → PNG/BMP/TGA**

Полностью автономное приложение — **НЕ требует** XNA Framework, MonoGame, DirectX или GPU.

[![Build](https://github.com/YOUR_USERNAME/XNBExporterPro/actions/workflows/build.yml/badge.svg)](https://github.com/YOUR_USERNAME/XNBExporterPro/actions/workflows/build.yml)

---

## 🚀 Возможности (улучшения по сравнению с оригиналом)

| Функция | Оригинал (v1.0) | XNB Exporter Pro (v2.0) |
|---------|-----------------|------------------------|
| Конвертация файлов | Только 1 файл | ✅ Пакетная (тысячи файлов) |
| Drag & Drop | ❌ | ✅ Файлы и папки |
| Предпросмотр | ❌ | ✅ С прозрачностью |
| Форматы вывода | Только PNG | ✅ PNG, BMP, TGA |
| Поверхностные форматы | Только Color | ✅ Color, DXT1, DXT3, DXT5, RGB565, BGRA5551, BGRA4444, Alpha8 |
| Сжатые XNB | ❌ | ✅ LZX и LZ4 |
| Командная строка | ❌ | ✅ CLI режим |
| Зависимости | XNA + DirectX + GPU | ✅ Только .NET |
| Рекурсивный поиск | ❌ | ✅ По подпапкам |
| Прогресс | ❌ | ✅ Полоса прогресса |
| Тёмная тема | ❌ | ✅ Современный UI |

---

## 📥 Скачать EXE

1. Перейдите в **[Actions](../../actions)** → выберите последний успешный билд
2. Скачайте артефакт **XNBExporterPro-win-x64**
3. Или: перейдите в **[Releases](../../releases)** для стабильных версий

---

## 🔨 Сборка через GitHub (автоматически!)

### Шаг 1: Создайте репозиторий
```bash
# Создайте новый репозиторий на GitHub, затем:
cd XNBExporterPro
git init
git add .
git commit -m "Initial commit - XNB Exporter Pro v2.0"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/XNBExporterPro.git
git push -u origin main
```

### Шаг 2: Подождите сборку
- GitHub Actions **автоматически** скомпилирует EXE
- Зайдите во вкладку **Actions** → увидите процесс сборки
- После завершения скачайте готовый **XNBExporterPro.exe**

### Шаг 3: Создание Release (для стабильной версии)
```bash
git tag v2.0.0
git push origin v2.0.0
```
GitHub Actions автоматически создаст Release с EXE файлом!

---

## 🎮 Использование

### GUI режим (двойной клик)
1. Запустите `XNBExporterPro.exe`
2. Перетащите XNB файлы/папки в окно
3. Или: File → Open Files / Open Folder
4. Выберите формат вывода (PNG/BMP/TGA)
5. Нажмите "▶ Convert All" или "▷ Convert Selected"

### CLI режим
```bash
# Конвертация одного файла
XNBExporterPro.exe texture.xnb

# Конвертация папки рекурсивно
XNBExporterPro.exe -r ./Content/

# Конвертация в BMP с выходной папкой
XNBExporterPro.exe -f BMP -o ./output/ *.xnb

# Показать справку
XNBExporterPro.exe --help
```

---

## 🔧 Поддерживаемые форматы

### Входные (XNB):
- **Texture2D** текстуры
- SurfaceFormat: Color, DXT1, DXT3, DXT5, RGB565, BGRA5551, BGRA4444, Alpha8
- Сжатие: LZX, LZ4, несжатые
- Платформы: Windows, Xbox, Phone, Android, iOS
- Версии XNB: 4, 5

### Выходные:
- **PNG** — с полной прозрачностью (alpha)
- **BMP** — 32-bit BGRA
- **TGA** — 32-bit true color

---

## 📋 Структура проекта

```
XNBExporterPro/
├── .github/
│   └── workflows/
│       └── build.yml        ← GitHub Actions (автосборка)
├── src/
│   ├── Program.cs           — Точка входа (GUI + CLI)
│   ├── MainForm.cs          — Главное окно GUI
│   ├── XnbReader.cs         — Парсер XNB файлов
│   ├── DxtDecoder.cs        — Декомпрессия DXT1/3/5
│   ├── LzxDecompressor.cs   — LZX/LZ4 декомпрессия
│   ├── ImageWriter.cs       — Запись PNG/BMP/TGA
│   ├── XNBExporterPro.csproj
│   ├── build.bat            — Локальная сборка (Windows)
│   └── build.ps1            — Локальная сборка (PowerShell)
├── .gitignore
├── XNBExporterPro.sln
└── README.md
```

---

## ⚡ Совместимость

Работает с XNB файлами из:
- **Terraria**
- **Stardew Valley**
- **Celeste**
- **FEZ**
- **Hacknet**
- Любые другие XNA/MonoGame/FNA игры

---

## 📄 Лицензия

MIT License

Основан на [XNBExporter](https://github.com/mediaexplorer74/XNBExporter) by mediaexplorer74.  
LZX декомпрессор основан на MonoGame (LGPL 2.1 / MS-PL).
