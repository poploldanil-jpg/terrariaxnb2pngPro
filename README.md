# XNB Exporter Pro v2.0

**Улучшенный конвертер XNB → PNG/BMP/TGA**

Полностью автономное приложение — **НЕ требует** XNA Framework, MonoGame, DirectX или GPU.

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
| Зависимости | XNA + DirectX + GPU | ✅ Только .NET Framework |
| Рекурсивный поиск | ❌ | ✅ По подпапкам |
| Прогресс | ❌ | ✅ Полоса прогресса |
| Тёмная тема | ❌ | ✅ Современный UI |

---

## 📦 Сборка (Build)

### Вариант 1: Visual Studio
1. Откройте `XNBExporterPro.sln` в Visual Studio 2019/2022
2. Установите целевой framework: .NET Framework 4.7.2
3. Build → Build Solution (Ctrl+Shift+B)
4. Результат: `src/bin/Release/XNBExporterPro.exe`

### Вариант 2: Командная строка (Developer Command Prompt)
```batch
cd src
build.bat
```

### Вариант 3: dotnet CLI
```bash
cd src
dotnet build -c Release
```

---

## 🎮 Использование

### GUI режим (двойной клик)
1. Запустите `XNBExporterPro.exe`
2. Перетащите XNB файлы/папки в окно
3. Или: File → Open Files / Open Folder
4. Выберите формат вывода (PNG/BMP/TGA)
5. Нажмите "Convert All" или "Convert Selected"

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
├── src/
│   ├── Program.cs          — Точка входа (GUI + CLI)
│   ├── MainForm.cs         — Главное окно GUI
│   ├── XnbReader.cs        — Парсер XNB файлов
│   ├── DxtDecoder.cs       — Декомпрессия DXT1/3/5
│   ├── LzxDecompressor.cs  — LZX декомпрессия
│   ├── ImageWriter.cs      — Запись PNG/BMP/TGA
│   ├── XNBExporterPro.csproj
│   └── build.bat
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
- Любые другие XNA/MonoGame/FNA игры

---

## 📄 Лицензия

MIT License

Основан на [XNBExporter](https://github.com/mediaexplorer74/XNBExporter) by mediaexplorer74.
LZX декомпрессор основан на MonoGame (LGPL 2.1 / MS-PL).
