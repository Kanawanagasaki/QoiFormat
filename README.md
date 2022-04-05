# The Quite OK Image format

### How to use it?

##### What do i need to "using"?
```csharp
using System.Drawing;
using System.Drawing.Imaging;
using QoiFormat;
```

##### Encode System.Drawing.Bitmap
```csharp
var files = new DirectoryInfo(inputFolder).GetFiles();
foreach(var file in files)
{
    using var img = Image.FromFile(file.FullName);
    using var bitmap = new Bitmap(img);
    var bytes = QoiEncoder.Encode(bitmap);
    File.WriteAllBytes(@$"{outputFolder}\{file.Name}.qoi", bytes);
}
```

##### Decode System.Drawing.Bitmap
```csharp
var files = new DirectoryInfo(inputFolder).GetFiles();
foreach(var file in files)
{
    var bytes = File.ReadAllBytes(file.FullName);
    using var bitmap = QoiEncoder.Decode(bytes);
    bitmap.Save(@$"{outputFolder}\{file.Name}.png", ImageFormat.Png);
}
```

[website](https://qoiformat.org/) [specification](https://qoiformat.org/qoi-specification.pdf)
