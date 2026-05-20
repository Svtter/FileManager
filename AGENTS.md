# FileManager

本地文件管理器，基于 WinForms (.NET 8)。

## 技术栈

- .NET 8 WinForms
- WebView2 (PDF 预览)
- Shell32 API (系统文件图标)

## 功能

- 左侧目录树导航，按需加载子目录
- 右侧文件列表（名称/大小/类型/修改日期）
- 图片预览：JPG, PNG, BMP, GIF, WebP, TIFF
- PDF 预览：通过 WebView2 (Edge 内核) 渲染
- 导航：后退/前进/上级/地址栏输入
- 双击文件夹进入，双击文件用系统默认程序打开

## 构建

```bash
dotnet build
```

## 运行

```bash
dotnet run
```
